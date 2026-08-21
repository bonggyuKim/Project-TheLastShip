using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 넘어뜨린 뒤 <b>어느 자유도가 몇 도 새는지</b>와 <b>몸이 얼마나 납작해지는지</b>를 잰다.
    ///
    /// <b><see cref="LastShiftRagdollStressProbe"/> 를 왜 못 쓰나.</b> 그쪽 <c>JointTracker</c> 는
    /// 상대 회전의 <b>크기 하나</b>를 재고, 그것을 <c>max(스윙) + max(비틀림)</c> 이라는 합계
    /// 예산에 나눈다. 엉덩이의 스윙 콘이 30/10 인데 비틀림이 -20..70 이면 예산이 100도가 되고,
    /// 그래서 <b>다리가 스윙으로 74도 벌어져도 "0.74배, 한계 안"</b> 으로 찍힌다. 실제로 그렇게
    /// 찍혔다(2026-08-21 base 측정). 자유도를 안 나누면 새는 자유도를 못 본다.
    ///
    /// 여기서는 상대 회전을 조인트 프레임에서 <b>비틀림·스윙1·스윙2 로 분해</b>해서 각각 제 한계와
    /// 견준다. 경첩은 축 둘레 각과 <b>축 밖으로 샌 각</b>을 따로 적는다 — 경첩이 축 밖으로 새면
    /// 그것은 솔버가 못 버틴 것이지 한계가 넓은 것이 아니다.
    ///
    /// <b>공처럼 뭉쳤는가</b>는 관절 각으로 안 나온다. 한계 안에서 여럿이 같이 접히면 각은 전부
    /// 멀쩡한데 실루엣만 사라진다. 그래서 <b>몸통 길이</b>(골반→머리)와 팔다리 뻗음을 정지 대비
    /// 비로 같이 적는다. 사용자가 말한 "배가 공처럼 뭉쳐 다리가 안 보인다"가 이 두 값이다.
    ///
    /// <b>물리 설정을 인자로 덮어쓸 수 있다.</b> 후보를 하나 바꿀 때마다 프리팹을 고치고 다시
    /// 구우면 무엇이 효과가 있었는지 못 가른다. 한 번 컴파일하고 인자만 바꿔 쓸어 본다.
    /// </summary>
    public static class LastShiftRagdollCollapseProbe
    {
        private static float Step => Time.fixedDeltaTime;

        private static readonly (string Name, float Lift, float Shove)[] Cases =
        {
            ("fall", 0f, 0f),
            ("shove", 0f, 3.4f),
            ("drop", 1.2f, 2f)
        };

        public static void RunForAutomation()
        {
            var seconds = FloatArg("-collapseSeconds", 4f);
            var tag = StringArg("-collapseTag", "base");

            var summary = new List<string>
            {
                "case,worstJoint,worstAxis,worstDeg,worstLimit,worstExcess,minTorsoSpan,minLimbSpan,foldedLimb,settleTorsoSpan,settleLimbSpan"
            };

            foreach (var (name, lift, shove) in Cases)
                summary.Add($"{name},{Run(seconds, lift, shove, $"tmp/ragdoll-collapse-{tag}-{name}.csv")}");

            var path = Path.GetFullPath($"tmp/ragdoll-collapse-{tag}-summary.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, summary);
            Debug.Log("[LAST_SHIFT_RAGDOLL_COLLAPSE]\n" + string.Join("\n", summary));
        }

        public static string Run(float seconds, float lift, float shove, string csvPath)
        {
            EditorSceneManager.OpenScene(LastShiftRagdollLabScene.ScenePath, OpenSceneMode.Single);

            var subject = GameObject.Find("RagdollSubject");
            if (subject == null) throw new System.InvalidOperationException("RagdollSubject 를 못 찾았다.");

            subject.transform.position += Vector3.up * lift;

            var selfCollision = subject.GetComponent<LastShiftRagdollSelfCollision>();
            var follow = subject.GetComponent<LastShiftRagdollSkinFollow>();
            if (selfCollision != null) selfCollision.Apply();
            // 플레이가 아니면 Awake 가 안 도므로 바디 안정화 설정도 손으로 밟는다.
            // 이것을 빼면 검사만 Unity 기본 솔버로 돌아 실제와 다른 값이 나온다.
            var bodySetup = subject.GetComponent<LastShiftRagdollBodySetup>();
            if (bodySetup != null) bodySetup.Apply();
            if (follow != null) follow.Capture();

            var bodies = new List<Rigidbody>(subject.GetComponentsInChildren<Rigidbody>(true));
            ApplyOverrides(bodies, subject);

            var trackers = new List<LastShiftRagdollJointLimitTracker>();
            foreach (var joint in subject.GetComponentsInChildren<Joint>(true))
            {
                if (joint.connectedBody == null) continue;
                trackers.Add(new LastShiftRagdollJointLimitTracker(joint));
            }

            var pelvis = Find(bodies, LastShiftRagdollRig.PelvisBoneName);
            var head = Find(bodies, LastShiftRagdollRig.HeadBoneName);
            var torsoRest = pelvis != null && head != null
                ? Mathf.Max(0.001f, Vector3.Distance(pelvis.transform.position, head.transform.position))
                : 1f;

            var limbs = new List<SpanTracker>();
            foreach (var name in new[] { "DEF-hand.L", "DEF-hand.R", "DEF-foot.L", "DEF-foot.R" })
            {
                var limb = Find(bodies, name);
                if (limb != null && pelvis != null) limbs.Add(new SpanTracker(limb, pelvis));
            }

            var previous = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;

            var rows = new List<string> { "t,torsoSpan,minLimbSpan,foldedLimb,worstJoint,worstAxis,worstDeg,worstLimit,worstExcess" };
            var minTorso = 1f;
            var minSpan = 1f;
            var foldedLimb = "-";
            var peakExcess = 0f;
            var peakJoint = "-";
            var peakAxis = "-";
            var peakDeg = 0f;
            var peakLimit = 0f;
            var settleTorso = 1f;
            var settleSpan = 1f;

            try
            {
                if (shove > 0f)
                {
                    var direction = new Vector3(0.6f, 0.25f, 0.76f).normalized;
                    foreach (var body in bodies)
                    {
                        if (body == null || body.isKinematic) continue;
                        body.AddForce(direction * shove, ForceMode.VelocityChange);
                    }
                }

                var steps = Mathf.CeilToInt(seconds / Step);
                for (var s = 0; s <= steps; s++)
                {
                    if (s > 0)
                    {
                        UnityEngine.Physics.Simulate(Step);
                        if (follow != null) follow.Apply();
                    }

                    var excess = 0f;
                    var joint = "-";
                    var axis = "-";
                    var deg = 0f;
                    var limit = 0f;
                    foreach (var tracker in trackers)
                    {
                        var value = tracker.Sample(out var axisName, out var current, out var budget);
                        if (value <= excess) continue;
                        excess = value;
                        joint = tracker.Name;
                        axis = axisName;
                        deg = current;
                        limit = budget;
                    }

                    var torso = pelvis != null && head != null
                        ? Vector3.Distance(pelvis.transform.position, head.transform.position) / torsoRest
                        : 1f;

                    var span = 1f;
                    var limbName = "-";
                    foreach (var limb in limbs)
                    {
                        var ratio = limb.SpanRatio();
                        if (ratio >= span) continue;
                        span = ratio;
                        limbName = limb.Name;
                    }

                    if (excess > peakExcess)
                    {
                        peakExcess = excess;
                        peakJoint = joint;
                        peakAxis = axis;
                        peakDeg = deg;
                        peakLimit = limit;
                    }

                    if (torso < minTorso) minTorso = torso;
                    if (span < minSpan)
                    {
                        minSpan = span;
                        foldedLimb = limbName;
                    }

                    settleTorso = torso;
                    settleSpan = span;

                    rows.Add(string.Join(",", new[]
                    {
                        (s * Step).ToString("F3", CultureInfo.InvariantCulture),
                        torso.ToString("F3", CultureInfo.InvariantCulture),
                        span.ToString("F3", CultureInfo.InvariantCulture),
                        limbName,
                        joint,
                        axis,
                        deg.ToString("F1", CultureInfo.InvariantCulture),
                        limit.ToString("F1", CultureInfo.InvariantCulture),
                        excess.ToString("F1", CultureInfo.InvariantCulture)
                    }));
                }
            }
            finally
            {
                UnityEngine.Physics.simulationMode = previous;
            }

            var detail = new List<string> { "joint,kind,worstAxis,worstDeg,limit,excess,twistDeg,swing1Deg,swing2Deg" };
            foreach (var tracker in trackers)
            {
                detail.Add(string.Join(",", new[]
                {
                    tracker.Name,
                    tracker.IsHinge ? "hinge" : "ball",
                    tracker.WorstAxis,
                    tracker.WorstDegrees.ToString("F1", CultureInfo.InvariantCulture),
                    tracker.WorstLimit.ToString("F1", CultureInfo.InvariantCulture),
                    tracker.WorstExcess.ToString("F1", CultureInfo.InvariantCulture),
                    tracker.Twist.ToString("F1", CultureInfo.InvariantCulture),
                    tracker.Swing1.ToString("F1", CultureInfo.InvariantCulture),
                    tracker.Swing2.ToString("F1", CultureInfo.InvariantCulture)
                }));
            }

            var full = Path.GetFullPath(csvPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllLines(full, rows);
            File.WriteAllLines(Path.ChangeExtension(full, ".joints.csv"), detail);

            Debug.Log($"[LAST_SHIFT_RAGDOLL_COLLAPSE_CASE] lift={lift:F2} shove={shove:F2} "
                      + $"worst={peakJoint}.{peakAxis} {peakDeg:F0}/{peakLimit:F0} (+{peakExcess:F0}deg) "
                      + $"minTorsoSpan={minTorso:F3} minLimbSpan={minSpan:F3}({foldedLimb}) "
                      + $"settle torso={settleTorso:F3} limb={settleSpan:F3}\n"
                      + string.Join("\n", detail));

            return string.Join(",", new[]
            {
                peakJoint,
                peakAxis,
                peakDeg.ToString("F1", CultureInfo.InvariantCulture),
                peakLimit.ToString("F1", CultureInfo.InvariantCulture),
                peakExcess.ToString("F1", CultureInfo.InvariantCulture),
                minTorso.ToString("F3", CultureInfo.InvariantCulture),
                minSpan.ToString("F3", CultureInfo.InvariantCulture),
                foldedLimb,
                settleTorso.ToString("F3", CultureInfo.InvariantCulture),
                settleSpan.ToString("F3", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// 후보 설정을 인자로 덮어쓴다. <b>프리팹은 안 건드린다</b> — 씬을 연 뒤 메모리에서만
        /// 바꾸므로 무엇이 효과가 있었는지 한 번에 하나씩 가를 수 있다.
        /// </summary>
        private static void ApplyOverrides(List<Rigidbody> bodies, GameObject subject)
        {
            var solver = IntArg("-collapseSolver", 0);
            var velocity = IntArg("-collapseVelocityIter", 0);
            var hingeBoost = IntArg("-collapseHingeBoost", 1);
            var depenetration = FloatArg("-collapseDepenetration", 0f);
            var angularDamping = FloatArg("-collapseAngularDamping", 0f);
            var projection = HasArg("-collapseProjection");
            var continuous = HasArg("-collapseContinuous");

            // 경첩 부위는 반복을 따로 올린다 — 프록시 빌더가 같은 이유로 그렇게 한다.
            var hinges = new HashSet<Rigidbody>();
            foreach (var hinge in subject.GetComponentsInChildren<HingeJoint>(true))
            {
                var body = hinge.GetComponent<Rigidbody>();
                if (body != null) hinges.Add(body);
            }

            foreach (var body in bodies)
            {
                if (body == null) continue;
                var boost = hinges.Contains(body) ? Mathf.Max(1, hingeBoost) : 1;
                if (solver > 0) body.solverIterations = solver * boost;
                if (velocity > 0) body.solverVelocityIterations = velocity * boost;
                if (depenetration > 0f) body.maxDepenetrationVelocity = depenetration;
                if (angularDamping > 0f) body.angularDamping = angularDamping;
                if (continuous) body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            if (HasArg("-collapsePreprocess"))
                foreach (var joint in subject.GetComponentsInChildren<Joint>(true))
                    joint.enablePreprocessing = true;

            var connectedMassScale = FloatArg("-collapseConnectedMassScale", 0f);
            if (connectedMassScale > 0f)
                foreach (var joint in subject.GetComponentsInChildren<Joint>(true))
                    joint.connectedMassScale = connectedMassScale;

            if (!projection) return;
            foreach (var joint in subject.GetComponentsInChildren<CharacterJoint>(true))
            {
                joint.enableProjection = true;
                joint.projectionDistance = 0.01f;
                joint.projectionAngle = 5f;
            }
        }

        private sealed class SpanTracker
        {
            public SpanTracker(Rigidbody limb, Rigidbody pelvis)
            {
                _limb = limb;
                _pelvis = pelvis;
                Name = limb.name;
                _rest = Mathf.Max(0.001f, Vector3.Distance(limb.transform.position, pelvis.transform.position));
            }

            private readonly Rigidbody _limb;
            private readonly Rigidbody _pelvis;
            private readonly float _rest;

            public string Name { get; }

            public float SpanRatio() => _limb == null || _pelvis == null
                ? 1f
                : Vector3.Distance(_limb.transform.position, _pelvis.transform.position) / _rest;
        }

        private static Rigidbody Find(List<Rigidbody> bodies, string name)
        {
            foreach (var body in bodies)
                if (body != null && body.name == name) return body;
            return null;
        }

        private static bool HasArg(string name)
        {
            foreach (var arg in System.Environment.GetCommandLineArgs())
                if (arg == name) return true;
            return false;
        }

        private static string StringArg(string name, string fallback)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return fallback;
        }

        private static float FloatArg(string name, float fallback) =>
            float.TryParse(StringArg(name, null), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;

        private static int IntArg(string name, int fallback) =>
            int.TryParse(StringArg(name, null), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
    }
}
