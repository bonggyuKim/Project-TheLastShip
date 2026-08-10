using Unity.Netcode;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 방 코드 로비. 게임을 켜면 처음 만나는 화면이고, 여기서 <b>호스트로 방을 열지</b>
    /// <b>코드를 받아 적어 남의 방에 들어갈지</b>를 고른다.
    ///
    /// HUD 와 같은 IMGUI 로 그린다. uGUI 캔버스를 새로 세우면 프리팹·씬 직렬화가 따라붙는데,
    /// 이 화면은 버튼 둘과 입력창 하나가 전부라 그 비용을 낼 이유가 없다. 화면 디자인이
    /// 정식으로 들어올 때 이 컴포넌트만 걷어내면 되도록, 로비 로직은 그리기와 분리해
    /// <see cref="LastShiftNetworkSession.OpenRoom"/> · <see cref="LastShiftNetworkSession.JoinRoom"/>
    /// 두 진입점만 쓴다.
    /// </summary>
    [RequireComponent(typeof(LastShiftNetworkSession))]
    public sealed class LastShiftRoomLobby : MonoBehaviour
    {
        private enum Phase
        {
            Hidden,
            Menu,
            Searching,
            Connecting,
            Hosting,
            Failed,
        }

        /// <summary>코드를 뿌리고 응답을 기다리는 시간. 이보다 오래 걸리면 그 방은 없는 것으로 본다.</summary>
        public const int LookupTimeoutMilliseconds = 4000;

        /// <summary>주소를 찾은 뒤 실제 접속이 성립하기를 기다리는 시간.</summary>
        private const float ConnectTimeoutSeconds = 10f;

        private const float PanelWidth = 460f;
        private const float PanelHeight = 250f;
        private const string CodeFieldName = "LastShiftRoomCodeField";

        private LastShiftNetworkSession session;
        private Phase phase = Phase.Hidden;
        private string typedCode = string.Empty;
        private string status = string.Empty;
        private LastShiftRoomLookup lookup;
        private float connectDeadline;
        private bool focusRequested;

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle codeStyle;

        private void Awake()
        {
            session = GetComponent<LastShiftNetworkSession>();
        }

        /// <summary>
        /// 로비를 띄운다. 세션이 인자 없이 시작할 때 부른다.
        /// </summary>
        public void Open()
        {
            phase = Phase.Menu;
            status = string.Empty;
            focusRequested = true;
            ReleaseCursor();
        }

        private void OnDestroy()
        {
            UnsubscribeFromClientEvents();
        }

        private void Update()
        {
            switch (phase)
            {
                case Phase.Searching:
                    PollLookup();
                    break;
                case Phase.Connecting:
                    PollConnection();
                    break;
                case Phase.Menu:
                    // 테스트나 자동화가 로비를 거치지 않고 직접 host 를 띄우는 경로가 있다.
                    // 그때 로비가 화면에 남아 있으면 조작을 가로채므로 스스로 물러난다.
                    if (session.NetworkManager != null && session.NetworkManager.IsListening) phase = Phase.Hidden;
                    break;
            }
        }

        private void HostRoom()
        {
            var code = LastShiftRoomCode.Generate();
            if (!session.OpenRoom(code))
            {
                phase = Phase.Failed;
                status = $"방을 열지 못했습니다. 포트 {session.Port} 를 이미 쓰고 있는지 확인하세요.";
                return;
            }

            phase = Phase.Hosting;
            status = session.RoomDiscoverable
                ? string.Empty
                : "이 PC 에서 이미 다른 방이 열려 있어 코드 검색이 꺼졌습니다. IP 로는 들어올 수 있습니다.";
        }

        private void JoinRoom()
        {
            var code = LastShiftRoomCode.Normalize(typedCode);
            if (!LastShiftRoomCode.IsValid(code))
            {
                status = $"코드는 {LastShiftRoomCode.Length}자리입니다. 받아 적은 코드를 다시 확인하세요.";
                return;
            }

            typedCode = code;
            lookup = new LastShiftRoomLookup(code, LookupTimeoutMilliseconds);
            phase = Phase.Searching;
            status = $"{code} 방을 찾는 중…";
        }

        private void PollLookup()
        {
            if (lookup == null || !lookup.Poll(out var address, out var port)) return;
            lookup = null;

            if (string.IsNullOrEmpty(address))
            {
                phase = Phase.Failed;
                status = $"코드 {typedCode} 인 방을 찾지 못했습니다. 호스트가 방을 열었는지, 같은 네트워크인지 확인하세요.";
                return;
            }

            if (!session.JoinRoom(address, port))
            {
                phase = Phase.Failed;
                status = $"{address}:{port} 접속을 시작하지 못했습니다.";
                return;
            }

            SubscribeToClientEvents();
            phase = Phase.Connecting;
            connectDeadline = Time.realtimeSinceStartup + ConnectTimeoutSeconds;
            status = $"{address}:{port} 에 접속 중…";
        }

        private void PollConnection()
        {
            if (Time.realtimeSinceStartup < connectDeadline) return;
            UnsubscribeFromClientEvents();
            session.StopSession();
            phase = Phase.Failed;
            status = "접속이 응답하지 않았습니다. 호스트 쪽 방화벽이 UDP 를 막고 있는지 확인하세요.";
        }

        private void SubscribeToClientEvents()
        {
            var manager = session.NetworkManager;
            if (manager == null) return;
            manager.OnClientConnectedCallback -= OnClientConnected;
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback -= OnClientDisconnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void UnsubscribeFromClientEvents()
        {
            var manager = session != null ? session.NetworkManager : null;
            if (manager == null) return;
            manager.OnClientConnectedCallback -= OnClientConnected;
            manager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void OnClientConnected(ulong clientId)
        {
            var manager = session.NetworkManager;
            if (manager == null || clientId != manager.LocalClientId) return;
            UnsubscribeFromClientEvents();
            phase = Phase.Hidden;
            status = string.Empty;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var manager = session.NetworkManager;
            if (manager == null || clientId != manager.LocalClientId) return;
            if (phase != Phase.Connecting) return;
            UnsubscribeFromClientEvents();
            // 승인 거절(정원 초과)은 여기로 온다. 서버가 남긴 사유가 있으면 그것이 가장 정확하다.
            var reason = manager.DisconnectReason;
            phase = Phase.Failed;
            status = string.IsNullOrEmpty(reason) ? "호스트가 접속을 거절했습니다." : reason;
            ReleaseCursor();
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (phase == Phase.Hidden) return;
            EnsureStyles();

            if (phase == Phase.Hosting)
            {
                DrawHostCodeStrip();
                return;
            }

            var panel = new Rect(
                (Screen.width - PanelWidth) * 0.5f,
                (Screen.height - PanelHeight) * 0.5f,
                PanelWidth,
                PanelHeight);
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 20f, panel.width - 48f, panel.height - 40f));
            GUILayout.Label("LAST SHIFT", titleStyle);

            switch (phase)
            {
                case Phase.Menu:
                    DrawMenu();
                    break;
                case Phase.Searching:
                case Phase.Connecting:
                    DrawProgress();
                    break;
                case Phase.Failed:
                    DrawFailure();
                    break;
            }

            GUILayout.EndArea();
        }

        private void DrawMenu()
        {
            GUILayout.Space(6f);
            if (GUILayout.Button("방 열기 (호스트)", GUILayout.Height(38f))) HostRoom();

            GUILayout.Space(14f);
            GUILayout.Label("호스트에게 받은 방 코드", bodyStyle);
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName(CodeFieldName);
            var typed = GUILayout.TextField(typedCode, LastShiftRoomCode.Length + 4, codeStyle, GUILayout.Height(34f));
            if (typed != typedCode) typedCode = LastShiftRoomCode.Normalize(typed);
            var join = GUILayout.Button("입장", GUILayout.Width(96f), GUILayout.Height(34f));
            GUILayout.EndHorizontal();

            if (focusRequested)
            {
                GUI.FocusControl(CodeFieldName);
                focusRequested = false;
            }

            var submitted = Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                && GUI.GetNameOfFocusedControl() == CodeFieldName;
            if (join || submitted) JoinRoom();

            if (!string.IsNullOrEmpty(status))
            {
                GUILayout.Space(8f);
                GUILayout.Label(status, bodyStyle);
            }
        }

        private void DrawProgress()
        {
            GUILayout.Space(12f);
            GUILayout.Label(status, bodyStyle);
            GUILayout.Space(12f);
            if (!GUILayout.Button("취소", GUILayout.Width(120f), GUILayout.Height(32f))) return;
            lookup = null;
            UnsubscribeFromClientEvents();
            session.StopSession();
            Open();
        }

        private void DrawFailure()
        {
            GUILayout.Space(12f);
            GUILayout.Label(status, bodyStyle);
            GUILayout.Space(12f);
            if (GUILayout.Button("돌아가기", GUILayout.Width(120f), GUILayout.Height(32f))) Open();
        }

        /// <summary>
        /// 호스트는 판이 도는 동안에도 코드를 불러 줘야 한다. 늦게 합류하는 친구가 늘 있다.
        /// HUD 가 좌상단을 쓰므로 우상단에 붙인다.
        /// </summary>
        private void DrawHostCodeStrip()
        {
            var width = string.IsNullOrEmpty(status) ? 240f : 420f;
            var height = string.IsNullOrEmpty(status) ? 74f : 116f;
            var strip = new Rect(Screen.width - width - 16f, 16f, width, height);
            GUI.Box(strip, GUIContent.none);
            GUI.Label(new Rect(strip.x + 16f, strip.y + 10f, strip.width - 32f, 22f), "방 코드", bodyStyle);
            GUI.Label(new Rect(strip.x + 16f, strip.y + 32f, strip.width - 32f, 34f), session.RoomCode, codeStyle);
            if (!string.IsNullOrEmpty(status))
                GUI.Label(new Rect(strip.x + 16f, strip.y + 70f, strip.width - 32f, 40f), status, bodyStyle);
        }

        private void EnsureStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = new Color(0.88f, 0.94f, 1f) },
            };
            codeStyle ??= new GUIStyle(GUI.skin.textField)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.55f, 0.9f, 1f) },
            };
        }
    }
}
