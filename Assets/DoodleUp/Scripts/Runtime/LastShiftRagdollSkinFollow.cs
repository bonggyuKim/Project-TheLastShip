using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 물리를 안 받는 변형본을 대응 부위의 <b>물리 델타</b>로 따라가게 한다.
    ///
    /// <b>왜 따로 있나.</b> 같은 계산이 <see cref="LastShiftRagdoll"/> 안에도 있는데(HelperBone),
    /// 그쪽은 프록시 골격을 세우면서 콜라이더까지 제 손으로 만든다. 손으로 잡아 둔 부위별
    /// 콜라이더 모양을 살려야 하는 씬에서는 그 빌더를 못 쓰므로, 찢어짐만 막는 이 조각을
    /// 따로 뗀다. 래그돌 자체(리지드바디·조인트·콜라이더)는 <b>하나도 안 건드린다</b>.
    ///
    /// <b>무엇이 찢어졌나.</b> Rigify 는 변형본을 제어본(<c>ORG-</c>/<c>MCH-</c>/<c>tweak</c>)
    /// 밑에 흩어 놓는다. 래그돌은 그중 열둘에만 바디를 주므로
    /// <c>DEF-shoulder.L/R</c>·<c>DEF-breast.L/R</c>·<c>DEF-pelvis.L/R</c> 여섯은 부모가
    /// 물리를 안 받아 <b>바인드 포즈에 박혀</b> 있었다 — 실측으로 정점 웨이트의 6.9%
    /// (478.4 / 6,921)가 제자리에 남아 어깨·가슴·골반에서 삼각형이 길게 늘어났다.
    ///
    /// <b>부모는 절대 안 바꾼다.</b> 재부모화로 고쳐 봤다가 임포트된 bindposes 와 어긋나
    /// 스킨 행렬이 통째로 깨졌다(2026-08-18 반려본). 계층은 그대로 두고 월드 포즈만 민다.
    ///
    /// <b>층이 둘이다.</b>
    /// <list type="number">
    /// <item>바디도 없고 부모도 안 움직이는 변형본 여섯을 대응 부위에 매단다(<see cref="Link"/>).</item>
    /// <item>바디가 있는 두 뼈 <b>사이에</b> 낀 변형본에 굽힘을 나눠 준다(<see cref="Segment"/>).
    /// 래그돌은 <c>DEF-spine.006</c>(머리) 하나만 돌리고 <c>DEF-spine.004</c>·<c>.005</c>(목)는
    /// 가슴에 딱 붙어 있어서, 목이 꺾이는 각이 전부 고리 하나에 몰려 찢어졌다. 블렌더에서는
    /// <c>head</c> 컨트롤이 목 전체를 나눠 굽히므로 같은 각도에서 멀쩡하다.</item>
    /// </list>
    ///
    /// <b>물리 이후에 불러야 한다.</b> 플레이에서는 <see cref="LateUpdate"/>, 에디터에서
    /// <c>Physics.Simulate</c> 를 손으로 밟는 캡처는 그 다음에 <see cref="Apply"/> 를 직접 부른다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftRagdollSkinFollow : MonoBehaviour
    {
        /// <summary>변형본 이름 앞머리. 리그의 정본 규칙이라 여기서도 이것으로 거른다.</summary>
        private const string DeformBonePrefix = "DEF-";

        [Tooltip("바디 있는 두 뼈 사이에 낀 변형본에 굽힘을 나눠 준다. 끄면 중간 뼈가 부모에 딱 붙는다 — 끄고 켜서 눈으로 비교하라고 남겨 둔 스위치다.")]
        [SerializeField] private bool distributeBend = true;

        private readonly List<Link> _links = new List<Link>();
        private readonly List<Segment> _segments = new List<Segment>();

        /// <summary>물리가 자리를 정하는 뼈들. 얕은 것부터 — 되돌릴 때 부모를 먼저 놓아야 한다.</summary>
        private readonly List<Transform> _driven = new List<Transform>();
        private Vector3[] _drivenPositions = System.Array.Empty<Vector3>();
        private Quaternion[] _drivenRotations = System.Array.Empty<Quaternion>();

        private LastShiftRagdoll _ragdoll;
        private bool _captured;

        /// <summary>실제로 물린 뼈 수. 이름이 바뀌어 매핑이 끊기면 여기서 바로 드러난다.</summary>
        public int LinkCount => _links.Count;

        /// <summary>굽힘을 나눠 주는 구간 수. 리그가 바뀌어 중간 뼈가 사라지면 0 이 된다.</summary>
        public int SegmentCount => _segments.Count;

        /// <summary>구간 하나가 품은 중간 뼈 수. 목 구간이 둘(.004·.005)인지 검사가 이것으로 본다.</summary>
        public int MidBoneCount(int segment) => _segments[segment].MidCount;

        private void Awake()
        {
            _ragdoll = GetComponent<LastShiftRagdoll>();
            Capture();
        }

        private void OnEnable()
        {
            if (!_captured) Capture();
        }

        /// <summary>
        /// 정지 포즈를 다시 잰다. <b>물리가 움직이기 전에</b> 불러야 한다 — 날아가는 중에 부르면
        /// 그 자세가 새 기준이 돼 자국이 어긋난 채 고정된다.
        /// </summary>
        public void Capture()
        {
            _links.Clear();
            _segments.Clear();
            _driven.Clear();

            var all = GetComponentsInChildren<Transform>(true);
            var attachments = LastShiftRagdollRig.HelperAttachments;
            for (var i = 0; i < attachments.Length; i++)
            {
                var (boneName, attachTo) = attachments[i];
                var bone = Find(all, boneName);
                var source = Find(all, LastShiftRagdollRig.SpecOf(attachTo).BoneName);
                if (bone == null || source == null) continue;
                _links.Add(new Link(bone, source));
            }

            CaptureSegments(all);
            _captured = true;
        }

        /// <summary>
        /// 바디가 있는 두 뼈 사이에 낀 변형본을 구간으로 묶는다.
        ///
        /// <b>표가 아니라 계층에서 찾는다.</b> 어느 뼈에 바디가 붙어 있는지는 씬마다 다르고
        /// (이 랩은 손으로 얹은 열다섯이다) 표에 박아 두면 리그를 바꿀 때마다 조용히 어긋난다.
        /// 리지드바디가 있는 뼈에서 부모를 타고 올라가 다음 리지드바디를 만나면 그 사이가 구간이다.
        ///
        /// <b>변형본만 넣는다.</b> 사이에 <c>ORG-</c>·<c>MCH-</c> 제어본이 끼어도 웨이트가 없어
        /// 화면에 안 나오므로 돌릴 이유가 없다. 웨이트를 <b>읽어서</b> 거르지 않는 이유는 하나다 —
        /// 승무원 메시는 <c>isReadable=false</c> 로 임포트돼 있어서 플레이어 빌드에서
        /// <c>GetAllBoneWeights</c> 가 던진다. 이름 규칙은 이 리그의 정본이고
        /// <see cref="LastShiftRagdollRig"/> 도 같은 규칙으로 뼈를 찾는다.
        /// </summary>
        private void CaptureSegments(Transform[] all)
        {
            for (var i = 0; i < all.Length; i++)
                if (all[i].GetComponent<Rigidbody>() != null) _driven.Add(all[i]);

            // 얕은 것부터. 되돌릴 때 부모를 먼저 놓지 않으면 자식이 두 번 끌려간다.
            _driven.Sort((a, b) => Depth(a).CompareTo(Depth(b)));
            _drivenPositions = new Vector3[_driven.Count];
            _drivenRotations = new Quaternion[_driven.Count];

            var drivenSet = new HashSet<Transform>(_driven);
            var mids = new List<Transform>();
            foreach (var child in _driven)
            {
                mids.Clear();
                var parent = child.parent;
                while (parent != null && !drivenSet.Contains(parent))
                {
                    if (parent.name.StartsWith(DeformBonePrefix)) mids.Add(parent);
                    parent = parent.parent;
                }

                // 위로 끝까지 갔는데 바디를 못 만났다 — 계층이 아니라 조인트로만 이어진 부위다.
                if (parent == null || mids.Count == 0) continue;

                mids.Reverse();  // 부모 → 자식 순서로 세운다
                _segments.Add(new Segment(parent, mids.ToArray(), child));
            }
        }

        private static int Depth(Transform t)
        {
            var depth = 0;
            for (var p = t.parent; p != null; p = p.parent) depth++;
            return depth;
        }

        /// <summary>물린 뼈를 한 번 갱신한다. 헤드리스 검사도 같은 함수를 쓴다.</summary>
        public void Apply()
        {
            DistributeBend();
            for (var i = 0; i < _links.Count; i++) _links[i].Follow();
        }

        /// <summary>
        /// 구간마다 굽힘을 나눠 준다.
        ///
        /// <b>순서가 전부다.</b> 중간 뼈를 돌리면 그 밑에 달린 <b>바디 있는 뼈</b>가 같이 끌려간다.
        /// 그래서 (1) 물리가 정한 월드 포즈를 먼저 적어 두고 (2) 중간 뼈를 돌린 뒤
        /// (3) 적어 둔 포즈를 얕은 것부터 도로 씌운다. 안 그러면 팔다리가 굽힘만큼 어긋난다.
        ///
        /// 나누는 비율은 <see cref="LastShiftRagdollRig.BendShareOf"/> 가 정한다 — 실측한 뼈는
        /// 그 값, 나머지는 등분이다. 목은 등분이 아니라 0%/50% 라는 것이 아트 실측으로 확인됐다.
        /// </summary>
        /// <summary>굽힘 분산을 쓰는가. 검사와 비교 촬영이 이것을 끄고 켠다.</summary>
        public bool DistributeBendEnabled
        {
            get => distributeBend;
            set => distributeBend = value;
        }

        private void DistributeBend()
        {
            if (!distributeBend || _segments.Count == 0) return;

            for (var i = 0; i < _driven.Count; i++)
            {
                if (_driven[i] == null) continue;
                _drivenPositions[i] = _driven[i].position;
                _drivenRotations[i] = _driven[i].rotation;
            }

            for (var i = 0; i < _segments.Count; i++) _segments[i].Bend();

            for (var i = 0; i < _driven.Count; i++)
            {
                if (_driven[i] == null) continue;
                _driven[i].SetPositionAndRotation(_drivenPositions[i], _drivenRotations[i]);
            }
        }

        private void LateUpdate()
        {
            // 프록시 빌더가 도는 씬에서는 그쪽 HelperBone 이 이미 같은 일을 한다. 둘이 겹치면
            // 같은 값을 두 번 쓸 뿐이지만, 기준 포즈가 서로 달라 미묘하게 어긋날 수 있다.
            if (_ragdoll != null && _ragdoll.IsBuilt) return;
            Apply();
        }

        private static Transform Find(Transform[] all, string boneName)
        {
            for (var i = 0; i < all.Length; i++)
                if (all[i].name == boneName) return all[i];
            return null;
        }

        /// <summary>
        /// 바디 있는 두 뼈와 그 사이에 낀 변형본들. 양 끝이 정지 포즈에서 <b>얼마나 돌았는지</b>를
        /// 재서, 그 사이를 등분한 회전을 중간 뼈에 준다.
        ///
        /// 위치는 안 건드린다 — 중간 뼈는 부모(바디 있는 뼈)의 자식이라 위치가 이미 따라와 있고,
        /// 월드 위치를 손대면 뼈 길이가 늘어나 오히려 메시가 찢어진다.
        /// </summary>
        private readonly struct Segment
        {
            public Segment(Transform parent, Transform[] mids, Transform child)
            {
                _parent = parent;
                _child = child;
                _mids = mids;
                _parentRest = parent.rotation;
                _childRest = child.rotation;
                _midRest = new Quaternion[mids.Length];
                for (var i = 0; i < mids.Length; i++) _midRest[i] = mids[i].rotation;
            }

            private readonly Transform _parent;
            private readonly Transform _child;
            private readonly Transform[] _mids;
            private readonly Quaternion _parentRest;
            private readonly Quaternion _childRest;
            private readonly Quaternion[] _midRest;

            public int MidCount => _mids.Length;

            public void Bend()
            {
                if (_parent == null || _child == null) return;

                var fromParent = _parent.rotation * Quaternion.Inverse(_parentRest);
                var fromChild = _child.rotation * Quaternion.Inverse(_childRest);

                for (var i = 0; i < _mids.Length; i++)
                {
                    if (_mids[i] == null) continue;
                    var share = LastShiftRagdollRig.BendShareOf(_mids[i].name, i, _mids.Length);
                    _mids[i].rotation = Quaternion.Slerp(fromParent, fromChild, share) * _midRest[i];
                }
            }
        }

        /// <summary>
        /// 변형본 하나와 그것이 따라갈 물리 뼈. 정지 시점의 <b>월드</b> 포즈 둘을 들고 있다가,
        /// 물리 뼈가 정지 포즈에서 돈 만큼을 제 정지 포즈에 곱한다. 로컬 값도 부모도 안 건드리므로
        /// bindpose 와 어긋날 일이 없다.
        /// </summary>
        private readonly struct Link
        {
            public Link(Transform bone, Transform source)
            {
                _bone = bone;
                _source = source;
                _restBonePosition = bone.position;
                _restBoneRotation = bone.rotation;
                _restSourcePosition = source.position;
                _restSourceRotation = source.rotation;
            }

            private readonly Transform _bone;
            private readonly Transform _source;
            private readonly Vector3 _restBonePosition;
            private readonly Quaternion _restBoneRotation;
            private readonly Vector3 _restSourcePosition;
            private readonly Quaternion _restSourceRotation;

            public void Follow()
            {
                if (_bone == null || _source == null) return;

                var delta = _source.rotation * Quaternion.Inverse(_restSourceRotation);
                _bone.SetPositionAndRotation(
                    _source.position + delta * (_restBonePosition - _restSourcePosition),
                    delta * _restBoneRotation);
            }
        }
    }
}
