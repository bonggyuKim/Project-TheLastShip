using System.Collections.Generic;
using System.IO;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// <see cref="LastShiftUiKit"/> 에셋을 굽는다.
    ///
    /// <b>손으로 배선하지 않는 이유</b>는 이 프로젝트가 이미 한 번 크게 데었기 때문이다 —
    /// 인스펙터로 이어 둔 참조는 부트스트랩 한 번에 통째로 초기값이 됐다. 표(<see
    /// cref="LastShiftUiKit.SpriteTable"/>)를 보고 굽는 함수가 있으면, 날아가도 한 번 더
    /// 구우면 그만이고 배선이 옳은지는 <c>Verify</c> 가 답한다.
    ///
    /// 배 프리팹과 같은 규칙이다: <b>생성물은 커밋하되 손으로 고치지 않는다.</b>
    /// </summary>
    public static class LastShiftUiKitBuilder
    {
        [MenuItem("DoodleUp/LAST SHIFT/UI 아트 키트 굽기")]
        public static void BuildMenu()
        {
            var report = Build();
            Debug.Log(report);
        }

        /// <summary>
        /// 표대로 스프라이트를 이어 에셋을 다시 쓴다. 이미 있으면 덮어쓴다 — 새로 만들면
        /// GUID 가 바뀌어 <c>Resources.Load</c> 는 멀쩡해도 씬·프리팹의 참조가 끊긴다.
        /// </summary>
        public static string Build()
        {
            var directory = Path.GetDirectoryName(LastShiftUiKit.AssetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            var kit = AssetDatabase.LoadAssetAtPath<LastShiftUiKit>(LastShiftUiKit.AssetPath);
            var created = kit == null;
            if (created) kit = ScriptableObject.CreateInstance<LastShiftUiKit>();

            var missing = new List<string>();
            foreach (var (field, fileName) in LastShiftUiKit.SpriteTable)
            {
                var path = $"{LastShiftUiKit.ArtFolder}/{fileName}.png";
                ApplyImportSettings(path, fileName);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) missing.Add(fileName);
                kit.Assign(field, sprite);
            }

            if (created) AssetDatabase.CreateAsset(kit, LastShiftUiKit.AssetPath);
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            LastShiftUiKit.ResetLookup();

            return missing.Count == 0
                ? $"[LastShiftUiKitBuilder] {LastShiftUiKit.SpriteTable.Length}칸 배선 완료 → {LastShiftUiKit.AssetPath}"
                : $"[LastShiftUiKitBuilder] 빠진 그림 {missing.Count}종: {string.Join(", ", missing)}";
        }

        /// <summary>
        /// 9-slice 경계. <b>키트 원본에는 이 정보가 없다</b> — 판과 프롬프트는 가로로 늘어나는데
        /// 모서리를 안 잠그면 둥근 깎임과 왼쪽 삼각 표식이 같이 눌린다. 값은 그림을 굽는
        /// <c>Tools/art/generate_last_shift_ui_kit.py</c> 의 모서리 반지름에서 온다:
        /// 패널 <c>22</c>, 판 <c>18</c>+표식 <c>34</c>.
        ///
        /// <b>아이콘에는 경계가 없다.</b> 늘어나지 않고 정사각형으로만 놓이며, 채움은 크기가
        /// 아니라 <c>fillAmount</c> 로 움직인다.
        /// </summary>
        private static Vector4 BorderOf(string fileName) => fileName switch
        {
            "panel_9slice" => new Vector4(16f, 16f, 16f, 16f),
            "prompt_plate" => new Vector4(44f, 20f, 24f, 20f),
            _ => Vector4.zero
        };

        /// <summary>
        /// 임포트 설정을 코드가 쥔다.
        ///
        /// <b>이 자리가 생긴 이유</b>는 아트 키트가 <c>.meta</c> 를 손으로 써서 납품했고 그
        /// YAML 을 유니티가 못 읽어 그림 17장이 통째로 무시됐기 때문이다("could not be
        /// parsed"). <c>.meta</c> 는 이제 guid 만 들고 있고, 스프라이트 여부·경계·메시 종류는
        /// 전부 여기서 정한다 — 그림을 다시 구워도 설정이 안 흔들린다.
        ///
        /// <b><see cref="SpriteMeshType.FullRect"/> 가 필수다.</b> 기본값 <c>Tight</c> 는 투명
        /// 영역을 잘라낸 메시를 만들어서, 9-slice 와 <c>Filled</c> 채움이 둘 다 깨진다.
        /// </summary>
        private static void ApplyImportSettings(string path, string fileName)
        {
            if (AssetImporter.GetAtPath(path) == null)
                AssetDatabase.ImportAsset(path,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            // <b>모양을 명시한다.</b> 유니티는 <c>.meta</c> 에 <c>textureShape</c> 가 없으면
            // 가로세로비로 큐브맵을 추측하는데, 512×64 게이지 프레임이 실제로 그렇게 들어와
            // <c>Cubemap</c> 으로 임포트됐다 — 그러면 스프라이트가 아예 안 생긴다.
            settings.textureShape = TextureImporterShape.Texture2D;
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePixelsPerUnit = 100f;
            settings.spriteExtrude = 0;
            settings.alphaIsTransparency = true;
            settings.filterMode = FilterMode.Bilinear;
            settings.wrapMode = TextureWrapMode.Clamp;
            settings.mipmapEnabled = false;
            importer.SetTextureSettings(settings);

            importer.spriteBorder = BorderOf(fileName);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        /// <summary>배치 실행용 진입점. 배선이 비면 종료 코드로 실패를 알린다.</summary>
        public static void BuildBatch()
        {
            var report = Build();
            Debug.Log(report);
            var kit = AssetDatabase.LoadAssetAtPath<LastShiftUiKit>(LastShiftUiKit.AssetPath);
            if (kit == null || !kit.IsFullyWired())
            {
                Debug.LogError("[LastShiftUiKitBuilder] 배선이 비었다.");
                EditorApplication.Exit(1);
            }
        }
    }
}
