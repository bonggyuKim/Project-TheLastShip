using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 승무원 개인의 예비 산소(B-3). 선체 압력과 별개의 요소이며, 보충 지점은 0개다 —
    /// 항해 1회분 예산이므로 한 번 쓰면 돌아오지 않는다.
    ///
    /// 3단 구조에서 이 컴포넌트가 맡는 것은 가운데 단이다:
    ///   압력 0.15  → 사이렌만. 여기서는 아무것도 줄지 않는다(<see cref="LastShiftSandboxController"/> 소관).
    ///   압력 0.00  → 진공. 그 구역에 있는 승무원만 <see cref="Tick"/> 으로 소모가 시작된다.
    ///   예비 0.00  → 사망. 판정은 <see cref="LastShiftVerdictResolver"/> 가 "전원 사망" 일 때만 실패로 올린다.
    ///
    /// 두 임계를 겹치지 않게 두는 것이 설계 의도다. 겹치면 사이렌이 곧 사망 예고가 되어
    /// 대응 창이 사라진다. 사이렌부터 사망까지 최소 약 104초이고 최악 복구 경로의 3배다.
    ///
    /// 씬 저작이나 프리팹에 미리 붙이지 않아도 되도록 <see cref="Ensure"/> 로 필요할 때 붙인다.
    /// 솔로 씬·네트워크 프리팹·EditMode 테스트 팩토리가 각각 다른 경로로 승무원을 만들기 때문이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftCrewOxygen : MonoBehaviour
    {
        private LastShiftPlayerController playerController;
        private AudioSource breathAudio;
        private bool wasDraining;
        private float autoReturnAt = float.NegativeInfinity;
        private float warningAt = float.NegativeInfinity;

        /// <summary>남은 개인 예비 산소. 1.00 에서 시작해 진공 구역에서만 줄어든다.</summary>
        public float SuitOxygen { get; private set; } = LastShiftRecoveryTuning.SuitOxygenInitial;

        public bool IsDead { get; private set; }

        /// <summary>이번 tick 에 실제로 소모가 돌았는가. HUD 막대는 이 값이 한 번이라도 참이 된 뒤에만 뜬다.</summary>
        public bool IsDraining { get; private set; }

        /// <summary>
        /// 소모가 시작된 승무원에게만 막대를 띄운다(N8). 압력이 회복돼 소모가 멈춰도 이미 깎인
        /// 예비는 돌아오지 않으므로 막대는 계속 남는다 — 남은 예산이 곧 남은 실수 여유이기 때문이다.
        /// </summary>
        public bool ShowsSuitGauge => IsDead || SuitOxygen < LastShiftRecoveryTuning.SuitOxygenInitial;

        public bool IsCritical => !IsDead && SuitOxygen <= LastShiftRecoveryTuning.SuitOxygenCriticalThreshold;

        /// <summary>
        /// 조항 <c>O-7</c> 자동 복귀가 난 뒤 지난 시간. 한 번도 안 났으면 무한이다.
        ///
        /// <b>사망과 다른 상태다.</b> 회수는 <see cref="IsDead"/> 를 안 세우고, 세워지기
        /// <b>전에</b> 끊는 것이 그 조항의 전부다 — 그래서 이 둘을 같은 연출에 묶으면
        /// "왕복을 잃었다" 와 "끝났다" 가 한 색이 된다.
        /// </summary>
        public float AutoReturnElapsedSeconds =>
            autoReturnAt <= float.NegativeInfinity ? float.PositiveInfinity : Time.unscaledTime - autoReturnAt;

        /// <summary>
        /// 지금 자동 복귀 펄스가 켜져 있는가. <b>위기색이 아니라 warning 색</b>을 쓰는 자리이며
        /// (game-art 확정 2026-08-11), 두 번 깜빡이고 정상으로 돌아온다. 창이 끝나면 그냥
        /// 거짓이라 "복귀" 를 따로 안 그린다.
        /// </summary>
        public bool IsAutoReturnFlash =>
            !IsDead && LastShiftUiTheme.IsAutoReturnWarningPulse(AutoReturnElapsedSeconds);

        /// <summary>
        /// 첫 경고선을 넘은 그 순간의 <b>한 번짜리</b> 펄스(game-art 확정 — 경고는 한 번,
        /// 회수는 두 번, 임계는 유지). <b>판당 한 번이다</b>: 산소가 한 상태에서 단조로워
        /// 경계를 되넘지 않으므로 다시 깜빡일 자리가 없다.
        /// </summary>
        public bool IsWarningEntryFlash => !IsDead && warningAt > float.NegativeInfinity
            && LastShiftUiTheme.IsWarningPulse(Time.unscaledTime - warningAt, 1,
                LastShiftUiTheme.WarningEntryPulseSeconds, 0f);

        /// <summary>
        /// 첫 경고 구간. <b>임계보다 넓다</b> — <see cref="IsCritical"/> 이 참이면 이쪽도 참이다.
        /// UI 는 둘을 배타로 쓰지 말고 "경고 안에서 임계로 좁혀진다" 로 읽어야 한다.
        ///
        /// 진입과 해제가 <b>같은 값</b>이다. 히스테리시스를 안 두는 이유는 산소가 한 상태에서
        /// 단조롭기 때문이다 — 진공에서는 줄기만 하고 가압에서는 늘기만 한다. 경계를 한 번
        /// 넘으면 되돌아오지 않으므로 깜빡일 자리가 없다.
        ///
        /// <b>예외가 하나 있다.</b> 승무원이 가압·진공 경계에 걸쳐 서 있으면 프레임마다
        /// 소모와 회복이 번갈아 돌아 값이 임계 근처에서 진동할 수 있다. 그 자리에서 소리와
        /// 점멸이 떨리면 히스테리시스를 여기 넣는다 — 지금 안 넣는 것은 없는 문제를 미리
        /// 막지 않으려는 것이고, 넣을 자리는 이 속성 하나다.
        /// </summary>
        public bool IsWarning => !IsDead && SuitOxygen <= LastShiftRecoveryTuning.SuitOxygenWarningThreshold;

        /// <summary>적색 점멸 위상. 사이렌 칸과 예비 막대가 같은 박자로 뛰어야 같은 사건으로 읽힌다.</summary>
        public static float BlinkPhase => 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);

        /// <summary>
        /// 예비 산소 막대 한 줄. 서버는 <see cref="LastShiftSandboxController"/> 가 전원 분을 그리지만
        /// 클라이언트에서는 sandbox 가 꺼져 있어(<c>enabled = IsServer</c>) 아무도 그리지 않는다.
        /// 그래서 <see cref="LastShiftNetworkPlayer"/> 가 같은 함수로 자기 막대를 그린다 —
        /// 두 경로가 다른 코드로 그리면 서버와 클라이언트의 막대가 서로 다르게 보인다.
        /// </summary>
        public static void ApplyGauge(
            LastShiftUiLayer layer, LastShiftCrewOxygen crew, string id, string slotLabel, int row)
        {
            if (layer == null || crew == null || !crew.ShowsSuitGauge) return;

            var gauge = layer.Gauge(id, LastShiftUiIcon.Oxygen,
                LastShiftHudLayout.SuitGaugeRect(LastShiftUiLayer.ScreenSize.y, row));
            gauge.SetValue(crew.SuitOxygen);
            gauge.SetName(crew.IsDead
                ? $"{slotLabel} 예비 고갈 — 사망"
                : $"{slotLabel} 예비 산소" + (crew.IsDraining ? " (소모 중)" : " (소모 정지)"));
            gauge.SetValueLabel(crew.IsDead ? "0%" : $"{crew.SuitOxygen:P0}");

            // 사망은 회색이다. 위기색으로 두면 "아직 손쓸 수 있다" 로 읽히는데 이 줄은
            // 이미 끝난 상태를 적는다.
            //
            // <b>위기 점멸은 다른 게이지와 같은 함수를 쓴다</b>(PulseCrisis). 전에는 여기서만
            // 직접 섞어 사이렌 칸의 박자를 따라갔는데, 아트 규격이 이 자리에 PulseCrisis 를
            // 지목했고 자원 게이지도 이미 그것을 쓴다 - 게이지끼리 맞추는 쪽이 옳다. 사이렌은
            // 선체 사건이고 이쪽은 사람의 산소라 같은 사건도 아니다.
            // 자동 복귀 펄스는 <b>위기색 앞</b>에 온다. 회수 직후에는 예비가 다시 차 있어
            // IsCritical 이 이미 거짓이지만, 순서를 남겨 둬야 나중에 회수 임계가 바뀌어도
            // 회수가 위기로 안 읽힌다(game-art 확정 — 회수는 실패 통보가 아니다).
            gauge.SetTone(crew.IsDead
                ? new Color(0.45f, 0.45f, 0.45f)
                : crew.IsAutoReturnFlash || crew.IsWarningEntryFlash
                    ? LastShiftUiTheme.Fault
                    : crew.IsCritical
                        ? LastShiftUiTheme.PulseCrisis(Time.unscaledTime)
                        : LastShiftUiTheme.Nominal);
            gauge.SetThresholds();
        }

        public static LastShiftCrewOxygen Ensure(LastShiftPlayerController player)
        {
            if (player == null) return null;
            var existing = player.GetComponent<LastShiftCrewOxygen>();
            return existing != null ? existing : player.gameObject.AddComponent<LastShiftCrewOxygen>();
        }

        private void Awake()
        {
            playerController = GetComponent<LastShiftPlayerController>();
        }

        /// <summary>
        /// 한 스텝 소모한다. 진공 판정은 호출자가 한다 — 어느 구역이 진공인지는 선체 상태와
        /// 구역 차단 여부를 함께 봐야 알 수 있고, 그 정보는 sandbox 가 가지고 있다.
        /// </summary>
        public void Tick(bool inVacuum, float deltaTime)
        {
            IsDraining = false;
            if (IsDead || deltaTime <= 0f) return;
            if (!inVacuum)
            {
                // 압력이 0.00 위로 돌아오면 소모가 멈춘다. 회복은 없다.
                UpdateBreathAudio();
                return;
            }

            IsDraining = true;
            SuitOxygen = Mathf.Max(0f, SuitOxygen - LastShiftRecoveryTuning.SuitOxygenDrainPerSecond * deltaTime);
            // 경고선을 처음 넘은 시각. 되넘지 않으므로 한 번만 찍힌다.
            if (warningAt <= float.NegativeInfinity && IsWarning) warningAt = Time.unscaledTime;
            UpdateBreathAudio();
            if (SuitOxygen > LastShiftRecoveryTuning.AsphyxiationSuitOxygenThreshold) return;

            IsDead = true;
            IsDraining = false;
            StopBreathAudio();
            // 사망한 승무원은 더 이상 조작할 수 없고 도킹 판정에도 잡히지 않는다.
            // 남은 승무원으로 항해가 계속되는 것이 이 카드의 핵심이라 게임을 끝내지 않는다.
            EnterGhost(true);
            Debug.Log($"[LAST_SHIFT_CREW] crew={name} event=DEATH cause=suit-oxygen-depleted");
        }

        /// <summary>
        /// 기항에서 우주복을 채운다 — <b>구간 중에는 절대로 안 돈다.</b>
        ///
        /// <b>왜 필요한가.</b> 예비 산소는 원래 "보충 지점 0개, 항해 1회분" 예산이었는데,
        /// 선외 파밍(<c>outboard-outpost-and-map-final-v1.md</c> §4.1)이 붙으면서 그 예산이
        /// 기항마다 왕복으로 나간다. 채울 데가 없으면 첫 EVA 한 번으로 남은 항해의 산소가
        /// 마르고, 그건 조항 <c>O-7</c>("죽지 않는다")이 지키려던 것보다 훨씬 무거운 대가다.
        ///
        /// <b>왜 기항에서만인가.</b> <c>RG-1(4-b)</c>(산소 탈출 예산)는 구간 안의 판정이고,
        /// 기항은 판 밖이라 <c>300</c>초 시계가 안 돈다(§4.1-4). 여기서만 채우면 구간 안
        /// 예산은 한 칸도 안 움직인다 — <b>새 판정도, 새 상수도 안 생긴다.</b>
        ///
        /// <b>수치는 <c>game-balance</c> 소관이다.</b> 이 함수가 정하는 것은 축 하나 —
        /// 채우는 데 걸리는 시간이 왕복 한 번보다 짧아야 기항이 대기 화면이 안 된다.
        /// </summary>
        public void RefillAtPort(float deltaTime)
        {
            if (IsDead || deltaTime <= 0f) return;
            if (SuitOxygen >= LastShiftRecoveryTuning.SuitOxygenInitial) return;

            SuitOxygen = Mathf.Min(LastShiftRecoveryTuning.SuitOxygenInitial,
                SuitOxygen + LastShiftRecoveryTuning.SuitOxygenRefillPerSecond * deltaTime);
        }

        /// <summary>
        /// 조항 <c>O-7</c> 자동 복귀가 부르는 경계 — 선외에서 마른 우주복을 즉시 채운다.
        /// <b>사망 판정을 건너뛰는 것이 아니라 그 앞에서 끊는 것이다</b>:
        /// <see cref="LastShiftAirlock.EvaReturnReserve"/> 한 칸이 남아 있을 때 걸리므로
        /// <see cref="Tick"/> 의 사망 갈래에는 아직 안 들어갔다.
        /// </summary>
        public void RefillForRescue()
        {
            if (IsDead) return;
            SuitOxygen = LastShiftRecoveryTuning.SuitOxygenInitial;
            IsDraining = false;
            // 회수가 난 시각. UI 가 이 한 값에서 펄스 창을 계산하므로 회수 쪽에 상태를
            // 따로 안 들고, 창이 지나면 저절로 꺼진다.
            autoReturnAt = Time.unscaledTime;
            StopBreathAudio();
        }

        /// <summary>
        /// 테스트가 "1명만 사망한 상태" 를 조립하기 위한 경계. 실제 경로로는 같은 선체 압력을
        /// 공유하는 두 승무원 중 하나만 죽이기 어렵다(구역을 나눠야 하고 그건 CT-06 범위다).
        /// </summary>
        public void KillForProbe()
        {
            SuitOxygen = 0f;
            IsDraining = false;
            IsDead = true;
            StopBreathAudio();
            EnterGhost(true);
        }

        /// <summary>프리셋 리셋. 예비 산소는 항해 단위 예산이므로 리셋에서만 1.00 으로 되돌아온다.</summary>
        public void ResetCrewOxygen()
        {
            SuitOxygen = LastShiftRecoveryTuning.SuitOxygenInitial;
            IsDead = false;
            IsDraining = false;
            wasDraining = false;
            StopBreathAudio();
            EnterGhost(false);
        }

        /// <summary>클라이언트가 서버 값을 그대로 받는다. 소모 계산은 서버만 한다.</summary>
        public void ApplyReplicated(float suitOxygen, bool isDead, bool isDraining)
        {
            SuitOxygen = suitOxygen;
            IsDraining = isDraining;
            if (IsDead != isDead)
            {
                IsDead = isDead;
                EnterGhost(isDead);
            }
            if (isDead) StopBreathAudio();
            else UpdateBreathAudio();
        }

        /// <summary>
        /// 사망 상태를 유령 모드로 넘긴다(기획 §4.4 N11 구현물 1).
        ///
        /// <b>서버·클라이언트·솔로 세 경로가 모두 여기로 모인다.</b> 원격 승무원의 인스턴스도
        /// 이 함수를 타야 하는 이유는 콜라이더 때문이다 — 원격 사본의 CharacterController 가
        /// 살아 있으면, 산 사람이 남의 화면에서 유령에게 막혀 통로가 시신으로 봉쇄된다.
        /// </summary>
        private void EnterGhost(bool ghost)
        {
            if (playerController == null) playerController = GetComponent<LastShiftPlayerController>();
            if (playerController != null) playerController.SetGhost(ghost);
        }

        /// <summary>
        /// 헬멧 내부 호흡음. 소모가 도는 동안에만 들리고, 0.25 아래에서 증폭된다.
        /// 막대를 못 보고 있는 순간에도 "내 산소가 줄고 있다" 가 귀로 먼저 오게 하려는 채널이다.
        /// </summary>
        private void UpdateBreathAudio()
        {
            if (!IsDraining)
            {
                StopBreathAudio();
                return;
            }

            EnsureBreathAudio();
            if (breathAudio == null) return;
            breathAudio.volume = IsCritical ? 0.75f : 0.35f;
            breathAudio.pitch = IsCritical ? 1.35f : 1f;
            if (!wasDraining || !breathAudio.isPlaying)
            {
                breathAudio.Play();
                wasDraining = true;
            }
        }

        private void StopBreathAudio()
        {
            wasDraining = false;
            if (breathAudio != null && breathAudio.isPlaying) breathAudio.Stop();
        }

        private void EnsureBreathAudio()
        {
            if (breathAudio != null) return;
            // 배치 테스트나 씬 빌드 중에는 오디오를 만들지 않는다. 재생 중이 아닌 상황에서
            // AudioClip 을 생성하면 EditMode 에서 자산이 누수된 것처럼 보인다.
            if (!Application.isPlaying) return;
            breathAudio = gameObject.AddComponent<AudioSource>();
            breathAudio.playOnAwake = false;
            breathAudio.loop = true;
            // A2. 다른 승무원의 호흡이 거리에 따라 들려야 통로에서 "근처에 누가 있다" 가 읽힌다.
            // 자기 호흡음은 2D 채널로 남는데, 음원이 자기 루트에 붙어 있고 MinDistance 가
            // 눈높이를 덮으므로 거리 감쇠가 안 걸린다 — 별도 분기가 필요하지 않다.
            LastShiftZoneAudio.ConfigureLocal(breathAudio, LastShiftZoneAudio.BreathMaxDistance);
            breathAudio.clip = LastShiftProceduralAudio.CreateBreathLoop();
        }
    }
}
