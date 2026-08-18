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
        public void SelfCollisionSurvivesForThePairsThatAreNotAlreadyOverlapping()
        {
            // 예전 기본값은 "전부 무시"였고, 그 상태로 뽑은 영상에서 팔·몸통이 머리 메시를
            // 통과해 스킨이 찢어진 것처럼 보였다. 촌수 상한이 그래프 지름 이상으로 다시 올라가면
            // 같은 증상이 그대로 돌아온다.
            var longest = LastShiftRagdollRig.Bones
                .SelectMany(a => LastShiftRagdollRig.Bones.Select(b => LastShiftRagdollRig.GraphDistance(a.Part, b.Part)))
                .Max();

            Assert.That(LastShiftRagdollTuning.Comic().SelfCollisionIgnoreDistance, Is.LessThan(longest),
                "촌수만으로 전부 끄면 몸이 서로를 통과한다 — 겹치는 쌍은 빌드 시점에 직접 재서 끈다.");
        }

        [Test]
        public void BuiltRagdollKeepsMostSelfCollisionPairs()
        {
            var ragdoll = BuildRagdoll();

            Assert.That(ragdoll.SelfCollisionsKept, Is.GreaterThan(0),
                "살아남은 쌍이 없으면 자기 충돌이 사실상 꺼진 것이고, 몸이 서로를 통과한다.");

            // 끄는 쌍은 "차렷 자세에서 이미 겹친 것"뿐이라 소수여야 한다. 절반을 넘으면
            // 겹침 판정이 아니라 다른 이유로 무더기로 꺼지고 있다는 뜻이다.
            Assert.That(ragdoll.SelfCollisionsKept, Is.GreaterThan(ragdoll.SelfCollisionsIgnored),
                "겹친 쌍만 끄는데 꺼진 쪽이 더 많다면 판정이 의도대로 안 돌고 있다.");
        }

        [Test]
        public void EveryPartResolvesItsOwnCollider()
        {
            // 뼈에서 GetComponentInChildren 로 찾으면 자식 뼈의 콜라이더를 먼저 집는다.
            // 그 값으로 자기 충돌 쌍을 고르면 엉뚱한 쌍이 꺼지고, 증상은 화면에서만 드러난다.
            var ragdoll = BuildRagdoll();

            foreach (var collider in ragdoll.Colliders)
                Assert.That(collider.transform.parent, Is.Not.Null,
                    "콜라이더 홀더는 항상 자기 뼈의 자식이어야 한다.");

            Assert.That(ragdoll.Colliders.Count, Is.EqualTo(LastShiftRagdollRig.Bones.Length),
                "부위마다 콜라이더가 정확히 하나여야 부위 → 콜라이더 대응이 성립한다.");
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
        public void EveryNonRootPartHangsFromItsParentByAJoint()
        {
            var ragdoll = BuildRagdoll();

            foreach (var spec in LastShiftRagdollRig.Bones)
            {
                var body = ragdoll.Bodies[spec.Part];
                var joints = body.GetComponents<Joint>();

                if (spec.IsRoot)
                {
                    Assert.That(joints, Is.Empty, "골반은 자유로워야 한다 — 조인트가 붙으면 래그돌 전체가 한 점에 묶인다.");
                    continue;
                }

                // 종류가 섞이면 두 조인트가 같은 바디를 물고 서로 당긴다. 재빌드 때 지우다 만
                // 조인트가 남는 경로가 실제로 있어서 개수까지 본다.
                Assert.That(joints.Length, Is.EqualTo(1), $"{spec.Part} 의 조인트가 하나가 아니다.");

                var joint = joints[0];
                Assert.That(joint.connectedBody, Is.SameAs(ragdoll.Bodies[spec.Parent]),
                    $"{spec.Part} 가 {spec.Parent} 가 아닌 곳에 매달렸다.");
                Assert.That(joint.axis.sqrMagnitude, Is.GreaterThan(0.5f), "회전축이 0 벡터다.");

                if (spec.IsHinge)
                {
                    var hinge = joint as HingeJoint;
                    Assert.That(hinge, Is.Not.Null, $"{spec.Part} 는 경첩이어야 한다.");
                    Assert.That(hinge.useLimits, Is.True, "한계를 안 켜면 경첩이 무한정 돈다.");

                    // 접히는 쪽만 크게 열려 있어야 한다. 양쪽이 다 열리면 무릎이 앞으로도 꺾인다.
                    var span = hinge.limits.max - hinge.limits.min;
                    Assert.That(span, Is.EqualTo(spec.Swing1Limit + 5f).Within(0.001f),
                        $"{spec.Part} 의 가동 범위가 한계 + 여유와 다르다.");
                    Assert.That(Mathf.Min(Mathf.Abs(hinge.limits.min), Mathf.Abs(hinge.limits.max)),
                        Is.EqualTo(5f).Within(0.001f), "반대쪽은 여유만 열려 있어야 한다.");
                    continue;
                }

                var character = joint as CharacterJoint;
                Assert.That(character, Is.Not.Null, $"{spec.Part} 는 볼 조인트여야 한다.");
                Assert.That(character.swing1Limit.limit, Is.EqualTo(spec.Swing1Limit).Within(0.001f));
                Assert.That(Vector3.Dot(character.axis.normalized, character.swingAxis.normalized),
                    Is.EqualTo(0f).Within(0.01f),
                    $"{spec.Part} 의 스윙 축이 비틀림 축과 수직이 아니다.");
            }
        }

        [Test]
        public void KneesAndElbowsAreHingesBecauseSquashedSwingConesLeak()
        {
            // 실측 근거: 스윙 콘 85°×10° 로 흉내 낸 무릎이 175° 까지 접혔고, 솔버 반복을
            // 12/4 → 32/8 로 올려도 1.8배 초과가 남았다. 1자유도로 잠가야 새지 않는다.
            foreach (var part in new[]
                     {
                         LastShiftRagdollPart.ForearmL, LastShiftRagdollPart.ForearmR,
                         LastShiftRagdollPart.ShinL, LastShiftRagdollPart.ShinR
                     })
                Assert.That(LastShiftRagdollRig.SpecOf(part).IsHinge, Is.True, $"{part} 는 경첩이어야 한다.");

            foreach (var part in new[]
                     {
                         LastShiftRagdollPart.Spine, LastShiftRagdollPart.Chest, LastShiftRagdollPart.Head,
                         LastShiftRagdollPart.UpperArmL, LastShiftRagdollPart.UpperArmR,
                         LastShiftRagdollPart.ThighL, LastShiftRagdollPart.ThighR
                     })
                Assert.That(LastShiftRagdollRig.SpecOf(part).IsHinge, Is.False,
                    $"{part} 는 여러 축으로 덜렁거려야 한다 — 경첩으로 잠그면 뻣뻣해진다.");
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
            Assert.That(_instance.GetComponentsInChildren<Joint>(true).Length,
                Is.EqualTo(LastShiftRagdollRig.Bones.Length - 1),
                "재빌드가 조인트를 겹쳐 쌓았다 — 경첩과 볼 조인트가 같은 바디에 함께 남는 경로가 있다.");
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

        /// <summary>
        /// <b>래그돌은 임포트된 스킨을 안 건드린다.</b> 뼈의 부모 경로도, <c>bindposes</c> 배열도.
        ///
        /// 한 번 어겼다가 크게 데었다. 바인드 포즈에 박혀 있던 변형본을 고치겠다고 부모를 물리
        /// 본 밑으로 옮겼는데, <c>bindposes</c> 는 임포트 당시 계층 기준 그대로라 스킨 행렬이
        /// 어긋나 배·엉덩이에서 삼각형이 통째로 튀어나왔다(2026-08-18 반려본).
        /// 물리는 <b>프록시</b>가 지고 스킨 뼈는 포즈만 받는다 — 그 경계를 여기서 못박는다.
        /// </summary>
        [Test]
        public void BuildingLeavesTheSkinHierarchyAndBindposesUntouched()
        {
            var subject = InstantiateCrew();

            var pathsBefore = new Dictionary<SkinnedMeshRenderer, string[]>();
            var bindposesBefore = new Dictionary<SkinnedMeshRenderer, Matrix4x4[]>();
            foreach (var skin in subject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                pathsBefore[skin] = System.Array.ConvertAll(skin.bones, PathOf);
                bindposesBefore[skin] = skin.sharedMesh.bindposes;
            }

            subject.AddComponent<LastShiftRagdoll>().Build(LastShiftRagdollTuning.Comic());

            foreach (var skin in subject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var pathsAfter = System.Array.ConvertAll(skin.bones, PathOf);
                Assert.That(pathsAfter, Is.EqualTo(pathsBefore[skin]),
                    $"{skin.name} 의 뼈 부모 경로가 바뀌었다 — bindpose 와 어긋나 스킨이 깨진다.");

                var bindposesAfter = skin.sharedMesh.bindposes;
                Assert.That(bindposesAfter.Length, Is.EqualTo(bindposesBefore[skin].Length));
                for (var i = 0; i < bindposesAfter.Length; i++)
                    Assert.That(bindposesAfter[i], Is.EqualTo(bindposesBefore[skin][i]),
                        $"{skin.name} 의 bindposes[{i}] 가 바뀌었다.");
            }
        }

        /// <summary>
        /// <b>웨이트를 든 뼈는 하나도 빠짐없이 물리를 따라와야 한다.</b>
        ///
        /// Rigify 는 <c>DEF-shoulder</c>·<c>DEF-breast</c>·<c>DEF-pelvis</c> 를 제어본 밑에
        /// 매달아 놓는데 래그돌은 열두 부위에만 바디를 준다. 그대로 두면 이 여섯이 바인드
        /// 포즈에 박혀, 몸이 날아가도 어깨·가슴·골반만 제자리에 남는다(사용자 판정 2026-08-18:
        /// "몸이 완전 고정되어 있고 다른 부위만 물리 영향받아 메시가 완전 깨진다").
        ///
        /// 물리를 돌리지 않고 프록시를 직접 옮겨 본다 — 무엇이 따라오는지만 보면 되고,
        /// 시뮬레이션을 끼우면 실패했을 때 원인이 둘로 갈린다.
        /// </summary>
        [Test]
        public void EveryWeightedBoneFollowsThePhysicsPose()
        {
            var ragdoll = BuildRagdoll();

            var weighted = new List<Transform>();
            foreach (var skin in ragdoll.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = skin.bones;
                var weights = skin.sharedMesh.GetAllBoneWeights();
                var counts = skin.sharedMesh.GetBonesPerVertex();
                var carries = new bool[bones.Length];
                var cursor = 0;
                for (var v = 0; v < counts.Length; v++)
                {
                    int used = counts[v];
                    for (var i = 0; i < used; i++, cursor++)
                    {
                        var w = weights[cursor];
                        if (w.weight > 0.0001f && w.boneIndex >= 0 && w.boneIndex < bones.Length)
                            carries[w.boneIndex] = true;
                    }
                }
                for (var i = 0; i < bones.Length; i++)
                    if (carries[i] && bones[i] != null && !weighted.Contains(bones[i])) weighted.Add(bones[i]);
            }
            Assert.That(weighted, Is.Not.Empty, "웨이트를 든 뼈를 하나도 못 찾았다.");

            var before = weighted.ConvertAll(bone => bone.position);

            // 모든 프록시를 같은 만큼 옮기고 돌린다. 따라오는 뼈는 전부 같이 움직여야 한다.
            var shift = new Vector3(0.7f, -0.4f, 0.5f);
            var turn = Quaternion.Euler(15f, 40f, 10f);
            foreach (var body in ragdoll.BodyList)
                body.transform.SetPositionAndRotation(
                    body.transform.position + shift, turn * body.transform.rotation);

            ragdoll.ApplyPhysicsPose();

            var stuck = new List<string>();
            for (var i = 0; i < weighted.Count; i++)
                if (Vector3.Distance(weighted[i].position, before[i]) < 0.01f) stuck.Add(weighted[i].name);

            Assert.That(stuck, Is.Empty,
                $"물리를 안 따라온 웨이트 뼈 {stuck.Count}개: {string.Join(", ", stuck)} — " +
                "이 뼈들이 바인드 포즈에 남아 메시를 찢는다.");
        }

        private static string PathOf(Transform bone)
        {
            if (bone == null) return "(null)";
            var path = bone.name;
            var walker = bone.parent;
            while (walker != null)
            {
                path = walker.name + "/" + path;
                walker = walker.parent;
            }
            return path;
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
