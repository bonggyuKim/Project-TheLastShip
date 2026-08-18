using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 래그돌 부위가 무언가에 부딪힌 사실을 <see cref="LastShiftBodyDeform"/> 으로 넘긴다.
    ///
    /// <b>Rigidbody 가 붙은 오브젝트에 얹는다.</b> 콜라이더는 <c>{Part}__RagdollCollider</c>
    /// 자식에 있지만 Unity 는 충돌 메시지를 <b>Rigidbody 쪽</b>으로 보낸다. 홀더에 붙이면
    /// 콜백이 아예 안 온다.
    ///
    /// <b>임펄스를 여기서 다시 걸지 않는다.</b> 충돌 반발은 PhysX 가 이미 처리했고, 여기서
    /// <c>AddForce</c> 를 또 하면 같은 충돌이 두 번 밀어 래그돌이 튄다. 이 릴레이가 하는 일은
    /// 그 충돌을 <b>표현층으로 옮기는 것</b>뿐이다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class LastShiftRagdollContactRelay : MonoBehaviour
    {
        /// <summary>
        /// 충격량 1 N·s 당 몇 미터를 눌 것인가. 물리적 유도값이 아니라 연출 계수다 —
        /// 살이 얼마나 무른지는 기획·아트가 정하지 물리가 정하지 않는다.
        /// </summary>
        public const float DefaultDepthPerImpulse = 0.012f;

        private LastShiftBodyDeform _deform;
        private LastShiftRagdollPart _part;
        private float _radius;
        private float _depthPerImpulse = DefaultDepthPerImpulse;

        /// <summary>이 릴레이가 대변하는 부위.</summary>
        public LastShiftRagdollPart Part => _part;

        /// <summary>눌림 반경(월드 m). 부위 콜라이더 굵기에서 뽑는다.</summary>
        public float Radius => _radius;

        /// <summary>
        /// 부위·반경을 박는다. 래그돌 빌드가 콜라이더 치수를 이미 알고 있으므로 그쪽이 넘긴다 —
        /// 여기서 다시 재면 두 곳이 갈린다.
        /// </summary>
        public void Configure(LastShiftBodyDeform deform, LastShiftRagdollPart part, float radius)
        {
            _deform = deform;
            _part = part;
            _radius = Mathf.Max(0.01f, radius);
        }

        /// <summary>연출 계수 조정. 튜닝 단계에서만 쓴다.</summary>
        public void SetDepthPerImpulse(float depthPerImpulse) => _depthPerImpulse = Mathf.Max(0f, depthPerImpulse);

        /// <summary>
        /// 검사와 다른 충격원(운석·폭발)이 부르는 진입점. 충돌 콜백과 <b>같은 자리</b>로 들어가야
        /// 연출이 갈리지 않으므로 <c>OnCollisionEnter</c> 도 이것을 부른다.
        /// </summary>
        public void ReportContact(Vector3 worldPoint, Vector3 worldNormal, float impulseMagnitude)
        {
            if (_deform == null || impulseMagnitude <= 0f) return;
            _deform.AddContact(transform, worldPoint, worldNormal, impulseMagnitude * _depthPerImpulse, _radius);
        }

        private void OnCollisionEnter(Collision collision) => Relay(collision);

        private void OnCollisionStay(Collision collision) => Relay(collision);

        private void Relay(Collision collision)
        {
            if (_deform == null) return;

            // 접촉점이 여러 개면 가장 센 하나만 쓴다. 전부 먹이면 슬롯 여덟 개가 한 충돌로
            // 다 차서 다음 충돌이 들어올 자리가 없다.
            var impulse = collision.impulse.magnitude;
            if (impulse <= 0f) return;

            var count = collision.contactCount;
            if (count <= 0) return;

            var deepest = collision.GetContact(0);
            for (var i = 1; i < count; i++)
            {
                var candidate = collision.GetContact(i);
                if (candidate.separation < deepest.separation) deepest = candidate;
            }

            // 법선은 상대에게서 나를 향한다. 눌림은 그 반대로 파고들어야 하므로 뒤집는다.
            ReportContact(deepest.point, -deepest.normal, impulse);
        }
    }
}
