"""통로 재설계 v6 도면 계산 — 개구부 2(전력실↔냉각실)의 z 를 고르는 근거.

유니티를 열기 전에 좌표를 확정하기 위한 계산이다. 여기서 나온 수치가
LastShiftShipDimensions 의 상수가 되고, 씬 빌더가 그것으로 일괄 배치한다.

축 규약은 런타임과 같다 — x = 전장, z = 전폭. 판정은 전부 xz 평면에서 한다
(배플·벌크헤드가 바닥부터 천장까지 서 있어 y 는 결과를 안 바꾼다).

실행: python Tools/corridor_v6_layout.py
"""
import io
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

W = 6.0          # 내부 전폭
HW = W / 2       # 3.0
L = 38.0         # 내부 전장 (v6 §2.2: 36 → 38)
HL = L / 2       # 19.0

OPEN_W = 1.6     # 개구부 폭
GAP_Z = 0.4      # 한 통로 안 두 개구부의 z 간격
PASS_W = OPEN_W * 2 + GAP_Z          # 3.6
PASS_OFF_Z = HW - PASS_W / 2         # 1.2
OPEN_OFF_Z = (OPEN_W + GAP_Z) / 2    # 1.0
BAFFLE_T = 0.4
BAFFLE_OFF_T = GAP_Z / (OPEN_W + GAP_Z)   # 0.2

# 방·통로 x 구간 (v6 §2.2 / §3)
ROOM = {
    "cockpit":     (-19.0, -11.0),
    "power":       (-5.0, 0.0),
    "cooling":     (0.0, 5.0),
    "lifesupport": (11.0, 19.0),
}
PASSAGE = {0: (-11.0, -5.0), 1: (5.0, 11.0)}
PASS_CZ = {0: +PASS_OFF_Z, 1: -PASS_OFF_Z}

OPEN_X = {0: -11.0, 1: -5.0, 2: 0.0, 3: +5.0, 4: +11.0}
# 통로 A: 방쪽(0)이 바깥벽, 문쪽(1)이 안쪽. 통로 B: 문쪽(3)이 바깥벽, 방쪽(4)이 안쪽.
# 기존 3구역 배치의 어긋남 규칙을 그대로 승계한다.
OPEN_CZ_FIXED = {
    0: +PASS_OFF_Z + OPEN_OFF_Z,   # +2.2
    1: +PASS_OFF_Z - OPEN_OFF_Z,   # +0.2
    3: -PASS_OFF_Z - OPEN_OFF_Z,   # -2.2
    4: -PASS_OFF_Z + OPEN_OFF_Z,   # -0.2
}
EPS = 1e-9


def rng(cz):
    return (cz - OPEN_W / 2, cz + OPEN_W / 2)


def baffle(passage):
    """배플 상자 (minx, maxx, minz, maxz). near = 방쪽 개구부, far = 문쪽."""
    near, far = (0, 1) if passage == 0 else (4, 3)
    cx = OPEN_X[near] + (OPEN_X[far] - OPEN_X[near]) * BAFFLE_OFF_T
    cz = OPEN_CZ_FIXED[near] + (OPEN_CZ_FIXED[far] - OPEN_CZ_FIXED[near]) * BAFFLE_OFF_T
    return (cx - BAFFLE_T / 2, cx + BAFFLE_T / 2, cz - OPEN_W / 2, cz + OPEN_W / 2)


