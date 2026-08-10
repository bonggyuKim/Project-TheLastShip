namespace DoodleUp.Runtime
{
    /// <summary>
    /// 거점 배치 동사의 <b>권위 있는 몸통</b> — 확정과 해제.
    /// <see cref="LastShiftPlacementCommands"/> 와 같은 자리에 같은 이유로 있다: 화면이 판정을
    /// 다시 하기 시작하면 지불 순서가 두 벌이 되고, 그 어긋남은 <b>잔액이 서서히 갈리는 형태</b>
    /// 로만 드러난다.
    ///
    /// <b>결과 형은 선체와 공유한다</b>(<see cref="LastShiftPlacementOutcome"/>). 사유 플래그가
    /// 같으므로 화면 문구를 내는 함수도 하나다 — 두 탭이 같은 거부를 다른 문장으로 적으면
    /// "같은 손동작" (§5.1)이 화면에서 먼저 깨진다.
    ///
    /// <b>여력을 한 줄도 안 부른다</b>(조항 <c>O-2</c>). 이 파일에 <c>LastShiftMaintenance</c> 가
    /// 나오면 그 순간 거점이 두 자원을 같이 요구하는 항목이 된다.
    ///
    /// <b>아직 서버 몸통이 없다.</b> 선체 배치는 <see cref="LastShiftNetworkPlacement"/> 가 표와
    /// 여력 원장을 복제하지만, <b>자재 원장(<see cref="LastShiftMaterials"/>)은 아직 복제 경로가
    /// 없다</b> — 선외 파밍 자체가 프로세스 안에서만 돈다
    /// (<see cref="LastShiftMaterials.ApplyNetworkLedger"/> 를 부르는 자리가 아직 하나도 없다).
    /// 거점만 먼저 복제하면 <b>자재는 각자 세고 골조는 공유하는</b> 상태가 되어, 값을 못 낸
    /// 클라이언트에도 골조가 서고 잔액만 갈린다. 복제는 파밍 쪽이 서버 권위로 옮겨오는 카드와
    /// 같이 붙어야 한다 — 그때 이 파일은 안 바뀌고 수신부만 이 함수를 부른다.
    /// </summary>
    public static class LastShiftOutpostCommands
    {
        /// <summary>
        /// 커서가 물고 있는 자리를 확정한다.
        ///
        /// <b>순서가 규약이다</b> — 값을 낼 수 있는지 먼저 묻고, 표에 들어간 것을 보고 나서 문다.
        /// 뒤집으면 판정에 걸린 배치가 자재만 태운다.
        /// </summary>
        public static bool TryPlace(LastShiftOutpostCursor cursor, out LastShiftPlacementOutcome outcome)
        {
            if (cursor == null || !LastShiftMaterials.CanAfford(cursor.Kind.MaterialCost))
            {
                outcome = LastShiftPlacementOutcome.Rejected(LastShiftPlacementCommandResult.Unaffordable);
                return false;
            }

            var cost = cursor.Kind.MaterialCost;
            if (!cursor.TryCommit(cost, out var index, out var rejection))
            {
                outcome = new LastShiftPlacementOutcome(
                    LastShiftPlacementCommandResult.Rejected, -1, 0, 0, rejection, cursor.Faults);
                return false;
            }

            LastShiftMaterials.TrySpend(cost);

            outcome = new LastShiftPlacementOutcome(
                LastShiftPlacementCommandResult.Accepted, index, cost, 0);
            return true;
        }

        /// <summary>
        /// 마지막에 세운 것을 뺀다.
        ///
        /// <b>전액 환수다.</b> 선체 쪽 조항 <c>M-4</c>(출항 뒤에는 절반)에 해당하는 조문이 자재
        /// 축에는 아직 없다 — <c>docs/outboard-outpost-and-map-final-v1.md</c> §4.3 이 확장 값을
        /// 통째로 <c>game-balance</c> 로 넘겼고, 환수율은 그 표가 서기 전에는 잴 대상이 없다.
        /// 그때까지는 <b>실수를 되돌리는 것을 안 막는 쪽</b>으로 틀린다: 목록이 한 종이고 뿌리도
        /// 하나라, 지금 감가를 붙이면 잘못 댄 골조 하나가 그 기항을 통째로 끝낸다.
        /// </summary>
        public static bool TryRemoveLast(out LastShiftPlacementOutcome outcome)
        {
            if (LastShiftOutpost.PieceCount <= 0)
            {
                outcome = LastShiftPlacementOutcome.Rejected(LastShiftPlacementCommandResult.NothingToRemove);
                return false;
            }

            if (!LastShiftOutpost.TryRemoveLast(out var refunded))
            {
                outcome = LastShiftPlacementOutcome.Rejected(LastShiftPlacementCommandResult.HasChildren);
                return false;
            }

            LastShiftMaterials.Refund(refunded);

            outcome = new LastShiftPlacementOutcome(
                LastShiftPlacementCommandResult.Accepted, -1, 0, refunded);
            return true;
        }

        /// <summary>
        /// 커서 하나를 요청 값으로 세운다. <see cref="LastShiftPlacementCommands.CursorFor"/> 와
        /// 같은 규약이다 — 치수는 안 받고 서버가 자기 카탈로그에서 읽는다. 복제 경로가 붙는 날
        /// 수신부가 부를 문이고, 지금은 테스트가 커서를 좌표로 세우는 데 쓴다.
        /// </summary>
        public static LastShiftOutpostCursor CursorFor(
            int catalogIndex, int quarterTurns, float anchorX, float anchorZ)
        {
            var cursor = new LastShiftOutpostCursor();
            cursor.Select(catalogIndex);
            cursor.Rotate(quarterTurns);
            cursor.MoveAnchorTo(new UnityEngine.Vector3(anchorX, 0f, anchorZ));
            return cursor;
        }
    }
}
