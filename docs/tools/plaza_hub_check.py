"""중앙 광장 허브 배치 v1 - 좌표 정합 검산.

정본: docs/central-plaza-hub-layout-v1.md §2.2 좌표표.
이 스크립트는 기획 검산용이고 씬 실좌표를 안 읽는다 - 원반 내접·프로브 각크기·
정식 RG-1 계산은 game-tech-director 의 EditMode 검사가 닫는다(그 문서 부록).

    python docs/tools/plaza_hub_check.py
"""
W = 1.6  # LastShiftZoneDoor.OpeningWidth
rooms = {
    "중앙광장":   (-6, 6, -6, 6),
    "조종석방":   (-14, -6, -3, 3),
    "산소실방":   (6, 14, -3, 3),
    "전력실":     (-3, 3, -11, -6),
    "냉각실":     (-3, 3, 6, 11),
    "에어록홀":   (-11, -3, -12, -6),
    "숙소":       (3, 9, 6, 10),
}
core = (-2, 2, -2, 2)
# (방, 평면축, 평면좌표, 문중심)
doors = [
    ("조종석방", "x", -6.0, 0.0, "개구부"),
    ("산소실방", "x",  6.0, 0.0, "압력문"),
    ("전력실",   "z", -6.0, 0.0, "압력문"),
    ("냉각실",   "z",  6.0, 0.0, "압력문"),
    ("에어록홀", "z", -6.0, -4.5, "일반문"),
    ("숙소",     "z",  6.0,  4.5, "일반문"),
]
def ov(a, b):
    return a[0] < b[1]-1e-6 and b[0] < a[1]-1e-6 and a[2] < b[3]-1e-6 and b[2] < a[3]-1e-6

print("== 1. 겹침 (모든 쌍) ==")
ks = list(rooms); bad = 0
for i in range(len(ks)):
    for j in range(i+1, len(ks)):
        if ov(rooms[ks[i]], rooms[ks[j]]):
            print(f"  겹침! {ks[i]} x {ks[j]}"); bad += 1
print(f"  쌍 {len(ks)*(len(ks)-1)//2}개, 겹침 {bad}")

print("== 2. 문이 자기 방 경계 + 광장 변에 동시에 얹히는가 ==")
pl = rooms["중앙광장"]
for name, ax, pc, dc, kind in doors:
    r = rooms[name]
    if ax == "x":
        on_own = abs(pc-r[0]) < 1e-6 or abs(pc-r[1]) < 1e-6
        own_ok = r[2]-1e-6 <= dc-W/2 and dc+W/2 <= r[3]+1e-6
        on_plaza = abs(pc-pl[0]) < 1e-6 or abs(pc-pl[1]) < 1e-6
        plaza_ok = pl[2]-1e-6 <= dc-W/2 and dc+W/2 <= pl[3]+1e-6
    else:
        on_own = abs(pc-r[2]) < 1e-6 or abs(pc-r[3]) < 1e-6
        own_ok = r[0]-1e-6 <= dc-W/2 and dc+W/2 <= r[1]+1e-6
        on_plaza = abs(pc-pl[2]) < 1e-6 or abs(pc-pl[3]) < 1e-6
        plaza_ok = pl[0]-1e-6 <= dc-W/2 and dc+W/2 <= pl[1]+1e-6
    ok = on_own and own_ok and on_plaza and plaza_ok
    print(f"  {name:8s} {kind:5s} 평면{ax}={pc:+5.1f} 중심={dc:+5.1f}  {'OK' if ok else 'FAIL'}")

print("== 3. 광장 변 자유면 (>=1.6m) ==")
def free_spans(fixed_lo, fixed_hi, occ):
    occ = sorted(occ); out = []; cur = fixed_lo
    for a, b in occ:
        if a > cur: out.append((cur, a))
        cur = max(cur, b)
    if cur < fixed_hi: out.append((cur, fixed_hi))
    return out