# ── 1) 3중 관통선 — 해석 판정 ────────────────────────────────────────────────
# 개구부 1(x=-5) 에서 z=a, 개구부 2(x=0) 에서 z=b 를 지나는 직선은 개구부 3(x=+5)
# 평면에서 z = 2b - a 다(등간격 5m 이므로 계수가 정확히 2, -1). 따라서 세 개구부를
# 모두 지나는 직선이 있는가는 구간 산술 하나로 끝난다 — 표본 개수에 안 걸린다.
#
# 눈·표적이 실제로 통로 안에 있는지도 같이 봐야 하는데, 개구부 1·3 의 z 구간은
# 각자 통로 z 구간의 부분집합이라 개구부 평면 바로 옆(x = ∓5 ∓ ε)이 언제나 통로
# 안이다. 배플은 그 ε 자리보다 바깥(x = -9.8 / +9.8)이라 이 선분을 못 막는다.
# 그래서 구간 조건이 곧 관통 가능 조건이다.
def triple_penetration(z2):
    lo1, hi1 = rng(OPEN_CZ_FIXED[1])
    lo2, hi2 = rng(z2)
    lo3, hi3 = rng(OPEN_CZ_FIXED[3])
    beam_lo, beam_hi = 2 * lo2 - hi1, 2 * hi2 - lo1
    lo, hi = max(beam_lo, lo3), min(beam_hi, hi3)
    return (lo, hi) if hi - lo > EPS else None


def triple_margin(z2):
    """관통이 없을 때 남은 여유(m). 빔 구간과 개구부 3 구간 사이의 거리."""
    lo1, hi1 = rng(OPEN_CZ_FIXED[1])
    lo2, hi2 = rng(z2)
    lo3, hi3 = rng(OPEN_CZ_FIXED[3])
    beam_lo, beam_hi = 2 * lo2 - hi1, 2 * hi2 - lo1
    return max(lo3 - beam_hi, beam_lo - hi3)


# ── 2) 개구부 1·2 관통 가시 영역 ─────────────────────────────────────────────
# 통로 A 안 어딘가에서 개구부 1·2 를 모두 지나 냉각실 바닥의 어디까지 보이는가.
# 눈이 개구부 1 바로 앞에 설 수 있으므로 배플은 이 시선을 못 막는다 — 그것이
# §3 미결의 핵심이고, 여기서 재는 것은 "막히는가"가 아니라 "얼마나 보이는가"다.
def visible_span_at(x, z2, from_side):
    """평면 x 에서 보이는 z 구간. from_side = 0 이면 통로 A → 냉각실 방향."""
    if from_side == 0:
        x1, x2 = OPEN_X[1], OPEN_X[2]
    else:
        x1, x2 = OPEN_X[3], OPEN_X[2]
    lo1, hi1 = rng(OPEN_CZ_FIXED[1] if from_side == 0 else OPEN_CZ_FIXED[3])
    lo2, hi2 = rng(z2)
    t = (x - x1) / (x2 - x1)          # 개구부1 → 개구부2 보간 인자, x 가 더 멀면 t > 1
    corners = [a + (b - a) * t for a in (lo1, hi1) for b in (lo2, hi2)]
    lo, hi = min(corners), max(corners)
    return max(lo, -HW), min(hi, HW)


def visible_area(z2, from_side, n=400):
    """목표 방 바닥 중 보이는 면적(m²)과 방 전체 면적."""
    rx0, rx1 = ROOM["cooling"] if from_side == 0 else ROOM["power"]
    step = (rx1 - rx0) / n
    area = 0.0
    for i in range(n):
        x = rx0 + step * (i + 0.5)
        lo, hi = visible_span_at(x, z2, from_side)
        area += max(0.0, hi - lo) * step
    return area, (rx1 - rx0) * W


# ── 3) 브루트포스 교차 확인 ──────────────────────────────────────────────────
# 위 두 계산은 같은 보간 대수를 쓴다. 대수 자체가 틀렸을 때를 잡으려고 독립적인
# 선분-상자 판정으로 한 번 더 센다(런타임 LastShiftSightlineProbe 와 같은 방식).
def seg_hits_box(p, q, box, eps=1e-6):
    minx, maxx, minz, maxz = box
    enter, exit_ = 0.0, 1.0
    for o, d, lo, hi in ((p[0], q[0] - p[0], minx, maxx), (p[1], q[1] - p[1], minz, maxz)):
        if abs(d) < eps:
            if not (lo + eps < o < hi - eps):
                return False
            continue
        a, b = (lo - o) / d, (hi - o) / d
        if a > b:
            a, b = b, a
        enter, exit_ = max(enter, a), min(exit_, b)
        if enter >= exit_:
            return False
    return exit_ - enter > eps


