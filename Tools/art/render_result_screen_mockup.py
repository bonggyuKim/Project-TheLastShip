"""LAST SHIFT 결과 화면 목업 렌더러.

docs/art/last-shift-result-screen-layout-v1.md 의 좌표·색·타이포 표를 그대로 그린다.
문서의 수치를 고치면 여기 상수도 같이 고쳐야 목업이 스펙과 어긋나지 않는다.

실행: python Tools/art/render_result_screen_mockup.py
출력: docs/art/mockups/last-shift-result-*.png (1920x1080), -verdicts.png (판정 5종 비교 스트립)

Windows 기본 폰트(맑은 고딕)를 쓴다. 인게임 폰트는 Unity 기본 폰트 + OS 폴백이라
실제 렌더와 완전히 같지는 않다 — 문서 §6 참조.
"""

import os

from PIL import Image, ImageDraw, ImageFilter

W, H = 1920, 1080

FONT_R = r"C:\Windows\Fonts\malgun.ttf"
FONT_B = r"C:\Windows\Fonts\malgunbd.ttf"

# --- 팔레트 (문서 §3) ---
DIM = (14, 26, 43)          # 딤 오버레이 색. 검정 아님
CARD = (26, 46, 72)         # 카드 바탕
DIVIDER = (48, 76, 109)
LABEL = (143, 166, 191)
VALUE = (255, 255, 255)
VALUE_ZERO = (110, 130, 153)
CAUSE = (216, 228, 242)
FOOTER = (159, 180, 204)

GREEN = (79, 216, 160)      # 정상 도킹
AMBER = (255, 219, 89)      # 절충 생환 (HUD 불안정 등급색 재사용)
RED = (255, 90, 77)         # 실패 3종 공통

# --- 좌표 (문서 §4, 1920x1080 기준) ---
CARD_X, CARD_Y, CARD_W, CARD_H = 400, 250, 1120, 460
PAD = 40
CONTENT_X = CARD_X + PAD
CONTENT_W = CARD_W - PAD * 2
BAND_H = 14
CHIP_Y, CHIP_H = 286, 34
BIG_Y = 326
CAUSE_Y = 428
DIV1_Y = 502
CELL_LABEL_Y = 526
CELL_VALUE_Y = 552
DIV2_Y = 622
FOOTER_Y = 646
CELL_W = CONTENT_W // 4


def font(path, size):
    from PIL import ImageFont
    return ImageFont.truetype(path, size)


F_BIG = None
F_CAUSE = None
F_CHIP = None
F_LABEL = None
F_VALUE = None
F_FOOTER = None


def init_fonts():
    global F_BIG, F_CAUSE, F_CHIP, F_LABEL, F_VALUE, F_FOOTER
    F_BIG = font(FONT_R, 64)
    F_CAUSE = font(FONT_R, 28)
    F_CHIP = font(FONT_B, 22)
    F_LABEL = font(FONT_R, 20)
    F_VALUE = font(FONT_B, 36)
    F_FOOTER = font(FONT_R, 24)


VERDICTS = [
    dict(
        key="nominal", chip="도킹", big="정상 도킹", cause="", color=GREEN,
        cells=[("포기한 것", "0", True), ("임시 수리", "0", True),
               ("재이탈", "0", True), ("도킹 진척", "150/150", None)],
    ),
    dict(
        key="compromised", chip="도킹", big="절충 생환", cause="포기한 계통 2개", color=AMBER,
        cells=[("포기한 것", "2", False), ("임시 수리", "1회", False),
               ("재이탈", "1회", False), ("도킹 진척", "150/150", None)],
    ),
    dict(
        key="asphyxiation", chip="산소", big="질식",
        cause="산소실 산소 고갈 · 예비 산소 소진", color=RED,
        cells=[("포기한 것", "0", True), ("임시 수리", "2회", False),
               ("재이탈", "1회", False), ("도킹 진척", "96/150", None)],
    ),
    dict(
        key="adrift", chip="도킹", big="표류",
        cause="도킹 진척 138/150 — 추력이 5분 평균 0.46이었다", color=RED,
        cells=[("포기한 것", "1", False), ("임시 수리", "3회", False),
               ("재이탈", "2회", False), ("도킹 진척", "138/150", None)],
    ),
    dict(
        key="thrust", chip="추력", big="추력 부족",
        cause="도착 시점 추력 0.25 (엔진 보호 잠금 12초)", color=RED,
        cells=[("포기한 것", "0", True), ("임시 수리", "1회", False),
               ("재이탈", "0", True), ("도킹 진척", "150/150", None)],
    ),
]


def blend(fg, bg, a):
    return tuple(int(f * a + b * (1 - a)) for f, b in zip(fg, bg))


