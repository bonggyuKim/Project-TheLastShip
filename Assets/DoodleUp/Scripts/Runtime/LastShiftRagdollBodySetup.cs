using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 손으로 얹은 래그돌 바디에 <b>프록시 빌더와 같은 안정화 설정</b>을 입힌다.
    ///
    /// <b>왜 필요한가.</b> 리지드바디의 솔버 반복 수·디페네트레이션 상한은 <b>직렬화되지 않는다</b> —
    /// 인스펙터에도 없고 프리팹 YAML 에도 안 남는다. 코드로 넣는 수밖에 없는데,
    /// <see cref="LastShiftRagdoll"/> 의 <c>ConfigureBody</c> 는 프록시 골격을 <b>제 손으로 만들 때만</b>
    /// 돈다. 씬에 실제로 들어가는 승무원은 콜라이더를 손으로 잡은 프리팹이라 그 경로를 안 거치고,
    /// 그래서 Unity 기본값(솔버 <c>6/1</c> · 디페네트레이션 <c>10 m/s</c> · 각감쇠 <c>0.05</c>)으로
    /// 돌고 있었다.
    ///
    /// <b>기본값이 왜 나쁜가.</b> <see cref="LastShiftRagdollTuning.MaxDepenetrationSpeed"/> 의 주석에
    /// 이미 적혀 있다 — Unity 기본 10 은 사람 크기 기준이라 이 작은 승무원에게는 너무 크고,
    /// 겹친 콜라이더를 그 속도로 밀어내면 래그돌이 터진다. 실측(2026-08-21)으로 착지 프레임에서
    /// 발목이 스윙 콘 30도를 <b>87도까지</b> 뚫었고, 이것만 1.5 로 낮춰도 80도로 줄었다.
    ///
    /// <b>경첩만 반복을 더 올린다.</b> 무릎·팔꿈치는 한 축만 열려 있어서 나머지 두 자유도를
    /// 솔버가 매 스텝 0 으로 눌러야 한다. 기본 반복으로는 못 눌러서 축 밖으로 21도까지 샜다.
    /// 전역으로 올리면 열다섯 바디가 전부 비싸지므로 <see cref="LastShiftRagdoll"/> 과 같은
    /// 배수(4배)만 쓴다.
    ///
    /// <b>중력·질량·보간은 안 건드린다.</b> 그쪽은 저중력 프로토타입의 연출값이라 지구 중력으로
    /// 도는 랩 씬에 그대로 옮기면 다른 것을 바꾼 것이 된다. 여기는 <b>솔버가 조인트 한계를
    /// 지키게 하는 데 필요한 것만</b> 넣는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftRagdollBodySetup : MonoBehaviour
    {
        /// <summary>경첩이 달린 바디의 반복 배수. <see cref="LastShiftRagdoll"/> 과 같은 값이다.</summary>
        public const int HingeSolverBoost = 4;

        /// <summary>
        /// 슬립 임계. <b>튜닝값을 안 쓰고 엔진 기본값을 쓴다.</b>
        ///
        /// <see cref="LastShiftRagdollTuning.SleepThreshold"/> 가 Unity 기본(<c>0.005</c>)의 열 배인
        /// <c>0.05</c> 인 데는 이유가 있다 — <see cref="LastShiftRagdoll"/> 이 <b>중력을 매 물리 스텝
        /// 손으로 넣어서</b> 바디가 계속 깨어 있고, 기본값으로는 정지 판정에 사실상 도달하지 않는다.
        /// 그 필드 주석에도 그렇게 적혀 있다.
        ///
        /// <b>이 프리팹은 그 경로가 아니다.</b> 손으로 얹은 승무원은 <c>useGravity = 1</c> 로 엔진
        /// 중력을 받으므로 그 전제가 성립하지 않는다. 열 배 임계를 그대로 옮기면 정착 도중 바디가
        /// 훨씬 일찍 정지 판정에 들어가 <b>덜 정착한 자세로 굳는다</b>.
        ///
        /// <b>실측(2026-08-22, PlayMode).</b> 4초 정착 뒤 같은 바디체크를 먹였을 때 골반 이동:
        /// <c>0.05</c> 에서 <b>0.024m</b>, <c>0.005</c> 에서 <b>0.075m</b>. 잠든 바디 수는 두 경우
        /// 모두 <c>0/15</c> 였다 — 그러니 이 값은 "래그돌이 얼어붙는" 원인은 아니고, <b>반응의 크기를
        /// 3분의 1로 깎는</b> 값이다. 얼어붙어 보인 진짜 이유는 랩 씬에 미는 수단이 없던 것이고,
        /// 그쪽은 <see cref="LastShiftRagdollSoftLab"/> 이 맡는다.
        ///
        /// 솔버 반복·디페네트레이션은 저중력과 무관한 안정화값이라 그대로 튜닝에서 가져온다.
        /// </summary>
        public const float SleepThreshold = 0.005f;

        /// <summary>설정을 입힌 바디 수. 0 이면 정책이 안 돈 것이다 — 검사가 이 값을 본다.</summary>
        public int ConfiguredBodies { get; private set; }

        /// <summary>반복을 더 올린 경첩 바디 수. 무릎·팔꿈치 넷이 나와야 한다.</summary>
        public int BoostedBodies { get; private set; }

        private void Awake() => Apply();

        /// <summary>
        /// 한 번 적용한다. 헤드리스 검사도 같은 함수를 쓴다 —
        /// 검사가 다른 설정으로 돌면 검사가 아니다.
        /// </summary>
        public void Apply()
        {
            var tuning = LastShiftRagdollTuning.Comic();
            ConfiguredBodies = 0;
            BoostedBodies = 0;

            foreach (var body in GetComponentsInChildren<Rigidbody>(true))
            {
                if (body == null) continue;

                var boost = body.GetComponent<HingeJoint>() != null ? HingeSolverBoost : 1;
                body.solverIterations = tuning.SolverIterations * boost;
                body.solverVelocityIterations = tuning.SolverVelocityIterations * boost;
                body.maxDepenetrationVelocity = tuning.MaxDepenetrationSpeed;
                body.angularDamping = tuning.AngularDamping;
                body.sleepThreshold = SleepThreshold;

                ConfiguredBodies++;
                if (boost > 1) BoostedBodies++;
            }
        }
    }
}
