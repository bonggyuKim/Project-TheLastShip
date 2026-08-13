"""CT-11 배경 밀도 — 통로 키트 6종 + 방 고유물 6종 프리팹 생성 + 배치 78건 산출.

에셋 생성기다. 런타임/에디터 코드는 건드리지 않는다 — 프리팹 YAML 과
`LastShiftDressingSet.asset` 의 항목만 만든다(`docs/scene-dressing-authoring.md`).

왜 스크립트인가. 배치 78건을 손으로 적으면 각 소품이 창(좌현 눈높이 0.6~2.1)·문 앞
1.5m·배플·승강구·통행 차선 중 무엇을 침범하는지 사람이 매번 다시 계산해야 한다.
여기서는 씬 빌더와 같은 식(WorldCenter)으로 좌표를 풀고 알려진 장애물 상자와
전부 대조한다. 충돌이 하나라도 나오면 스크립트가 실패하고 파일을 안 쓴다.

실행: python Tools/ct11_background_density.py
"""
import io
import os
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PREFAB_DIR = os.path.join(ROOT, "Assets", "DoodleUp", "Prefabs", "Dressing")
SET_ASSET = os.path.join(ROOT, "Assets", "DoodleUp", "Dressing", "LastShiftDressingSet.asset")

# ── 치수 정본 미러 (LastShiftShipDimensions / LastShiftShipPhysics) ──────────
L = 38.0
W = 6.0
HL = L / 2          # 19
HW = W / 2          # 3
CEIL = 3.2
END_ROOM = 8.0
PASS_LEN = 6.0
MID_ROOM = (L - 2 * (END_ROOM + PASS_LEN)) / 2   # 5
OPEN_W = 1.6
GAP_Z = 0.4
PASS_W = OPEN_W * 2 + GAP_Z      # 3.6
PASS_OFF_Z = HW - PASS_W / 2     # 1.2
OPEN_OFF_Z = (OPEN_W + GAP_Z) / 2  # 1.0
BAFFLE_T = 0.4
BAFFLE_OFF_T = GAP_Z / (OPEN_W + GAP_Z)  # 0.2

ROOM_X = {
    "Cockpit": (-HL, -HL + END_ROOM),            # -19 ~ -11
    "Power": (-MID_ROOM, 0.0),                   # -5 ~ 0
    "Cooling": (0.0, MID_ROOM),                  # 0 ~ +5
    "LifeSupport": (HL - END_ROOM, HL),          # +11 ~ +19
}
ZONE_ENUM = {"Cockpit": 0, "Power": 1, "Cooling": 2, "LifeSupport": 3}

PASSAGE_X = {0: (-HL + END_ROOM, -HL + END_ROOM + PASS_LEN),   # -11 ~ -5
             1: (MID_ROOM, MID_ROOM + PASS_LEN)}               # +5 ~ +11
PASSAGE_CZ = {0: PASS_OFF_Z, 1: -PASS_OFF_Z}


def passage_z(p):
    c = PASSAGE_CZ[p]
    return (c - PASS_W / 2, c + PASS_W / 2)


# 개구부 x 평면과 z 구간 (문 앞 여유 판정용)
OPENING = {
    0: (-11.0, PASS_OFF_Z + OPEN_OFF_Z),    # 조종석↔통로A   z +2.2
    1: (-5.0, PASS_OFF_Z - OPEN_OFF_Z),     # 통로A↔전력실   z +0.2
    2: (0.0, PASS_OFF_Z + OPEN_OFF_Z),      # 전력실↔냉각실  z +2.2
    3: (11.0, -PASS_OFF_Z - OPEN_OFF_Z),    # 통로B↔산소실   z -2.2
    4: (5.0, -PASS_OFF_Z + OPEN_OFF_Z),     # 냉각실↔통로B   z -0.2
}


def space_bounds(kind, key):
    """LastShiftDressingSpaces.BoundsOf 와 같은 값."""
    if kind == "Zone":
        x0, x1 = ROOM_X[key]
        return (x0, x1, -HW, HW, 0.0, CEIL)
    x0, x1 = PASSAGE_X[key]
    z0, z1 = passage_z(key)
    return (x0, x1, z0, z1, 0.0, CEIL)


def world_center(bounds, size, anchor, bottom_y, clearance):
    """LastShiftDressingSpaces.WorldCenter (UnitOfSpace) 와 같은 계산."""
    x0, x1, z0, z1, fy, _ = bounds
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    hx, hz = (x1 - x0) / 2, (z1 - z0) / 2
    sx = max(0.0, hx - size[0] / 2 - clearance)
    sz = max(0.0, hz - size[2] / 2 - clearance)
    return (cx + anchor[0] * sx, fy + bottom_y + size[1] / 2, cz + anchor[1] * sz)


def solve_anchor(bounds, size, want_x, want_z, clearance):
    """원하는 월드 (x, z) 를 내는 단위좌표 앵커. 치수가 바뀌면 벽에 붙은 채 따라간다."""
    x0, x1, z0, z1, _, _ = bounds
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    hx, hz = (x1 - x0) / 2, (z1 - z0) / 2
    sx = max(1e-6, hx - size[0] / 2 - clearance)
    sz = max(1e-6, hz - size[2] / 2 - clearance)
    return (round((want_x - cx) / sx, 4), round((want_z - cz) / sz, 4))


# ── 프리팹 정의 ─────────────────────────────────────────────────────────────
# 부품 하나 = (이름, 재질, 중심(x,y,z), 크기(x,y,z), 오일러, 메시)
# 루트 원점은 밑면·xz 중심이다(assets-v1 §2 "루트 = 밑면", 배치 훅이 그 규약을 쓴다).
CUBE, CYL = "cube", "cylinder"

MAT = {
    "LSD_Mat": "186fbedcded72e31e874a55c7feef431",
    "LSD_Conduit": "09f3f769972914cca05f6bc321b4c161",
    "LSD_ConduitBand": "d62830a387cb103b0544eae211bc4a9e",
    "LSD_Locker": "2af786e9752c9374ebd2865711979e6e",
    "LSD_Crate": "90ba3e6312c77461ae1a0b6b4a7d3483",
    "LSD_CrateTrim": "f760265a0a7e10af258992ae9aca48bd",
    "LSD_Fabric": "69d0724af37e372c2d636bd3f56c059d",
    "LSD_Screen": "fb06c59be4599382c70df894030c0d96",
    "LSD_ScreenAmber": "ed928d6dd0a63ffd6e455fa54f3bf087",
    "LS_Fixture": "639cd5c4b4dadd94fb366837fe9f5c4c",
    "LS_Panel": "cb0519809d32f8347b4dd32f464c8118",
}


def part(name, mat, pos, scale, euler=(0, 0, 0), mesh=CUBE):
    return dict(name=name, mat=mat, pos=pos, scale=scale, euler=euler, mesh=mesh)