def draw_backdrop():
    """딤 처리된 인게임 화면을 흉내낸다 — 판이 끝난 배가 뒤에 남아 있어야 한다(문서 §3)."""
    img = Image.new("RGB", (W, H), (18, 34, 54))
    d = ImageDraw.Draw(img)
    for y in range(H):
        t = y / H
        d.line([(0, y), (W, y)], fill=(
            int(16 + 14 * t), int(30 + 20 * t), int(50 + 26 * t)))
    # 별
    for i in range(220):
        x = (i * 8677) % W
        y = (i * 4231) % H
        v = 120 + (i * 37) % 90
        d.point((x, y), fill=(v, v, v + 20))
    # 원반 헐 실루엣 3단 스텝
    d.ellipse([260, 700, 1660, 1120], fill=(38, 62, 88))
    d.ellipse([420, 640, 1500, 900], fill=(48, 76, 106))
    d.ellipse([700, 600, 1220, 760], fill=(58, 90, 124))
    # 창 발광
    for x in range(520, 1400, 110):
        d.rectangle([x, 742, x + 54, 774], fill=(126, 196, 214))
    img = img.filter(ImageFilter.GaussianBlur(1.2))
    # 딤 오버레이 68%
    overlay = Image.new("RGB", (W, H), DIM)
    return Image.blend(img, overlay, 0.68)


def draw_card(d, v, ox=0, oy=0):
    color = v["color"]
    x, y = CARD_X + ox, CARD_Y + oy
    d.rectangle([x, y, x + CARD_W, y + CARD_H], fill=CARD)
    d.rectangle([x, y, x + CARD_W, y + BAND_H], fill=color)

    cx = CONTENT_X + ox
    # 칩
    tw = d.textlength(v["chip"], font=F_CHIP)
    chip_w = int(tw) + 28
    d.rectangle([cx, CHIP_Y + oy, cx + chip_w, CHIP_Y + CHIP_H + oy],
                fill=blend(color, CARD, 0.18))
    d.text((cx + 14, CHIP_Y + 5 + oy), v["chip"], font=F_CHIP, fill=color)

    # 판정 큰 줄
    d.text((cx, BIG_Y + oy), v["big"], font=F_BIG, fill=color)

    # 원인 줄 (정상 도킹은 비운다 — 자리는 고정)
    if v["cause"]:
        d.text((cx, CAUSE_Y + oy), v["cause"], font=F_CAUSE, fill=CAUSE)

    d.line([(cx, DIV1_Y + oy), (cx + CONTENT_W, DIV1_Y + oy)], fill=DIVIDER)

    for i, (label, value, muted) in enumerate(v["cells"]):
        lx = cx + CELL_W * i
        d.text((lx, CELL_LABEL_Y + oy), label, font=F_LABEL, fill=LABEL)
        # muted=None -> 판정색(도킹 진척 셀 전용), True -> 0 이라 낮춘 값, False -> 흰색
        fill = VALUE_ZERO if muted is True else (color if muted is None else VALUE)
        d.text((lx, CELL_VALUE_Y + oy), value, font=F_VALUE, fill=fill)

    d.line([(cx, DIV2_Y + oy), (cx + CONTENT_W, DIV2_Y + oy)], fill=DIVIDER)
    d.text((cx, FOOTER_Y + oy), "다음 판 — Space", font=F_FOOTER, fill=FOOTER)


def render_full(v, out_dir):
    img = draw_backdrop()
    d = ImageDraw.Draw(img)
    draw_card(d, v)
    path = os.path.join(out_dir, f"last-shift-result-{v['key']}.png")
    img.save(path)
    return path


def render_strip(out_dir):
    """판정 5종 상단부만 비교하는 스트립. 색 체계가 성공/실패로 갈리는지 한눈에 본다."""
    row_h = 230
    img = Image.new("RGB", (CARD_W, row_h * len(VERDICTS)), DIM)
    d = ImageDraw.Draw(img)
    for i, v in enumerate(VERDICTS):
        top = row_h * i
        color = v["color"]
        d.rectangle([0, top + 12, CARD_W, top + row_h - 12], fill=CARD)
        d.rectangle([0, top + 12, CARD_W, top + 22], fill=color)
        chip = v["chip"]
        tw = int(d.textlength(chip, font=F_CHIP)) + 28
        d.rectangle([PAD, top + 46, PAD + tw, top + 80], fill=blend(color, CARD, 0.18))
        d.text((PAD + 14, top + 51), chip, font=F_CHIP, fill=color)
        d.text((PAD, top + 88), v["big"], font=F_BIG, fill=color)
        if v["cause"]:
            d.text((PAD, top + 172), v["cause"], font=F_CAUSE, fill=CAUSE)
    path = os.path.join(out_dir, "last-shift-result-verdicts.png")
    img.save(path)
    return path


def main():
    init_fonts()
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    out_dir = os.path.join(root, "docs", "art", "mockups")
    os.makedirs(out_dir, exist_ok=True)
    for key in ("nominal", "compromised", "adrift"):
        v = next(v for v in VERDICTS if v["key"] == key)
        print(render_full(v, out_dir))
    print(render_strip(out_dir))


if __name__ == "__main__":
    main()
