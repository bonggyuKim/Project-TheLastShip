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
                ReportIslands(skin, text);
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

        /// <summary>
        /// <b>메시가 몇 덩어리인가.</b> 포즈를 크게 주면 판 모양 조각이 통째로 튀어나오는데,
        /// 뼈가 전부 매끄럽게 따라오는데도 그러면 남는 후보는 <b>본체에서 떨어진 섬</b>이다.
        /// 섬은 자기 뼈만 따라가므로 포즈가 커질수록 본체에서 멀어져 떠 보인다.
        /// 정지 자세에서는 딱 붙어 있어 눈으로는 절대 안 걸린다.
        /// </summary>
        private static void ReportIslands(SkinnedMeshRenderer skin, StringBuilder text)
        {
            var mesh = skin.sharedMesh;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            // 같은 자리에 있는 정점은 하나로 본다 - UV/법선 이음매 때문에 갈라진 정점을
            // 따로 세면 멀쩡한 메시도 수백 덩어리로 나온다.
            var welded = new int[vertices.Length];
            var lookup = new System.Collections.Generic.Dictionary<Vector3Int, int>();
            for (var i = 0; i < vertices.Length; i++)
            {
                var key = new Vector3Int(
                    Mathf.RoundToInt(vertices[i].x * 10000f),
                    Mathf.RoundToInt(vertices[i].y * 10000f),
                    Mathf.RoundToInt(vertices[i].z * 10000f));
                if (!lookup.TryGetValue(key, out var id)) { id = lookup.Count; lookup[key] = id; }
                welded[i] = id;
            }

            var parent = new int[lookup.Count];
            for (var i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            void Union(int a, int b) { var ra = Find(a); var rb = Find(b); if (ra != rb) parent[ra] = rb; }

            for (var i = 0; i < triangles.Length; i += 3)
            {
                Union(welded[triangles[i]], welded[triangles[i + 1]]);
                Union(welded[triangles[i + 2]], welded[triangles[i + 1]]);
            }

            var sizes = new System.Collections.Generic.Dictionary<int, int>();
            for (var i = 0; i < parent.Length; i++)
            {
                var root = Find(i);
                sizes.TryGetValue(root, out var count);
                sizes[root] = count + 1;
            }

            var ordered = new System.Collections.Generic.List<int>(sizes.Values);
            ordered.Sort();
            ordered.Reverse();
            text.AppendLine($"islands count={ordered.Count} sizes={string.Join(",", ordered.GetRange(0, Mathf.Min(12, ordered.Count)))}");

            // 작은 섬이 어디 있는지도 남긴다. 아트가 블렌더에서 바로 찾아갈 수 있게.
            if (ordered.Count > 1)
            {
                foreach (var pair in sizes)
                {
                    if (pair.Value > 200) continue;
                    var center = Vector3.zero;
                    var seen = 0;
                    for (var i = 0; i < vertices.Length; i++)
                        if (Find(welded[i]) == pair.Key) { center += vertices[i]; seen++; }
                    if (seen == 0) continue;
                    text.AppendLine($"island verts={pair.Value} center={(center / seen).ToString("F3")}");
                }
            }
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
