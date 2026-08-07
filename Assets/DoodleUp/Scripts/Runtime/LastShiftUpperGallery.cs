using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>회랑 다리 하나의 평면 발자국. 높이는 전 구간 공통이라 여기 없다.</summary>
    public readonly struct LastShiftGalleryLeg
    {
        public LastShiftGalleryLeg(string name, float minX, float maxX, float minZ, float maxZ)
        {
            Name = name;
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public string Name { get; }
        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterZ => (MinZ + MaxZ) * 0.5f;
        public float LengthX => MaxX - MinX;
        public float WidthZ => MaxZ - MinZ;
    }

    /// <summary>
    /// 회랑이 구획에 붙는 자리. 문이 뚫리는 면은 <b>구획 쪽</b>이고, 그래서 평면·좌표가
    /// 전부 구획 경계면 기준이다 — 씬 빌더의 면 소유 규칙(구획이 자기 면을 세우고 거기에
    /// 구멍을 뚫는다)을 그대로 따른다.
    /// </summary>
    public readonly struct LastShiftGalleryBranch
    {
        public LastShiftGalleryBranch(LastShiftCompartment compartment,
            LastShiftDoorPlane doorPlane, float doorPlaneCoordinate, float doorCenter, int legIndex)
        {
            Compartment = compartment;
            DoorPlane = doorPlane;
            DoorPlaneCoordinate = doorPlaneCoordinate;
            DoorCenter = doorCenter;
            LegIndex = legIndex;
        }

        public LastShiftCompartment Compartment { get; }
        public LastShiftDoorPlane DoorPlane { get; }

        /// <summary>문이 놓인 구획 경계면. <see cref="LastShiftDoorPlane.AlongX"/> 면 x, 아니면 z 다.</summary>
        public float DoorPlaneCoordinate { get; }

        /// <summary>그 면 위 문 중심. <see cref="LastShiftDoorPlane.AlongX"/> 면 z, 아니면 x 다.</summary>
        public float DoorCenter { get; }

        /// <summary>이 문 너머로 이어지는 회랑 다리.</summary>
        public int LegIndex { get; }
    }

    /// <summary>
    /// 상부 회랑(격납고 ~ 구명정)의 좌표 정본. 기획 정본은
    /// <c>docs/corridor-4p-redesign-v1.md</c> §25.4(B)(개념)와 §27.4(경로 확정)다.
    ///
    /// <b>이 회랑이 고리를 닫는다.</b> 지금까지 보조 구획 열한 개는 선체에서 두 갈래로 뻗은
    /// 나뭇가지였고(§25.2), 그래서 배가 "일자형" 으로 읽혔다. 선수 클러스터 끝(격납고)과
    /// 선미 클러스터 끝(구명정)을 이으면 같은 방들이 <b>중심축 + 테두리 고리</b>가 된다 —
    /// 그 고리가 곧 §26.3 의 원반 테두리라, 실루엣(<see cref="LastShiftHullShell"/>)과
    /// 위상이 같은 선을 공유한다. §27.5 가 스파인 꺾기를 채택하지 않은 근거가 이것이다.
    ///
    /// <b>압력존과 무관하다</b>(§27.4 마지막 줄, §24). <c>ZonePressure</c>·<c>RG-1</c>·
    /// <c>RG-4</c>·<c>SIMUL_ZONES</c> 어느 것도 여기 적용되지 않는다. 다만 이 회랑이
    /// <b>주 통로를 안 거치는 이면 동선을 만든다는 것은 사실이고 의도다</b>(§25.4(B)) —
    /// 서버/통신실(조종석 구역 우현)과 수경재배(산소실 구역 우현)가 이 회랑으로 이어지므로,
    /// 둘 다 열리면 조종석↔산소실 사이에 갑판 하부 우회 통로(<see cref="LastShiftBypassDuct"/>)
    /// 말고 두 번째 우회로가 생긴다. <c>RG-1(2)</c>(최악 <b>복구</b> 경로)는 상한 제약이라
    /// 경로가 짧아지는 쪽으로는 안 깨지지만, 우회로가 둘이 되는 것 자체는 4인 게이트의
    /// 대안 경로 설계(§5·§9.5)와 겹치는 판단이라 기획이 알고 있어야 한다.
    ///
    /// 축 규약은 선체와 같다 — x = 전장, z = 전폭, y = 높이. 좌표는 전부 구획 표에서
    /// 파생한다. 리터럴 <c>+10</c>/<c>+12</c>/<c>-19</c> 를 적으면 선체 전장이 또 바뀔 때
    /// (36→38 이 이미 한 번 있었다) 구획만 따라 움직이고 회랑이 제자리에 남아, 문이
    /// 벽 옆 허공에 뜬다.
    /// </summary>
    public static class LastShiftUpperGallery
    {
        /// <summary>회랑 폭. §27.4 확정값 <c>2m</c>.</summary>
        public const float Width = 2f;

        /// <summary>내부 높이·판 두께는 보조 구획과 같다 — 같은 고리 위의 같은 층이다.</summary>
        public const float InteriorHeight = LastShiftCompartments.InteriorHeight;

        public const float PanelThickness = LastShiftCompartments.PanelThickness;

        /// <summary>
        /// 분기 구획 바깥 면과 회랑 안쪽 면 사이의 여유. §27.4 가 "기존 <c>+z</c>측 구획
        /// 최대치 <c>+9</c>보다 바깥, 벽 두께 여유 포함" 이라고 적은 그 여유다. 판 두께
        /// (<c>0.2</c>) 두 장이 들어가고도 남아야 구획 바깥 벽과 회랑 안쪽 벽이 안 겹친다.
        /// </summary>
        public const float Clearance = 1f;

        /// <summary>
        /// 회랑 안쪽 면의 z. 분기 구획들의 바깥 면 중 가장 먼 것에서 <see cref="Clearance"/>
        /// 만큼 떨어진다 — 표의 <c>+10</c> 이 여기서 나온다.
        ///
        /// 격납고(<c>z +4~+14</c>)는 이 계산에 안 들어간다. 격납고는 회랑이 옆구리로
        /// 붙는 분기가 아니라 <b>회랑이 끝나는 종점</b>이고, 회랑의 z 띠가 격납고 z 범위
        /// <b>안</b>에 들어가야 문이 그 끝벽에 뚫린다 — 방향이 반대다.
        /// </summary>
        public static float NearZ =>
            Mathf.Max(
                LastShiftCompartments.Of(LastShiftCompartment.ServerRoom).MaxZ,
                LastShiftCompartments.Of(LastShiftCompartment.Hydroponics).MaxZ,
                LastShiftCompartments.Of(LastShiftCompartment.MedBay).MaxZ) + Clearance;

        /// <summary>회랑 바깥 면의 z.</summary>
        public static float FarZ => NearZ + Width;

        public static float CenterZ => NearZ + Width * 0.5f;

        /// <summary>회랑이 시작하는 x. 격납고의 선미 쪽 끝벽이다(§27.4 의 분기문 <c>x=-19</c>).</summary>
        public static float RunMinX => LastShiftCompartments.Of(LastShiftCompartment.Hangar).MaxX;

        /// <summary>
        /// 구명정으로 내려가는 다리의 중심 x. 구명정 방 중심이라 문 구멍이 그 면 한가운데
        /// 온다.
        ///
        /// <b>여기가 §27.4 의 도해와 갈리는 유일한 자리다.</b> 표는 <c>x=+25~+29</c> 구간에서
        /// <c>z</c> 를 <c>+10 → +2</c> 로 비스듬히 좁히라고 적었고, 그 정밀 곡선을
        /// <c>tech</c> 실측 대상(§27.4·§27.7-2)으로 남겼다. 실측 결과 그 사선은 <b>두 방을
        /// 관통한다</b> — 의무실(<c>x +21~+26</c>, <c>z +3~+8</c>)의 바깥 모서리를
        /// <c>x ≥ +25.7</c> 구간에서 파고들고, 계속 내려가면 휴게실(<c>x +25~+29</c>,
        /// <c>z -3~+3</c>)을 정면으로 가로지른다. 사선이 <c>z=+2</c> 에 닿는 자리가 구명정이
        /// 아니라 휴게실이기 때문이다.
        ///
        /// 그래서 <b>사선 대신 직각으로 꺾어</b> 휴게실 선미 끝(<c>x=+29</c>) 너머에서
        /// 내려간다. 도착 면(구명정 우현 <c>z=+2</c>)과 강하 폭(<c>+10 → +2</c>)은 표
        /// 그대로이고, 바뀐 것은 내려가는 <c>x</c> 뿐이다. 축 정렬을 고른 이유는 이
        /// 그레이박스의 겹침 검증(§21.1)이 <c>AABB</c> 비교이기 때문이다 — 사선 다리는
        /// 자기 <c>AABB</c> 가 의무실·휴게실과 겹쳐서, 실제로 안 겹치게 만들어도 검증이
        /// 그것을 말해 주지 못한다.
        /// </summary>
        public static float DescentCenterX =>
            LastShiftCompartments.Of(LastShiftCompartment.EscapePod).CenterX;

        /// <summary>회랑이 끝나는 x. 강하 다리의 바깥 면이다.</summary>
        public static float RunMaxX => DescentCenterX + Width * 0.5f;

        /// <summary>강하 다리가 합류하는 z. 구명정 우현 면이다(§27.4 의 <c>+2</c>).</summary>
        public static float DescentEndZ => LastShiftCompartments.Of(LastShiftCompartment.EscapePod).MaxZ;

        /// <summary>다리 수 — 긴 구간 하나, 강하 하나, 옆구리 분기 셋.</summary>
        public const int LegCount = 5;

        public const int RunLeg = 0;
        public const int DescentLeg = 1;
        public const int ServerRoomSpur = 2;
        public const int HydroponicsSpur = 3;
        public const int MedBaySpur = 4;

        /// <summary>분기·종점 수. 격납고·구명정(종점 둘)과 옆구리 분기 셋이다(§27.4).</summary>
        public const int BranchCount = 5;

        private static readonly LastShiftGalleryLeg[] legs = BuildLegs();
        private static readonly LastShiftGalleryBranch[] branches = BuildBranches();

        public static LastShiftGalleryLeg[] Legs => legs;
        public static LastShiftGalleryBranch[] Branches => branches;

        /// <summary>
        /// 다리 하나를 번호로 집는다. 범위를 벗어난 번호는 자르고 던지지 않는다 —
        /// 부르는 쪽이 드레싱 데이터(<see cref="LastShiftDressingSpace.galleryLeg"/>)이고,
        /// 거기 든 숫자는 Inspector 에서 손으로 적힌 값이라 언제든 다리 수보다 클 수 있다.
        /// 그때 예외를 던지면 씬 빌드가 통째로 죽고, 정작 어느 소품이 범인인지는
        /// 스택에 안 남는다. 잘라서 경계 검사(<c>R1_Bounds</c>)에 걸리게 두는 쪽이
        /// 그 소품의 이름을 로그에 남긴다.
        /// </summary>
        public static LastShiftGalleryLeg LegAt(int index) =>
            legs[Mathf.Clamp(index, 0, LegCount - 1)];

        /// <summary>회랑 오브젝트 이름의 접두. 씬 로그와 검증기가 같은 문자열을 봐야 한다.</summary>
        public const string RootName = "UpperGallery";

        private static LastShiftGalleryLeg[] BuildLegs()
        {
            var result = new LastShiftGalleryLeg[LegCount];

            // 격납고 끝벽에서 구명정 위까지 달리는 긴 구간. 방향 전환이 없다 — 꺾임은
            // 강하 다리가 전담한다.
            result[RunLeg] = new LastShiftGalleryLeg("Run", RunMinX, RunMaxX, NearZ, FarZ);

            // 구명정 우현 면으로 내려가는 다리. z 로 달리므로 폭은 x 축에 온다.
            result[DescentLeg] = new LastShiftGalleryLeg("Descent",
                DescentCenterX - Width * 0.5f, DescentCenterX + Width * 0.5f, DescentEndZ, NearZ);

            result[ServerRoomSpur] = Spur("Spur_ServerRoom", LastShiftCompartment.ServerRoom);
            result[HydroponicsSpur] = Spur("Spur_Hydroponics", LastShiftCompartment.Hydroponics);
            result[MedBaySpur] = Spur("Spur_MedBay", LastShiftCompartment.MedBay);

            return result;
        }

        /// <summary>
        /// 옆구리 분기 하나. 구획 바깥 면(<c>MaxZ</c>)에서 회랑 안쪽 면까지의 짧은 목이고,
        /// x 중심은 그 구획의 중심이다 — §27.4 표의 <c>-15</c>/<c>+13</c>/<c>+23.5</c> 가
        /// 셋 다 구획 중심이라 리터럴을 안 적어도 같은 값이 나온다.
        /// </summary>
        private static LastShiftGalleryLeg Spur(string name, LastShiftCompartment compartment)
        {
            var spec = LastShiftCompartments.Of(compartment);
            return new LastShiftGalleryLeg(name,
                spec.CenterX - Width * 0.5f, spec.CenterX + Width * 0.5f, spec.MaxZ, NearZ);
        }

        private static LastShiftGalleryBranch[] BuildBranches()
        {
            var hangar = LastShiftCompartments.Of(LastShiftCompartment.Hangar);
            var escapePod = LastShiftCompartments.Of(LastShiftCompartment.EscapePod);

            return new[]
            {
                // 종점 둘. 회랑이 방의 끝벽에 정면으로 닿으므로 짧은 목이 필요 없다.
                new LastShiftGalleryBranch(LastShiftCompartment.Hangar,
                    LastShiftDoorPlane.AlongX, hangar.MaxX, CenterZ, RunLeg),
                new LastShiftGalleryBranch(LastShiftCompartment.EscapePod,
                    LastShiftDoorPlane.AlongZ, escapePod.MaxZ, DescentCenterX, DescentLeg),

                // 옆구리 분기 셋(§27.4). 이 셋이 "주 통로를 안 거치는 이면 동선" 을 만든다.
                Branch(LastShiftCompartment.ServerRoom, ServerRoomSpur),
                Branch(LastShiftCompartment.Hydroponics, HydroponicsSpur),
                Branch(LastShiftCompartment.MedBay, MedBaySpur)
            };
        }

        private static LastShiftGalleryBranch Branch(LastShiftCompartment compartment, int legIndex)
        {
            var spec = LastShiftCompartments.Of(compartment);
            return new LastShiftGalleryBranch(compartment,
                LastShiftDoorPlane.AlongZ, spec.MaxZ, spec.CenterX, legIndex);
        }

        /// <summary>
        /// 이 회랑 문이 실제로 뚫리는가. 잠긴 구획은 그레이박스에서 구멍이 아니라 메운
        /// 판이다(§15.2) — 회랑도 같은 규칙을 따라야 언락 하나로 고리가 같이 열린다.
        /// </summary>
        public static bool IsPassable(in LastShiftGalleryBranch branch) =>
            LastShiftCompartments.Of(branch.Compartment).IsPassable;

        /// <summary>
        /// 이 구획 면에 뚫려야 하는 회랑 문의 자유축 좌표. 없으면 빈 배열이다.
        /// 씬 빌더가 구획 벽을 세울 때 자식 문(<c>ChildDoorwaysOn</c>)과 합쳐서 쓴다.
        /// </summary>
        public static float[] DoorwaysOn(LastShiftCompartment compartment,
            LastShiftDoorPlane plane, float faceCoordinate)
        {
            var count = 0;
            foreach (var branch in branches)
                if (Matches(branch, compartment, plane, faceCoordinate)) count++;

            var result = new float[count];
            var index = 0;
            foreach (var branch in branches)
                if (Matches(branch, compartment, plane, faceCoordinate)) result[index++] = branch.DoorCenter;
            return result;
        }

        private static bool Matches(in LastShiftGalleryBranch branch, LastShiftCompartment compartment,
            LastShiftDoorPlane plane, float faceCoordinate) =>
            branch.Compartment == compartment && branch.DoorPlane == plane &&
            Mathf.Abs(branch.DoorPlaneCoordinate - faceCoordinate) < 0.001f &&
            IsPassable(branch);

        /// <summary>
        /// 회랑 다리가 구획 볼륨을 침범하는가. 맞닿는 면은 침범이 아니다 —
        /// 분기와 종점은 언제나 한 면을 공유한다.
        /// </summary>
        public static bool LegOverlapsCompartment(in LastShiftGalleryLeg leg, in LastShiftCompartmentSpec spec) =>
            leg.MinX < spec.MaxX - Epsilon && spec.MinX < leg.MaxX - Epsilon &&
            leg.MinZ < spec.MaxZ - Epsilon && spec.MinZ < leg.MaxZ - Epsilon;

        /// <summary>회랑이 선체 내부(방·통로가 타일링한 영역)를 침범하는가.</summary>
        public static bool LegOverlapsHullInterior(in LastShiftGalleryLeg leg) =>
            leg.MinX < LastShiftShipDimensions.HalfLength - Epsilon &&
            -LastShiftShipDimensions.HalfLength < leg.MaxX - Epsilon &&
            leg.MinZ < LastShiftShipDimensions.HalfWidth - Epsilon &&
            -LastShiftShipDimensions.HalfWidth < leg.MaxZ - Epsilon;

        /// <summary>
        /// 고리 한 바퀴의 통행 거리 — 격납고 끝벽에서 구명정 옆면까지 회랑만 타고 간 길이다.
        /// 주 통로(스파인)와 비교해 이 길이 지름길이 아닌지 재는 값이고, 테스트가 그 성질을
        /// 고정한다. 회랑이 스파인보다 짧아지면 방들이 아니라 회랑이 배의 주 동선이 된다.
        /// </summary>
        public static float TravelDistance =>
            Mathf.Abs(RunMaxX - RunMinX) + Mathf.Abs(NearZ - DescentEndZ);

        private const float Epsilon = 0.0001f;
    }
}
