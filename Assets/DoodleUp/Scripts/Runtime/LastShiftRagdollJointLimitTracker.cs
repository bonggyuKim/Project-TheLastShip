using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 관절 하나가 <b>어느 자유도로 몇 도 새는지</b>를 잰다. 상대 회전을 조인트 프레임에서
    /// 비틀림·스윙1·스윙2 로 분해해 각각 제 한계와 견준다.
    ///
    /// <b>왜 크기 하나로 재면 안 되나.</b> 예전 검사는 상대 회전의 <b>각도 크기</b>를 재고
    /// <c>max(스윙) + max(비틀림)</c> 이라는 합계 예산에 나눴다. 엉덩이가 스윙 콘 <c>30/10</c> ·
    /// 비틀림 <c>-20..70</c> 이면 예산이 100도가 되므로, <b>다리가 스윙으로 56도 벌어져도
    /// "0.56배 — 한계 안"</b> 으로 찍힌다. 실제로 그렇게 찍혔고(2026-08-21), 그 사이 사용자
    /// 화면에서는 다리가 개구리처럼 퍼져 있었다. 자유도를 안 나누면 새는 자유도를 못 본다.
    ///
    /// <b>비틀림 부호는 뒤집어 견준다.</b> Unity 의 <c>lowTwistLimit</c>/<c>highTwistLimit</c> 는
    /// 여기서 재는 축 둘레 각과 부호가 반대다. 실측(솔버 6/20/40 세 벌 모두)으로 멈추는 자리가
    /// 정확히 반대 부호의 경계였다 — 엉덩이 <c>-20..70</c> 이 <c>-70.2</c> 에서, 어깨
    /// <c>-50..10</c> 이 <c>+50.6</c> 에서, 목 <c>-10..5</c> 가 <c>+10.3</c> 에서 멈췄다.
    /// 안 뒤집으면 <b>한계를 지키고 있는 관절이 50도씩 새는 것으로</b> 찍힌다.
    /// <see cref="HingeJoint"/> 는 부호가 그대로다 — 무릎 <c>-80..5</c> 는 굽는 쪽인
    /// <c>-89</c> 에서 멈춘다.
    ///
    /// <b>경첩은 축 밖으로 샌 각을 따로 적는다.</b> 경첩에는 축 밖 자유도가 <b>아예 없으므로</b>,
    /// 거기서 나온 각은 한계가 넓은 것이 아니라 솔버가 못 버틴 것이다. 둘을 같이 세면 원인을
    /// 못 가른다.
    ///
    /// 검사와 측정 도구가 <b>같은 자</b>를 쓰게 하려고 런타임에 둔다.
    /// </summary>
    public sealed class LastShiftRagdollJointLimitTracker
    {
        public LastShiftRagdollJointLimitTracker(Joint joint)
        {
            _joint = joint;
            _hinge = joint as HingeJoint;
            var character = joint as CharacterJoint;

            var twistLocal = _hinge != null ? _hinge.axis : character != null ? character.axis : Vector3.right;
            var swingLocal = character != null ? character.swingAxis : Vector3.up;

            var child = joint.transform;
            var parent = joint.connectedBody.transform;

            var worldTwist = child.TransformDirection(twistLocal).normalized;
            var worldSwing = Orthogonal(child.TransformDirection(swingLocal), worldTwist);

            // 정지 자세의 월드 축을 양쪽 로컬로 한 번씩 옮겨 둔다. 그때가 PhysX 가 부모 프레임을
            // 굳히는 시점이라, 이후로는 두 프레임의 차이만 보면 같은 자를 쓰는 셈이 된다.
            _childTwist = child.InverseTransformDirection(worldTwist);
            _childSwing = child.InverseTransformDirection(worldSwing);
            _parentTwist = parent.InverseTransformDirection(worldTwist);
            _parentSwing = parent.InverseTransformDirection(worldSwing);

            Name = joint.name;
            IsHinge = _hinge != null;

            if (_hinge != null)
            {
                _limitLow = _hinge.useLimits ? _hinge.limits.min : -180f;
                _limitHigh = _hinge.useLimits ? _hinge.limits.max : 180f;
            }
            else if (character != null)
            {
                _limitLow = -character.highTwistLimit.limit;
                _limitHigh = -character.lowTwistLimit.limit;
                _swing1 = Mathf.Max(1f, character.swing1Limit.limit);
                _swing2 = Mathf.Max(1f, character.swing2Limit.limit);
            }
        }

        private readonly Joint _joint;
        private readonly HingeJoint _hinge;
        private readonly Vector3 _childTwist;
        private readonly Vector3 _childSwing;
        private readonly Vector3 _parentTwist;
        private readonly Vector3 _parentSwing;
        private readonly float _limitLow;
        private readonly float _limitHigh;
        private readonly float _swing1 = 1f;
        private readonly float _swing2 = 1f;

        public string Name { get; }
        public bool IsHinge { get; }

        /// <summary>가장 많이 샌 자유도의 이름. <c>twist</c>·<c>swing1</c>·<c>swing2</c>·<c>bend</c>·<c>offAxis</c>.</summary>
        public string WorstAxis { get; private set; } = "-";

        /// <summary>그때 그 자유도가 낸 각(도).</summary>
        public float WorstDegrees { get; private set; }

        /// <summary>그 자유도의 한계(도).</summary>
        public float WorstLimit { get; private set; }

        /// <summary>한계를 넘은 만큼(도). <b>0 이어야 한다.</b></summary>
        public float WorstExcess { get; private set; }

        /// <summary>마지막으로 잰 세 각. 표에 그대로 적어 어느 자세였는지 남긴다.</summary>
        public float Twist { get; private set; }

        public float Swing1 { get; private set; }

        public float Swing2 { get; private set; }

        /// <summary>지금 한계를 넘은 각(도)을 돌려주고 최악값을 갱신한다.</summary>
        public float Sample(out string axis, out float degrees, out float limit)
        {
            axis = "-";
            degrees = 0f;
            limit = 0f;
            if (_joint == null || _joint.connectedBody == null) return 0f;

            var child = _joint.transform;
            var parent = _joint.connectedBody.transform;

            var parentFrame = Frame(
                parent.TransformDirection(_parentTwist), parent.TransformDirection(_parentSwing));
            var childFrame = Frame(
                child.TransformDirection(_childTwist), child.TransformDirection(_childSwing));

            // 조인트 프레임 좌표계에서 본 상대 회전. x = 비틀림, y = 스윙1, z = 스윙2.
            Decompose(Quaternion.Inverse(parentFrame) * childFrame, out var twist, out var swing1, out var swing2);

            Twist = twist;
            Swing1 = swing1;
            Swing2 = swing2;

            if (_hinge != null)
            {
                var off = Mathf.Sqrt(swing1 * swing1 + swing2 * swing2);
                var overBend = Mathf.Max(0f, Mathf.Max(twist - _limitHigh, _limitLow - twist));
                return overBend >= off
                    ? Record("bend", twist, twist > _limitHigh ? _limitHigh : _limitLow, overBend,
                        out axis, out degrees, out limit)
                    : Record("offAxis", off, 0f, off, out axis, out degrees, out limit);
            }

            var twistExcess = Mathf.Max(0f, Mathf.Max(twist - _limitHigh, _limitLow - twist));
            var swing1Excess = Mathf.Max(0f, Mathf.Abs(swing1) - _swing1);
            var swing2Excess = Mathf.Max(0f, Mathf.Abs(swing2) - _swing2);

            if (twistExcess >= swing1Excess && twistExcess >= swing2Excess)
            {
                return Record("twist", twist, twist > _limitHigh ? _limitHigh : _limitLow, twistExcess,
                    out axis, out degrees, out limit);
            }

            return swing1Excess >= swing2Excess
                ? Record("swing1", Mathf.Abs(swing1), _swing1, swing1Excess, out axis, out degrees, out limit)
                : Record("swing2", Mathf.Abs(swing2), _swing2, swing2Excess, out axis, out degrees, out limit);
        }

        private float Record(string axisName, float value, float budget, float excess,
            out string axis, out float degrees, out float limit)
        {
            axis = axisName;
            degrees = value;
            limit = budget;
            if (excess > WorstExcess)
            {
                WorstExcess = excess;
                WorstAxis = axisName;
                WorstDegrees = value;
                WorstLimit = budget;
            }

            return excess;
        }

        /// <summary>비틀림 축을 x, 스윙 축을 y 로 두는 정규 직교 프레임.</summary>
        public static Quaternion Frame(Vector3 twist, Vector3 swing)
        {
            var x = twist.normalized;
            var y = Orthogonal(swing, x);
            var z = Vector3.Cross(x, y);
            // Unity 의 LookRotation 은 z 를 앞, y 를 위로 잡는다. 그러면 로컬 x 가 cross(y,z) = 비틀림 축이 된다.
            return Quaternion.LookRotation(z, y);
        }

        private static Vector3 Orthogonal(Vector3 value, Vector3 axis)
        {
            var result = value - axis * Vector3.Dot(value, axis);
            return result.sqrMagnitude > 1e-8f ? result.normalized : Vector3.Cross(axis, Vector3.up).normalized;
        }

        /// <summary>
        /// 스윙-비틀림 분해. 비틀림은 x 축 성분만 남긴 사원수로, 스윙은 나머지로 가른다.
        /// 스윙 각을 y·z 로 다시 나눠 각각 제 한계와 견줄 수 있게 한다.
        /// </summary>
        public static void Decompose(Quaternion q, out float twist, out float swing1, out float swing2)
        {
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);

            var norm = Mathf.Sqrt(q.x * q.x + q.w * q.w);
            Quaternion twistQuaternion;
            if (norm < 1e-6f)
            {
                twistQuaternion = Quaternion.identity;
                twist = 0f;
            }
            else
            {
                twistQuaternion = new Quaternion(q.x / norm, 0f, 0f, q.w / norm);
                twist = 2f * Mathf.Atan2(twistQuaternion.x, twistQuaternion.w) * Mathf.Rad2Deg;
                if (twist > 180f) twist -= 360f;
                if (twist < -180f) twist += 360f;
            }

            var swing = q * Quaternion.Inverse(twistQuaternion);
            if (swing.w < 0f) swing = new Quaternion(-swing.x, -swing.y, -swing.z, -swing.w);

            var sin = Mathf.Sqrt(Mathf.Max(0f, swing.y * swing.y + swing.z * swing.z));
            if (sin < 1e-6f)
            {
                swing1 = 0f;
                swing2 = 0f;
                return;
            }

            var angle = 2f * Mathf.Atan2(sin, swing.w) * Mathf.Rad2Deg;
            swing1 = angle * (swing.y / sin);
            swing2 = angle * (swing.z / sin);
        }
    }
}
