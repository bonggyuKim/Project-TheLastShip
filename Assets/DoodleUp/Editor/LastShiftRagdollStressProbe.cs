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
    /// 넘어뜨려 보고 <b>관절이 한계를 지키는지</b>와 <b>팔다리가 몸통에 접혀 들어가는지</b>를 잰다.
    ///
    /// <b><see cref="LastShiftRagdollFallProbe"/> 와 무엇이 다른가.</b> 그쪽은 스킨이 얼마나
    /// 늘어나는지를 본다 — 같은 뼈 자세에서 메시가 찢어지는 문제다. 이쪽은 뼈 자세 자체가
    /// 틀렸는지를 본다. 사용자가 본 "팔다리가 몸통으로 완전히 접혀 뭉개진 덩어리"는 이쪽이다.
    ///
    /// <b><see cref="LastShiftRagdollLimitCheck"/> 와 무엇이 다른가.</b> 그쪽은
    /// <see cref="LastShiftRagdollLabScene.Build"/> 로 씬을 새로 구워 <b>프록시 빌더</b>의 래그돌을
    /// 잰다. 사용자가 여는 것은 저장된 씬이고 그 안에 든 것은 손으로 잡은 프리팹이라 조인트 구성이
    /// 다르다 — 빌더는 팔꿈치·무릎을 <see cref="HingeJoint"/> 로 두는데 프리팹은 전부
    /// <see cref="CharacterJoint"/> 다. 그래서 그 도구는 이 버그를 못 본다.
    ///
    /// <b>한계는 표가 아니라 조인트에서 읽는다.</b> 프리팹의 한계는 <see cref="LastShiftRagdollRig"/>
    /// 표와 다르다(무릎 표 85도 · 프리팹 트위스트 -80..0 에 스윙 5). 실제로 걸려 있는 값으로
    /// 재야 "한계를 넘었다"가 말이 된다.
    /// </summary>
    public static class LastShiftRagdollStressProbe
    {
        private static float Step => Time.fixedDeltaTime;

        /// <summary>몸 전체에 주는 속도 변화(m/s). 그냥 쓰러지는 것보다 세게 넘어뜨려야 한계가 드러난다.</summary>
        private static readonly (string Name, float Lift, float Shove)[] Cases =
        {
            ("fall", 0f, 0f),
            ("shove", 0f, 3.4f),
            ("drop", 1.2f, 2f)
        };

        public static void RunForAutomation()
        {
            var seconds = FloatArg("-stressSeconds", 4f);
            var tag = StringArg("-stressTag", "stress");

            var summary = new List<string>
            {
                "case,worstOvershoot,worstJoint,worstAngle,allowed,maxTorsoPenetration,buriedLimb,"
                + "minLimbSpan,foldedLimb,contactPairsIgnored,contactPairsKept"
            };

            foreach (var (name, lift, shove) in Cases)
                summary.Add($"{name},{Run(seconds, lift, shove, $"tmp/ragdoll-stress-{tag}-{name}.csv")}");

            var path = Path.GetFullPath($"tmp/ragdoll-stress-{tag}-summary.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, summary);
            Debug.Log("[LAST_SHIFT_RAGDOLL_STRESS]\n" + string.Join("\n", summary));
        }

        [MenuItem("Last Shift/Prototype/Probe Ragdoll Stress")]
        private static void RunFromMenu() => Run(4f, 0f, 3.4f, "tmp/ragdoll-stress.csv");

        /// <summary>타일 한 장 크기. 뭉개졌는지를 실루엣으로 봐야 하므로 너무 작으면 소용없다.</summary>
        private const int TileSize = 384;

        private static readonly float[] CaptureTimes = { 0f, 0.4f, 0.8f, 1.6f, 4f };

        /// <summary>
        /// 같은 시나리오를 돌리며 정해진 시각에 한 장씩 찍어 <b>콘택트 시트 한 장</b>으로 붙인다.
        ///
        /// 숫자만으로는 "웅크렸다"와 "뭉개졌다"를 못 가른다 — 발이 골반에 가까워지는 것은 무릎을
        /// 접어도 일어나는 일이라, 실루엣을 봐야 판정이 선다. 그림은 <b>한 장만</b> 만든다.
        /// </summary>
        public static void CaptureForAutomation()
        {
            var tag = StringArg("-stressTag", "after");
            foreach (var (name, lift, shove) in Cases)
                Capture(4f, lift, shove, $"output/ragdoll-stress-{tag}-{name}.png");
        }

        public static void Capture(float seconds, float lift, float shove, string pngPath)
        {
            EditorSceneManager.OpenScene(LastShiftRagdollLabScene.ScenePath, OpenSceneMode.Single);

            var subject = GameObject.Find("RagdollSubject");
            subject.transform.position += Vector3.up * lift;

            var selfCollision = subject.GetComponent<LastShiftRagdollSelfCollision>();
            var follow = subject.GetComponent<LastShiftRagdollSkinFollow>();
            if (selfCollision != null) selfCollision.Apply();
            // 플레이가 아니면 Awake 가 안 도므로 바디 안정화 설정도 손으로 밟는다.
            // 이것을 빼면 검사만 Unity 기본 솔버로 돌아 실제와 다른 값이 나온다.
            var bodySetup = subject.GetComponent<LastShiftRagdollBodySetup>();
            if (bodySetup != null) bodySetup.Apply();
            if (follow != null) follow.Capture();

            // 헤드리스에서 스킨 행렬은 프레임당 한 번만 갱신된다. 물리를 손으로 밟으며
            // Camera.Render 를 직접 부르면 그 갱신이 안 끼어들어 바인드 포즈만 찍힌다.
            foreach (var skin in subject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.updateWhenOffscreen = true;
                skin.forceMatrixRecalculationPerRender = true;
            }

            var bodies = new List<Rigidbody>(subject.GetComponentsInChildren<Rigidbody>(true));
            var pelvis = Find(bodies, LastShiftRagdollRig.PelvisBoneName);

            var cameraObject = new GameObject("StressCaptureCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            camera.allowHDR = false;

            var previous = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;

            var tiles = new List<Texture2D>();
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

                var elapsed = 0f;
                var next = 0;
                var steps = Mathf.CeilToInt(seconds / Step);
                for (var s = 0; s <= steps && next < CaptureTimes.Length; s++)
                {
                    if (s > 0)
                    {
                        UnityEngine.Physics.Simulate(Step);
                        if (follow != null) follow.Apply();
                        elapsed += Step;
                    }

                    if (elapsed + 1e-4f < CaptureTimes[next]) continue;

                    var focus = pelvis != null ? pelvis.transform.position : subject.transform.position;

                    // 밀친 <b>반대편</b>에서 본다. 미는 쪽에 두면 승무원이 카메라를 향해 오면서
                    // 문벽 뒤로 들어가고, 남는 것은 어두운 벽 다섯 장이다(2026-08-21에 실제로 그랬다).
                    var back = shove > 0f
                        ? -new Vector3(0.6f, 0f, 0.76f).normalized
                        : new Vector3(0.72f, 0f, 0.69f);
                    cameraObject.transform.position = focus + back * 2.8f + Vector3.up * 1.1f;
                    cameraObject.transform.LookAt(focus);
                    tiles.Add(Render(camera, TileSize, TileSize));
                    next++;
                }
            }
            finally
            {
                UnityEngine.Physics.simulationMode = previous;
                Object.DestroyImmediate(cameraObject);
            }

            WriteSheet(tiles, pngPath);
            foreach (var tile in tiles) Object.DestroyImmediate(tile);
            Debug.Log($"[LAST_SHIFT_RAGDOLL_STRESS_SHOT] lift={lift:F2} shove={shove:F2} tiles={tiles.Count} png={pngPath}");
        }

        private static Texture2D Render(Camera camera, int width, int height)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;

            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;

            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();

            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            return image;
        }

        private static void WriteSheet(List<Texture2D> tiles, string pngPath)
        {
            if (tiles.Count == 0) return;

            var sheet = new Texture2D(TileSize * tiles.Count, TileSize, TextureFormat.RGB24, false);
            for (var i = 0; i < tiles.Count; i++)
                sheet.SetPixels(i * TileSize, 0, TileSize, TileSize, tiles[i].GetPixels());
            sheet.Apply();

            var full = Path.GetFullPath(pngPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
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
            var joints = new List<Joint>(subject.GetComponentsInChildren<Joint>(true));
            var trackers = new List<JointTracker>();
            foreach (var joint in joints)
            {
                if (joint.connectedBody == null) continue;
                trackers.Add(new JointTracker(joint));
            }

            var pelvis = Find(bodies, LastShiftRagdollRig.PelvisBoneName);
            var limbs = new List<LimbTracker>();
            foreach (var name in new[] { "DEF-hand.L", "DEF-hand.R", "DEF-foot.L", "DEF-foot.R" })
            {
                var limb = Find(bodies, name);
                if (limb != null && pelvis != null) limbs.Add(new LimbTracker(limb, pelvis));
            }

            var torso = TorsoColliders(subject);
            var limbColliders = LimbColliders(subject);

            var previous = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;

            var rows = new List<string>
            {
                "t,worstOvershoot,worstJoint,worstAngle,allowed,torsoPenetration,buriedLimb,minLimbSpanRatio,foldedLimb"
            };
            var peakOvershoot = 0f;
            var peakJoint = "-";
            var peakAngle = 0f;
            var peakAllowed = 0f;
            var minSpan = 1f;
            var foldedLimb = "-";
            var maxBuried = 0f;
            var buriedLimb = "-";

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

                    var overshoot = 0f;
                    var joint = "-";
                    var angle = 0f;
                    var allowed = 0f;
                    foreach (var tracker in trackers)
                    {
                        var value = tracker.Overshoot(out var current, out var budget);
                        if (value <= overshoot) continue;
                        overshoot = value;
                        joint = tracker.Name;
                        angle = current;
                        allowed = budget;
                    }

                    var buried = 0f;
                    var buriedName = "-";
                    foreach (var limb in limbColliders)
                    foreach (var trunk in torso)
                    {
                        var depth = Penetration(limb, trunk);
                        if (depth <= buried) continue;
                        buried = depth;
                        buriedName = limb.transform.parent != null ? limb.transform.parent.name : limb.name;
                    }

                    var span = 1f;
                    var limbName = "-";
                    foreach (var limb in limbs)
                    {
                        var ratio = limb.SpanRatio();
                        if (ratio >= span) continue;
                        span = ratio;
                        limbName = limb.Name;
                    }

                    if (overshoot > peakOvershoot)
                    {
                        peakOvershoot = overshoot;
                        peakJoint = joint;
                        peakAngle = angle;
                        peakAllowed = allowed;
                    }

                    if (span < minSpan)
                    {
                        minSpan = span;
                        foldedLimb = limbName;
                    }

                    if (buried > maxBuried)
                    {
                        maxBuried = buried;
                        buriedLimb = buriedName;
                    }

                    rows.Add(string.Join(",", new[]
                    {
                        (s * Step).ToString("F3", CultureInfo.InvariantCulture),
                        overshoot.ToString("F2", CultureInfo.InvariantCulture),
                        joint,
                        angle.ToString("F1", CultureInfo.InvariantCulture),
                        allowed.ToString("F1", CultureInfo.InvariantCulture),
                        buried.ToString("F4", CultureInfo.InvariantCulture),
                        buriedName,
                        span.ToString("F3", CultureInfo.InvariantCulture),
                        limbName
                    }));
                }
            }
            finally
            {
                UnityEngine.Physics.simulationMode = previous;
            }

            // 마지막 프레임의 관절 전부를 따로 적는다. 어느 관절이 몇 도까지 갔는지가 없으면
            // "무너졌다" 까지만 알고 어디를 고칠지는 계속 추측하게 된다.
            var detail = new List<string> { "joint,angle,allowed,overshoot" };
            foreach (var tracker in trackers)
            {
                tracker.Worst(out var worstAngle, out var worstAllowed, out var worstOvershoot);
                detail.Add(string.Join(",", new[]
                {
                    tracker.Name,
                    worstAngle.ToString("F1", CultureInfo.InvariantCulture),
                    worstAllowed.ToString("F1", CultureInfo.InvariantCulture),
                    worstOvershoot.ToString("F2", CultureInfo.InvariantCulture)
                }));
            }

            var full = Path.GetFullPath(csvPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllLines(full, rows);
            File.WriteAllLines(Path.ChangeExtension(full, ".joints.csv"), detail);

            var ignored = selfCollision != null ? selfCollision.IgnoredPairs : -1;
            var kept = selfCollision != null ? selfCollision.KeptPairs : -1;
            Debug.Log($"[LAST_SHIFT_RAGDOLL_STRESS_CASE] lift={lift:F2} shove={shove:F2} "
                      + $"worst={peakJoint} {peakAngle:F0}/{peakAllowed:F0} ({peakOvershoot:F2}x) "
                      + $"buried={maxBuried * 100f:F1}cm({buriedLimb}) "
                      + $"minLimbSpan={minSpan:F3}({foldedLimb}) csv={full}");

            return string.Join(",", new[]
            {
                peakOvershoot.ToString("F2", CultureInfo.InvariantCulture),
                peakJoint,
                peakAngle.ToString("F1", CultureInfo.InvariantCulture),
                peakAllowed.ToString("F1", CultureInfo.InvariantCulture),
                maxBuried.ToString("F4", CultureInfo.InvariantCulture),
                buriedLimb,
                minSpan.ToString("F3", CultureInfo.InvariantCulture),
                foldedLimb,
                ignored.ToString(CultureInfo.InvariantCulture),
                kept.ToString(CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// 관절 하나가 정지 자세에서 몇 도 벌어졌는지와, 설정상 낼 수 있는 각의 비.
        ///
        /// 재는 값은 스윙과 비틀림이 <b>합성된</b> 회전이므로 상한도 합으로 잡는다
        /// (<c>angle(q1·q2) ≤ angle(q1)+angle(q2)</c>). 경첩은 한 축뿐이라 한계 폭을 그대로 쓴다.
        ///
        /// <b>이 자로는 새는 자유도를 못 본다(2026-08-21).</b> 분모가 합계라 엉덩이
        /// (스윙 <c>30/10</c> · 비틀림 <c>-20..70</c>)의 예산이 100도가 되고, 다리가 <b>스윙으로
        /// 56도</b> 벌어져도 <c>0.56배 — 한계 안</c> 으로 찍힌다. 실제로 그렇게 찍히는 동안 화면에서는
        /// 다리가 개구리처럼 퍼져 있었다. 자유도별로 갈라 보려면
        /// <see cref="LastShiftRagdollCollapseProbe"/> 와 <see cref="LastShiftRagdollJointLimitTracker"/>
        /// 를 쓴다. 여기는 앞선 측정값과 비교할 수 있게 자를 안 바꾸고 남겨 둔 것이다.
        /// 판단 기준은 <see cref="LastShiftRagdollLimitCheck"/> 와 같게 뒀다 — 두 도구가 다른 자를
        /// 쓰면 값을 비교할 수 없다.
        /// </summary>
        private sealed class JointTracker
        {
            public JointTracker(Joint joint)
            {
                _joint = joint;
                _rest = Quaternion.Inverse(joint.connectedBody.transform.rotation) * joint.transform.rotation;
                Name = joint.name;
                _allowed = Mathf.Max(1f, AllowedOf(joint));
            }

            private readonly Joint _joint;
            private readonly Quaternion _rest;
            private readonly float _allowed;
            private float _worstAngle;

            public string Name { get; }

            public float Overshoot(out float angle, out float allowed)
            {
                allowed = _allowed;
                angle = 0f;
                if (_joint == null || _joint.connectedBody == null) return 0f;

                var now = Quaternion.Inverse(_joint.connectedBody.transform.rotation) * _joint.transform.rotation;
                angle = Quaternion.Angle(_rest, now);
                _worstAngle = Mathf.Max(_worstAngle, angle);
                return angle / _allowed;
            }

            public void Worst(out float angle, out float allowed, out float overshoot)
            {
                angle = _worstAngle;
                allowed = _allowed;
                overshoot = _worstAngle / _allowed;
            }

            private static float AllowedOf(Joint joint)
            {
                switch (joint)
                {
                    case CharacterJoint character:
                        return Mathf.Max(character.swing1Limit.limit, character.swing2Limit.limit)
                               + Mathf.Max(
                                   Mathf.Abs(character.lowTwistLimit.limit),
                                   Mathf.Abs(character.highTwistLimit.limit));
                    case HingeJoint hinge:
                        return hinge.useLimits ? Mathf.Abs(hinge.limits.max - hinge.limits.min) : 360f;
                    default:
                        return 360f;
                }
            }
        }

        /// <summary>
        /// 팔다리 끝이 골반에서 얼마나 떨어져 있는가를 정지 대비 비로 본다.
        /// <b>덩어리로 뭉개졌다</b>는 이 값이 무너지는 것으로만 숫자가 된다 — 관절 각만 보면
        /// 한계 안에서 여럿이 겹쳐 접힌 경우를 못 잡는다.
        /// </summary>
        private sealed class LimbTracker
        {
            public LimbTracker(Rigidbody limb, Rigidbody pelvis)
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

            public float SpanRatio()
            {
                if (_limb == null || _pelvis == null) return 1f;
                return Vector3.Distance(_limb.transform.position, _pelvis.transform.position) / _rest;
            }
        }

        /// <summary>
        /// 몸통 콜라이더(골반·가슴). <b>뭉개졌다</b>는 팔다리가 이 안으로 얼마나 파고들었는가로만
        /// 제대로 숫자가 된다 — 발이 골반에 가까워지는 것 자체는 무릎을 접어도 일어나는 일이라,
        /// 거리만 보면 정상적인 접힘과 관통을 못 가른다(실제로 축을 고친 뒤 거리는 나빠졌는데
        /// 그림은 좋아졌다, 2026-08-21).
        /// </summary>
        private static List<Collider> TorsoColliders(GameObject subject)
        {
            var names = new HashSet<string>
            {
                LastShiftRagdollRig.PelvisBoneName, LastShiftRagdollRig.ChestBoneName
            };

            var found = new List<Collider>();
            foreach (var collider in subject.GetComponentsInChildren<Collider>(true))
            {
                var owner = collider.attachedRigidbody;
                if (owner != null && names.Contains(owner.name)) found.Add(collider);
            }

            return found;
        }

        private static List<Collider> LimbColliders(GameObject subject)
        {
            var names = new HashSet<string>
            {
                "DEF-thigh.L", "DEF-thigh.R", "DEF-shin.L", "DEF-shin.R", "DEF-foot.L", "DEF-foot.R",
                "DEF-upper_arm.L", "DEF-upper_arm.R", "DEF-forearm.L", "DEF-forearm.R",
                "DEF-hand.L", "DEF-hand.R"
            };

            var found = new List<Collider>();
            foreach (var collider in subject.GetComponentsInChildren<Collider>(true))
            {
                var owner = collider.attachedRigidbody;
                if (owner != null && names.Contains(owner.name)) found.Add(collider);
            }

            return found;
        }

        /// <summary>두 콜라이더가 파고든 깊이(m). 안 겹치면 0.</summary>
        private static float Penetration(Collider a, Collider b)
        {
            if (a == null || b == null) return 0f;
            return UnityEngine.Physics.ComputePenetration(
                a, a.transform.position, a.transform.rotation,
                b, b.transform.position, b.transform.rotation,
                out _, out var distance)
                ? distance
                : 0f;
        }

        private static Rigidbody Find(List<Rigidbody> bodies, string name)
        {
            foreach (var body in bodies)
                if (body != null && body.name == name) return body;
            return null;
        }

        private static float FloatArg(string name, float fallback)
        {
            var text = StringArg(name, null);
            return text != null && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }

        private static string StringArg(string name, string fallback)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return fallback;
        }
    }
}