def clear(p, q, z2, eps=1e-6):
    czs = dict(OPEN_CZ_FIXED)
    czs[2] = z2
    for o in (0, 1, 2, 3, 4):
        px = OPEN_X[o]
        lo, hi = min(p[0], q[0]), max(p[0], q[0])
        if px <= lo + eps or px >= hi - eps:
            continue
        z = p[1] + (q[1] - p[1]) * (px - p[0]) / (q[0] - p[0])
        a, b = rng(czs[o])
        if not (a - eps <= z <= b + eps):
            return False
    for passage in (0, 1):
        if seg_hits_box(p, q, baffle(passage)):
            return False
    return True


def lin(a, b, n):
    return [a + (b - a) * i / (n - 1) for i in range(n)]


def brute_triple(z2, n=81):
    """통로 A ↔ 통로 B 직선 관통을 선분 대 상자로 직접 찾는다."""
    ez0, ez1 = PASS_CZ[0] - PASS_W / 2, PASS_CZ[0] + PASS_W / 2
    tz0, tz1 = PASS_CZ[1] - PASS_W / 2, PASS_CZ[1] + PASS_W / 2
    eyes = [(x, z) for x in lin(-10.95, -5.05, 12) for z in lin(ez0 + 0.02, ez1 - 0.02, n)]
    tgts = [(x, z) for x in lin(5.05, 10.95, 12) for z in lin(tz0 + 0.02, tz1 - 0.02, n)]
    for e in eyes:
        for t in tgts:
            if clear(e, t, z2):
                return e, t
    return None


CANDIDATES = [round(-2.2 + 0.2 * i, 1) for i in range(23)]

print("v6 도면 — 개구부 2(전력실↔냉각실) z 후보 평가")
print(f"선체 {L:.0f}m × {W:.0f}m | 개구부 x = " + ", ".join(f"{o}:{OPEN_X[o]:+.0f}" for o in OPEN_X))
print(f"개구부 z 고정분 = " + ", ".join(f"{o}:{OPEN_CZ_FIXED[o]:+.1f}" for o in sorted(OPEN_CZ_FIXED)))
print(f"배플 A = {tuple(round(v,2) for v in baffle(0))}   배플 B = {tuple(round(v,2) for v in baffle(1))}")
print()
print(f"{'z2':>6} {'개구부2 z구간':>15} {'1-2-3관통':>10} {'여유':>7} "
      f"{'냉각실가시':>13} {'전력실가시':>13}")
print("-" * 72)

viable = []
for z2 in CANDIDATES:
    lo, hi = rng(z2)
    if lo < -HW - EPS or hi > HW + EPS:
        continue
    hit = triple_penetration(z2)
    margin = triple_margin(z2)
    va, ta = visible_area(z2, 0)
    vb, tb = visible_area(z2, 1)
    print(f"{z2:>+6.1f} {f'[{lo:+.1f},{hi:+.1f}]':>15} {'뚫림' if hit else '없음':>10} "
          f"{margin:>+6.2f}m {va:>7.1f}m² {va/ta*100:>3.0f}% {vb:>7.1f}m² {vb/tb*100:>3.0f}%")
    if not hit:
        viable.append((va + vb, z2, va, vb, margin))

print()
if not viable:
    print("3중 관통선을 없애는 z2 가 없다 - 다른 구조가 필요하다.")
    sys.exit(1)

viable.sort()
tot, z2, va, vb, margin = viable[0]
print(f"권장 z2 = {z2:+.1f}   개구부 2 z 구간 [{rng(z2)[0]:+.1f}, {rng(z2)[1]:+.1f}]")
print(f"  3중 관통선 없음 (여유 {margin:+.2f}m)")
print(f"  개구부 1-2 관통으로 보이는 냉각실 바닥 {va:.1f}m² ({va/(5*W)*100:.0f}%)")
print(f"  개구부 3-2 관통으로 보이는 전력실 바닥 {vb:.1f}m² ({vb/(5*W)*100:.0f}%)")

