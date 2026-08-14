using System.Collections.Generic;
using System.Linq;
using DoodleUp.Editor;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 부위별 래그돌 프로토타입의 조립 계약을 고정한다.
    ///
    /// <b>이 파일이 실제로 막는 사고는 하나다.</b> 리그가 Generic 이라 뼈를 <b>이름 문자열</b>로만
    /// 찾는데, 아트가 뼈 이름을 바꾸거나 FBX 를 다시 뽑으면 매핑이 조용히 끊긴다 — 씬을 열기
    /// 전까지 아무도 모르고, 열어도 "래그돌이 안 생겼네"까지만 보인다.
    /// 그래서 여기서는 실제 FBX 프리팹을 열어 표의 이름이 전부 존재하는지까지 확인한다.
    ///
    /// 물리 <b>느낌</b>은 여기서 안 잰다. 그건 <see cref="LastShiftRagdollCapture"/> 가 연속 캡처와
    /// 정지 시각 CSV 로 뽑는다 — 숫자로 못 쓰는 판정이라 테스트에 넣으면 거짓 초록이 된다.
    /// </summary>
    public sealed class LastShiftRagdollTests
    {
        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
            _instance = null;
        }

        [Test]
        public void PartTableCoversEveryPartExactlyOnce()
        {
            var parts = LastShiftRagdollRig.Bones.Select(bone => bone.Part).ToList();

            Assert.That(parts.Distinct().Count(), Is.EqualTo(parts.Count), "래그돌 표에 같은 부위가 두 번 들어 있다.");
            Assert.That(parts, Is.EquivalentTo(System.Enum.GetValues(typeof(LastShiftRagdollPart)).Cast<LastShiftRagdollPart>()),
                "표와 열거형이 어긋났다 — 한쪽만 고치면 빌더가 조용히 부위를 빠뜨린다.");
        }

        [Test]
        public void OnlyThePelvisIsFreeOfAJoint()
        {
            var roots = LastShiftRagdollRig.Bones.Where(bone => bone.IsRoot).ToList();

            Assert.That(roots.Count, Is.EqualTo(1), "조인트 없는 바디는 하나뿐이어야 한다.");
            Assert.That(roots[0].Part, Is.EqualTo(LastShiftRagdollPart.Pelvis));
        }

        [Test]
        public void MassesAddUpToOneCrew()
        {
            var total = LastShiftRagdollRig.Bones.Sum(bone => LastShiftRagdollRig.MassOf(bone.Part));

            Assert.That(total, Is.EqualTo(LastShiftRagdollRig.TotalMass).Within(0.001f),
                "부위 질량 합이 승무원 한 명과 다르다 — 임펄스 세기가 전부 이 값 기준이라 같이 어긋난다.");
            Assert.That(LastShiftRagdollRig.MassOf(LastShiftRagdollPart.Head),
                Is.GreaterThan(LastShiftRagdollRig.MassOf(LastShiftRagdollPart.UpperArmL)),
                "머리가 팔보다 가벼우면 목이 안 끌려가 덜렁거림이 안 산다.");
        }

        [Test]
        public void BoneGraphDistanceMatchesTheActualSkeleton()
        {
            // 어깨(가슴 ↔ 아래팔)와 사타구니(좌 ↔ 우 허벅지)는 두 다리 건너다 — 이 승무원처럼
            // 땅딸막한 몸에서는 생성 시점부터 콜라이더가 겹치는 바로 그 쌍들이다.
            Assert.That(LastShiftRagdollRig.GraphDistance(LastShiftRagdollPart.Chest, LastShiftRagdollPart.ForearmL),
                Is.EqualTo(2));
            Assert.That(LastShiftRagdollRig.GraphDistance(LastShiftRagdollPart.ThighL, LastShiftRagdollPart.ThighR),
                Is.EqualTo(2));
            Assert.That(LastShiftRagdollRig.GraphDistance(LastShiftRagdollPart.ShinL, LastShiftRagdollPart.ShinR),
                Is.EqualTo(4));
            Assert.That(LastShiftRagdollRig.GraphDistance(LastShiftRagdollPart.Head, LastShiftRagdollPart.Head),
                Is.EqualTo(0));
        }

        [Test]
        public void SelfCollisionIsOffByDefaultBecauseTheCrewIsStubby()
        {
            var tuning = LastShiftRagdollTuning.Comic();
            var longest = LastShiftRagdollRig.Bones
                .SelectMany(a => LastShiftRagdollRig.Bones.Select(b => LastShiftRagdollRig.GraphDistance(a.Part, b.Part)))
                .Max();

            Assert.That(tuning.SelfCollisionIgnoreDistance, Is.GreaterThanOrEqualTo(longest),
                "자기 충돌을 남기면 팔·골반 캡슐이 처음부터 겹쳐 있어 리셋마다 래그돌이 터진다.");
        }

        [Test]
        public void DepenetrationIsClampedSoOverlapCannotLaunchTheCrew()
        {
            // 실측: 이 값을 안 잡았을 때 승무원이 충격 없이도 20m 상공까지 발사됐다.
            Assert.That(LastShiftRagdollTuning.Comic().MaxDepenetrationSpeed, Is.LessThanOrEqualTo(3f));
            Assert.That(LastShiftRagdollTuning.Comic().MaxDepenetrationSpeed, Is.GreaterThan(0f));
        }

        [Test]
        public void SettleDoesNotFreezeTheRagdollAtTheApex()
        {
            var tuning = LastShiftRagdollTuning.Comic();
            var settle = new LastShiftRagdollSettle();

            // 저중력 포물선의 정점에서는 속도가 한 스텝만 0 에 가까워진다. 여기서 정지로 확정하면
            // 승무원이 공중에서 얼어붙는다.
            Assert.That(settle.Step(0f, 0f, 1f / 60f, tuning), Is.False);

            // 다음 스텝에 다시 떨어지기 시작하면 조용했던 시간이 초기화돼야 한다.
            settle.Step(2.2f, 1.1f, 1f / 60f, tuning);
            Assert.That(settle.QuietSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void SettleConfirmsOnlyAfterTheHoldWindow()
        {
            var tuning = LastShiftRagdollTuning.Comic();
            var settle = new LastShiftRagdollSettle();
            var settled = false;

            for (var step = 0; step < 120; step++)
                settled = settle.Step(0.01f, 0.02f, 1f / 60f, tuning);

            Assert.That(settled, Is.True, "조용한 상태가 이어졌는데도 정지 판정이 안 섰다.");
            Assert.That(settle.QuietSeconds, Is.GreaterThanOrEqualTo(tuning.SettleHoldSeconds));
        }

        [Test]
        public void OneFrameSolverSpikeDoesNotEraseTheQuietWindow()
        {
            // 바닥에 누워 쉬는 열두 바디에서 솔버가 한 프레임짜리 각속도 스파이크를 낸다
            // (실측: 0.2 rad/s 로 잦아든 뒤 7.5초에 1.30 이 한 번 튐). 순간값으로 재면 그 한 장이
            // 조용했던 시간을 통째로 지워서 운석 시나리오가 영원히 정지 판정을 못 받았다.
            var tuning = LastShiftRagdollTuning.Comic();
            var settle = new LastShiftRagdollSettle();
            const float step = 1f / 60f;

            for (var i = 0; i < 20; i++) settle.Step(0.005f, 0.02f, step, tuning);
            var quietBefore = settle.QuietSeconds;

            settle.Step(0.005f, 1.3f, step, tuning);

            Assert.That(settle.QuietSeconds, Is.GreaterThan(quietBefore),
                "스파이크 한 장에 조용했던 시간이 0 으로 돌아갔다 — 잡음이 판정을 지배한다.");
        }

        [Test]
        public void SustainedMotionStillKeepsTheRagdollAwake()
        {
            // 평활이 잡음만 걸러야지 진짜 움직임까지 걸러 버리면 공중에서 정지가 선다.
            var tuning = LastShiftRagdollTuning.Comic();
            var settle = new LastShiftRagdollSettle();

            for (var i = 0; i < 300; i++)
                Assert.That(settle.Step(1.4f, 3.0f, 1f / 60f, tuning), Is.False);
        }

        [Test]
        public void RestBrakeOnlyEngagesBelowFlightSpeed()
        {
            var tuning = LastShiftRagdollTuning.Comic();

            Assert.That(tuning.RestBrakeStrength, Is.GreaterThan(0f), "정지 제동이 꺼져 있으면 구형 머리가 안 멈춘다.");
            Assert.That(tuning.RestBrakeSpeed, Is.LessThan(tuning.BodyCheckSpeed),
                "제동 문턱이 충돌 속도보다 높으면 날아가는 도중에 회전이 굳는다.");
            Assert.That(tuning.RestBrakeHoldSeconds, Is.GreaterThan(0f),
                "유지 시간이 없으면 저중력 포물선 정점에서 한 프레임 느려진 것만으로 제동이 걸린다.");
            Assert.That(LastShiftRagdollTuning.WizardDefault().RestBrakeStrength, Is.EqualTo(0f),
                "대조군이 제동을 쓰면 '튜닝을 안 하면 안 멈춘다'를 재현할 수 없다.");
        }

        [Test]
        public void WizardDefaultTuningNeverSettlesOnItsOwn()
        {
            // 대조군은 정지 판정을 아예 안 쓴다. "저중력에서 안 멈춘다"의 재현 조건이 이것이다.
            var tuning = LastShiftRagdollTuning.WizardDefault();
            var settle = new LastShiftRagdollSettle();

            for (var step = 0; step < 600; step++)
                Assert.That(settle.Step(0f, 0f, 1f / 60f, tuning), Is.False);
        }

        [Test]
        public void PrototypeGravityMatchesTheShipAndNotTheProjectSetting()
        {
            Assert.That(LastShiftRagdollTuning.Comic().GravityY,
                Is.EqualTo(LastShiftShipPhysics.GravityY).Within(0.0001f),
                "프로토타입이 선내 저중력 정본을 안 쓰면 여기서 본 느낌이 배 안에서 안 재현된다.");
            Assert.That(UnityEngine.Physics.gravity.y, Is.EqualTo(-9.81f).Within(0.01f),
                "전역 중력이 바뀌었다 — DU02/DU03BC 접지 검증이 지구 중력을 전제한다.");
        }

        [Test]
        public void EveryBoneNameInTheTableExistsInTheActualGenericRig()
        {
            var subject = InstantiateCrew();
            var names = new HashSet<string>(subject.GetComponentsInChildren<Transform>(true).Select(bone => bone.name));

            foreach (var spec in LastShiftRagdollRig.Bones)
            {
                Assert.That(names, Contains.Item(spec.BoneName), $"리그에 뼈 '{spec.BoneName}' 가 없다.");
                if (spec.TipBoneName != null)
                    Assert.That(names, Contains.Item(spec.TipBoneName), $"리그에 끝 뼈 '{spec.TipBoneName}' 가 없다.");
            }

            foreach (var name in new[]
                     {
                         LastShiftRagdollRig.ArmatureName,
                         LastShiftRagdollRig.LeftHipBoneName,
                         LastShiftRagdollRig.RightHipBoneName,
                         LastShiftRagdollRig.LeftShoulderBoneName,
                         LastShiftRagdollRig.RightShoulderBoneName
                     })
                Assert.That(names, Contains.Item(name), $"리그에 '{name}' 가 없다.");
        }

        [Test]
        public void BuildingProducesOneBodyPerPartWithGlobalGravityOff()
        {
            var ragdoll = BuildRagdoll();

            Assert.That(ragdoll.BodyList.Count, Is.EqualTo(LastShiftRagdollRig.Bones.Length));

            foreach (var spec in LastShiftRagdollRig.Bones)
            {
                var body = ragdoll.Bodies[spec.Part];
                Assert.That(body, Is.Not.Null, $"{spec.Part} 바디가 없다.");
                Assert.That(body.useGravity, Is.False,
                    $"{spec.Part} 가 전역 중력을 쓴다 — 지구 중력(-9.81)으로 떨어져 저중력 확인이 무의미해진다.");
                Assert.That(body.mass, Is.EqualTo(LastShiftRagdollRig.MassOf(spec.Part)).Within(0.001f));
            }
        }

        [Test]
        public void EveryPartGetsAColliderWithRealSize()
        {
            var ragdoll = BuildRagdoll();

            foreach (var spec in LastShiftRagdollRig.Bones)
            {
                var body = ragdoll.Bodies[spec.Part];
                var collider = body.GetComponentInChildren<Collider>();
                Assert.That(collider, Is.Not.Null, $"{spec.Part} 에 콜라이더가 없다 — 충돌이 안 나면 래그돌이 아니다.");

                // 콜라이더 치수는 <b>로컬</b>이고 뼈에는 FBX 임포트 스케일이 얹혀 있다.
                // 로컬 값으로 재면 실제로는 멀쩡한 콜라이더가 작아 보이므로 월드로 환산해 본다.
                var scale = collider.transform.lossyScale;
                var uniform = (scale.x + scale.y + scale.z) / 3f;

                switch (collider)
                {
                    case CapsuleCollider capsule:
                        Assert.That(capsule.radius * uniform, Is.GreaterThan(0.01f), $"{spec.Part} 캡슐 반지름이 0 에 가깝다.");
                        Assert.That(capsule.height, Is.GreaterThanOrEqualTo(capsule.radius * 2f));
                        Assert.That(capsule.direction, Is.EqualTo(1), "캡슐 홀더를 뼈 방향에 맞췄으므로 축은 항상 Y 다.");
                        break;
                    case SphereCollider sphere:
                        // 이 승무원은 머리가 몸의 절반이다. 목 길이로 대충 잡은 값이 아니라
                        // 실제 정수리까지를 재고 있는지를 여기서 지킨다.
                        Assert.That(sphere.radius * uniform, Is.GreaterThan(0.15f),
                            "머리 구가 두들 비례에 비해 너무 작다 — 정수리 높이 측정이 실패했을 때 나오는 값이다.");
                        break;
                    default:
                        Assert.Fail($"{spec.Part} 에 예상 못 한 콜라이더 {collider.GetType().Name} 가 붙었다.");
                        break;
                }
            }
        }

        [Test]
        public void EveryNonRootPartHangsFromItsParentByACharacterJoint()
        {
            var ragdoll = BuildRagdoll();

            foreach (var spec in LastShiftRagdollRig.Bones)
            {
                var body = ragdoll.Bodies[spec.Part];
                var joint = body.GetComponent<CharacterJoint>();

                if (spec.IsRoot)
                {
                    Assert.That(joint, Is.Null, "골반은 자유로워야 한다 — 조인트가 붙으면 래그돌 전체가 한 점에 묶인다.");
                    continue;
                }

                Assert.That(joint, Is.Not.Null, $"{spec.Part} 에 조인트가 없다.");
                Assert.That(joint.connectedBody, Is.SameAs(ragdoll.Bodies[spec.Parent]),
                    $"{spec.Part} 가 {spec.Parent} 가 아닌 곳에 매달렸다.");
                Assert.That(joint.swing1Limit.limit, Is.EqualTo(spec.Swing1Limit).Within(0.001f));
                Assert.That(joint.axis.sqrMagnitude, Is.GreaterThan(0.5f), "비틀림 축이 0 벡터다.");
                Assert.That(Vector3.Dot(joint.axis.normalized, joint.swingAxis.normalized), Is.EqualTo(0f).Within(0.01f),
                    $"{spec.Part} 의 스윙 축이 비틀림 축과 수직이 아니다 — 팔꿈치·무릎이 옆으로 접힌다.");
            }
        }

        [Test]
        public void ElbowsAndKneesStayHingesWhileTheNeckStaysLoose()
        {
            var elbow = LastShiftRagdollRig.SpecOf(LastShiftRagdollPart.ForearmL);
            var knee = LastShiftRagdollRig.SpecOf(LastShiftRagdollPart.ShinL);
            var neck = LastShiftRagdollRig.SpecOf(LastShiftRagdollPart.Head);

            Assert.That(elbow.Swing2Limit, Is.LessThan(20f), "팔꿈치가 두 축으로 열리면 팔이 꺾여 고장으로 보인다.");
            Assert.That(knee.Swing2Limit, Is.LessThan(20f));
            Assert.That(neck.Swing1Limit, Is.GreaterThan(40f), "목이 안 열리면 머리 덜렁거림이 안 나온다.");
        }

        [Test]
        public void RebuildingDoesNotStackDuplicatePhysicsComponents()
        {
            var ragdoll = BuildRagdoll();
            ragdoll.Build(LastShiftRagdollTuning.WizardDefault());

            Assert.That(_instance.GetComponentsInChildren<Rigidbody>(true).Length,
                Is.EqualTo(LastShiftRagdollRig.Bones.Length), "재빌드가 바디를 겹쳐 쌓았다.");
            Assert.That(_instance.GetComponentsInChildren<CharacterJoint>(true).Length,
                Is.EqualTo(LastShiftRagdollRig.Bones.Length - 1));
            Assert.That(_instance.GetComponentsInChildren<Collider>(true).Length,
                Is.EqualTo(LastShiftRagdollRig.Bones.Length), "재빌드가 콜라이더 홀더를 지우지 않았다.");
        }

        [Test]
        public void BuildingSwitchesOffTheAnimatorSoItCannotFightThePhysics()
        {
            BuildRagdoll();
            var animator = _instance.GetComponentInChildren<Animator>(true);

            if (animator == null) Assert.Ignore("프리팹에 Animator 가 없다.");
            Assert.That(animator.enabled, Is.False,
                "애니메이터가 살아 있으면 매 프레임 뼈를 되돌려 래그돌이 제자리에서 떨기만 한다.");
        }

        private LastShiftRagdoll BuildRagdoll()
        {
            var subject = InstantiateCrew();
            var ragdoll = subject.AddComponent<LastShiftRagdoll>();
            ragdoll.Build(LastShiftRagdollTuning.Comic());
            return ragdoll;
        }

        private GameObject InstantiateCrew()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LastShiftRagdollLabScene.CharacterPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"승무원 프리팹이 없다: {LastShiftRagdollLabScene.CharacterPrefabPath}");

            _instance = Object.Instantiate(prefab);
            _instance.name = "RagdollSubject";
            return _instance;
        }
    }
}
