using System.Collections.Generic;
using DoodleUp.Editor;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 씬에 실제로 들어가는 래그돌 프리팹의 <b>조인트 프레임</b>을 고정한다.
    ///
    /// <b>이 파일이 막는 사고.</b> 프리팹의 조인트 축은 뼈 배치와 무관하게 잡혀 있었다 —
    /// 비틀림 축과 뼈 방향의 내적이 엉덩이 <c>0.21</c>, 어깨 <c>0.55</c>, 허리·목 <c>0.00</c>
    /// (1.0 이어야 하는 값이다). 그러면 한계가 엉뚱한 자유도를 막고 정작 접히는 방향은 안 막아서,
    /// 넘어질 때 <b>어느 관절도 "한계 초과" 로 안 찍히면서</b> 팔다리가 몸통까지 접혀 들어간다.
    /// 콘솔에는 끝까지 아무 말도 안 나온다 — 그래서 검사가 필요하다.
    ///
    /// <b>PlayMode 가 아니라 여기 있는 이유.</b> 랩 씬(<c>LAST_SHIFT_RAGDOLL_LAB</c>)은 빌드
    /// 세팅에 없어서 PlayMode 에서 <c>SceneManager.LoadSceneAsync</c> 로 못 연다. 넣으면 프로토타입
    /// 씬이 실제 빌드에 실린다. EditMode 에서 <c>Physics.Simulate</c> 를 손으로 밟으면 같은 PhysX 를
    /// 같은 스텝으로 돌릴 수 있고, 프레임 타이밍에 안 흔들려 오히려 더 결정적이다.
    /// </summary>
    public sealed class LastShiftRagdollJointFrameTests
    {
        /// <summary>
        /// 넘어진 뒤에도 이 배수를 넘으면 안 된다. 실측(2026-08-21) 최악이 발목 <c>1.64</c>배라
        /// 여유를 두고 잡았다 — 잡으려는 것은 미세한 초과가 아니라 <b>무릎이 175도까지 접히는</b>
        /// 종류의 붕괴다(커밋 <c>62134a4</c> 가 프록시 빌더에서 겪은 그것).
        /// </summary>
        private const float MaxOvershoot = 2.0f;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private SimulationMode _previousMode;
        private bool _modeChanged;

        [TearDown]
        public void TearDown()
        {
            if (_modeChanged) UnityEngine.Physics.simulationMode = _previousMode;
            _modeChanged = false;
            foreach (var spawned in _spawned)
                if (spawned != null) Object.DestroyImmediate(spawned);
            _spawned.Clear();
        }

        [Test]
        public void EveryBallJointTwistsAroundItsOwnBone()
        {
            var subject = Spawn();

            foreach (var joint in subject.GetComponentsInChildren<CharacterJoint>(true))
            {
                var alignment = LastShiftRagdollJointFrame.TwistAlignment(
                    joint.transform, TipOf(subject, joint.transform), ParentOf(joint), joint.axis);

                Assert.That(alignment, Is.GreaterThanOrEqualTo(LastShiftRagdollJointRebuild.MinTwistAlignment),
                    $"'{joint.name}' 의 비틀림 축이 뼈에서 벗어났다(내적 {alignment:F2}). "
                    + "한계가 엉뚱한 자유도를 막게 된다 — Last Shift/Prototype/Rebuild Ragdoll Joint Frames 를 돌려라.");
            }
        }

        [Test]
        public void KneesAndElbowsAreHingesNotPinchedCones()
        {
            // 스윙 5/5 에 비틀림 80~90도인 CharacterJoint 는 "경첩 흉내" 다. PhysX 는 그렇게
            // 찌그러진 콘에서 한계를 못 지킨다 — 프록시 빌더가 이미 같은 이유로 경첩을 쓴다.
            var subject = Spawn();

            var hinged = new List<string>();
            foreach (var hinge in subject.GetComponentsInChildren<HingeJoint>(true)) hinged.Add(hinge.name);

            foreach (var name in new[] { "DEF-shin.L", "DEF-shin.R", "DEF-forearm.L", "DEF-forearm.R" })
            {
                Assert.That(hinged, Contains.Item(name),
                    $"'{name}' 가 경첩이 아니다 — 무릎·팔꿈치가 볼 조인트로 되돌아갔다.");
            }
        }

        [Test]
        public void HingeAxesStayPerpendicularToTheirBone()
        {
            var subject = Spawn();

            foreach (var hinge in subject.GetComponentsInChildren<HingeJoint>(true))
            {
                var bone = hinge.transform;
                var twist = LastShiftRagdollJointFrame.TwistDirection(
                    bone, TipOf(subject, bone), ParentOf(hinge), Vector3.up);
                var axis = bone.TransformDirection(hinge.axis).normalized;

                Assert.That(Mathf.Abs(Vector3.Dot(axis, twist)), Is.LessThan(0.2f),
                    $"'{hinge.name}' 의 경첩 축이 뼈와 나란해졌다 — 무릎이 제 축 둘레로 도는 셈이 된다.");
                Assert.That(hinge.useLimits, Is.True, $"'{hinge.name}' 에 한계가 안 걸려 있다.");
            }
        }

        [Test]
        public void ShovedRagdollKeepsEveryJointWithinItsBudget()
        {
            // 밀쳐 넘어뜨리고 4초를 실제로 돌린다. 관절이 설정한 각의 두 배를 넘으면 그 자세는
            // 화면에서 "팔다리가 몸통에 뭉개진 덩어리" 로 보인다 — 사용자가 본 그것이다.
            var subject = Spawn();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(floor);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);

            var joints = new List<Joint>(subject.GetComponentsInChildren<Joint>(true));
            var rests = new List<Quaternion>();
            var budgets = new List<float>();
            foreach (var joint in joints)
            {
                rests.Add(Quaternion.Inverse(joint.connectedBody.transform.rotation) * joint.transform.rotation);
                budgets.Add(Mathf.Max(1f, BudgetOf(joint)));
            }

            _previousMode = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;
            _modeChanged = true;

            var shove = new Vector3(0.6f, 0.25f, 0.76f).normalized * 3.4f;
            foreach (var body in subject.GetComponentsInChildren<Rigidbody>(true))
            {
                if (body.isKinematic) continue;
                body.AddForce(shove, ForceMode.VelocityChange);
            }

            var worst = 0f;
            var worstName = "-";
            var step = Time.fixedDeltaTime;
            for (var s = 0; s < Mathf.CeilToInt(4f / step); s++)
            {
                UnityEngine.Physics.Simulate(step);

                for (var i = 0; i < joints.Count; i++)
                {
                    var now = Quaternion.Inverse(joints[i].connectedBody.transform.rotation)
                              * joints[i].transform.rotation;
                    var ratio = Quaternion.Angle(rests[i], now) / budgets[i];
                    if (ratio <= worst) continue;
                    worst = ratio;
                    worstName = joints[i].name;
                }
            }

            Assert.That(worst, Is.LessThan(MaxOvershoot),
                $"'{worstName}' 이 설정한 각의 {worst:F2}배까지 벌어졌다 — 관절이 한계를 못 잡고 있다.");
        }

        private static float BudgetOf(Joint joint)
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

        private static Transform ParentOf(Joint joint) =>
            joint.connectedBody != null ? joint.connectedBody.transform : null;

        private static Transform TipOf(GameObject root, Transform bone)
        {
            foreach (var spec in LastShiftRagdollRig.Bones)
            {
                if (spec.BoneName != bone.name || spec.TipBoneName == null) continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == spec.TipBoneName) return t;
            }

            foreach (Transform child in bone)
            {
                if (!child.name.StartsWith("DEF-")) continue;
                if (child.name.EndsWith("_Col") || child.name.Contains(".soft.")) continue;
                return child;
            }

            return null;
        }

        private GameObject Spawn()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LastShiftRagdollLabScene.RagdollPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"래그돌 프리팹이 없다: {LastShiftRagdollLabScene.RagdollPrefabPath}");

            var instance = Object.Instantiate(prefab);
            instance.name = "RagdollSubject";
            instance.transform.position = new Vector3(0f, 0.304f, 0f);
            _spawned.Add(instance);
            return instance;
        }
    }
}