def kit_panel_bank():
    p = [part("Backing", "LSD_Mat", (0, 0.45, -0.025), (1.80, 0.90, 0.04))]
    for i, x in enumerate((-0.66, -0.22, 0.22, 0.66)):
        p.append(part(f"Door_{i}", "LS_Panel", (x, 0.45, 0.010), (0.42, 0.82, 0.03)))
        p.append(part(f"Latch_{i}", "LS_Fixture", (x + 0.15, 0.28, 0.033), (0.07, 0.10, 0.02)))
    p.append(part("Label", "LSD_ConduitBand", (0, 0.855, 0.010), (1.70, 0.05, 0.03)))
    return p


def kit_conduit_junction():
    p = [
        part("Body", "LSD_Conduit", (0, 0.28, 0), (0.50, 0.44, 0.26)),
        part("Stub_Port", "LSD_Conduit", (-0.31, 0.28, 0), (0.12, 0.20, 0.20)),
        part("Stub_Starboard", "LSD_Conduit", (0.31, 0.28, 0), (0.12, 0.20, 0.20)),
        part("CapPlate", "LSD_ConduitBand", (0, 0.53, 0), (0.44, 0.06, 0.24)),
        part("Bracket", "LS_Fixture", (0, 0.03, -0.10), (0.34, 0.06, 0.08)),
    ]
    for i, x in enumerate((-0.12, 0.12)):
        p.append(part(f"Valve_{i}", "LS_Fixture", (x, 0.60, 0.0), (0.18, 0.08, 0.18), mesh=CYL))
    return p


def kit_cable_tray():
    p = [part("TrayFloor", "LSD_Conduit", (0, 0.05, 0), (2.40, 0.03, 0.24))]
    for i, z in enumerate((-0.115, 0.115)):
        p.append(part(f"Rail_{i}", "LSD_Conduit", (0, 0.09, z), (2.40, 0.10, 0.02)))
    for i, z in enumerate((-0.07, 0.0, 0.07)):
        p.append(part(f"Cable_{i}", "LSD_ConduitBand", (0, 0.10, z), (2.36, 0.05, 0.05)))
    for i, x in enumerate((-0.90, 0.0, 0.90)):
        p.append(part(f"Hanger_{i}", "LS_Fixture", (x, 0.14, 0), (0.05, 0.10, 0.20)))
    return p


def kit_deck_grate():
    p = [part("Frame", "LSD_Mat", (0, 0.012, 0), (1.20, 0.024, 0.90))]
    for i in range(7):
        x = -0.45 + i * 0.15
        p.append(part(f"Bar_{i}", "LS_Fixture", (x, 0.028, 0), (0.08, 0.026, 0.86)))
    for i, (x, z) in enumerate(((-0.55, -0.40), (0.55, -0.40), (-0.55, 0.40), (0.55, 0.40))):
        p.append(part(f"Bolt_{i}", "LS_Fixture", (x, 0.030, z), (0.06, 0.030, 0.06), mesh=CYL))
    return p


def kit_rib_frame():
    p = [
        part("Post", "LS_Fixture", (0, 1.05, 0), (0.16, 2.10, 0.14)),
        part("FootPlate", "LSD_Mat", (0, 0.05, 0.015), (0.34, 0.10, 0.20)),
        part("HeadPlate", "LSD_Mat", (0, 2.04, 0.015), (0.34, 0.10, 0.20)),
        part("Brace_Port", "LS_Fixture", (-0.19, 1.84, 0.0), (0.40, 0.07, 0.11), (0, 0, 45)),
        part("Brace_Starboard", "LS_Fixture", (0.19, 1.84, 0.0), (0.40, 0.07, 0.11), (0, 0, -45)),
    ]
    for i in range(5):
        p.append(part(f"Rivet_{i}", "LSD_ConduitBand", (0, 0.36 + i * 0.40, 0.078),
                      (0.05, 0.05, 0.02), mesh=CYL))
    return p


def kit_stowage_net():
    p = [
        part("Rail_Top", "LS_Fixture", (0, 0.78, -0.055), (1.10, 0.06, 0.06)),
        part("Rail_Bottom", "LS_Fixture", (0, 0.03, -0.055), (1.10, 0.06, 0.06)),
        part("Strap_Span", "LSD_CrateTrim", (0, 0.40, 0.115), (1.06, 0.05, 0.02)),
    ]
    for i, x in enumerate((-0.34, 0.0, 0.34)):
        p.append(part(f"Bundle_{i}", "LSD_Fabric", (x, 0.36, 0.005), (0.32, 0.56, 0.22)))
        p.append(part(f"Strap_{i}", "LSD_CrateTrim", (x, 0.40, 0.115), (0.05, 0.66, 0.02)))
    return p


def nav_chart_table():
    p = [
        part("Top", "LSD_Mat", (0, 0.86, 0), (1.40, 0.06, 0.86)),
        part("ChartGlass", "LSD_Screen", (0, 0.895, 0), (1.22, 0.02, 0.70)),
        part("Frame", "LS_Panel", (0, 0.55, 0), (1.24, 0.56, 0.72)),
        part("RimStrip", "LSD_ScreenAmber", (0, 0.83, 0.43), (1.30, 0.04, 0.03)),
    ]
    for i, (x, z) in enumerate(((-0.62, -0.38), (0.62, -0.38), (-0.62, 0.38), (0.62, 0.38))):
        p.append(part(f"Leg_{i}", "LS_Fixture", (x, 0.27, z), (0.08, 0.54, 0.08)))
    return p


def helm_throttle_stand():
    p = [
        part("Base", "LSD_Mat", (0, 0.05, 0), (0.46, 0.10, 0.54)),
        part("Column", "LS_Panel", (0, 0.42, 0), (0.26, 0.66, 0.30)),
        part("Head", "LS_Fixture", (0, 0.81, 0.03), (0.44, 0.14, 0.38), (-18, 0, 0)),
        part("Readout", "LSD_Screen", (0, 0.84, 0.20), (0.28, 0.05, 0.02)),
    ]
    for i, x in enumerate((-0.10, 0.10)):
        p.append(part(f"Lever_{i}", "LSD_CrateTrim", (x, 0.93, 0.09), (0.05, 0.24, 0.05), (-24, 0, 0)))
    return p


def breaker_cabinet():
    p = [
        part("Body", "LSD_Locker", (0, 0.97, 0), (0.92, 1.94, 0.40)),
        part("Label", "LSD_ScreenAmber", (0, 1.86, 0.21), (0.60, 0.08, 0.02)),
    ]
    for i, x in enumerate((-0.23, 0.23)):
        p.append(part(f"Door_{i}", "LS_Panel", (x, 1.10, 0.21), (0.42, 1.46, 0.03)))
        p.append(part(f"Handle_{i}", "LS_Fixture", (x + (0.16 if i == 0 else -0.16), 1.10, 0.24),
                      (0.03, 0.28, 0.03)))
    for i in range(4):
        p.append(part(f"Vent_{i}", "LSD_ConduitBand", (0, 0.16 + i * 0.09, 0.21), (0.70, 0.035, 0.02)))
    return p


