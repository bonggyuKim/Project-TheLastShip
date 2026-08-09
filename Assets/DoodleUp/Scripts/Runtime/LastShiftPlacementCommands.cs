namespace DoodleUp.Runtime
{
    /// <summary>
    /// 배치 요청이 어디서 걸렸는가. <b>문장이 아니라 값이다</b> — 서버가 판정하고 클라이언트가
    /// 적는 구조에서 결과를 문장으로 넘기면, 그 문장을 만든 쪽에만 숫자가 남고 받는 쪽은
    /// 그것을 다시 못 쓴다. 판정기가 거부 사유를 <b>플래그로</b> 돌려주는 것과 같은 이유다.
    /// </summary>
    public enum LastShiftPlacementCommandResult : byte
    {
        Accepted,

        /// <summary>커서를 안 잡고 있다. 네트워크에서만 나온다(§12-9).</summary>
        NotCursorHolder,

        /// <summary>여력이 모자라거나 기항 밖이다.</summary>
        Unaffordable,

        /// <summary>판정기 또는 붙임 검사에 걸렸다. 사유는 <c>Rejection</c>·<c>Faults</c> 에 있다.</summary>
        Rejected,

        /// <summary>뺄 모듈이 없다.</summary>
        NothingToRemove,

        /// <summary>자식이 달려 못 뺀다 — 잎부터 빼야 한다.</summary>
        HasChildren
    }

    /// <summary>
    /// 배치 동사 하나가 남긴 것. 거부 사유를 플래그로 들고 있어서 <b>서버가 낸 판정을 그대로
    /// 클라이언트 화면 문구로 옮길 수 있다</b>(<see cref="LastShiftPlacementCommands.Reason"/>).
    /// </summary>
    public readonly struct LastShiftPlacementOutcome
    {
        public LastShiftPlacementOutcome(
            LastShiftPlacementCommandResult result, int index, int cost, int refunded,
            LastShiftPlacementRejection rejection = LastShiftPlacementRejection.None,
            LastShiftPlacementFault faults = LastShiftPlacementFault.None)
        {
            Result = result;
            Index = index;
            Cost = cost;
            Refunded = refunded;
            Rejection = rejection;
            Faults = faults;
            Verdict = default;
        }

        /// <summary>
        /// 판정 전체를 들고 있는 결과. <b>같은 프로세스에서만 나온다</b> — 화면이 "구역 · 깊이 ·
        /// 이탈" 을 적으려면 판정 구조체가 필요한데, 그걸 요청자에게 되돌리는 것은 요청 하나에
        /// 판정 전체를 실어 보내는 일이라 값이 안 맞는다. 원격 요청자는 모듈이 실제로 서는
        /// 것으로 결과를 본다(복제).
        /// </summary>
        public LastShiftPlacementOutcome(
            LastShiftPlacementCommandResult result, int index, int cost, int refunded,
            in LastShiftPlacementVerdict verdict, LastShiftPlacementFault faults)
        {
            Result = result;
            Index = index;
            Cost = cost;
            Refunded = refunded;
            Rejection = verdict.Rejection;
            Faults = faults;
            Verdict = verdict;
        }

        /// <summary>확정된 배치의 판정. 원격으로 건너온 결과에서는 기본값이다.</summary>
        public LastShiftPlacementVerdict Verdict { get; }

        public LastShiftPlacementCommandResult Result { get; }

        public bool Accepted => Result == LastShiftPlacementCommandResult.Accepted;

        /// <summary>표에 들어간 자리. 거부됐거나 철거면 <c>-1</c> 이다.</summary>
        public int Index { get; }

        /// <summary>실제로 빠져나간 여력.</summary>
        public int Cost { get; }

        /// <summary>철거로 돌아온 여력.</summary>
        public int Refunded { get; }

        public LastShiftPlacementRejection Rejection { get; }

        public LastShiftPlacementFault Faults { get; }

        /// <summary>화면에 적을 사유 한 줄.</summary>
        public string Message => Result switch
        {
            LastShiftPlacementCommandResult.Accepted => string.Empty,
            LastShiftPlacementCommandResult.NotCursorHolder => "다른 승무원이 배치 중이다",
            LastShiftPlacementCommandResult.Unaffordable => "여력이 모자란다",
            LastShiftPlacementCommandResult.NothingToRemove => "뺄 모듈이 없다",
            LastShiftPlacementCommandResult.HasChildren => "자식이 달린 모듈은 못 뺀다",
            _ => LastShiftPlacementCommands.Reason(Rejection, Faults)
        };

        public static LastShiftPlacementOutcome Rejected(LastShiftPlacementCommandResult result) =>
            new(result, -1, 0, 0);
    }

    /// <summary>
    /// 배치 동사의 <b>권위 있는 몸통</b> — 확정과 철거. 화면(<see cref="LastShiftPlacementUi"/>)과
    /// 서버 수신부(<see cref="LastShiftNetworkPlacement"/>)가 <b>같은 함수</b>를 부른다.
    ///
    /// <b>왜 가르는가.</b> 네트워크가 붙으면 같은 판정을 두 곳이 하게 된다 — 혼자 도는 화면과
    /// 클라이언트 요청을 받는 서버. 두 벌로 두면 여력을 무는 순서(표에 들어간 것을 보고 나서
    /// 문다)나 환수 순서 같은 규약이 한쪽에서만 지켜지고, 그 어긋남은 <b>잔액이 서서히 갈리는
    /// 형태</b>로만 드러나서 어느 쪽이 틀렸는지가 화면에 안 보인다.
    ///
    /// <b>씬을 모른다.</b> 조립(<see cref="LastShiftModuleAssembler.Rebuild"/>)과 벽뚫기는 부르는
    /// 쪽 몫이다 — 표가 바뀐 것과 씬이 다시 선 것은 다른 사건이고, 클라이언트는 표를 복제로
    /// 받은 뒤에 씬을 세운다.
    /// </summary>
    public static class LastShiftPlacementCommands
    {
        /// <summary>
        /// 커서가 물고 있는 자리를 확정한다.
        ///
        /// <b>순서가 규약이다</b> — 값을 낼 수 있는지 먼저 묻고(<see cref="LastShiftMaintenance.CanAfford"/>),
        /// 표에 들어간 것을 보고 나서 문다. 뒤집으면 판정에 걸린 배치가 여력만 태운다.
        /// </summary>
        public static bool TryPlace(LastShiftPlacementCursor cursor, out LastShiftPlacementOutcome outcome)
        {
            if (cursor == null || !LastShiftMaintenance.CanAfford(cursor.Kind.MaintenanceCost))
            {
                outcome = LastShiftPlacementOutcome.Rejected(LastShiftPlacementCommandResult.Unaffordable);
                return false;
            }

            var cost = cursor.Kind.MaintenanceCost;
            if (!cursor.TryCommit(out var index, out var verdict))
            {
                outcome = new LastShiftPlacementOutcome(
                    LastShiftPlacementCommandResult.Rejected, -1, 0, 0, verdict, cursor.Faults);
                return false;
            }

            LastShiftMaintenance.TryChargeModule(
                index - LastShiftCompartments.FixedCount, cursor.CatalogIndex, cost);

            outcome = new LastShiftPlacementOutcome(
                LastShiftPlacementCommandResult.Accepted, index, cost, 0,
                verdict, LastShiftPlacementFault.None);
            return true;
        }

        /// <summary>
        /// 마지막에 놓은 모듈을 뺀다. <b>잎부터 빼는 것은 표가 강제한다</b> —
        /// 자식이 달린 칸은 <see cref="LastShiftCompartments.TryRemove"/> 가 거부한다.
        /// </summary>
        public static bool TryRemoveLast(out LastShiftPlacementOutcome outcome)
        {
            if (LastShiftCompartments.ModuleCount <= 0)
            {
                outcome = LastShiftPlacementOutcome.Rejected(LastShiftPlacementCommandResult.NothingToRemove);
                return false;
            }

            var slot = LastShiftCompartments.ModuleCount - 1;
            if (!LastShiftCompartments.TryRemove(LastShiftCompartments.Count - 1))
            {
                outcome = LastShiftPlacementOutcome.Rejected(LastShiftPlacementCommandResult.HasChildren);
                return false;
            }

            LastShiftMaintenance.TryRefundModule(slot, out var refunded);

            outcome = new LastShiftPlacementOutcome(
                LastShiftPlacementCommandResult.Accepted, -1, 0, refunded);
            return true;
        }

        /// <summary>
        /// 커서 하나를 요청 값으로 세운다. <b>서버가 클라이언트 요청을 받는 자리다</b> —
        /// 요청에는 <c>카탈로그 번호 · 회전 · 최소 모서리 · 부모</c> 만 들어오고 발자국 치수는
        /// 서버가 자기 카탈로그에서 읽는다. 치수를 요청에 실으면 목록에 없는 크기의 방을
        /// 요청할 수 있고, 판정기는 겹침·사슬만 보므로 그 방은 통과한다.
        /// </summary>
        public static LastShiftPlacementCursor CursorFor(
            int catalogIndex, int quarterTurns, float anchorX, float anchorZ,
            int parentIndex, bool autoParent)
        {
            var cursor = new LastShiftPlacementCursor();
            cursor.Select(catalogIndex);
            cursor.Rotate(quarterTurns);
            cursor.MoveAnchorTo(new UnityEngine.Vector3(anchorX, 0f, anchorZ));
            if (autoParent) cursor.AttachAutomatically();
            else cursor.AttachTo(parentIndex);
            return cursor;
        }

        /// <summary>
        /// 왜 안 들어가는가. <b>사유를 다 적는다</b> — 하나만 적으면 그걸 고칠 때마다 다음
        /// 사유를 새로 만나고, 몇 개가 남았는지가 화면에서 안 보인다(판정기가 사유를 모아서
        /// 돌려주는 것과 같은 이유다).
        /// </summary>
        public static string Reason(in LastShiftPlacementVerdict verdict, LastShiftPlacementFault faults) =>
            Reason(verdict.Rejection, faults);

        /// <summary>
        /// 판정 결과 대신 <b>사유 플래그만</b> 받는 문. 네트워크로 건너온 거부는 판정 구조체가
        /// 아니라 플래그 둘로 오므로(<see cref="LastShiftPlacementOutcome"/>), 같은 문구를 두 번
        /// 적지 않으려면 이쪽이 몸통이어야 한다.
        /// </summary>
        public static string Reason(
            LastShiftPlacementRejection rejection, LastShiftPlacementFault faults)
        {
            var text = string.Empty;

            void Add(string reason) => text = text.Length == 0 ? reason : text + " · " + reason;

            if ((rejection & LastShiftPlacementRejection.OverlapsPlacement) != 0) Add("다른 방과 겹친다");
            if ((rejection & LastShiftPlacementRejection.OverlapsHullInterior) != 0) Add("선체를 파고든다");
            if ((rejection & LastShiftPlacementRejection.ChainBroken) != 0) Add("선체까지 사슬이 안 닿는다");
            if ((rejection & LastShiftPlacementRejection.ChainTooDeep) != 0) Add("사슬이 너무 깊다");
            if ((rejection & LastShiftPlacementRejection.EgressOverLimit) != 0) Add("이탈 한도를 넘는다");

            if ((faults & LastShiftPlacementFault.ParentMissing) != 0) Add("붙을 상대가 없다");
            if ((faults & LastShiftPlacementFault.DoorOffOwnFace) != 0) Add("문이 자기 벽에서 벗어났다");
            if ((faults & LastShiftPlacementFault.DoorOffParentFace) != 0) Add("문이 벽에 안 닿는다");
            if ((faults & LastShiftPlacementFault.DoorOutsideParentSpan) != 0) Add("문이 벽 끝을 지나쳤다");

            return text.Length == 0 ? "배치 가능" : text;
        }
    }
}
