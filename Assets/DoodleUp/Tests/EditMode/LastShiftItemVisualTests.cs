using DoodleUp.Editor;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 부품 프리팹이 프리미티브 상자에서 실물 모델로 넘어간 자리를 지킨다. 여기서 보는 것은
    /// "잘 생겼는가" 가 아니라 <b>갈아 끼우면서 게임 규칙이 조용히 바뀌지 않았는가</b> 다 —
    /// 판정 상자와 부피 판정은 루트 스케일에 걸려 있고, 실물은 그 상자 안에 들어가야 한다.
    /// </summary>
    public sealed class LastShiftItemVisualTests
    {
        private const string CanisterPrefabPath = "Assets/DoodleUp/Prefabs/LastShiftItem_CoolingCanister.prefab";

        private static GameObject LoadCanister()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanisterPrefabPath);
            Assert.That(prefab, Is.Not.Null, CanisterPrefabPath);
            return prefab;
        }

        [Test]
        public void CoolingCanisterUsesProductionMeshInsteadOfPrimitiveBox()
        {
            var prefab = LoadCanister();
            Assert.That(prefab.GetComponent<MeshFilter>(), Is.Null,
                "루트에 프리미티브 메시가 남아 있으면 실물 안에 상자가 겹쳐 보인다.");

            var visual = prefab.transform.Find(LastShiftSceneBuilder.ItemVisualName);
            Assert.That(visual, Is.Not.Null, "실물은 Visual 자식으로 들어간다.");
            Assert.That(visual.GetComponentsInChildren<MeshRenderer>(true), Is.Not.Empty,
                "Visual 안에 렌더러가 없다 — FBX 임포트가 비었다.");

            var source = PrefabUtility.GetCorrespondingObjectFromSource(visual.gameObject);
            Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo(LastShiftSceneBuilder.CoolingCanisterModelPath),
                "Visual 은 Blender 로 만든 냉각통 FBX 여야 한다.");
        }

        /// <summary>
        /// 부피 판정(<see cref="LastShiftPlayerController.IsBulky"/>)이 루트 스케일을 읽으므로,
        /// 모델 교체가 루트 스케일을 건드리면 이동 속도 규칙이 말없이 바뀐다.
        /// </summary>
        [Test]
        public void CoolingCanisterKeepsGameplayBoxAndBulkyRule()
        {
            var prefab = LoadCanister();
            var expected = new Vector3(0.55f, 1.1f, 0.55f);
            Assert.That(prefab.transform.localScale.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(prefab.transform.localScale.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(prefab.transform.localScale.z, Is.EqualTo(expected.z).Within(0.001f));

            var box = prefab.GetComponent<BoxCollider>();
            Assert.That(box, Is.Not.Null, "잡기·충돌 판정 상자가 사라졌다.");
            Assert.That((box.size - Vector3.one).magnitude, Is.LessThan(0.001f), $"판정 상자 크기 {box.size:F3}");
            Assert.That(box.center.magnitude, Is.LessThan(0.001f), $"판정 상자 중심 {box.center:F3}");

            Assert.That(LastShiftPlayerController.IsBulky(prefab.GetComponent<LastShiftGrabbable>()), Is.True,
                "냉각통은 여전히 부피 큰 물건이어야 한다 — 들면 느려지는 규칙이 여기 걸려 있다.");
        }

        /// <summary>
        /// 실물이 세워져 있고 판정 상자 밖으로 튀지 않는가. 씬을 눈으로 보지 않고 좌표로 본다.
        /// FBX 축 변환이 잘못 들어오면 통이 누워 들어오는데, 그때 가장 긴 변이 y 가 아니게 된다.
        /// </summary>
        [Test]
        public void CoolingCanisterVisualStandsUprightInsideItsBox()
        {
            var prefab = LoadCanister();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                var visual = instance.transform.Find(LastShiftSceneBuilder.ItemVisualName);
                var bounds = LastShiftSceneBuilder.CombinedRendererBounds(visual.gameObject);

                // 제작 문서의 1.07m 는 원점부터 손잡이 끝까지고, 메시가 실제로 차지하는 높이는
                // 발 링 아랫면(0.07m)부터 재는 1.00m 다. 여기서 보는 것은 후자다.
                Assert.That(bounds.size.y, Is.EqualTo(1.00f).Within(0.05f),
                    $"세로 치수가 제작 치수와 다르다 — 실측 {bounds.size:F3}.");
                Assert.That(bounds.size.y, Is.GreaterThan(Mathf.Max(bounds.size.x, bounds.size.z)),
                    $"가장 긴 변이 세로가 아니다 — 통이 누워 들어왔다. 실측 {bounds.size:F3}.");

                var box = new Bounds(Vector3.zero, new Vector3(0.55f, 1.1f, 0.55f));
                Assert.That(box.Contains(bounds.min), Is.True, $"실물 아랫면이 판정 상자 밖이다 — {bounds.min:F3}.");
                Assert.That(box.Contains(bounds.max), Is.True, $"실물 윗면이 판정 상자 밖이다 — {bounds.max:F3}.");
                Assert.That(bounds.min.y, Is.EqualTo(-0.55f).Within(0.02f),
                    "실물 바닥이 판정 상자 바닥에 붙어 있어야 놓인 자리와 보이는 자리가 같다.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
