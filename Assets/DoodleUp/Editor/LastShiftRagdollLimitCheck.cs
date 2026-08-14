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
                    rest.Add(ragdoll.Bodies[spec.Part].transform.localRotation);
                }

                var direction = tuning.ImpactDirection(LastShiftRagdollLab.DefaultImpactHeading);
                if (alsoPushWholeBody) ragdoll.ApplyVelocityChange(direction * tuning.BodyCheckSpeed);
                ragdoll.ApplyImpulse(target, direction * impulse(tuning));

                var worst = new float[parts.Count];
                for (var s = 0; s < Mathf.CeilToInt(5f / step); s++)
                {
                    ragdoll.StepPhysics(step);
                    UnityEngine.Physics.Simulate(step);

                    for (var i = 0; i < parts.Count; i++)
                    {
                        var delta = Quaternion.Inverse(rest[i]) * ragdoll.Bodies[parts[i]].transform.localRotation;
                        worst[i] = Mathf.Max(worst[i], Quaternion.Angle(Quaternion.identity, delta));
                    }
                }

                var overshoot = 0f;
                var line = new System.Text.StringBuilder();
                for (var i = 0; i < parts.Count; i++)
                {
                    var spec = LastShiftRagdollRig.SpecOf(parts[i]);
                    var allowed = Mathf.Max(1f, Mathf.Max(spec.Swing1Limit, Mathf.Max(spec.Swing2Limit, spec.TwistLimit)));
                    overshoot = Mathf.Max(overshoot, worst[i] / allowed);
                    line.Append($"{spec.BoneName}={worst[i].ToString("F0", CultureInfo.InvariantCulture)}/{allowed:F0}");
                    line.Append(spec.IsHinge ? "(hinge) " : " ");
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
    }
}
