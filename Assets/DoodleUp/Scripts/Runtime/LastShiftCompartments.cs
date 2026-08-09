using System;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 배와 함께 태어나는 방. <b>M-2 에서 열하나가 하나로 줄었다</b> —
    /// <c>docs/core-four-rooms-and-hull-schematic-v1.md</c> §2.4·§3.2 가 정본이고, 나머지 열은
    /// 카탈로그로 이관되거나(일곱) 폐지됐다(화장실·휴게실은 숙소 프리팹에 흡수, 구명정은
    /// 배에서 제거).
    ///
    /// <b>"고정 4실" 은 방 넷이 아니라 <c>방 1 + 압력 스파인 4구역</c> 이다</b>(§2.1 조항 S-1).
    /// 조종석·산소실은 <see cref="LastShiftZone"/> 항목이지 이 표의 항목이 아니었고, 중앙 광장은
    /// 아직 코드에 없다(§2.2 — 통로A 가 그 자리를 맡는다). 그래서 이 enum 에 남는 것은 숙소
    /// 하나다.
    ///
    /// 여기서 "구획"은 <see cref="LastShiftZone"/>(압력 구역)과 다른 것이다 — 그레이박스가
    /// 답하는 것은 "방이 어디 있는가" 뿐이고, 그 방이 <c>ZonePressure</c>·게이지·
    /// <c>SIMUL_ZONES</c>·<c>RG-1</c> 중 무엇을 따르는지는 별개다. 이 enum 이
    /// <see cref="LastShiftZone"/> 과 섞이면 그 구분이 조용히 무너진다.
    ///
    /// <b>이 표는 이제 <c>부속</c> 만 든다</b>(중앙 광장 허브 §2.2). 고정 공간 일곱 중
    /// 본선 다섯(광장 + 조종석·산소실·전력실·냉각실)은 천장 <c>3.2m</c> 로 선체가 자기 몸으로
    /// 세우고, 부속 둘(에어록 홀·숙소)만 천장 <c>3.0m</c> 짜리 구획으로 붙는다. 그 높이 차이가
    /// §2.2 마지막 단락의 규약이고, 이 표가 높이를 하나만 들 수 있다는 사실이 경계를 그대로
    /// 그어 준다 — 본선을 여기 담으면 <see cref="LastShiftCompartments.InteriorHeight"/> 가
    /// 두 값이 되어야 한다.
    ///
    /// <b>좌표는 여기서 안 적는다.</b> 둘 다 <see cref="LastShiftPlazaLayout.Footprints"/> 의
    /// 발자국을 그대로 옮긴다.
    ///
    /// <b>값이 늘면 모듈 인덱스가 통째로 밀린다.</b> 세이브 파일·네트워크 복제가 모듈 슬롯을
    /// <c>인덱스 - FixedCount</c> 로 싣고 있으므로 <c>1 → 2</c> 는 옛 세이브를 못 읽는
    /// 변경이다 — M-2 와 같은 계열의 되돌리기 비싼 단계다.
    /// </summary>
    public enum LastShiftCompartment
    {
        /// <summary>
        /// 숙소. 광장 우현 변에 직결한다.
        ///
        /// <b>번호를 안 놓친다.</b> 드레싱 에셋이 이 enum 값을 숫자로 직렬화해 들고 있어
        /// (<c>compartment: 0</c>), 앞에 값을 끼워 넣으면 숙소 소품 열여덟 개가 통째로
        /// 다른 방 것이 된다 — 실제로 한 번 그렇게 나서 침상이 에어록 홀로 옮겨갔다.
        /// </summary>
        Quarters = 0,

        /// <summary>파밍 출정소. 광장 좌현 변에 직결한다(<c>docs/airlock-hall-sortie-room-v1.md</c>).</summary>
        AirlockHall = 1
    }

    /// <summary>
    /// 초기 접근 상태. <b>그레이박스가 무엇을 세워야 하는지</b>만 구분한다 —
    /// 문 구멍을 뚫을지, 잠긴 판으로 메울지가 갈린다.
    ///
    /// <b>값이 셋에서 둘로 줄었다</b>(맵 개편 §6.2-6). 셋째 값
    /// <c>SpaceOpenFunctionLocked</c>("공간은 열려 있고 발진만 잠긴다")는 구명정 하나만을
    /// 위해 있었고, 구명정이 배에서 제거되면서 사용처가 <c>0</c> 이 됐다.
    ///
    /// <b>개방 계열도 대상이 <c>0</c> 이다</b>(조항 K-2). <see cref="Locked"/> 였던 셋
    /// (서버/통신실·수경재배·의무실)은 전부 자유 배치 카탈로그로 갔고, 배치된 모듈은
    /// 언제나 <see cref="Open"/> 으로 선다. 값을 남겨 두는 것은 판정·씬 빌더가 "잠긴 면은
    /// 구멍이 아니라 메운 판" 규칙을 그대로 들고 있기 때문이고, 그 규칙이 다시 쓰일 자리는
    /// 메타 진행 백본이다.
    /// </summary>
    public enum LastShiftCompartmentAccess
    {
        /// <summary>처음부터 드나든다. 고정 숙소와 배치된 모듈 전부가 여기다.</summary>
        Open = 0,

        /// <summary>공간은 있되 문이 안 열린다. 지금 이 값을 쓰는 방은 없다.</summary>
        Locked = 1
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
        /// enum 이 덮는 고정 구획 수. <b>M-2 에서 <c>11 → 1</c> 이 됐다</b> — 숙소 하나다
        /// (맵 개편 §2.4).
        ///
        /// <b><see cref="Count"/> 와 다른 것이 이 표가 이중인 이유다.</b> 자유 배치 모듈은
        /// 컴파일 타임에 enum 값을 가질 수 없으므로 <c>[0, FixedCount)</c> 를 enum 영역으로
        /// 두고 그 위를 append 영역으로 연다. 근거는
        /// <c>docs/tech/free-placement-runtime-chain-estimate-v1.md</c> §3.2.
        ///
        /// <b>이 값이 줄면 모듈 인덱스가 통째로 앞당겨진다.</b> 세이브 파일·네트워크 복제가
        /// 모듈 슬롯을 <c>인덱스 - FixedCount</c> 로 싣고 있으므로, 옛 세이브는 이 개편을
        /// 건너뛸 수 없다 — 그래서 M-2 가 되돌리기 비싼 단계다(§8).
        /// </summary>
        public const int FixedCount = 2;

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
        /// <b>고정 구획만.</b> 선체가 자기 몸으로 세우는 것들 — 선체 골조·문틀·
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
        /// <b>범위 밖 값은 모듈 이름이 된다.</b> 고정 표가 하나로 줄면서 이 함수에 들어오는
        /// 값의 대다수가 모듈이 됐다 — 기본 갈래가 고정 방 이름을 내놓으면 배치된 모듈
        /// 전부가 숙소 이름을 단다.
        /// </summary>
        public static string NameOf(LastShiftCompartment compartment) => compartment switch
        {
            LastShiftCompartment.AirlockHall => "Compartment_AirlockHall",
            LastShiftCompartment.Quarters => "Compartment_Quarters",
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
            // 둘 다 광장 변에 <b>직결</b>한다 — 경유 방이 없다는 것이 중앙 광장 허브의 요지이고
            // (§2.3), 사슬 깊이가 전부 1 인 것이 최악 이탈 `6.05 → 4.26초` 의 실체다.
            // 좌표는 한 줄도 새로 안 적고 발자국표에서 그대로 옮긴다.
            var result = new LastShiftCompartmentSpec[FixedCount];
            result[(int)LastShiftCompartment.AirlockHall] =
                AnnexOf(LastShiftCompartment.AirlockHall, LastShiftPlazaSpace.AirlockHall);
            result[(int)LastShiftCompartment.Quarters] =
                AnnexOf(LastShiftCompartment.Quarters, LastShiftPlazaSpace.Quarters);
            return result;
        }

        /// <summary>
        /// 광장 발자국 하나를 부속 구획 제원으로 옮긴다. 문은 <see cref="LastShiftPlazaLayout.Doors"/>
        /// 가 이미 <b>자기 방 경계와 광장 변에 동시에 얹힌</b> 평면으로 두었으므로
        /// (<see cref="DoorSitsOnOwnBoundary"/> 와 <see cref="LastShiftModuleAttachment"/> 의
        /// 선체 면 판정이 같은 좌표에서 둘 다 성립한다) 축만 옮겨 담으면 된다.
        /// </summary>
        private static LastShiftCompartmentSpec AnnexOf(
            LastShiftCompartment compartment, LastShiftPlazaSpace space)
        {
            var footprint = LastShiftPlazaLayout.Of(space);
            var door = LastShiftPlazaLayout.DoorOf(space);
            return new LastShiftCompartmentSpec(
                compartment,
                footprint.MinX, footprint.MaxX, footprint.MinZ, footprint.MaxZ,
                door.PlaneIsX ? LastShiftDoorPlane.AlongX : LastShiftDoorPlane.AlongZ,
                door.Plane, door.Center,
                -1, LastShiftCompartmentAccess.Open);
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

        /// <summary>
        /// 발자국 좌표만 받는 같은 판정.
        ///
        /// <b>사각형 하나로 못 잰다</b>(§9.3-1). 일자 스파인에서는 배 내부가 <c>38 x 6</c>
        /// 직사각형 하나라 네 부등식이면 끝났는데, 방사형 발자국은 플러스 모양이라 그 경계
        /// 상자를 쓰면 팔 사이 빈 사분면(예: 전력실 좌현 <c>z [-12,-11]</c>)이 "선체 안" 으로
        /// 판정돼 붙일 수 있는 자리가 통째로 막힌다. 고정 공간이 일곱으로 상수라 그냥 훑는다.
        /// </summary>
        public static bool OverlapsHullInterior(float minX, float maxX, float minZ, float maxZ)
        {
            foreach (var footprint in LastShiftPlazaLayout.Footprints)
            {
                // <b>부속 둘은 여기서 안 센다.</b> 에어록 홀·숙소는 발자국표와 이 표에 <b>둘 다</b>
                // 있어서, 세면 그 방이 자기 자신을 파고든 것으로 판정된다 — 고정 좌표가 자기
                // 판정기를 통과 못 하는 상태이고 실제로 그렇게 났다. 모듈이 부속을 파고드는
                // 경우는 표 대 표 <see cref="VolumesOverlap"/> 가 이미 잡는다(부속이 표 안에 있다).
                if (footprint.Space == LastShiftPlazaSpace.AirlockHall) continue;
                if (footprint.Space == LastShiftPlazaSpace.Quarters) continue;

                if (VolumesOverlap(minX, maxX, minZ, maxZ,
                        footprint.MinX, footprint.MaxX, footprint.MinZ, footprint.MaxZ))
                    return true;
            }

            return false;
        }

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
