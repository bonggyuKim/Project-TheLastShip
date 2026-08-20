using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 자기 몸끼리의 충돌을 <b>정지 자세에서 이미 겹쳐 있는 쌍만</b> 끈다.
    ///
    /// <b>왜 필요한가.</b> 손으로 얹은 래그돌에는 자기충돌 정책이 없어서 105쌍이 전부 살아 있었다.
    /// 그런데 이 승무원은 땅딸막해서 <b>차렷 자세에서 이미</b> 위팔·손·머리 캡슐이 골반 구
    /// (반지름 0.197 × 스케일 1.5 ≈ 30cm) 안에 파묻혀 있다 — 여덟 쌍이 최대 11cm 겹친 채로 시작한다.
    /// 겹친 채 만난 두 콜라이더는 솔버 예산을 통째로 먹으면서 서로를 밀어내고, 정작 막아야 할
    /// 접촉(발 ↔ 골반 같은)은 굶어서 안 풀린다. 실측으로 비인접 관통이 13쌍·최대 13cm까지 났다.
    ///
    /// <b>그래서 겹친 쌍만 끈다.</b> 폭발의 씨앗은 겹침이지 촌수가 아니므로, 겹침을 직접 재는 쪽이
    /// 촌수로 뭉뚱그리는 것보다 정확하다 — 안 겹친 쌍은 전부 살아남아 서로를 막는다.
    /// 같은 판단이 <see cref="LastShiftRagdoll"/> 의 프록시 빌더에도 있는데, 그쪽은 콜라이더를
    /// 제 손으로 만들어 붙이므로 손으로 잡아 둔 콜라이더를 살려야 하는 이 씬에서는 쓸 수 없다.
    ///
    /// <b>조인트로 이어진 쌍은 손대지 않는다.</b> <c>CharacterJoint.enableCollision = false</c> 면
    /// PhysX 가 내부에서 이미 그 쌍을 빼므로 여기서 또 끌 이유가 없다.
    ///
    /// <b>물리가 움직이기 전에 재야 한다.</b> 날아가는 중에 재면 그때 우연히 겹친 쌍까지 꺼져
    /// 그 뒤로 영영 서로를 안 막는다. 그래서 <see cref="Awake"/> 에서 한 번만 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftRagdollSelfCollision : MonoBehaviour
    {
        /// <summary>이 깊이 미만은 접촉 오프셋 수준이라 겹친 것으로 보지 않는다(m).</summary>
        private const float OverlapEpsilon = 0.005f;

        /// <summary>충돌을 끈 쌍 수. 0 이면 정책이 안 돈 것이다 — 검사가 이 값을 본다.</summary>
        public int IgnoredPairs { get; private set; }

        /// <summary>살려 둔 쌍 수. 0 이면 몸이 서로를 통과한다.</summary>
        public int KeptPairs { get; private set; }

        private void Awake() => Apply();

        /// <summary>
        /// 정책을 한 번 적용한다. 헤드리스 검사도 같은 함수를 쓴다 —
        /// 검사가 다른 규칙을 돌면 검사가 아니다.
        /// </summary>
        public void Apply()
        {
            IgnoredPairs = 0;
            KeptPairs = 0;

            var linked = LinkedBodies();
            var colliders = GetComponentsInChildren<Collider>(true);

            for (var a = 0; a < colliders.Length; a++)
            for (var b = a + 1; b < colliders.Length; b++)
            {
                var first = colliders[a];
                var second = colliders[b];
                if (first == null || second == null) continue;

                // 조인트로 이어진 쌍은 PhysX 담당이다. 여기서 세지도 끄지도 않는다.
                if (linked.Contains(PairKey(BodyNameOf(first), BodyNameOf(second)))) continue;

                if (!OverlapsAtRest(first, second))
                {
                    KeptPairs++;
                    continue;
                }

                Physics.IgnoreCollision(first, second, true);
                IgnoredPairs++;
            }
        }

        /// <summary>조인트가 직접 잇는 바디 쌍. 이름 쌍으로 들고 있는다.</summary>
        private HashSet<string> LinkedBodies()
        {
            var linked = new HashSet<string>();
            foreach (var joint in GetComponentsInChildren<CharacterJoint>(true))
            {
                if (joint.connectedBody == null) continue;
                linked.Add(PairKey(joint.name, joint.connectedBody.name));
            }

            return linked;
        }

        /// <summary>
        /// 콜라이더가 붙은 게임오브젝트가 아니라 <b>그 부모 뼈</b> 이름을 쓴다.
        /// 콜라이더는 뼈 밑의 홀더(<c>*_Col</c>)에 달려 있어서, 홀더 이름으로는 조인트와 못 맞춘다.
        /// </summary>
        private static string BodyNameOf(Collider collider)
        {
            var body = collider.attachedRigidbody;
            if (body != null) return body.name;
            return collider.transform.parent != null ? collider.transform.parent.name : collider.name;
        }

        private static string PairKey(string a, string b) =>
            string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;

        /// <summary>정지 자세에서 두 콜라이더가 이미 파고들어 있는가.</summary>
        private static bool OverlapsAtRest(Collider a, Collider b)
        {
            if (!Physics.ComputePenetration(
                    a, a.transform.position, a.transform.rotation,
                    b, b.transform.position, b.transform.rotation,
                    out _, out var distance))
            {
                return false;
            }

            return distance >= OverlapEpsilon;
        }
    }
}
