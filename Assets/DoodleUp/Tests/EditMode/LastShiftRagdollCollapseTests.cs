using System.Collections.Generic;
using DoodleUp.Editor;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 넘어진 승무원이 <b>공처럼 뭉치지 않는지</b>를 고정한다.
    ///
    /// <b>이 파일이 막는 사고.</b> 2026-08-21 에 조인트 <b>축</b>만 뼈에 맞추고 <b>한계 숫자는
    /// 그대로</b> 뒀다. 축이 78도 돌아갔으니 같은 숫자가 다른 자유도에 걸렸고, 엉덩이에서는 옛 축
    /// 기준 "굽힘" 이던 <c>-20..70</c>(90도)이 <b>넓적다리를 제 축 둘레로 90도 돌리는 허가</b>가
    /// 됐다. 무릎 경첩이 그 돌아간 평면을 따라 접히면서 다리가 개구리처럼 퍼졌고, 사용자가 본
    /// 화면은 고치기 <b>전보다 심했다</b>.
    ///
    /// 그때 검사가 못 잡은 이유는 <see cref="LastShiftRagdollJointFrameTests"/> 를 포함해 모든 자가
    /// 상대 회전의 <b>크기 하나</b>를 <c>max(스윙)+max(비틀림)</c> 합계 예산에 나눴기 때문이다 —
    /// 엉덩이 예산이 100도가 되어 스윙 56도가 "0.56배, 한계 안" 으로 찍혔다. 여기서는
    /// <see cref="LastShiftRagdollJointLimitTracker"/> 로 자유도를 갈라 <b>각각</b> 제 한계와 견준다.
    ///
    /// <b>PlayMode 가 아니라 여기 있는 이유</b>는 <see cref="LastShiftRagdollJointFrameTests"/> 와
    /// 같다 — 랩 씬이 빌드 세팅에 없고, <c>Physics.Simulate</c> 를 손으로 밟는 쪽이 더 결정적이다.
    /// </summary>
    public sealed class LastShiftRagdollCollapseTests
    {
        /// <summary>
        /// 넘어뜨린 뒤 어느 자유도도 이보다 더 새면 안 된다(도).
        /// 실측 최악이 <c>22.1</c> 도라 여유를 두고 잡았다 — 고치기 전은 <c>56.9</c> 도였다.
        /// </summary>
        private const float MaxExcessDegrees = 32f;

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
        public void PrefabCarriesBodySetupBecauseSolverSettingsDoNotSerialize()
        {
            // 리지드바디의 솔버 반복 수·디페네트레이션 상한은 프리팹 YAML 에 안 남는다.
            // 컴포넌트가 빠지면 씬은 Unity 기본값(6/1 · 10m/s)으로 조용히 되돌아간다.
            var subject = Spawn();

            Assert.That(subject.GetComponent<LastShiftRagdollBodySetup>(), Is.Not.Null,
                "래그돌 프리팹에 LastShiftRagdollBodySetup 이 없다 — 솔버 설정은 직렬화되지 않으므로 "
                + "이것이 빠지면 Unity 기본값으로 돌아간다. Last Shift/Prototype/Rebuild Ragdoll Joint Frames 를 돌려라.");
        }

        [Test]
        public void BodySetupHardensEveryBodyAndBoostsOnlyHinges()
        {
            var tuning = LastShiftRagdollTuning.Comic();
            var root = new GameObject("BodySetupProbe");
            _spawned.Add(root);

            var parent = new GameObject("parent").transform;
            parent.SetParent(root.transform);
            var parentBody = parent.gameObject.AddComponent<Rigidbody>();

            var ball = new GameObject("ball").transform;
            ball.SetParent(root.transform);
            ball.gameObject.AddComponent<Rigidbody>();
            ball.gameObject.AddComponent<CharacterJoint>().connectedBody = parentBody;

            var hinge = new GameObject("hinge").transform;
            hinge.SetParent(root.transform);
            var hingeBody = hinge.gameObject.AddComponent<Rigidbody>();
            hinge.gameObject.AddComponent<HingeJoint>().connectedBody = parentBody;

            root.AddComponent<LastShiftRagdollBodySetup>().Apply();

            Assert.That(parentBody.maxDepenetrationVelocity, Is.EqualTo(tuning.MaxDepenetrationSpeed).Within(0.001f),
                "디페네트레이션 상한이 Unity 기본값(10m/s)으로 남아 있다 — 겹친 콜라이더를 그 속도로 "
                + "밀어내면 착지 프레임에 관절이 통째로 뚫린다.");
            Assert.That(parentBody.solverIterations, Is.EqualTo(tuning.SolverIterations));
            Assert.That(hingeBody.solverIterations,
                Is.EqualTo(tuning.SolverIterations * LastShiftRagdollBodySetup.HingeSolverBoost),
                "경첩 바디의 반복이 안 올라갔다 — 무릎·팔꿈치는 나머지 두 자유도를 매 스텝 0 으로 "
                + "눌러야 해서 기본 반복으로는 축 밖으로 샌다.");
        }

        [Test]
        public void HipTwistStaysWithinTheDesignedRotation()
        {
            // 엉덩이 비틀림은 넓적다리를 <b>제 축 둘레로</b> 돌리는 자유도다. 설계표가 정한 30도를
            // 넘겨 열면 무릎 경첩의 굽힘 평면이 통째로 돌아가 다리가 옆으로 퍼진다.
            var subject = Spawn();
            var spec = LastShiftRagdollRig.SpecOf(LastShiftRagdollPart.ThighL);
            var design = spec.TwistLimit;
            var checkedJoints = 0;

            foreach (var joint in subject.GetComponentsInChildren<CharacterJoint>(true))
            {
                if (!joint.name.StartsWith("DEF-thigh")) continue;
                checkedJoints++;

                Assert.That(Mathf.Abs(joint.highTwistLimit.limit), Is.LessThanOrEqualTo(design + 0.5f),
                    $"'{joint.name}' 의 비틀림 상한이 설계({design:F0}도)를 넘는다 — "
                    + "축이 어긋나 있던 때 잡은 굽힘 숫자가 비틀림에 그대로 남아 있는 것이다.");
                Assert.That(Mathf.Abs(joint.lowTwistLimit.limit), Is.LessThanOrEqualTo(design + 0.5f),
                    $"'{joint.name}' 의 비틀림 하한이 설계({design:F0}도)를 넘는다.");

                // 반대쪽도 같이 고정한다. 비틀림만 좁히고 스윙을 30/10 으로 두면 이번에는
                // <b>못 지킬 콘</b>이 남는다 — 실측에서 10도 콘이 47도까지 뚫렸다.
                Assert.That(joint.swing1Limit.limit, Is.EqualTo(spec.Swing1Limit).Within(0.5f),
                    $"'{joint.name}' 의 스윙1 이 설계({spec.Swing1Limit:F0}도)와 다르다.");
                Assert.That(joint.swing2Limit.limit, Is.EqualTo(spec.Swing2Limit).Within(0.5f),
                    $"'{joint.name}' 의 스윙2 가 설계({spec.Swing2Limit:F0}도)와 다르다.");
            }

            Assert.That(checkedJoints, Is.EqualTo(2), "엉덩이 조인트 둘을 못 찾았다 — 뼈 이름이 바뀌었다.");
        }

        [Test]
        public void ShovedRagdollKeepsEveryAxisWithinItsOwnLimit()
        {
            var subject = Spawn();
            subject.GetComponent<LastShiftRagdollBodySetup>().Apply();
            subject.GetComponent<LastShiftRagdollSelfCollision>().Apply();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(floor);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);

            var trackers = new List<LastShiftRagdollJointLimitTracker>();
            foreach (var joint in subject.GetComponentsInChildren<Joint>(true))
            {
                if (joint.connectedBody == null) continue;
                trackers.Add(new LastShiftRagdollJointLimitTracker(joint));
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

            var step = Time.fixedDeltaTime;
            for (var s = 0; s < Mathf.CeilToInt(4f / step); s++)
            {
                UnityEngine.Physics.Simulate(step);
                foreach (var tracker in trackers) tracker.Sample(out _, out _, out _);
            }

            var worst = trackers[0];
            foreach (var tracker in trackers)
                if (tracker.WorstExcess > worst.WorstExcess) worst = tracker;

            Assert.That(worst.WorstExcess, Is.LessThan(MaxExcessDegrees),
                $"'{worst.Name}.{worst.WorstAxis}' 가 {worst.WorstDegrees:F0}도까지 갔다"
                + $"(한계 {worst.WorstLimit:F0}도, {worst.WorstExcess:F0}도 초과). "
                + "한계를 못 잡으면 화면에서는 팔다리가 몸통에 접힌 덩어리로 보인다.");
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
