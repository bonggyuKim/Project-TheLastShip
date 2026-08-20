using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 손으로 잡은 래그돌 프리팹의 <b>조인트 축</b>을 뼈 배치에서 다시 세운다.
    ///
    /// <b>무엇이 틀렸었나.</b> 프리팹의 조인트는 <see cref="LastShiftRagdollJointFrame"/> 규칙을
    /// 한 번도 안 거쳤다. 실측(2026-08-21)으로 비틀림 축과 뼈 방향의 내적이 엉덩이 <c>0.21</c>,
    /// 어깨 <c>0.55</c>, 허리·목 <c>0.00</c> 이었다 — 1.0 이어야 하는 값이다. 축이 어긋나면
    /// 한계가 엉뚱한 자유도를 막고, 정작 접히는 방향은 안 막는다. 그래서 넘어질 때 어느 관절도
    /// "한계 초과" 로 안 찍히면서 팔다리가 몸통까지 접혀 들어갔다(왼발이 골반까지 거리의 19%).
    ///
    /// <b>한계 숫자는 안 바꾼다.</b> 프리팹의 한계는 <see cref="LastShiftRagdollSkinLimits"/> 가
    /// <c>min(원본, 스킨여유)</c> 로 이 프리팹에 맞춰 잡아 둔 값이다. 축과 한계를 한꺼번에 바꾸면
    /// 나아졌는지 나빠졌는지를 못 가른다. 여기서는 <b>축만</b> 고친다.
    ///
    /// <b>무릎·팔꿈치는 경첩으로 바꾼다.</b> 그 둘의 한계는 스윙 5/5 에 비틀림 80~90도인데,
    /// 이것은 "비틀림 축을 굽힘 축으로 쓰는 경첩"을 <see cref="CharacterJoint"/> 로 흉내 낸 것이다.
    /// 축을 규칙대로 뼈 방향에 맞추면 그 흉내가 깨져 다리가 막대가 된다. 프록시 빌더는 이미
    /// 같은 이유로 <see cref="HingeJoint"/> 를 쓴다(커밋 <c>62134a4</c>: CharacterJoint 로 두면
    /// 한계 85도인 무릎이 175도까지 접혔다). 같은 결정을 프리팹에도 적용한다.
    ///
    /// 되돌릴 수 있게 만들었다 — 다시 돌려도 같은 결과가 나오고(멱등), 무엇을 바꿨는지 표로 찍는다.
    /// </summary>
    public static class LastShiftRagdollJointRebuild
    {
        /// <summary>이 값 미만이면 프레임이 어긋난 것으로 본다. 비틀림 축과 뼈 방향의 내적 절댓값.</summary>
        public const float MinTwistAlignment = 0.9f;

        [MenuItem("Last Shift/Prototype/Rebuild Ragdoll Joint Frames")]
        public static void RunFromMenu() => Run(true);

        public static void RunForAutomation() => Run(!HasArg("-jointDryRun"));

        public static void Run(bool write)
        {
            var path = LastShiftRagdollLabScene.RagdollPrefabPath;
            var root = PrefabUtility.LoadPrefabContents(path);
            var log = new List<string> { "joint,kind,twistAlignBefore,twistAlignAfter,limits" };

            try
            {
                var plans = PlanAll(root);
                foreach (var plan in plans) log.Add(Apply(root, plan, write));

                if (write)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    AssetDatabase.SaveAssets();
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var output = Path.GetFullPath("tmp/ragdoll-joint-rebuild.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllLines(output, log);
            Debug.Log($"[LAST_SHIFT_RAGDOLL_JOINT_REBUILD] write={write} prefab={path}\n" + string.Join("\n", log));
        }

        /// <summary>바꿀 조인트 하나의 계획. 기존 조인트에서 읽은 것만 들고 있는다.</summary>
        private readonly struct Plan
        {
            public Plan(Transform bone, Rigidbody connected, bool hinge, float swing1, float swing2,
                float twistLow, float twistHigh, bool bendsForward)
            {
                Bone = bone;
                Connected = connected;
                Hinge = hinge;
                Swing1 = swing1;
                Swing2 = swing2;
                TwistLow = twistLow;
                TwistHigh = twistHigh;
                BendsForward = bendsForward;
            }

            public Transform Bone { get; }
            public Rigidbody Connected { get; }
            public bool Hinge { get; }
            public float Swing1 { get; }
            public float Swing2 { get; }
            public float TwistLow { get; }
            public float TwistHigh { get; }
            public bool BendsForward { get; }
        }

        private static List<Plan> PlanAll(GameObject root)
        {
            var plans = new List<Plan>();
            foreach (var joint in root.GetComponentsInChildren<CharacterJoint>(true))
            {
                if (joint.connectedBody == null) continue;
                var spec = SpecOf(joint.name);
                plans.Add(new Plan(
                    joint.transform,
                    joint.connectedBody,
                    spec.HasValue && spec.Value.IsHinge,
                    joint.swing1Limit.limit,
                    joint.swing2Limit.limit,
                    joint.lowTwistLimit.limit,
                    joint.highTwistLimit.limit,
                    spec?.HingeBendsForward ?? false));
            }

            // 이미 한 번 돌린 프리팹은 경첩이 붙어 있다. 같은 규칙으로 다시 세운다(멱등).
            foreach (var hinge in root.GetComponentsInChildren<HingeJoint>(true))
            {
                if (hinge.connectedBody == null) continue;
                var spec = SpecOf(hinge.name);
                var span = Mathf.Abs(hinge.limits.max - hinge.limits.min) - LastShiftRagdoll.HingeSlackDegrees;
                plans.Add(new Plan(
                    hinge.transform, hinge.connectedBody, true, 5f, 5f, -span, 0f,
                    spec?.HingeBendsForward ?? false));
            }

            return plans;
        }

        private static string Apply(GameObject root, Plan plan, bool write)
        {
            var bone = plan.Bone;
            var tip = TipOf(root, bone);
            var parent = plan.Connected.transform;
            var before = BeforeAlignment(bone);

            var twist = LastShiftRagdollJointFrame.TwistDirection(bone, tip, parent, root.transform.up);
            var swing = LastShiftRagdollJointFrame.SwingAxis(bone, twist, root.transform.up, root.transform.forward);

            var kind = plan.Hinge ? "hinge" : "ball";
            var limits = plan.Hinge
                ? $"bend {Mathf.Max(Mathf.Abs(plan.TwistLow), Mathf.Abs(plan.TwistHigh)):F0}"
                : $"s1 {plan.Swing1:F0} s2 {plan.Swing2:F0} tw {plan.TwistLow:F0}..{plan.TwistHigh:F0}";

            float after;
            if (!write) return Row(bone.name, kind, before, -1f, limits);

            foreach (var existing in bone.GetComponents<Joint>()) Object.DestroyImmediate(existing);

            if (plan.Hinge)
            {
                var hinge = bone.gameObject.AddComponent<HingeJoint>();
                hinge.connectedBody = plan.Connected;
                hinge.anchor = Vector3.zero;
                hinge.autoConfigureConnectedAnchor = true;
                hinge.enablePreprocessing = false;
                hinge.enableCollision = false;
                hinge.useMotor = false;
                hinge.useSpring = false;
                hinge.axis = bone.InverseTransformDirection(swing);
                hinge.useLimits = true;

                var bend = Mathf.Max(Mathf.Abs(plan.TwistLow), Mathf.Abs(plan.TwistHigh));
                hinge.limits = LastShiftRagdollJointFrame.BendsPositive(
                    twist, swing, root.transform.forward, plan.BendsForward)
                    ? new JointLimits { min = -LastShiftRagdoll.HingeSlackDegrees, max = bend }
                    : new JointLimits { min = -bend, max = LastShiftRagdoll.HingeSlackDegrees };

                // 경첩 축은 뼈에 <b>수직</b>이어야 맞다. 볼 조인트와 기준이 반대라 따로 적는다.
                after = 1f - Mathf.Abs(Vector3.Dot(swing, twist));
            }
            else
            {
                var ball = bone.gameObject.AddComponent<CharacterJoint>();
                ball.connectedBody = plan.Connected;
                ball.anchor = Vector3.zero;
                ball.autoConfigureConnectedAnchor = true;
                ball.enablePreprocessing = false;
                ball.enableProjection = false;
                ball.enableCollision = false;
                ball.axis = bone.InverseTransformDirection(twist);
                ball.swingAxis = bone.InverseTransformDirection(swing);
                ball.lowTwistLimit = new SoftJointLimit { limit = plan.TwistLow };
                ball.highTwistLimit = new SoftJointLimit { limit = plan.TwistHigh };
                ball.swing1Limit = new SoftJointLimit { limit = plan.Swing1 };
                ball.swing2Limit = new SoftJointLimit { limit = plan.Swing2 };

                after = LastShiftRagdollJointFrame.TwistAlignment(bone, tip, parent, ball.axis);
            }

            return Row(bone.name, kind, before, after, limits);
        }

        private static string Row(string name, string kind, float before, float after, string limits) =>
            string.Join(",", new[]
            {
                name,
                kind,
                before.ToString("F2", CultureInfo.InvariantCulture),
                after.ToString("F2", CultureInfo.InvariantCulture),
                limits
            });

        private static float BeforeAlignment(Transform bone)
        {
            var root = bone.root;
            var tip = TipOf(root.gameObject, bone);
            var character = bone.GetComponent<CharacterJoint>();
            if (character != null)
            {
                return LastShiftRagdollJointFrame.TwistAlignment(
                    bone, tip, character.connectedBody != null ? character.connectedBody.transform : null,
                    character.axis);
            }

            var hinge = bone.GetComponent<HingeJoint>();
            if (hinge == null) return -1f;

            var twist = LastShiftRagdollJointFrame.TwistDirection(
                bone, tip, hinge.connectedBody != null ? hinge.connectedBody.transform : null, Vector3.up);
            return 1f - Mathf.Abs(Vector3.Dot(bone.TransformDirection(hinge.axis).normalized, twist));
        }

        /// <summary>
        /// 뼈가 뻗은 방향을 가리키는 끝 뼈. 표에 있으면 표의 값을(빌더와 같은 것을 쓰려고),
        /// 없으면 계층의 첫 변형본 자식을 쓴다. 콜라이더 홀더(<c>*_Col</c>)는 뼈가 아니라 제외한다.
        /// </summary>
        private static Transform TipOf(GameObject root, Transform bone)
        {
            var spec = SpecOf(bone.name);
            if (spec.HasValue && spec.Value.TipBoneName != null)
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == spec.Value.TipBoneName) return t;
            }

            foreach (Transform child in bone)
            {
                // 콜라이더 홀더(`*_Col`)는 뼈가 아니고, 소프트 변형본(`*.soft.*`)은 부모에 매달린
                // 살덩이라 방향이 제멋대로다 — 머리에서 이것을 집으면 목 축이 통째로 돌아간다.
                if (!child.name.StartsWith("DEF-")) continue;
                if (child.name.EndsWith("_Col") || child.name.Contains(".soft.")) continue;
                return child;
            }

            return null;
        }

        private static LastShiftRagdollBone? SpecOf(string boneName)
        {
            foreach (var spec in LastShiftRagdollRig.Bones)
                if (spec.BoneName == boneName) return spec;
            return null;
        }

        private static bool HasArg(string name)
        {
            foreach (var arg in System.Environment.GetCommandLineArgs())
                if (arg == name) return true;
            return false;
        }
    }
}
