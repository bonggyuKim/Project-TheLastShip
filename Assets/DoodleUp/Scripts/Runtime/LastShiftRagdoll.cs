using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 캐릭터 하나를 부위별 물리 바디로 바꾸는 프로토타입 컴포넌트.
    ///
    /// <b>이건 게임 통합이 아니다.</b> <see cref="LastShiftPlayerController"/>(CharacterController 기반)
    /// 와 아무 관계가 없고, 평시 이동 ↔ 래그돌 전환도 안 한다 — 카드 범위가 "물리 자체가 원하는
    /// 느낌으로 나오는지"까지라서 전환 로직을 넣으면 무엇 때문에 이상한지 못 가른다.
    /// 전용 씬 <c>Assets/Scenes/LAST_SHIFT_RAGDOLL_LAB.unity</c> 에서만 쓴다.
    ///
    /// <b>중력.</b> 전역 <c>Physics.gravity</c> 는 지구 중력(-9.81)로 고정돼 있고 DU02/DU03BC 검증이
    /// 그걸 전제하므로 손대지 않는다(<see cref="LastShiftShipPhysics"/> 주석 참고). 대신 모든 바디를
    /// <c>useGravity = false</c> 로 두고 <see cref="StepPhysics"/> 가 선내 저중력을 직접 넣는다.
    ///
    /// <b>가속으로 넣지 속도로 안 넣는다.</b> 소품 쪽 <see cref="LastShiftShipPhysics.ApplyShipGravity"/>
    /// 는 <c>linearVelocity</c> 에 직접 더하는데, 조인트로 묶인 바디에 같은 짓을 하면 솔버가 방금
    /// 맞춘 구속 속도를 매 스텝 덮어써서 팔이 늘어나고 떨린다. 그래서 여기서는
    /// <c>ForceMode.Acceleration</c> 으로 넣는다 — 결과 가속은 같고 솔버만 안 깨진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftRagdoll : MonoBehaviour
    {
        private readonly Dictionary<LastShiftRagdollPart, Rigidbody> _bodies =
            new Dictionary<LastShiftRagdollPart, Rigidbody>();

        private readonly List<Rigidbody> _bodyList = new List<Rigidbody>();
        private readonly List<Collider> _colliders = new List<Collider>();
        private readonly List<RestPose> _restPoses = new List<RestPose>();

        /// <summary>
        /// 부위 → 그 부위 <b>자신의</b> 콜라이더. 뼈에서 <c>GetComponentInChildren</c> 로 찾으면
        /// 안 된다 — 콜라이더 홀더는 뼈의 <b>마지막</b> 자식이라, 자식 뼈를 가진 부위(골반·가슴 등)는
        /// 깊이 우선 탐색이 자식 뼈 쪽 콜라이더를 먼저 집는다. 자기 충돌을 끌 쌍을 고르는 데
        /// 그 값을 쓰면 엉뚱한 쌍이 꺼진다.
        /// </summary>
        private readonly Dictionary<LastShiftRagdollPart, Collider> _colliderOf =
            new Dictionary<LastShiftRagdollPart, Collider>();

        private LastShiftRagdollTuning _tuning;
        private LastShiftRagdollSettle _settle;
        private float _restSeconds;

        /// <summary>부위 → 물리 바디. 빌드 전에는 비어 있다.</summary>
        public IReadOnlyDictionary<LastShiftRagdollPart, Rigidbody> Bodies => _bodies;

        public IReadOnlyList<Rigidbody> BodyList => _bodyList;

        public IReadOnlyList<Collider> Colliders => _colliders;

        /// <summary>골반. 조인트가 없는 유일한 바디라 전체 위치의 기준이다.</summary>
        public Rigidbody Root { get; private set; }

        public LastShiftRagdollTuning Tuning => _tuning;

        public bool IsBuilt => _bodyList.Count > 0;

        /// <summary>정지 판정이 선 상태인지. 저중력에서 안 멈추는 문제를 재는 바로 그 값이다.</summary>
        public bool IsSettled => _settle.Settled;

        /// <summary>마지막 충격 이후 흐른 물리 시간(초). 정지까지 몇 초 걸렸는지 재는 데 쓴다.</summary>
        public float SecondsSinceImpulse { get; private set; }

        /// <summary>정지가 선 시각(초). 아직이면 음수.</summary>
        public float SettledAtSeconds { get; private set; } = -1f;

        /// <summary>현재 최대 선속도(m/s). 측정·로그용.</summary>
        public float MaxLinearSpeed { get; private set; }

        /// <summary>현재 최대 각속도(rad/s).</summary>
        public float MaxAngularSpeed { get; private set; }

        /// <summary>
        /// 질량 가중 평균 속도의 크기(m/s) — 사실상 무게중심 속도다.
        /// "아직 날아가는 중인가"는 이 값으로만 제대로 판정된다. 최대 선속도는 바닥에 누워
        /// 흔들리는 중에도 팔끝 하나 때문에 쉽게 튀어서 게이트로 못 쓴다.
        /// </summary>
        public float CenterOfMassSpeed { get; private set; }

        /// <summary>빌드 시점에 잰 발밑에서 정수리까지(m). 임펄스 세기가 맞는 크기인지 볼 때 쓴다.</summary>
        public float StandingHeight { get; private set; }

        /// <summary>좌우 허벅지 뼈 간격(m).</summary>
        public float HipSpan { get; private set; }

        /// <summary>좌우 위팔 뼈 간격(m).</summary>
        public float ShoulderSpan { get; private set; }

        /// <summary>
        /// 리그를 물리 바디로 바꾼다. 두 번 부르면 앞서 만든 바디·조인트·콜라이더를 지우고 다시 만든다 —
        /// 튜닝을 바꿔 가며 같은 씬에서 비교하는 게 이 프로토타입의 주 사용법이라 재빌드가 기본이다.
        /// </summary>
        public void Build(LastShiftRagdollTuning tuning)
        {
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));

            Clear();
            _tuning = tuning;

            var animator = GetComponentInChildren<Animator>(true);
            if (animator != null) animator.enabled = false;

            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // 래그돌은 바운즈가 원래 자리에서 크게 벗어나므로, 안 켜면 날아가는 도중에 컬링된다.
                skin.updateWhenOffscreen = true;

                // <b>캡처가 빈 그림을 뽑던 원인.</b> 스킨 행렬은 보통 한 프레임에 한 번만 갱신되는데,
                // 에디터에서 물리를 손으로 밟으며 <c>Camera.Render</c> 를 직접 부르면 그 갱신이
                // 안 끼어 든다 — 뼈는 움직이는데 화면은 바인드 포즈 그대로였다(실측: 열 장이
                // 바이트 단위로 동일). 매 렌더마다 다시 계산하게 강제한다.
                skin.forceMatrixRecalculationPerRender = true;
            }

            var bones = ResolveBones();
            var hipSpan = Distance(bones, LastShiftRagdollRig.LeftHipBoneName, LastShiftRagdollRig.RightHipBoneName);
            var shoulderSpan = Distance(bones, LastShiftRagdollRig.LeftShoulderBoneName, LastShiftRagdollRig.RightShoulderBoneName);
            var crownRise = CrownRise(bones);

            HipSpan = hipSpan;
            ShoulderSpan = shoulderSpan;
            StandingHeight = MeasureStandingHeight();

            for (var i = 0; i < LastShiftRagdollRig.Bones.Length; i++)
            {
                var spec = LastShiftRagdollRig.Bones[i];
                var bone = bones[spec.BoneName];

                var body = bone.gameObject.GetComponent<Rigidbody>();
                if (body == null) body = bone.gameObject.AddComponent<Rigidbody>();
                ConfigureBody(body, spec, tuning);

                _bodies[spec.Part] = body;
                _bodyList.Add(body);
                _restPoses.Add(new RestPose(bone));

                AddCollider(bone, bones, spec, hipSpan, shoulderSpan, crownRise);
            }

            Root = _bodies[LastShiftRagdollPart.Pelvis];

            for (var i = 0; i < LastShiftRagdollRig.Bones.Length; i++)
            {
                var spec = LastShiftRagdollRig.Bones[i];
                if (spec.IsRoot) continue;
                AddJoint(bones, spec);
            }

            IgnoreNearbySelfCollisions(tuning);
            _settle.Wake();
            _restSeconds = 0f;
            SecondsSinceImpulse = 0f;
            SettledAtSeconds = -1f;
        }

        /// <summary>
        /// 물리 한 스텝. 저중력을 넣고 정지 여부를 갱신한다.
        /// <c>FixedUpdate</c> 뿐 아니라 에디터의 수동 시뮬레이션 루프에서도 같은 함수를 부른다 —
        /// 캡처가 플레이와 다른 물리를 돌면 증거가 증거가 아니다.
        /// </summary>
        public void StepPhysics(float deltaTime)
        {
            if (!IsBuilt || _tuning == null) return;

            MeasureSpeeds();

            var settledNow = _settle.Step(MaxLinearSpeed, MaxAngularSpeed, deltaTime, _tuning);
            SecondsSinceImpulse += deltaTime;

            if (settledNow)
            {
                if (SettledAtSeconds < 0f)
                {
                    SettledAtSeconds = SecondsSinceImpulse;
                    for (var i = 0; i < _bodyList.Count; i++)
                    {
                        var body = _bodyList[i];
                        if (body == null || body.isKinematic) continue;
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                        body.Sleep();
                    }
                }

                // 잠든 뒤에도 중력을 계속 넣으면 매 스텝 깨어나 영원히 안 멈춘다.
                return;
            }

            _restSeconds = CenterOfMassSpeed < _tuning.RestBrakeSpeed ? _restSeconds + deltaTime : 0f;
            var braking = _tuning.RestBrakeStrength > 0f && _restSeconds >= _tuning.RestBrakeHoldSeconds;
            var brakeFactor = braking ? Mathf.Clamp01(1f - _tuning.RestBrakeStrength * deltaTime) : 1f;

            var acceleration = new Vector3(0f, _tuning.GravityY, 0f);
            for (var i = 0; i < _bodyList.Count; i++)
            {
                var body = _bodyList[i];
                if (body == null || body.isKinematic) continue;
                body.AddForce(acceleration, ForceMode.Acceleration);
                if (braking) body.angularVelocity *= brakeFactor;
            }
        }

        private void FixedUpdate()
        {
            StepPhysics(Time.fixedDeltaTime);
        }

        /// <summary>부위 하나에 충격을 준다. 정지 판정은 여기서 풀린다.</summary>
        public void ApplyImpulse(LastShiftRagdollPart part, Vector3 impulse)
        {
            if (!_bodies.TryGetValue(part, out var body) || body == null) return;
            WakeAll();
            body.AddForce(impulse, ForceMode.Impulse);
        }

        /// <summary>
        /// 몸 전체에 같은 속도 변화를 준다. 밀쳐지는 건 부위가 아니라 사람이라, 몸싸움은
        /// 이쪽으로 모델링해야 "제자리 회전"이 아니라 "밀려 떠간다"가 나온다.
        /// </summary>
        public void ApplyVelocityChange(Vector3 velocityChange)
        {
            if (!IsBuilt) return;
            WakeAll();
            for (var i = 0; i < _bodyList.Count; i++)
            {
                var body = _bodyList[i];
                if (body == null || body.isKinematic) continue;
                body.AddForce(velocityChange, ForceMode.VelocityChange);
            }
        }

        /// <summary>운석 충격(R-3). 폭심에서 모든 부위를 한꺼번에 민다.</summary>
        public void ApplyBlast(Vector3 origin, float impulse, float radius)
        {
            if (!IsBuilt) return;
            WakeAll();
            for (var i = 0; i < _bodyList.Count; i++)
            {
                var body = _bodyList[i];
                if (body == null || body.isKinematic) continue;
                body.AddExplosionForce(impulse, origin, radius, 0.35f, ForceMode.Impulse);
            }
        }

        /// <summary>모든 바디를 깨우고 정지 판정을 되돌린다.</summary>
        public void WakeAll()
        {
            _settle.Wake();
            _restSeconds = 0f;
            SecondsSinceImpulse = 0f;
            SettledAtSeconds = -1f;

            for (var i = 0; i < _bodyList.Count; i++)
            {
                var body = _bodyList[i];
                if (body == null || body.isKinematic) continue;
                body.WakeUp();
            }
        }

        /// <summary>
        /// 빌드 시점의 자세로 되돌린다. 같은 시나리오를 반복해 보는 게 프로토타입의 사용법이라
        /// 리셋이 정확해야 두 튜닝을 비교할 수 있다.
        /// </summary>
        public void ResetToRestPose()
        {
            for (var i = 0; i < _bodyList.Count; i++)
            {
                var body = _bodyList[i];
                if (body == null) continue;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            for (var i = 0; i < _restPoses.Count; i++) _restPoses[i].Apply();

            for (var i = 0; i < _bodyList.Count; i++)
            {
                var body = _bodyList[i];
                if (body == null) continue;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            WakeAll();
        }

        /// <summary>만들어 둔 물리 컴포넌트를 전부 지운다.</summary>
        public void Clear()
        {
            // 조인트를 먼저 전부 지운다. 부모 바디를 앞서 지우면 자식 조인트가 사라진 연결을
            // 물고 남아 다음 빌드에서 조용히 어긋난다.
            for (var i = 0; i < _bodyList.Count; i++)
            {
                var body = _bodyList[i];
                if (body == null) continue;
                var joint = body.GetComponent<CharacterJoint>();
                if (joint != null) DestroyComponent(joint);
            }

            for (var i = 0; i < _bodyList.Count; i++)
            {
                var body = _bodyList[i];
                if (body == null) continue;
                DestroyComponent(body);
            }

            for (var i = 0; i < _colliders.Count; i++)
            {
                var collider = _colliders[i];
                if (collider == null) continue;
                DestroyObject(collider.gameObject);
            }

            _bodies.Clear();
            _bodyList.Clear();
            _colliders.Clear();
            _colliderOf.Clear();
            _restPoses.Clear();
            SelfCollisionsIgnored = 0;
            SelfCollisionsKept = 0;
            Root = null;
        }

        private void MeasureSpeeds()
        {
            MaxLinearSpeed = 0f;
            MaxAngularSpeed = 0f;

            var momentum = Vector3.zero;
            var mass = 0f;

            for (var i = 0; i < _bodyList.Count; i++)
            {
                var body = _bodyList[i];
                if (body == null || body.isKinematic) continue;

                var velocity = body.linearVelocity;
                MaxLinearSpeed = Mathf.Max(MaxLinearSpeed, velocity.magnitude);
                MaxAngularSpeed = Mathf.Max(MaxAngularSpeed, body.angularVelocity.magnitude);

                momentum += velocity * body.mass;
                mass += body.mass;
            }

            CenterOfMassSpeed = mass > 0f ? (momentum / mass).magnitude : 0f;
        }

        private static void ConfigureBody(Rigidbody body, LastShiftRagdollBone spec, LastShiftRagdollTuning tuning)
        {
            body.mass = LastShiftRagdollRig.MassOf(spec.Part);
            body.useGravity = false;
            body.linearDamping = tuning.LinearDamping;
            body.angularDamping = tuning.AngularDamping;
            body.maxAngularVelocity = tuning.MaxAngularSpeed;
            body.sleepThreshold = tuning.SleepThreshold;
            body.solverIterations = tuning.SolverIterations;
            body.solverVelocityIterations = tuning.SolverVelocityIterations;
            body.maxDepenetrationVelocity = tuning.MaxDepenetrationSpeed;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void AddCollider(
            Transform bone,
            IReadOnlyDictionary<string, Transform> bones,
            LastShiftRagdollBone spec,
            float hipSpan,
            float shoulderSpan,
            float crownRise)
        {
            var holder = new GameObject(spec.Part + "__RagdollCollider");
            holder.transform.SetParent(bone, false);

            // 길이·반지름은 전부 <b>월드</b> 거리에서 뽑는데 콜라이더 치수는 로컬이다. 씬이
            // 승무원을 1.5배로 쓰므로(게임과 같은 크기) 나눠 주지 않으면 콜라이더만 2.25배가 돼
            // 몸이 자기 콜라이더 안에 파묻힌다.
            var lossy = holder.transform.lossyScale;
            var uniformScale = (lossy.x + lossy.y + lossy.z) / 3f;
            if (uniformScale <= 0.0001f)
                throw new InvalidOperationException($"{spec.BoneName} 의 스케일이 0 이다 — 콜라이더를 만들 수 없다.");

            if (spec.Girth == LastShiftRagdollGirth.CrownRise)
            {
                // 머리는 자식 뼈가 없다. 메시 정수리까지의 높이를 지름으로 삼아 구를 얹는다.
                var radius = Mathf.Max(0.02f, crownRise * spec.GirthScale);
                holder.transform.position = bone.position + Vector3.up * radius;
                holder.transform.rotation = Quaternion.identity;

                var sphere = holder.AddComponent<SphereCollider>();
                sphere.radius = radius / uniformScale;
                _colliders.Add(sphere);
                _colliderOf[spec.Part] = sphere;
                return;
            }

            var tip = bones[spec.TipBoneName];
            var delta = tip.position - bone.position;
            var length = delta.magnitude;
            if (length <= 0.0001f)
                throw new InvalidOperationException($"{spec.BoneName} → {spec.TipBoneName} 길이가 0 이다 — 리그가 바뀌었다.");

            var direction = delta / length;
            var girthSource = spec.Girth switch
            {
                LastShiftRagdollGirth.HipSpan => hipSpan,
                LastShiftRagdollGirth.ShoulderSpan => shoulderSpan,
                _ => length
            };

            var capsuleRadius = Mathf.Max(0.015f, girthSource * spec.GirthScale);
            holder.transform.position = bone.position + direction * (length * 0.5f);
            holder.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);

            var capsule = holder.AddComponent<CapsuleCollider>();
            capsule.direction = 1; // 홀더의 로컬 Y 를 뼈 방향에 맞춰 놨으므로 항상 Y 다.
            capsule.radius = capsuleRadius / uniformScale;
            capsule.height = Mathf.Max(length, capsuleRadius * 2f) / uniformScale;
            _colliders.Add(capsule);
            _colliderOf[spec.Part] = capsule;
        }

        private void AddJoint(IReadOnlyDictionary<string, Transform> bones, LastShiftRagdollBone spec)
        {
            var tuning = _tuning;
            var bone = bones[spec.BoneName];
            var joint = bone.gameObject.GetComponent<CharacterJoint>();
            if (joint == null) joint = bone.gameObject.AddComponent<CharacterJoint>();

            joint.connectedBody = _bodies[spec.Parent];
            joint.anchor = Vector3.zero;
            joint.autoConfigureConnectedAnchor = true;
            joint.enablePreprocessing = false;
            joint.enableProjection = true;
            joint.projectionDistance = tuning.JointProjectionDistance;
            joint.projectionAngle = tuning.JointProjectionAngle;
            joint.enableCollision = false;

            var twist = TwistDirection(bones, spec);

            // 스윙 축은 비틀림 축에 수직인 아무 축이면 되는데, 아무거나 잡으면 팔꿈치·무릎이
            // 옆으로 접힌다. 캐릭터의 위쪽(또는 뼈가 위쪽과 나란하면 앞쪽)과의 외적을 쓰면
            // 다리는 무릎 축, 팔은 팔꿈치 축이 자연히 나온다 — 부위별 예외 없이 한 식으로 된다.
            var reference = Mathf.Abs(Vector3.Dot(twist, transform.up)) < 0.95f ? transform.up : transform.forward;
            var swing = Vector3.Cross(twist, reference).normalized;

            joint.axis = bone.InverseTransformDirection(twist);
            joint.swingAxis = bone.InverseTransformDirection(swing);

            joint.lowTwistLimit = new SoftJointLimit { limit = -spec.TwistLimit };
            joint.highTwistLimit = new SoftJointLimit { limit = spec.TwistLimit };
            joint.swing1Limit = new SoftJointLimit { limit = spec.Swing1Limit };
            joint.swing2Limit = new SoftJointLimit { limit = spec.Swing2Limit };
        }

        private Vector3 TwistDirection(IReadOnlyDictionary<string, Transform> bones, LastShiftRagdollBone spec)
        {
            var bone = bones[spec.BoneName];

            if (spec.TipBoneName != null)
            {
                var delta = bones[spec.TipBoneName].position - bone.position;
                if (delta.sqrMagnitude > 1e-8f) return delta.normalized;
            }

            // 머리처럼 끝 뼈가 없으면 부모에서 자기 쪽으로 오는 방향이 곧 뼈 방향이다.
            var fromParent = bone.position - bones[LastShiftRagdollRig.SpecOf(spec.Parent).BoneName].position;
            return fromParent.sqrMagnitude > 1e-8f ? fromParent.normalized : transform.up;
        }

        /// <summary>
        /// 자기 몸끼리 충돌을 <b>꼭 필요한 쌍만</b> 끈다.
        ///
        /// <b>전부 끄면 몸이 서로를 통과한다.</b> 예전 기본값(<c>SelfCollisionIgnoreDistance = 99</c>)이
        /// 그랬고, 그 상태로 뽑은 영상에서 팔·몸통이 머리 메시 안으로 파고들어 스킨이 찢어져
        /// 보였다 — 스킨이 실제로 찢어진 게 아니라 <b>가려 줄 충돌이 아예 없었다.</b>
        /// (실측으로 확인: 스킨 웨이트는 전부 물리로 구동되는 뼈에 걸려 있고, 조인트가 벌어진
        /// 최대치도 3cm 라 둘 다 원인이 아니었다.)
        ///
        /// 그렇다고 전부 켜면 첫 프레임에 터진다. 이 승무원은 땅딸막해서 <b>차렷 자세에서 이미</b>
        /// 위팔 캡슐이 몸통과 겹쳐 있고, 겹친 채로 만난 두 콜라이더는 서로를 밀어내며 폭발한다.
        ///
        /// 그래서 거리 기준(관절로 이어진 직계 쌍)에 더해, <b>차렷 자세에서 실제로 겹쳐 있는
        /// 쌍</b>만 골라서 끈다. 폭발의 씨앗은 겹침이지 촌수가 아니므로, 겹침을 직접 재는 쪽이
        /// 촌수로 뭉뚱그리는 것보다 정확하다 — 안 겹친 쌍은 전부 살아남아 서로를 막는다.
        /// </summary>
        private void IgnoreNearbySelfCollisions(LastShiftRagdollTuning tuning)
        {
            var ignored = 0;
            var kept = 0;

            for (var a = 0; a < LastShiftRagdollRig.Bones.Length; a++)
            for (var b = a + 1; b < LastShiftRagdollRig.Bones.Length; b++)
            {
                var partA = LastShiftRagdollRig.Bones[a].Part;
                var partB = LastShiftRagdollRig.Bones[b].Part;

                var colliderA = ColliderOf(partA);
                var colliderB = ColliderOf(partB);
                if (colliderA == null || colliderB == null) continue;

                var near = LastShiftRagdollRig.GraphDistance(partA, partB) <= tuning.SelfCollisionIgnoreDistance;
                if (!near && !OverlapsAtRest(colliderA, colliderB))
                {
                    kept++;
                    continue;
                }

                UnityEngine.Physics.IgnoreCollision(colliderA, colliderB, true);
                ignored++;
            }

            SelfCollisionsIgnored = ignored;
            SelfCollisionsKept = kept;
        }

        /// <summary>차렷 자세에서 두 콜라이더가 이미 파고들어 있는가. 빌드 직후에만 뜻이 있다.</summary>
        private static bool OverlapsAtRest(Collider a, Collider b)
        {
            return UnityEngine.Physics.ComputePenetration(
                a, a.transform.position, a.transform.rotation,
                b, b.transform.position, b.transform.rotation,
                out _, out _);
        }

        /// <summary>충돌을 끈 쌍 수. 전부 꺼져 있으면 몸이 서로를 통과한다 — 회귀 시험이 이 값을 본다.</summary>
        public int SelfCollisionsIgnored { get; private set; }

        /// <summary>충돌을 살려 둔 쌍 수. 0 이면 자기 충돌이 사실상 없는 것이다.</summary>
        public int SelfCollisionsKept { get; private set; }

        private Collider ColliderOf(LastShiftRagdollPart part)
        {
            return _colliderOf.TryGetValue(part, out var collider) ? collider : null;
        }

        private Dictionary<string, Transform> ResolveBones()
        {
            var wanted = new HashSet<string>();
            for (var i = 0; i < LastShiftRagdollRig.Bones.Length; i++)
            {
                var spec = LastShiftRagdollRig.Bones[i];
                wanted.Add(spec.BoneName);
                if (spec.TipBoneName != null) wanted.Add(spec.TipBoneName);
            }

            wanted.Add(LastShiftRagdollRig.LeftHipBoneName);
            wanted.Add(LastShiftRagdollRig.RightHipBoneName);
            wanted.Add(LastShiftRagdollRig.LeftShoulderBoneName);
            wanted.Add(LastShiftRagdollRig.RightShoulderBoneName);

            var found = new Dictionary<string, Transform>();
            foreach (var candidate in GetComponentsInChildren<Transform>(true))
            {
                if (!wanted.Contains(candidate.name)) continue;
                if (found.ContainsKey(candidate.name)) continue;
                found[candidate.name] = candidate;
            }

            foreach (var name in wanted)
                if (!found.ContainsKey(name))
                    throw new InvalidOperationException(
                        $"{gameObject.name} 리그에 뼈 '{name}' 가 없다 — Generic 리그라 이름 매핑이 유일한 연결 고리다. " +
                        $"{nameof(LastShiftRagdollRig)} 의 이름 표를 리그에 맞춰 고쳐야 한다.");

            return found;
        }

        private static float Distance(IReadOnlyDictionary<string, Transform> bones, string a, string b)
        {
            return Vector3.Distance(bones[a].position, bones[b].position);
        }

        private float MeasureStandingHeight()
        {
            MeasureRenderedSpan(out var bottom, out var top);
            return top > bottom ? top - bottom : 0f;
        }

        /// <summary>
        /// 렌더되는 실루엣의 위·아래 월드 높이.
        ///
        /// <b><c>Renderer.bounds</c> 를 그냥 쓰면 안 된다.</b> 스킨드 메시의 바운즈는 렌더 루프가
        /// 한 번 돌아야 갱신되는데, 프리팹을 막 인스턴스한 EditMode 테스트에서는 그 루프가 아직
        /// 안 돌아서 머리 높이가 0.005m 로 잡혔다 — 그대로 두면 머리 콜라이더가 사실상 점이 된다.
        /// 그래서 바인드 포즈 메시 바운즈의 여덟 꼭짓점을 직접 월드로 옮겨 잰다. 지연 갱신에
        /// 안 걸리고, 회전이 섞여도 맞다.
        /// </summary>
        private void MeasureRenderedSpan(out float bottom, out float top)
        {
            top = float.NegativeInfinity;
            bottom = float.PositiveInfinity;

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : null;
                if (mesh == null)
                {
                    top = Mathf.Max(top, renderer.bounds.max.y);
                    bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                    continue;
                }

                var local = mesh.bounds;
                var matrix = renderer.transform.localToWorldMatrix;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z);
                    var world = matrix.MultiplyPoint3x4(point);
                    top = Mathf.Max(top, world.y);
                    bottom = Mathf.Min(bottom, world.y);
                }
            }
        }

        private float CrownRise(IReadOnlyDictionary<string, Transform> bones)
        {
            var head = bones["head"];
            MeasureRenderedSpan(out _, out var top);

            var rise = top - head.position.y;
            if (rise > 0.01f) return rise;

            // 메시가 없거나 바운즈가 이상하면 목 길이로 대신한다 — 프로토타입이 아예 못 서는 것보다 낫다.
            return Mathf.Max(0.08f, Vector3.Distance(head.position, bones["chest"].position));
        }

        private static void DestroyComponent(Component component)
        {
            if (Application.isPlaying) Destroy(component);
            else DestroyImmediate(component);
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private readonly struct RestPose
        {
            public RestPose(Transform bone)
            {
                _bone = bone;
                _localPosition = bone.localPosition;
                _localRotation = bone.localRotation;
            }

            private readonly Transform _bone;
            private readonly Vector3 _localPosition;
            private readonly Quaternion _localRotation;

            public void Apply()
            {
                if (_bone == null) return;
                _bone.localPosition = _localPosition;
                _bone.localRotation = _localRotation;
            }
        }
    }
}
