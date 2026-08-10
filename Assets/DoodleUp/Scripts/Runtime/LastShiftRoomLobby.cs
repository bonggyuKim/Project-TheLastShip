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

        private const float PanelWidth = 560f;
        private const float PanelHeight = 486f;
        private const string CodeFieldName = "LastShiftRoomCodeField";

        /// <summary>로비 뒤에 깔리는 색. 판 화면이 아니라 메뉴 화면임이 첫 프레임에 읽혀야 한다.</summary>
        private static readonly Color BackdropColor = new(0.04f, 0.05f, 0.07f);

        /// <summary>
        /// 지금 화면을 잡고 있는 로비. 게임 HUD 는 이것 하나만 보고 스스로 물러난다.
        ///
        /// 정적으로 둔 이유는 HUD 를 그리는 컴포넌트가 씬 곳곳(<c>LAST_SHIFT_RUNTIME</c> 의
        /// 샌드박스·도면, 그리고 스폰된 승무원)에 흩어져 있어 로비 참조를 각자 물리면
        /// 씬을 다시 구울 때마다 그 배선이 하나씩 빠지기 때문이다. 파괴된 로비는 유니티
        /// 널 규칙에 따라 스스로 <c>null</c> 이 되므로 정적 값이 늙어 붙지 않는다.
        /// </summary>
        private static LastShiftRoomLobby screenOwner;

        /// <summary>로비가 화면을 잡고 있는지. 참이면 게임 HUD 는 한 줄도 그리지 않는다.</summary>
        public static bool IsBlockingGameplay => screenOwner != null;

        private LastShiftNetworkSession session;
        private Phase phase = Phase.Hidden;
        private Camera backdrop;
        private bool backdropRetiring;
        private string typedCode = string.Empty;
        private string status = string.Empty;
        private LastShiftRoomLookup lookup;
        private float connectDeadline;
        private bool focusRequested;
        private bool joinExpanded;

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle codeStyle;
        private GUIStyle primaryButtonStyle;
        private GUIStyle secondaryButtonStyle;
        private GUIStyle exitButtonStyle;

        private void Awake()
        {
            session = GetComponent<LastShiftNetworkSession>();
        }

        /// <summary>
        /// 로비를 띄운다. 세션이 인자 없이 시작할 때 부른다.
        /// </summary>
        public void Open()
        {
            SetPhase(Phase.Menu);
            status = string.Empty;
            focusRequested = false;
            joinExpanded = false;
            ReleaseCursor();
        }

        private void OnDestroy()
        {
            UnsubscribeFromClientEvents();
            if (screenOwner == this) screenOwner = null;
            DestroyBackdrop();
        }

        /// <summary>
        /// 단계 전환은 전부 여기를 지난다. 화면 주인과 배경 카메라가 단계와 어긋나지 않게
        /// 한 곳에서 같이 움직인다 — 필드에 직접 대입하면 로비는 사라졌는데 HUD 가 계속
        /// 숨어 있거나, 로비는 떠 있는데 카메라가 없는 상태가 경로마다 따로 생긴다.
        /// </summary>
        private void SetPhase(Phase next)
        {
            phase = next;

            // Hosting 은 판이 이미 도는 중이다 — 우상단 코드 띠만 남고 게임 화면이 정본이다.
            var blocking = next != Phase.Hidden && next != Phase.Hosting;
            if (blocking)
            {
                screenOwner = this;
                EnsureBackdrop();
                return;
            }

            if (screenOwner == this) screenOwner = null;
            // 여기서 바로 지우면 승무원 카메라가 아직 안 붙은 프레임에 "렌더링할 카메라가
            // 없다" 가 다시 뜬다. 판 카메라가 실제로 생긴 것을 보고 물러난다.
            backdropRetiring = backdrop != null;
        }

        private void Update()
        {
            if (backdropRetiring) RetireBackdropWhenGameCameraExists();

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
                    if (session.NetworkManager != null && session.NetworkManager.IsListening) SetPhase(Phase.Hidden);
                    break;
            }
        }

        /// <summary>
        /// 로비 배경 카메라. 카메라는 승무원 프리팹에만 있어서, 아직 아무도 스폰되지 않은
        /// 로비 단계에는 씬에 카메라가 하나도 없다 — 그래서 "No cameras rendering" 경고가
        /// 떴다. 판을 비추라는 것이 아니라 <b>메뉴 뒤를 덮으라는</b> 카메라이므로
        /// <c>cullingMask</c> 를 0 으로 두고 단색만 칠한다. 3D 화면은 방에 들어간 뒤에 나온다.
        /// </summary>
        private void EnsureBackdrop()
        {
            backdropRetiring = false;
            if (backdrop != null) return;

            // hideFlags 는 건드리지 않는다. DontSave 를 걸면 씬을 갈아 끼울 때 이 오브젝트만
            // 살아남아, 다음 씬에 검은 카메라가 하나 얹힌 채로 시작한다.
            var host = new GameObject("LAST_SHIFT_LOBBY_BACKDROP");
            backdrop = host.AddComponent<Camera>();
            backdrop.clearFlags = CameraClearFlags.SolidColor;
            backdrop.backgroundColor = BackdropColor;
            backdrop.cullingMask = 0;
            backdrop.depth = -100f;
            backdrop.useOcclusionCulling = false;
            backdrop.allowHDR = false;
            backdrop.allowMSAA = false;
        }

        private void RetireBackdropWhenGameCameraExists()
        {
            if (backdrop == null)
            {
                backdropRetiring = false;
                return;
            }

            var cameras = Camera.allCameras;
            for (var index = 0; index < cameras.Length; index++)
            {
                if (cameras[index] == backdrop) continue;
                DestroyBackdrop();
                return;
            }
        }

        private void DestroyBackdrop()
        {
            backdropRetiring = false;
            if (backdrop == null) return;
            Destroy(backdrop.gameObject);
            backdrop = null;
        }

        private void HostRoom()
        {
            var code = LastShiftRoomCode.Generate();
            if (!session.OpenRoom(code))
            {
                SetPhase(Phase.Failed);
                status = $"방을 열지 못했습니다. 포트 {session.Port} 를 이미 쓰고 있는지 확인하세요.";
                return;
            }

            SetPhase(Phase.Hosting);
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
            SetPhase(Phase.Searching);
            status = $"{code} 방을 찾는 중…";
        }

        private void PollLookup()
        {
            if (lookup == null || !lookup.Poll(out var address, out var port)) return;
            lookup = null;

            if (string.IsNullOrEmpty(address))
            {
                SetPhase(Phase.Failed);
                status = $"코드 {typedCode} 인 방을 찾지 못했습니다. 호스트가 방을 열었는지, 같은 네트워크인지 확인하세요.";
                return;
            }

            if (!session.JoinRoom(address, port))
            {
                SetPhase(Phase.Failed);
                status = $"{address}:{port} 접속을 시작하지 못했습니다.";
                return;
            }

            SubscribeToClientEvents();
            SetPhase(Phase.Connecting);
            connectDeadline = Time.realtimeSinceStartup + ConnectTimeoutSeconds;
            status = $"{address}:{port} 에 접속 중…";
        }

        private void PollConnection()
        {
            if (Time.realtimeSinceStartup < connectDeadline) return;
            UnsubscribeFromClientEvents();
            session.StopSession();
            SetPhase(Phase.Failed);
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
            SetPhase(Phase.Hidden);
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
            SetPhase(Phase.Failed);
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
            // 로비 배경만 9-slice 패널로 바뀌었다(아트 키트 v1 §"화면별 적용"). 방 코드
            // 입력칸과 버튼은 IMGUI 로 남는다 — UGUI 로 옮기려면 EventSystem 과 InputField 가
            // 들어와야 하고, 그건 접속 경로 전체를 다시 검증해야 하는 별개의 일이다.
            LastShiftUiLayer.Instance?.Panel("lobby", panel, 0.96f);
            GUILayout.BeginArea(new Rect(panel.x + 32f, panel.y + 26f, panel.width - 64f, panel.height - 52f));
            GUILayout.Label("THE LAST SHIP", titleStyle);

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
            GUILayout.Space(24f);
            if (DrawMenuButton("방 만들기", primaryButtonStyle, GUILayout.Height(52f))) HostRoom();

            GUILayout.Space(12f);
            if (DrawMenuButton("코드로 참가", secondaryButtonStyle, GUILayout.Height(48f)))
            {
                joinExpanded = true;
                focusRequested = true;
            }

            var join = false;
            if (joinExpanded)
            {
                GUILayout.Space(14f);
                GUILayout.Label("호스트에게 받은 방 코드", bodyStyle);
                GUILayout.BeginHorizontal();
                GUI.SetNextControlName(CodeFieldName);
                var typed = GUILayout.TextField(typedCode, LastShiftRoomCode.Length + 4, codeStyle, GUILayout.Height(48f));
                if (typed != typedCode) typedCode = LastShiftRoomCode.Normalize(typed);
                join = DrawMenuButton("입장", primaryButtonStyle, GUILayout.Width(112f), GUILayout.Height(48f));
                GUILayout.EndHorizontal();
            }

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

            GUILayout.FlexibleSpace();
            if (DrawMenuButton("종료", exitButtonStyle, GUILayout.Height(48f))) ExitApplication();
        }

        private static bool DrawMenuButton(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            var previousBackground = GUI.backgroundColor;
            var previousColor = GUI.color;
            GUI.backgroundColor = Color.white;
            GUI.color = Color.white;
            var clicked = GUILayout.Button(text, style, options);
            GUI.backgroundColor = previousBackground;
            GUI.color = previousColor;
            return clicked;
        }

        private static void ExitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
            var layer = LastShiftUiLayer.Instance;
            if (layer == null) return;

            layer.Panel("lobbyCode", strip, 0.94f);
            layer.Label("lobbyCodeCaption", new Rect(strip.x + 16f, strip.y + 10f, strip.width - 32f, 22f),
                "방 코드", 14, LastShiftUiTheme.BodyText);

            // 코드는 입력칸이 아니라 읽는 값이라, IMGUI 시절 <c>textField</c> 스타일이 주던
            // 상자는 뒤 9-slice 판이 이미 대신한다. 상자를 두 겹 그리면 판이 두 장으로 보인다.
            layer.Label("lobbyCodeValue", new Rect(strip.x + 16f, strip.y + 32f, strip.width - 32f, 34f),
                session.RoomCode, 26, Color.white, fontStyle: FontStyle.Bold);
            if (!string.IsNullOrEmpty(status))
                layer.Label("lobbyCodeStatus", new Rect(strip.x + 16f, strip.y + 70f, strip.width - 32f, 40f),
                    status, 14, LastShiftUiTheme.BodyText, wrap: true);
        }

        private void EnsureStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = LastShiftUiTheme.Ivory },
            };
            bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = LastShiftUiTheme.BodyText },
            };
            codeStyle ??= new GUIStyle(GUI.skin.textField)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = LastShiftUiTheme.Nominal },
            };
            primaryButtonStyle ??= CreateButtonStyle(20, LastShiftUiTheme.Ivory, LastShiftUiTheme.PanelNavy,
                LastShiftUiTheme.Nominal, LastShiftUiTheme.Fault, true);
            secondaryButtonStyle ??= CreateButtonStyle(17, LastShiftUiTheme.PanelNavy, LastShiftUiTheme.Ivory,
                LastShiftUiTheme.Nominal, LastShiftUiTheme.Ivory, false);
            exitButtonStyle ??= CreateButtonStyle(16, LastShiftUiTheme.PanelNavy, LastShiftUiTheme.BodyText,
                LastShiftUiTheme.Fault, LastShiftUiTheme.Fault, false);
        }

        private static GUIStyle CreateButtonStyle(
            int fontSize, Color background, Color foreground, Color hover, Color border, bool tintHoverFill)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(12, 12, 8, 8),
                border = new RectOffset(1, 1, 1, 1),
            };
            style.normal.textColor = foreground;
            style.hover.textColor = hover;
            style.active.textColor = hover;
            style.focused.textColor = hover;
            style.normal.background = BorderedTexture(background, border);
            style.hover.background = BorderedTexture(tintHoverFill ? Color.Lerp(background, hover, 0.16f) : background, hover);
            style.active.background = BorderedTexture(tintHoverFill ? Color.Lerp(background, hover, 0.28f) : background, hover);
            return style;
        }

        private static Texture2D BorderedTexture(Color fill, Color border)
        {
            var texture = new Texture2D(3, 3, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
                texture.SetPixel(x, y, x == 1 && y == 1 ? fill : border);
            texture.Apply();
            return texture;
        }
    }
}
