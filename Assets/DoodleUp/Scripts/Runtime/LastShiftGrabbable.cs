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
            ApplyPhysicsState();
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
            ApplyPhysicsState();
        }

        public void Grab(Transform socket)
        {
            if (socket == null) return;
            BeginHold();
            transform.SetParent(socket, false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        public void BeginNetworkHold(Transform socket)
        {
            if (socket == null) return;
            BeginHold();
            transform.SetPositionAndRotation(socket.position, socket.rotation);
        }

        public void BeginReplicatedHold()
        {
            BeginHold();
        }

        public void EndNetworkHold(Vector3 velocity)
        {
            IsHeld = false;
            secured = false;
            SecuredByCrew = false;
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
            ApplyPhysicsState();
        }

        public void Drop(Vector3 velocity)
        {
            IsHeld = false;
            transform.SetParent(originalParent, true);
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
