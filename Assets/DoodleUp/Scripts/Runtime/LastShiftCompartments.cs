using System;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 그레이박스 구획. 기획 정본은 <c>docs/corridor-4p-redesign-v1.md</c> §17.4 이고
    /// 에어록은 빠져 있다 — 우회 통로 z 경로가 아직 미결이라 좌표를 못 박을 수 없다(§17.5).
    /// 그래서 12 항목 중 11 이다.
    ///
    /// 여기서 "구획"은 <see cref="LastShiftZone"/>(압력 구역)과 다른 것이다. §17.6 이 명시한 대로
    /// 그레이박스가 답하는 것은 "방이 어디 있는가" 뿐이고, 그 방이 <c>ZonePressure</c>·게이지·
    /// <c>SIMUL_ZONES</c>·<c>RG-1</c> 중 무엇을 따르는지는 아직 정해지지 않았다. 이 enum 이
    /// <see cref="LastShiftZone"/> 과 섞이면 그 미결이 조용히 "편입됨"으로 굳는다.
    /// </summary>
    public enum LastShiftCompartment
    {
        Observatory = 0,
        Workshop = 1,
        CargoBay = 2,
        Hangar = 3,
        ServerRoom = 4,
        Lavatory = 5,
        Quarters = 6,
        Lounge = 7,
        Hydroponics = 8,
        MedBay = 9,
        EscapePod = 10
    }

    /// <summary>
    /// 초기 접근 상태. §15.2 의 언락 트리거 자체는 메타 진행 백본(구현은 P0 이후, §15.5)이
    /// 들고 있을 것이므로 여기서는 <b>그레이박스가 무엇을 세워야 하는지</b>만 구분한다 —
    /// 문 구멍을 뚫을지, 잠긴 판으로 메울지가 갈린다.
    /// </summary>
    public enum LastShiftCompartmentAccess
    {
        /// <summary>
        /// 처음부터 드나든다. 생활공간 셋(§9)과 선수 사슬 넷(화물칸·격납고·정비창·관측실)이
        /// 여기다 — 뒤 넷은 §15.2 언락 대상이지만 트리거가 P0 이후라 확장 검토 §2 가
        /// P0 초기값을 열어 두기로 했다.
        /// </summary>
        Open = 0,

        /// <summary>
        /// 공간은 있되 문이 안 열린다. §15.2 언락 대상 중 P0 에서 안 여는 셋
        /// (서버/통신실·수경재배·의무실)이 여기다.
        /// </summary>
        Locked = 1,

        /// <summary>
        /// 공간은 처음부터 열려 있고 "발진 가능 상태" 만 잠긴다(§15.4). 구명정 전용이다 —
        /// 나머지 여덟과 언락의 <b>종류</b>가 다르므로 <see cref="Locked"/> 로 뭉뚱그리지 않는다.
        /// </summary>
        SpaceOpenFunctionLocked = 2
    }

    /// <summary>문이 놓인 평면의 법선 축. 그레이박스에서는 둘뿐이다.</summary>
    public enum LastShiftDoorPlane
    {
        /// <summary>x 평면에 놓인 문. 평면 위 자유 좌표는 z 다.</summary>
        AlongX = 0,

        /// <summary>z 평면에 놓인 문. 평면 위 자유 좌표는 x 다.</summary>
        AlongZ = 1
    }

    /// <summary>
    /// 구획 하나의 그레이박스 제원. 높이는 전 항목 공통이라 여기 없다.
    ///
    /// <b>이 형은 두 종류를 같이 든다.</b> <see cref="LastShiftCompartments.FixedCount"/> 미만의
    /// <see cref="Index"/> 는 <see cref="LastShiftCompartment"/> 값 그대로이고(<see cref="IsFixed"/>),
    /// 그 위는 자유 배치로 붙은 모듈이다. 모듈에는 줄 enum 값이 없으므로
    /// <see cref="Compartment"/> 는 <see cref="IsFixed"/> 일 때만 뜻이 있다 —
    /// <c>docs/tech/free-placement-runtime-chain-estimate-v1.md</c> §3.1.
    ///
    /// <b><c>(int)Compartment == Index</c> 는 두 종류 모두에서 성립한다.</b> 모듈도 범위 밖
    /// 값으로 캐스팅해 담는다. 그래야 부모를 인덱스로 가리키는 자리(<c>ParentIndex</c>)와
    /// 구획을 enum 으로 가리키는 자리가 한 표 안에서 안 갈린다. 대신 그 값을
    /// <see cref="LastShiftCompartments.Of"/> 에 넣으면 고정 표 길이를 넘어 <b>터진다</b> —
    /// 조용히 <c>Observatory</c> 로 읽히는 것보다 낫다.
    /// </summary>
    public readonly struct LastShiftCompartmentSpec
    {
        public LastShiftCompartmentSpec(
            LastShiftCompartment compartment,
            float minX, float maxX, float minZ, float maxZ,
            LastShiftDoorPlane doorPlane, float doorPlaneCoordinate, float doorCenter,
            int parentIndex, LastShiftCompartmentAccess access)
            : this((int)compartment, minX, maxX, minZ, maxZ,
                doorPlane, doorPlaneCoordinate, doorCenter, parentIndex, access)
        {
        }

        /// <summary>
        /// 표 인덱스로 짓는다. 자유 배치 모듈이 이 쪽이다 — 인덱스는
        /// <see cref="LastShiftCompartments.NextModuleIndex"/> 가 준다.
        /// </summary>
        public LastShiftCompartmentSpec(
            int index,
            float minX, float maxX, float minZ, float maxZ,
            LastShiftDoorPlane doorPlane, float doorPlaneCoordinate, float doorCenter,
            int parentIndex, LastShiftCompartmentAccess access)
        {
            Index = index;
            Compartment = (LastShiftCompartment)index;
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
            DoorPlane = doorPlane;
            DoorPlaneCoordinate = doorPlaneCoordinate;
            DoorCenter = doorCenter;
            ParentIndex = parentIndex;
            Access = access;
        }

        /// <summary>
        /// <see cref="LastShiftCompartments.Specs"/> 안의 자리. <c>ParentIndex</c> 가 가리키는
        /// 것도 이 값이다. 고정 구획은 <c>(int)Compartment</c> 와 같다.
        /// </summary>
        public int Index { get; }

        /// <summary>enum 이 있는 구획인가. <see cref="Compartment"/> 를 믿어도 되는지가 이것이다.</summary>
        public bool IsFixed => Index < LastShiftCompartments.FixedCount;

        /// <summary><see cref="IsFixed"/> 일 때만 뜻이 있다. 모듈에서는 범위 밖 값이다.</summary>
        public LastShiftCompartment Compartment { get; }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        public LastShiftDoorPlane DoorPlane { get; }

        /// <summary>문이 놓인 평면. <see cref="LastShiftDoorPlane.AlongX"/> 면 x, 아니면 z 다.</summary>
        public float DoorPlaneCoordinate { get; }

        /// <summary>그 평면 위 문 중심. <see cref="LastShiftDoorPlane.AlongX"/> 면 z, 아니면 x 다.</summary>
        public float DoorCenter { get; }

        /// <summary>
        /// 이 구획을 안쪽으로 잇는 상대. <c>-1</c> 이면 선체(주 통로)에 직접 붙는다.
        /// <b>구획마다 안쪽 문이 정확히 하나</b>라는 것이 §9.4·§17.6 의 "막다른 방" 전제를
        /// 자료 구조로 강제하는 자리다 — 둘을 허용하면 우회로가 생겨 §9.5 가 명시적으로
        /// 아니라고 답한 "4인 게이트 대안 경로"가 실수로 만들어진다.
        /// </summary>
        public int ParentIndex { get; }

        public LastShiftCompartmentAccess Access { get; }

        public float LengthX => MaxX - MinX;
        public float WidthZ => MaxZ - MinZ;
        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterZ => (MinZ + MaxZ) * 0.5f;

        /// <summary>문 중심 좌표. y 는 문 구멍 높이의 절반이다.</summary>
        public Vector3 DoorPosition => DoorPlane == LastShiftDoorPlane.AlongX
            ? new Vector3(DoorPlaneCoordinate, LastShiftZoneDoor.OpeningHeight * 0.5f, DoorCenter)
            : new Vector3(DoorCenter, LastShiftZoneDoor.OpeningHeight * 0.5f, DoorPlaneCoordinate);

        /// <summary>바닥부터 천장 내면까지를 채우는 내부 볼륨.</summary>
        public Bounds Volume => new(
            new Vector3(CenterX, LastShiftCompartments.InteriorHeight * 0.5f, CenterZ),
            new Vector3(LengthX, LastShiftCompartments.InteriorHeight, WidthZ));

        /// <summary>드나들 수 있는가. 잠긴 문은 그레이박스에서 구멍이 아니라 메운 판이다.</summary>
        public bool IsPassable => Access != LastShiftCompartmentAccess.Locked;
    }

    /// <summary>
    /// 그레이박스 좌표의 정본. 씬 빌더·검증기·테스트가 전부 여기서 파생한다.
    ///
    /// <b>x 리터럴을 적지 않는다.</b> §17.4 표의 값들(`-19`, `+19`, `-15`, `+13` 등)은 전부
    /// 선체 반전장 `19m`(전장 `38m`) 기준이고, 지금 선체는 아직 `36m` 다 — §2.2 의
    /// `36 → 38` 개정이 아직 코드에 안 들어왔기 때문이다. 표 값을 그대로 박아 두면 그
    /// 개정이 들어오는 순간 구획 열한 개가 통째로 선체 안쪽 `1m` 로 파고들거나 `1m` 떠서
    /// 벌어진다. 그래서 표의 숫자가 아니라 <b>표가 무엇에 붙어 있는지</b>를 적는다 —
    /// 화물칸은 조종석 선수 끝벽에, 생활공간은 산소실 선미 끝벽에, 서버실은 조종석 방
    /// 중심의 우현 벽에 붙는다. 그러면 전장이 바뀔 때 구획이 따라 움직인다.
    ///
    /// 축 규약은 선체와 같다 — x = 전장, z = 전폭, y = 높이.
    /// </summary>
    public static class LastShiftCompartments
    {
        /// <summary>
        /// 전 항목 공통 내부 높이. §17.4 가 "전 항목 높이 3m 균일" 로 확정했다.
        /// 선체 내부(<see cref="LastShiftShipPhysics.CeilingInnerHeight"/> `3.2m`)보다 낮은 것이
        /// 의도다 — 부속 구획이 본선보다 낮아야 통과할 때 "다른 공간으로 넘어왔다"가 읽힌다.
        /// 문 구멍 높이 `2.2m` 는 그대로 들어간다.
        /// </summary>
        public const float InteriorHeight = 3f;

        /// <summary>구획 벽·바닥·천장 판 두께. 선체와 같은 두께를 쓴다.</summary>
        public const float PanelThickness = LastShiftShipDimensions.HullThickness;

        /// <summary>
        /// enum 이 덮는 고정 구획 수. 에어록을 뺀 `11` 이다(§17.5).
        ///
        /// <b><see cref="Count"/> 와 다른 것이 이 표가 이중인 이유다.</b> 자유 배치 모듈은
        /// 컴파일 타임에 enum 값을 가질 수 없으므로 <c>[0, FixedCount)</c> 를 enum 영역으로
        /// 두고 그 위를 append 영역으로 연다 — <see cref="Of"/> 를 부르는 `37` 자리와
        /// 그 값을 리터럴로 물고 있는 넷(<c>UpperGallery</c>·<c>ObservationGallery</c>·
        /// <c>ObservatoryWindow</c>·<c>DressingRules</c>)을 한 줄도 안 고치기 위해서다.
        /// 근거는 <c>docs/tech/free-placement-runtime-chain-estimate-v1.md</c> §3.2.
        /// </summary>
        public const int FixedCount = 11;

        private static readonly LastShiftCompartmentSpec[] fixedSpecs = BuildSpecs();

        /// <summary>
        /// 지금 살아 있는 표 전체. 모듈이 하나도 없으면 <see cref="fixedSpecs"/> 그 자체다 —
        /// 배열을 새로 안 잡으므로 자유 배치가 안 붙은 배에서 이 파일은 예전과 같이 돈다.
        /// </summary>
        private static LastShiftCompartmentSpec[] specs = fixedSpecs;

        /// <summary>모듈 칸 하나가 <see cref="LastShiftPlacedModules"/> 에서 받은 핸들. 표와 같은 순서다.</summary>
        private static int[] moduleHandles = Array.Empty<int>();

        /// <summary>
        /// 표 길이. <b>이제 상수가 아니다</b> — <see cref="FixedCount"/> + 배치된 모듈 수다.
        /// enum 을 다 덮었는지를 묻는 자리는 <see cref="FixedCount"/> 를 봐야 한다.
        /// </summary>
        public static int Count => specs.Length;

        /// <summary>배치된 모듈 수. <c>Count - FixedCount</c> 다.</summary>
        public static int ModuleCount => specs.Length - FixedCount;

        /// <summary>
        /// 표가 바뀔 때마다 오른다. 표를 옮겨 담아 두는 쪽(판정기 입력·씬 조립기)이
        /// 자기 사본이 낡았는지 묻는 자리다 — <see cref="Specs"/> 참조를 들고 있으면
        /// 등록·해제가 새 배열로 갈아 끼우므로 그 참조는 그 순간 낡는다.
        /// </summary>
        public static int Revision { get; private set; }

        /// <summary>
        /// <b>고정 구획 열하나만.</b> 선체가 자기 몸으로 세우는 것들 — 선체 골조·문틀·
        /// 드레싱처럼 <b>배와 함께 태어난 것만</b> 훑어야 하는 자리가 이쪽이다.
        /// </summary>
        public static LastShiftCompartmentSpec[] FixedSpecs => fixedSpecs;

        /// <summary>
        /// <b>고정 구획 + 배치된 모듈 전체.</b> <c>[0, FixedCount)</c> 가
        /// <see cref="LastShiftCompartment"/> 값 순이고 그 위가 배치 순이다.
        /// 배열 길이는 언제나 <see cref="Count"/> 다 — 빈 칸이 없다(<see cref="TryRemove"/>).
        /// </summary>
        public static LastShiftCompartmentSpec[] Specs => specs;

        /// <summary>
        /// <b>고정 표에서만 찾는다.</b> append 영역 인덱스를 캐스팅해 넣으면 여기서 터진다 —
        /// 모듈에는 enum 값이 없으므로 그 물음 자체가 틀린 것이다.
        /// </summary>
        public static LastShiftCompartmentSpec Of(LastShiftCompartment compartment) => fixedSpecs[(int)compartment];

        /// <summary>표 인덱스로 찾는다. 모듈까지 본다.</summary>
        public static LastShiftCompartmentSpec At(int index) => specs[index];

        /// <summary>
        /// 구획 이름. 씬 오브젝트 이름과 로그가 같은 문자열을 봐야 검증이 성립한다.
        ///
        /// <b>범위 밖 값은 모듈 이름이 된다.</b> 예전 <c>_ =&gt; "Compartment_EscapePod"</c> 를
        /// 그대로 뒀으면 모듈 열 개가 전부 구명정 이름을 달았을 것이다.
        /// </summary>
        public static string NameOf(LastShiftCompartment compartment) => compartment switch
        {
            LastShiftCompartment.Observatory => "Compartment_Observatory",
            LastShiftCompartment.Workshop => "Compartment_Workshop",
            LastShiftCompartment.CargoBay => "Compartment_CargoBay",
            LastShiftCompartment.Hangar => "Compartment_Hangar",
            LastShiftCompartment.ServerRoom => "Compartment_ServerRoom",
            LastShiftCompartment.Lavatory => "Compartment_Lavatory",
            LastShiftCompartment.Quarters => "Compartment_Quarters",
            LastShiftCompartment.Lounge => "Compartment_Lounge",
            LastShiftCompartment.Hydroponics => "Compartment_Hydroponics",
            LastShiftCompartment.MedBay => "Compartment_MedBay",
            LastShiftCompartment.EscapePod => "Compartment_EscapePod",
            _ => ModuleName((int)compartment)
        };

        /// <summary>
        /// 구획 하나의 이름. 고정이면 enum 이름이고, 모듈이면 <c>Compartment_Module_{인덱스}</c> 다.
        ///
        /// <b><see cref="LastShiftCompartmentSpec"/> 에 <c>DisplayName</c> 을 안 붙이는 이유가
        /// 이 함수가 있는 이유다.</b> <c>readonly struct</c> 가 문자열을 들면 배치 판정 루프가
        /// 그 구조체를 복사할 때마다 참조를 끌고 다니게 된다 — 이름은 씬을 세울 때만 필요하다.
        /// </summary>
        public static string NameOf(in LastShiftCompartmentSpec spec) =>
            spec.IsFixed ? NameOf(spec.Compartment) : ModuleName(spec.Index);

        /// <summary>
        /// 모듈 칸 이름. <b>인덱스가 이름에 들어가므로 <see cref="TryRemove"/> 로 앞 칸이
        /// 빠지면 뒤 모듈의 이름이 하나씩 당겨진다.</b> 표를 빈 칸 없이 유지하는 대가이고,
        /// 배치 해제는 기항에서만 일어나 그때 씬을 다시 세우므로 지금은 문제가 아니다 —
        /// 판 안에서 해제가 가능해지면 이름을 인덱스에서 떼야 한다.
        /// </summary>
        public static string ModuleName(int index) => "Compartment_Module_" + index;

        /// <summary>
        /// 안쪽 문이 선체 벽을 뚫는 구획인가. <c>ParentIndex &lt; 0</c> 과 같은 뜻이지만
        /// 씬 빌더가 "선체 판에 구멍을 내야 하는가" 를 묻는 자리라 이름을 따로 둔다.
        /// </summary>
        public static bool ConnectsToHull(LastShiftCompartmentSpec spec) => spec.ParentIndex < 0;

        private static LastShiftCompartmentSpec[] BuildSpecs()
        {
            // 붙는 자리. 전부 선체 치수 정본에서 뽑고, 여기서만 이름을 짧게 준다.
            var bow = -LastShiftShipDimensions.HalfLength;   // 조종석 선수 끝벽 안쪽 면 (§17.4 의 -19)
            var stern = LastShiftShipDimensions.HalfLength;  // 산소실 선미 끝벽 안쪽 면 (§17.4 의 +19)
            var starboard = LastShiftShipDimensions.HalfWidth;   // 우현 긴 벽 (§17.4 의 +3)
            var port = -LastShiftShipDimensions.HalfWidth;       // 좌현 긴 벽 — 창이 있는 쪽이다
            var cockpitCenter = LastShiftShipDimensions.CockpitCenterX;                     // §17.4 의 -15
            var lifeSupportMin = LastShiftShipDimensions.RoomMinX(LastShiftZone.LifeSupport); // §17.4 의 +11

            var result = new LastShiftCompartmentSpec[FixedCount];

            // ── 선수 쪽 사슬 — 조종석 끝벽에서 화물칸 → 정비창 → 관측실 로 뻗고,
            //    격납고만 화물칸 우현으로 갈라진다(§17.3 도해).
            //
            //    <b>넷 다 P0 에서 상시 개방이다</b>(확장 검토 §2). §15.2 는 이 넷을 언락
            //    순서 1·2·3·4 로 두었지만 그 트리거가 메타 진행 백본에 걸려 있고 구현이
            //    P0 이후(§15.5)라, 잠가 두면 P0 기간 내내 안 열린다 — 지어 놓은 `181m²`
            //    가 통째로 메운 판이 되고, 폭 `8m` 이상인 방 둘(화물칸·격납고)이 거기
            //    들어 있어 "열린 공간 중 가장 넓은 방이 폭 `6m`" 가 된다. 회랑 둘도
            //    같이 죽는다: 관측 회랑은 화물칸 쪽 끝이, 상부 회랑은 격납고 쪽 끝이
            //    메운 판이라 지어만 놓고 못 쓰는 `195.5m²` 였다.
            //
            //    <b>언락 설계를 폐기하는 것이 아니라 "P0 씬 = 언락이 끝난 뒤의 배" 로
            //    정의하는 것이다</b>(확장 검토 §2.3). §15.2 의 순서 근거는 그대로 살아
            //    있고, 메타 진행 백본이 붙을 때 여기 초기 <c>Access</c> 값만 되돌리면
            //    설계가 복원된다 — 그래서 사슬 넷만 바꾸고 §15.2 표는 안 건드린다.
            //    안 여는 셋(서버/통신실·수경재배·의무실)은 §2.2 대로 <c>Locked</c> 다.
            result[(int)LastShiftCompartment.CargoBay] = new LastShiftCompartmentSpec(
                LastShiftCompartment.CargoBay,
                bow - 8f, bow, -4f, 4f,
                LastShiftDoorPlane.AlongX, bow, 0f,
                -1, LastShiftCompartmentAccess.Open);

            result[(int)LastShiftCompartment.Hangar] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Hangar,
                bow - 8f, bow, 4f, 14f,
                LastShiftDoorPlane.AlongZ, 4f, bow - 4f,
                (int)LastShiftCompartment.CargoBay, LastShiftCompartmentAccess.Open);

            result[(int)LastShiftCompartment.Workshop] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Workshop,
                bow - 13f, bow - 8f, -2.5f, 2.5f,
                LastShiftDoorPlane.AlongX, bow - 8f, 0f,
                (int)LastShiftCompartment.CargoBay, LastShiftCompartmentAccess.Open);

            result[(int)LastShiftCompartment.Observatory] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Observatory,
                bow - 16f, bow - 13f, -2f, 2f,
                LastShiftDoorPlane.AlongX, bow - 13f, 0f,
                (int)LastShiftCompartment.Workshop, LastShiftCompartmentAccess.Open);

            // ── 조종석 분기. §17.4 는 좌현(`z -9~-3`)이라고 적었지만 <b>이 선체의 좌현은 벽이
            //    아니라 창이다</b> — `OuterHull_Front*` 는 전장 전체에 걸쳐 눈높이 구간이 비어
            //    있고 그 너머에 `SpaceVoid` 와 `StarField` 가 놓여 있다. 좌현에 구획을 붙이면
            //    조종석에서 보이는 별이 통째로 회색 상자로 막힌다. 치수(`4×6×3`)와 문 x(조종석
            //    방 중심)는 표 그대로 두고 <b>부호만 우현으로 뒤집는다</b>. §9.2 가 부속 블록
            //    위치를 "구조적으로 동일, art/tech 판단" 으로 넘긴 것과 같은 종류의 판단이다.
            result[(int)LastShiftCompartment.ServerRoom] = new LastShiftCompartmentSpec(
                LastShiftCompartment.ServerRoom,
                cockpitCenter - 2f, cockpitCenter + 2f, starboard, starboard + 6f,
                LastShiftDoorPlane.AlongZ, starboard, cockpitCenter,
                -1, LastShiftCompartmentAccess.Locked);

            // ── 산소실 우현 분기. 문은 산소실 방 선수 끝에서 2m 들어간 자리다.
            result[(int)LastShiftCompartment.Hydroponics] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Hydroponics,
                lifeSupportMin - 1f, lifeSupportMin + 5f, starboard, starboard + 6f,
                LastShiftDoorPlane.AlongZ, starboard, lifeSupportMin + 2f,
                -1, LastShiftCompartmentAccess.Locked);

            // ── 선미 쪽 사슬 — 생활공간 셋(§9)이 일렬로 붙고 그 끝에 구명정이 온다.
            //    생활공간은 §15.2 언락 목록에 없다. 처음부터 드나드는 공간이다.
            result[(int)LastShiftCompartment.Lavatory] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Lavatory,
                stern, stern + 2f, port, starboard,
                LastShiftDoorPlane.AlongX, stern, 0f,
                -1, LastShiftCompartmentAccess.Open);

            result[(int)LastShiftCompartment.Quarters] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Quarters,
                stern + 2f, stern + 6f, port, starboard,
                LastShiftDoorPlane.AlongX, stern + 2f, 0f,
                (int)LastShiftCompartment.Lavatory, LastShiftCompartmentAccess.Open);

            result[(int)LastShiftCompartment.Lounge] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Lounge,
                stern + 6f, stern + 10f, port, starboard,
                LastShiftDoorPlane.AlongX, stern + 6f, 0f,
                (int)LastShiftCompartment.Quarters, LastShiftCompartmentAccess.Open);

            result[(int)LastShiftCompartment.EscapePod] = new LastShiftCompartmentSpec(
                LastShiftCompartment.EscapePod,
                stern + 10f, stern + 14f, -2f, 2f,
                LastShiftDoorPlane.AlongX, stern + 10f, 0f,
                (int)LastShiftCompartment.Lounge, LastShiftCompartmentAccess.SpaceOpenFunctionLocked);

            // ── 숙소 우현 분기.
            result[(int)LastShiftCompartment.MedBay] = new LastShiftCompartmentSpec(
                LastShiftCompartment.MedBay,
                stern + 2f, stern + 7f, starboard, starboard + 5f,
                LastShiftDoorPlane.AlongZ, starboard, stern + 4f,
                (int)LastShiftCompartment.Quarters, LastShiftCompartmentAccess.Locked);

            return result;
        }

        /// <summary>
        /// 두 구획 볼륨이 실제로 겹치는가. 맞닿는 면(공유 벽)은 겹침이 아니다 —
        /// 사슬로 이어 붙인 구획은 언제나 한 면을 공유하므로 열린 구간 비교를 써야 한다.
        /// </summary>
        public static bool VolumesOverlap(in LastShiftCompartmentSpec a, in LastShiftCompartmentSpec b) =>
            VolumesOverlap(a.MinX, a.MaxX, a.MinZ, a.MaxZ, b.MinX, b.MaxX, b.MinZ, b.MaxZ);

        /// <summary>
        /// 발자국 좌표만 받는 같은 판정. <see cref="LastShiftPlacementRules"/> 가 구획표 밖의
        /// 배치 후보를 잴 때 쓴다 — 자유 배치 후보는 아직 <see cref="LastShiftCompartmentSpec"/>
        /// 가 아니지만 겹침 규약은 같아야 한다.
        /// </summary>
        public static bool VolumesOverlap(
            float aMinX, float aMaxX, float aMinZ, float aMaxZ,
            float bMinX, float bMaxX, float bMinZ, float bMaxZ) =>
            aMinX < bMaxX - Epsilon && bMinX < aMaxX - Epsilon &&
            aMinZ < bMaxZ - Epsilon && bMinZ < aMaxZ - Epsilon;

        /// <summary>구획 볼륨이 선체 내부(방·통로가 타일링한 영역)를 침범하는가.</summary>
        public static bool OverlapsHullInterior(in LastShiftCompartmentSpec spec) =>
            OverlapsHullInterior(spec.MinX, spec.MaxX, spec.MinZ, spec.MaxZ);

        /// <summary>발자국 좌표만 받는 같은 판정. <see cref="VolumesOverlap(float,float,float,float,float,float,float,float)"/> 와 같은 이유로 있다.</summary>
        public static bool OverlapsHullInterior(float minX, float maxX, float minZ, float maxZ) =>
            minX < LastShiftShipDimensions.HalfLength - Epsilon &&
            -LastShiftShipDimensions.HalfLength < maxX - Epsilon &&
            minZ < LastShiftShipDimensions.HalfWidth - Epsilon &&
            -LastShiftShipDimensions.HalfWidth < maxZ - Epsilon;

        /// <summary>
        /// 문이 <b>자기 구획의 경계면 위</b>에 있고, 구멍 폭이 그 면 안에 다 들어가는가.
        /// 문이 면에서 벗어나 있으면 씬에서는 벽 옆 허공에 문틀이 서고, 폭이 넘치면
        /// 모서리에 틈이 남아 그레이박스가 안 닫힌다.
        /// </summary>
        public static bool DoorSitsOnOwnBoundary(in LastShiftCompartmentSpec spec)
        {
            var half = LastShiftZoneDoor.OpeningWidth * 0.5f;
            if (spec.DoorPlane == LastShiftDoorPlane.AlongX)
                return (Mathf.Abs(spec.DoorPlaneCoordinate - spec.MinX) < Epsilon ||
                        Mathf.Abs(spec.DoorPlaneCoordinate - spec.MaxX) < Epsilon) &&
                       spec.DoorCenter - half >= spec.MinZ - Epsilon &&
                       spec.DoorCenter + half <= spec.MaxZ + Epsilon;

            return (Mathf.Abs(spec.DoorPlaneCoordinate - spec.MinZ) < Epsilon ||
                    Mathf.Abs(spec.DoorPlaneCoordinate - spec.MaxZ) < Epsilon) &&
                   spec.DoorCenter - half >= spec.MinX - Epsilon &&
                   spec.DoorCenter + half <= spec.MaxX + Epsilon;
        }

        /// <summary>
        /// 선체까지 몇 개의 문을 지나야 하는가. 부모 사슬을 거슬러 올라가고,
        /// 사슬이 <see cref="Count"/> 보다 길어지면 순환이라 <c>-1</c> 을 돌려준다 —
        /// 순환이 있으면 §9.5 가 아니라고 답한 대안 경로가 생긴다.
        /// </summary>
        public static int DoorDepth(LastShiftCompartment compartment) => DoorDepth((int)compartment);

        /// <summary>표 인덱스로 묻는 같은 자. 모듈까지 본다.</summary>
        public static int DoorDepth(int index)
        {
            if (index < 0 || index >= specs.Length) return -1;

            for (var depth = 1; depth <= specs.Length; depth++)
            {
                var parent = specs[index].ParentIndex;
                if (parent < 0) return depth;
                if (parent >= specs.Length) return -1;
                index = parent;
            }
            return -1;
        }

        // ── append 영역 ─────────────────────────────────────────────────────

        /// <summary>
        /// 다음 모듈이 받을 인덱스. 후보 제원을 짓는 쪽이 이 값을 <see cref="LastShiftCompartmentSpec"/>
        /// 생성자에 넣어야 <see cref="Judge"/> 로 미리 재 본 후보와 <see cref="TryRegister"/> 가
        /// 실제로 넣는 것이 같은 물건이 된다.
        /// </summary>
        public static int NextModuleIndex => specs.Length;

        /// <summary>
        /// 후보 하나를 <b>넣지 않고</b> 재 본다. 배치 커서가 매 프레임 부르는 자리다 —
        /// 판정 자체는 <see cref="LastShiftPlacementRules.Evaluate"/> 이고, 이 함수가 하는
        /// 일은 지금 표를 판정기 입력으로 옮기는 것뿐이다.
        /// </summary>
        public static LastShiftPlacementVerdict Judge(in LastShiftCompartmentSpec candidate) =>
            LastShiftPlacementRules.Evaluate(
                LastShiftPlacementRules.TableOf(specs), LastShiftPlacement.From(candidate));

        /// <summary>
        /// 배치 하나를 확정한다. <b>판정을 통과해야만 표에 들어간다</b> — 판정기를 건너뛰는
        /// 등록 경로를 안 두는 것이 이 함수의 요지다. 겹치거나 사슬이 끊긴 방이 표에 들어가면
        /// 그 뒤의 모든 이탈·최장 쌍 계산이 그 방을 진짜로 걸어갈 수 있는 것으로 센다.
        ///
        /// <b>구역 오버레이 등록을 같이 한다.</b> 표와 <see cref="LastShiftPlacedModules"/> 가
        /// 따로 등록되면 발자국은 있는데 압력이 선체 밴드에서 나오는 방이 생긴다 —
        /// 문을 닫아도 격리가 안 되는 배가 그것이다(타당성 검토 §11-1). 넘기는 구역은 후보
        /// 자기 좌표가 아니라 <see cref="LastShiftPlacementVerdict.Zone"/>, 즉 사슬 뿌리의
        /// 선체 문이 정한 값이다(조항 F-1).
        ///
        /// <paramref name="catalogIndex"/> 는 오버레이로 그대로 흘러가 <b>효과의 정본</b>이 된다
        /// (<see cref="LastShiftModuleEffects"/>). 안 넘기면 발자국만 있고 효과가 없는 방이 서는데,
        /// 그건 표를 직접 쓰는 테스트·조립 경로에서 맞는 기본값이다 — 카탈로그를 안 거친 칸에
        /// 산소 감속이 붙으면 그 배는 아무도 안 산 효과를 갖는다.
        /// </summary>
        public static bool TryRegister(
            in LastShiftCompartmentSpec candidate, out int index, out LastShiftPlacementVerdict verdict,
            int catalogIndex = LastShiftPlacedModule.NoCatalogIndex)
        {
            if (candidate.Index != specs.Length)
                throw new ArgumentException(
                    $"module spec index must be {nameof(NextModuleIndex)}({specs.Length}) but was {candidate.Index}",
                    nameof(candidate));

            index = -1;
            verdict = Judge(candidate);
            if (!verdict.Accepted) return false;

            var grown = new LastShiftCompartmentSpec[specs.Length + 1];
            Array.Copy(specs, grown, specs.Length);
            grown[specs.Length] = candidate;

            var handles = new int[moduleHandles.Length + 1];
            Array.Copy(moduleHandles, handles, moduleHandles.Length);
            handles[moduleHandles.Length] = LastShiftPlacedModules.Register(
                candidate.MinX, candidate.MaxX, candidate.MinZ, candidate.MaxZ, verdict.Zone, catalogIndex);

            index = specs.Length;
            specs = grown;
            moduleHandles = handles;
            Revision++;
            return true;
        }

        /// <summary>
        /// 모듈 하나를 뺀다. <b>자식이 달린 모듈은 못 뺀다</b> — 빼면 그 자식들이 표 밖을
        /// 가리키거나(사슬 끊김) 엉뚱한 부모에 붙는다. 잎부터 빼는 것은 부르는 쪽 몫이다.
        ///
        /// <b>표에 빈 칸을 안 남긴다.</b> 뒤 칸을 당기고 그보다 큰 <c>ParentIndex</c> 를 하나씩
        /// 줄인다. 대신 <b>모듈 인덱스는 안정적이지 않다</b> — 인덱스를 들고 있는 쪽은
        /// <see cref="Revision"/> 을 같이 들고 있어야 한다. 빈 칸(무덤)을 남기는 쪽을 안 고른
        /// 것은 <see cref="Specs"/> 를 훑는 자리 여섯 곳이 전부 "죽은 칸인가" 를 물어야 하고,
        /// 그 물음을 한 곳에서 빠뜨리면 씬에 부피 없는 방이 서기 때문이다.
        /// </summary>
        public static bool TryRemove(int index)
        {
            if (index < FixedCount || index >= specs.Length) return false;

            for (var other = FixedCount; other < specs.Length; other++)
                if (specs[other].ParentIndex == index) return false;

            LastShiftPlacedModules.Remove(moduleHandles[index - FixedCount]);

            // 마지막 모듈이 빠지면 고정 표 자체로 되돌린다. 길이만 같은 사본을 남기면
            // "모듈이 없는 표 == 고정 표" 가 참조로는 거짓이 되고, 그 뒤로 둘 중
            // 어느 쪽을 고쳤는지가 갈린다.
            if (specs.Length - 1 == FixedCount)
            {
                specs = fixedSpecs;
                moduleHandles = Array.Empty<int>();
                Revision++;
                return true;
            }

            var shrunk = new LastShiftCompartmentSpec[specs.Length - 1];
            Array.Copy(specs, shrunk, index);
            for (var source = index + 1; source < specs.Length; source++)
                shrunk[source - 1] = Reindex(specs[source], source - 1, index);

            var handles = new int[moduleHandles.Length - 1];
            Array.Copy(moduleHandles, handles, index - FixedCount);
            Array.Copy(moduleHandles, index - FixedCount + 1,
                handles, index - FixedCount, moduleHandles.Length - (index - FixedCount) - 1);

            specs = shrunk;
            moduleHandles = handles;
            Revision++;
            return true;
        }

        /// <summary>
        /// 모듈 칸 하나가 어느 카탈로그 항목으로 섰는가. 표는 종류를 안 들고 오버레이가 드는데
        /// (<see cref="LastShiftPlacedModule.CatalogIndex"/>), 그 둘을 잇는 핸들 배열이 여기
        /// 비공개라 밖에서는 종류를 읽을 길이 없었다.
        ///
        /// <b>복제가 이 문으로 종류를 싣는다</b>(<see cref="LastShiftPlacementReplication"/>) —
        /// 종류를 안 실으면 클라이언트에 선 모듈은 발자국만 같고 효과가 하나도 안 붙는다.
        /// 고정 구획과 범위 밖은 <see cref="LastShiftPlacedModule.NoCatalogIndex"/> 다.
        /// </summary>
        public static int CatalogIndexOf(int index)
        {
            if (index < FixedCount || index >= specs.Length) return LastShiftPlacedModule.NoCatalogIndex;

            return LastShiftPlacedModules.TryGet(moduleHandles[index - FixedCount], out var module)
                ? module.CatalogIndex
                : LastShiftPlacedModule.NoCatalogIndex;
        }

        /// <summary>모듈을 전부 뺀다. 씬을 다시 세울 때와 테스트가 부른다.</summary>
        public static void ClearModules()
        {
            if (ReferenceEquals(specs, fixedSpecs)) return;

            foreach (var handle in moduleHandles) LastShiftPlacedModules.Remove(handle);

            specs = fixedSpecs;
            moduleHandles = Array.Empty<int>();
            Revision++;
        }

        /// <summary>칸이 당겨질 때 인덱스와 부모를 같이 옮긴다. 고정 구획을 가리키는 부모는 안 움직인다.</summary>
        private static LastShiftCompartmentSpec Reindex(
            in LastShiftCompartmentSpec spec, int index, int removed) => new(
            index, spec.MinX, spec.MaxX, spec.MinZ, spec.MaxZ,
            spec.DoorPlane, spec.DoorPlaneCoordinate, spec.DoorCenter,
            spec.ParentIndex > removed ? spec.ParentIndex - 1 : spec.ParentIndex,
            spec.Access);

        /// <summary>
        /// 정적 상태라 초기화 훅이 있어야 한다 — 도메인 리로드를 끈 에디터에서는 플레이를
        /// 멈춰도 정적 필드가 안 죽으므로 지난 판의 모듈이 다음 판의 표에 그대로 남는다.
        /// <see cref="LastShiftPlacedModules"/> 와 같은 이유·같은 방식이다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => ClearModules();

        private const float Epsilon = 0.0001f;

        static LastShiftCompartments()
        {
            if (fixedSpecs.Length != Enum.GetValues(typeof(LastShiftCompartment)).Length)
                throw new InvalidOperationException("compartment spec table must cover every LastShiftCompartment value");
        }
    }
}
