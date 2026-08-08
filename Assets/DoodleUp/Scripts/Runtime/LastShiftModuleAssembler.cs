using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 프리팹 하나가 표의 칸 하나에 맞는지, 안 맞으면 무엇이 안 맞는지.
    ///
    /// <b>"안 맞는다" 를 한 값으로 뭉치지 않는다.</b> 발자국이 틀린 것은 아트가 프리팹 치수를
    /// 고칠 일이고, 문 면이 틀린 것은 배치 쪽이 방향을 고칠 일이다 — 뭉쳐 두면 로그를 보고도
    /// 누가 고쳐야 하는지가 안 갈린다.
    /// </summary>
    public enum LastShiftModuleFit
    {
        /// <summary>맞는다.</summary>
        Fits = 0,

        /// <summary>네 회전 어디서도 발자국이 표의 칸과 안 맞는다.</summary>
        FootprintMismatch = 1,

        /// <summary>발자국은 맞는데 그 회전에서 문이 표가 요구하는 면·자리에 안 온다.</summary>
        DoorMismatch = 2,

        /// <summary>프리팹이 선언한 발자국 자체가 성립하지 않는다 — 치수가 <c>0</c> 이하이거나 문이 면을 넘친다.</summary>
        AnchorInvalid = 3
    }

    /// <summary>
    /// 표에 들어온 모듈 칸을 씬에 세운다. <b>배치를 판정하지 않는다</b> —
    /// <see cref="LastShiftCompartments.TryRegister"/> 를 통과한 칸만 여기 온다. 조립기가 다시
    /// 판정하면 표에 있는데 씬에 없는 방이 생기고, 그 방은 압력 오버레이에는 있으므로 문을 닫아도
    /// 격리가 안 되는 배와 같은 종류의 어긋남이 된다.
    ///
    /// <b>회전은 <c>90°</c> 4단이다.</b> <c>45°</c> 는 확장 검토 §3.3 에서 기각됐고, 허용되면
    /// 발자국 AABB 겹침 판정부터 다시 열린다(추정 §8). 그래서 여기서 고르는 것은 각도가 아니라
    /// <b>네 개 중 하나</b>이고, 네 개를 다 넣어 보고 맞는 것을 쓴다 — 면에서 각도를 유도하는
    /// 식을 쓰면 그 식과 실제 회전이 갈릴 자리가 하나 더 생긴다. 넷을 돌리는 비용은 배치
    /// 확정 한 번에 네 번이다.
    ///
    /// <b>프리팹이 없으면 그레이박스로 선다.</b> 아트 에셋이 오기 전에도 배치 경로가 끝까지
    /// 도는 쪽을 골랐다 — 아트를 기다리면 축 D·A·B 가 씬에서 한 번도 안 서 본 상태로 쌓인다.
    /// 그레이박스는 <b>추정 §4.1 의 절차적 생성 경로가 아니다</b>: 판·문 구멍·인방까지이고 창·
    /// 라벨·드레싱·등은 없다. 프리팹이 들어오면 이 경로는 안 돈다.
    ///
    /// <b>안 하는 것 하나를 여기 적어 둔다.</b> 모듈이 <see cref="LastShiftCompartmentSpec.ParentIndex"/>
    /// 로 가리키는 상대가 선체이거나 고정 구획이면, 그 벽은 배 프리팹에 이미 구워져 있고 구멍이
    /// 없다 — 그래서 <b>지금 세운 모듈은 문 자리까지 이어지되 그 벽을 뚫고 들어갈 수는 없다.</b>
    /// 구운 벽을 뚫는 일("모듈 문틀")은 축 B 가 <c>docs/tech/free-placement-compartment-table-v1.md</c>
    /// §6 에 안 한 것으로 남긴 항목이고 이 카드에서도 안 열었다. 모듈끼리 잇는 사슬은 선다 —
    /// 부모가 모듈이면 그 벽은 여기서 세우므로 구멍이 같이 뚫린다.
    /// </summary>
    public static class LastShiftModuleAssembler
    {
        /// <summary>조립된 모듈이 매달리는 칸의 이름. 씬에서 사람이 찾는 자리이고 테스트도 이 이름으로 센다.</summary>
        public const string YardName = "PlacedModules";

        private const float Epsilon = 0.001f;

        // ── 맞춤 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 프리팹 발자국을 <c>90°</c> 4단으로 돌려 표의 칸에 맞는 회전을 찾는다.
        ///
        /// <paramref name="quarterTurns"/> 는 <c>0..3</c> 이고 씬에서는 <c>y</c> 오일러
        /// <c>quarterTurns * 90</c> 이다. 발자국과 문점을 <b>같은 회전에서 같이</b> 봐야 한다 —
        /// 따로 맞추면 정사각 방에서 발자국은 네 회전 다 맞고 문만 하나가 맞는데, 그 하나를
        /// 놓치고 먼저 맞은 발자국을 쓰면 문이 엉뚱한 면에 선다.
        /// </summary>
        public static bool TryFit(
            in LastShiftModuleFootprint prefab, in LastShiftCompartmentSpec spec,
            out int quarterTurns, out LastShiftModuleFit fit)
        {
            quarterTurns = 0;

            if (!prefab.DoorFits)
            {
                fit = LastShiftModuleFit.AnchorInvalid;
                return false;
            }

            var target = LastShiftModuleFootprint.Of(spec);
            var footprintMatched = false;

            for (var turns = 0; turns < 4; turns++)
            {
                // 홀수 회전은 x 와 z 를 맞바꾼다. 이걸 빼먹으면 8×5 방이 5×8 자리에 선다.
                var swapped = turns % 2 == 1;
                var rotatedX = swapped ? prefab.WidthZ : prefab.LengthX;
                var rotatedZ = swapped ? prefab.LengthX : prefab.WidthZ;

                if (Mathf.Abs(rotatedX - target.LengthX) > Epsilon ||
                    Mathf.Abs(rotatedZ - target.WidthZ) > Epsilon) continue;
                footprintMatched = true;

                if ((Rotate(prefab.DoorPoint, turns) - target.DoorPoint).sqrMagnitude > Epsilon * Epsilon)
                    continue;

                quarterTurns = turns;
                fit = LastShiftModuleFit.Fits;
                return true;
            }

            fit = footprintMatched ? LastShiftModuleFit.DoorMismatch : LastShiftModuleFit.FootprintMismatch;
            return false;
        }

        /// <summary>
        /// <c>y</c> 축 <c>90°</c> 회전을 정수로 돈다. <b>삼각함수를 안 쓴다</b> — <c>Quaternion</c>
        /// 으로 돌리면 <c>90°</c> 에서도 <c>1e-7</c> 짜리 잔차가 남고, 그 잔차가 문점 비교
        /// 허용오차를 먹는다. 유니티 규약과 같은 방향이다: <c>Euler(0, 90, 0)</c> 이 <c>+Z</c> 를
        /// <c>+X</c> 로 보내므로 <c>(x, z) → (z, -x)</c> 다.
        /// </summary>
        public static Vector2 Rotate(Vector2 point, int quarterTurns) => (quarterTurns & 3) switch
        {
            0 => point,
            1 => new Vector2(point.y, -point.x),
            2 => new Vector2(-point.x, -point.y),
            _ => new Vector2(-point.y, point.x)
        };

        /// <summary>
        /// 팔레트에서 이 칸에 맞는 프리팹을 고른다. <b>먼저 맞는 것을 쓴다</b> — 아트가 kit 순서로
        /// 우선순위를 정할 수 있게 두는 것이고, 그래서 목록 순서가 뜻을 갖는다.
        /// 앵커가 없는 항목은 건너뛴다: 앵커 없는 프리팹은 발자국을 선언하지 않았으므로 어디에
        /// 놓아야 하는지를 알 방법이 없다.
        /// </summary>
        public static bool TryPick(
            LastShiftModulePalette palette, in LastShiftCompartmentSpec spec,
            out GameObject prefab, out int quarterTurns)
        {
            prefab = null;
            quarterTurns = 0;
            if (palette == null) return false;

            var prefabs = palette.ModulePrefabs;
            for (var index = 0; index < prefabs.Count; index++)
            {
                var candidate = prefabs[index];
                if (candidate == null) continue;

                var anchor = candidate.GetComponent<LastShiftModuleAnchor>();
                if (anchor == null) continue;

                if (!TryFit(anchor.Footprint, spec, out quarterTurns, out _)) continue;

                prefab = candidate;
                return true;
            }

            quarterTurns = 0;
            return false;
        }

        // ── 조립 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 표의 칸 하나를 세운다. 고정 구획은 <b>거부한다</b> — 그 열하나는 씬 빌더가 배 프리팹에
        /// 구워 넣으므로, 여기서 또 세우면 같은 방이 두 겹으로 서고 문이 이중으로 막힌다.
        ///
        /// 루트는 언제나 <c>(CenterX, 0, CenterZ)</c> 에 서고 이름은
        /// <see cref="LastShiftCompartments.NameOf(in LastShiftCompartmentSpec)"/> 다. 프리팹이든
        /// 그레이박스든 이 껍데기는 같다 — 씬에서 모듈을 찾는 쪽이 어느 경로로 섰는지 몰라도 되게.
        /// </summary>
        public static GameObject Build(
            in LastShiftCompartmentSpec spec, Transform parent, LastShiftModulePalette palette)
        {
            if (spec.IsFixed)
                throw new System.ArgumentException(
                    $"fixed compartment {spec.Compartment} is baked into the ship prefab; " +
                    "the assembler only stands placed modules", nameof(spec));

            var root = new GameObject(LastShiftCompartments.NameOf(spec));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(spec.CenterX, 0f, spec.CenterZ);

            if (TryPick(palette, spec, out var prefab, out var quarterTurns))
            {
                var shell = Object.Instantiate(prefab, root.transform);
                shell.name = "Shell";
                shell.transform.localPosition = Vector3.zero;
                shell.transform.localEulerAngles = new Vector3(0f, quarterTurns * 90f, 0f);
                return root;
            }

            BuildGreybox(root.transform, spec, palette);
            return root;
        }

        /// <summary>
        /// 표에 있는 모듈 전부를 다시 세운다. <b>먼저 비운다</b> — 표는 해제할 때 뒤 칸을 당기므로
        /// (<see cref="LastShiftCompartments.TryRemove"/>) 칸과 씬 오브젝트를 짝지어 두면 그 당김을
        /// 씬에서 한 번 더 풀어야 하고, 그 두 벌이 갈리면 이름과 자리가 어긋난 방이 남는다.
        /// 배치 해제는 기항에서만 일어난다는 전제(추정 §8)에서 다시 세우는 값이 싸다.
        /// </summary>
        public static int Rebuild(Transform parent, LastShiftModulePalette palette)
        {
            if (parent == null) throw new System.ArgumentNullException(nameof(parent));

            var yard = Clear(parent);
            var specs = LastShiftCompartments.Specs;
            var built = 0;

            for (var index = LastShiftCompartments.FixedCount; index < specs.Length; index++)
            {
                Build(specs[index], yard, palette);
                built++;
            }

            return built;
        }

        /// <summary>
        /// 세워 둔 모듈을 전부 지우고 빈 칸을 돌려준다. 칸 자체는 남긴다 — 씬에서 누가 그
        /// <c>Transform</c> 을 참조로 물고 있을 수 있고, 매번 새로 만들면 그 참조가 끊긴다.
        /// </summary>
        public static Transform Clear(Transform parent)
        {
            if (parent == null) throw new System.ArgumentNullException(nameof(parent));

            var yard = parent.Find(YardName);
            if (yard == null)
            {
                var created = new GameObject(YardName);
                created.transform.SetParent(parent, false);
                return created.transform;
            }

            for (var child = yard.childCount - 1; child >= 0; child--)
                DestroyObject(yard.GetChild(child).gameObject);

            return yard;
        }

        // ── 그레이박스 ──────────────────────────────────────────────────────

        /// <summary>
        /// 프리팹이 없을 때 세우는 껍데기. 바닥·천장 슬래브와 벽 넷이고, <b>자기 안쪽 문이 놓인
        /// 면은 안 세운다</b> — 그 면은 부모가 소유한다(씬 빌더 <c>IsOwnDoorFace</c> 와 같은 규약).
        /// 자식 모듈의 문이 놓인 면에는 구멍을 뚫는다: 안 뚫으면 사슬 두 칸째부터 벽으로 막힌
        /// 방이 서고, 그건 표에서는 걸어갈 수 있는 것으로 세어진다.
        /// </summary>
        private static void BuildGreybox(
            Transform root, in LastShiftCompartmentSpec spec, LastShiftModulePalette palette)
        {
            const float thickness = LastShiftCompartments.PanelThickness;
            const float height = LastShiftCompartments.InteriorHeight;

            var halfX = spec.LengthX * 0.5f;
            var halfZ = spec.WidthZ * 0.5f;
            var slabX = spec.LengthX + 2f * thickness;
            var slabZ = spec.WidthZ + 2f * thickness;

            var floor = palette != null ? palette.FloorMaterial : null;
            var ceiling = palette != null ? palette.CeilingMaterial : null;
            var wall = palette != null ? palette.WallMaterial : null;

            CreateCube(root, "Floor", new Vector3(0f, -thickness * 0.5f, 0f),
                new Vector3(slabX, thickness, slabZ), floor);
            CreateCube(root, "Ceiling", new Vector3(0f, height + thickness * 0.5f, 0f),
                new Vector3(slabX, thickness, slabZ), ceiling);

            for (var face = 0; face < 4; face++)
            {
                var alongX = face < 2;
                var atMax = face % 2 == 1;
                if (IsOwnDoorFace(spec, alongX, atMax)) continue;

                var half = alongX ? halfX : halfZ;
                var freeHalf = alongX ? halfZ : halfX;
                var plane = (atMax ? half + thickness * 0.5f : -half - thickness * 0.5f);

                CreateWall(root, $"Wall_{(alongX ? "X" : "Z")}{(atMax ? "Max" : "Min")}",
                    alongX, plane, -freeHalf - thickness, freeHalf + thickness, height, thickness,
                    ChildDoorwaysOn(spec, alongX, atMax), wall);
            }
        }

        /// <summary>이 면이 구획 자기 안쪽 문이 놓인 면인가. 씬 빌더의 같은 이름 함수와 같은 판정이다.</summary>
        private static bool IsOwnDoorFace(in LastShiftCompartmentSpec spec, bool alongX, bool atMax)
        {
            if (spec.DoorPlane != (alongX ? LastShiftDoorPlane.AlongX : LastShiftDoorPlane.AlongZ)) return false;
            var face = alongX ? (atMax ? spec.MaxX : spec.MinX) : (atMax ? spec.MaxZ : spec.MinZ);
            return Mathf.Abs(spec.DoorPlaneCoordinate - face) < Epsilon;
        }

        /// <summary>
        /// 이 면에 뚫어야 하는 구멍의 자유축 로컬 좌표. 잠긴 자식은 구멍을 안 낸다 — 그레이박스에서
        /// 잠긴 문은 구멍이 아니라 메운 판이다(<see cref="LastShiftCompartmentSpec.IsPassable"/>).
        ///
        /// 회랑은 안 본다. 상부·관측 회랑은 고정 구획에만 붙고(둘 다 <c>LastShiftCompartment</c> 를
        /// 키로 받는다) 그 구획은 배 프리팹에 구워져 있으므로 이 경로에 안 온다.
        /// </summary>
        private static float[] ChildDoorwaysOn(in LastShiftCompartmentSpec spec, bool alongX, bool atMax)
        {
            var origin = alongX ? spec.CenterZ : spec.CenterX;
            var face = alongX ? (atMax ? spec.MaxX : spec.MinX) : (atMax ? spec.MaxZ : spec.MinZ);
            var plane = alongX ? LastShiftDoorPlane.AlongX : LastShiftDoorPlane.AlongZ;

            var openings = new List<float>();
            var specs = LastShiftCompartments.Specs;
            for (var index = 0; index < specs.Length; index++)
            {
                ref readonly var child = ref specs[index];
                if (child.ParentIndex != spec.Index || !child.IsPassable) continue;
                if (child.DoorPlane != plane) continue;
                if (Mathf.Abs(child.DoorPlaneCoordinate - face) > Epsilon) continue;
                openings.Add(child.DoorCenter - origin);
            }

            openings.Sort();
            return openings.ToArray();
        }

        /// <summary>
        /// 판 한 장. <paramref name="openings"/> 가 비면 통짜이고, 있으면 구간을 잘라 세운 뒤
        /// 그 위에 인방을 얹는다 — 인방이 없으면 문 높이(<c>2.2</c>)에서 천장까지 뚫려 그림과
        /// 통행 가능 범위가 어긋난다. 씬 빌더 <c>CreateWallWithOpenings</c> 의 문 경로와 같은
        /// 규칙이고, 창은 없다(그레이박스에 창을 낼 자리가 아직 없다).
        /// </summary>
        private static void CreateWall(Transform root, string name, bool alongX, float plane,
            float freeMin, float freeMax, float height, float thickness,
            float[] openings, Material material)
        {
            const float doorWidth = LastShiftZoneDoor.OpeningWidth;
            const float doorHeight = LastShiftZoneDoor.OpeningHeight;

            var edges = new List<float> { freeMin };
            for (var index = 0; index < openings.Length; index++)
            {
                edges.Add(openings[index] - doorWidth * 0.5f);
                edges.Add(openings[index] + doorWidth * 0.5f);
            }
            edges.Add(freeMax);

            // 짝수 index 로 시작하는 구간이 판, 그 사이가 구멍이다.
            for (var segment = 0; segment + 1 < edges.Count; segment += 2)
            {
                var min = edges[segment];
                var max = edges[segment + 1];
                if (max - min <= Epsilon * 0.1f) continue;
                CreateSlab(root, $"{name}_{segment / 2}", alongX, plane,
                    (min + max) * 0.5f, max - min, height, 0f, thickness, material);
            }

            if (height - doorHeight <= Epsilon * 0.1f) return;

            for (var index = 0; index < openings.Length; index++)
                CreateSlab(root, $"{name}_Lintel_{index}", alongX, plane,
                    openings[index], doorWidth, height - doorHeight, doorHeight, thickness, material);
        }

        private static void CreateSlab(Transform root, string name, bool alongX, float plane,
            float freeCenter, float freeSize, float height, float bottom, float thickness, Material material)
        {
            var position = alongX
                ? new Vector3(plane, bottom + height * 0.5f, freeCenter)
                : new Vector3(freeCenter, bottom + height * 0.5f, plane);
            var scale = alongX
                ? new Vector3(thickness, height, freeSize)
                : new Vector3(freeSize, height, thickness);
            CreateCube(root, name, position, scale, material);
        }

        /// <summary>
        /// 판 하나. <b>콜라이더가 붙은 채로 둔다</b> — 벽을 통과할 수 있으면 §9.4 막다른 방
        /// 전제가 씬에서 성립하지 않고, 이탈 거리 계산이 재는 경로와 실제 경로가 갈린다.
        ///
        /// 머티리얼이 없으면 프리미티브 기본값 그대로 둔다. <b><c>Shader.Find</c> 로 만들지
        /// 않는다</b> — 그게 빌드에서 스트립돼 분홍색으로 서는 경로다(추정 §4.2,
        /// <see cref="LastShiftModulePalette"/>).
        /// </summary>
        private static void CreateCube(
            Transform root, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(root, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = scale;
            if (material != null) cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>
        /// 에디터에서는 <c>Destroy</c> 가 프레임 끝까지 미뤄져 <see cref="Rebuild"/> 가 지운 방을
        /// 같은 프레임에 다시 세면 두 겹이 된다. EditMode 테스트가 도는 자리이므로 갈라 둔다.
        /// </summary>
        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