def heat_exchanger_coil():
    p = [
        part("Post_Port", "LS_Fixture", (-0.54, 0.81, 0), (0.08, 1.62, 0.56)),
        part("Post_Starboard", "LS_Fixture", (0.54, 0.81, 0), (0.08, 1.62, 0.56)),
        part("Header_Top", "LSD_ConduitBand", (0, 1.56, 0), (1.16, 0.12, 0.30)),
        part("Header_Bottom", "LSD_ConduitBand", (0, 0.06, 0), (1.16, 0.12, 0.30)),
        part("Valve", "LS_Fixture", (0.38, 1.66, 0.14), (0.16, 0.08, 0.16), mesh=CYL),
    ]
    for i in range(7):
        p.append(part(f"Coil_{i}", "LSD_Conduit", (0, 0.22 + i * 0.19, 0), (1.02, 0.09, 0.50)))
    return p


def scrubber_stack():
    p = [
        part("BasePlate", "LSD_Mat", (0, 0.06, 0), (1.42, 0.12, 0.58)),
        part("Manifold", "LSD_ConduitBand", (0, 1.76, 0), (1.42, 0.14, 0.26)),
    ]
    for i, x in enumerate((-0.48, 0.0, 0.48)):
        p.append(part(f"Canister_{i}", "LSD_Conduit", (x, 0.90, 0), (0.40, 0.78, 0.40), mesh=CYL))
        p.append(part(f"Cap_{i}", "LS_Fixture", (x, 1.72, 0.12), (0.12, 0.08, 0.12), mesh=CYL))
    return p


def o2_tank_bank():
    p = [
        part("Cradle", "LSD_Mat", (0, 0.10, 0), (1.72, 0.20, 0.52)),
        part("Manifold", "LSD_Conduit", (0, 1.12, 0), (1.72, 0.08, 0.10)),
        part("Strap", "LSD_CrateTrim", (0, 0.80, 0.19), (1.68, 0.06, 0.03)),
    ]
    for i, x in enumerate((-0.60, -0.20, 0.20, 0.60)):
        p.append(part(f"Tank_{i}", "LSD_Conduit", (x, 0.64, 0), (0.34, 0.44, 0.34), mesh=CYL))
        p.append(part(f"Regulator_{i}", "LS_Fixture", (x, 1.09, 0.11), (0.12, 0.10, 0.12)))
    return p


# guid 는 스크립트에 박아 둔다 — 다시 돌려도 참조가 안 끊긴다.
PREFABS = [
    ("LSDress_Kit_PanelBank", "3a71c0d5e4b28f4d9a1c66f0d2ba7e11", kit_panel_bank, False),
    ("LSDress_Kit_ConduitJunction", "5c92e18b7d3a44e2b60f19ac8d5e3b27", kit_conduit_junction, False),
    ("LSDress_Kit_CableTray", "7e40b6a2c95d41f8ad2378e1b4c60d93", kit_cable_tray, False),
    ("LSDress_Kit_DeckGrate", "91b5d73f28ae4c60b1df5a82e7c04d6b", kit_deck_grate, False),
    ("LSDress_Kit_RibFrame", "b3f8a2170ce54d9b8a67f3d1c20e95a4", kit_rib_frame, False),
    ("LSDress_Kit_StowageNet", "d6c41e93a70b48f5926ad83c1fe07b52", kit_stowage_net, False),
    ("LSDress_NavChartTable", "f2a09d6c53b1487eb4c8027fa1d6e389", nav_chart_table, True),
    ("LSDress_HelmThrottleStand", "0d73b845e29c4af1a05e63d7bc918f26", helm_throttle_stand, True),
    ("LSDress_BreakerCabinet", "2b98c60fd41e4a37890b5ce2f37a0d14", breaker_cabinet, True),
    ("LSDress_HeatExchangerCoil", "4e15af820b6d43c9a72f8d05e6b31c78", heat_exchanger_coil, True),
    ("LSDress_ScrubberStack", "6c3d70b19af24e58b31067ca8d24f095", scrubber_stack, True),
    ("LSDress_O2TankBank", "8a52fc3706e94db2a8c1f490b5d76e31", o2_tank_bank, True),
]

MESH_ID = {CUBE: 10202, CYL: 10206}


def euler_to_quat(e):
    import math
    rx, ry, rz = (math.radians(v) for v in e)
    cx, sx = math.cos(rx / 2), math.sin(rx / 2)
    cy, sy = math.cos(ry / 2), math.sin(ry / 2)
    cz, sz = math.cos(rz / 2), math.sin(rz / 2)
    # Unity ZXY 순서
    x = sx * cy * cz + cx * sy * sz
    y = cx * sy * cz - sx * cy * sz
    z = cx * cy * sz - sx * sy * cz
    w = cx * cy * cz + sx * sy * sz
    return (x, y, z, w)


def rotated_extent(scale, euler):
    """회전한 상자의 축정렬 반치수. 크기 표기는 실제 바깥 경계여야 경계 검사가 참이 된다."""
    import math
    q = euler_to_quat(euler)
    x, y, z, w = q
    m = [
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ]
    h = [scale[i] / 2 for i in range(3)]
    return [sum(abs(m[r][c]) * h[c] for c in range(3)) for r in range(3)]


def prefab_bbox(parts):
    lo = [1e9] * 3
    hi = [-1e9] * 3
    for pt in parts:
        ext = rotated_extent(pt["scale"], pt["euler"])
        for i in range(3):
            lo[i] = min(lo[i], pt["pos"][i] - ext[i])
            hi[i] = max(hi[i], pt["pos"][i] + ext[i])
    return lo, hi


def fid(seed):
    """결정적 fileID. 재생성해도 같은 값이라 씬·에셋 참조가 안 흔들린다."""
    h = 1469598103934665603
    for ch in seed.encode("utf-8"):
        h = ((h ^ ch) * 1099511628211) & 0xFFFFFFFFFFFFFFFF
    return (h % 9000000000000000000) + 1000000000000000


def f(v):
    s = f"{v:.6f}".rstrip("0").rstrip(".")
    return "0" if s in ("", "-0") else s


def v3(t):
    return "{x: %s, y: %s, z: %s}" % (f(t[0]), f(t[1]), f(t[2]))


RENDERER_TAIL = """  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 1
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_GlobalIlluminationMeshLod: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_MaskInteraction: 0
  m_AdditionalVertexStreams: {fileID: 0}
"""


