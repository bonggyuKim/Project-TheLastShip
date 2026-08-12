using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 상시 HUD 가 <b>프리팹에서 자리를 받는가</b>.
    ///
    /// 예전에는 아이콘 크기·여백·간격이 코드 상수였고 매 프레임 <c>Rect</c> 를 계산해 얹었다.
    /// 그러면 아이콘을 조금 옮기는 일이 코드 수정과 재컴파일이 되고, 에디터에서 드래그로
    /// 맞출 수가 없다 — 사용자가 "이러면 에디터에서 수정을 못 하잖아" 로 지적한 그 상태다.
    ///
    /// <b>가장 중요한 검사는 마지막 것이다.</b> 프리팹이 있는 것만으로는 부족하고, 런타임이
    /// 그 자리를 <b>안 덮어야</b> 편집이 실제로 살아남는다. 자리를 다시 잡는 코드가 한 줄이라도
    /// 돌아오면 "저장은 되는데 게임에서는 안 보이는" 조용한 실패가 된다.
    /// </summary>
    public sealed class LastShiftHudPrefabTests
    {
        private const string PrefabPath = "Assets/DoodleUp/Resources/LastShiftHud.prefab";

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private static GameObject LoadPrefab() => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        /// <summary>프리팹이 실제로 있고 세 아이콘이 다 꽂혀 있다.</summary>
        [Test]
        public void TheHudPrefabExistsAndIsWired()
        {
            var prefab = LoadPrefab();
            Assert.That(prefab, Is.Not.Null,
                $"{PrefabPath} 가 없다 — Last Shift/UI/Build HUD Prefab 으로 한 번 굽는다");

            var view = prefab.GetComponent<LastShiftHudView>();
            Assert.That(view, Is.Not.Null, "루트에 LastShiftHudView 가 없다");
            Assert.That(view.IsWired, Is.True,
                "아이콘 참조가 비어 있다 — 하나라도 비면 그 계통이 화면에서 사라진다");
        }

        /// <summary>
        /// <b>자리가 프리팹 안에 있다.</b> 앵커가 우측 상단이어야 해상도가 달라도 오른쪽에
        /// 붙고, 런타임이 화면 폭을 읽어 자리를 다시 계산할 이유가 없어진다.
        /// </summary>
        [Test]
        public void TheLayoutLivesInTheRectTransforms()
        {
            var view = LoadPrefab().GetComponent<LastShiftHudView>();

            foreach (var icon in new[] { LastShiftUiIcon.Oxygen, LastShiftUiIcon.Power, LastShiftUiIcon.Heat })
            {
                var rect = (RectTransform)view.GaugeOf(icon).transform;

                Assert.That(rect.anchorMin, Is.EqualTo(Vector2.one), $"{icon} 앵커가 우측 상단이 아니다");
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one), $"{icon} 앵커가 우측 상단이 아니다");
                Assert.That(rect.sizeDelta.x, Is.GreaterThan(0f), $"{icon} 크기가 0 이다");
                Assert.That(rect.sizeDelta.y, Is.GreaterThan(0f), $"{icon} 크기가 0 이다");
                Assert.That(rect.anchoredPosition, Is.Not.EqualTo(Vector2.zero),
                    $"{icon} 이 앵커 원점에 그대로 있다 — 자리가 프리팹에 안 들어갔다");
            }
        }

        /// <summary>세 아이콘이 서로 겹치지 않는다. 겹치면 하나가 다른 하나를 가린다.</summary>
        [Test]
        public void TheThreeIconsDoNotOverlap()
        {
            var view = LoadPrefab().GetComponent<LastShiftHudView>();
            var seen = new List<Rect>();

            foreach (var icon in new[] { LastShiftUiIcon.Oxygen, LastShiftUiIcon.Power, LastShiftUiIcon.Heat })
            {
                var rect = (RectTransform)view.GaugeOf(icon).transform;
                // 피벗이 우상단이라 앵커 좌표에서 왼쪽·아래로 크기만큼 뻗는다.
                var box = new Rect(
                    rect.anchoredPosition.x - rect.sizeDelta.x,
                    rect.anchoredPosition.y - rect.sizeDelta.y,
                    rect.sizeDelta.x, rect.sizeDelta.y);

                foreach (var other in seen)
                    Assert.That(box.Overlaps(other), Is.False, $"{icon} 이 다른 아이콘과 겹친다");
                seen.Add(box);
            }
        }

        /// <summary>
        /// <b>런타임이 프리팹 자리를 안 덮는다 — 이 검사가 완료 기준이다.</b>
        ///
        /// 에디터에서 아이콘을 끌어 옮긴 상황을 흉내 내고(좌표를 직접 바꾼다), 값 갱신 경로를
        /// 여러 번 태운 뒤에도 그 자리가 그대로인지 본다. 자리를 다시 잡는 코드가 한 줄이라도
        /// 돌아오면 여기서 붉어진다.
        /// </summary>
        [Test]
        public void UpdatingValuesNeverMovesTheIcons()
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab());
            spawned.Add(instance);
            var view = instance.GetComponent<LastShiftHudView>();

            // 에디터에서 끌어 옮긴 셈 친다.
            var moved = new Vector2(-321f, -654f);
            var target = (RectTransform)view.GaugeOf(LastShiftUiIcon.Power).transform;
            target.anchoredPosition = moved;
            var size = target.sizeDelta;

            for (var i = 0; i < 5; i++)
            {
                view.Set(LastShiftUiIcon.Oxygen, 0.8f, Color.white);
                view.Set(LastShiftUiIcon.Power, i * 0.2f, Color.red);
                view.Set(LastShiftUiIcon.Heat, 0.3f, Color.green);
            }

            Assert.That(target.anchoredPosition, Is.EqualTo(moved),
                "값을 갱신했더니 아이콘이 제자리로 끌려갔다 — 런타임이 프리팹 자리를 덮는다");
            Assert.That(target.sizeDelta, Is.EqualTo(size), "크기도 덮였다");
        }

        /// <summary>값 갱신은 실제로 채움에 들어간다 — 자리를 안 건드린다고 값도 안 들어가면 안 된다.</summary>
        [Test]
        public void UpdatingValuesStillFillsTheIcon()
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab());
            spawned.Add(instance);
            var view = instance.GetComponent<LastShiftHudView>();

            view.Set(LastShiftUiIcon.Heat, 0.42f, Color.cyan);
            var gauge = view.GaugeOf(LastShiftUiIcon.Heat);

            Assert.That(gauge.Value, Is.EqualTo(0.42f).Within(0.001f), "값이 안 들어갔다");
            Assert.That(gauge.Fill.color, Is.EqualTo(Color.cyan), "색이 안 들어갔다");
        }

        /// <summary>
        /// 아이콘 전용 규격 — 숫자·이름 줄이 꺼져 있다(아트 규격). 프리팹에 구운 상태 그대로여야
        /// 런타임이 매 프레임 끄러 갈 필요가 없다.
        /// </summary>
        [Test]
        public void TheBakedIconsCarryNoLabels()
        {
            var view = LoadPrefab().GetComponent<LastShiftHudView>();

            foreach (var icon in new[] { LastShiftUiIcon.Oxygen, LastShiftUiIcon.Power, LastShiftUiIcon.Heat })
            {
                var gauge = view.GaugeOf(icon);
                Assert.That(gauge.ValueLabel == null || !gauge.ValueLabel.gameObject.activeSelf, Is.True,
                    $"{icon} 에 숫자 줄이 켜져 있다");
            }
        }
    }
}
