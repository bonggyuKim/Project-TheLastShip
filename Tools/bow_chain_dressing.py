"""선수 사슬 4개 방(화물칸·격납고·정비창·관측실) 드레싱 — 프리팹 6종 + 배치.

`#88367814` 가 이 넷을 `Locked -> Open` 으로 바꿨고, 그 카드가 "그 외 드레싱은 별도
art 카드" 로 남긴 자리다(확장 검토 §4-1).

에셋 생성기다. 런타임/에디터 코드는 안 건드린다 — 프리팹 YAML 과
`LastShiftDressingSet.asset` 의 항목만 만든다(`docs/scene-dressing-authoring.md`).
프리팹 작성기·좌표 풀이·YAML 조립은 `ct11_background_density.py` 를 그대로 import 해
쓴다. 같은 계산을 두 번 적으면 두 스크립트가 서로 다른 자리에 소품을 놓게 된다.

`ct11` 과 다른 것은 셋이다.

1. **공간이 구획(`kind=1`)이다.** 구획 천장은 `3.0m` 로 구역(`3.2m`)보다 낮아서
   천장 소품 높이를 구역 값으로 복사하면 천장을 뚫는다.
2. **문이 방마다 여럿이다.** 화물칸은 문이 넷(선체·정비창·격납고·관측 회랑),
   격납고는 둘(화물칸·상부 회랑)이다. 잠겨 있을 때 놓인 소품들이 지금 그 문턱
   위에 서 있고, 이 스크립트가 그걸 옮긴다(`MOVES`).
3. **기존 항목을 옮긴다.** `ct11` 은 추가만 했다. 여기서는 `(공간, id)` 가 같은
   기존 블록을 새 좌표로 갈아 끼운다 — id 를 유지해야 `SyncMissingProps` 가
   나중에 같은 항목을 다시 만들지 않는다.

실행: python Tools/bow_chain_dressing.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ct11_background_density as ct11  # noqa: E402  (좌표 풀이·프리팹 작성기 재사용)

from ct11_background_density import (  # noqa: E402
    CUBE, CYL, MAT, SET_ASSET, block_key, f, overlaps, part,
    read_prefab_ref, solve_anchor, split_prop_blocks, write_prefab,
)

# ── 치수 정본 미러 (LastShiftCompartments.BuildSpecs) ────────────────────────
BOW = -19.0                     # -LastShiftShipDimensions.HalfLength
CEIL_C = 3.0                    # LastShiftCompartments.InteriorHeight
DOOR_W = 1.6                    # LastShiftZoneDoor.OpeningWidth
DOOR_H = 2.2                    # LastShiftZoneDoor.OpeningHeight

# 이름 -> (enum 값, minX, maxX, minZ, maxZ)
ROOM = {
    "Observatory": (0, BOW - 16, BOW - 13, -2.0, 2.0),      # -35~-32, -2~+2   12m²
    "Workshop":    (1, BOW - 13, BOW - 8, -2.5, 2.5),       # -32~-27          25m²
    "CargoBay":    (2, BOW - 8, BOW, -4.0, 4.0),            # -27~-19          64m²
    "Hangar":      (3, BOW - 8, BOW, 4.0, 14.0),            # -27~-19, +4~+14  80m²
}

# 상부 회랑 안쪽 면 = max(서버실 +9, 수경재배 +9, 의무실 +8) + Clearance 1 = +10.
GALLERY_NEAR_Z = 10.0
GALLERY_CENTER_Z = GALLERY_NEAR_Z + 2.0 * 0.5      # 회랑 폭 2m 의 중심 = +11

# 문 앞 여유. `ct11` 은 구역 개구부에 `1.5m` 를 썼고 여기도 같다.
DOOR_CLEAR = 1.5
# 관측실만 예외다 — 방 깊이가 `3m` 라 `1.5` 를 파면 방의 절반이 비어야 하고,
# 막다른 방이라 이 문 앞은 "지나가는 자리" 가 아니라 "들어와서 서는 자리" 다.
DEAD_END_CLEAR = 1.0
DOOR_HALF = DOOR_W / 2 + 0.3    # 문폭 1.6 + 양옆 0.3

# (방, 문이 놓인 축, 면 좌표, 자유축 중심, 안쪽 방향, 여유 깊이)
#   axis "x" = x 평면에 놓인 문(자유축이 z), "z" = 그 반대.
DOORS = [
    ("Observatory", "x", BOW - 13, 0.0, -1, DEAD_END_CLEAR),   # 정비창 쪽. 유일한 문
    ("Workshop",    "x", BOW - 13, 0.0, +1, DOOR_CLEAR),       # 관측실 쪽
    ("Workshop",    "x", BOW - 8, 0.0, -1, DOOR_CLEAR),        # 화물칸 쪽
    ("CargoBay",    "x", BOW - 8, 0.0, +1, DOOR_CLEAR),        # 정비창 쪽
    ("CargoBay",    "x", BOW, 0.0, -1, DOOR_CLEAR),            # 선체 주 통로
    ("CargoBay",    "z", 4.0, BOW - 4, -1, DOOR_CLEAR),        # 격납고 쪽
    ("CargoBay",    "z", -4.0, BOW - 4, +1, DOOR_CLEAR),       # 관측 회랑 (§29.4-(2))
    ("Hangar",      "z", 4.0, BOW - 4, +1, DOOR_CLEAR),        # 화물칸 쪽
    ("Hangar",      "x", BOW, GALLERY_CENTER_Z, -1, DOOR_CLEAR),  # 상부 회랑 종점
]


def bounds(room):
    _, x0, x1, z0, z1 = ROOM[room]
    return (x0, x1, z0, z1, 0.0, CEIL_C)


def door_boxes():
    """문 앞 여유 상자. 어떤 소품도 여기 못 들어온다."""
    out = []
    for index, (room, axis, plane, center, inward, depth) in enumerate(DOORS):
        lo, hi = sorted((plane, plane + inward * depth))
        if axis == "x":
            out.append((f"{room}_Door{index}", lo, hi, 0.0, DOOR_H,
                        center - DOOR_HALF, center + DOOR_HALF))
        else:
            out.append((f"{room}_Door{index}", center - DOOR_HALF, center + DOOR_HALF,
                        0.0, DOOR_H, lo, hi))
    return out


def label_boxes():
    """구획 라벨(`CreateCompartmentLabel`). 선수 쪽 벽 안쪽 `y 2.25` 에 글자가 붙는다.

    `TextMesh` 라 실제 폭은 글자 수를 따르고 여기서 정확히 못 잰다. 벽에 붙는 소품이
    라벨을 가리지 않게 넉넉한 상자로 잡는다 — 틀리면 넓은 쪽으로 틀리게 둔다.
    """
    out = []
    for room in ROOM:
        _, x0, x1, z0, _ = ROOM[room]
        cx = (x0 + x1) / 2
        out.append((f"{room}_Label", cx - 1.4, cx + 1.4, 1.90, 2.60, z0, z0 + 0.30))
    return out


# ── 신규 프리팹 6 ────────────────────────────────────────────────────────────
# 루트 원점은 밑면·xz 중심(assets-v1 §2), 정면은 +z. 전부 원시 도형이다.
# 재질은 assets-v1 §6 명도 사다리를 그대로 쓴다 — 새 재질을 안 만든다.

def hangar_tug():
    """격납고 크래들 위의 소형 작업정. **이 방의 주어다.**

    격납고에 지금 있는 것은 빈 크래들 둘과 유도선 둘이다. 발진 구역을 비워 두는
    것은 맞지만(§`AddCompartmentProps`), 그러면 방이 "무언가를 넣어 두는 곳" 이
    아니라 그냥 넓은 빈 방으로 읽힌다. 하나만 올린다 — 선미 크래들은 비운 채로
    둔다. 둘 중 하나가 비어 있는 것이 "한 대는 나가서 안 돌아왔다" 로 읽힌다.
    """
    p = [
        part("Hull", "LSD_Locker", (0, 0.55, 0), (2.10, 0.62, 0.96)),
        part("Nose", "LSD_Locker", (1.32, 0.52, 0), (0.66, 0.48, 0.70)),
        part("Canopy", "LSD_Glass", (0.72, 0.94, 0), (0.78, 0.30, 0.62)),
        part("Spine", "LS_Fixture", (-0.20, 0.92, 0), (1.30, 0.14, 0.30)),
        part("TailFin", "LSD_Locker", (-1.16, 0.92, 0), (0.40, 0.60, 0.10)),
        part("GrappleArm", "LS_Fixture", (0.30, 1.14, 0), (1.10, 0.12, 0.12)),
        part("GrappleHead", "LS_Fixture", (0.90, 1.14, 0), (0.16, 0.20, 0.24)),
        part("Stripe", "LSD_CrateTrim", (0, 0.82, 0.481), (1.90, 0.06, 0.02)),
    ]
    for i, z in enumerate((-0.60, 0.60)):
        p.append(part(f"Pod_{i}", "LSD_Conduit", (-0.72, 0.62, z), (0.86, 0.36, 0.36)))
        p.append(part(f"PodMount_{i}", "LS_Fixture", (-0.72, 0.55, z * 0.62), (0.30, 0.10, 0.34)))
        p.append(part(f"Skid_{i}", "LS_Fixture", (0.10, 0.06, z * 0.72), (1.70, 0.12, 0.12)))
        p.append(part(f"SkidLeg_{i}", "LS_Fixture", (0.10, 0.20, z * 0.72), (0.10, 0.28, 0.10)))
    return p


def cargo_net_bay():
    """벽면 화물 그물 베이. 화물칸 벽이 지금 통짜 회색이다."""
    p = [
        part("Backing", "LSD_Mat", (0, 1.00, -0.19), (1.80, 2.00, 0.04)),
        part("Rail_Top", "LS_Fixture", (0, 1.94, -0.06), (1.76, 0.10, 0.22)),
        part("Rail_Mid", "LS_Fixture", (0, 1.00, -0.06), (1.76, 0.08, 0.22)),
        part("Rail_Bottom", "LS_Fixture", (0, 0.06, -0.06), (1.76, 0.10, 0.22)),
    ]
    for i, x in enumerate((-0.58, 0.0, 0.58)):
        p.append(part(f"Bundle_Upper_{i}", "LSD_Fabric", (x, 1.46, -0.02), (0.50, 0.80, 0.28)))
        p.append(part(f"Bundle_Lower_{i}", "LSD_Crate", (x, 0.52, -0.02), (0.50, 0.82, 0.30)))
        p.append(part(f"Strap_{i}", "LSD_CrateTrim", (x, 1.00, 0.185), (0.05, 1.92, 0.02)))
    for i, y in enumerate((0.52, 1.46)):
        p.append(part(f"StrapSpan_{i}", "LSD_CrateTrim", (0, y, 0.185), (1.72, 0.05, 0.02)))
    for i, x in enumerate((-0.87, 0.87)):
        p.append(part(f"Post_{i}", "LS_Fixture", (x, 1.00, -0.06), (0.06, 2.00, 0.24)))
    return p


def pallet_jack():
    """팔레트 잭. 화물칸에 <b>움직이는 물건</b>이 하나도 없다."""
    p = [
        part("Body", "LSD_Locker", (0, 0.22, -0.16), (0.46, 0.40, 0.26)),
        part("Pump", "LS_Fixture", (0, 0.50, -0.16), (0.16, 0.24, 0.16)),
        part("Handle", "LS_Fixture", (0, 0.74, -0.28), (0.10, 0.62, 0.10), (26, 0, 0)),
        part("Grip", "LSD_CrateTrim", (0, 1.00, -0.40), (0.34, 0.08, 0.08)),
    ]
    for i, z in enumerate((-0.16, 0.16)):
        p.append(part(f"Fork_{i}", "LS_Fixture", (0.10, 0.09, z), (0.90, 0.09, 0.16)))
        p.append(part(f"ForkTip_{i}", "LS_Fixture", (0.52, 0.05, z), (0.16, 0.05, 0.14)))
        p.append(part(f"Wheel_{i}", "LSD_Mat", (0.48, 0.05, z), (0.10, 0.05, 0.10), mesh=CYL))
        p.append(part(f"Caster_{i}", "LSD_Mat", (-0.20, 0.06, z * 1.1), (0.12, 0.06, 0.12), mesh=CYL))
    return p


def parts_bin_shelf():
    """부품 통 선반. 정비창·화물칸이 "쓰이는 방" 으로 읽히는 자리다."""
    p = [
        part("Backing", "LSD_Mat", (0, 0.92, -0.19), (1.50, 1.84, 0.04)),
    ]
    for i, x in enumerate((-0.71, 0.71)):
        p.append(part(f"Upright_{i}", "LS_Fixture", (x, 0.92, 0), (0.08, 1.84, 0.40)))
    for tier in range(4):
        y = 0.30 + tier * 0.46
        p.append(part(f"Shelf_{tier}", "LS_Fixture", (0, y, 0), (1.42, 0.05, 0.40)))
        for i, x in enumerate((-0.46, 0.0, 0.46)):
            p.append(part(f"Bin_{tier}_{i}", "LSD_Crate", (x, y + 0.14, 0.04), (0.40, 0.23, 0.30)))
            p.append(part(f"BinLip_{tier}_{i}", "LSD_CrateTrim", (x, y + 0.24, 0.185), (0.36, 0.04, 0.02)))
    return p


def gas_cylinder_bank():
    """용접 가스 실린더 4연 + 매니폴드. 정비창의 "제작" 과 격납고의 정비를 같이 말한다.

    실린더는 원기둥 메시라 `scale.y` 가 반높이다(유니티 기본 Cylinder 는 높이 `2`).
    바깥 치수는 <see cref="bbox_of"/> 가 그 배수를 되돌려 계산한다.
    """
    p = [
        part("BasePlate", "LSD_Mat", (0, 0.03, 0), (1.20, 0.06, 0.40)),
        part("Manifold", "LSD_Conduit", (0, 1.30, -0.10), (1.12, 0.10, 0.10)),
        part("Regulator", "LS_Fixture", (0.46, 1.42, -0.10), (0.16, 0.14, 0.14)),
        part("ChainUpper", "LSD_CrateTrim", (0, 1.06, 0.17), (1.16, 0.05, 0.03)),
        part("ChainLower", "LSD_CrateTrim", (0, 0.46, 0.17), (1.16, 0.05, 0.03)),
    ]
    for i, x in enumerate((-0.45, -0.15, 0.15, 0.45)):
        p.append(part(f"Cylinder_{i}", "LSD_Conduit", (x, 0.63, 0), (0.26, 0.60, 0.26), mesh=CYL))
        p.append(part(f"Neck_{i}", "LS_Fixture", (x, 1.30, 0), (0.10, 0.07, 0.10), mesh=CYL))
        p.append(part(f"Band_{i}", "LSD_ConduitBand", (x, 1.10, 0), (0.28, 0.05, 0.28), mesh=CYL))
    return p


def personal_shelf():
    """관측실 개인 물품 선반 — 머그·담요·기록장.

    관측실은 기능이 없는 방이다(확장 검토 §3, "P0 에서는 드레싱만"). 그러면 이 방이
    왜 있는지를 말하는 것은 <b>누가 여기 온다</b>는 흔적뿐이고, 그 흔적이 이거다.
    작다 — 눈높이 아래(`y 1.10~1.72`)에 붙여 창 쪽 시선을 안 막는다.
    """
    p = [
        part("Board", "LSD_Mat", (0, 0.28, -0.11), (0.72, 0.56, 0.03)),
        part("Shelf", "LS_Fixture", (0, 0.06, 0), (0.68, 0.04, 0.24)),
        part("Bracket_Port", "LS_Fixture", (-0.28, 0.03, -0.06), (0.03, 0.10, 0.12)),
        part("Bracket_Starboard", "LS_Fixture", (0.28, 0.03, -0.06), (0.03, 0.10, 0.12)),
        part("Lip", "LSD_ConduitBand", (0, 0.11, 0.11), (0.68, 0.06, 0.02)),
        part("Mug", "LSD_Ceramic", (-0.24, 0.14, 0.01), (0.09, 0.06, 0.09), mesh=CYL),
        part("Blanket", "LSD_Fabric", (0.14, 0.20, 0.0), (0.34, 0.24, 0.20)),
        part("Notebook", "LSD_Crate", (-0.06, 0.11, 0.02), (0.16, 0.03, 0.22)),
        part("Rail", "LS_Fixture", (0, 0.52, -0.09), (0.66, 0.04, 0.04)),
    ]
    return p


# 새 재질은 안 만들었다. 여기 쓰는 것 중 ct11 표에 없는 둘만 더한다.
MAT.setdefault("LSD_Glass", "d0d37c665cd18990c1bca177655d6824")
MAT.setdefault("LSD_Ceramic", "9c0e9e2a9056a046485ae4fbde2fc926")

PREFABS = [
    ("LSDress_HangarTug", "3f1c9a2b7e604d8fa15c3d0b62e7481a", hangar_tug, True),
    ("LSDress_CargoNetBay", "b48e15c0d9a24f7cb2e6083a5d17cf92", cargo_net_bay, False),
    ("LSDress_PalletJack", "6c2a70f4b1d84e39ad50c7e2f9b3164d", pallet_jack, True),
    ("LSDress_PartsBinShelf", "9d5f38a6c07b4e12b8a3ef4170c962d8", parts_bin_shelf, True),
    ("LSDress_GasCylinderBank", "2e7b41d95f3c40a6ba18d9c07e35f4b1", gas_cylinder_bank, True),
    ("LSDress_PersonalShelf", "af06d283419c4b57902e6d1c85f37b40", personal_shelf, False),
]


def bbox_of(parts):
    """바깥 경계. `ct11.prefab_bbox` 와 달리 원기둥 높이를 `2×scale.y` 로 센다.

    유니티 기본 Cylinder 는 `scale 1` 에서 높이가 `2` 다. `ct11` 은 밸브·볼트처럼
    본체에 파묻히는 것에만 원기둥을 써서 이 배수가 표에 안 드러났지만, 여기 가스
    실린더는 원기둥이 곧 바깥 실루엣이라 `scale` 을 그대로 치수로 쓰면 `0.6m` 짜리
    통이 `1.2m` 로 서고 경계 검사가 절반짜리 상자를 본다.
    """
    lo = [1e9] * 3
    hi = [-1e9] * 3
    for pt in parts:
        scale = list(pt["scale"])
        if pt["mesh"] == CYL:
            scale[1] *= 2.0
        ext = ct11.rotated_extent(scale, pt["euler"])
        for i in range(3):
            lo[i] = min(lo[i], pt["pos"][i] - ext[i])
            hi[i] = max(hi[i], pt["pos"][i] + ext[i])
    return lo, hi


# ── 배치 ────────────────────────────────────────────────────────────────────
# 규칙 넷.
#  1. 문 앞 여유(`DOOR_CLEAR`)를 절대 안 밟는다. 넷 다 이제 <b>실제로 열린 문</b>이다
#  2. 관통 동선을 안 막는다 — 정비창은 관측실↔화물칸, 화물칸은 두 축이 다 관통이다
#  3. 천장은 `3.0m` 다. 구역(`3.2`) 값을 복사하지 않는다
#  4. 부피가 있는 것은 벽 쪽, 통로에는 벽·천장에 붙는 것만

TRAY_Y = 2.66       # 천장 케이블 트레이 밑면 (트레이 0.15 -> 윗면 2.81, 등기구 2.84 아래)
JUNC_Y = 2.20       # 벽 상부 배관 접합
PANEL_Y = 1.25      # 벽 점검 패널
NET_Y = 1.30        # 그물 수납
SPOT_Y = CEIL_C - 0.34   # 작업 스팟 밑면(등기구 0.34)

Z_LOW, Z_HIGH = "zlow", "zhigh"     # z 최소/최대 벽
X_LOW, X_HIGH = "xlow", "xhigh"     # x 최소(선수)/최대(선미) 벽

KIT = {
    "PanelBank": "LSDress_Kit_PanelBank",
    "ConduitJunction": "LSDress_Kit_ConduitJunction",
    "CableTray": "LSDress_Kit_CableTray",
    "DeckGrate": "LSDress_Kit_DeckGrate",
    "RibFrame": "LSDress_Kit_RibFrame",
    "StowageNet": "LSDress_Kit_StowageNet",
}

LAYOUT = []


def add(room, pid, prefab, x, z, bottom_y=0.0, euler=(0, 0, 0), clearance=0.06,
        light=0.0, allow=()):
    """새 항목 하나. `x`/`z` 는 원하는 월드 좌표 또는 벽 지정자다."""
    LAYOUT.append(dict(room=room, id=pid, prefab=prefab, x=x, z=z, bottom_y=bottom_y,
                       euler=euler, clearance=clearance, light=light, allow=tuple(allow),
                       move=False))


def move(room, pid, prefab, x, z, bottom_y=0.0, euler=(0, 0, 0), clearance=0.06,
         light=0.0, allow=()):
    """<b>이미 에셋에 있는</b> 항목을 새 좌표로 갈아 끼운다. id 는 그대로 둔다."""
    LAYOUT.append(dict(room=room, id=pid, prefab=prefab, x=x, z=z, bottom_y=bottom_y,
                       euler=euler, clearance=clearance, light=light, allow=tuple(allow),
                       move=True))


# ── 관측실 (3×4, 12m², 25 lx) ────────────────────────────────────────────────
# 배에서 가장 어둡고 가장 작은 방이다. 밀도를 올리지 않는다 — 다섯만 더한다.
#
# 별표(StarChart)를 옮기는 이유. 이 프리팹은 z 로 얇은 판(`1.50 × 1.10 × 0.06`)
# 인데 앵커가 `x = -1`(선수 끝벽)이라, 회전 없이 x 벽에 밀면 판이 벽에 붙는 게
# 아니라 벽에서 `0.75m` 튀어나와 방 한가운데 z ≈ +0.57 에 떠 선다. 잠겨 있을
# 때는 아무도 못 봤다. `euler y=90` 을 주고 치수를 축정렬 값으로 다시 적는다.
move("Observatory", "StarChart", "LSDress_StarChart", X_LOW, 0.0,
     bottom_y=1.0, euler=(0, 90, 0))

add("Observatory", "PersonalShelf", "LSDress_PersonalShelf", -34.30, Z_HIGH, bottom_y=1.10)
add("Observatory", "RibFrame_Starboard", "RibFrame", -33.00, Z_HIGH, clearance=0.16)
add("Observatory", "CableTray", "CableTray", -33.50, 1.10, bottom_y=TRAY_Y)
add("Observatory", "DeckGrate", "DeckGrate", -34.20, 0.90, bottom_y=0.001)
add("Observatory", "StowageNet_Port", "StowageNet", -33.60, Z_LOW, bottom_y=1.00, clearance=0.16)

# ── 정비창 (5×5, 25m², 450 lx) ───────────────────────────────────────────────
# 양 끝 x 면이 둘 다 문이라 이제 <b>관통 통로</b>다(관측실↔화물칸). z ≈ 0 의 x
# 동선을 비워 둬야 하는데 부품 팔레트가 정확히 거기 있었다 — 문 앞 `1.5m` 규칙에는
# 안 걸리지만(양 문에서 각각 `1.5m` 바깥이다) 5m 방을 가로지르는 유일한 직선 위다.
# 우현 쪽으로 물린다.
move("Workshop", "PartsPallet", "LSDress_PartsPallet", -29.30, 1.15)

# 작업 스팟은 하나만 단다. 이 방은 상시등이 이미 `7.74`(`450 lx`, 배에서 최대)라
# 스팟 둘을 더하면 국소 대비가 아니라 그냥 전체가 더 밝아진다 — §3.2 가 "명암 대비"
# 를 스팟의 존재 이유로 적었으므로 대비를 지우는 개수는 그 근거를 뒤집는다.
add("Workshop", "TaskSpot_Bench_Port", "LSDress_TaskSpot", -29.50, -1.90,
    bottom_y=SPOT_Y, light=4.2)
add("Workshop", "PanelBank_Starboard", "PanelBank", -29.50, Z_HIGH, bottom_y=PANEL_Y)
add("Workshop", "GasCylinderBank", "LSDress_GasCylinderBank", -31.20, 2.20)
add("Workshop", "CableTray_Fore", "CableTray", -30.60, 0.90, bottom_y=TRAY_Y)
add("Workshop", "CableTray_Aft", "CableTray", -28.40, -0.90, bottom_y=TRAY_Y)
add("Workshop", "DeckGrate_Aisle", "DeckGrate", -29.30, -1.00, bottom_y=0.001)
add("Workshop", "StowageNet_Port", "StowageNet", -31.20, Z_LOW, bottom_y=1.00, clearance=0.16)
add("Workshop", "ConduitJunction_Aft", "ConduitJunction", -27.90, Z_HIGH, bottom_y=JUNC_Y)

# ── 화물칸 (8×8, 64m², 90 lx) ────────────────────────────────────────────────
# 문이 넷이다. 선체(x=-19) · 정비창(x=-27) · 격납고(z=+4) · 관측 회랑(z=-4) 이고
# 뒤 둘은 `#88367814` 로 방금 뚫렸다. 그래서 이 방은 창고가 아니라 <b>십자 교차로</b>
# 이고, x 축과 z 축 둘 다 관통선이다. 소품은 네 사분면 구석으로 물린다.
#
# 옮기는 셋은 전부 "잠겨 있어서 아무도 안 걸었을 때" 놓인 것이다.
#   Crate_2  — 선체 문 앞 `1.5m` 상자를 `x` 로 `0.4m` 파고든다
#   LashRail_Port      — 관측 회랑 문턱을 바닥에서 `0.2m` 높이로 가로지른다
#   LashRail_Starboard — 격납고 문턱을 같은 식으로 가로지른다
# 레일은 짧게 자르는 대신 <b>문 양쪽으로 두 도막씩</b> 둔다. 결박선이 문에서 끊기는
# 것이 맞다 — 이어져 있으면 그 위를 걸어 넘게 된다.
move("CargoBay", "Crate_2", "LSDress_Crate", -21.30, -2.60)
move("CargoBay", "LashRail_Port", "LSDress_LashRail", -25.50, Z_LOW)
move("CargoBay", "LashRail_Starboard", "LSDress_LashRail", -25.50, Z_HIGH)
add("CargoBay", "LashRail_Port_Aft", "LSDress_LashRail", -20.60, Z_LOW)
add("CargoBay", "LashRail_Starboard_Aft", "LSDress_LashRail", -20.60, Z_HIGH)

add("CargoBay", "CargoNetBay_Port", "LSDress_CargoNetBay", -25.90, Z_LOW, bottom_y=0.25)
add("CargoBay", "CargoNetBay_Starboard", "LSDress_CargoNetBay", -25.90, Z_HIGH, bottom_y=0.25)
add("CargoBay", "PartsBinShelf", "LSDress_PartsBinShelf", X_LOW, 2.00, euler=(0, 90, 0))
add("CargoBay", "PalletJack", "LSDress_PalletJack", -21.00, 1.90)
add("CargoBay", "TaskSpot_Manifest", "LSDress_TaskSpot", -21.00, 1.90,
    bottom_y=SPOT_Y, light=4.2)
add("CargoBay", "CrateStack_Fore", "LSDress_CrateStack", -24.60, -1.70)
add("CargoBay", "CrateStack_Aft", "LSDress_CrateStack", -20.90, 3.00)
add("CargoBay", "RibFrame_Port", "RibFrame", -20.30, Z_LOW, clearance=0.20)
add("CargoBay", "RibFrame_Starboard", "RibFrame", -20.30, Z_HIGH, clearance=0.20)
add("CargoBay", "CableTray_Fore", "CableTray", -25.20, -1.20, bottom_y=TRAY_Y)
add("CargoBay", "CableTray_Aft", "CableTray", -21.00, 1.20, bottom_y=TRAY_Y)
add("CargoBay", "DeckGrate_Port", "DeckGrate", -24.80, -0.80, bottom_y=0.001)
add("CargoBay", "StowageNet_Starboard", "StowageNet", -21.30, Z_HIGH,
    bottom_y=NET_Y, clearance=0.20)

# ── 격납고 (8×10, 80m², 200 lx) ──────────────────────────────────────────────
# 배에서 가장 넓은 방이고, `#88367814` 로 선미 끝벽(x=-19)에 상부 회랑 문이 뚫렸다.
# 갠트리(`0.3 × 2.84 × 4.1`)가 그 벽에 바짝 붙어 `z 6.95~11.05` 를 막고 서 있어서
# <b>새로 뚫린 문 앞에 2.84m 짜리 구조물이 통째로 서 있는 상태</b>였다. 선수 쪽으로
# 물린다 — 갠트리는 크래들 위 작업 설비라 크래들 옆이 원래 자리다.
move("Hangar", "Gantry", "LSDress_Gantry", X_HIGH, 7.00)

# 발진 구역(가운데 z 축 띠)은 계속 비운다. 새 소품은 전부 네 벽과 천장이고,
# 유일한 예외가 선수 크래들 위의 작업정이다.
add("Hangar", "HangarTug", "LSDress_HangarTug", -23.00, 7.103, bottom_y=0.80)
add("Hangar", "EvaSuitRack_Fore", "LSDress_EvaSuitRack", X_LOW, 6.00, euler=(0, 90, 0))
add("Hangar", "EvaSuitRack_Aft", "LSDress_EvaSuitRack", X_LOW, 12.00, euler=(0, 90, 0))
add("Hangar", "GasCylinderBank", "LSDress_GasCylinderBank", -25.50, Z_HIGH)
add("Hangar", "PanelBank_Far", "PanelBank", -22.00, Z_HIGH, bottom_y=PANEL_Y, clearance=0.20)
add("Hangar", "PanelBank_Near", "PanelBank", -25.50, Z_LOW, bottom_y=PANEL_Y, clearance=0.20)
add("Hangar", "RibFrame_Near", "RibFrame", -20.60, Z_LOW, clearance=0.20)
add("Hangar", "RibFrame_Far", "RibFrame", -20.60, Z_HIGH, clearance=0.20)
add("Hangar", "CableTray_Fore", "CableTray", -24.00, 5.60, bottom_y=TRAY_Y)
add("Hangar", "CableTray_Mid", "CableTray", -21.50, 8.40, bottom_y=TRAY_Y)
add("Hangar", "CableTray_Aft", "CableTray", -24.00, 12.20, bottom_y=TRAY_Y)
add("Hangar", "DeckGrate_Fore", "DeckGrate", -21.20, 6.40, bottom_y=0.001)
add("Hangar", "DeckGrate_Aft", "DeckGrate", -21.90, 13.40, bottom_y=0.001)
add("Hangar", "StowageNet_Far", "StowageNet", -23.50, Z_HIGH, bottom_y=NET_Y, clearance=0.20)
add("Hangar", "CrateStack_Far", "LSDress_CrateStack", -25.60, 11.50)

# 기존 프리팹(재사용) 바깥 치수 — assets-v1 §5 표 그대로.
EXISTING_SIZE = {
    "LSDress_TaskSpot": (0.32, 0.34, 0.32),
    "LSDress_EvaSuitRack": (1.20, 2.10, 0.61),
    "LSDress_CrateStack": (1.00, 1.55, 0.80),
    "LSDress_Crate": (0.92, 0.72, 0.77),
    "LSDress_LashRail": (2.40, 0.20, 0.12),
    "LSDress_PartsPallet": (1.12, 0.40, 0.92),
    "LSDress_StarChart": (1.50, 1.10, 0.06),
    "LSDress_Gantry": (0.30, 2.84, 4.10),
}
KIT_SIZE = {
    "LSDress_Kit_PanelBank": (1.80, 0.90, 0.09),
    "LSDress_Kit_ConduitJunction": (0.74, 0.64, 0.27),
    "LSDress_Kit_CableTray": (2.40, 0.15, 0.25),
    "LSDress_Kit_DeckGrate": (1.20, 0.04, 0.90),
    "LSDress_Kit_RibFrame": (0.71, 2.10, 0.20),
    "LSDress_Kit_StowageNet": (1.10, 0.81, 0.23),
}

PROP_TEMPLATE = (
    "  - id: {id}\n"
    "    space:\n"
    "      kind: 1\n"
    "      zone: 0\n"
    "      compartment: {compartment}\n"
    "      passage: 0\n"
    "      galleryLeg: 0\n"
    "    size: {{x: {sx}, y: {sy}, z: {sz}}}\n"
    "    anchorMode: 0\n"
    "    anchor: {{x: {ax}, y: {az}}}\n"
    "    bottomY: {by}\n"
    "    clearance: {cl}\n"
    "    eulerAngles: {{x: {ex}, y: {ey}, z: {ez}}}\n"
    "    prefab: {{fileID: {pid}, guid: {guid}, type: 3}}\n"
    "    material: {{fileID: 0}}\n"
    "    semantics: {sem}\n"
    "    lightIntensity: {li}\n"
    "    justification: "
)

LIGHT_SOURCE = 64   # LastShiftDressingSemantics.LightSource


def parse_blocks():
    """에셋의 항목 블록을 (키, 원문) 으로 읽는다."""
    with open(SET_ASSET, encoding="utf-8") as fh:
        body = fh.read().rstrip("\n")
    head, blocks = split_prop_blocks(body)
    return head, blocks


def compartment_of(block):
    for line in block.splitlines():
        s = line.strip()
        if s.startswith("compartment: "):
            return int(s[13:])
    return 0


def parse_prop(block):
    """구획 항목 하나를 배치 계산용 값으로 푼다."""
    out = {"id": block.splitlines()[0].strip()[len("- id: "):]}
    for line in block.splitlines():
        s = line.strip()
        if s.startswith("kind: "):
            out["kind"] = int(s[6:])
        elif s.startswith("compartment: "):
            out["compartment"] = int(s[13:])
        elif s.startswith("size: "):
            out["size"] = tuple(float(p.split(":")[1]) for p in s[6:].strip("{}").split(","))
        elif s.startswith("anchorMode: "):
            out["mode"] = int(s[12:])
        elif s.startswith("anchor: "):
            out["anchor"] = tuple(float(p.split(":")[1]) for p in s[8:].strip("{}").split(","))
        elif s.startswith("bottomY: "):
            out["bottom"] = float(s[9:])
        elif s.startswith("clearance: "):
            out["clear"] = float(s[11:])
    return out


ROOM_BY_ENUM = {v[0]: k for k, v in ROOM.items()}


def existing_boxes(mine):
    """이미 에셋에 든 선수 사슬 소품. 이번에 옮기는 것(`mine`)은 뺀다."""
    out = []
    _, blocks = parse_blocks()
    for block in blocks:
        prop = parse_prop(block)
        if prop.get("kind") != 1:
            continue
        room = ROOM_BY_ENUM.get(prop.get("compartment"))
        if room is None:
            continue
        if (room, prop["id"]) in mine:
            continue
        b = bounds(room)
        size = prop["size"]
        if prop["mode"] == 1:
            cx, cz = (b[0] + b[1]) / 2, (b[2] + b[3]) / 2
            c = (cx + prop["anchor"][0], b[4] + prop["bottom"] + size[1] / 2,
                 cz + prop["anchor"][1])
        else:
            c = ct11.world_center(b, size, prop["anchor"], prop["bottom"], prop["clear"])
        out.append((f"{room}_{prop['id']}",
                    c[0] - size[0] / 2, c[0] + size[0] / 2,
                    c[1] - size[1] / 2, c[1] + size[1] / 2,
                    c[2] - size[2] / 2, c[2] + size[2] / 2))
    return out


def main():
    refs = {}
    for name, guid, builder, collider in PREFABS:
        parts = builder()
        root = write_prefab(name, guid, parts, collider)
        lo, hi = bbox_of(parts)
        size = (round(hi[0] - lo[0], 4), round(hi[1] - lo[1], 4), round(hi[2] - lo[2], 4))
        refs[name] = (root, guid, size)
        mark = "  [collider]" if collider else ""
        print("prefab %-30s size %5.2f x %5.2f x %5.2f%s" % (name, size[0], size[1], size[2], mark))

    for name, size in list(EXISTING_SIZE.items()) + list(KIT_SIZE.items()):
        root, guid = read_prefab_ref(name)
        refs[name] = (root, guid, size)

    mine = {(it["room"], it["id"]) for it in LAYOUT}
    obstacles = door_boxes() + label_boxes() + existing_boxes(mine)

    placed, errors, boxes = [], [], []
    for item in LAYOUT:
        prefab = KIT.get(item["prefab"], item["prefab"])
        if prefab not in refs:
            errors.append(f"{item['id']}: 프리팹 {prefab} 이 없다")
            continue
        root, guid, raw = refs[prefab]

        # 회전은 축정렬 치수로 되돌려 적는다 — 경계 검사(R1_Bounds)가 `size` 만 본다.
        ext = ct11.rotated_extent(raw, item["euler"])
        size = (round(ext[0] * 2, 4), round(ext[1] * 2, 4), round(ext[2] * 2, 4))

        b = bounds(item["room"])
        clearance = item["clearance"]
        x, z = item["x"], item["z"]
        if x == X_LOW:
            x = b[0] + size[0] / 2 + clearance
        elif x == X_HIGH:
            x = b[1] - size[0] / 2 - clearance
        if z == Z_LOW:
            z = b[2] + size[2] / 2 + clearance
        elif z == Z_HIGH:
            z = b[3] - size[2] / 2 - clearance

        anchor = solve_anchor(b, size, x, z, clearance)
        tag = f"{item['room']}/{item['id']}"
        if abs(anchor[0]) > 1.0001 or abs(anchor[1]) > 1.0001:
            errors.append(f"{tag}: 앵커 {anchor} 가 공간 밖이다")
            continue

        c = ct11.world_center(b, size, anchor, item["bottom_y"], clearance)
        box = (c[0] - size[0] / 2, c[0] + size[0] / 2,
               c[1] - size[1] / 2, c[1] + size[1] / 2,
               c[2] - size[2] / 2, c[2] + size[2] / 2)
        if box[2] < -1e-3 or box[3] > CEIL_C + 1e-3:
            errors.append(f"{tag}: y {box[2]:.2f}~{box[3]:.2f} 가 바닥/천장을 뚫는다")

        for ob in obstacles:
            if not ob[0].startswith(item["room"]):
                continue
            if any(a in ob[0] for a in item["allow"]):
                continue
            if overlaps(box, ob[1:]):
                errors.append(f"{tag}: {ob[0]} 와 겹친다 "
                              f"(x {box[0]:.2f}~{box[1]:.2f} y {box[2]:.2f}~{box[3]:.2f} "
                              f"z {box[4]:.2f}~{box[5]:.2f})")
        for other_tag, other_room, other in boxes:
            if other_room == item["room"] and overlaps(box, other):
                errors.append(f"{tag}: 신규 {other_tag} 와 겹친다")
        boxes.append((tag, item["room"], box))
        placed.append(dict(item=item, root=root, guid=guid, size=size, anchor=anchor))

    if errors:
        print("\n배치 실패 %d건 — 에셋을 쓰지 않는다." % len(errors))
        for e in errors:
            print("  " + e)
        return 1

    blocks_new = []
    for p in placed:
        it = p["item"]
        light = it["light"]
        blocks_new.append(PROP_TEMPLATE.format(
            id=it["id"], compartment=ROOM[it["room"]][0],
            sx=f(p["size"][0]), sy=f(p["size"][1]), sz=f(p["size"][2]),
            ax=f(p["anchor"][0]), az=f(p["anchor"][1]),
            by=f(it["bottom_y"]), cl=f(it["clearance"]),
            ex=f(it["euler"][0]), ey=f(it["euler"][1]), ez=f(it["euler"][2]),
            pid=p["root"], guid=p["guid"],
            sem=LIGHT_SOURCE if light > 0 else 0, li=f(light)))

    head, existing = parse_blocks()
    new_keys = {(1, ROOM[p["item"]["room"]][0], p["item"]["id"]) for p in placed}
    kept = [b for b in existing if block_key_compartment(b) not in new_keys]
    dropped = len(existing) - len(kept)
    body = head + "\n" + "\n".join(b.rstrip("\n") for b in kept + blocks_new) + "\n"
    with open(SET_ASSET, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(body)

    moved = sum(1 for p in placed if p["item"]["move"])
    print("\n배치 %d건(신규 %d · 이전 %d) — 기존 %d건 대체, 에셋 항목 총 %d개"
          % (len(placed), len(placed) - moved, moved, dropped,
             body.count("\n  - id: ")))
    return 0


def block_key_compartment(block):
    """`ct11.block_key` 는 kind 0/2 만 본다. 구획은 compartment 로 키를 잡는다."""
    kind, _, pid = block_key(block)
    if kind != 1:
        return (kind, -1, pid)
    return (1, compartment_of(block), pid)


if __name__ == "__main__":
    sys.exit(main())