sides = {
    "선수 x=-6 (z축)": (pl[2], pl[3], [(rooms["조종석방"][2], rooms["조종석방"][3])]),
    "선미 x=+6 (z축)": (pl[2], pl[3], [(rooms["산소실방"][2], rooms["산소실방"][3])]),
    "좌현 z=-6 (x축)": (pl[0], pl[1], [(max(pl[0],rooms["전력실"][0]), min(pl[1],rooms["전력실"][1])),
                                        (max(pl[0],rooms["에어록홀"][0]), min(pl[1],rooms["에어록홀"][1]))]),
    "우현 z=+6 (x축)": (pl[0], pl[1], [(max(pl[0],rooms["냉각실"][0]), min(pl[1],rooms["냉각실"][1])),
                                        (max(pl[0],rooms["숙소"][0]), min(pl[1],rooms["숙소"][1]))]),
}
tot = 0; n = 0
for k,(lo,hi,occ) in sides.items():
    sp = [s for s in free_spans(lo,hi,occ) if s[1]-s[0] >= W - 1e-6]
    for s in sp: tot += s[1]-s[0]; n += 1
    print(f"  {k}: {[(round(a,1),round(b,1)) for a,b in sp]}")
print(f"  유효 자유면 {n}구간 / 합 {tot:.1f}m")

print("== 4. SIMUL_ZONES - 광장 안 3구역 동시 판독 영역 ==")
G = {"전력실": (0.0,-11.0,"z",-6.0), "냉각실": (0.0,11.0,"z",6.0), "산소실": (14.0,0.0,"x",6.0)}
def visible(px,pz,g):
    gx,gz,ax,pc = g
    if ax == "z":
        if (pz-pc)*(gz-pc) >= 0: return False
        t = (pc-pz)/(gz-pz); return abs(px + (gx-px)*t) <= W/2 + 1e-9
    else:
        if (px-pc)*(gx-pc) >= 0: return False
        t = (pc-px)/(gx-px); return abs(pz + (gz-pz)*t) <= W/2 + 1e-9
step = 0.05; three = []; two = 0; one = 0; zero = 0; inside_core = 0; tot_pts = 0
x = pl[0]+step/2
while x < pl[1]:
    z = pl[2]+step/2
    while z < pl[3]:
        if core[0] <= x <= core[1] and core[2] <= z <= core[3]:
            inside_core += 1; z += step; continue
        tot_pts += 1
        c = sum(1 for g in G.values() if visible(x,z,g))
        if c >= 3: three.append((round(x,2),round(z,2)))
        elif c == 2: two += 1
        elif c == 1: one += 1
        else: zero += 1
        z += step
    x += step
print(f"  격자 {step}m, 코어 제외 유효점 {tot_pts}")
print(f"  3구역 동시: {len(three)}점  |  2구역: {two}  |  1구역: {one}  |  0구역: {zero}")
if three: print(f"  위반 예시: {three[:5]}")
print("== 5. 이탈 거리 개산 (문 중심 경유, 4m/s) ==")
import math
def d(a,b): return math.hypot(a[0]-b[0], a[1]-b[1])
paths = {
 "조종석방 선수구석 -> 전력실 문": [(-14,-3), (-6,0), (0,-6)],
 "광장 우현선미 구석 -> 냉각실 문": [(6,6), (0,6)],
 "에어록홀 먼구석 -> 전력실 문": [(-11,-12), (-4.5,-6), (0,-6)],
 "숙소 먼구석 -> 냉각실 문": [(9,10), (4.5,6), (0,6)],
 "산소실방 선미구석 -> 산소실 문": [(14,-3), (6,0)],
}
for k,p in paths.items():
    L = sum(d(p[i],p[i+1]) for i in range(len(p)-1))
    print(f"  {k}: {L:.2f}m -> {L/4:.2f}초")
print("== 6. 발자국 합 ==")
s = 0
for k,r in rooms.items():
    a = (r[1]-r[0])*(r[3]-r[2]); s += a
    print(f"  {k}: {r[1]-r[0]:.0f} x {r[3]-r[2]:.0f} = {a:.0f}m2")
print(f"  합 {s:.0f}m2 (코어 {((core[1]-core[0])*(core[3]-core[2])):.0f}m2 공제 -> {s-16:.0f}m2)")
xs=[r[0] for r in rooms.values()]+[r[1] for r in rooms.values()]
zs=[r[2] for r in rooms.values()]+[r[3] for r in rooms.values()]
print(f"  전체 AABB x[{min(xs)},{max(xs)}] z[{min(zs)},{max(zs)}] = {max(xs)-min(xs)} x {max(zs)-min(zs)}")
