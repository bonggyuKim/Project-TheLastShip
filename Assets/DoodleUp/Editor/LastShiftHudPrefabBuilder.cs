using System.IO;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 상시 HUD 프리팹을 <b>한 번</b> 굽는다.
    ///
    /// <b>이미 있으면 안 덮는다.</b> 이 도구의 목적은 아이콘 자리를 코드에서 프리팹으로 옮기는
    /// 것이고, 옮긴 뒤에는 <b>프리팹이 정본</b>이다. 매번 다시 구우면 에디터에서 끌어 옮긴
    /// 위치가 그때마다 초기값으로 돌아가서, 결국 좌표가 코드로 되돌아온 것과 같아진다.
    /// 아래 숫자들은 그래서 "정본" 이 아니라 <b>첫 배치의 씨앗</b>이다(아트 규격
    /// <c>last-shift-hud-icon-only-v1.md</c> 값).
    ///
    /// 다시 굽고 싶으면 프리팹을 지우고 메뉴를 부른다 — 지우는 행위가 곧 "지금 위치를
    /// 버린다" 는 뜻이라 실수로 날아가지 않는다.
    /// </summary>
    public static class LastShiftHudPrefabBuilder
    {
        private const string Folder = "Assets/DoodleUp/Resources";
        private const string Path = Folder + "/" + LastShiftHudView.ResourcePath + ".prefab";

        // ── 첫 배치 씨앗. 여기 값이 프리팹에 한 번 구워지고 나면 프리팹이 정본이 된다. ──
        private const float IconSize = 56f;
        private const float IconGap = 12f;
        private const float RightMargin = 48f;
        private const float Top = 28f;

        [MenuItem("Last Shift/UI/Build HUD Prefab")]
        public static void Build()
        {
            if (File.Exists(Path))
            {
                Debug.Log($"[LAST_SHIFT_HUD_PREFAB] path={Path} result=SKIPPED " +
                          "detail=이미 있다 — 프리팹이 정본이므로 안 덮는다. 다시 구우려면 먼저 지운다");
                return;
            }

            Directory.CreateDirectory(Folder);

            var root = new GameObject("LastShiftHud", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            // 캔버스 전체를 덮는 빈 판. 아이콘은 이 안에서 우측 상단 앵커로 앉는다.
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var view = root.AddComponent<LastShiftHudView>();
            var oxygen = CreateIcon(rootRect, "Icon:Oxygen", LastShiftUiIcon.Oxygen, 0);
            var power = CreateIcon(rootRect, "Icon:Power", LastShiftUiIcon.Power, 1);
            var heat = CreateIcon(rootRect, "Icon:Heat", LastShiftUiIcon.Heat, 2);
            view.Configure(oxygen, power, heat);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, Path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            Debug.Log(saved != null
                ? $"[LAST_SHIFT_HUD_PREFAB] path={Path} icons=3 result=PASS"
                : $"[LAST_SHIFT_HUD_PREFAB] path={Path} result=FAIL");
        }

        /// <summary>
        /// 아이콘 하나. <b>앵커를 우측 상단에 붙인다</b> — 그래야 해상도가 달라도 오른쪽에
        /// 붙어 있고, 런타임이 화면 폭을 읽어 자리를 다시 계산할 필요가 없다.
        /// </summary>
        private static LastShiftGaugeView CreateIcon(
            RectTransform parent, string name, LastShiftUiIcon icon, int slot)
        {
            var gauge = LastShiftGaugeView.Create(parent, name, icon);
            var rect = (RectTransform)gauge.transform;

            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(IconSize, IconSize);
            // 우측 상단 기준이라 둘 다 음수로 들어간다.
            rect.anchoredPosition = new Vector2(-RightMargin, -(Top + slot * (IconSize + IconGap)));

            gauge.MakeIconOnly();
            return gauge;
        }
    }
}
