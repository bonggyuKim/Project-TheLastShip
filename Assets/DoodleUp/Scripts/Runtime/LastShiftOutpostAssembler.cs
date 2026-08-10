using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 거점 표를 씬에 세운다 — <see cref="LastShiftModuleAssembler"/> 의 거점판이고, <b>훨씬
    /// 짧은 것이 설계다.</b>
    ///
    /// <b>방을 안 세운다. 골조를 세운다.</b> 거점은 처음부터 진공이고 승무원은 우주복을 입고
    /// 잠깐 들른다(§4.4) — 벽·천장·문틀이 하나도 필요 없다. 선체 조립기가 하는 일의 대부분
    /// (자기 문 면은 안 세우기, 자식 문 구멍 뚫기, 판 두께 맞추기)이 여기서는 <b>대상 자체가
    /// 없다.</b> 그래서 이 파일은 조립기를 재사용하지 않는다: 재사용하면 진공에 기밀 방이 서고,
    /// 그 방은 <see cref="LastShiftBakedDoorways"/> 를 부르며 <b>선체 판을 찾아 자르려 든다.</b>
    ///
    /// <b>그레이박스인 것도 같은 이유로 임시가 아니다.</b> 골조 형상은 <c>game-art</c> 몫이고
    /// (튜토리얼 §8 미결 <c>2</c>), 여기서 세우는 것은 발자국이 실제로 그 자리에 있다는 것을
    /// 눈으로 확인할 수 있는 최소 형태다 — 갑판 판 하나와 기둥 넷.
    ///
    /// <b>배 밑에 안 붙인다.</b> 뿌리를 배에 매달면 배가 움직일 때 거점이 따라간다 —
    /// 조항 <c>O-5</c> 가 명시적으로 아니라고 적은 그것이다.
    /// </summary>
    public static class LastShiftOutpostAssembler
    {
        /// <summary>씬에서 거점을 담는 칸 이름. 검증기와 로그가 같은 문자열을 본다.</summary>
        public const string RootName = "LastShiftOutpost";

        /// <summary>갑판 판 두께. 선체 구획 판과 같은 값이다.</summary>
        public const float DeckThickness = LastShiftCompartments.PanelThickness;

        /// <summary>기둥 한 변. 골조가 방으로 안 읽힐 만큼 가늘어야 한다.</summary>
        public const float PostSize = 0.25f;

        /// <summary>
        /// 표를 그대로 다시 세운다. 확정·해제 뒤에 한 번만 부르는 자리다 — 커서를 옮길 때마다
        /// 돌리면 거점 전체를 지웠다 세우는 값을 매 프레임 문다(선체 쪽과 같은 규약).
        /// </summary>
        /// <param name="palette">벽 재질만 쓴다. 비면 프리미티브 기본 재질로 선다.</param>
        /// <returns>세운 구조물 수. 잔해 뿌리는 안 센다 — 지은 것이 아니다.</returns>
        public static int Rebuild(LastShiftModulePalette palette = null)
        {
            var root = EnsureRoot();
            var built = 0;

            for (var index = LastShiftOutpost.FixedCount; index < LastShiftOutpost.Count; index++)
            {
                Build(root, LastShiftOutpost.At(index), index, palette);
                built++;
            }

            return built;
        }

        /// <summary>
        /// 거점 칸을 찾거나 만든다. <b>칸 자체는 안 지운다</b> — 씬에서 누가 그
        /// <see cref="Transform"/> 을 물고 있을 수 있고, 매번 새로 만들면 그 참조가 끊긴다.
        /// 안에 있던 것은 전부 비운다.
        /// </summary>
        public static Transform EnsureRoot()
        {
            var existing = GameObject.Find(RootName);
            var root = existing != null
                ? existing.transform
                : new GameObject(RootName).transform;

            root.position = new Vector3(0f, LastShiftOutpost.DeckY, 0f);
            root.rotation = Quaternion.identity;

            for (var child = root.childCount - 1; child >= 0; child--)
                DestroyObject(root.GetChild(child).gameObject);

            return root;
        }

        private static void Build(
            Transform root, in LastShiftCompartmentSpec spec, int index, LastShiftModulePalette palette)
        {
            var piece = new GameObject($"Outpost_{index}_{LastShiftOutpost.NameOf(index)}");
            piece.transform.SetParent(root, false);
            piece.transform.localPosition = new Vector3(spec.CenterX, 0f, spec.CenterZ);

            var material = palette != null ? palette.WallMaterial : null;

            CreateCube(piece.transform, "Deck",
                new Vector3(0f, -DeckThickness * 0.5f, 0f),
                new Vector3(spec.LengthX, DeckThickness, spec.WidthZ), material);

            // 기둥 넷. 높이만 있고 면이 없어서 <b>안에 갇히지 않는다</b> — 진공에 세운 골조가
            // 사람을 가두면 산소가 마르는 자리가 하나 는다(조항 O-7).
            var halfX = (spec.LengthX - PostSize) * 0.5f;
            var halfZ = (spec.WidthZ - PostSize) * 0.5f;
            for (var corner = 0; corner < 4; corner++)
            {
                var x = (corner & 1) == 0 ? -halfX : halfX;
                var z = (corner & 2) == 0 ? -halfZ : halfZ;
                CreateCube(piece.transform, $"Post_{corner}",
                    new Vector3(x, LastShiftOutpost.FrameHeight * 0.5f, z),
                    new Vector3(PostSize, LastShiftOutpost.FrameHeight, PostSize), material);
            }
        }

        private static void CreateCube(
            Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = scale;
            if (material != null) cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null) return;

            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
