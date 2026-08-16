using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 선체 자세(<see cref="LastShiftShipState.ShipAttitudeDegrees"/>)의 관측 밴드.
    /// 경계값은 <see cref="LastShiftSituationTable"/> 의 <c>AttitudeDrift</c> 발동·해제
    /// 임계를 그대로 쓴다 — 화면이 "기울었다"고 말하는 구간과 판정이 상황을 접는 구간이
    /// 어긋나면, 플레이어는 눈으로 본 것과 결과 화면 원인 줄이 다르다고 읽는다.
    /// </summary>
    public enum LastShiftAttitudeBand
    {
        /// <summary>해제 임계(45°) 미만. 자세는 정보로 읽히되 사고로는 안 읽힌다.</summary>
        Level,

        /// <summary>해제(45°) 이상 발동(60°) 미만. 히스테리시스 구간 — 아직 상황은 아니다.</summary>
        Listing,

        /// <summary>발동 임계(60°) 이상. <c>AttitudeDrift</c> 가 서는 구간.</summary>
        Critical
    }

    /// <summary>
    /// 선체 자세의 관측 채널. 자세는 <c>BadAttitudeHighOxygen</c>(선체 파손) 판정을 만드는
    /// 두 축 중 하나인데, 지금까지 F3 디버그 문자열과 시뮬레이션 값으로만 존재해서 배가
    /// 실제로 기울지 않았다 — 플레이어가 결과 화면 전에 확인할 방법이 없었다.
    ///
    /// <b>실내 지오메트리는 기울이지 않는다.</b> 배 안 좌표는 전부 월드 축 상수에서 파생하고
    /// (<see cref="LastShiftShipDimensions"/>), 중력·<c>CharacterController</c>·구역 판정·
    /// 개구부 검증이 그 축을 전제한다. 선체 루트를 돌리면 콜라이더와 판정이 같이 돌아 EditMode
    /// 기하 검증이 통째로 어긋나고, 돌리지 않으면 벽 그림과 충돌면이 갈린다. 그래서 1인칭에서
    /// "배가 기울었다"로 읽히는 축 하나만 쓴다 — <b>카메라 롤</b>.
    ///
    /// 채널은 둘이다: (1) 자세에 비례하는 정상 롤, (2) 해제 임계를 넘은 뒤에만 붙는 저주파
    /// 흔들림. 둘을 나눈 이유는 정상 롤만으로는 "기울어 고정된 화면" 이라 조작 실수처럼 보이고,
    /// 흔들림이 붙어야 배가 자세를 못 잡고 있다는 시간 형태가 생기기 때문이다.
    ///
    /// 매 프레임 로그는 금지(SP-04 규칙)이므로 밴드가 바뀔 때만 한 줄 남긴다.
    /// </summary>
    public sealed class LastShiftAttitudeFeedback : MonoBehaviour
    {
        /// <summary>
        /// 자세 1도당 카메라 롤(도). 자세는 ±90 으로 잘리므로 이 비율이 곧 롤 상한을 정한다.
        /// 0.2 는 <c>AttitudeDrift</c> 발동값 60° 에서 12° 롤이 되는 값이다 — 수평선이
        /// 확실히 기운 것이 보이면서, 1인칭에서 계속 보고 있어도 멀미가 나는 대역
        /// (통상 20° 이상)에는 안 들어간다.
        /// </summary>
        public const float RollPerAttitudeDegree = 0.2f;

        /// <summary>롤 상한(도). 자세 ±90 에서의 값이라 비율과 함께 움직인다.</summary>
        public const float MaxCameraRollDegrees = 90f * RollPerAttitudeDegree;

        /// <summary>
        /// 롤 추종 속도(1/초). 지수 추종이라 값이 클수록 빨리 따라붙는다. 4 는 대략
        /// 0.6초면 목표 롤의 90% 에 닿는 값이다 — 프리셋 전환이나 자극으로 자세가 계단처럼
        /// 뛰어도 화면이 끊기지 않고, 조종 입력에 대한 반응은 같은 판 안에서 읽힌다.
        /// </summary>
        public const float RollFollowRate = 4f;

        /// <summary>흔들림 최대 진폭(도). 자세 ±90 에서의 값.</summary>
        public const float MaxSwayDegrees = 1.5f;

        /// <summary>흔들림 주기(Hz). 0.31 은 약 3.2초 한 주기라 "떨림" 이 아니라 "표류" 로 읽힌다.</summary>
        public const float SwayCyclesPerSecond = 0.31f;

        /// <summary>
        /// 승무원 재탐색 주기(초). 네트워크 씬에서 승무원이 도중에 스폰되므로 한 번만 찾아서는
        /// 늦게 들어온 화면에 롤이 안 걸린다. 반대로 매 프레임
        /// <see cref="Object.FindObjectsByType{T}(FindObjectsSortMode)"/> 는 비싸다.
        /// </summary>
        public const float PlayerRescanSeconds = 0.5f;

        private LastShiftSandboxController sandbox;
        private LastShiftPlayerController[] crew = System.Array.Empty<LastShiftPlayerController>();
        private float rescanRemaining;
        private float elapsedSeconds;
        private bool hasBand;

        /// <summary>흔들림을 뺀 정상 롤(도). 지금 자세를 따라가고 있는 값이다.</summary>
        public float SteadyRollDegrees { get; private set; }

        /// <summary>이번 프레임에 승무원 카메라로 밀어 넣은 롤(도). 흔들림 포함.</summary>
        public float RollDegrees { get; private set; }

        /// <summary>마지막으로 읽은 자세의 밴드.</summary>
        public LastShiftAttitudeBand Band { get; private set; } = LastShiftAttitudeBand.Level;

        /// <summary>
        /// 자세에 대응하는 정상 롤. 부호를 뒤집는 이유는 <b>실내가 기우는 것처럼 보여야</b>
        /// 하기 때문이다 — 카메라를 자세와 같은 부호로 돌리면 머리만 갸웃한 것이 되고, 반대로
        /// 돌려야 배가 +방향(우현)으로 기울 때 실내가 화면에서 그쪽으로 내려앉는다.
        /// </summary>
        public static float SteadyRollOf(float attitudeDegrees) =>
            Mathf.Clamp(-attitudeDegrees, -90f, 90f) * RollPerAttitudeDegree;

        /// <summary>
        /// 저주파 흔들림. 해제 임계(<see cref="LastShiftSituationTable.AttitudeReleaseDegrees"/>)
        /// 아래에서는 0 이다 — 정상 항해 프리셋(자세 8°·12°)까지 흔들면 흔들림이 정보를 잃고
        /// 그냥 카메라 버릇이 된다. 시간의 순수 함수라 서버·클라이언트가 같은 값을 만든다.
        /// </summary>
        public static float SwayOf(float attitudeDegrees, float elapsedSeconds)
        {
            var magnitude = Mathf.Min(Mathf.Abs(attitudeDegrees), 90f);
            var past = Mathf.InverseLerp(LastShiftSituationTable.AttitudeReleaseDegrees, 90f, magnitude);
            if (past <= 0f) return 0f;
            return Mathf.Sin(elapsedSeconds * SwayCyclesPerSecond * 2f * Mathf.PI) * MaxSwayDegrees * past;
        }

        /// <summary>
        /// 자세의 관측 밴드. <c>AttitudeDrift</c> 의 발동·해제 임계를 그대로 나눈다.
        /// 히스테리시스가 아니라 <b>단순 구간</b>인 것이 맞다 — 상황 래치는
        /// <see cref="LastShiftSituationTracker"/> 가 이미 들고 있고, 여기서 또 래치하면
        /// 두 벌의 상태가 서로 다른 시점에 접힌다.
        /// </summary>
        public static LastShiftAttitudeBand BandOf(float attitudeDegrees)
        {
            var magnitude = Mathf.Abs(attitudeDegrees);
            if (magnitude >= LastShiftSituationTable.AttitudeTriggerDegrees) return LastShiftAttitudeBand.Critical;
            if (magnitude >= LastShiftSituationTable.AttitudeReleaseDegrees) return LastShiftAttitudeBand.Listing;
            return LastShiftAttitudeBand.Level;
        }

        /// <summary>
        /// 자세를 물어올 샌드박스를 지정한다. 안 부르면 씬에서 하나 찾는다.
        /// 클라이언트에서 <see cref="LastShiftSandboxController"/> 컴포넌트가 꺼져 있어도
        /// <see cref="LastShiftSandboxController.CurrentState"/> 는 스냅샷으로 갱신되므로
        /// 읽는 쪽인 이 컴포넌트만 살아 있으면 된다.
        /// </summary>
        public void Configure(LastShiftSandboxController target)
        {
            sandbox = target;
        }

        private void LateUpdate()
        {
            if (sandbox == null) sandbox = GetComponent<LastShiftSandboxController>();
            if (sandbox == null) sandbox = FindFirstObjectByType<LastShiftSandboxController>(FindObjectsInactive.Include);
            if (sandbox == null) return;

            Tick(sandbox.CurrentState.ShipAttitudeDegrees, Time.deltaTime);
        }

        /// <summary>
        /// 한 프레임 진행. 시간을 밖에서 안 받고 내부 누적을 쓰는 이유는 흔들림 위상이
        /// <see cref="Time.time"/> 에 묶이면 씬을 다시 열 때마다 위상이 튀기 때문이다.
        /// 검증에서는 이 메서드를 직접 불러 프레임을 만든다.
        /// </summary>
        public void Tick(float attitudeDegrees, float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            elapsedSeconds += deltaTime;

            var target = SteadyRollOf(attitudeDegrees);
            // 지수 추종. deltaTime 이 커도 발산하지 않도록 1 - exp(-k dt) 형태로 쓴다.
            SteadyRollDegrees = Mathf.Lerp(SteadyRollDegrees, target, 1f - Mathf.Exp(-RollFollowRate * deltaTime));
            RollDegrees = SteadyRollDegrees + SwayOf(attitudeDegrees, elapsedSeconds);

            var band = BandOf(attitudeDegrees);
            if (!hasBand || band != Band)
            {
                hasBand = true;
                Band = band;
                Debug.Log($"[LAST_SHIFT_ATTITUDE_FEEDBACK] band={band} attitude={attitudeDegrees:F1} " +
                          $"roll={RollDegrees:F2} sway={SwayOf(attitudeDegrees, elapsedSeconds):F2}");
            }

            PushToCrew(deltaTime);
        }

        /// <summary>
        /// 승무원 카메라에 롤을 민다. 충격 흔들림과 같은 이유로 카메라 localRotation 을 직접
        /// 쓰지 않는다 — 조준(pitch/yaw)을 소유한
        /// <see cref="LastShiftPlayerController"/> 가 합성해야 다음 조준 갱신에 덮이지 않는다.
        /// </summary>
        private void PushToCrew(float deltaTime)
        {
            rescanRemaining -= deltaTime;
            if (rescanRemaining <= 0f || crew.Length == 0)
            {
                rescanRemaining = PlayerRescanSeconds;
                crew = FindObjectsByType<LastShiftPlayerController>(FindObjectsSortMode.None);
            }

            var offset = new Vector3(0f, 0f, RollDegrees);
            foreach (var member in crew)
                if (member != null) member.SetCameraAttitudeOffset(offset);
        }
    }
}