def write_prefab(name, guid, parts, collider):
    root_go = fid(name + "/go")
    root_tr = fid(name + "/tr")
    child_tr = [fid(f"{name}/{p['name']}/tr") for p in parts]

    out = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:"]
    comps = [f"  - component: {{fileID: {root_tr}}}"]
    if collider:
        root_bc = fid(name + "/bc")
        comps.append(f"  - component: {{fileID: {root_bc}}}")
    out += [
        f"--- !u!1 &{root_go}", "GameObject:", "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}", "  serializedVersion: 6", "  m_Component:",
        *comps,
        "  m_Layer: 0", f"  m_Name: {name}", "  m_TagString: Untagged", "  m_Icon: {fileID: 0}",
        "  m_NavMeshLayer: 0", "  m_StaticEditorFlags: 0", "  m_IsActive: 1",
        f"--- !u!4 &{root_tr}", "Transform:", "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}", f"  m_GameObject: {{fileID: {root_go}}}",
        "  serializedVersion: 2", "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
        "  m_LocalPosition: {x: 0, y: 0, z: 0}", "  m_LocalScale: {x: 1, y: 1, z: 1}",
        "  m_ConstrainProportionsScale: 0", "  m_Children:",
        *[f"  - {{fileID: {c}}}" for c in child_tr],
        "  m_Father: {fileID: 0}", "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}",
    ]

    if collider:
        lo, hi = prefab_bbox(parts)
        size = (hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2])
        centre = ((hi[0] + lo[0]) / 2, (hi[1] + lo[1]) / 2, (hi[2] + lo[2]) / 2)
        out += [
            f"--- !u!65 &{fid(name + '/bc')}", "BoxCollider:", "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}", f"  m_GameObject: {{fileID: {root_go}}}",
            "  m_Material: {fileID: 0}", "  m_IncludeLayers:", "    serializedVersion: 2",
            "    m_Bits: 0", "  m_ExcludeLayers:", "    serializedVersion: 2", "    m_Bits: 0",
            "  m_LayerOverridePriority: 0", "  m_IsTrigger: 0", "  m_ProvidesContacts: 0",
            "  m_Enabled: 1", "  serializedVersion: 3", f"  m_Size: {v3(size)}",
            f"  m_Center: {v3(centre)}",
        ]

    for pt, tr in zip(parts, child_tr):
        go = fid(f"{name}/{pt['name']}/go")
        mf = fid(f"{name}/{pt['name']}/mf")
        mr = fid(f"{name}/{pt['name']}/mr")
        q = euler_to_quat(pt["euler"])
        out += [
            f"--- !u!1 &{go}", "GameObject:", "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}", "  serializedVersion: 6", "  m_Component:",
            f"  - component: {{fileID: {tr}}}", f"  - component: {{fileID: {mf}}}",
            f"  - component: {{fileID: {mr}}}",
            "  m_Layer: 0", f"  m_Name: {pt['name']}", "  m_TagString: Untagged",
            "  m_Icon: {fileID: 0}", "  m_NavMeshLayer: 0", "  m_StaticEditorFlags: 0",
            "  m_IsActive: 1",
            f"--- !u!4 &{tr}", "Transform:", "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}", f"  m_GameObject: {{fileID: {go}}}",
            "  serializedVersion: 2",
            "  m_LocalRotation: {x: %s, y: %s, z: %s, w: %s}" % (f(q[0]), f(q[1]), f(q[2]), f(q[3])),
            f"  m_LocalPosition: {v3(pt['pos'])}", f"  m_LocalScale: {v3(pt['scale'])}",
            "  m_ConstrainProportionsScale: 0", "  m_Children: []",
            f"  m_Father: {{fileID: {root_tr}}}",
            f"  m_LocalEulerAnglesHint: {v3(pt['euler'])}",
            f"--- !u!33 &{mf}", "MeshFilter:", "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}", f"  m_GameObject: {{fileID: {go}}}",
            "  m_Mesh: {fileID: %d, guid: 0000000000000000e000000000000000, type: 0}" % MESH_ID[pt["mesh"]],
            f"--- !u!23 &{mr}", "MeshRenderer:", "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}", f"  m_GameObject: {{fileID: {go}}}",
            "  m_Enabled: 1", "  m_CastShadows: 1", "  m_ReceiveShadows: 1",
            "  m_DynamicOccludee: 1", "  m_StaticShadowCaster: 0", "  m_MotionVectors: 1",
            "  m_LightProbeUsage: 1", "  m_ReflectionProbeUsage: 1", "  m_RayTracingMode: 2",
            "  m_RayTraceProcedural: 0", "  m_RayTracingAccelStructBuildFlagsOverride: 0",
            "  m_RayTracingAccelStructBuildFlags: 1", "  m_SmallMeshCulling: 1",
            "  m_ForceMeshLod: -1", "  m_MeshLodSelectionBias: 0", "  m_RenderingLayerMask: 1",
            "  m_RendererPriority: 0", "  m_Materials:",
            "  - {fileID: 2100000, guid: %s, type: 2}" % MAT[pt["mat"]],
            RENDERER_TAIL.rstrip("\n"),
        ]

    path = os.path.join(PREFAB_DIR, name + ".prefab")
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(out) + "\n")
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
        fh.write("fileFormatVersion: 2\nguid: %s\nPrefabImporter:\n  externalObjects: {}\n"
                 "  userData:\n  assetBundleName:\n  assetBundleVariant:\n" % guid)
    return root_go


# ── 알려진 장애물 (씬 빌더가 이미 세운 것 + 게임플레이가 요구하는 빈자리) ────
# 소품은 배경이다. 여기 하나라도 걸리면 배경이 아니라 방해물이 된다.
def _openings():
    out = []
    for i, (px, cz) in OPENING.items():
        out.append((f"Opening{i}_Clearance", px - 1.5, px + 1.5, 0.0, 2.2, cz - 1.1, cz + 1.1))
    return out


def _risers():
    out = []
    for zc in (-12.0, -2.5, 2.5, 12.0):        # ZoneCenterX (구역, 방 아님)
        for s in (-1, 1):
            x = zc + s * 2.05
            out.append((f"DuctRiser_{x:+.2f}", x - 0.13, x + 0.13, -0.2, 3.15, 2.65, 2.91))
    return out


def _zone_walls():
    out = []
    for name, c in (("Cockpit", -15.0), ("Power", -2.5), ("Cooling", 2.5), ("LifeSupport", 15.0)):
        out.append((f"Panel_{name}", c - 1.62, c + 1.62, 0.93, 2.17, 2.84, 3.01))
        out.append((f"ZoneLabel_{name}", c - 2.05, c + 2.05, 2.03, 2.47, 2.78, 2.96))
    return out


