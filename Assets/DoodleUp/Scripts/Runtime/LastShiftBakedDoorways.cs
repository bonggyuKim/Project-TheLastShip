using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// <see cref="LastShiftBakedDoorways.Open"/> 한 번이 무엇을 했는지. <b>숫자 하나로 안 뭉친다</b> —
    /// "판을 잘랐다" 와 "이미 뚫려 있었다" 와 "벽 주인을 씬에서 못 찾았다" 는 서로 다른 일이고,
    /// 마지막 것만이 고칠 거리다(씬이 안 세워졌거나 이름 규약이 갈렸다).
    ///
    /// <see cref="Doorways"/> 는 언제나 <see cref="Cut"/> + <see cref="Clear"/> + <see cref="Missing"/> 다.
    /// </summary>
    public readonly struct LastShiftBakedDoorwayReport
    {
        public LastShiftBakedDoorwayReport(int doorways, int cut, int clear, int missing, int slabs)
        {
            Doorways = doorways;
            Cut = cut;
            Clear = clear;
            Missing = missing;
            Slabs = slabs;
        }

        /// <summary>훑은 모듈 문 수. 잠긴 모듈은 안 센다 — 잠긴 문은 구멍이 아니라 메운 판이다.</summary>
        public int Doorways { get; }

        /// <summary>판을 실제로 잘라 구멍을 낸 문 수.</summary>
        public int Cut { get; }

        /// <summary>벽 주인은 찾았는데 자를 판이 없던 문 수. 이미 뚫려 있었다는 뜻이다.</summary>
        public int Clear { get; }

        /// <summary>
        /// 벽 주인을 씬에서 못 찾은 문 수. <b>이 값이 <c>0</c> 이 아니면 그 모듈은 표에만 있고
        /// 걸어 들어갈 수는 없다.</b> 씬을 안 세우고 표만 만졌을 때가 대부분이다.
        /// </summary>
        public int Missing { get; }

        /// <summary>다시 자른 판 수. 문 하나가 판 여럿에 걸릴 수 있어 <see cref="Cut"/> 과 다르다.</summary>
        public int Slabs { get; }
    }

    /// <summary>
    /// <b>구운 벽에 모듈 문틀을 뚫는다.</b>
    ///
    /// 모듈이 <see cref="LastShiftCompartmentSpec.ParentIndex"/> 로 가리키는 상대가 선체이거나
    /// 고정 구획이면 그 벽은 배 프리팹에 이미 구워져 있고 구멍이 없다 — 조립기가 세운 모듈은
    /// 문 자리까지 이어지되 그 벽을 뚫고 들어갈 수 없었다(축 C 가
    /// <c>docs/tech/free-placement-module-assembly-v1.md</c> §6-1 에 남긴 항목). 자유 배치가
    /// 플레이로 확인되려면 이것이 먼저 있어야 한다.
    ///
    /// <b>메시를 안 자른다.</b> 구운 벽은 통짜 메시가 아니라 축 정렬 큐브 판 여러 장이고
    /// (씬 빌더 <c>CreateWallWithOpenings</c>), 판 한 장을 문 폭만큼 <b>다시 자르는</b> 것은
    /// 좌표 계산 몇 줄이다. 런타임 CSG 를 들이면 콜라이더·라이트맵 UV·머티리얼 슬롯이 전부
    /// 딸려 오고, 배치 해제에서 그것을 되돌릴 방법이 없다 — 여기서는 원본 판의 위치·크기를
    /// 적어 두고 <see cref="Restore"/> 로 그대로 돌린다.
    ///
    /// <b>부모가 모듈이어도 돌린다.</b> 그레이박스로 선 모듈은 자식 문 구멍을 조립기가 같이
    /// 뚫으므로 여기서 자를 판이 없고(<see cref="LastShiftBakedDoorwayReport.Clear"/>),
    /// 아트 프리팹으로 선 모듈은 그 프리팹도 구운 벽이라 여기서 뚫어야 한다. 둘을 안 가르는
    /// 것이 요지다 — 가르면 팔레트가 차는 날 "프리팹 모듈만 막힌 방" 이 조용히 생긴다.
    /// </summary>
    public static class LastShiftBakedDoorways
    {
        private const float Epsilon = 0.001f;

        /// <summary>
        /// 판으로 인정하는 최대 두께. 벽 판은 <see cref="LastShiftCompartments.PanelThickness"/>
        /// 정확히고, 여유를 두는 것은 아트가 판을 조금 두껍게 만들 자리를 남기는 것이다.
        /// 이보다 두꺼운 것은 벽이 아니라 방 안에 선 물건이므로 안 자른다.
        /// </summary>
        private const float MaxPanelThickness = LastShiftCompartments.PanelThickness * 1.5f;

        /// <summary>지금 열려 있는 구멍들. <see cref="Restore"/> 가 뒤에서부터 되돌린다.</summary>
        private static readonly List<Cut> cuts = new();

        /// <summary>되돌리지 않은 절단 수. 씬과 표가 어긋났는지 묻는 쪽이 본다.</summary>
        public static int CutCount => cuts.Count;

        /// <summary>
        /// 배치된 모듈 전부의 문을 벽 주인 쪽에 뚫는다. <b>먼저 되돌린다</b> — 표는 해제할 때
        /// 뒤 칸을 당기므로(<see cref="LastShiftCompartments.TryRemove"/>) 예전 구멍을 그대로
        /// 두면 아무 방도 안 붙은 자리에 구멍만 남는다. 조립기 <c>Rebuild</c> 가 씬을 통째로
        /// 다시 세우는 것과 같은 이유이고 같은 순서다.
        /// </summary>
        /// <param name="shipRoot">
        /// 선체 판과 구획 루트를 담은 칸. 조립기가 <c>PlacedModules</c> 를 매다는 칸과 같다.
        /// </param>
        public static LastShiftBakedDoorwayReport Open(Transform shipRoot)
        {
            if (shipRoot == null) throw new System.ArgumentNullException(nameof(shipRoot));

            Restore();

            var roots = CompartmentRootNames();
            var specs = LastShiftCompartments.Specs;
            int doorways = 0, cut = 0, clear = 0, missing = 0, slabs = 0;

            for (var index = LastShiftCompartments.FixedCount; index < specs.Length; index++)
            {
                ref readonly var spec = ref specs[index];
                if (!spec.IsPassable) continue;
                doorways++;

                var owner = ResolveOwner(shipRoot, spec, roots);
                if (owner == null)
                {
                    missing++;
                    continue;
                }

                var opened = OpenOne(shipRoot, owner, LastShiftDoorways.Of(spec), roots);
                slabs += opened;
                if (opened > 0) cut++;
                else clear++;
            }

            return new LastShiftBakedDoorwayReport(doorways, cut, clear, missing, slabs);
        }

        /// <summary>
        /// 뚫어 둔 구멍을 전부 메운다. 잘린 판은 원래 위치·크기·활성 상태로 돌아가고 새로
        /// 만든 조각은 지워진다. <b>뒤에서부터 되돌린다</b> — 같은 벽에 문이 둘이면 두 번째
        /// 절단이 첫 번째가 만든 조각을 잘랐을 수 있고, 앞에서부터 되돌리면 그 조각이 먼저
        /// 지워져 두 번째 기록이 허공을 가리킨다.
        /// </summary>
        /// <returns>되돌린 판 수.</returns>
        public static int Restore()
        {
            var restored = 0;
            for (var index = cuts.Count - 1; index >= 0; index--)
            {
                if (cuts[index].Undo()) restored++;
            }

            cuts.Clear();
            return restored;
        }

        // ── 벽 주인 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 이 모듈의 문이 놓인 면을 누가 세웠는가. 면 소유 규칙이 그대로다 — 구획은 자기
        /// 안쪽 문이 놓인 면을 안 세우고, 그 면은 부모(또는 선체)가 세운다.
        ///
        /// <b>그래서 부모에서 멈추면 안 된다.</b> 부모 자신의 문이 같은 평면에 있으면 부모도
        /// 그 면을 안 세웠고, 판은 조부모(끝까지 가면 선체)가 들고 있다. 한 칸만 보고 "부모
        /// 안에 자를 판이 없다" 로 끝내면 실제로는 막힌 방이 <see cref="LastShiftBakedDoorwayReport.Clear"/>
        /// 로 세어진다 — 이 함수에서 가장 틀리기 쉬운 자리다.
        /// </summary>
        private static Transform ResolveOwner(
            Transform shipRoot, in LastShiftCompartmentSpec spec, HashSet<string> roots)
        {
            var index = spec.ParentIndex;

            // 사슬 깊이는 상한이 있지만(maxDoorDepth) 여기서 그 값을 믿지 않는다 —
            // 표가 고리를 물면 이 순회가 안 끝나고, 그건 씬 조립 중에 멎는 배가 된다.
            for (var step = 0; step <= LastShiftCompartments.Count; step++)
            {
                if (index < 0) return shipRoot;
                if (index >= LastShiftCompartments.Count) return null;

                var owner = LastShiftCompartments.At(index);
                if (!SharesDoorPlane(owner, spec))
                    return FindRoot(shipRoot, LastShiftCompartments.NameOf(owner), roots);

                index = owner.ParentIndex;
            }

            return null;
        }

        /// <summary>
        /// 이 구획의 안쪽 문이 그 모듈 문과 같은 평면에 있는가. 같으면 이 구획은 그 면을
        /// 안 세웠다(씬 빌더·조립기의 <c>IsOwnDoorFace</c> 와 같은 판정).
        /// </summary>
        private static bool SharesDoorPlane(
            in LastShiftCompartmentSpec owner, in LastShiftCompartmentSpec spec) =>
            owner.DoorPlane == spec.DoorPlane &&
            Mathf.Abs(owner.DoorPlaneCoordinate - spec.DoorPlaneCoordinate) < Epsilon;

        /// <summary>
        /// 구획 루트를 이름으로 찾는다. <b>다른 구획 안으로는 안 들어간다</b> — 구획 루트는
        /// 서로 겹쳐 안 담기므로 그 안을 뒤질 이유가 없고, 안 막으면 선체를 주인으로 받은
        /// 문이 구획 안 판까지 훑는다.
        /// </summary>
        private static Transform FindRoot(Transform node, string name, HashSet<string> roots)
        {
            for (var index = 0; index < node.childCount; index++)
            {
                var child = node.GetChild(index);
                if (child.name == name) return child;
                if (roots.Contains(child.name)) continue;

                var found = FindRoot(child, name, roots);
                if (found != null) return found;
            }

            return null;
        }

        private static HashSet<string> CompartmentRootNames()
        {
            var specs = LastShiftCompartments.Specs;
            var names = new HashSet<string>();
            for (var index = 0; index < specs.Length; index++)
                names.Add(LastShiftCompartments.NameOf(specs[index]));
            return names;
        }

        // ── 절단 ────────────────────────────────────────────────────────────

        /// <summary>문 하나를 뚫는다. 돌려주는 값은 다시 자른 판 수다.</summary>
        private static int OpenOne(
            Transform shipRoot, Transform owner, in LastShiftDoorway door, HashSet<string> roots)
        {
            // 자르면서 새 조각이 생기고, 같은 벽의 다음 문이 그 조각을 물 수 있다. 그래서
            // 후보를 미리 다 모아 두고 조각을 뒤에 붙인다 — 훑는 중에 계층을 고치면
            // 자식 인덱스가 밀려 판 하나를 건너뛴다.
            var candidates = new List<Transform>();
            Collect(owner, candidates, roots);

            var slabs = 0;
            for (var index = 0; index < candidates.Count; index++)
            {
                if (TryCut(shipRoot, candidates[index], door, candidates)) slabs++;
            }

            return slabs;
        }

        /// <summary>
        /// 벽 주인 아래의 판 후보를 모은다. <b>다른 구획 루트에서 멈춘다</b> — 선체를 주인으로
        /// 받은 문이 구획 안까지 내려가면 방 안 소품이 후보가 된다.
        /// </summary>
        private static void Collect(Transform node, List<Transform> into, HashSet<string> roots)
        {
            for (var index = 0; index < node.childCount; index++)
            {
                var child = node.GetChild(index);
                into.Add(child);
                if (roots.Contains(child.name)) continue;
                Collect(child, into, roots);
            }
        }

        /// <summary>
        /// 이 판이 문을 막고 있으면 다시 자른다.
        ///
        /// <b>무엇을 판으로 볼지가 이 함수의 전부다.</b> 벽과 소품은 둘 다 큐브라 이름으로
        /// 가르면 씬 빌더의 문자열 규약이 런타임 계약이 된다. 대신 넷을 다 만족하는 것만
        /// 자른다 — 축에 정렬돼 있고, 콜라이더로 <b>실제로 막고</b> 있고, 판 두께이고,
        /// 바닥에 서서 문 구멍 높이에 걸린다. 콜라이더를 넣은 것이 요점이다: 갑판 띠·격자
        /// 같은 장식은 콜라이더가 없으므로(씬 빌더 <c>CreateDecorCube</c>) 승무원을 안 막고,
        /// 안 막는 것을 자르면 문 앞 갑판 표시에 구멍이 난다.
        /// </summary>
        private static bool TryCut(
            Transform shipRoot, Transform slab, in LastShiftDoorway door, List<Transform> discovered)
        {
            if (slab.childCount != 0) return false;
            if (!TryLocalBox(shipRoot, slab, out var center, out var size)) return false;

            var collider = slab.GetComponent<Collider>();
            if (collider == null || !collider.enabled) return false;

            var throughHalf = door.ThroughSizeOf(size) * 0.5f;
            if (throughHalf * 2f > MaxPanelThickness + Epsilon) return false;

            var through = door.ThroughOf(center);
            if (Mathf.Abs(through - door.Plane) > throughHalf + Epsilon) return false;

            var bottom = center.y - size.y * 0.5f;
            var top = center.y + size.y * 0.5f;
            if (bottom > Epsilon) return false;
            if (top <= Epsilon) return false;

            var free = door.FreeOf(center);
            var freeHalf = door.FreeSizeOf(size) * 0.5f;
            var freeMin = free - freeHalf;
            var freeMax = free + freeHalf;
            if (freeMin >= door.MaxFree - Epsilon || freeMax <= door.MinFree + Epsilon) return false;

            Split(shipRoot, slab, door, center, size, freeMin, freeMax, bottom, top, discovered);
            return true;
        }

        /// <summary>
        /// 판 한 장을 문 구멍만큼 비우고 남는 조각으로 다시 세운다. 남는 것은 최대 셋 —
        /// 구멍 양옆과 그 위 인방이다. 인방을 안 남기면 문 높이(<c>2.2</c>)에서 천장까지
        /// 그대로 뚫려 그림과 통행 가능 범위가 어긋난다(씬 빌더와 같은 규칙).
        ///
        /// 첫 조각은 <b>원본 판을 옮겨</b> 쓴다. 조각을 전부 새로 만들고 원본을 지우면 씬에서
        /// 그 판을 참조로 물고 있는 쪽이 끊기고, 되돌릴 때 같은 판이 아닌 것이 선다.
        /// </summary>
        private static void Split(
            Transform shipRoot, Transform slab, in LastShiftDoorway door,
            Vector3 center, Vector3 size, float freeMin, float freeMax, float bottom, float top,
            List<Transform> discovered)
        {
            const float doorTop = LastShiftZoneDoor.OpeningHeight;

            var pieces = new List<(float FreeMin, float FreeMax, float Bottom, float Top)>(3);
            if (door.MinFree - freeMin > Epsilon) pieces.Add((freeMin, door.MinFree, bottom, top));
            if (freeMax - door.MaxFree > Epsilon) pieces.Add((door.MaxFree, freeMax, bottom, top));

            var lintelMin = Mathf.Max(freeMin, door.MinFree);
            var lintelMax = Mathf.Min(freeMax, door.MaxFree);
            if (top - doorTop > Epsilon && lintelMax - lintelMin > Epsilon)
                pieces.Add((lintelMin, lintelMax, Mathf.Max(bottom, doorTop), top));

            var record = new Cut(slab, pieces.Count - 1);
            for (var index = 1; index < pieces.Count; index++)
            {
                var clone = Object.Instantiate(slab.gameObject, slab.parent, false);
                clone.name = $"{slab.name}_Cut{index}";
                record.SetPiece(index - 1, clone);
                discovered.Add(clone.transform);
                Place(shipRoot, clone.transform, door, center, size, pieces[index]);
            }

            if (pieces.Count == 0) slab.gameObject.SetActive(false);
            else Place(shipRoot, slab, door, center, size, pieces[0]);

            cuts.Add(record);
        }

        /// <summary>
        /// 조각 하나를 자리에 놓는다. 문을 지나는 축은 원본 그대로이고 자유축·높이만 바뀐다.
        ///
        /// 크기를 <b>비율로</b> 고친다 — 배 로컬 크기에서 <c>localScale</c> 을 되짚으면 부모
        /// 스케일을 한 번 더 나눠야 하고, 그 나눗셈이 하나라도 빠지면 판이 배 밖으로 자란다.
        /// </summary>
        private static void Place(
            Transform shipRoot, Transform piece, in LastShiftDoorway door,
            Vector3 center, Vector3 size, (float FreeMin, float FreeMax, float Bottom, float Top) rect)
        {
            var alongX = door.PlaneAxis == LastShiftDoorPlane.AlongX;
            var freeCenter = (rect.FreeMin + rect.FreeMax) * 0.5f;
            var freeSize = rect.FreeMax - rect.FreeMin;

            var target = alongX
                ? new Vector3(center.x, (rect.Bottom + rect.Top) * 0.5f, freeCenter)
                : new Vector3(freeCenter, (rect.Bottom + rect.Top) * 0.5f, center.z);

            var scale = piece.localScale;
            var ratioFree = freeSize / door.FreeSizeOf(size);
            var ratioY = (rect.Top - rect.Bottom) / size.y;
            piece.localScale = alongX
                ? new Vector3(scale.x, scale.y * ratioY, scale.z * ratioFree)
                : new Vector3(scale.x * ratioFree, scale.y * ratioY, scale.z);
            piece.position = shipRoot.TransformPoint(target);
        }

        /// <summary>
        /// 판의 배 로컬 축 정렬 상자. <b>돌아간 것은 안 본다</b> — 원반 외피 테두리처럼 비스듬히
        /// 선 판은 축 정렬 상자로 잴 수 없고, 문 평면은 전부 축에 정렬돼 있으므로 그런 판은
        /// 애초에 문틀 자리에 안 온다.
        ///
        /// <b>피벗이 메시 한가운데 있어야 한다.</b> 조각을 놓을 때 <c>position</c> 을 상자
        /// 중심으로 잡는데, 피벗이 치우친 메시는 <c>localScale</c> 을 바꾸면 상자 중심이
        /// 피벗에서 따로 움직인다 — 그 경우를 여기서 거르지 않으면 조각이 문 옆으로 밀려 서고,
        /// 씬을 봐야만 드러난다. 구운 판은 전부 <c>CreatePrimitive(Cube)</c> 라 이 조건을 만족한다.
        /// </summary>
        private static bool TryLocalBox(
            Transform shipRoot, Transform slab, out Vector3 center, out Vector3 size)
        {
            center = default;
            size = default;

            var filter = slab.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;
            if (Quaternion.Angle(shipRoot.rotation, slab.rotation) > 0.01f) return false;

            var bounds = filter.sharedMesh.bounds;
            if (bounds.center.sqrMagnitude > Epsilon * Epsilon) return false;

            var lossy = slab.lossyScale;
            var shipScale = shipRoot.lossyScale;
            if (Mathf.Abs(shipScale.x) < Epsilon ||
                Mathf.Abs(shipScale.y) < Epsilon ||
                Mathf.Abs(shipScale.z) < Epsilon) return false;

            center = shipRoot.InverseTransformPoint(slab.TransformPoint(bounds.center));
            size = new Vector3(
                Mathf.Abs(bounds.size.x * lossy.x / shipScale.x),
                Mathf.Abs(bounds.size.y * lossy.y / shipScale.y),
                Mathf.Abs(bounds.size.z * lossy.z / shipScale.z));
            return size.x > Epsilon && size.y > Epsilon && size.z > Epsilon;
        }

        // ── 기록 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 판 한 장을 다시 자른 기록. 원본의 위치·크기·활성 상태와 새로 만든 조각을 든다.
        /// </summary>
        private readonly struct Cut
        {
            private readonly Transform slab;
            private readonly Vector3 localPosition;
            private readonly Vector3 localScale;
            private readonly bool active;
            private readonly GameObject[] pieces;

            public Cut(Transform slab, int pieceCount)
            {
                this.slab = slab;
                localPosition = slab.localPosition;
                localScale = slab.localScale;
                active = slab.gameObject.activeSelf;
                pieces = pieceCount > 0 ? new GameObject[pieceCount] : System.Array.Empty<GameObject>();
            }

            public void SetPiece(int index, GameObject piece) => pieces[index] = piece;

            /// <summary>
            /// 되돌린다. <b>판이 이미 사라졌어도 조각은 지운다</b> — 조립기가 모듈을 걷어내면
            /// 그 안의 판이 같이 사라지는데, 거기서 멈추면 조각만 남아 씬에 뜬 판이 선다.
            /// </summary>
            public bool Undo()
            {
                foreach (var piece in pieces)
                    if (piece != null) DestroyObject(piece);

                if (slab == null) return false;

                slab.localPosition = localPosition;
                slab.localScale = localScale;
                slab.gameObject.SetActive(active);
                return true;
            }
        }

        /// <summary>
        /// 에디터에서는 <c>Destroy</c> 가 프레임 끝까지 미뤄져 같은 프레임에 다시 자르면
        /// 지운 조각이 아직 서 있다. EditMode 테스트가 도는 자리이므로 갈라 둔다 —
        /// 조립기 <c>DestroyObject</c> 와 같은 이유다.
        /// </summary>
        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
