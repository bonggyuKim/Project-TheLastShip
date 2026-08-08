using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 배치 커서. 고른 모듈 하나를 <c>1m</c> 격자 위에서 옮기고 <c>90°</c> 4단으로 돌리며,
    /// 그 자리가 성립하는지를 <b>확정 전에</b> 판정기에 물어 둔다.
    ///
    /// <b><see cref="MonoBehaviour"/> 가 아니다.</b> 화면·입력·씬이 없어도 커서 전체가 성립해야
    /// EditMode 에서 배치 흐름을 잴 수 있다. 씬에 붙는 껍데기는
    /// <see cref="LastShiftPlacementUi"/> 이고, 그쪽은 키를 여기 함수로 옮기는 일만 한다.
    ///
    /// <b>사슬은 다섯이다</b> — 커서 → 판정(<see cref="LastShiftCompartments.Judge"/>) →
    /// 표(<see cref="LastShiftCompartments.TryRegister"/>) → 씬
    /// (<see cref="LastShiftModuleAssembler.Rebuild"/>) → 벽뚫기
    /// (<see cref="LastShiftBakedDoorways.Open"/>). 이 클래스는 앞 셋까지이고, 뒤 둘은 씬을
    /// 아는 <see cref="LastShiftPlacementUi"/> 가 확정 직후에 부른다. 가른 이유는 되돌림이다:
    /// 표는 값이라 <see cref="LastShiftCompartments.TryRemove"/> 로 되돌지만 씬은 다시 세워야
    /// 하고, 그 비용을 매 커서 이동에 물릴 수는 없다.
    ///
    /// <b>판정은 캐시한다.</b> <see cref="LastShiftCompartments.Judge"/> 는 부를 때마다 표를
    /// 판정기 입력으로 옮겨 담는다(배열 하나). 매 프레임 부르면 그 배열이 매 프레임 쓰레기가
    /// 되므로, 커서 상태가 바뀌었을 때와 표 <see cref="LastShiftCompartments.Revision"/> 이
    /// 올랐을 때만 다시 잰다. <b>후자를 빼면 안 된다</b> — 옆 사람이 배치를 확정한 순간 내
    /// 화면의 "배치 가능" 이 낡은 표를 근거로 남는다.
    /// </summary>
    public sealed class LastShiftPlacementCursor
    {
        /// <summary>격자 크기. 목록 정본과 같은 값을 봐야 치수와 스냅이 안 갈린다.</summary>
        public const float GridMeters = LastShiftModuleCatalog.GridMeters;

        private int catalogIndex;
        private int quarterTurns;
        private int parentIndex = -1;

        // 커서가 드는 것은 중심이 아니라 <b>최소 모서리</b>다. 중심을 들면 홀수 치수 모듈에서
        // 중심을 격자에 얹는 순간 경계가 0.5m 어긋나고, 벽은 경계에 선다.
        private float minX;
        private float minZ;

        private bool dirty = true;
        private int cachedRevision = -1;
        private LastShiftCompartmentSpec candidate;
        private LastShiftPlacementVerdict verdict;
        private LastShiftPlacementFault faults;

        /// <summary>
        /// 문이 닿아 있는 벽의 주인을 부모로 자동 지정한다. <b>기본값이 켜짐인 것이 의도다</b> —
        /// 부모를 목록에서 따로 고르게 하면 벽에 붙여 놓고 엉뚱한 부모를 고른 배치가 판정을
        /// 통과한다(사슬 계산은 좌표를 안 보고 인덱스만 본다). 끄는 것은
        /// <see cref="AttachTo"/> 를 부른 쪽이다.
        /// </summary>
        public bool AutoParent { get; private set; } = true;

        public int CatalogIndex => catalogIndex;

        /// <summary><c>0..3</c>. 씬에서는 <c>y</c> 오일러 <c>quarterTurns * 90</c> 이다.</summary>
        public int QuarterTurns => quarterTurns;

        /// <summary>지금 붙을 상대. <c>-1</c> 이면 선체다.</summary>
        public int ParentIndex { get { Refresh(); return candidate.ParentIndex; } }

        public LastShiftModuleKind Kind => LastShiftModuleCatalog.At(catalogIndex);

        /// <summary>커서가 잡고 있는 최소 모서리. <c>y</c> 는 갑판이다.</summary>
        public Vector3 Anchor => new(minX, 0f, minZ);

        /// <summary>지금 표에 넣으려는 칸. 확정 전이므로 표에는 없다.</summary>
        public LastShiftCompartmentSpec Candidate { get { Refresh(); return candidate; } }

        /// <summary>그 칸에 대한 판정. 거부 사유가 여럿이면 다 들어 있다.</summary>
        public LastShiftPlacementVerdict Verdict { get { Refresh(); return verdict; } }

        /// <summary>판정기가 안 보는 결함 — 문이 남의 벽에 실제로 닿는가.</summary>
        public LastShiftPlacementFault Faults { get { Refresh(); return faults; } }

        /// <summary>지금 누르면 들어가는가. <b>둘 다 통과해야 한다</b>(판정 + 붙임).</summary>
        public bool CanCommit
        {
            get
            {
                Refresh();
                return verdict.Accepted && faults == LastShiftPlacementFault.None;
            }
        }

        // ── 조작 ────────────────────────────────────────────────────────────

        /// <summary>목록에서 하나 고른다. 범위 밖은 순환한다.</summary>
        public void Select(int index)
        {
            var wrapped = LastShiftModuleCatalog.Wrap(index);
            if (wrapped == catalogIndex) return;
            catalogIndex = wrapped;
            dirty = true;
        }

        /// <summary>목록을 앞뒤로 넘긴다.</summary>
        public void SelectNext(int step = 1) => Select(catalogIndex + step);

        /// <summary>
        /// <c>90°</c> 단위로 돌린다. <b>최소 모서리를 붙잡고 돈다</b> — 중심을 잡고 돌리면
        /// 홀수 치수 모듈이 회전할 때마다 경계가 격자에서 <c>0.5m</c> 씩 빠져나간다.
        /// </summary>
        public void Rotate(int steps = 1)
        {
            var turned = (quarterTurns + steps) & 3;
            if (turned == quarterTurns) return;
            quarterTurns = turned;
            dirty = true;
        }

        /// <summary>격자 칸 단위로 민다.</summary>
        public void Nudge(int stepsX, int stepsZ)
        {
            if (stepsX == 0 && stepsZ == 0) return;
            minX += stepsX * GridMeters;
            minZ += stepsZ * GridMeters;
            dirty = true;
        }

        /// <summary>
        /// 발자국 <b>중심</b>이 그 자리에 오도록 옮기고 격자에 얹는다. 마우스·시선으로
        /// 자리를 찍는 쪽이 쓰는 문이고, 사람은 방의 중심을 겨냥하지 모서리를 겨냥하지 않는다.
        /// </summary>
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

        /// <summary>
        /// 부모를 손으로 정하고 <see cref="AutoParent"/> 를 끈다. 자동으로는 안 나오는
        /// 사슬(면을 공유하는 상대가 둘인 자리)을 사람이 고르는 문이다.
        /// </summary>
        public void AttachTo(int parent)
        {
            AutoParent = false;
            parentIndex = parent;
            dirty = true;
        }

        /// <summary>자동 부모로 되돌린다.</summary>
        public void AttachAutomatically()
        {
            if (AutoParent) return;
            AutoParent = true;
            dirty = true;
        }

        // ── 확정 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 표에 넣는다. <b>붙임 결함이 있으면 표를 안 건드린다</b> — 판정기는 그 결함을 모르므로
        /// <see cref="LastShiftCompartments.TryRegister"/> 는 통과시킨다.
        ///
        /// 넣고 나면 <see cref="LastShiftCompartments.NextModuleIndex"/> 가 오르므로 커서가 든
        /// 후보는 그 순간 낡는다. 자리를 안 옮기는 것이 의도다 — 방금 놓은 자리에 그대로 두면
        /// 다음 판정이 곧바로 <see cref="LastShiftPlacementRejection.OverlapsPlacement"/> 를 내고,
        /// 그게 "여긴 이미 찼다" 라는 가장 읽기 쉬운 화면이다.
        /// </summary>
        public bool TryCommit(out int index, out LastShiftPlacementVerdict result)
        {
            Refresh();

            index = -1;
            result = verdict;
            if (faults != LastShiftPlacementFault.None) return false;
            if (!LastShiftCompartments.TryRegister(candidate, out index, out result)) return false;

            dirty = true;
            return true;
        }

        // ── 계산 ────────────────────────────────────────────────────────────

        private static float Snap(float value) => Mathf.Round(value / GridMeters) * GridMeters;

        private LastShiftModuleFootprint Footprint() => Kind.Footprint.Rotated(quarterTurns);

        private void Refresh()
        {
            if (!dirty && cachedRevision == LastShiftCompartments.Revision) return;

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

            var table = LastShiftCompartments.Specs;
            candidate = new LastShiftCompartmentSpec(
                LastShiftCompartments.NextModuleIndex,
                minX, maxX, minZ, maxZ,
                onXFace ? LastShiftDoorPlane.AlongX : LastShiftDoorPlane.AlongZ,
                coordinate,
                (onXFace ? centerZ : centerX) + footprint.DoorOffset,
                parentIndex,
                LastShiftCompartmentAccess.Open);

            // 부모를 먼저 정하고 나서 판정한다 — 사슬·구역 귀속이 전부 부모에서 나온다.
            if (AutoParent)
            {
                LastShiftModuleAttachment.TryResolveParent(candidate, table, out var resolved);
                if (resolved != candidate.ParentIndex)
                    candidate = new LastShiftCompartmentSpec(
                        candidate.Index, candidate.MinX, candidate.MaxX, candidate.MinZ, candidate.MaxZ,
                        candidate.DoorPlane, candidate.DoorPlaneCoordinate, candidate.DoorCenter,
                        resolved, candidate.Access);
            }

            verdict = LastShiftCompartments.Judge(candidate);
            faults = LastShiftModuleAttachment.Check(candidate, table);

            cachedRevision = LastShiftCompartments.Revision;
            dirty = false;
        }
    }
}