OBSTACLES = [
    # 씬 빌더 큐브
    ("CockpitConsole", -16.65, -15.95, 0.0, 1.10, -1.25, 1.25),
    ("TetherRack", -11.85, -11.35, 0.0, 1.20, -1.75, -0.85),
    ("BusCabinet", -3.30, -1.70, 0.0, 1.30, 2.20, 2.70),
    ("LifeSupportRack", 15.70, 16.50, 0.0, 1.50, 1.85, 2.65),
    ("CoolingStack", 1.40, 3.60, 0.0, 1.80, 2.10, 2.70),
    ("CoolingStackFins", 1.63, 3.37, 1.60, 2.10, 2.05, 2.75),
    ("Panel_PortWall", -19.10, -18.90, 1.15, 2.25, -2.05, 0.25),
    ("Panel_StarboardWall", 18.90, 19.10, 1.15, 2.25, -2.05, 0.25),
    # 좌현은 벽이 아니라 창이다 — 눈높이 구간을 막으면 별이 사라진다
    ("PortWindows", -HL, HL, 0.55, 2.15, -3.05, -2.90),
    # 천장 리브 / 주 배관
    ("CeilingRibs", -HL, HL, 3.05, CEIL, -HW, HW),
    ("Duct_Main_Fore", -HL, HL, 2.60, 2.96, -2.04, -1.68),
    ("Duct_Main_Aft", -HL, HL, 2.63, 2.93, 1.80, 2.10),
    # 승강구 입구 — 드레싱이 0.9m 구멍을 좁히면 안 된다
    ("ShaftMouth_Fore", -12.70, -11.30, -0.20, 0.60, -2.20, -0.80),
    ("ShaftMouth_Aft", 11.30, 12.70, -0.20, 0.60, 0.80, 2.20),
    # 구획 문 — 선체 벽에 뚫린 자리
    ("Door_ServerRoom", -15.90, -14.10, 0.0, 2.20, 2.80, 3.05),
    ("Door_Hydroponics", 12.10, 13.90, 0.0, 2.20, 2.80, 3.05),
    ("Door_CargoBay", -19.05, -18.80, 0.0, 2.20, -0.90, 0.90),
    ("Door_Lavatory", 18.80, 19.05, 0.0, 2.20, -0.90, 0.90),
    # 배플
    ("Baffle_A", -10.10, -9.50, 0.0, CEIL, 0.90, 2.70),
    ("Baffle_B", 5.90, 6.50, 0.0, CEIL, -1.50, 0.30),
    # 통로 통행 차선 — 물건을 들고 직진하는 폭이다(A3 / CARRY_SPEED)
    ("Lane_A", -11.0, -5.0, 0.0, 2.20, -0.60, 1.00),
    ("Lane_B", 5.0, 11.0, 0.0, 2.20, -3.00, -1.40),
    ("Rail_A_Port", -10.70, -5.30, 0.98, 1.22, -0.70, -0.42),
    ("Rail_A_Starboard", -10.70, -5.30, 0.98, 1.22, 2.82, 3.00),
    ("Rail_B_Port", 5.30, 10.70, 0.98, 1.22, -3.00, -2.82),
    ("Rail_B_Starboard", 5.30, 10.70, 0.98, 1.22, 0.42, 0.70),
    # 스폰과 조준선 — PlayMode 테스트가 이 선을 전제로 서 있다
    ("CrewSpawnLane", -13.00, -10.40, 0.0, 2.00, -1.10, 1.10),
    # 부품 정위치
    ("Item_Battery", -1.40, -0.20, 0.0, 1.00, 0.20, 1.40),
    ("Item_CoolingCanister", 1.90, 3.10, 0.0, 1.20, -1.90, -0.70),
    ("Item_PatchPlate", 14.80, 16.20, 0.0, 1.30, -2.30, -0.90),
] + _openings() + _risers() + _zone_walls()


def overlaps(a, b):
    return all(a[2 * i] < b[2 * i + 1] - 1e-4 and b[2 * i] < a[2 * i + 1] - 1e-4 for i in range(3))


# ── 배치 ────────────────────────────────────────────────────────────────────
# 규칙 셋을 지킨다.
#  1. 좌현(창)은 눈높이 0.6~2.1 을 절대 안 막는다 — 위(2.2+) 아니면 바닥(0.05-)만 쓴다
#  2. 세로 리브는 창틀(WindowMullion, 간격 3.2m) x 에 맞춘다 — 창을 반으로 자르지 않는다
#  3. 부피가 있는 것(콜라이더)은 전부 방 안 벽 쪽이고, 통로에는 벽·천장에 붙는 것만 둔다
MULLION = [-14.4 + 3.2 * i for i in range(10)]     # -14.4 … +14.4
PORT_WALL = "port"
STAR_WALL = "starboard"

# (id, prefab, space, want_x, want_z 또는 벽 지정, bottomY, euler)
LAYOUT = []


def add(pid, prefab, kind, key, x, z, bottom_y, euler=(0, 0, 0), clearance=0.06):
    LAYOUT.append(dict(id=pid, prefab=prefab, kind=kind, key=key, x=x, z=z,
                       bottom_y=bottom_y, euler=euler, clearance=clearance))


def wall_z(kind, key, wall, depth, gap=0.06):
    z0, z1 = space_bounds(kind, key)[2:4]
    return (z1 - depth / 2 - gap) if wall == STAR_WALL else (z0 + depth / 2 + gap)


KIT = {
    "PanelBank": "LSDress_Kit_PanelBank",
    "ConduitJunction": "LSDress_Kit_ConduitJunction",
    "CableTray": "LSDress_Kit_CableTray",
    "DeckGrate": "LSDress_Kit_DeckGrate",
    "RibFrame": "LSDress_Kit_RibFrame",
    "StowageNet": "LSDress_Kit_StowageNet",
}

# 천장 케이블 트레이 밑면. 주 배관(2.60~2.96)과 천장 리브(3.05~) 사이 자리다.
TRAY_Y = 2.86
# 벽 상부 배관 접합. 좌현 창 상단(2.15)과 리브 사이.
JUNC_Y = 2.22
# 통로 손잡이(0.98~1.22) 위. 방에서는 계기 패널(0.93~2.17)을 피해 따로 잡는다.
PANEL_Y_PASSAGE = 1.26
NET_Y = 1.30

Z_PORT, Z_STAR, Z_INNER = "port", "starboard", "inner"

# ── 조종석 ──────────────────────────────────────────────────────────────────
add("CableTray_Fore", "CableTray", "Zone", "Cockpit", -17.4, -1.0, TRAY_Y)
add("CableTray_Mid", "CableTray", "Zone", "Cockpit", -14.6, -1.0, TRAY_Y)
add("CableTray_Aft", "CableTray", "Zone", "Cockpit", -12.3, 0.9, TRAY_Y)
add("ConduitJunction_Port_Fore", "ConduitJunction", "Zone", "Cockpit", -18.2, Z_PORT, JUNC_Y)
add("ConduitJunction_Port_Aft", "ConduitJunction", "Zone", "Cockpit", -12.6, Z_PORT, JUNC_Y)
add("ConduitJunction_Starboard", "ConduitJunction", "Zone", "Cockpit", -17.6, Z_STAR, JUNC_Y)
add("PanelBank_Starboard", "PanelBank", "Zone", "Cockpit", -17.6, Z_STAR, 0.95)
add("RibFrame_Port_Fore", "RibFrame", "Zone", "Cockpit", -17.6, Z_PORT, 0.0, clearance=0.16)
add("RibFrame_Port_Aft", "RibFrame", "Zone", "Cockpit", -14.4, Z_PORT, 0.0, clearance=0.16)
add("DeckGrate_Fore", "DeckGrate", "Zone", "Cockpit", -18.3, -1.6, 0.001)
add("DeckGrate_Starboard", "DeckGrate", "Zone", "Cockpit", -17.0, 2.45, 0.001)
add("NavChartTable", "LSDress_NavChartTable", "Zone", "Cockpit", -17.9, -0.6, 0.0)
add("HelmThrottle_Port", "LSDress_HelmThrottleStand", "Zone", "Cockpit", -16.3, -1.62, 0.0)
add("HelmThrottle_Starboard", "LSDress_HelmThrottleStand", "Zone", "Cockpit", -16.3, 1.62, 0.0)
add("HelmSeat_Port", "LSDress_Seat", "Zone", "Cockpit", -15.3, -1.70, 0.0)
add("HelmSeat_Starboard", "LSDress_Seat", "Zone", "Cockpit", -15.3, 1.70, 0.0)
add("CrateStack_Aft", "LSDress_CrateStack", "Zone", "Cockpit", -13.1, 2.4, 0.0)

