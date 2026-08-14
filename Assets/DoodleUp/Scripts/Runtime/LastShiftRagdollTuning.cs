using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 래그돌 물리의 튜닝 묶음. <b>프로토타입에서 실제로 손댈 값은 전부 여기 하나에 모아 둔다</b> —
    /// 값이 코드 여기저기 흩어지면 "무엇을 바꿔서 느낌이 달라졌는지"를 못 되짚는다.
    ///
    /// <see cref="Comic"/> 와 <see cref="WizardDefault"/> 두 프리셋이 있는 이유는 재현 실험 때문이다.
    /// 카드가 요구한 "저중력에서 래그돌이 안 멈추는 문제가 실제로 재현되는지"는 두 프리셋을 같은
    /// 시나리오로 돌려 정지 시각을 비교해야만 답이 나온다(<c>LastShiftRagdollCapture</c> 가 CSV 로 뽑는다).
    /// </summary>
    public sealed class LastShiftRagdollTuning
    {
        /// <summary>
        /// 선형 감쇠. 0 이면 저중력에서 튕긴 몸이 계속 미끄러져 화면 밖으로 나간다.
        /// 소품(<see cref="LastShiftShipPhysics.ItemLinearDamping"/> = 0.35)보다 낮게 잡았다 —
        /// 사람은 소품보다 무겁고, 너무 빨리 멈추면 "몇 초간 지켜보는 개그"가 안 된다.
        /// </summary>
        public float LinearDamping = 0.12f;

        /// <summary>
        /// 회전 감쇠. <b>여기가 이 프로토타입에서 가장 민감한 값이다.</b> 낮으면 팔다리가 영원히
        /// 팔랑거려 정지 판정이 안 서고, 높으면 첫 튕김부터 굳어서 안 웃긴다.
        ///
        /// <b>정지시키는 일은 여기 맡기지 않는다.</b> 0.70 까지 올려 봤더니 몸통 충돌만 멈추고
        /// 머리 튕기기·운석은 8초까지 흔들렸고, 그 대가로 날아가는 동안 팔다리가 굳었다.
        /// 정지는 <see cref="RestBrakeStrength"/> 가 맡고 이 값은 <b>공중에서의 팔랑거림</b>만
        /// 정한다 — 그래서 낮게 둔다.
        /// </summary>
        public float AngularDamping = 0.5f;

        /// <summary>
        /// 거의 멈춘 뒤에만 거는 회전 제동의 세기(1/s). 0 이면 안 건다.
        ///
        /// <b>왜 감쇠로는 안 되나.</b> 머리 콜라이더가 반지름 0.39m 짜리 <b>구</b>라, 넘어진 뒤
        /// 바닥에서 계속 굴러 흔들린다 — 구는 스스로 안 멈추는 모양이다. 회전 감쇠를 0.45 → 0.70
        /// 으로 올려 봤더니 몸통 충돌 하나만 멈추고(2.92초) 머리 튕기기·운석은 8초까지 잔여
        /// 각속도 0.70~1.46 rad/s 로 계속 흔들렸다. 감쇠를 더 올리면 이번엔 <b>날아가는 동안</b>
        /// 팔다리가 굳어 코믹이 죽는다. 그래서 감쇠는 낮게 두고, <b>이미 거의 멈춘 뒤에만</b>
        /// 회전을 따로 깎는다 — 웃긴 구간과 정리 구간을 분리한 것이다.
        /// </summary>
        public float RestBrakeStrength = 3.2f;

        /// <summary>
        /// 제동을 걸 <b>무게중심</b> 속도(m/s). 이보다 빠르면 아직 "날아가는 중"이다.
        ///
        /// 처음엔 <b>최대</b> 선속도로 재다가 두 번 실패했다 — 바닥에 누워 흔들리는 중에도
        /// 팔끝 하나가 문턱을 계속 넘겨서 제동이 아예 안 걸렸다(실측: 운석 시나리오가 8초까지
        /// 잔여 각속도 0.92~2.69 rad/s). 무게중심 속도는 팔이 아무리 팔랑거려도 거의 안 움직여서
        /// "몸이 아직 날아가는가"만 정확히 답한다.
        /// </summary>
        public float RestBrakeSpeed = 1.2f;

        /// <summary>
        /// 위 조건을 연속으로 만족해야 하는 시간(초). 저중력 포물선 정점에서 잠깐 느려진 것을
        /// 접지로 오인해 공중에서 회전이 굳는 걸 막는다.
        /// </summary>
        public float RestBrakeHoldSeconds = 0.3f;

        /// <summary>
        /// 최대 각속도(rad/s). Unity 기본 7 은 팔이 크게 휘두르는 순간 잘려 나가 밋밋해진다.
        /// </summary>
        public float MaxAngularSpeed = 22f;

        /// <summary>
        /// 슬립 임계. Unity 기본 0.005 는 저중력에서 사실상 도달하지 않는다 —
        /// 매 물리 스텝 중력을 손으로 넣으면 바디가 계속 깨어 있기 때문이다.
        /// </summary>
        public float SleepThreshold = 0.05f;

        public int SolverIterations = 12;
        public int SolverVelocityIterations = 4;

        /// <summary>
        /// 겹친 콜라이더를 밀어내는 속도의 상한(m/s). <b>여기를 안 잡으면 래그돌이 첫 프레임에
        /// 폭발한다</b> — 실제로 이 프로토타입의 첫 측정에서 승무원이 20m 상공으로 발사됐고,
        /// 원인은 충격이 아니라 생성 직후 서로 파고든 콜라이더를 PhysX 가 무한대에 가까운 속도로
        /// 밀어낸 것이었다. Unity 기본값(10)은 사람 크기 기준이라 이 작은 승무원에게는 너무 크다.
        /// </summary>
        public float MaxDepenetrationSpeed = 1.5f;

        /// <summary>
        /// 자기 몸끼리 충돌을 무시할 뼈 그래프 거리 상한. 기본값이 사실상 "전부 무시"인 이유는
        /// 이 승무원의 몸이 <b>땅딸막해서</b>다 — 팔을 내리면 위팔·아래팔 캡슐이 골반과 이미
        /// 겹쳐 있고, 그 겹침이 매 리셋마다 폭발의 씨앗이 된다. 팔이 몸통을 스쳐 지나가는 대신
        /// 래그돌이 안 터지는 쪽을 골랐다. 실험하려면 2~3 으로 낮춰 보면 된다.
        /// </summary>
        public int SelfCollisionIgnoreDistance = 99;

        /// <summary>
        /// 정지 판정을 쓸지. <c>false</c> 면 Unity 자체 슬립에만 맡긴다 —
        /// 저중력에서 안 멈추는 현상을 재현하는 쪽이 이 설정이다.
        /// </summary>
        public bool SettleEnabled = true;

        /// <summary>정지로 볼 최대 선속도(m/s).</summary>
        public float SettleLinearSpeed = 0.06f;

        /// <summary>정지로 볼 최대 각속도(rad/s).</summary>
        public float SettleAngularSpeed = 0.6f;

        /// <summary>
        /// 위 두 조건을 연속으로 만족해야 하는 시간(초). 저중력 포물선의 <b>정점</b>에서도 속도가
        /// 잠깐 0 에 가까워지므로 순간 판정은 공중에서 얼어붙게 만든다. 0.6초면 정점을 지나
        /// 이미 2m/s 넘게 떨어지고 있어 안전하다.
        /// </summary>
        public float SettleHoldSeconds = 0.6f;

        /// <summary>
        /// 정지 판정에 쓰는 속도의 평활 시간(초). 0 이면 순간값을 그대로 본다.
        ///
        /// <b>왜 필요한가.</b> 열두 바디가 바닥에 닿아 쉬고 있으면 솔버가 이따금 한 프레임짜리
        /// 각속도 스파이크를 낸다 — 실측으로 각속도가 0.2 rad/s 까지 잦아든 뒤에도 7.5초 지점에서
        /// 1.30 이 한 번 튀었다. 순간값으로 보면 그 한 프레임이 조용했던 시간을 통째로 지워서
        /// 운석 시나리오는 <b>영원히</b> 정지 판정이 안 섰다. 스파이크는 움직임이 아니라 수치
        /// 잡음이므로, 잡음을 걸러 낸 뒤에 재는 게 맞다.
        /// </summary>
        public float SettleSmoothingSeconds = 0.15f;

        /// <summary>
        /// 문 앞 충돌(R-1)로 몸 전체가 얻는 속도(m/s).
        ///
        /// <b>왜 임펄스가 아니라 속도인가.</b> 처음엔 가슴 한 부위에만 임펄스를 넣었는데,
        /// 가슴만 16m/s 로 튀고 나머지가 조인트에 끌려가는 모양이라 <b>몸이 밀려나는 게 아니라
        /// 제자리에서 회전</b>했다(실측: 골반이 0.25m 만 뜨고 끝났다). 실제 몸싸움은 몸 전체가
        /// 같이 밀리는 것이므로, 전체에 같은 속도 변화를 주고 국소 스냅을 따로 얹는다.
        /// </summary>
        public float BodyCheckSpeed = 3.4f;

        /// <summary>충돌 지점(가슴)에만 추가로 넣는 임펄스(N·s). 상체가 먼저 꺾이고 팔다리가 뒤따르게 한다.</summary>
        public float BodyCheckSnapImpulse = 22f;

        /// <summary>충격 방향의 위쪽 성분. 저중력에서 체공을 만드는 값이라 여기가 재미를 정한다.</summary>
        public float BodyCheckRise = 0.55f;

        /// <summary>머리만 톡 치는 임펄스. 목 관절이 얼마나 덜렁거리는지 보는 값이다.</summary>
        public float HeadFlickImpulse = 26f;

        /// <summary>
        /// 운석 충격(R-3)의 폭발 임펄스·반경. 운석은 지속되는 힘이 아니라 한 번의 타격이라
        /// <c>ForceMode.Impulse</c> 로 넣는다 — <c>Force</c> 로 넣으면 한 스텝(1/60초)만큼만
        /// 곱해져서 사실상 아무 일도 안 일어난다.
        /// </summary>
        /// 45 에서 30 으로 내린 근거: 45 는 승무원을 <b>굴려 보냈다</b>. 머리가 반지름 0.35m 짜리
        /// 구라 옆으로 넘어지면 공처럼 구르는데, 구름은 마찰로 거의 안 죽어서 저중력에서 8초
        /// 뒤에도 각속도 1.86 rad/s 로 계속 굴러갔다. 운석은 날려야지 볼링공을 만들면 안 된다.
        public float BlastImpulse = 30f;
        public float BlastRadius = 4.5f;

        /// <summary>선내 저중력(화성). 프로토타입은 전역 중력을 안 건드리고 이 값을 직접 적분한다.</summary>
        public float GravityY = LastShiftShipPhysics.GravityY;

        /// <summary>수평 방향(<paramref name="heading"/>)에 위쪽 성분을 얹은 실제 충격 방향.</summary>
        public Vector3 ImpactDirection(Vector3 heading)
        {
            var flat = new Vector3(heading.x, 0f, heading.z);
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
            return (flat.normalized + Vector3.up * BodyCheckRise).normalized;
        }

        /// <summary>이 카드가 목표로 하는 세팅.</summary>
        public static LastShiftRagdollTuning Comic()
        {
            return new LastShiftRagdollTuning();
        }

        /// <summary>
        /// Unity Ragdoll Wizard 가 만들어 주는 상태에 가까운 대조군. 감쇠 기본값, 슬립 임계 기본값,
        /// 정지 판정 없음. 중력만 저중력으로 맞춰 두는 이유는 <b>비교 대상에서 중력을 변수로 빼기
        /// 위해서</b>다 — 지구 중력까지 같이 바꾸면 무엇 때문에 멈췄는지 못 가른다.
        ///
        /// 같은 이유로 <see cref="MaxDepenetrationSpeed"/> 와 <see cref="SelfCollisionIgnoreDistance"/>
        /// 는 목표 튜닝과 <b>같은 값을 쓴다.</b> 이 둘까지 Unity 기본값으로 되돌리면 대조군이
        /// 폭발해 버려서, 알고 싶은 것("감쇠·슬립을 안 만지면 저중력에서 안 멈추는가")이
        /// 폭발에 묻혀 안 보인다.
        /// </summary>
        public static LastShiftRagdollTuning WizardDefault()
        {
            return new LastShiftRagdollTuning
            {
                LinearDamping = 0f,
                AngularDamping = 0.05f,
                MaxAngularSpeed = 7f,
                SleepThreshold = 0.005f,
                SolverIterations = 6,
                SolverVelocityIterations = 1,
                RestBrakeStrength = 0f,
                SettleEnabled = false
            };
        }

        /// <summary>지구 중력 대조군. 저중력이 왜 필요한지를 같은 장면으로 보여 주는 용도다.</summary>
        public LastShiftRagdollTuning WithEarthGravity()
        {
            var copy = (LastShiftRagdollTuning)MemberwiseClone();
            copy.GravityY = -9.81f;
            return copy;
        }
    }

    /// <summary>
    /// 정지 판정 상태. <b>물리 없이 숫자만으로 검사할 수 있게</b> 일부러 <c>MonoBehaviour</c> 밖으로
    /// 뺐다 — 공중 정점에서 얼어붙는 실수는 씬을 띄우지 않고 EditMode 에서 잡는 편이 싸다.
    /// </summary>
    public struct LastShiftRagdollSettle
    {
        /// <summary>연속으로 조용했던 시간(초).</summary>
        public float QuietSeconds;

        /// <summary>정지로 확정됐는지.</summary>
        public bool Settled;

        private float _linearAverage;
        private float _angularAverage;
        private bool _seeded;

        /// <summary>판정에 실제로 쓰이는 평활된 선속도(m/s).</summary>
        public float SmoothedLinearSpeed => _linearAverage;

        /// <summary>판정에 실제로 쓰이는 평활된 각속도(rad/s).</summary>
        public float SmoothedAngularSpeed => _angularAverage;

        /// <summary>한 물리 스텝만큼 진행하고 정지 여부를 돌려준다.</summary>
        public bool Step(float maxLinearSpeed, float maxAngularSpeed, float deltaTime, LastShiftRagdollTuning tuning)
        {
            if (tuning == null || !tuning.SettleEnabled)
            {
                QuietSeconds = 0f;
                Settled = false;
                return false;
            }

            if (!_seeded)
            {
                _linearAverage = maxLinearSpeed;
                _angularAverage = maxAngularSpeed;
                _seeded = true;
            }
            else
            {
                var step = Mathf.Max(0f, deltaTime);
                var alpha = tuning.SettleSmoothingSeconds <= 0f
                    ? 1f
                    : Mathf.Clamp01(step / (tuning.SettleSmoothingSeconds + step));
                _linearAverage += (maxLinearSpeed - _linearAverage) * alpha;
                _angularAverage += (maxAngularSpeed - _angularAverage) * alpha;
            }

            var quiet = _linearAverage <= tuning.SettleLinearSpeed
                        && _angularAverage <= tuning.SettleAngularSpeed;

            QuietSeconds = quiet ? QuietSeconds + Mathf.Max(0f, deltaTime) : 0f;
            Settled = QuietSeconds >= tuning.SettleHoldSeconds;
            return Settled;
        }

        /// <summary>충격이 새로 들어와 다시 움직이기 시작할 때 되돌린다.</summary>
        public void Wake()
        {
            QuietSeconds = 0f;
            Settled = false;
            _seeded = false;
        }
    }
}
