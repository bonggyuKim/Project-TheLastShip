using System.Text;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 캐릭터 리그의 계층·본 위치를 그대로 찍는다. 리그가 갈릴 때마다 부위 매핑을 손으로
    /// 다시 세워야 하는데, 그 판단의 입력이 계층과 뼈 위치다 — 스크린샷으로는 못 읽는다.
    /// </summary>
    public static class LastShiftRigReport
    {
        public const string OutputPath = "rig-report.txt";

        [MenuItem("Last Shift/Prototype/Dump Character Rig")]
        public static void Dump()
        {
            var path = CommandLineValue("-rigAsset")
                       ?? "Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_RigifyDeform.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[LAST_SHIFT_RIG_REPORT] asset missing: {path}");
                return;
            }

            var instance = Object.Instantiate(prefab);
            var text = new StringBuilder();
            text.AppendLine($"asset={path}");
            Walk(instance.transform, instance.transform, text, 0);

            foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                text.AppendLine($"skin name={skin.name} verts={skin.sharedMesh.vertexCount} " +
                                $"bones={skin.bones.Length} submeshes={skin.sharedMesh.subMeshCount} " +
                                $"materials={string.Join("|", System.Array.ConvertAll(skin.sharedMaterials, m => m == null ? "null" : m.name))}");
                ReportWeights(skin, text);
            }

            System.IO.File.WriteAllText(OutputPath, text.ToString());
            Object.DestroyImmediate(instance);
            Debug.Log($"[LAST_SHIFT_RIG_REPORT] wrote={OutputPath} result=PASS");
        }

        /// <summary>
        /// <b>제어본이 웨이트를 들고 있는가.</b> Rigify 는 <c>DEF-</c> 만 변형본으로 쓰는 것이
        /// 규약이지만, 익스포트가 <c>MCH-</c>/<c>ORG-</c>/<c>tweak</c> 까지 딸려 보내면서 웨이트가
        /// 섞이면 래그돌에서 <b>그 부분만 바인드 포즈에 남아</b> 메시가 찢어진다.
        /// 화면에서는 "래그돌이 이상하다" 로만 보이고 원인은 안 보인다.
        /// </summary>
        private static void ReportWeights(SkinnedMeshRenderer skin, StringBuilder text)
        {
            var mesh = skin.sharedMesh;
            var bones = skin.bones;
            var weights = mesh.GetAllBoneWeights();
            var counts = mesh.GetBonesPerVertex();

            var perBone = new float[bones.Length];
            var maxInfluences = 0;
            var cursor = 0;
            for (var v = 0; v < counts.Length; v++)
            {
                int used = counts[v];
                if (used > maxInfluences) maxInfluences = used;
                for (var i = 0; i < used; i++, cursor++)
                {
                    var w = weights[cursor];
                    if (w.boneIndex >= 0 && w.boneIndex < perBone.Length) perBone[w.boneIndex] += w.weight;
                }
            }

            var deform = 0f;
            var control = 0f;
            var controlNames = new System.Collections.Generic.List<string>();
            for (var i = 0; i < bones.Length; i++)
            {
                if (perBone[i] <= 0.0001f) continue;
                var name = bones[i] == null ? "(null)" : bones[i].name;
                if (name.StartsWith("DEF-")) deform += perBone[i];
                else
                {
                    control += perBone[i];
                    controlNames.Add($"{name}:{perBone[i]:F2}");
                }
            }

            text.AppendLine($"weights maxInfluences={maxInfluences} deformTotal={deform:F1} controlTotal={control:F1}");
            text.AppendLine(controlNames.Count == 0
                ? "weights controlBones=NONE"
                : $"weights controlBones={string.Join(", ", controlNames)}");
        }

        private static void Walk(Transform node, Transform root, StringBuilder text, int depth)
        {
            var local = root.InverseTransformPoint(node.position);
            text.AppendLine($"{new string(' ', depth * 2)}{node.name} " +
                            $"pos=({local.x:F3},{local.y:F3},{local.z:F3})");
            for (var i = 0; i < node.childCount; i++) Walk(node.GetChild(i), root, text, depth + 1);
        }

        private static string CommandLineValue(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return null;
        }
    }
}
