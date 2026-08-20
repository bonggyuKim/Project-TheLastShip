using System.Collections.Generic;
using System.IO;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 구간에 낀 변형본이 굽힘을 <b>얼마나</b> 받아야 스킨이 안 찢어지는지 잰다.
    ///
    /// <b>왜 필요한가.</b> <see cref="LastShiftRagdollRig.BendShareOf"/> 표에 실측값이 있는 것은
    /// 목 둘(<c>DEF-spine.004</c>·<c>.005</c>)뿐이고, 나머지 열은 전부 <b>등분 추정치</b>다.
    /// 그런데 낙하 실측에서 남은 찢어짐은 정확히 그 등분 구간들에 몰려 있었다 —
    /// <c>DEF-spine</c> 80 · <c>DEF-shin.L</c>+<c>DEF-thigh.L.001</c> 68 · <c>DEF-spine.001</c> 49
    /// (2026-08-21, 저장된 랩 씬 착지 최악 프레임). 등분이 이 리그에 맞는다는 근거가 애초에 없었다.
    ///
    /// <b>물리를 안 쓴다.</b> <see cref="LastShiftSkinToleranceProbe"/> 와 같은 이유다 — 래그돌을
    /// 돌리면 자세가 매번 달라 몫끼리 비교가 안 된다. 구간의 <b>자식</b> 뼈만 정해진 각도로 꺾고,
    /// 몫을 0 부터 1 까지 쓸며 늘어난 삼각형이 가장 적은 값을 고른다.
    ///
    /// <b>매 측정 전에 골격을 스냅샷으로 되돌린다.</b> 안 되돌리면 값이 조용히 밀린다 —
    /// 중간 뼈를 옮기면 그 자식이 매달린 자리가 달라지고, 다음
    /// <see cref="LastShiftRagdollSkinFollow.Capture"/> 가 그 어긋난 자세를 새 정지 포즈로 굳힌다.
    /// 첫 판에서 늘어난 삼각형이 6,396 에서 25,120 으로 단조 증가하며 "최적값"이 등분보다 나빠
    /// 보였던 것이 이 되먹임이었다(2026-08-21).
    ///
    /// <b>구간이 건드리는 삼각형만 센다.</b> 몸통을 70도 꺾으면 팔·머리까지 끌려가는데, 그것까지
    /// 합계에 넣으면 지금 쓸고 있는 몫과 무관한 값이 신호를 덮는다.
    /// </summary>
    public static class LastShiftRagdollBendShareProbe
    {
        private const float ShareStep = 0.05f;

        private const int Passes = 2;

        /// <summary>정지 대비 이 배율 이상 늘어난 모서리를 가진 삼각형을 "찢어졌다"로 센다.</summary>
        private const float TornRatio = 3f;

        private const float MinRestEdge = 0.003f;

        /// <summary>
        /// 구간 자식을 이 각도들로 꺾어 보고 합계로 판단한다. 한 각도만 보면 그 각도에만 맞는다.
        ///
        /// <b>부호를 둘 다 넣는다.</b> Rigify 는 좌우 뼈의 로컬 축을 거울로 뽑으므로, 한쪽 부호만
        /// 재면 왼쪽은 발등 방향·오른쪽은 발바닥 방향을 잰 뒤 그 차이를 "왼쪽 웨이트가 나쁘다"로
        /// 읽게 된다. 좌우를 같은 자로 재려면 양쪽 부호가 다 있어야 한다.
        /// </summary>
        private static readonly float[] TestAngles = { 20f, -20f, 45f, -45f, 70f, -70f };

        /// <summary>골격 전체의 로컬 TRS. 매 측정을 같은 자리에서 시작시키는 기준이다.</summary>
        private readonly struct Snapshot
        {
            public Snapshot(Transform root)
            {
                _bones = root.GetComponentsInChildren<Transform>(true);
                _positions = new Vector3[_bones.Length];
                _rotations = new Quaternion[_bones.Length];
                for (var i = 0; i < _bones.Length; i++)
                {
                    _positions[i] = _bones[i].localPosition;
                    _rotations[i] = _bones[i].localRotation;
                }
            }

            private readonly Transform[] _bones;
            private readonly Vector3[] _positions;
            private readonly Quaternion[] _rotations;

            public void Restore()
            {
                for (var i = 0; i < _bones.Length; i++)
                {
                    if (_bones[i] == null) continue;
                    _bones[i].localPosition = _positions[i];
                    _bones[i].localRotation = _rotations[i];
                }
            }
        }

        [MenuItem("Last Shift/Prototype/Probe Bend Shares")]
        public static void RunForAutomation()
        {
            EditorSceneManager.OpenScene(LastShiftRagdollLabScene.ScenePath, OpenSceneMode.Single);

            var subject = GameObject.Find("RagdollSubject");
            if (subject == null) throw new System.InvalidOperationException("RagdollSubject 를 못 찾았다.");

            var follow = subject.GetComponent<LastShiftRagdollSkinFollow>();
            if (follow == null) throw new System.InvalidOperationException("SkinFollow 가 없다.");

            var renderer = BodyRenderer(subject);
            var rest = renderer.sharedMesh.vertices;
            var triangles = renderer.sharedMesh.triangles;
            var snapshot = new Snapshot(subject.transform);

            var shares = new Dictionary<string, float>();
            follow.OverrideBendShares(shares);

            var log = new List<string>();
            var mids = new List<(int Segment, int Mid, string Name)>();
            for (var s = 0; s < follow.SegmentCount; s++)
            {
                var names = new List<string>();
                for (var m = 0; m < follow.MidBoneCount(s); m++)
                {
                    names.Add(follow.MidBoneName(s, m));
                    mids.Add((s, m, follow.MidBoneName(s, m)));
                }

                log.Add($"segment {s}: {string.Join(" -> ", names)}");
            }

            var masks = new bool[follow.SegmentCount][];
            for (var s = 0; s < follow.SegmentCount; s++) masks[s] = MaskOf(subject, follow, renderer, triangles, s);

            var baseline = new Dictionary<string, float>();
            foreach (var (_, mid, name) in mids)
            {
                baseline[name] = LastShiftRagdollRig.BendShareOf(name, mid, MidCountOf(mids, name));
                shares[name] = baseline[name];
            }

            for (var s = 0; s < follow.SegmentCount; s++)
            {
                var cost = Cost(subject, follow, baseline, s, renderer, rest, triangles, masks[s], snapshot);
                log.Add($"baseline segment {s}  torn {cost}");
            }

            for (var pass = 0; pass < Passes; pass++)
            {
                foreach (var (segment, _, name) in mids)
                {
                    var best = shares[name];
                    var bestCost = int.MaxValue;
                    for (var share = 0f; share <= 1.0001f; share += ShareStep)
                    {
                        shares[name] = Mathf.Round(share / ShareStep) * ShareStep;
                        var cost = Cost(
                            subject, follow, shares, segment, renderer, rest, triangles, masks[segment], snapshot);
                        if (cost >= bestCost) continue;
                        bestCost = cost;
                        best = shares[name];
                    }

                    shares[name] = best;
                    log.Add($"pass {pass}  {name,-22}share {best:F2}  torn {bestCost}");
                }
            }

            log.Add("--- 표에 넣을 값 ---");
            foreach (var (_, _, name) in mids) log.Add($"  {{ \"{name}\", {shares[name]:F2}f }},");
            log.Add($"total torn  현행 {Total(subject, follow, baseline, renderer, rest, triangles, masks, snapshot)}"
                    + $"  실측최적 {Total(subject, follow, shares, renderer, rest, triangles, masks, snapshot)}");

            var path = Path.GetFullPath("tmp/ragdoll-bend-shares.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, log);
            Debug.Log("[LAST_SHIFT_BEND_SHARES]\n" + string.Join("\n", log));
        }

        private static int MidCountOf(List<(int Segment, int Mid, string Name)> mids, string name)
        {
            var segment = -1;
            foreach (var entry in mids)
                if (entry.Name == name) segment = entry.Segment;

            var count = 0;
            foreach (var entry in mids)
                if (entry.Segment == segment) count++;

            return count;
        }

        /// <summary>구간 하나를 꺾어 보며, 그 구간이 건드리는 삼각형 중 늘어난 것을 센다.</summary>
        private static int Cost(
            GameObject subject,
            LastShiftRagdollSkinFollow follow,
            Dictionary<string, float> shares,
            int segment,
            SkinnedMeshRenderer renderer,
            Vector3[] rest,
            int[] triangles,
            bool[] mask,
            Snapshot snapshot)
        {
            snapshot.Restore();
            follow.OverrideBendShares(shares);

            var child = ChildOf(follow, subject, segment);
            if (child == null) return 0;

            var saved = child.localRotation;
            var total = 0;
            foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            foreach (var degrees in TestAngles)
            {
                snapshot.Restore();
                child.localRotation = Quaternion.AngleAxis(degrees, axis) * saved;
                follow.Apply();
                total += Torn(renderer, rest, triangles, mask);
            }

            snapshot.Restore();
            return total;
        }

        private static int Total(
            GameObject subject,
            LastShiftRagdollSkinFollow follow,
            Dictionary<string, float> shares,
            SkinnedMeshRenderer renderer,
            Vector3[] rest,
            int[] triangles,
            bool[][] masks,
            Snapshot snapshot)
        {
            var total = 0;
            for (var s = 0; s < follow.SegmentCount; s++)
                total += Cost(subject, follow, shares, s, renderer, rest, triangles, masks[s], snapshot);
            return total;
        }

        /// <summary>
        /// 구간이 건드리는 삼각형만 <c>true</c>. 정점의 <b>주 웨이트 뼈</b>가 구간의 양 끝이나
        /// 중간 뼈이면 그 삼각형을 센다.
        /// </summary>
        private static bool[] MaskOf(
            GameObject subject,
            LastShiftRagdollSkinFollow follow,
            SkinnedMeshRenderer renderer,
            int[] triangles,
            int segment)
        {
            var names = new HashSet<string>();
            for (var m = 0; m < follow.MidBoneCount(segment); m++) names.Add(follow.MidBoneName(segment, m));

            var child = ChildOf(follow, subject, segment);
            if (child != null) names.Add(child.name);

            // 중간 뼈의 부모(바디 있는 뼈)도 넣는다 — 이음매는 그쪽에도 걸쳐 있다.
            var first = follow.MidBoneName(segment, 0);
            foreach (var t in subject.GetComponentsInChildren<Transform>(true))
                if (t.name == first && t.parent != null) names.Add(t.parent.name);

            var bones = renderer.bones;
            var weights = renderer.sharedMesh.boneWeights;
            var mask = new bool[triangles.Length / 3];
            for (var t = 0; t < triangles.Length; t += 3)
            {
                for (var k = 0; k < 3; k++)
                {
                    var index = weights[triangles[t + k]].boneIndex0;
                    if (index < 0 || index >= bones.Length || bones[index] == null) continue;
                    if (!names.Contains(bones[index].name)) continue;
                    mask[t / 3] = true;
                    break;
                }
            }

            return mask;
        }

        /// <summary>
        /// 구간의 자식 뼈. <see cref="LastShiftRagdollSkinFollow"/> 는 이름만 내주므로
        /// 마지막 중간 뼈의 자손 중 바디를 가진 뼈를 찾아 되짚는다.
        /// </summary>
        private static Transform ChildOf(LastShiftRagdollSkinFollow follow, GameObject subject, int segment)
        {
            var lastMid = follow.MidBoneName(segment, follow.MidBoneCount(segment) - 1);
            foreach (var t in subject.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != lastMid) continue;
                foreach (var body in t.GetComponentsInChildren<Rigidbody>(true))
                    if (body.transform != t) return body.transform;
            }

            return null;
        }

        private static int Torn(SkinnedMeshRenderer renderer, Vector3[] rest, int[] triangles, bool[] mask)
        {
            var baked = new Mesh();
            renderer.BakeMesh(baked, false);
            var posed = baked.vertices;

            var torn = 0;
            for (var t = 0; t < triangles.Length; t += 3)
            {
                if (mask != null && !mask[t / 3]) continue;

                var ratio = 0f;
                for (var k = 0; k < 3; k++)
                {
                    var a = triangles[t + k];
                    var b = triangles[t + ((k + 1) % 3)];
                    var restLength = Vector3.Distance(rest[a], rest[b]);
                    if (restLength < MinRestEdge) continue;
                    ratio = Mathf.Max(ratio, Vector3.Distance(posed[a], posed[b]) / restLength);
                }

                if (ratio >= TornRatio) torn++;
            }

            Object.DestroyImmediate(baked);
            return torn;
        }

        private static SkinnedMeshRenderer BodyRenderer(GameObject subject)
        {
            SkinnedMeshRenderer body = null;
            foreach (var skin in subject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.sharedMesh != null && skin.sharedMesh.name.Contains("Body")) body = skin;

            if (body == null) throw new System.InvalidOperationException("몸 스킨 렌더러를 못 찾았다.");
            return body;
        }
    }
}