print()
print("브루트포스 교차 확인 (선분 대 상자, 대수와 독립):")
for label, cand in (("권장", z2), ("최악(0.0)", 0.0)):
    found = brute_triple(cand)
    print(f"  z2={cand:+.1f} {label:>9}: " +
          (f"관통 발견 눈{tuple(round(v,2) for v in found[0])} → 표적{tuple(round(v,2) for v in found[1])}"
           if found else "관통 없음"))

# ── 확정 z2 의 노출 원뿔 (art/ta 인계용) ────────────────────────────────────
# 전력실↔냉각실 문(개구부 2)은 형상으로 시선을 못 막는다(벽 하나에 구멍 하나뿐이라
# 막는 판이 곧 문을 막는다). 그래서 기획은 "상태 단서를 노출 원뿔 밖에 둔다" 로 결정했다.
# 그 원뿔을 x 단면별 z 구간으로 뽑아 넘긴다 — 이펙트 배치는 좌표 문제가 아니라 이 구간
# 밖에 두는가의 문제다.
DECIDED_Z2 = +2.2

print()
print("=" * 72)
print(f"확정 개구부 2 z = {DECIDED_Z2:+.1f}  (구간 [{rng(DECIDED_Z2)[0]:+.1f}, {rng(DECIDED_Z2)[1]:+.1f}])")
print(f"  1-2-3 관통 여유 {triple_margin(DECIDED_Z2):+.2f}m")
va, _ = visible_area(DECIDED_Z2, 0)
vb, _ = visible_area(DECIDED_Z2, 1)
print(f"  냉각실 노출 {va:.1f}m² / 전력실 노출 {vb:.1f}m²")
print()
print("노출 원뿔 — 이 z 구간 안에는 상태 단서(서리·스파크·연기)를 두지 않는다")
print(f"{'x':>7} {'냉각실 노출 z':>22} {'x':>7} {'전력실 노출 z':>22}")
print("-" * 62)
for i in range(11):
    xc = ROOM["cooling"][0] + (ROOM["cooling"][1] - ROOM["cooling"][0]) * i / 10
    xp = ROOM["power"][1] - (ROOM["power"][1] - ROOM["power"][0]) * i / 10
    lo_c, hi_c = visible_span_at(xc, DECIDED_Z2, 0)
    lo_p, hi_p = visible_span_at(xp, DECIDED_Z2, 1)
    cell_c = f"[{lo_c:+.2f}, {hi_c:+.2f}]" if hi_c > lo_c else "없음"
    cell_p = f"[{lo_p:+.2f}, {hi_p:+.2f}]" if hi_p > lo_p else "없음"
    print(f"{xc:>+7.1f} {cell_c:>22} {xp:>+7.1f} {cell_p:>22}")
print()
print("→ 안전대(단서를 두어도 되는 자리): 각 x 단면에서 위 구간 밖의 z. "
      f"선체 z 범위는 [{-HW:+.1f}, {HW:+.1f}] 이다.")
print("=" * 72)

print()
print(f"권장 z2 에서 냉각실 가시 z 구간 (개구부 3 벽 x=+5 기준): "
      f"[{visible_span_at(5.0, z2, 0)[0]:+.2f}, {visible_span_at(5.0, z2, 0)[1]:+.2f}]  "
      f"개구부 3 구간 [{rng(OPEN_CZ_FIXED[3])[0]:+.1f}, {rng(OPEN_CZ_FIXED[3])[1]:+.1f}]")
print(f"권장 z2 에서 전력실 가시 z 구간 (개구부 1 벽 x=-5 기준): "
      f"[{visible_span_at(-5.0, z2, 1)[0]:+.2f}, {visible_span_at(-5.0, z2, 1)[1]:+.2f}]  "
      f"개구부 1 구간 [{rng(OPEN_CZ_FIXED[1])[0]:+.1f}, {rng(OPEN_CZ_FIXED[1])[1]:+.1f}]")
