using System.Collections.Generic;
using System.Globalization;
using DoodleUp.Runtime;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>관절이 설정 한계 안에 머무는지 실측한다. 경첩 전환의 효과를 재는 자다.</summary>
    public static class LastShiftRagdollLimitCheck
    {
        public static void RunForAutomation()
        {
            Measure("headflick", LastShiftRagdollPart.Head, tuning => tuning.HeadFlickImpulse, false);
            Measure("bodycheck", LastShiftRagdollPart.Chest, tuning => tuning.BodyCheckSnapImpulse, true);
        }

        private static void Measure(
            string label,
            LastShiftRagdollPart target,
            System.Func<LastShiftRagdollTuning, float> impulse,
            bool alsoPushWholeBody)
        {
            const float step = 1f / 60f;

            LastShiftRagdollLabScene.Build();

            var subject = GameObject.Find("RagdollSubject");
            var lab = subject.GetComponent<LastShiftRagdollLab>();
            if (lab != null) Object.DestroyImmediate(lab);

            var ragdoll = subject.GetComponent<LastShiftRagdoll>();
            var tuning = LastShiftRagdollTuning.Comic();
            ragdoll.Build(tuning);

            var previousMode = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;

            try
            {
                UnityEngine.Physics.Simulate(step);
                ragdoll.ResetToRestPose();

                var parts = new List<LastShiftRagdollPart>();
                var rest = new List<Quaternion>();
                foreach (var spec in LastShiftRagdollRig.Bones)
                {
                    if (spec.IsRoot) continue;
                    parts.Add(spec.Part);
                    rest.Add(JointRotation(ragdoll, spec.Part));
                }

                var direction = tuning.ImpactDirection(LastShiftRagdollLab.DefaultImpactHeading);
                if (alsoPushWholeBody) ragdoll.ApplyVelocityChange(direction * tuning.BodyCheckSpeed);
                ragdoll.ApplyImpulse(target, direction * impulse(tuning));

                var worst = new float[parts.Count];
                // 경첩은 <b>축 둘레 각</b>도 따로 잰다. 3D 상대 회전이 한계를 넘어도 경첩 각이
                // 한계 안이면 새는 것은 경첩이 아니라 축 밖 구속(솔버 오차)이다 — 둘을 같이
                // 안 재면 어느 쪽을 고쳐야 하는지 알 수가 없다.
                var worstHinge = new float[parts.Count];
                for (var s = 0; s < Mathf.CeilToInt(5f / step); s++)
                {
                    ragdoll.StepPhysics(step);
                    UnityEngine.Physics.Simulate(step);

                    for (var i = 0; i < parts.Count; i++)
                    {
                        worst[i] = Mathf.Max(worst[i],
                            Quaternion.Angle(rest[i], JointRotation(ragdoll, parts[i])));

                        var hinge = ragdoll.Bodies[parts[i]].GetComponent<HingeJoint>();
                        if (hinge != null) worstHinge[i] = Mathf.Max(worstHinge[i], Mathf.Abs(hinge.angle));
                    }
                }

                var overshoot = 0f;
                var line = new System.Text.StringBuilder();
                for (var i = 0; i < parts.Count; i++)
                {
                    var spec = LastShiftRagdollRig.SpecOf(parts[i]);
                    var allowed = Mathf.Max(1f, AllowedAngle(spec));
                    overshoot = Mathf.Max(overshoot, worst[i] / allowed);
                    line.Append($"{spec.BoneName}={worst[i].ToString("F0", CultureInfo.InvariantCulture)}/{allowed:F0}");
                    line.Append(spec.IsHinge
                        ? $"(hinge axis={worstHinge[i].ToString("F0", CultureInfo.InvariantCulture)}) "
                        : " ");
                }

                Debug.Log($"[LAST_SHIFT_RAGDOLL_LIMIT] case={label} " +
                          $"worstOvershoot={overshoot.ToString("F2", CultureInfo.InvariantCulture)}x " +
                          $"settle={(ragdoll.SettledAtSeconds >= 0f ? ragdoll.SettledAtSeconds.ToString("F2") : "NONE")} " +
                          $"| {line}");
            }
            finally
            {
                UnityEngine.Physics.simulationMode = previousMode;
            }
        }

        /// <summary>
        /// 이 관절이 <b>설정상 낼 수 있는</b> 최대 각.
        ///
        /// 예전에는 단일 축 한계 중 최댓값을 썼는데, 재는 값은 스윙과 비틀림이 <b>합성된</b>
        /// 회전이라 비교 대상이 틀렸다. 스윙 25도 관절이 비틀림 20도를 같이 쓰면 합성 회전은
        /// 32도까지 나오는 것이 정상인데, 그것이 "25도 한계를 28% 초과" 로 찍혔다.
        /// 회전 합성의 각은 각각의 합을 넘지 않으므로(<c>angle(q1·q2) ≤ angle(q1)+angle(q2)</c>)
        /// 볼 조인트는 <c>최대 스윙 + 비틀림</c> 이 옳은 상한이다. 경첩은 한 축뿐이라
        /// 한계에 반대쪽 여유만 더한다.
        /// </summary>
        private static float AllowedAngle(LastShiftRagdollBone spec)
        {
            if (spec.IsHinge) return spec.Swing1Limit + LastShiftRagdoll.HingeSlackDegrees;
            return Mathf.Max(spec.Swing1Limit, spec.Swing2Limit) + spec.TwistLimit;
        }

        /// <summary>
        /// 관절이 실제로 벌어진 각. <b>계층 부모가 아니라 조인트가 물고 있는 부위 기준으로 잰다.</b>
        ///
        /// 예전에는 <c>transform.localRotation</c> 을 썼다. 옛 리그에서는 팔의 계층 부모가 곧
        /// 가슴(물리 바디)이라 그 값이 관절 각과 같았는데, Rigify 리그에서는 팔다리의 계층 부모가
        /// <c>ORG-shoulder.L</c> 같은 <b>물리를 안 받는 제어본</b>이다. 그러면 몸이 통째로 구르는
        /// 각이 그대로 "관절 각" 으로 찍혀서, 관절은 멀쩡한데 초과 154도 같은 숫자가 나온다.
        /// 리그 계층에 안 기대는 이 식이 두 리그 모두에서 같은 것을 잰다.
        /// </summary>
        private static Quaternion JointRotation(LastShiftRagdoll ragdoll, LastShiftRagdollPart part)
        {
            var spec = LastShiftRagdollRig.SpecOf(part);
            var body = ragdoll.Bodies[part].transform.rotation;
            if (spec.IsRoot) return body;
            return Quaternion.Inverse(ragdoll.Bodies[spec.Parent].transform.rotation) * body;
        }
    }
}
