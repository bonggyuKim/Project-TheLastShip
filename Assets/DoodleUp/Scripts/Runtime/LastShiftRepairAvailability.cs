namespace DoodleUp.Runtime
{
    /// <summary>
    /// <c>RG-2</c> — 수리 동사는 전력을 전제하지 않는다(기획 §4.3).
    ///
    /// 기획이 "가장 중요한 규칙" 이라고 못박은 항목이며, 배터리 삽입·냉각통 연결·파공 봉합은
    /// 모두 손으로 하는 물리 작업이라 <c>BusPower</c> 가 <c>0</c> 이어도 실행된다. 전력은 회복
    /// <b>속도</b>만 바꾼다(재가압 정지, 조명 소실).
    ///
    /// <b>이 규칙을 주석이 아니라 함수로 두는 이유.</b> "전력이 없으면 전력을 복구할 수 없다" 는
    /// 순환은 코드 한 줄로 생기고, 생긴 뒤에는 그 조합에 실제로 도달해 봐야만 드러난다.
    /// 규칙을 함수로 만들어 두면 <c>RG-4</c> 전수 검증이 1,440 조합 전부에서 이 성질을 직접
    /// 물어볼 수 있고, 누가 나중에 전력 게이트를 넣는 순간 테스트가 잡는다.
    ///
    /// 여기서 다루는 것은 <b>물리적 실행 가능성</b>이지 물건이 손 닿는 곳에 있는지가 아니다.
    /// 물건 위치는 씬 배치 문제이고 <c>CT-01 §3.3</c>(물건 최대 이동 거리) 소관이다.
    /// </summary>
    public static class LastShiftRepairAvailability
    {
        /// <summary>
        /// 이 계통의 수리 동사를 지금 실행할 수 있는가.
        ///
        /// 이미 복구된 계통은 <c>false</c> 다 — 할 일이 없다는 뜻이지 막혔다는 뜻이 아니다.
        /// 그 구분은 호출부가 <see cref="NeedsRepair"/> 로 한다.
        ///
        /// <b>인자에 <see cref="LastShiftShipState"/> 를 받으면서 <c>BusPower</c> 를 읽지 않는 것이
        /// 이 함수의 요점이다.</b> 전력을 안 받으면 규칙이 자명해 보이지만, 그러면 나중에 전력
        /// 게이트를 넣으려는 사람이 인자를 늘리는 것으로 시작하게 되고 그건 리뷰에서 눈에 띈다.
        /// </summary>
        public static bool IsExecutable(LastShiftShipSystem system, in LastShiftContainment containment)
        {
            return NeedsRepair(system, containment);
        }

        /// <summary>이 계통이 아직 복구되지 않았는가. 성능 포기는 복구가 아니다.</summary>
        public static bool NeedsRepair(LastShiftShipSystem system, in LastShiftContainment containment)
        {
            return system switch
            {
                LastShiftShipSystem.Cooling => !containment.CoolingRestored,
                LastShiftShipSystem.Power => !containment.PowerRestored,
                _ => !containment.OxygenRestored
            };
        }

        /// <summary>
        /// 지금 실행 가능한 수리 동사가 하나라도 있는가 — <c>RG-4</c> 검증 항목 (1).
        /// 고칠 것이 하나도 없으면 <c>false</c> 이며, 그건 막힌 상태가 아니라 온전한 배다.
        /// </summary>
        public static bool AnyExecutable(in LastShiftContainment containment)
        {
            for (var index = 0; index < LastShiftSystemMap.SystemCount; index++)
                if (IsExecutable((LastShiftShipSystem)index, containment))
                    return true;
            return false;
        }

        /// <summary>
        /// 손상된 계통 중 수리 동사가 막힌 것이 하나라도 있는가. <c>RG-2</c> 위반의 관측 형태이며
        /// 항상 <c>false</c> 여야 한다 — 손상됐는데 못 고치는 계통이 있으면 그게 순환이다.
        /// </summary>
        public static bool AnyBlocked(in LastShiftContainment containment)
        {
            for (var index = 0; index < LastShiftSystemMap.SystemCount; index++)
            {
                var system = (LastShiftShipSystem)index;
                if (NeedsRepair(system, containment) && !IsExecutable(system, containment))
                    return true;
            }
            return false;
        }
    }
}
