using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 오버레이가 들고 있는 배치 하나. <b>씬 참조가 없다</b> — 발자국 여섯 값과 확정된 구역뿐이다.
    ///
    /// 구역을 값으로 들고 있는 것이 조항 F-1 이다(<c>docs/tech/free-placement-expansion-feasibility-v1.md</c>).
    /// 모듈의 구역은 <b>배치 시점에 사슬 뿌리의 선체 문</b>이 정하고 그 뒤로 재계산하지 않는다.
    /// 모듈 자기 좌표로 매번 다시 읽으면 선수 쪽으로 뻗은 모듈이 조종석 구역으로 읽히고, 그러면
    /// 산소실 문을 닫아도 그 모듈이 격리가 안 되는 배가 나온다(타당성 검토 §11-1).
    /// </summary>
    public readonly struct LastShiftPlacedModule
    {
        public LastShiftPlacedModule(
            float minX, float maxX, float minZ, float maxZ, float minY, float maxY, LastShiftZone zone,
            int catalogIndex = NoCatalogIndex)
        {
            MinX = Mathf.Min(minX, maxX);
            MaxX = Mathf.Max(minX, maxX);
            MinZ = Mathf.Min(minZ, maxZ);
            MaxZ = Mathf.Max(minZ, maxZ);
            MinY = Mathf.Min(minY, maxY);
            MaxY = Mathf.Max(minY, maxY);
            Zone = zone;
            CatalogIndex = catalogIndex;
            Registered = true;
        }

        /// <summary>카탈로그를 안 거치고 등록된 칸. 효과가 하나도 안 붙는다.</summary>
        public const int NoCatalogIndex = -1;

        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        /// <summary>바닥·천장. 갑판 아래(덕트·에어록)를 모듈이 먹지 않게 하는 것이 이 두 값의 일이다.</summary>
        public float MinY { get; }
        public float MaxY { get; }

        /// <summary>배치 시점에 정해진 구역. <b>여기서 다시 계산하지 않는다</b>(조항 F-1).</summary>
        public LastShiftZone Zone { get; }

        /// <summary>
        /// 어느 카탈로그 항목으로 세웠는가(<see cref="LastShiftModuleCatalog"/> 인덱스).
        /// <see cref="NoCatalogIndex"/> 면 카탈로그 밖 등록이다.
        ///
        /// <b>효과가 이 값 하나에 매달린다</b>(<see cref="LastShiftModuleEffects"/>). 종류와 구역을
        /// 한 칸에 같이 두는 것이 요지다 — 종류를 다른 장부(여력 원장)에서 읽고 구역을 여기서
        /// 읽으면, 두 장부의 자리 번호가 한 번이라도 어긋나는 날 <b>남의 모듈 효과가 남의 구역에</b>
        /// 붙는다. 그 어긋남은 화면 어디에도 안 보인다.
        /// </summary>
        public int CatalogIndex { get; }

        /// <summary>
        /// 이 칸이 살아 있는가. <c>default</c> 구조체가 <c>false</c> 가 되도록 이 방향으로 든다 —
        /// 지운 칸을 배열에서 당겨오면 이미 나눠 준 핸들이 다른 모듈을 가리키게 된다.
        /// </summary>
        public bool Registered { get; }

        /// <summary>
        /// 점–AABB. <b>경계면은 안쪽이다</b> — 벽에 붙어 선 승무원은 방 안에 있다. 맞닿은 두 모듈의
        /// 공유 벽에서만 양쪽이 다 참인데, 그 동점은 <see cref="LastShiftPlacedModules.TryResolve"/> 가
        /// 등록 순서로 가른다.
        /// </summary>
        public bool Contains(Vector3 position) =>
            position.x >= MinX && position.x <= MaxX &&
            position.z >= MinZ && position.z <= MaxZ &&
            position.y >= MinY && position.y <= MaxY;
    }

    /// <summary>
    /// 자유 배치로 확정된 모듈의 구역 오버레이. <see cref="LastShiftZoneAtlas.Resolve"/> 가
    /// 선체 x 밴드를 보기 <b>전에</b> 여기를 먼저 본다.
    ///
    /// <b>이 자리에 같은 모양의 선례가 이미 매 tick 경로에 있다.</b>
    /// <see cref="LastShiftSandboxController.IsZoneVacuum(Vector3)"/> 가 <c>Resolve</c> 를 부르기
    /// 전에 <see cref="LastShiftBypassDuct.IsUnpressurizedSpace"/> 로 점–AABB 루프를 먼저 돈다.
    /// 그 파일이 적어 둔 규약("진공 판정을 매 tick 도는 자리에 씬 조회를 들이지 않는다")을 그대로
    /// 따른다 — <b>오버레이는 씬을 안 보고 값 배열만 본다.</b>
    ///
    /// <b><see cref="LastShiftZoneAtlas.ZoneCount"/> 를 안 건드린다.</b> 모듈은 새 구역을 만들지
    /// 않고 기존 넷 중 하나에 붙는다. 그래서 <c>LastShiftZonePressures</c> 배열·<c>SIMUL_ZONES</c>·
    /// <c>RG-4</c> 조합·HUD 칸·네트워크 스냅샷이 하나도 안 열린다 —
    /// <c>docs/tech/free-placement-runtime-chain-estimate-v1.md</c> §2.3.
    ///
    /// 등록·해제를 부르는 쪽은 아직 없다. 배치 확정 경로(축 B·C)가 붙을 때 <see cref="Register"/>
    /// 를 부르고, 그때 넘기는 구역은 <see cref="LastShiftPlacementRules.Evaluate"/> 가 돌려준
    /// <see cref="LastShiftPlacementVerdict.Zone"/> 이다.
    /// </summary>
    public static class LastShiftPlacedModules
    {
        /// <summary>
        /// 기본 바닥. 갑판이다. 승무원 위치는 발밑 기준이라 갑판 위에 선 사람이 <c>y = 0</c> 이고,
        /// 경계면을 안쪽으로 잡는 <see cref="LastShiftPlacedModule.Contains"/> 가 그를 잡는다.
        /// </summary>
        public const float DefaultFloorY = LastShiftBypassDuct.DeckY;

        /// <summary>기본 천장. 선체 내부 높이와 같다 — 모듈이 배와 같은 층고를 쓴다는 전제다.</summary>
        public const float DefaultCeilingY = LastShiftShipPhysics.CeilingInnerHeight;

        /// <summary>
        /// 처음 잡는 칸 수. <c>N = 20</c> 이 타당성 검토가 잡은 상한이라 그 위로 하나 잡는다 —
        /// 넘으면 배로 늘린다. 매 tick 도는 자리라 정상 운전 중 재할당이 안 나는 쪽을 고른다.
        /// </summary>
        private const int InitialCapacity = 24;

        private static LastShiftPlacedModule[] modules = new LastShiftPlacedModule[InitialCapacity];

        /// <summary>나눠 준 핸들의 상한. 해제한 칸을 포함하므로 살아 있는 모듈 수가 아니다.</summary>
        public static int Count { get; private set; }

        /// <summary>지금 구역을 덮고 있는 모듈 수.</summary>
        public static int ActiveCount
        {
            get
            {
                var active = 0;
                for (var index = 0; index < Count; index++)
                    if (modules[index].Registered) active++;
                return active;
            }
        }

        /// <summary>
        /// 위치 → 모듈 구역. 등록이 하나도 없으면 곧바로 <c>false</c> 이고, 그것이 자유 배치가
        /// 안 붙은 배에서 <see cref="LastShiftZoneAtlas.Resolve"/> 가 예전과 한 글자도 다르지
        /// 않게 도는 이유다.
        ///
        /// 공유 벽 동점은 <b>먼저 등록한 쪽</b>이 갖는다. 겹침 자체는 판정기가 이미 물리므로
        /// (<see cref="LastShiftPlacementRejection.OverlapsPlacement"/>) 동점이 나는 곳은 맞닿은
        /// 면 하나뿐인데, 그 면조차 답이 갈리면 문을 여닫을 때 같은 좌표가 두 구역을 오간다.
        /// </summary>
        public static bool TryResolve(Vector3 position, out LastShiftZone zone)
        {
            for (var index = 0; index < Count; index++)
            {
                ref readonly var module = ref modules[index];
                if (!module.Registered || !module.Contains(position)) continue;
                zone = module.Zone;
                return true;
            }

            zone = default;
            return false;
        }

        /// <summary>배치 하나를 등록하고 핸들을 돌려준다. 층고는 기본값을 쓴다.</summary>
        public static int Register(
            float minX, float maxX, float minZ, float maxZ, LastShiftZone zone,
            int catalogIndex = LastShiftPlacedModule.NoCatalogIndex) =>
            Register(minX, maxX, minZ, maxZ, DefaultFloorY, DefaultCeilingY, zone, catalogIndex);

        public static int Register(
            float minX, float maxX, float minZ, float maxZ, float minY, float maxY, LastShiftZone zone,
            int catalogIndex = LastShiftPlacedModule.NoCatalogIndex)
        {
            var handle = Count;
            for (var index = 0; index < Count; index++)
            {
                if (modules[index].Registered) continue;
                handle = index;
                break;
            }

            if (handle == Count)
            {
                if (Count == modules.Length)
                {
                    var grown = new LastShiftPlacedModule[modules.Length * 2];
                    System.Array.Copy(modules, grown, modules.Length);
                    modules = grown;
                }

                Count++;
            }

            modules[handle] = new LastShiftPlacedModule(minX, maxX, minZ, maxZ, minY, maxY, zone, catalogIndex);
            return handle;
        }

        /// <summary>
        /// 판정기 입력을 그대로 등록한다. <paramref name="zone"/> 은 <b>후보 자기 좌표가 아니라</b>
        /// <see cref="LastShiftPlacementVerdict.Zone"/> — 사슬 뿌리가 정한 값이다(조항 F-1).
        /// </summary>
        public static int Register(
            in LastShiftPlacement placement, LastShiftZone zone,
            int catalogIndex = LastShiftPlacedModule.NoCatalogIndex) => Register(
            placement.MinX, placement.MaxX, placement.MinZ, placement.MaxZ,
            DefaultFloorY, DefaultCeilingY, zone, catalogIndex);

        /// <summary>
        /// 이미 등록된 모듈을 옮긴다. 핸들이 살아 있어야 한다 — 해제한 칸을 되살리면 그 사이에
        /// 그 핸들을 다시 나눠 줬을 수 있다.
        /// </summary>
        public static bool TryReplace(
            int handle, float minX, float maxX, float minZ, float maxZ, LastShiftZone zone)
        {
            if (handle < 0 || handle >= Count || !modules[handle].Registered) return false;

            // 종류는 안 바뀐다 — 옮기는 것은 자리이지 산 물건이 아니다. 여기서 기본값으로
            // 덮으면 모듈을 옮긴 순간 효과만 조용히 사라진다.
            modules[handle] = new LastShiftPlacedModule(
                minX, maxX, minZ, maxZ, DefaultFloorY, DefaultCeilingY, zone, modules[handle].CatalogIndex);
            return true;
        }

        /// <summary>등록을 해제한다. 그 좌표는 다시 선체 밴드가 답한다.</summary>
        public static bool Remove(int handle)
        {
            if (handle < 0 || handle >= Count || !modules[handle].Registered) return false;

            modules[handle] = default;
            if (handle == Count - 1)
            {
                // 꼬리부터 죽은 칸을 걷어내야 매 tick 루프가 지운 만큼 짧아진다.
                while (Count > 0 && !modules[Count - 1].Registered) Count--;
            }

            return true;
        }

        /// <summary>핸들 하나가 지금 무엇을 덮고 있는지. 살아 있지 않으면 <c>false</c> 다.</summary>
        public static bool TryGet(int handle, out LastShiftPlacedModule module)
        {
            if (handle < 0 || handle >= Count || !modules[handle].Registered)
            {
                module = default;
                return false;
            }

            module = modules[handle];
            return true;
        }

        /// <summary>
        /// 전부 지운다. 씬을 다시 세울 때와 테스트가 부른다.
        ///
        /// <b>정적 상태라 반드시 초기화 훅이 있어야 한다.</b> 도메인 리로드를 끈 에디터에서는
        /// 플레이를 멈춰도 정적 필드가 안 죽으므로, 지난 판의 모듈이 다음 판의 진공 판정에
        /// 그대로 남는다.
        /// </summary>
        public static void Clear()
        {
            for (var index = 0; index < Count; index++) modules[index] = default;
            Count = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Clear();
    }
}
