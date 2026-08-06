using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 상태 단서의 종류. 지금은 정적 판이고 상태 연동은 `game-ta` 소관이지만, <b>자리</b>는
    /// 여기서 확정한다 — §19.4 가 제한하는 것이 자리이기 때문이다.
    /// </summary>
    public enum LastShiftStateCue
    {
        /// <summary>냉각실. 열이 빠진 자리에 앉는 서리.</summary>
        Frost = 0,

        /// <summary>전력실. 아크가 지나간 자리에 남는 그을음.</summary>
        Scorch = 1
    }

    /// <summary>
    /// 상태 단서 하나의 자리. <b>x 는 방 중심 상대</b>이고 y·z 는 선체 절대다 —
    /// 전장이 바뀌면 방은 움직이지만 선체 폭(<c>6m</c>)과 안전대(<c>z ≤ +1.40</c>)는 안 움직인다.
    /// 셋을 다 상대로 적으면 폭이 안 바뀌었는데도 단서가 z 로 흘러 안전대를 벗어난다.
    /// </summary>
    public readonly struct LastShiftStateCueSpec
    {
        public LastShiftStateCueSpec(string name, LastShiftZone room, LastShiftStateCue kind,
            float offsetX, float centerY, float centerZ, Vector3 size)
        {
            Name = name;
            Room = room;
            Kind = kind;
            OffsetX = offsetX;
            CenterY = centerY;
            CenterZ = centerZ;
            Size = size;
        }

        public string Name { get; }
        public LastShiftZone Room { get; }
        public LastShiftStateCue Kind { get; }

        /// <summary>방 중심에서의 x 오프셋.</summary>
        public float OffsetX { get; }

        public float CenterY { get; }
        public float CenterZ { get; }
        public Vector3 Size { get; }

        public Vector3 Center => new(
            LastShiftShipDimensions.RoomCenterX(Room) + OffsetX, CenterY, CenterZ);

        /// <summary>이 단서가 차지하는 가장 큰 z. 안전대 판정은 중심이 아니라 이 값으로 한다.</summary>
        public float MaxZ => CenterZ + Size.z * 0.5f;

        public float MinX => Center.x - Size.x * 0.5f;
        public float MaxX => Center.x + Size.x * 0.5f;
    }

    /// <summary>
    /// 그레이박스 비주얼 드레싱의 데이터 정본. 씬 빌더(Editor)는 여기 있는 값을 판으로
    /// 세우기만 하고, 색·좌표를 자기 안에 다시 적지 않는다 — 좌표 정본을 Runtime 에 두는
    /// 이유는 <see cref="LastShiftCompartments"/>·<see cref="LastShiftBypassDuct"/> 와 같다.
    /// 씬을 다시 굽기 전에 EditMode 테스트가 제약을 확인할 수 있어야 한다.
    ///
    /// <b>색 위계가 이 파일의 설계다.</b> 배에는 이미 압력 구역 색 넷(조종석·전력·냉각·산소)이
    /// 있고 그것이 1차 인지 앵커다. 부속 구획 열한 개에 벽 색을 따로 주면 색이 스물다섯
    /// 가지가 되어 구역 색이 그 안에 묻힌다. 그래서 <b>구획 벽은 공통 중성색으로 두고</b>
    /// 구획색은 바닥 띠와 라벨에만 쓴다 — 면적으로 위계를 만든다.
    /// </summary>
    public static class LastShiftDressing
    {
        /// <summary>
        /// 상태 단서를 놓아도 되는 z 상한. §19.7 의 `art`/`ta` 인계 데이터 그대로다 —
        /// 개구부`2`(전력실↔냉각실)의 노출 원뿔이 `z ∈ [+1.40, +3.00]` 이고, 그 안에 든
        /// 시각 단서는 게이지가 없어도 사실상 세 번째 게이지처럼 상태를 흘린다(§19.4).
        ///
        /// <b>정적 구조물에는 안 걸린다.</b> 제한 대상은 상태에 반응하는 단서다 — 열교환기나
        /// 배전반은 상태와 무관하게 늘 같은 모습이라 원뿔 안에 있어도 새는 정보가 없다.
        /// </summary>
        public const float StateCueSafeMaxZ = 1.40f;

        /// <summary>
        /// 구획색. 기능군으로 묶고 군 안에서만 갈랐다 — 열한 개를 전부 무관한 색으로 두면
        /// 색이 분류가 아니라 소음이 된다.
        ///
        ///   작업·화물  화물칸 · 격납고 · 정비창    황토~주황
        ///   관측·정보  관측실 · 서버통신실         청보라
        ///   생활       화장실 · 숙소 · 휴게실      따뜻한 중성
        ///   생명유지   수경재배 · 의무실           녹 · 백
        ///   비상       구명정                     적 (배 전체에서 여기 하나뿐이다)
        ///
        /// 구명정만 단독 적색인 것이 의도다. 배 어디에도 이 색이 없으므로 복도 끝에 적색이
        /// 보이면 그것이 "마지막 수단" 이라는 뜻이 되고, 언락 상태를 말하지 않으면서도
        /// 방의 역할이 색 하나로 읽힌다.
        /// </summary>
        public static Color TintOf(LastShiftCompartment compartment) => compartment switch
        {
            LastShiftCompartment.CargoBay => new Color(0.60f, 0.44f, 0.24f),
            LastShiftCompartment.Hangar => new Color(0.72f, 0.56f, 0.22f),
            LastShiftCompartment.Workshop => new Color(0.56f, 0.34f, 0.34f),
            LastShiftCompartment.Observatory => new Color(0.34f, 0.44f, 0.70f),
            LastShiftCompartment.ServerRoom => new Color(0.48f, 0.38f, 0.72f),
            LastShiftCompartment.Lavatory => new Color(0.46f, 0.58f, 0.62f),
            LastShiftCompartment.Quarters => new Color(0.62f, 0.50f, 0.42f),
            LastShiftCompartment.Lounge => new Color(0.74f, 0.60f, 0.34f),
            LastShiftCompartment.Hydroponics => new Color(0.36f, 0.66f, 0.36f),
            LastShiftCompartment.MedBay => new Color(0.78f, 0.82f, 0.84f),
            _ => new Color(0.78f, 0.26f, 0.22f)
        };

        /// <summary>
        /// 압력 구역 색. 씬 빌더가 이미 들고 있던 값을 여기로 옮겼다 — 구획색이 구역색과
        /// 충분히 떨어졌는지를 테스트가 확인하려면 스물다섯 색이 한 곳에 있어야 한다.
        ///
        /// <b>냉각실 색은 옮겼다.</b> 예전 값 <c>(0.26, 0.42, 0.50)</c> 은 조종석
        /// <c>(0.24, 0.38, 0.50)</c> 과 RGB 거리가 <c>0.045</c> 로, 1차 인지 앵커 넷 중 둘이
        /// 사실상 같은 색이었다. 두 방이 붙어 있지 않아 나란히 보이지는 않지만, 개구부 너머로
        /// 보이는 색이 어느 방인지 안 갈리면 구역색이 앵커 노릇을 못 한다. 파랑을 유지한 채
        /// 채도를 청록 쪽으로 밀어 조종석의 강청색과 갈랐다 — 냉각이라는 기능과도 맞는다.
        /// </summary>
        public static Color TintOf(LastShiftZone zone) => zone switch
        {
            LastShiftZone.Cockpit => new Color(0.24f, 0.38f, 0.50f),
            LastShiftZone.Power => new Color(0.42f, 0.38f, 0.28f),
            LastShiftZone.Cooling => new Color(0.20f, 0.50f, 0.56f),
            _ => new Color(0.26f, 0.48f, 0.36f)
        };

        /// <summary>
        /// 구획색 사이의 최소 거리(RGB 유클리드). 이 아래로 내려가면 두 방이 같은 조명 아래
        /// 같은 색으로 보인다 — 실측으로 뽑은 하한이 아니라, 새 색을 넣을 때 기존 색 옆에
        /// 슬쩍 붙이는 것을 막는 문턱이다.
        /// </summary>
        public const float MinimumTintSeparation = 0.12f;

        public static float TintDistance(Color a, Color b) =>
            Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b));

        private static readonly LastShiftStateCueSpec[] stateCues = BuildStateCues();

        public static LastShiftStateCueSpec[] StateCues => stateCues;

        /// <summary>
        /// 냉각실·전력실 상태 단서. 전부 좌현(창 쪽, z 음수)에 몰려 있다 —
        /// <see cref="StateCueSafeMaxZ"/> 가 상한이므로 음수 z 는 언제나 안전하고,
        /// 그 자리가 마침 창가라 서리·그을음이 역광으로 실루엣이 선다.
        ///
        /// 우현(z+)에 하나도 안 둔 것이 의도다. 안전대는 <see cref="StateCueSafeMaxZ"/> 까지
        /// 열려 있지만 경계에 붙여 두면 방 배치가 조금만 바뀌어도 원뿔 안으로 넘어간다.
        ///
        /// <b>x 오프셋은 방 반치수 안에 들어가야 한다.</b> 전력실·냉각실은 §2.1 분할로 각
        /// <c>5m</c>(반치수 <c>2.5</c>)뿐이라, 8m 방을 가정하고 적은 오프셋은 그대로 벽을
        /// 뚫는다. 뒷벽 소품(배전반·열교환기)은 전부 <c>z+</c> 라 여기 단서와 안 겹친다.
        /// </summary>
        private static LastShiftStateCueSpec[] BuildStateCues()
        {
            // 창이 있는 좌현 벽 안쪽 면. 벽에 붙는 단서는 전부 여기 기준이다.
            var portWall = -LastShiftShipDimensions.HalfWidth;

            return new[]
            {
                new LastShiftStateCueSpec("Frost_Deck", LastShiftZone.Cooling, LastShiftStateCue.Frost,
                    -1.3f, 0.02f, -2.0f, new Vector3(2.0f, 0.04f, 1.2f)),
                new LastShiftStateCueSpec("Frost_PortWall", LastShiftZone.Cooling, LastShiftStateCue.Frost,
                    0.6f, 0.55f, portWall + 0.11f, new Vector3(2.4f, 1.1f, 0.06f)),
                new LastShiftStateCueSpec("Frost_Conduit", LastShiftZone.Cooling, LastShiftStateCue.Frost,
                    -1.6f, 1.90f, -1.6f, new Vector3(1.6f, 0.14f, 0.14f)),

                new LastShiftStateCueSpec("Scorch_Deck", LastShiftZone.Power, LastShiftStateCue.Scorch,
                    1.2f, 0.02f, -1.8f, new Vector3(1.8f, 0.04f, 1.4f)),
                new LastShiftStateCueSpec("Scorch_PortWall", LastShiftZone.Power, LastShiftStateCue.Scorch,
                    -0.8f, 1.20f, portWall + 0.11f, new Vector3(2.0f, 1.4f, 0.06f)),
                new LastShiftStateCueSpec("Scorch_Conduit", LastShiftZone.Power, LastShiftStateCue.Scorch,
                    1.5f, 2.20f, -1.0f, new Vector3(1.2f, 0.18f, 0.18f))
            };
        }
    }
}
