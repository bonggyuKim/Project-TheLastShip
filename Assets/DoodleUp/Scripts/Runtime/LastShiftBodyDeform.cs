using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 승무원 몸의 국소 눌림. <b>표현 전용이다 — 물리에 되먹이지 않는다.</b>
    ///
    /// 접촉 하나가 두 곳으로 간다. 물리는 <see cref="LastShiftRagdoll.ApplyImpulse"/> 가
    /// 받아 전신을 흔들고, 여기는 <b>그 자리가 눌리는 것</b>만 맡는다. 두 층은 서로 안 읽는다 —
    /// 래그돌이 잠들어도(<see cref="LastShiftRagdoll.IsSettled"/>) 자국은 스스로 복원되고,
    /// 이 컴포넌트가 없어도 물리는 그대로다.
    ///
    /// <b>멀티에서 복제하지 않는다.</b> 임펄스는 이미 호스트 권위로 복제되므로 각 클라이언트가
    /// 같은 접촉에서 같은 변형을 만든다. 변형 자체를 복제하면 승무원 4인분 대역폭이 이유 없이 든다.
    ///
    /// <b>정점을 CPU 에서 안 만진다.</b> 슬롯 여덟 개를 셰이더에 넘기고 변위는 정점 셰이더가
    /// 한다. 그래서 뼈도 웨이트도 안 늘고, 메시가 한 장이든 여섯 장이든 같은 코드가 돈다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftBodyDeform : MonoBehaviour
    {
        /// <summary>슬롯 수. 셰이더 배열 길이와 <b>반드시</b> 같아야 한다.</summary>
        public const int SlotCount = 8;

        private static readonly int DeformPositionId = Shader.PropertyToID("_LSDeformPosition");
        private static readonly int DeformNormalId = Shader.PropertyToID("_LSDeformNormal");
        private static readonly int DeformCountId = Shader.PropertyToID("_LSDeformCount");

        [Header("스프링")]
        [Tooltip("복원 진동수(Hz). 높을수록 빨리 돌아온다.")]
        [SerializeField] private float frequency = 4.5f;

        [Tooltip("감쇠비. 1 미만이면 한 번 출렁이고 돌아온다 — 살 느낌은 여기서 난다.")]
        [SerializeField] private float damping = 0.45f;

        [Tooltip("접촉이 끊긴 뒤 목표 깊이가 0 으로 가기까지의 시간(초).")]
        [SerializeField] private float releaseSeconds = 0.08f;

        [Header("접촉")]
        [Tooltip("한 접촉이 낼 수 있는 최대 눌림 깊이(월드 m).")]
        [SerializeField] private float maxDepth = 0.06f;

        [Tooltip("같은 앵커에서 이 거리(월드 m) 안이면 새 슬롯을 안 쓰고 기존 것을 갱신한다.")]
        [SerializeField] private float mergeDistance = 0.08f;

        [Tooltip("깊이가 이 값(월드 m) 아래로 잦아들면 슬롯을 놓아 준다.")]
        [SerializeField] private float sleepDepth = 0.0008f;

        private readonly Slot[] _slots = new Slot[SlotCount];
        private readonly List<SkinnedMeshRenderer> _renderers = new List<SkinnedMeshRenderer>();
        private readonly Vector4[] _positionBuffer = new Vector4[SlotCount];
        private readonly Vector4[] _normalBuffer = new Vector4[SlotCount];
        private MaterialPropertyBlock _block;

        /// <summary>지금 살아 있는 슬롯 수. 검사와 진단이 본다.</summary>
        public int ActiveSlots
        {
            get
            {
                var count = 0;
                for (var i = 0; i < SlotCount; i++) if (_slots[i].Active) count++;
                return count;
            }
        }

        /// <summary>슬롯의 현재 눌림 깊이(월드 m). 스프링 검사가 이것만 본다.</summary>
        public float DepthOf(int slot) => _slots[slot].Depth;

        /// <summary>슬롯이 물고 있는 뼈. 접촉이 어느 부위로 갔는지 검사가 이것으로 본다.</summary>
        public Transform AnchorOf(int slot) => _slots[slot].Anchor;

        /// <summary>몸 렌더러 수. 셰이더가 실제로 몇 장에 걸리는지 검사가 본다.</summary>
        public int RendererCount => _renderers.Count;

        private void Awake() => CollectRenderers();

        /// <summary>
        /// 몸 렌더러를 다시 찾는다. 이름이 아니라 구조로 찾는 규칙은
        /// <see cref="LastShiftCrewBody"/> 하나에만 있고 여기도 그것을 쓴다 — 셸이 다시
        /// 쪼개지거나 합쳐져도 따라간다.
        /// </summary>
        public void CollectRenderers()
        {
            _renderers.Clear();
            var body = transform.Find(LastShiftCrewBody.RootName);
            _renderers.AddRange(LastShiftCrewBody.Renderers(body != null ? body : transform));
        }

        /// <summary>
        /// 접촉 하나를 먹인다. <paramref name="anchor"/> 는 맞은 부위의 뼈다.
        ///
        /// <b>앵커 로컬로 저장하는 이유.</b> 오브젝트 공간에 그대로 박아 두면 래그돌이 굴러갈 때
        /// 자국만 공중에 남는다. 뼈에 매달아 두고 매 프레임 렌더러 공간으로 다시 쏘면 따라간다.
        /// </summary>
        public void AddContact(Transform anchor, Vector3 worldPoint, Vector3 worldNormal, float strength, float radius)
        {
            if (anchor == null || radius <= 0.0001f) return;
            var depth = Mathf.Min(Mathf.Abs(strength), maxDepth);
            if (depth <= sleepDepth) return;
            if (worldNormal.sqrMagnitude < 0.000001f) return;

            var localPoint = anchor.InverseTransformPoint(worldPoint);
            var localNormal = anchor.InverseTransformDirection(worldNormal.normalized);
            var index = PickSlot(anchor, worldPoint);

            // 같은 자리를 다시 맞으면 더 깊은 쪽을 남긴다. 약한 접촉이 강한 자국을 지우면
            // 세게 맞은 것이 화면에서 사라진다.
            var sameSpot = _slots[index].Active && _slots[index].Anchor == anchor;
            if (!sameSpot || depth > _slots[index].Target)
            {
                _slots[index].Target = depth;
                _slots[index].Radius = radius;
                _slots[index].LocalPoint = localPoint;
                _slots[index].LocalNormal = localNormal;
            }
            _slots[index].Anchor = anchor;
            _slots[index].Active = true;
            _slots[index].IdleSeconds = 0f;
        }

        private int PickSlot(Transform anchor, Vector3 worldPoint)
        {
            var mergeSqr = mergeDistance * mergeDistance;
            var freeIndex = -1;
            var weakestIndex = 0;
            var weakestDepth = float.MaxValue;

            for (var i = 0; i < SlotCount; i++)
            {
                if (!_slots[i].Active)
                {
                    if (freeIndex < 0) freeIndex = i;
                    continue;
                }
                if (_slots[i].Anchor == anchor &&
                    (_slots[i].Anchor.TransformPoint(_slots[i].LocalPoint) - worldPoint).sqrMagnitude <= mergeSqr)
                    return i;
                if (_slots[i].Depth < weakestDepth)
                {
                    weakestDepth = _slots[i].Depth;
                    weakestIndex = i;
                }
            }
            return freeIndex >= 0 ? freeIndex : weakestIndex;
        }

        /// <summary>
        /// 스프링 한 스텝. <c>LateUpdate</c> 뿐 아니라 헤드리스 검사도 같은 함수를 부른다 —
        /// 검사가 다른 적분을 돌면 검사가 아니다.
        /// </summary>
        public void Step(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            var omega = 2f * Mathf.PI * Mathf.Max(0.01f, frequency);
            var release = Mathf.Max(0.0001f, releaseSeconds);

            for (var i = 0; i < SlotCount; i++)
            {
                if (!_slots[i].Active) continue;
                if (_slots[i].Anchor == null)
                {
                    _slots[i].Clear();
                    continue;
                }

                _slots[i].IdleSeconds += deltaTime;
                if (_slots[i].IdleSeconds > release) _slots[i].Target = 0f;

                // 준음함 적분. 명시적 오일러는 진동수를 올리면 발산한다.
                var acceleration = -omega * omega * (_slots[i].Depth - _slots[i].Target)
                                   - 2f * damping * omega * _slots[i].Velocity;
                _slots[i].Velocity += acceleration * deltaTime;
                _slots[i].Depth += _slots[i].Velocity * deltaTime;

                if (_slots[i].Target <= 0f &&
                    Mathf.Abs(_slots[i].Depth) < sleepDepth &&
                    Mathf.Abs(_slots[i].Velocity) < sleepDepth) _slots[i].Clear();
            }
        }

        private void LateUpdate()
        {
            Step(Time.deltaTime);
            PushToRenderers();
        }

        /// <summary>
        /// 슬롯을 렌더러마다의 오브젝트 공간으로 옮겨 넘긴다.
        ///
        /// <b>스케일을 나눠 준다.</b> 접촉 반경과 깊이는 월드 미터인데 정점 셰이더는 오브젝트
        /// 공간에서 잰다. 씬이 승무원을 1.5배로 쓰므로 그냥 넘기면 자국만 1.5배로 커진다 —
        /// 콜라이더에서 이미 한 번 겪은 어긋남이다.
        /// </summary>
        public void PushToRenderers()
        {
            if (_renderers.Count == 0) return;
            if (_block == null) _block = new MaterialPropertyBlock();

            for (var r = 0; r < _renderers.Count; r++)
            {
                var target = _renderers[r];
                if (target == null) continue;

                var toObject = target.transform;
                var lossy = toObject.lossyScale;
                var scale = (Mathf.Abs(lossy.x) + Mathf.Abs(lossy.y) + Mathf.Abs(lossy.z)) / 3f;
                if (scale <= 0.0001f) continue;

                var count = 0;
                for (var i = 0; i < SlotCount; i++)
                {
                    if (!_slots[i].Active || _slots[i].Anchor == null) continue;
                    if (Mathf.Abs(_slots[i].Depth) < sleepDepth) continue;

                    var world = _slots[i].Anchor.TransformPoint(_slots[i].LocalPoint);
                    var normal = _slots[i].Anchor.TransformDirection(_slots[i].LocalNormal);
                    var objectPoint = toObject.InverseTransformPoint(world);
                    var objectNormal = toObject.InverseTransformDirection(normal).normalized;

                    _positionBuffer[count] = new Vector4(
                        objectPoint.x, objectPoint.y, objectPoint.z, _slots[i].Radius / scale);
                    _normalBuffer[count] = new Vector4(
                        objectNormal.x, objectNormal.y, objectNormal.z, _slots[i].Depth / scale);
                    count++;
                }
                for (var i = count; i < SlotCount; i++)
                {
                    _positionBuffer[i] = Vector4.zero;
                    _normalBuffer[i] = Vector4.zero;
                }

                target.GetPropertyBlock(_block);
                _block.SetVectorArray(DeformPositionId, _positionBuffer);
                _block.SetVectorArray(DeformNormalId, _normalBuffer);
                _block.SetFloat(DeformCountId, count);
                target.SetPropertyBlock(_block);
            }
        }

        /// <summary>모든 자국을 즉시 지운다. 리셋·리스폰이 부른다.</summary>
        public void ClearContacts()
        {
            for (var i = 0; i < SlotCount; i++) _slots[i].Clear();
            PushToRenderers();
        }

        private struct Slot
        {
            public Transform Anchor;
            public Vector3 LocalPoint;
            public Vector3 LocalNormal;
            public float Radius;
            public float Depth;
            public float Velocity;
            public float Target;
            public float IdleSeconds;
            public bool Active;

            public void Clear()
            {
                Anchor = null;
                Depth = 0f;
                Velocity = 0f;
                Target = 0f;
                IdleSeconds = 0f;
                Active = false;
            }
        }
    }
}
