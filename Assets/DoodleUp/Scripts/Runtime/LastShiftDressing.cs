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
    /// 상태 단서 하나의 자리. <b>x·z 가 둘 다 방 중심 상대</b>이고 y 만 갑판 절대다.
    ///
    /// <b>z 가 절대에서 상대로 바뀌었다.</b> 일자 스파인에서는 방 넷이 전폭을 다 써서 z 가
    /// 안 움직였고, 안전대(<c>z ≤ +1.40</c>)도 절대값이라 절대 z 로 적는 것이 맞았다.
    /// 중앙 광장 허브에서 전력실·냉각실이 <c>z [-11,-6]</c>·<c>z [+6,+11]</c> 로 갈라져
    /// 나가면서 그 전제가 깨졌다 — 옛 절대값을 그대로 두면 냉각실 서리가 광장 한복판에 선다.
    ///
    /// 드레싱 에셋(<c>LastShiftDressingSet</c>)이 이 값을 <c>MetersFromSpaceCenter</c> 앵커로
    /// 그대로 싣고 있고, 씬은 그 에셋에서 선다 — 그래서 <b>둘이 같은 규약이어야 한다</b>.
    /// </summary>
    public readonly struct LastShiftStateCueSpec
    {
        public LastShiftStateCueSpec(string name, LastShiftZone room, LastShiftStateCue kind,
            float offsetX, float centerY, float offsetZ, Vector3 size)
        {
            Name = name;
            Room = room;
            Kind = kind;
            OffsetX = offsetX;
            CenterY = centerY;
            OffsetZ = offsetZ;
            Size = size;
        }

        public string Name { get; }
        public LastShiftZone Room { get; }
        public LastShiftStateCue Kind { get; }

        /// <summary>방 중심에서의 x 오프셋.</summary>
        public float OffsetX { get; }

        public float CenterY { get; }

        /// <summary>방 중심에서의 z 오프셋.</summary>
        public float OffsetZ { get; }

        public Vector3 Size { get; }

        public Vector3 Center => new(
            LastShiftShipDimensions.RoomCenterX(Room) + OffsetX, CenterY,
            LastShiftShipDimensions.RoomCenterZ(Room) + OffsetZ);

        public float MinX => Center.x - Size.x * 0.5f;
        public float MaxX => Center.x + Size.x * 0.5f;
        public float MinZ => Center.z - Size.z * 0.5f;
        public float MaxZ => Center.z + Size.z * 0.5f;
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
        /// 고정 구획색. <b>M-2 에서 열하나가 하나로 줄었다</b> — 배와 함께 태어나는 방이
        /// 숙소뿐이라(<see cref="LastShiftCompartments.FixedCount"/>) 여기 남는 색도 하나다.
        /// 자유 배치 모듈의 색은 카탈로그 쪽(<see cref="LastShiftModulePalette"/>)이 든다.
        ///
        /// <b>붉은 계열이 여기서 사라졌다.</b> 예전 기본 갈래는 구명정 적색이었고, 배 어디에도
        /// 그 색이 없다는 것이 "복도 끝의 적색 = 마지막 수단" 을 성립시켰다. 구명정이 제거되면서
        /// 대상이 <c>0</c> 이 됐고, 그 시각적 강조는 에어록이 물려받기를 권고한다
        /// (<c>docs/outboard-outpost-and-map-final-v1.md</c> §7-7 · 맵 개편 §6.2-6).
        /// </summary>
        public static Color TintOf(LastShiftCompartment compartment) => compartment switch
        {
            LastShiftCompartment.Quarters => new Color(0.62f, 0.50f, 0.42f),
            _ => ModuleTint
        };

        /// <summary>
        /// 자유 배치 모듈의 띠 색. <b>종류별로 안 가른다</b> — 카탈로그 열 종에 색을 하나씩
        /// 주면 배 한 척에 스물다섯 색이 다시 서고, 구획색이 분류가 아니라 소음이 된다는
        /// 예전 진단이 그대로 돌아온다. 모듈이 어느 종류인지는 이름표가 말하고
        /// (<see cref="LastShiftCompartmentLabels.TextOf(in LastShiftCompartmentSpec)"/>),
        /// 색은 "이건 항해 중에 붙인 방이다" 만 말한다.
        ///
        /// 구역색 넷·고정 숙소색과 <see cref="MinimumTintSeparation"/> 이상 떨어져야
        /// 한다는 조건은 그대로 걸린다.
        /// </summary>
        public static readonly Color ModuleTint = new(0.52f, 0.56f, 0.60f);

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
        /// 냉각실·전력실 상태 단서. <b>전부 문 구멍 정면을 비켜 방 선수 쪽으로 몰려 있다</b> —
        /// 두 방은 이제 광장 변에 문 하나씩만 내고 있고, 광장에서 그 구멍을 지나는 직선에
        /// 걸리는 단서는 게이지가 없어도 세 번째 게이지가 된다(§4). 구멍 반폭이 <c>0.8</c> 이라
        /// <c>x</c> 로 그만큼 비켜난 자리가 안전한 자리이고, 그 판정은
        /// <see cref="LastShiftDressingRules"/> 의 <c>C1_ExposureCone</c> 이 기계로 한다.
        ///
        /// 벽에 붙는 둘은 <b>광장 반대쪽 바깥 벽</b>이다(냉각실 <c>z = +11</c> · 전력실
        /// <c>z = -11</c>). 문이 난 벽에 붙이면 그 자체로 구멍 정면이 된다.
        ///
        /// <b>오프셋은 방 반치수 안에 들어가야 한다.</b> 전력실·냉각실은 <c>6 x 5</c> 라
        /// 반치수가 <c>x 3</c> · <c>z 2.5</c> 뿐이고, 더 넓은 방을 가정하고 적은 오프셋은
        /// 그대로 벽을 뚫는다.
        /// </summary>
        private static LastShiftStateCueSpec[] BuildStateCues()
        {
            // 광장 반대쪽 바깥 벽 안쪽 면까지의 z 오프셋. 벽 단서 자체의 두께 절반만 안으로
            // 들어온다. 방 반치수를 발자국에서 뽑는 것이 요점이다 — 리터럴로 두면 두 방이
            // 깊어질 때 단서만 제자리에 남아 벽에서 떨어진다.
            const float wallCueHalfThickness = 0.03f;
            var outerWall =
                LastShiftPlazaLayout.Of(LastShiftPlazaSpace.CoolingRoom).WidthZ * 0.5f - wallCueHalfThickness;

            return new[]
            {
                new LastShiftStateCueSpec("Frost_Deck", LastShiftZone.Cooling, LastShiftStateCue.Frost,
                    -1.9f, 0.02f, -2.0f, new Vector3(2.0f, 0.04f, 1.2f)),
                new LastShiftStateCueSpec("Frost_StarboardWall", LastShiftZone.Cooling, LastShiftStateCue.Frost,
                    -1.9f, 0.55f, outerWall, new Vector3(2.0f, 1.1f, 0.06f)),
                new LastShiftStateCueSpec("Frost_Conduit", LastShiftZone.Cooling, LastShiftStateCue.Frost,
                    -1.6f, 1.90f, -1.6f, new Vector3(1.6f, 0.14f, 0.14f)),

                new LastShiftStateCueSpec("Scorch_Deck", LastShiftZone.Power, LastShiftStateCue.Scorch,
                    1.9f, 0.02f, -1.8f, new Vector3(1.8f, 0.04f, 1.4f)),
                new LastShiftStateCueSpec("Scorch_PortWall", LastShiftZone.Power, LastShiftStateCue.Scorch,
                    -1.9f, 1.20f, -outerWall, new Vector3(2.0f, 1.4f, 0.06f)),
                new LastShiftStateCueSpec("Scorch_Conduit", LastShiftZone.Power, LastShiftStateCue.Scorch,
                    1.5f, 2.20f, -1.0f, new Vector3(1.2f, 0.18f, 0.18f))
            };
        }
    }
}
