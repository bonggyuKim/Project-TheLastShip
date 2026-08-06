using UnityEngine;

namespace DoodleUp.Runtime
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class LastShiftGrabbable : MonoBehaviour
    {
        [SerializeField] private LastShiftItemRole role;
        [SerializeField] private bool secured;

        private Rigidbody body;
        private Transform originalParent;

        /// <summary>
        /// 드는 동안 충돌을 꺼 둔 상대 콜라이더들. <b>든 물건은 잡은 사람을 밀면 안 된다</b> —
        /// kinematic 으로 바꿔도 콜라이더는 살아 있고, 물건이 소켓에 붙어 눈앞에 오므로
        /// CharacterController 가 매 프레임 그것을 밀어내며 플레이어가 튕겨 나간다.
        ///
        /// 레이어를 나누지 않고 쌍으로 끄는 이유는, 물건이 <b>다른</b> 승무원과 배 구조물과는
        /// 계속 부딪혀야 하기 때문이다. 레이어로 빼면 든 사람뿐 아니라 전부와 안 부딪힌다.
        ///
        /// 되돌릴 대상을 들고 있어야 한다. <c>UnityEngine.Physics.IgnoreCollision</c> 은 쌍에 남는 상태라
        /// 놓을 때 같은 쌍으로 풀지 않으면 그 물건은 영영 그 사람을 통과한다.
        /// </summary>
        private readonly System.Collections.Generic.List<Collider> holderColliders = new();
        [SerializeField] private Vector3 nominalPosition;
        [SerializeField] private Quaternion nominalRotation = Quaternion.identity;
        [SerializeField] private bool spawnSecured;

        public LastShiftItemRole Role => role;
        public bool Secured => secured;

        /// <summary>
        /// 고정 사유 구분. true 면 승무원이 제자리에 고정한 것이고, false 면 프리셋 초기 상태로 고정된 것이다.
        /// 프롬프트가 "왜 못 잡는지"를 구분해서 보여주기 위해 필요하다.
        /// </summary>
        public bool SecuredByCrew { get; private set; }
        public bool IsHeld { get; private set; }
        public Vector3 NominalPosition => nominalPosition;
        public float DisplacementFromNominal => secured ? 0f : Vector3.Distance(transform.position, nominalPosition);
        public Rigidbody Body => body != null ? body : GetComponent<Rigidbody>();

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            originalParent = transform.parent;
            nominalPosition = transform.position;
            nominalRotation = transform.rotation;
            spawnSecured = secured;
            LastShiftShipPhysics.ConfigureItemBody(body);
            ApplyPhysicsState();
        }

        /// <summary>
        /// 선내 저중력을 직접 적용한다. useGravity 를 끈 대신 여기서 가속을 준다.
        /// FixedUpdate 이므로 물리 스텝과 정확히 1:1 이고, 프레임률에 따라 낙하가 달라지지 않는다.
        /// secured/held 는 kinematic 이라 ApplyShipGravity 가 스스로 걸러낸다.
        /// </summary>
        private void FixedUpdate()
        {
            LastShiftShipPhysics.ApplyShipGravity(body, Time.fixedDeltaTime);
        }

        public void Configure(LastShiftItemRole itemRole, bool startsSecured)
        {
            role = itemRole;
            secured = startsSecured;
            body = GetComponent<Rigidbody>();
            originalParent = transform.parent;
            nominalPosition = transform.position;
            nominalRotation = transform.rotation;
            spawnSecured = startsSecured;
            LastShiftShipPhysics.ConfigureItemBody(body);
            ApplyPhysicsState();
        }

        public void Grab(Transform socket)
        {
            if (socket == null) return;
            BeginHold();
            IgnoreHolderCollisions(socket);
            transform.SetParent(socket, false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        public void BeginNetworkHold(Transform socket)
        {
            if (socket == null) return;
            BeginHold();
            IgnoreHolderCollisions(socket);
            transform.SetPositionAndRotation(socket.position, socket.rotation);
        }

        /// <summary>
        /// 원격 사본. 소켓을 모르므로 충돌 해제 대상도 모른다 — 이 경로는 위치가 네트워크
        /// 트랜스폼으로 오고 로컬 플레이어가 그것을 들고 있지도 않다.
        /// </summary>
        public void BeginReplicatedHold()
        {
            BeginHold();
        }

        public void EndNetworkHold(Vector3 velocity)
        {
            IsHeld = false;
            secured = false;
            SecuredByCrew = false;
            RestoreHolderCollisions();
            body.isKinematic = false;
            body.linearVelocity = velocity;
        }

        public void ApplyReplicatedSecured(bool value)
        {
            ApplyReplicatedSecured(value, false);
        }

        public void ApplyReplicatedSecured(bool value, bool byCrew)
        {
            IsHeld = false;
            secured = value;
            SecuredByCrew = value && byCrew;
            RestoreHolderCollisions();
            ApplyPhysicsState();
        }

        public void Drop(Vector3 velocity)
        {
            IsHeld = false;
            transform.SetParent(originalParent, true);
            RestoreHolderCollisions();
            body.isKinematic = false;
            body.linearVelocity = velocity;
        }

        public void SetSecured(bool value)
        {
            SetSecured(value, false);
        }

        public void SetSecured(bool value, bool byCrew)
        {
            IsHeld = false;
            secured = value;
            SecuredByCrew = value && byCrew;
            RestoreHolderCollisions();
            transform.SetParent(originalParent, true);
            if (secured)
                transform.SetPositionAndRotation(nominalPosition, nominalRotation);
            ApplyPhysicsState();
        }

        public bool TrySecureAtNominal(float maxDistance)
        {
            if (IsHeld || secured || Vector3.Distance(transform.position, nominalPosition) > Mathf.Max(0f, maxDistance))
                return false;

            SetSecured(true, true);
            return true;
        }

        public void ApplyImpact(in LastShiftMeteorStimulus meteor, float severity)
        {
            if (secured || IsHeld || body == null) return;

            var direction = meteor.ImpactVector.sqrMagnitude > 0.0001f
                ? meteor.ImpactVector.normalized
                : Vector3.zero;
            var pointProximity = 1f / (1f + Vector3.Distance(transform.position, meteor.ImpactPoint) / 6f);
            var nominalDisplacement = Vector3.Distance(transform.position, nominalPosition);
            var roleResponse = role switch
            {
                LastShiftItemRole.Battery => 1.25f,
                LastShiftItemRole.CoolingCanister => 1.05f,
                LastShiftItemRole.PatchPlate => 1.15f,
                _ => 0.75f
            };
            var displacement = direction * (severity * pointProximity * roleResponse * 1.6f);
            transform.position += displacement;
            body.linearVelocity = direction * (severity * pointProximity * roleResponse * 2.4f);
            body.angularVelocity = Vector3.Cross(direction, Vector3.up) * (severity * roleResponse * 1.5f);
            Debug.Log($"[LAST_SHIFT_ITEM_IMPACT] role={role} point={meteor.ImpactPoint} vector={meteor.ImpactVector} E={meteor.Energy:F1} nominalTravel={nominalDisplacement:F2}->{DisplacementFromNominal:F2}");
        }

        public void ResetItem()
        {
            RecoverToNominal(spawnSecured, false);
        }

        /// <summary>
        /// 월드 경계 이탈과 preset reset 이 공유하는 원자적 복구 경계. parent/pose/secured/held와
        /// Rigidbody 선·각속도를 한 번에 정리해 "위치는 돌아왔지만 계속 날아감" 또는
        /// "holder는 없는데 IsHeld=true" 같은 반쪽 상태를 남기지 않는다.
        /// </summary>
        public void RecoverToNominal(bool recoveredSecured, bool byCrew)
        {
            IsHeld = false;
            secured = recoveredSecured;
            SecuredByCrew = recoveredSecured && byCrew;
            transform.SetParent(originalParent, false);
            transform.SetPositionAndRotation(nominalPosition, nominalRotation);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            ApplyPhysicsState();
        }

        private void BeginHold()
        {
            IsHeld = true;
            secured = false;
            SecuredByCrew = false;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
        }

        /// <summary>
        /// 잡은 사람과의 충돌을 끈다. <paramref name="holder"/> 아래의 모든 콜라이더를 대상으로
        /// 하는 이유는 승무원이 <see cref="CharacterController"/> 하나로 끝나지 않을 수 있어서다
        /// (자식 콜라이더가 붙으면 그쪽으로 다시 밀린다).
        /// </summary>
        private void IgnoreHolderCollisions(Transform holder)
        {
            RestoreHolderCollisions();
            if (holder == null) return;

            var root = holder.GetComponentInParent<LastShiftPlayerController>();
            var source = root != null ? root.transform : holder;
            var mine = GetComponentsInChildren<Collider>(true);
            foreach (var other in source.GetComponentsInChildren<Collider>(true))
            {
                if (other == null || other.transform.IsChildOf(transform)) continue;
                foreach (var self in mine)
                {
                    if (self == null) continue;
                    UnityEngine.Physics.IgnoreCollision(self, other, true);
                }

                holderColliders.Add(other);
            }
        }

        private void RestoreHolderCollisions()
        {
            if (holderColliders.Count == 0) return;

            var mine = GetComponentsInChildren<Collider>(true);
            foreach (var other in holderColliders)
            {
                // 놓는 순간 상대가 이미 파괴됐을 수 있다(씬 리로드·리스폰). 그때는 되돌릴
                // 쌍 자체가 없으므로 건너뛴다 — null 에 IgnoreCollision 을 부르면 예외다.
                if (other == null) continue;
                foreach (var self in mine)
                {
                    if (self == null) continue;
                    UnityEngine.Physics.IgnoreCollision(self, other, false);
                }
            }

            holderColliders.Clear();
        }

        private void ApplyPhysicsState()
        {
            if (body == null) return;
            if (!secured)
            {
                body.isKinematic = false;
                return;
            }

            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
        }
    }
}