# ── 통로 A ──────────────────────────────────────────────────────────────────
add("CableTray_Fore", "CableTray", "Passage", 0, -9.7, 0.4, TRAY_Y)
add("CableTray_Aft", "CableTray", "Passage", 0, -6.6, 0.4, TRAY_Y)
add("ConduitJunction_Starboard_Fore", "ConduitJunction", "Passage", 0, -8.4, Z_STAR, JUNC_Y)
add("ConduitJunction_Starboard_Aft", "ConduitJunction", "Passage", 0, -5.5, Z_STAR, JUNC_Y)
add("ConduitJunction_Port", "ConduitJunction", "Passage", 0, -7.6, Z_PORT, JUNC_Y)
add("PanelBank_Starboard", "PanelBank", "Passage", 0, -8.4, Z_STAR, PANEL_Y_PASSAGE)
add("RibFrame_Starboard", "RibFrame", "Passage", 0, -6.6, Z_STAR, 0.0, clearance=0.22)
add("DeckGrate_Fore", "DeckGrate", "Passage", 0, -8.6, 2.4, 0.001)
add("DeckGrate_Aft", "DeckGrate", "Passage", 0, -7.0, 2.0, 0.001)

# ── 전력실 ──────────────────────────────────────────────────────────────────
add("CableTray_Port", "CableTray", "Zone", "Power", -2.5, -1.0, TRAY_Y)
add("CableTray_Starboard", "CableTray", "Zone", "Power", -2.5, 0.6, TRAY_Y)
add("ConduitJunction_Port", "ConduitJunction", "Zone", "Power", -4.0, Z_PORT, JUNC_Y)
add("RibFrame_Port", "RibFrame", "Zone", "Power", -1.6, Z_PORT, 0.0, clearance=0.16)
add("StowageNet_Port", "StowageNet", "Zone", "Power", -2.6, Z_PORT, 2.20, clearance=0.16)
add("DeckGrate_Port", "DeckGrate", "Zone", "Power", -2.6, -0.6, 0.001)
add("DeckGrate_Starboard", "DeckGrate", "Zone", "Power", -4.3, 1.8, 0.001)
add("BreakerCabinet", "LSDress_BreakerCabinet", "Zone", "Power", -3.9, Z_STAR, 0.0, clearance=0.1725)
add("Power_SpareBatteryRack", "LSDress_PartsPallet", "Zone", "Power", -3.0, -2.2, 0.0)

# ── 냉각실 ──────────────────────────────────────────────────────────────────
add("CableTray_Port", "CableTray", "Zone", "Cooling", 2.5, -1.0, TRAY_Y)
add("CableTray_Starboard", "CableTray", "Zone", "Cooling", 2.5, 0.6, TRAY_Y)
add("ConduitJunction_Port", "ConduitJunction", "Zone", "Cooling", 4.0, Z_PORT, JUNC_Y)
add("RibFrame_Port", "RibFrame", "Zone", "Cooling", 1.6, Z_PORT, 0.0, clearance=0.16)
add("StowageNet_Port", "StowageNet", "Zone", "Cooling", 2.6, Z_PORT, 2.20, clearance=0.16)
add("DeckGrate_Fore", "DeckGrate", "Zone", "Cooling", 2.8, 0.4, 0.001)
add("DeckGrate_Aft", "DeckGrate", "Zone", "Cooling", 1.0, 0.4, 0.001)
add("HeatExchangerCoil", "LSDress_HeatExchangerCoil", "Zone", "Cooling", 4.3, 1.3, 0.0)
add("CoolingRack", "LSDress_CoolingRack", "Zone", "Cooling", 1.3, -0.4, 0.0)
add("LashRail_Port", "LSDress_LashRail", "Zone", "Cooling", 2.5, -2.4, 0.10)

# ── 통로 B ──────────────────────────────────────────────────────────────────
add("CableTray_Fore", "CableTray", "Passage", 1, 8.6, -1.2, TRAY_Y)
add("CableTray_Aft", "CableTray", "Passage", 1, 9.7, -2.4, TRAY_Y)
add("ConduitJunction_Port_Fore", "ConduitJunction", "Passage", 1, 7.0, Z_PORT, JUNC_Y)
add("ConduitJunction_Port_Aft", "ConduitJunction", "Passage", 1, 10.2, Z_PORT, JUNC_Y)
add("PanelBank_Inner", "PanelBank", "Passage", 1, 9.2, Z_INNER, PANEL_Y_PASSAGE)
add("RibFrame_Inner", "RibFrame", "Passage", 1, 9.6, Z_INNER, 0.0, clearance=0.22)
add("StowageNet_Inner", "StowageNet", "Passage", 1, 7.6, Z_INNER, NET_Y)
add("DeckGrate_Fore", "DeckGrate", "Passage", 1, 8.0, -0.4, 0.001)
add("DeckGrate_Aft", "DeckGrate", "Passage", 1, 10.0, -0.4, 0.001)

