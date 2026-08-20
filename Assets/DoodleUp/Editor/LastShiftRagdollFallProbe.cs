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
    /// 낙하 한 번을 헤드리스로 돌려 <b>메시가 언제 무너지는지</b>를 시간축으로 잰다.
    ///
    /// <b>왜 새로 만드나.</b> <see cref="LastShiftSkinToleranceProbe"/> 는 물리를 안 쓰고 뼈 하나만
    /// 손으로 돌린다 — "이 각까지 버틴다"는 재지만 "떨어뜨리면 무너진다"는 못 잡는다.
    /// <see cref="LastShiftRagdollLimitCheck"/> 는 관절 각만 보고 스킨은 안 본다. 사용자가 본 것은
    /// 그 둘 다 못 보는 자리에 있었다.
    ///
    /// <b>씬을 그대로 쓴다.</b> <see cref="LastShiftRagdollLabScene"/> 빌더는 FBX 를 다시 심으므로
    /// 손으로 잡아 둔 콜라이더·조인트가 사라진다. 사용자가 여는 것은 저장된 씬이라 그것을 연다.
    /// 그 씬에는 조작기도 저중력도 없다 — Play 를 누르면 지구 중력으로 그냥 무너지고, 그것이
    /// 사용자가 본 장면이다.
    ///
    /// <b>솔버 설정은 원인이 아니다(2026-08-21 실측, 다시 훑지 말 것).</b> 이 프리팹은 프록시
    /// 빌더를 안 거쳐 Unity 기본값(솔버 6/1 · 디페네트레이션 10 m/s · Discrete · 프로젝션 꺼짐)으로
    /// 돌고 있어서 그것이 원인처럼 보였는데, 다섯 가지를 따로 켜 재 보니 늘어난 삼각형은
    /// 436 → 최선 413 으로 사실상 그대로였다. 프로젝션은 조인트 이탈을 8.5cm → 5.0cm 로 정확히
    /// 묶었는데도 찢어짐이 안 줄었다 — <b>관절 분리가 원인이 아니라는 뜻</b>이고, 그 대신 정착
    /// 상태를 나쁘게 만들었다(30 → 149). 그래서 아무것도 안 켠다.
    /// </summary>
    public static class LastShiftRagdollFallProbe
    {
        /// <summary>플레이와 같은 스텝이어야 증거가 증거다. 프로젝트는 0.02(50Hz)다.</summary>
        private static float Step => Time.fixedDeltaTime;

        /// <summary>정지 대비 이 배율 이상 늘어난 모서리를 가진 삼각형을 "찢어졌다"로 센다.</summary>
        private const float TornRatio = 3f;

        /// <summary>
        /// 눈에 띄는 붕괴로 치는 배율. <b>개수만 보면 안 된다</b> — 찢어짐을 구간 전체로 펴면
        /// ×3 을 갓 넘은 삼각형이 늘면서 개수가 오히려 커지는데, 화면에서 튀는 것은 배율 쪽이다.
        /// </summary>
        private const float SevereRatio = 6f;

        /// <summary>이보다 짧은 정지 모서리는 무시한다 — 미세 삼각형이 배율을 지배한다.</summary>
        private const float MinRestEdge = 0.003f;

        private static readonly (string Name, Follow Follow)[] Variants =
        {
            ("shipping", Follow.Full),
            ("nobend", Follow.NoBend),
            ("nofollow", Follow.Off)
        };

        private static readonly (string Name, float Lift)[] Heights =
        {
            ("stand", 0f),
            ("drop050", 0.5f)
        };

        /// <summary>표현층을 얼마나 돌릴 것인가. 찢어짐이 물리 쪽인지 표현 쪽인지 가르는 스위치다.</summary>
        public enum Follow
        {
            /// <summary>씬 그대로 — 굽힘 분산 + 헬퍼 뼈.</summary>
            Full,

            /// <summary>헬퍼 뼈만. 굽힘 분산을 끈다.</summary>
            NoBend,

            /// <summary>표현층을 아예 안 돌린다. 물리 라이트백만 남는다.</summary>
            Off
        }

        public static void RunForAutomation()
        {
            var seconds = FloatArg("-fallSeconds", 3f);
            var tag = StringArg("-fallTag", "fall");
            var summary = new List<string>
            {
                "variant,height,peakTorn,peakSevere,peakStretch,peakGap,settledTorn,settledSevere,settledStretch"
            };

            foreach (var (variantName, follow) in Variants)
            foreach (var (heightName, lift) in Heights)
            {
                var rows = Run(seconds, lift, $"tmp/ragdoll-fall-{tag}-{variantName}-{heightName}.csv", follow);
                summary.Add($"{variantName},{heightName},{Peaks(rows)}");
            }

            var path = Path.GetFullPath($"tmp/ragdoll-fall-{tag}-summary.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, summary);
            Debug.Log("[LAST_SHIFT_RAGDOLL_FALL_SUMMARY]\n" + string.Join("\n", summary));
        }

        [MenuItem("Last Shift/Prototype/Probe Ragdoll Fall")]
        private static void RunFromMenu() => Run(3f, 0f, "tmp/ragdoll-fall.csv", Follow.Full);

        /// <summary>마지막 0.5초를 "정착"으로 본다 — 무너진 채 굳었는지는 그 구간이 말해 준다.</summary>
        private static string Peaks(List<Sample> rows)
        {
            var peakTorn = 0;
            var peakSevere = 0;
            var peakStretch = 0f;
            var peakGap = 0f;
            var settledTorn = 0;
            var settledSevere = 0;
            var settledStretch = 0f;
            var last = rows.Count > 0 ? rows[rows.Count - 1].Time : 0f;

            foreach (var row in rows)
            {
                peakTorn = Mathf.Max(peakTorn, row.Torn);
                peakSevere = Mathf.Max(peakSevere, row.Severe);
                peakStretch = Mathf.Max(peakStretch, row.Stretch);
                peakGap = Mathf.Max(peakGap, row.Gap);
                if (row.Time < last - 0.5f) continue;
                settledTorn = Mathf.Max(settledTorn, row.Torn);
                settledSevere = Mathf.Max(settledSevere, row.Severe);
                settledStretch = Mathf.Max(settledStretch, row.Stretch);
            }

            return string.Join(",", new[]
            {
                peakTorn.ToString(CultureInfo.InvariantCulture),
                peakSevere.ToString(CultureInfo.InvariantCulture),
                peakStretch.ToString("F2", CultureInfo.InvariantCulture),
                peakGap.ToString("F4", CultureInfo.InvariantCulture),
                settledTorn.ToString(CultureInfo.InvariantCulture),
                settledSevere.ToString(CultureInfo.InvariantCulture),
                settledStretch.ToString("F2", CultureInfo.InvariantCulture)
            });
        }

        public readonly struct Sample
        {
            public Sample(float time, int torn, int severe, float stretch, float gap)
            {
                Time = time;
                Torn = torn;
                Severe = severe;
                Stretch = stretch;
                Gap = gap;
            }

            public float Time { get; }

            public int Torn { get; }

            public int Severe { get; }

            public float Stretch { get; }

            public float Gap { get; }
        }

        public static List<Sample> Run(float seconds, float lift, string csvPath, Follow followMode)
        {
            EditorSceneManager.OpenScene(LastShiftRagdollLabScene.ScenePath, OpenSceneMode.Single);

            var subject = GameObject.Find("RagdollSubject");
            if (subject == null) throw new System.InvalidOperationException("RagdollSubject 를 못 찾았다.");

            subject.transform.position += Vector3.up * lift;

            var follow = subject.GetComponent<LastShiftRagdollSkinFollow>();
            var selfCollision = subject.GetComponent<LastShiftRagdollSelfCollision>();
            var renderer = BodyRenderer(subject);
            var rest = renderer.sharedMesh.vertices;
            var triangles = renderer.sharedMesh.triangles;

            // 에디터에서는 Awake 가 안 돈다. 플레이와 같은 순서로 손으로 밟는다.
            if (selfCollision != null) selfCollision.Apply();
            if (follow != null)
            {
                follow.DistributeBendEnabled = followMode == Follow.Full;
                follow.Capture();
            }

            var bodies = new List<Rigidbody>(subject.GetComponentsInChildren<Rigidbody>(true));
            var joints = new List<CharacterJoint>(subject.GetComponentsInChildren<CharacterJoint>(true));
            var restSeparation = new float[joints.Count];
            for (var i = 0; i < joints.Count; i++) restSeparation[i] = Separation(joints[i]);

            var previous = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;

            var rows = new List<string>
            {
                "t,rootY,maxSpeed,jointGapMax,jointGapWorst,tornTriangles,severeTriangles,worstStretch"
            };
            var samples = new List<Sample>();
            var peakTorn = -1;
            var peak = new List<string>();

            try
            {
                var steps = Mathf.CeilToInt(seconds / Step);
                for (var s = 0; s <= steps; s++)
                {
                    if (s > 0)
                    {
                        UnityEngine.Physics.Simulate(Step);
                        if (follow != null && followMode != Follow.Off) follow.Apply();
                    }

                    var maxSpeed = 0f;
                    for (var i = 0; i < bodies.Count; i++)
                    {
                        if (bodies[i] == null) continue;
                        maxSpeed = Mathf.Max(maxSpeed, bodies[i].linearVelocity.magnitude);
                    }

                    var gapMax = 0f;
                    var gapWorst = "-";
                    for (var i = 0; i < joints.Count; i++)
                    {
                        var gap = Separation(joints[i]) - restSeparation[i];
                        if (gap <= gapMax) continue;
                        gapMax = gap;
                        gapWorst = joints[i].name;
                    }

                    Measure(renderer, rest, triangles, out var torn, out var severe, out var worst);
                    samples.Add(new Sample(s * Step, torn, severe, worst, gapMax));

                    if (torn > peakTorn)
                    {
                        peakTorn = torn;
                        peak = Dump(
                            s * Step, torn, severe, worst, renderer, rest, triangles, joints, restSeparation);
                    }

                    rows.Add(string.Join(",", new[]
                    {
                        (s * Step).ToString("F3", CultureInfo.InvariantCulture),
                        subject.transform.position.y.ToString("F4", CultureInfo.InvariantCulture),
                        maxSpeed.ToString("F3", CultureInfo.InvariantCulture),
                        gapMax.ToString("F4", CultureInfo.InvariantCulture),
                        gapWorst,
                        torn.ToString(CultureInfo.InvariantCulture),
                        severe.ToString(CultureInfo.InvariantCulture),
                        worst.ToString("F2", CultureInfo.InvariantCulture)
                    }));
                }
            }
            finally
            {
                UnityEngine.Physics.simulationMode = previous;
            }

            var full = Path.GetFullPath(csvPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllLines(full, rows);
            File.WriteAllLines(Path.ChangeExtension(full, ".peak.txt"), peak);
            Debug.Log($"[LAST_SHIFT_RAGDOLL_FALL] follow={followMode} lift={lift:F2} csv={full}");
            return samples;
        }

        /// <summary>
        /// 가장 심한 프레임 하나를 통째로 적는다. 어느 <b>뼈</b>에 붙은 삼각형이 늘어났는지가
        /// 없으면 "무너졌다"까지만 알고 어디를 고칠지는 계속 추측하게 된다.
        /// </summary>
        private static List<string> Dump(
            float time,
            int torn,
            int severe,
            float worst,
            SkinnedMeshRenderer renderer,
            Vector3[] rest,
            int[] triangles,
            List<CharacterJoint> joints,
            float[] restSeparation)
        {
            var lines = new List<string>
            {
                $"t={time:F3} torn={torn} severe={severe} worstStretch={worst:F2}",
                "-- joint gaps (정지 이격 대비) --"
            };

            var gaps = new List<(string Name, float Gap, float RestGap)>();
            for (var i = 0; i < joints.Count; i++)
                gaps.Add((joints[i].name, Separation(joints[i]) - restSeparation[i], restSeparation[i]));
            gaps.Sort((a, b) => b.Gap.CompareTo(a.Gap));
            foreach (var (name, gap, restGap) in gaps)
                lines.Add($"  {name,-20}{gap * 100f,7:F2} cm   (정지 {restGap * 100f:F2} cm)");

            var baked = new Mesh();
            renderer.BakeMesh(baked, false);
            var posed = baked.vertices;
            var weights = renderer.sharedMesh.boneWeights;
            var bones = renderer.bones;

            var hits = new List<(float Ratio, int Triangle)>();
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var ratio = Stretch(rest, posed, triangles, t);
                if (ratio >= TornRatio) hits.Add((ratio, t));
            }

            hits.Sort((a, b) => b.Ratio.CompareTo(a.Ratio));

            // 어느 뼈 짝에서 갈라졌는지를 센다. 개수 순으로 봐야 한 곳인지 온몸인지가 드러난다.
            var byPair = new Dictionary<string, int>();
            foreach (var (_, t) in hits)
            {
                var names = new SortedSet<string>();
                for (var k = 0; k < 3; k++) names.Add(DominantBone(weights, bones, triangles[t + k]));
                var key = string.Join("+", names);
                byPair[key] = byPair.TryGetValue(key, out var seen) ? seen + 1 : 1;
            }

            lines.Add("-- torn triangles by dominant bone set --");
            var ranked = new List<KeyValuePair<string, int>>(byPair);
            ranked.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (var i = 0; i < ranked.Count && i < 20; i++)
                lines.Add($"  {ranked[i].Value,5}  {ranked[i].Key}");

            lines.Add("-- worst 10 triangles --");
            for (var i = 0; i < hits.Count && i < 10; i++)
            {
                var (ratio, t) = hits[i];
                var names = new List<string>();
                for (var k = 0; k < 3; k++) names.Add(DominantBone(weights, bones, triangles[t + k]));
                lines.Add($"  x{ratio,6:F2}  {string.Join(" | ", names)}");
            }

            Object.DestroyImmediate(baked);
            return lines;
        }

        private static string DominantBone(BoneWeight[] weights, Transform[] bones, int vertex)
        {
            if (weights == null || vertex >= weights.Length) return "?";
            var index = weights[vertex].boneIndex0;
            return index >= 0 && index < bones.Length && bones[index] != null ? bones[index].name : "?";
        }

        /// <summary>조인트 앵커 두 개가 월드에서 얼마나 떨어져 있는가. 벌어지면 그만큼 뼈가 늘어난다.</summary>
        private static float Separation(CharacterJoint joint)
        {
            if (joint == null || joint.connectedBody == null) return 0f;
            var here = joint.transform.TransformPoint(joint.anchor);
            var there = joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);
            return Vector3.Distance(here, there);
        }

        private static float Stretch(Vector3[] rest, Vector3[] posed, int[] triangles, int t)
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

            return ratio;
        }

        private static void Measure(
            SkinnedMeshRenderer renderer,
            Vector3[] rest,
            int[] triangles,
            out int torn,
            out int severe,
            out float worst)
        {
            var baked = new Mesh();
            renderer.BakeMesh(baked, false);
            var posed = baked.vertices;

            torn = 0;
            severe = 0;
            worst = 0f;
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var ratio = Stretch(rest, posed, triangles, t);
                if (ratio < TornRatio) continue;
                torn++;
                if (ratio >= SevereRatio) severe++;
                worst = Mathf.Max(worst, ratio);
            }

            Object.DestroyImmediate(baked);
        }

        private static SkinnedMeshRenderer BodyRenderer(GameObject subject)
        {
            SkinnedMeshRenderer body = null;
            foreach (var skin in subject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.sharedMesh != null && skin.sharedMesh.name.Contains("Body")) body = skin;

            if (body == null) throw new System.InvalidOperationException("몸 스킨 렌더러를 못 찾았다.");
            return body;
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
