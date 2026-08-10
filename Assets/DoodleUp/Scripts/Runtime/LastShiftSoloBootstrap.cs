using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 네트워크 없이 승무원 하나를 세운다. <b>새 계통이 아니라 배선 하나다</b> — 이동·시점·
    /// 애니메이션·상호작용은 전부 <see cref="LastShiftPlayerController"/> 와
    /// <see cref="LastShiftPlayerAnimator"/> 가 이미 갖고 있고, 없던 것은 "host 없이 그것을
    /// 씬에 올리는 자리" 뿐이었다.
    ///
    /// <b>PlayMode 검사가 이미 같은 일을 하고 있다.</b> <c>LastShiftCrewOxygenPlayModeTests</c> 가
    /// <see cref="LastShiftNetworkSession.AutoStartHost"/> 를 끄고 프리팹을 직접 세워 돌린다 —
    /// 이 컴포넌트는 그 경로를 에디터 Play 에서도 쓰게 꺼내 놓은 것이다. 그래서 솔로용 플레이어
    /// 프리팹을 따로 두지 않는다. 두 벌이 되면 조작이 한쪽에서만 고쳐진다.
    ///
    /// 네트워크가 이미 돌고 있으면 아무것도 안 한다 — 같은 씬을 host 로 열었을 때 승무원이
    /// 둘 서지 않게 하려는 것이고, 그 판정이 <see cref="ShouldSpawn"/> 이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftSoloBootstrap : MonoBehaviour
    {
        [SerializeField] private LastShiftNetworkSession session;
        [SerializeField] private LastShiftSandboxController sandbox;

        /// <summary>세운 승무원. 안 세웠으면 <c>null</c> 이다.</summary>
        public LastShiftPlayerController Crew { get; private set; }

        public void Configure(LastShiftNetworkSession soloSession, LastShiftSandboxController soloSandbox)
        {
            session = soloSession;
            sandbox = soloSandbox;
        }

        /// <summary>
        /// 지금 승무원을 세워야 하는가. <b>NGO 가 이미 듣고 있으면 세우지 않는다</b> —
        /// host/client 로 열린 판에서는 세션이 슬롯별로 스폰하므로, 여기서 하나 더 세우면
        /// 슬롯 배정과 <see cref="LastShiftSandboxController.Players"/> 가 어긋난다.
        /// </summary>
        public static bool ShouldSpawn()
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            return manager == null || !manager.IsListening;
        }

        private void Awake()
        {
            // 씬이 열리자마자 host 가 뜨는 것을 막는다. Start 에서 끄면 이미 늦다.
            LastShiftNetworkSession.AutoStartHost = false;
        }

        private void Start()
        {
            if (!ShouldSpawn()) return;
            if (session == null) session = FindAnyObjectByType<LastShiftNetworkSession>();
            if (sandbox == null) sandbox = FindAnyObjectByType<LastShiftSandboxController>();
            if (session == null || session.PlayerPrefab == null)
            {
                Debug.LogError("[LAST_SHIFT_SOLO] 세션이나 플레이어 프리팹이 없다 — 씬을 다시 구워야 한다.");
                return;
            }

            var crew = Instantiate(session.PlayerPrefab.gameObject);
            crew.name = "SoloCrew";
            Crew = crew.GetComponent<LastShiftPlayerController>();

            // CharacterController 가 붙어 있으면 transform 대입이 씹힌다 — 컨트롤러를 잠깐
            // 끄고 옮긴다. 안 그러면 스폰 지점이 아니라 프리팹 원점에서 시작한다.
            var body = crew.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            crew.transform.position = LastShiftShipDimensions.SpawnPoint;
            if (body != null) body.enabled = true;

            // 1인칭 표현. 내 카메라에 내 몸이 보이면 안 된다 — 규칙은
            // LastShiftNetworkPlayer 가 갖고 있고 여기서는 소유자 자격으로 부르기만 한다.
            var presentation = crew.GetComponent<LastShiftNetworkPlayer>();
            if (presentation != null) presentation.ApplySoloPresentation();
            else Debug.LogWarning("[LAST_SHIFT_SOLO] LastShiftNetworkPlayer 가 없다 — 자기 몸이 화면에 남는다.");

            // 시뮬레이션이 승무원을 알아야 산소·진공·판정이 이 사람에게 걸린다.
            if (sandbox != null)
            {
                var items = FindObjectsByType<LastShiftGrabbable>(FindObjectsSortMode.None);
                sandbox.Configure(new[] { Crew }, items);
            }

            Debug.Log($"[LAST_SHIFT_SOLO] crew=1 spawn={LastShiftShipDimensions.SpawnPoint} " +
                      $"sandbox={(sandbox != null ? "wired" : "missing")} result=PASS");
        }
    }
}
