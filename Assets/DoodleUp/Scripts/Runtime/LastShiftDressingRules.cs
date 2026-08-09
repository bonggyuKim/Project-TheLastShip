using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>위반 하나. 규칙 id 를 같이 들고 다녀 로그만 보고도 어느 제약인지 갈린다.</summary>
    public readonly struct LastShiftDressingViolation
    {
        public LastShiftDressingViolation(string rule, string propId, LastShiftDressingSpace space, string message)
        {
            Rule = rule;
            PropId = propId;
            Space = space;
            Message = message;
        }

        public string Rule { get; }
        public string PropId { get; }
        public LastShiftDressingSpace Space { get; }
        public string Message { get; }

        public override string ToString() => $"[{Rule}] {Space}/{PropId}: {Message}";
    }

    /// <summary>
    /// 브리프 4대 제약의 기계 검사. <b>이 파일이 이 카드의 핵심이다</b> — 제약을 문서에만
    /// 두면 지키는지 여부가 사람의 기억에 달리고, 드레싱은 앞으로 계속 늘어난다.
    ///
    /// 검사는 순수 계산이라 씬도 에디터도 필요 없다. EditMode 테스트가 직접 부르고,
    /// 씬 빌더도 세우기 <b>전에</b> 부른다 — 세운 다음 재면 위반 씬이 한 번은 저장된다.
    ///
    /// <b>기하가 아니라 선언을 본다.</b> 상자가 게이지인지 아닌지는 좌표로 알 수 없어서,
    /// 데이터를 넣는 사람이 <see cref="LastShiftDressingSemantics"/> 로 선언하고 검증기는
    /// 그 선언이 놓인 자리와 맞는지만 판정한다. 선언을 빠뜨리면 검사가 안 걸리는 것은
    /// 맞지만, 그건 "게이지를 게이지라고 안 적었다" 는 문제라 리뷰가 잡을 수 있다 —
    /// 반대로 기하로 추측하게 만들면 오탐이 쌓여 검사 자체가 꺼진다.
    /// </summary>
    public static class LastShiftDressingRules
    {
        /// <summary>제약 1. 상태 반응 단서가 넘으면 안 되는 z. §19.7 인계값 그대로다.</summary>
        public const float StateCueSafeMaxZ = LastShiftDressing.StateCueSafeMaxZ;

        /// <summary>
        /// 제약 4. 우회 통로 발광체 밝기 합. 넘으면 관이 밝아져 "여기 있고 싶지 않다" 가
        /// 사라진다 — §5 는 이 통로를 산소를 태워서라도 쓰는 비상용으로 설계했고,
        /// 쾌적해지는 순간 평상시 최단경로가 되어 갑판 위 통로 설계가 통째로 무의미해진다.
        ///
        /// <b>지금 든 것이 거의 다 찬 값이다.</b> 관 안에는 바닥 유도띠 두 줄(본선·선수 다리,
        /// 각 0.8)뿐이고 합이 1.6 이라, 남은 0.4 는 에어록 진입 표시 정도만 들어간다.
        /// 여유를 크게 잡으면 예산이 아니라 장식이 된다 — 이 상한의 목적은 "더 밝히려면
        /// 무엇을 뺄지 먼저 정하라" 이지 특정 밝기를 맞추는 것이 아니다.
        /// </summary>
        public const float BypassLightBudget = 2.0f;

        /// <summary>
        /// 제약 4-b. 에어록 발광체 밝기 합. <b>관과 따로 센다</b> — 같은 비상 경로지만
        /// 요구가 반대다. 관은 어두워야 "여기 있고 싶지 않다" 가 서고, 에어록은 진입 지점이라
        /// 무엇을 조작하는지 보여야 한다. 관 예산 하나로 묶으면 둘 중 하나가 반드시 진다.
        ///
        /// <b>값의 근거는 art §3.3 이 이미 적어 둔 에어록 등기구 둘이다.</b>
        /// 상시 등 <c>LSDress_Lamp_Airlock</c> 이 <c>4.57</c>(목표 `200 lx`, `2700K` 경고 톤),
        /// 사고 시 켜지는 <c>LSDress_EmergencyStrobe</c> 가 <c>1.10</c> 이다. 합 <c>5.67</c> 을
        /// 올려 <c>5.7</c> 로 둔다.
        ///
        /// 스트로브가 상시 점등이 아닌데도 합에 넣는 이유는, 그게 켜지는 순간이 곧
        /// <b>둘이 동시에 켜져 있는 순간</b>이라서다. 예산은 평균이 아니라 최악을 막는 값이다.
        /// 대신 이 상한은 지금 든 것으로 꽉 찬다 — 여유가 없는 것이 의도다(<see cref="BypassLightBudget"/>
        /// 와 같은 이유). 경고등을 더 달려면 무엇을 뺄지 먼저 정해야 한다.
        ///
        /// <b>쾌적 설비 금지는 이 예산과 별개로 그대로 걸린다</b>(<c>C4_BypassComfort</c> 는
        /// 관과 에어록 양쪽을 본다). 밝아도 되는 것과 편해도 되는 것은 다른 문제다.
        /// </summary>
        public const float AirlockLightBudget = 5.7f;

        /// <summary>
        /// 제약 3의 예외를 쓸 수 있는 방. 브리프 §1.3·§4.2·§4.3·§5.3·§6.2 가 넷(수경재배·
        /// 서버통신실·의무실·구명정)의 이름을 댔었다.
        ///
        /// <b>M-2 에서 대상이 <c>0</c> 이 됐다.</b> 넷 중 셋은 자유 배치 카탈로그로 갔고
        /// 하나(구명정)는 배에서 제거됐다(맵 개편 §3.2). 남은 고정 방은 숙소뿐이고, 숙소는
        /// 브리프가 예외로 지명한 적이 없다 — 침상·위생·휴게에 계기가 붙을 이유가 없다.
        ///
        /// <b>함수를 안 지운다.</b> 예외를 <b>방 이름으로 못 박는다</b>는 규칙 자체가 제약 3의
        /// 실체이고, 지우면 "이것도 그 방 고유 시스템" 이라는 말로 계기를 다는 것을 막는
        /// 자리가 사라진다. 모듈에 고유 계기를 허용할지는 <c>game-planning</c> 결정이 있어야
        /// 열리는 문이다 — 그때 여기 조건이 다시 붙는다.
        /// </summary>
        public static bool AllowsRoomSystemReadout(LastShiftCompartment compartment) => false;

        private const float Epsilon = 0.001f;

        public static List<LastShiftDressingViolation> Validate(IReadOnlyList<LastShiftDressingProp> props)
        {
            var violations = new List<LastShiftDressingViolation>();
            if (props == null) return violations;

            var seen = new HashSet<string>();
            var bypassLight = 0f;
            var airlockLight = 0f;

            foreach (var prop in props)
            {
                if (prop == null) continue;

                CheckIdentity(prop, seen, violations);
                CheckBounds(prop, violations);
                CheckExposureCone(prop, violations);
                CheckHatchMarker(prop, violations);
                CheckGaugeAndSiren(prop, violations);
                CheckReadoutException(prop, violations);
                CheckBypassComfort(prop, violations);
                CheckLightDeclaration(prop, violations);

                if (!Has(prop, LastShiftDressingSemantics.LightSource)) continue;

                // 우회 경로의 두 공간은 밝기를 따로 센다. 근거는 AirlockLightBudget 주석에 있다.
                if (prop.space.kind == LastShiftDressingSpaceKind.BypassRun)
                    bypassLight += Mathf.Max(0f, prop.lightIntensity);
                else if (prop.space.kind == LastShiftDressingSpaceKind.AirlockBranch)
                    airlockLight += Mathf.Max(0f, prop.lightIntensity);
            }

            if (bypassLight > BypassLightBudget + Epsilon)
                violations.Add(new LastShiftDressingViolation("C4_BypassLightBudget", "*",
                    LastShiftDressingSpace.OfBypassRun(),
                    $"우회 통로 발광체 밝기 합이 {bypassLight:0.##} 로 예산 {BypassLightBudget} 을 넘는다 — " +
                    "관이 밝아지면 §5 가 설계한 '불편해서 평소엔 안 쓰는 길' 이 최단경로가 된다."));

            if (airlockLight > AirlockLightBudget + Epsilon)
                violations.Add(new LastShiftDressingViolation("C4_AirlockLightBudget", "*",
                    LastShiftDressingSpace.OfAirlock(),
                    $"에어록 발광체 밝기 합이 {airlockLight:0.##} 로 예산 {AirlockLightBudget} 을 넘는다 — " +
                    "에어록은 조작을 보여야 하므로 관보다 밝아도 되지만, 상한이 없으면 " +
                    "비상 경로에서 여기만 생활 공간처럼 밝아진다."));

            CheckDoorwayClearance(props, violations);

            return violations;
        }

        /// <summary>
        /// 제약 5 — 문 통행 폭. <b>소품 하나씩이 아니라 문 하나씩 본다</b>: 상자 둘이 각각
        /// 구멍의 반대쪽 끝을 조금씩 물면 하나씩 재서는 둘 다 통과하고, 실제로는 가운데
        /// 한 토막만 남는다. 그래서 무는 구간을 문마다 모아 합친 뒤 가장 긴 빈 토막을 잰다.
        ///
        /// 2026-08-08 플레이테스트가 이 검사를 요구했다(카드 955678c7) — 냉각실
        /// <c>CrateStack_Aft</c> 가 냉각실↔통로B 문을 통째로 물어 산소실 쪽으로 갈 길이
        /// 아예 없었고, 선수·선미 끝벽 문도 <c>0.95m</c>·<c>0.80m</c> 로 눌려 있었다.
        /// 좌표는 전부 맞았고 <b>소품만 잘못 놓여 있었다</b> — 기존 검사 넷 중 어느 것도
        /// 통행을 안 보고 있었기 때문에 씬 빌드도 테스트도 통과했다.
        /// </summary>
        private static void CheckDoorwayClearance(IReadOnlyList<LastShiftDressingProp> props,
            List<LastShiftDressingViolation> violations)
        {
            var spans = new List<Vector2>();
            var blockers = new List<LastShiftDressingProp>();

            foreach (var door in LastShiftDoorways.All)
            {
                spans.Clear();
                blockers.Clear();

                foreach (var prop in props)
                {
                    if (prop == null) continue;
                    var center = LastShiftDressingSpaces.WorldCenter(prop);
                    if (!LastShiftDoorways.Intrudes(door, center, prop.Size,
                            LastShiftDressingSpaces.BottomY(prop), out var span)) continue;

                    spans.Add(span);
                    blockers.Add(prop);
                }

                if (blockers.Count == 0) continue;

                var clear = LastShiftDoorways.ClearWidth(door, spans);
                if (clear >= LastShiftDoorways.MinClearWidth - Epsilon) continue;

                // 위반은 문 하나에 한 줄이다. 소품마다 찍으면 문이 완전히 막힌 사고와
                // 여럿이 조금씩 좁힌 사고가 로그에서 같은 무게로 보인다.
                var names = string.Join(", ", blockers.ConvertAll(p => p.id));
                violations.Add(new LastShiftDressingViolation("C5_DoorwayClearance", names,
                    blockers[0].space,
                    $"{door.Name} 문에 남은 통행 폭이 {clear:0.##}m 로 " +
                    $"최소 {LastShiftDoorways.MinClearWidth}m 아래다 — 소품({names})을 문 앞 " +
                    $"{LastShiftDoorways.ApproachDepth}m 밖으로 빼야 승무원이 지나간다."));
            }
        }

        private static bool Has(LastShiftDressingProp prop, LastShiftDressingSemantics flag) =>
            (prop.semantics & flag) != 0;

        private static void CheckIdentity(LastShiftDressingProp prop, HashSet<string> seen,
            List<LastShiftDressingViolation> violations)
        {
            if (string.IsNullOrWhiteSpace(prop.id))
            {
                violations.Add(new LastShiftDressingViolation("R0_Id", "(빈 이름)", prop.space,
                    "이름이 비었다 — 씬 하이어라키에서 어느 소품인지 못 짚는다."));
                return;
            }

            // 유일성은 <b>공간 안에서만</b> 요구한다. 소품은 공간별 루트 아래에 붙으므로
            // 정비창 Bench_Port 와 휴게실 Bench_Port 는 하이어라키에서 안 겹치고, 위반
            // 로그도 공간을 같이 찍는다. 전역 유일을 요구하면 방 이름을 접두어로 달아야
            // 하고, 그러면 씬 하이어라키가 Workshop/Workshop_Bench_Port 로 두 번 말한다.
            if (!seen.Add($"{prop.space}/{prop.id}"))
                violations.Add(new LastShiftDressingViolation("R0_Id", prop.id, prop.space,
                    "이름이 중복이다."));
        }

        private static void CheckBounds(LastShiftDressingProp prop, List<LastShiftDressingViolation> violations)
        {
            var bounds = LastShiftDressingSpaces.BoundsOf(prop.space);
            var center = LastShiftDressingSpaces.WorldCenter(prop);

            // x·z 는 중심만 본다. 벽에 박히는 트림·띠는 상자가 경계를 넘는 것이 정상이라
            // 상자 전체를 재면 정상 소품이 전부 걸린다.
            if (center.x < bounds.MinX - Epsilon || center.x > bounds.MaxX + Epsilon)
                violations.Add(new LastShiftDressingViolation("R1_Bounds", prop.id, prop.space,
                    $"중심 x={center.x:0.##} 가 공간 x 범위 [{bounds.MinX:0.##}, {bounds.MaxX:0.##}] 밖이다."));

            if (center.z < bounds.MinZ - Epsilon || center.z > bounds.MaxZ + Epsilon)
                violations.Add(new LastShiftDressingViolation("R1_Bounds", prop.id, prop.space,
                    $"중심 z={center.z:0.##} 가 공간 z 범위 [{bounds.MinZ:0.##}, {bounds.MaxZ:0.##}] 밖이다."));

            // y 는 상자 전체를 본다. 갑판을 뚫고 내려간 소품이나 천장을 뚫고 올라간 소품은
            // 벽 트림과 달리 변명의 여지가 없다 — 아래는 우회 통로, 위는 다음 층이다.
            if (LastShiftDressingSpaces.BottomY(prop) < bounds.FloorY - Epsilon)
                violations.Add(new LastShiftDressingViolation("R1_Bounds", prop.id, prop.space,
                    $"밑면이 바닥 {bounds.FloorY:0.##} 아래로 내려간다."));

            if (LastShiftDressingSpaces.TopY(prop) > bounds.CeilingY + Epsilon)
                violations.Add(new LastShiftDressingViolation("R1_Bounds", prop.id, prop.space,
                    $"윗면이 천장 {bounds.CeilingY:0.##} 를 뚫는다."));
        }

        /// <summary>
        /// 제약 1 — 노출 원뿔. 냉각실·전력실의 <b>상태에 반응하는</b> 단서만 대상이다.
        /// 열교환기나 배전반처럼 늘 같은 모습인 설비는 원뿔 안에 있어도 새는 정보가 없다.
        /// </summary>
        private static void CheckExposureCone(LastShiftDressingProp prop, List<LastShiftDressingViolation> violations)
        {
            if (!Has(prop, LastShiftDressingSemantics.StateResponsive)) return;
            if (prop.space.kind != LastShiftDressingSpaceKind.Zone) return;
            if (prop.space.zone is not (LastShiftZone.Power or LastShiftZone.Cooling)) return;

            // <b>기준이 절대 z 밴드에서 문 쐐기로 바뀌었다.</b> 옛 상한
            // (<see cref="LastShiftDressing.StateCueSafeMaxZ"/> = 1.40)은 전력실↔냉각실
            // <b>방-방 개구부</b>의 노출 원뿔에서 나온 값인데, §3.4 가 그 개구부를 폐지했다 —
            // 두 방은 이제 서로 안 닿고 각자 광장에만 문이 있다. 그 상한을 그대로 두면 두 방이
            // z 6~11 대역에 있으므로 상태 단서를 <b>어디에도</b> 못 놓는다.
            //
            // 남은 실제 요건은 §4 그대로다: 광장에 선 사람이 방 안 상태를 읽으면 그것이
            // 게이지 하나 몫을 더 한다. 그래서 문 구멍이 만드는 쐐기로 잰다 — 구멍 폭 밖으로
            // 비켜난 자리(방 옆벽 쪽)면 광장에서 그 소품을 지나는 직선이 없다.
            var door = LastShiftPlazaLayout.DoorOf(LastShiftPlazaLayout.RoomOf(prop.space.zone));
            var half = LastShiftZoneDoor.OpeningWidth * 0.5f;
            var propMin = door.PlaneIsX ? LastShiftDressingSpaces.MinZ(prop) : LastShiftDressingSpaces.MinX(prop);
            var propMax = door.PlaneIsX ? LastShiftDressingSpaces.MaxZ(prop) : LastShiftDressingSpaces.MaxX(prop);

            if (propMax <= door.Center - half + Epsilon || propMin >= door.Center + half - Epsilon) return;

            violations.Add(new LastShiftDressingViolation("C1_ExposureCone", prop.id, prop.space,
                $"상태 단서가 {prop.space.zone} 문 구멍 정면([{door.Center - half:0.##}, {door.Center + half:0.##}])에 " +
                "걸쳐 광장에서 읽힌다 — 게이지가 없어도 사실상 세 번째 게이지가 된다(§4). " +
                "문 정면 쐐기를 벗어난 방 옆벽 쪽으로 옮겨야 한다."));
        }

        /// <summary>
        /// 제약 2 — 해치 표식 금지. 언락 전 구획은 표식·조명 신호 없이 그냥 막힌 판이다(§21.4).
        /// 처음부터 열려 있는 구명정에는 걸리지 않는다 — 거기 표식은 잠금을 안 흘린다.
        /// </summary>
        private static void CheckHatchMarker(LastShiftDressingProp prop, List<LastShiftDressingViolation> violations)
        {
            if (!Has(prop, LastShiftDressingSemantics.HatchMarker)) return;

            if (prop.space.kind != LastShiftDressingSpaceKind.Compartment)
            {
                violations.Add(new LastShiftDressingViolation("C2_HatchMarker", prop.id, prop.space,
                    "해치 표식은 구획에만 붙을 수 있다 — 통로·덕트에 붙은 표식은 어느 문을 " +
                    "가리키는지가 데이터에 안 남아 언락 상태를 흘리는지 판정할 수 없다."));
                return;
            }

            // 판정 기준은 <b>공간</b>이 잠겼는지다. 구명정은 기능만 잠겨 있고 휴게실에서
            // 걸어 들어가므로(§15.4) 표식이 있어도 흘릴 언락 상태가 없다 — 브리프 §4.3 이
            // 이 방을 원칙 적용 대상에서 뺀 이유가 그것이다.
            var access = LastShiftCompartments.Of(prop.space.compartment).Access;
            if (access == LastShiftCompartmentAccess.Locked)
                violations.Add(new LastShiftDressingViolation("C2_HatchMarker", prop.id, prop.space,
                    $"{LastShiftCompartments.NameOf(prop.space.compartment)} 는 언락 대상(access={access})이라 " +
                    "해치 표식을 달 수 없다 — 표식이 있으면 문 너머에 방이 있다는 것이 " +
                    "언락 전에 새고, §17.7-3/§17.8-4 미결이 그림에서 먼저 닫힌다(§21.4)."));
        }

        /// <summary>
        /// 제약 3 — 압력존 미편입 구획 열한 개에 게이지·사이렌 금지(§24).
        /// 시각 단서 자체는 허용이고, 압력 게이지와 전선 사이렌 <b>재사용</b>만 막는다.
        /// </summary>
        private static void CheckGaugeAndSiren(LastShiftDressingProp prop, List<LastShiftDressingViolation> violations)
        {
            if (prop.space.kind != LastShiftDressingSpaceKind.Compartment) return;

            if (Has(prop, LastShiftDressingSemantics.PressureGauge))
                violations.Add(new LastShiftDressingViolation("C3_NoGauge", prop.id, prop.space,
                    $"{LastShiftCompartments.NameOf(prop.space.compartment)} 는 압력존 미편입이라 " +
                    "압력 게이지를 둘 수 없다(§24) — 계기가 붙으면 이 방이 압력존이라는 " +
                    "뜻이 되고, 편입 여부가 아직 안 정해졌다."));

            if (Has(prop, LastShiftDressingSemantics.SirenEffect))
                violations.Add(new LastShiftDressingViolation("C3_NoSiren", prop.id, prop.space,
                    $"{LastShiftCompartments.NameOf(prop.space.compartment)} 에 전선 사이렌 이펙트를 " +
                    "재사용할 수 없다(§24)."));
        }

        /// <summary>
        /// 제약 3의 예외 관리. "그 방 고유 시스템의 표현" 은 허용이지만(브리프 §1.3),
        /// 예외가 무주공산이 되면 게이지 금지가 이름만 남는다 — 방을 못 박고 사유를 받는다.
        /// </summary>
        private static void CheckReadoutException(LastShiftDressingProp prop, List<LastShiftDressingViolation> violations)
        {
            if (!Has(prop, LastShiftDressingSemantics.RoomSystemReadout)) return;

            if (prop.space.kind == LastShiftDressingSpaceKind.Compartment &&
                !AllowsRoomSystemReadout(prop.space.compartment))
            {
                violations.Add(new LastShiftDressingViolation("C3_ReadoutRoom", prop.id, prop.space,
                    $"{LastShiftCompartments.NameOf(prop.space.compartment)} 는 브리프가 고유 시스템 " +
                    "표시를 인정한 방이 아니다 — 브리프가 이름을 댄 넷(수경재배·서버통신실·의무실·" +
                    "구명정)은 M-2 에서 전부 배를 떠났으므로 지금 이 예외를 쓸 수 있는 방은 없다."));
            }

            if (string.IsNullOrWhiteSpace(prop.justification))
                violations.Add(new LastShiftDressingViolation("C3_ReadoutReason", prop.id, prop.space,
                    "고유 시스템 표시는 사유를 적어야 한다 — 게이지처럼 읽히는지는 씬이 나온 뒤 " +
                    "체감 판정할 항목이라(브리프 §8.2), 그때 무엇을 근거로 넣었는지가 남아 있어야 한다."));
        }

        /// <summary>
        /// 제약 4의 보조 — <b>장부와 화면이 갈라지지 않게 한다.</b>
        ///
        /// <see cref="LastShiftDressingProp.lightIntensity"/> 는 예산 집계용 숫자이고 실제로
        /// 화면을 밝히는 것은 프리팹의 <see cref="Light"/> 다. 둘이 따로 놀면 예산은 통과하는데
        /// 관은 그대로 밝은 상태가 되고, 그때 예산은 설계를 지키는 게 아니라 지킨다고 적힌
        /// 종이가 된다. 실제로 그런 적이 있다 — 바닥 띠 둘의 합을 1.6 에서 1.4 로 내렸지만
        /// 그 둘은 발광 재질이라 화면 밝기는 그대로였다.
        ///
        /// 그래서 <b>프리팹이 Light 를 들고 있을 때만</b> 두 값을 맞춘다. 재질 발광은 여기서
        /// 재지 못하므로(셰이더·노출·톤매핑까지 봐야 한다) 규칙으로 만들지 않는다 — 못 재는
        /// 것을 재는 척하면 오탐이 쌓여 검사 자체가 꺼진다. 재질 발광만으로 밝히는 소품은
        /// 지금도 여섯 개 있고, 그건 규칙이 아니라 리뷰가 볼 몫이다.
        /// </summary>
        private static void CheckLightDeclaration(LastShiftDressingProp prop,
            List<LastShiftDressingViolation> violations)
        {
            if (prop.prefab == null) return;

            var lights = prop.prefab.GetComponentsInChildren<Light>(true);
            if (lights == null || lights.Length == 0) return;

            var actual = 0f;
            foreach (var light in lights)
                if (light != null) actual += Mathf.Max(0f, light.intensity);

            if (!Has(prop, LastShiftDressingSemantics.LightSource))
            {
                violations.Add(new LastShiftDressingViolation("C4_LightUndeclared", prop.id, prop.space,
                    $"프리팹 {prop.prefab.name} 이 Light {lights.Length}개(합 {actual:0.##})를 들고 있는데 " +
                    "LightSource 로 선언하지 않았다 — 선언이 없으면 밝기 예산이 이 빛을 못 본다."));
                return;
            }

            if (Mathf.Abs(prop.lightIntensity - actual) > Epsilon)
                violations.Add(new LastShiftDressingViolation("C4_LightMismatch", prop.id, prop.space,
                    $"선언한 밝기 {prop.lightIntensity:0.##} 가 프리팹 실제 합 {actual:0.##} 와 다르다 — " +
                    "예산은 선언을 세므로, 어긋난 채 두면 장부만 줄이고 화면은 그대로인 상태가 된다."));
        }

        /// <summary>제약 4 — 우회 통로는 불편해야 한다(§5).</summary>
        private static void CheckBypassComfort(LastShiftDressingProp prop, List<LastShiftDressingViolation> violations)
        {
            if (prop.space.kind is not (LastShiftDressingSpaceKind.BypassRun or LastShiftDressingSpaceKind.AirlockBranch))
                return;

            if (Has(prop, LastShiftDressingSemantics.Comfort))
                violations.Add(new LastShiftDressingViolation("C4_BypassComfort", prop.id, prop.space,
                    "우회 통로에 쾌적 설비를 둘 수 없다 — 핸드레일·넓은 발판·밝은 띠가 붙는 순간 " +
                    "이 길은 비상용이 아니라 지름길이 되고, 산소를 태워 지나간다는 §5 의 " +
                    "비용 구조가 사라진다."));
        }
    }
}
