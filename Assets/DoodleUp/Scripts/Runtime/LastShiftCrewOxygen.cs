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

        /// <summary>적색 점멸 위상. 사이렌 칸과 예비 막대가 같은 박자로 뛰어야 같은 사건으로 읽힌다.</summary>
        public static float BlinkPhase => 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);

        /// <summary>
        /// 예비 산소 막대 한 줄. 서버는 <see cref="LastShiftSandboxController"/> 가 전원 분을 그리지만
        /// 클라이언트에서는 sandbox 가 꺼져 있어(<c>enabled = IsServer</c>) 아무도 그리지 않는다.
        /// 그래서 <see cref="LastShiftNetworkPlayer"/> 가 같은 함수로 자기 막대를 그린다 —
        /// 두 경로가 다른 코드로 그리면 서버와 클라이언트의 막대가 서로 다르게 보인다.
        /// </summary>
        public static void DrawGauge(LastShiftCrewOxygen crew, string slotLabel, int row, ref GUIStyle style)
        {
            if (crew == null || !crew.ShowsSuitGauge) return;
            var y = Screen.height - 96f - row * 34f;
            GUI.Box(new Rect(24f, y, 320f, 28f), GUIContent.none);
            var fill = Mathf.Clamp01(crew.SuitOxygen);
            var color = crew.IsDead
                ? new Color(0.45f, 0.45f, 0.45f)
                : crew.IsCritical
                    ? Color.Lerp(new Color(0.35f, 0.05f, 0.05f), new Color(1f, 0.2f, 0.15f), BlinkPhase)
                    : new Color(0.3f, 0.75f, 1f);
            var previous = GUI.color;
            GUI.color = color;
            GUI.Box(new Rect(28f, y + 4f, 312f * fill, 20f), GUIContent.none);
            GUI.color = previous;

            style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            style.normal.textColor = Color.white;
            var label = crew.IsDead
                ? $"{slotLabel} 예비 산소 고갈 — 사망"
                : $"{slotLabel} 예비 산소 {crew.SuitOxygen:P0}" + (crew.IsDraining ? "  (소모 중)" : "  (소모 정지)");
            GUI.Label(new Rect(34f, y + 5f, 306f, 20f), label, style);
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
            UpdateBreathAudio();
            if (SuitOxygen > LastShiftRecoveryTuning.AsphyxiationSuitOxygenThreshold) return;

            IsDead = true;
            IsDraining = false;
            StopBreathAudio();
            // 사망한 승무원은 더 이상 조작할 수 없고 도킹 판정에도 잡히지 않는다.
            // 남은 승무원으로 항해가 계속되는 것이 이 카드의 핵심이라 게임을 끝내지 않는다.
            if (playerController != null) playerController.enabled = false;
            Debug.Log($"[LAST_SHIFT_CREW] crew={name} event=DEATH cause=suit-oxygen-depleted");
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
            if (playerController != null) playerController.enabled = false;
        }

        /// <summary>프리셋 리셋. 예비 산소는 항해 단위 예산이므로 리셋에서만 1.00 으로 되돌아온다.</summary>
        public void ResetCrewOxygen()
        {
            SuitOxygen = LastShiftRecoveryTuning.SuitOxygenInitial;
            IsDead = false;
            IsDraining = false;
            wasDraining = false;
            StopBreathAudio();
            if (playerController != null) playerController.enabled = true;
        }

        /// <summary>클라이언트가 서버 값을 그대로 받는다. 소모 계산은 서버만 한다.</summary>
        public void ApplyReplicated(float suitOxygen, bool isDead, bool isDraining)
        {
            SuitOxygen = suitOxygen;
            IsDraining = isDraining;
            if (IsDead != isDead)
            {
                IsDead = isDead;
                if (playerController != null) playerController.enabled = !isDead;
            }
            if (isDead) StopBreathAudio();
            else UpdateBreathAudio();
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
