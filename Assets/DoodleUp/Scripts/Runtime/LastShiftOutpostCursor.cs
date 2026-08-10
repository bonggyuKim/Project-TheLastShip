using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 거점 배치 커서. <see cref="LastShiftPlacementCursor"/> 와 <b>같은 손동작</b>이다 —
    /// <c>1m</c> 격자로 옮기고 <c>90°</c> 4단으로 돌리며 확정 전에 판정을 물어 둔다. 튜토리얼이
    /// 거점에서 조작을 가르치고 선체에서 규칙을 가르치는 순서(§5.1)가 성립하려면 두 커서가
    /// 한 글자도 다르게 움직이면 안 된다.
    ///
    /// <b>다른 것은 셋뿐이다.</b> 보는 표가 <see cref="LastShiftOutpost"/> 이고, 목록이
    /// <see cref="LastShiftOutpostCatalog"/> 이며, 부모 찾기에 <b>선체 갈래가 없다</b>
    /// (<see cref="LastShiftModuleAttachment.TryResolveParentWithin"/>).
    ///
    /// <b>부모를 손으로 고르는 문이 없다.</b> 선체 쪽에는 있지만(면을 공유하는 상대가 둘인
    /// 자리) 거점은 지금 목록이 한 종이고 뿌리도 하나라 그 상황 자체가 안 생긴다 — 확장 넷이
    /// 붙는 카드가 필요해지면 그때 <see cref="LastShiftPlacementCursor.AttachTo"/> 와 같은 문을
    /// 연다.
    /// </summary>
    public sealed class LastShiftOutpostCursor
    {
        public const float GridMeters = LastShiftOutpostCatalog.GridMeters;

        private int catalogIndex;
        private int quarterTurns;

        // 선체 커서와 같은 규약 — 드는 것은 중심이 아니라 최소 모서리다.
        private float minX;
        private float minZ;

        private bool dirty = true;
        private int cachedRevision = -1;
        private LastShiftCompartmentSpec candidate;
        private LastShiftPlacementRejection rejection;
        private LastShiftPlacementFault faults;

        public LastShiftOutpostCursor()
        {
            // 처음 열었을 때 커서가 허공이 아니라 <b>계류가 성립하는 자리</b>에 있어야 한다 —
            // 안 그러면 화면이 열리는 첫 프레임에 빨간 사유가 뜨고, §5.1 의 "실패할 수 없다" 가
            // 거기서 깨진다. 기준 자세는 계류면이 <c>MinX</c> 이므로 잔해의 <c>MaxX</c> 면에
            // 붙이는 자리가 곧 그 자리다.
            //
            // <b>조항 <c>T-3</c>(첫 자세가 안 맞아야 회전을 배운다)은 여기서 안 만든다.</b>
            // 그건 튜토리얼 상태가 커서를 어긋난 자세로 세워 두는 일이고, 잠금 훅과 같은 카드다 —
            // 시스템 기본값이 첫 프레임부터 빨간 것은 튜토리얼 밖에서는 그냥 고장으로 읽힌다.
            var anchor = LastShiftOutpost.Anchor;
            var kind = LastShiftOutpostCatalog.At(LastShiftOutpostCatalog.MooringFrame);
            minX = anchor.MaxX;
            minZ = Snap(anchor.CenterZ - kind.WidthZ * 0.5f);
        }

        public int CatalogIndex => catalogIndex;

        /// <summary><c>0..3</c>. 씬에서는 <c>y</c> 오일러 <c>quarterTurns * 90</c> 이다.</summary>
        public int QuarterTurns => quarterTurns;

        public LastShiftOutpostKind Kind => LastShiftOutpostCatalog.At(catalogIndex);

        /// <summary>커서가 잡고 있는 최소 모서리. <c>y</c> 는 선외 보행면이다.</summary>
        public Vector3 Anchor => new(minX, LastShiftOutpost.DeckY, minZ);

        public LastShiftCompartmentSpec Candidate { get { Refresh(); return candidate; } }

        /// <summary>지금 붙는 상대. <c>0</c> 이면 잔해다.</summary>
        public int ParentIndex { get { Refresh(); return candidate.ParentIndex; } }

        /// <summary>겹침·사슬 사유. 선체와 같은 플래그 형이라 문구도 같은 함수가 낸다.</summary>
        public LastShiftPlacementRejection Rejection { get { Refresh(); return rejection; } }

        /// <summary>계류면이 실제로 남의 면에 닿는가.</summary>
        public LastShiftPlacementFault Faults { get { Refresh(); return faults; } }

        /// <summary>잔해까지 몇 칸인가. 계류 골조는 <c>1</c> 이다.</summary>
        public int ChainDepth { get { Refresh(); return LastShiftOutpost.ChainDepth(candidate); } }

        /// <summary>지금 누르면 들어가는가. <b>둘 다 통과해야 한다</b>(판정 + 접면).</summary>
        public bool CanCommit
        {
            get
            {
                Refresh();
                return rejection == LastShiftPlacementRejection.None &&
                       faults == LastShiftPlacementFault.None;
            }
        }

        // ── 조작 ────────────────────────────────────────────────────────────

        public void Select(int index)
        {
            var wrapped = LastShiftOutpostCatalog.Wrap(index);
            if (wrapped == catalogIndex) return;
            catalogIndex = wrapped;
            dirty = true;
        }

        public void SelectNext(int step = 1) => Select(catalogIndex + step);

        public void Rotate(int steps = 1)
        {
            var turned = (quarterTurns + steps) & 3;
            if (turned == quarterTurns) return;
            quarterTurns = turned;
            dirty = true;
        }

        public void Nudge(int stepsX, int stepsZ)
        {
            if (stepsX == 0 && stepsZ == 0) return;
            minX += stepsX * GridMeters;
            minZ += stepsZ * GridMeters;
            dirty = true;
        }

        /// <summary>발자국 <b>중심</b>이 그 자리에 오도록 옮기고 격자에 얹는다.</summary>
        public void MoveTo(Vector3 world)
        {
            var footprint = Footprint();
            minX = Snap(world.x - footprint.LengthX * 0.5f);
            minZ = Snap(world.z - footprint.WidthZ * 0.5f);
            dirty = true;
        }

        /// <summary>최소 모서리를 그 자리에 얹는다.</summary>
        public void MoveAnchorTo(Vector3 world)
        {
            minX = Snap(world.x);
            minZ = Snap(world.z);
            dirty = true;
        }

        // ── 확정 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 표에 넣는다. <b>접면 결함이 있으면 표를 안 건드린다</b> — 판정기는 그 결함을 모르므로
        /// <see cref="LastShiftOutpost.TryRegister"/> 는 통과시킨다(선체 커서와 같은 이유).
        /// </summary>
        public bool TryCommit(int paid, out int index, out LastShiftPlacementRejection result)
        {
            Refresh();

            index = -1;
            result = rejection;
            if (faults != LastShiftPlacementFault.None) return false;
            if (!LastShiftOutpost.TryRegister(candidate, catalogIndex, paid, out index, out result)) return false;

            dirty = true;
            return true;
        }

        // ── 계산 ────────────────────────────────────────────────────────────

        private static float Snap(float value) => Mathf.Round(value / GridMeters) * GridMeters;

        private LastShiftModuleFootprint Footprint() => Kind.Footprint.Rotated(quarterTurns);

        private void Refresh()
        {
            if (!dirty && cachedRevision == LastShiftOutpost.Revision) return;

            var footprint = Footprint();
            var maxX = minX + footprint.LengthX;
            var maxZ = minZ + footprint.WidthZ;
            var centerX = (minX + maxX) * 0.5f;
            var centerZ = (minZ + maxZ) * 0.5f;

            var onXFace = footprint.DoorOnXFace;
            var coordinate = footprint.DoorFace switch
            {
                LastShiftModuleFace.MinX => minX,
                LastShiftModuleFace.MaxX => maxX,
                LastShiftModuleFace.MinZ => minZ,
                _ => maxZ
            };

            var table = LastShiftOutpost.Specs;
            candidate = new LastShiftCompartmentSpec(
                LastShiftOutpost.NextIndex,
                minX, maxX, minZ, maxZ,
                onXFace ? LastShiftDoorPlane.AlongX : LastShiftDoorPlane.AlongZ,
                coordinate,
                (onXFace ? centerZ : centerX) + footprint.DoorOffset,
                -1, LastShiftCompartmentAccess.Open);

            // 접면 자석 — 계류면이 닿은 쪽이 곧 부모다. 못 찾으면 -1 로 남고, 그 값은
            // LastShiftOutpost.Judge 에서 ChainBroken 이 된다("허공에 뜬 골조").
            LastShiftModuleAttachment.TryResolveParentWithin(candidate, table, out var resolved);
            if (resolved != candidate.ParentIndex)
                candidate = new LastShiftCompartmentSpec(
                    candidate.Index, candidate.MinX, candidate.MaxX, candidate.MinZ, candidate.MaxZ,
                    candidate.DoorPlane, candidate.DoorPlaneCoordinate, candidate.DoorCenter,
                    resolved, candidate.Access);

            rejection = LastShiftOutpost.Judge(candidate);
            faults = LastShiftModuleAttachment.CheckWithin(candidate, table);

            cachedRevision = LastShiftOutpost.Revision;
            dirty = false;
        }
    }
}