# ── 산소실 ──────────────────────────────────────────────────────────────────
add("CableTray_Fore", "CableTray", "Zone", "LifeSupport", 12.3, -1.0, TRAY_Y)
add("CableTray_Mid", "CableTray", "Zone", "LifeSupport", 15.0, -1.0, TRAY_Y)
add("CableTray_Aft", "CableTray", "Zone", "LifeSupport", 17.6, 0.9, TRAY_Y)
add("ConduitJunction_Port_Fore", "ConduitJunction", "Zone", "LifeSupport", 12.4, Z_PORT, JUNC_Y)
add("ConduitJunction_Port_Aft", "ConduitJunction", "Zone", "LifeSupport", 17.8, Z_PORT, JUNC_Y)
add("ConduitJunction_Starboard", "ConduitJunction", "Zone", "LifeSupport", 17.6, Z_STAR, JUNC_Y)
add("PanelBank_Starboard", "PanelBank", "Zone", "LifeSupport", 17.6, Z_STAR, 0.95)
add("RibFrame_Port_Fore", "RibFrame", "Zone", "LifeSupport", 14.4, Z_PORT, 0.0, clearance=0.16)
add("RibFrame_Port_Aft", "RibFrame", "Zone", "LifeSupport", 17.6, Z_PORT, 0.0, clearance=0.16)
add("DeckGrate_Fore", "DeckGrate", "Zone", "LifeSupport", 16.0, -0.2, 0.001)
add("DeckGrate_Aft", "DeckGrate", "Zone", "LifeSupport", 15.9, 1.2, 0.001)
add("ScrubberStack", "LSDress_ScrubberStack", "Zone", "LifeSupport", 14.95, Z_STAR, 0.0, clearance=0.20)
add("O2TankBank_Fore", "LSDress_O2TankBank", "Zone", "LifeSupport", 13.6, -2.2, 0.0)
add("O2TankBank_Aft", "LSDress_O2TankBank", "Zone", "LifeSupport", 17.9, -2.2, 0.0)
add("WallLocker_Fore", "LSDress_WallLocker", "Zone", "LifeSupport", 11.75, Z_STAR, 0.0, clearance=0.20)
add("LockerBank_Aft", "LSDress_LockerBank", "Zone", "LifeSupport", 17.6, Z_STAR, 0.0, clearance=0.20)
add("LifeSupport_CrateLashed_Fore", "LSDress_CrateStack", "Zone", "LifeSupport", 13.0, -0.4, 0.0)
add("LifeSupport_CrateLashed_Aft", "LSDress_CrateStack", "Zone", "LifeSupport", 17.4, 1.4, 0.0)


# 기존 프리팹(재사용) 바깥 치수 — assets-v1 §5 표 그대로.
EXISTING_SIZE = {
    "LSDress_Seat": (0.50, 0.90, 0.50),
    "LSDress_CrateStack": (1.00, 1.55, 0.80),
    "LSDress_WallLocker": (1.02, 1.90, 0.44),
    "LSDress_LockerBank": (2.02, 1.90, 0.44),
    "LSDress_PartsPallet": (1.12, 0.40, 0.92),
    "LSDress_LashRail": (2.40, 0.20, 0.12),
    "LSDress_ToolRack": (1.40, 1.10, 0.05),
    "LSDress_CoolingRack": (1.52, 1.87, 0.52),
}


def read_prefab_ref(name):
    """(rootFileID, guid). 재사용 프리팹은 파일에서 직접 읽는다 — 표를 또 적지 않는다."""
    path = os.path.join(PREFAB_DIR, name + ".prefab")
    root = None
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            if line.startswith("--- !u!1 &"):
                root = int(line.strip().split("&")[1])
                break
    if root is None:
        raise SystemExit(name + ": 루트 GameObject 를 못 찾았다")
    with open(path + ".meta", encoding="utf-8") as fh:
        guid = [l.split(": ")[1].strip() for l in fh if l.startswith("guid:")][0]
    return root, guid


def layout_keys():
    """이번에 쓸 (kind, 공간, id). 재실행 때 <b>자기 자신을 장애물로 세지 않기</b> 위한 것이다."""
    keys = {(0 if it["kind"] == "Zone" else 2,
             ZONE_ENUM[it["key"]] if it["kind"] == "Zone" else it["key"],
             it["id"]) for it in LAYOUT}
    keys |= retired_layout_keys()
    return keys


def retired_layout_keys():
    return {
        (0, ZONE_ENUM["Power"], "PartsPallet"),
        (0, ZONE_ENUM["Power"], "CrateStack_Fore"),
        (0, ZONE_ENUM["Power"], "CrateStack_Aft"),
        (0, ZONE_ENUM["Cooling"], "CrateStack_Aft"),
        (0, ZONE_ENUM["Cooling"], "PartsPallet"),
        (0, ZONE_ENUM["LifeSupport"], "CrateStack_Fore"),
        (0, ZONE_ENUM["LifeSupport"], "CrateStack_Mid"),
        (0, ZONE_ENUM["LifeSupport"], "CrateStack_Aft"),
        (0, ZONE_ENUM["LifeSupport"], "PartsPallet"),
    }


def existing_props_as_obstacles():
    """이미 에셋에 든 소품. 새 소품이 그 위에 겹치면 밀도가 아니라 뭉침이 된다."""
    out = []
    mine = layout_keys()
    # 데이터 앵커는 정적 메시가 아니다. 특히 Frost_Deck 은 냉각실 바닥 상태 표식이라
    # LashRail 과 공간을 공유해도 실제 형상 충돌이 생기지 않는다.
    nonvisual_ids = {"Frost_Deck", "Scorch_Deck"}
    cur = None
    zone_name = {v: k for k, v in ZONE_ENUM.items()}
    with open(SET_ASSET, encoding="utf-8") as fh:
        lines = fh.readlines()
    for raw in lines:
        s = raw.strip()
        if s.startswith("- id: "):
            cur = {"id": s[6:].strip()}
        elif cur is None:
            continue
        elif s.startswith("kind: "):
            cur["kind"] = int(s[6:])
        elif s.startswith("zone: "):
            cur["zone"] = int(s[6:])
        elif s.startswith("passage: "):
            cur["passage"] = int(s[9:])
        elif s.startswith("size: "):
            cur["size"] = tuple(float(p.split(":")[1]) for p in s[6:].strip("{}").split(","))
        elif s.startswith("anchorMode: "):
            cur["mode"] = int(s[12:])
        elif s.startswith("anchor: "):
            cur["anchor"] = tuple(float(p.split(":")[1]) for p in s[8:].strip("{}").split(","))
        elif s.startswith("bottomY: "):
            cur["bottom"] = float(s[9:])
        elif s.startswith("clearance: "):
            cur["clear"] = float(s[11:])
            if cur.get("kind") == 0:
                b = space_bounds("Zone", zone_name[cur["zone"]])
            elif cur.get("kind") == 2:
                b = space_bounds("Passage", cur["passage"])
            else:
                cur = None
                continue
            key = (cur["kind"], cur["zone"] if cur["kind"] == 0 else cur["passage"], cur["id"])
            if key in mine or cur["id"] in nonvisual_ids:
                cur = None
                continue
            sz = cur["size"]
            if cur["mode"] == 1:
                cx, cz = (b[0] + b[1]) / 2, (b[2] + b[3]) / 2
                c = (cx + cur["anchor"][0], b[4] + cur["bottom"] + sz[1] / 2, cz + cur["anchor"][1])
            else:
                c = world_center(b, sz, cur["anchor"], cur["bottom"], cur["clear"])
            out.append(("Existing_" + cur["id"],
                        c[0] - sz[0] / 2, c[0] + sz[0] / 2,
                        c[1] - sz[1] / 2, c[1] + sz[1] / 2,
                        c[2] - sz[2] / 2, c[2] + sz[2] / 2))
            cur = None
    return out


