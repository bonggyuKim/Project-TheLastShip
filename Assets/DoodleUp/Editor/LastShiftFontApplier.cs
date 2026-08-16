using System.Collections.Generic;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 손으로 엮은 프리팹·씬 안의 <see cref="Text"/> 폰트를 번들 한글 폰트로 맞춘다.
    ///
    /// <b>왜 도구가 필요한가.</b> 코드가 세우는 글자는
    /// <see cref="LastShiftUiFactory.CreateText"/> 한 곳을 지나므로 자동으로 따라오는데,
    /// HUD 는 <b>프리팹이 정본</b>이라 그 안의 폰트 참조는 코드가 못 고친다. 그리고 프리팹은
    /// 이 프로젝트에서 이미 한 번 통째로 초기값으로 되돌아간 적이 있어서, 그때 다시 부를
    /// 수 있는 형태로 남겨 둔다.
    ///
    /// 이름 그대로 <b>폰트만</b> 만진다. 다른 필드는 읽지도 쓰지도 않는다 — 배선을 날린 전례가
    /// 있는 자리라 "겸사겸사" 를 넣으면 안 된다.
    /// </summary>
    public static class LastShiftFontApplier
    {
        /// <summary>훑을 자리. 지금 <see cref="Text"/> 가 들어 있는 에셋은 HUD 프리팹뿐이다.</summary>
        private static readonly string[] SearchFolders = { "Assets/DoodleUp" };

        [MenuItem("Last Shift/UI/Apply Bundled Font")]
        public static void Apply()
        {
            var font = LastShiftFonts.Korean;
            if (!LastShiftFonts.HasBundledKorean)
            {
                Debug.LogError(
                    $"[LAST_SHIFT_FONT] resource={LastShiftFonts.KoreanResourcePath} result=MISSING " +
                    "detail=번들 폰트를 못 찾았다 — 폰트 에셋부터 확인한다");
                return;
            }

            var changed = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", SearchFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var touched = 0;
                    foreach (var text in root.GetComponentsInChildren<Text>(true))
                    {
                        if (text.font == font) continue;
                        text.font = font;
                        touched++;
                    }

                    if (touched == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed.Add($"{path}({touched})");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[LAST_SHIFT_FONT] font={font.name} result=OK " +
                      $"changed={(changed.Count == 0 ? "NONE" : string.Join(",", changed))}");
        }
    }
}
