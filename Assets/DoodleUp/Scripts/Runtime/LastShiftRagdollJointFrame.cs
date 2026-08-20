using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 관절 하나의 <b>축 프레임</b>을 뼈 배치에서 뽑는다. 비틀림 축과 그에 수직인 스윙 축.
    ///
    /// <b>왜 따로 있나.</b> 같은 계산이 <see cref="LastShiftRagdoll"/> 안에 있었는데, 씬에 실제로
    /// 들어가는 승무원은 콜라이더를 손으로 잡은 프리팹이라 그 빌더를 안 거친다. 그래서 프리팹의
    /// 조인트 축은 <b>아무도 이 규칙으로 세운 적이 없었고</b>, 그대로 버그가 됐다 —
    /// 실측(2026-08-21)으로 비틀림 축과 뼈 방향의 내적이 엉덩이 <c>0.21</c>, 어깨 <c>0.55</c>,
    /// 허리·목 <c>0.00</c> 이었다. <b>1.0 이어야 하는 값이다.</b>
    ///
    /// 축이 어긋나면 한계가 엉뚱한 자유도를 막는다. 엉덩이의 비틀림 허용 <c>-20..70</c> 은
    /// 다리를 제 축 둘레로 돌리라고 준 90도인데, 축이 뼈에서 78도 어긋나 있으면 그 90도가
    /// <b>굽힘</b>이 된다 — 스윙 콘 30/10 은 그 굽힘을 못 막는다. 넘어지면 다리가 몸통까지
    /// 접혀 들어가고, 그동안 어느 한계도 "초과"로 안 찍힌다. 사용자가 본 뭉개진 덩어리가 이것이다.
    ///
    /// <b>그래서 규칙을 한 곳에 둔다.</b> 빌더와 프리팹 정비 도구가 같은 함수를 부르지 않으면
    /// 둘은 또 갈라진다. 갈라진 것을 화면으로 알아채는 데 사흘이 걸렸다.
    /// </summary>
    public static class LastShiftRagdollJointFrame
    {
        /// <summary>
        /// 비틀림 축 = <b>뼈가 뻗은 방향</b>. 끝 뼈가 있으면 그쪽으로, 없으면 부모에서 자기 쪽으로.
        /// </summary>
        public static Vector3 TwistDirection(Transform bone, Transform tip, Transform parent, Vector3 fallback)
        {
            if (tip != null)
            {
                var delta = tip.position - bone.position;
                if (delta.sqrMagnitude > 1e-8f) return delta.normalized;
            }

            if (parent != null)
            {
                var fromParent = bone.position - parent.position;
                if (fromParent.sqrMagnitude > 1e-8f) return fromParent.normalized;
            }

            return fallback.normalized;
        }

        /// <summary>
        /// 비틀림 축에 수직인 두 번째 축. <b>뼈 자신의 정지 로컬 축에서 고른다.</b>
        ///
        /// 예전에는 캐릭터의 위쪽(또는 앞쪽)과 외적을 썼다. 좌우 대칭 리그에서는 그것으로도
        /// 좌우가 거울처럼 나왔지만, 지금 리그는 정지 자세부터 좌우가 다르다(오른팔이 왼팔보다
        /// 최대 10.8cm 뒤에 있다). 캐릭터 기준 축 하나로 양쪽을 뽑으면 같은 관절이 좌우로 다르게
        /// 기울어진 프레임을 받고, 그러면 같은 충격에도 한쪽만 한계를 밟는다.
        ///
        /// 뼈의 로컬 축 셋 중 <b>비틀림 축과 가장 수직인 것</b>을 골라 직교화한다.
        /// </summary>
        public static Vector3 SwingAxis(Transform bone, Vector3 twist, Vector3 referenceUp, Vector3 referenceForward)
        {
            var best = Vector3.zero;
            var bestAlignment = float.MaxValue;
            for (var i = 0; i < 3; i++)
            {
                var candidate = i switch
                {
                    0 => bone.right,
                    1 => bone.up,
                    _ => bone.forward
                };
                var alignment = Mathf.Abs(Vector3.Dot(candidate, twist));
                if (alignment >= bestAlignment) continue;
                bestAlignment = alignment;
                best = candidate;
            }

            // 비틀림 축 성분을 뺀다. 안 빼면 축 둘이 직교하지 않아 PhysX 가 프레임을 자기 식대로
            // 다시 세우고, 그러면 한계면이 의도한 자리에서 어긋난다.
            var swing = best - twist * Vector3.Dot(best, twist);
            if (swing.sqrMagnitude > 1e-6f) return swing.normalized;

            // 뼈 로컬 축이 전부 비틀림 축과 나란한 퇴화 상황. 캐릭터 기준으로 물러난다.
            var reference = Mathf.Abs(Vector3.Dot(twist, referenceUp)) < 0.95f ? referenceUp : referenceForward;
            return Vector3.Cross(twist, reference).normalized;
        }

        /// <summary>
        /// 축 둘레 <b>양의</b> 회전이 이 관절이 접혀야 할 쪽인가.
        /// 뼈 끝점을 실제로 조금 돌려 보고, 그 끝점이 캐릭터 앞뒤 어느 쪽으로 가는지로 판단한다.
        /// </summary>
        public static bool BendsPositive(Vector3 twist, Vector3 axis, Vector3 characterForward, bool bendsForward)
        {
            var moved = Quaternion.AngleAxis(10f, axis) * twist - twist;
            var forward = Vector3.Dot(moved, characterForward);
            return bendsForward ? forward > 0f : forward < 0f;
        }

        /// <summary>
        /// 프레임이 제대로 섰는지를 한 숫자로. <b>1 에 가까워야 한다</b> — 비틀림 축과 뼈 방향의
        /// 내적 절댓값이다. 검사와 정비 도구가 같은 자로 보게 여기 둔다.
        /// </summary>
        public static float TwistAlignment(Transform bone, Transform tip, Transform parent, Vector3 localTwistAxis)
        {
            var boneDirection = TwistDirection(bone, tip, parent, Vector3.up);
            var axis = bone.TransformDirection(localTwistAxis).normalized;
            return Mathf.Abs(Vector3.Dot(axis, boneDirection));
        }
    }
}
