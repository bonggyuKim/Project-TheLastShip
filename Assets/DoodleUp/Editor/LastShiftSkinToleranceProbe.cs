using System.Collections.Generic;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 관절 하나가 <b>스킨을 안 찢고</b> 열 수 있는 각도를 잰다.
    ///
    /// <b>왜 도구로 남기나.</b> 이 값은 메시·웨이트·굽힘 분산이 바뀔 때마다 달라진다. 표에 박아
    /// 두고 손으로 고치면 근거가 사라지므로, <see cref="LastShiftRagdollSkinLimits"/> 의 표는
    /// 항상 이 도구의 출력에서 나와야 한다.
    ///
    /// <b>물리를 안 쓴다.</b> 래그돌을 돌려서 재면 자세가 매번 달라 비교가 안 된다. 프리팹을 새로
    /// 띄워 뼈 하나만 조인트 축 둘레로 2도씩 돌리며, 예산을 넘기 직전 각을 기록한다.
    ///
    /// <b>굽힘 분산을 켜고 잰다.</b> 실제 래그돌에는 <see cref="LastShiftRagdollSkinFollow"/> 가
    /// 붙어 중간 변형본에 굽힘을 나눠 준다. 그것을 끄고 재면 스킨 여유를 실제보다 좁게 봐서
    /// 관절을 필요 이상으로 조이게 된다(2026-08-19 첫 측정이 그랬다). 그래서 여기서는 컴포넌트를
    /// <b>그대로 붙여</b> 쓴다 — 계산을 베껴 오면 둘이 어긋난다.
    /// </summary>
    public static class LastShiftSkinToleranceProbe
    {
        /// <summary>여기까지만 연다. 이 이상은 래그돌 설계상 의미가 없다.</summary>
        public const int MaxAngleDegrees = 60;

        public const int StepDegrees = 2;

        /// <summary>이 개수 이하이고 아래 배율 이하이면 "안 보인다"로 친다.</summary>
        public const int TornTriangleBudget = 5;

        public const float WorstStretchBudget = 4.05f;

        /// <summary>정지 대비 이 배율 이상 늘어난 모서리를 가진 삼각형을 "찢어졌다"로 센다.</summary>
        private const float TornRatio = 3f;

        /// <summary>이보다 짧은 정지 모서리는 무시한다 — 미세 삼각형이 배율을 지배한다.</summary>
        private const float MinRestEdge = 0.003f;

        /// <summary>관절 하나가 버티는 각(도).</summary>
        public readonly struct Tolerance
        {
            public Tolerance(int swing1, int swing2, int twistLow, int twistHigh)
            {
                Swing1 = swing1;
                Swing2 = swing2;
                TwistLow = twistLow;
                TwistHigh = twistHigh;
            }

            public int Swing1 { get; }

            public int Swing2 { get; }

            /// <summary>음수다. 조인트의 <c>lowTwistLimit</c> 과 같은 부호.</summary>
            public int TwistLow { get; }

            public int TwistHigh { get; }

            public override string ToString() => $"{Swing1}/{Swing2}/{TwistLow}..{TwistHigh}";
        }

        [MenuItem("Last Shift/Prototype/Probe Skin Tolerance")]
        private static void RunOnOpenScene()
        {
            var subject = GameObject.Find("RagdollSubject");
            if (subject == null)
            {
                Debug.LogError("RagdollSubject 를 못 찾았다 — 래그돌 랩 씬을 먼저 열어라.");
                return;
            }

            var log = new List<string>();
            var table = Measure(subject, log);
            log.Add("--- 좌우 중 빡빡한 쪽, 5도 단위 내림 ---");
            foreach (var pair in table) log.Add($"  {pair.Key,-18}{pair.Value}");
            Debug.Log("스킨 여유 실측:\n" + string.Join("\n", log));
        }

        /// <summary>
        /// 씬의 래그돌이 가진 조인트 구성을 그대로 써서 뼈 종류별 여유를 잰다.
        /// 좌우는 한 종류로 묶어 <b>빡빡한 쪽</b>을 남긴다 — 좌우를 다르게 주면 몸이 한쪽으로만 접힌다.
        /// </summary>
        public static Dictionary<string, Tolerance> Measure(GameObject sceneSubject, List<string> log)
        {
            var joints = sceneSubject.GetComponentsInChildren<CharacterJoint>(true);
            var bodyNames = new List<string>();
            foreach (var body in sceneSubject.GetComponentsInChildren<Rigidbody>(true))
            {
                bodyNames.Add(body.name);
            }

            var probe = BuildProbe(bodyNames, out var follow, out var renderer);
            try
            {
                var mesh = renderer.sharedMesh;
                var rest = mesh.vertices;
                var triangles = mesh.triangles;
                var bones = Index(probe);

                var result = new Dictionary<string, Tolerance>();
                foreach (var joint in joints)
                {
                    if (!bones.TryGetValue(joint.name, out var bone)) continue;

                    var twist = joint.axis.normalized;
                    var swing1 = Vector3.ProjectOnPlane(joint.swingAxis, twist).normalized;
                    var swing2 = Vector3.Cross(twist, swing1).normalized;

                    var s1 = Mathf.Min(
                        Sweep(bone, swing1, 1, follow, renderer, rest, triangles),
                        Sweep(bone, swing1, -1, follow, renderer, rest, triangles));
                    var s2 = Mathf.Min(
                        Sweep(bone, swing2, 1, follow, renderer, rest, triangles),
                        Sweep(bone, swing2, -1, follow, renderer, rest, triangles));
                    var high = Sweep(bone, twist, 1, follow, renderer, rest, triangles);
                    var low = Sweep(bone, twist, -1, follow, renderer, rest, triangles);

                    log?.Add($"  {joint.name,-18}s1 {s1,3}  s2 {s2,3}  tw -{low}..+{high}");

                    var kind = KindOf(joint.name);
                    var next = new Tolerance(Floor5(s1), Floor5(s2), -Floor5(low), Floor5(high));
                    result[kind] = result.TryGetValue(kind, out var seen) ? Tighter(seen, next) : next;
                }

                return result;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// 씬과 같은 뼈에 <b>키네마틱</b> 리지드바디를 얹은 측정용 인스턴스. 바디가 있어야
        /// <see cref="LastShiftRagdollSkinFollow"/> 가 구간을 찾는다. 물리는 안 돈다.
        /// </summary>
        private static GameObject BuildProbe(
            List<string> bodyNames,
            out LastShiftRagdollSkinFollow follow,
            out SkinnedMeshRenderer renderer)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(LastShiftRagdollLabScene.CharacterPrefabPath);
            if (source == null)
            {
                throw new System.InvalidOperationException(
                    $"승무원 프리팹을 못 찾았다: {LastShiftRagdollLabScene.CharacterPrefabPath}");
            }

            var probe = Object.Instantiate(source);
            var bones = Index(probe);
            foreach (var name in bodyNames)
            {
                if (!bones.TryGetValue(name, out var bone)) continue;
                bone.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            }

            follow = probe.AddComponent<LastShiftRagdollSkinFollow>();
            follow.Capture();

            // <b>이름으로 고른다.</b> 눈이 별도 렌더러로 들어와 있어서 GetComponentInChildren 은
            // 몸이 아니라 눈을 집을 수 있다 — 그러면 머리를 90도 꺾어도 찢어짐 0 이 나온다(실제로 겪음).
            renderer = null;
            foreach (var skin in probe.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh != null && skin.sharedMesh.name.Contains("Body")) renderer = skin;
            }

            if (renderer == null) throw new System.InvalidOperationException("몸 스킨 렌더러를 못 찾았다.");

            return probe;
        }

        /// <summary>한 축 한 방향으로 벌리며 예산을 넘기 직전 각을 찾는다.</summary>
        private static int Sweep(
            Transform bone,
            Vector3 localAxis,
            int sign,
            LastShiftRagdollSkinFollow follow,
            SkinnedMeshRenderer renderer,
            Vector3[] rest,
            int[] triangles)
        {
            var saved = bone.rotation;
            var last = 0;
            for (var degrees = StepDegrees; degrees <= MaxAngleDegrees; degrees += StepDegrees)
            {
                bone.rotation = Quaternion.AngleAxis(degrees * sign, saved * localAxis) * saved;
                follow.Apply();
                if (!WithinBudget(renderer, rest, triangles)) break;
                last = degrees;
            }

            bone.rotation = saved;
            follow.Apply();
            return last;
        }

        private static bool WithinBudget(SkinnedMeshRenderer renderer, Vector3[] rest, int[] triangles)
        {
            var baked = new Mesh();
            renderer.BakeMesh(baked, false);
            var posed = baked.vertices;

            var torn = 0;
            var worst = 0f;
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var ratio = 0f;
                for (var k = 0; k < 3; k++)
                {
                    var a = triangles[t + k];
                    var b = triangles[t + ((k + 1) % 3)];
                    var restLength = Vector3.Distance(rest[a], rest[b]);
                    if (restLength < MinRestEdge) continue;
                    ratio = Mathf.Max(ratio, Vector3.Distance(posed[a], posed[b]) / restLength);
                }

                if (ratio < TornRatio) continue;
                torn++;
                worst = Mathf.Max(worst, ratio);
            }

            Object.DestroyImmediate(baked);
            return torn <= TornTriangleBudget && worst <= WorstStretchBudget;
        }

        private static Dictionary<string, Transform> Index(GameObject root)
        {
            var map = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) map[t.name] = t;
            return map;
        }

        private static Tolerance Tighter(Tolerance a, Tolerance b) => new Tolerance(
            Mathf.Min(a.Swing1, b.Swing1),
            Mathf.Min(a.Swing2, b.Swing2),
            Mathf.Max(a.TwistLow, b.TwistLow),
            Mathf.Min(a.TwistHigh, b.TwistHigh));

        private static int Floor5(int degrees) => degrees / 5 * 5;

        /// <summary>좌우를 한 종류로 묶는다.</summary>
        public static string KindOf(string boneName) =>
            boneName.Replace(".L", string.Empty).Replace(".R", string.Empty);
    }
}