PROP_TEMPLATE = (
    "  - id: {id}\n"
    "    space:\n"
    "      kind: {kind}\n"
    "      zone: {zone}\n"
    "      compartment: 0\n"
    "      passage: {passage}\n"
    "    size: {{x: {sx}, y: {sy}, z: {sz}}}\n"
    "    anchorMode: 0\n"
    "    anchor: {{x: {ax}, y: {az}}}\n"
    "    bottomY: {by}\n"
    "    clearance: {cl}\n"
    "    eulerAngles: {{x: {ex}, y: {ey}, z: {ez}}}\n"
    "    prefab: {{fileID: {pid}, guid: {guid}, type: 3}}\n"
    "    material: {{fileID: 0}}\n"
    "    semantics: 0\n"
    "    lightIntensity: 0\n"
    "    justification:"
)

def split_prop_blocks(body):
    """에셋 본문을 (헤더, [항목 블록]) 으로 가른다.

    <b>YAML 주석으로 구간을 표시하지 않는다.</b> 유니티 YAML 파서는 리스트 중간의 `#` 줄에서
    항목 읽기를 멈춘다 — 실제로 그렇게 했다가 파일에는 180개가 있는데 에디터는 102개만
    읽었다. 재실행 멱등성은 주석이 아니라 (공간, id) 키로 잡는다.
    """
    head, sep, rest = body.partition("\n  - id: ")
    if not sep:
        return body, []
    chunks = ("  - id: " + rest).split("\n  - id: ")
    return head, [chunks[0]] + ["  - id: " + c for c in chunks[1:]]


def block_key(block):
    lines = [l.strip() for l in block.splitlines()]
    pid = lines[0][len("- id: "):].strip()
    kind = zone = passage = 0
    for l in lines:
        if l.startswith("kind: "):
            kind = int(l[6:])
        elif l.startswith("zone: "):
            zone = int(l[6:])
        elif l.startswith("passage: "):
            passage = int(l[9:])
    return (kind, zone if kind == 0 else passage, pid)


def main():
    refs = {}
    for name, guid, builder, collider in PREFABS:
        parts = builder()
        root = write_prefab(name, guid, parts, collider)
        lo, hi = prefab_bbox(parts)
        size = (round(hi[0] - lo[0], 4), round(hi[1] - lo[1], 4), round(hi[2] - lo[2], 4))
        refs[name] = (root, guid, size)
        mark = "  [collider]" if collider else ""
        print("prefab %-32s size %5.2f x %5.2f x %5.2f%s" % (name, size[0], size[1], size[2], mark))

    for name, size in EXISTING_SIZE.items():
        root, guid = read_prefab_ref(name)
        refs[name] = (root, guid, size)

    obstacles = list(OBSTACLES) + existing_props_as_obstacles()
    placed, errors, boxes = [], [], []

    for item in LAYOUT:
        prefab = KIT.get(item["prefab"], item["prefab"])
        if prefab not in refs:
            errors.append(item["id"] + ": 프리팹 " + prefab + " 이 없다")
            continue
        root, guid, size = refs[prefab]
        bounds = space_bounds(item["kind"], item["key"])
        clearance = item["clearance"]
        z = item["z"]
        if z == Z_PORT:
            z = bounds[2] + size[2] / 2 + clearance
        elif z in (Z_STAR, Z_INNER):
            z = bounds[3] - size[2] / 2 - clearance
        anchor = solve_anchor(bounds, size, item["x"], z, clearance)
        tag = "%s[%s]/%s" % (item["kind"], item["key"], item["id"])
        if abs(anchor[0]) > 1.0001 or abs(anchor[1]) > 1.0001:
            errors.append("%s: 앵커 %s 가 공간 밖이다" % (tag, anchor))
            continue
        c = world_center(bounds, size, anchor, item["bottom_y"], clearance)
        box = (c[0] - size[0] / 2, c[0] + size[0] / 2,
               c[1] - size[1] / 2, c[1] + size[1] / 2,
               c[2] - size[2] / 2, c[2] + size[2] / 2)
        if box[2] < -1e-3 or box[3] > CEIL + 1e-3:
            errors.append("%s: y %.2f~%.2f 가 바닥/천장을 뚫는다" % (tag, box[2], box[3]))
        for ob in obstacles:
            if overlaps(box, ob[1:]):
                errors.append("%s: %s 와 겹친다 (x %.2f~%.2f y %.2f~%.2f z %.2f~%.2f)"
                              % (tag, ob[0], box[0], box[1], box[2], box[3], box[4], box[5]))
        for other_tag, other in boxes:
            if overlaps(box, other):
                errors.append("%s: 신규 %s 와 겹친다" % (tag, other_tag))
        boxes.append((tag, box))
        placed.append(dict(item=item, root=root, guid=guid, size=size,
                           anchor=anchor, clearance=clearance))

    if errors:
        print("\n배치 실패 %d건 — 에셋을 쓰지 않는다." % len(errors))
        for e in errors:
            print("  " + e)
        return 1

    blocks = []
    for p in placed:
        it = p["item"]
        blocks.append(PROP_TEMPLATE.format(
            id=it["id"],
            kind=0 if it["kind"] == "Zone" else 2,
            zone=ZONE_ENUM[it["key"]] if it["kind"] == "Zone" else 0,
            passage=it["key"] if it["kind"] == "Passage" else 0,
            sx=f(p["size"][0]), sy=f(p["size"][1]), sz=f(p["size"][2]),
            ax=f(p["anchor"][0]), az=f(p["anchor"][1]),
            by=f(it["bottom_y"]), cl=f(p["clearance"]),
            ex=f(it["euler"][0]), ey=f(it["euler"][1]), ez=f(it["euler"][2]),
            pid=p["root"], guid=p["guid"]))

    with open(SET_ASSET, encoding="utf-8") as fh:
        body = fh.read().rstrip("\n")
    new_keys = {(0 if p["item"]["kind"] == "Zone" else 2,
                 ZONE_ENUM[p["item"]["key"]] if p["item"]["kind"] == "Zone" else p["item"]["key"],
                 p["item"]["id"]) for p in placed}
    # 방 서사 정리로 폐기/개명된 슬롯도 기존 asset 에서 제거한다. 새 키만 교체하면
    # 이전 CrateStack/PartsPallet 블록이 남아 총량과 이름이 조용히 되돌아온다.
    new_keys |= retired_layout_keys()
    head, existing = split_prop_blocks(body)
    kept = [b for b in existing if block_key(b) not in new_keys]
    body = head + "\n" + "\n".join(b.rstrip("\n") for b in kept + blocks) + "\n"
    with open(SET_ASSET, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(body)

    print("\n배치 %d건 추가 — 에셋 항목 총 %d개" % (len(placed), body.count("\n  - id: ")))
    return 0


if __name__ == "__main__":
    sys.exit(main())
