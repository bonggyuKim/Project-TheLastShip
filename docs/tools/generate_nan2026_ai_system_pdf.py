"""Generate the NAN 2026 DoodleUp AI development system technical brief.

The PDF is intentionally evidence-led. It combines repository-backed metrics,
tracked project images, vector diagrams, and explicit submission caveats.
"""

from __future__ import annotations

from math import atan2, cos, pi, sin
from pathlib import Path
from typing import Iterable, Sequence

from reportlab.lib.colors import Color, HexColor, white
from reportlab.lib.pagesizes import A4
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "output" / "pdf" / "doodleup-nan2026-ai-development-system.pdf"
FONT_REGULAR = Path(r"C:\Windows\Fonts\malgun.ttf")
FONT_BOLD = Path(r"C:\Windows\Fonts\malgunbd.ttf")

LIME_NEUTRAL = (
    ROOT
    / "Assets"
    / "DoodleUp"
    / "Art"
    / "Characters"
    / "LastShiftLimeAlien"
    / "LastShiftLimeAlien_Rig_NeutralPose.png"
)
LIME_STRESS = (
    ROOT
    / "Assets"
    / "DoodleUp"
    / "Art"
    / "Characters"
    / "LastShiftLimeAlien"
    / "LastShiftLimeAlien_Rig_StressPose.png"
)
RESULT_NOMINAL = ROOT / "docs" / "art" / "mockups" / "last-shift-result-nominal.png"

W, H = A4
M = 42
CW = W - 2 * M

NAVY = HexColor("#0B1728")
NAVY_2 = HexColor("#142A43")
NAVY_3 = HexColor("#203A55")
PAPER = HexColor("#F4F7F5")
PAPER_2 = HexColor("#E9F0ED")
INK = HexColor("#122033")
MUTED = HexColor("#617084")
LINE = HexColor("#CFDAD7")
LIME = HexColor("#5EE2A2")
LIME_DARK = HexColor("#178B64")
CYAN = HexColor("#58C8E6")
YELLOW = HexColor("#F5C75B")
RED = HexColor("#F26C68")
VIOLET = HexColor("#9A8DF2")
SOFT_LIME = HexColor("#DDF8EA")
SOFT_CYAN = HexColor("#DFF5FA")
SOFT_YELLOW = HexColor("#FFF3D2")
SOFT_RED = HexColor("#FDE6E4")
SOFT_VIOLET = HexColor("#ECE9FD")


def register_fonts() -> None:
    if not FONT_REGULAR.exists() or not FONT_BOLD.exists():
        raise FileNotFoundError("Malgun Gothic fonts are required.")
    pdfmetrics.registerFont(TTFont("KR", str(FONT_REGULAR)))
    pdfmetrics.registerFont(TTFont("KR-Bold", str(FONT_BOLD)))


def safe_text(value: str) -> str:
    """Use ASCII hyphens for dash-like punctuation."""
    return (
        value.replace("\u2011", "-")
        .replace("\u2012", "-")
        .replace("\u2013", "-")
        .replace("\u2014", "-")
    )


def text_width(text: str, font: str, size: float) -> float:
    return pdfmetrics.stringWidth(safe_text(text), font, size)


def split_long_token(token: str, max_width: float, font: str, size: float) -> list[str]:
    parts: list[str] = []
    current = ""
    for char in token:
        candidate = current + char
        if current and text_width(candidate, font, size) > max_width:
            parts.append(current)
            current = char
        else:
            current = candidate
    if current:
        parts.append(current)
    return parts


def wrap_text(text: str, max_width: float, font: str, size: float) -> list[str]:
    wrapped: list[str] = []
    for paragraph in safe_text(text).splitlines() or [""]:
        if not paragraph:
            wrapped.append("")
            continue
        words = paragraph.split(" ")
        line = ""
        for word in words:
            if text_width(word, font, size) > max_width:
                if line:
                    wrapped.append(line)
                    line = ""
                chunks = split_long_token(word, max_width, font, size)
                wrapped.extend(chunks[:-1])
                line = chunks[-1]
                continue
            candidate = word if not line else f"{line} {word}"
            if line and text_width(candidate, font, size) > max_width:
                wrapped.append(line)
                line = word
            else:
                line = candidate
        if line:
            wrapped.append(line)
    return wrapped


def draw_text(
    c: canvas.Canvas,
    text: str,
    x: float,
    y_top: float,
    max_width: float,
    *,
    font: str = "KR",
    size: float = 10,
    color: Color = INK,
    leading: float | None = None,
    max_lines: int | None = None,
    align: str = "left",
) -> float:
    leading = leading or size * 1.45
    lines = wrap_text(text, max_width, font, size)
    if max_lines is not None and len(lines) > max_lines:
        lines = lines[:max_lines]
        last = lines[-1]
        while last and text_width(last + "...", font, size) > max_width:
            last = last[:-1]
        lines[-1] = last + "..."
    c.setFont(font, size)
    c.setFillColor(color)
    y = y_top
    for line in lines:
        if align == "center":
            c.drawCentredString(x + max_width / 2, y, line)
        elif align == "right":
            c.drawRightString(x + max_width, y, line)
        else:
            c.drawString(x, y, line)
        y -= leading
    return y


def rounded_box(
    c: canvas.Canvas,
    x: float,
    y: float,
    w: float,
    h: float,
    *,
    fill: Color = white,
    stroke: Color | None = LINE,
    radius: float = 12,
    line_width: float = 0.8,
) -> None:
    c.saveState()
    c.setFillColor(fill)
    if stroke is None:
        c.setStrokeColor(fill)
        c.setLineWidth(0)
    else:
        c.setStrokeColor(stroke)
        c.setLineWidth(line_width)
    c.roundRect(x, y, w, h, radius, fill=1, stroke=0 if stroke is None else 1)
    c.restoreState()


def chip(
    c: canvas.Canvas,
    text: str,
    x: float,
    y: float,
    *,
    fill: Color = SOFT_LIME,
    color: Color = LIME_DARK,
    size: float = 8,
    pad_x: float = 8,
    h: float = 20,
) -> float:
    label = safe_text(text)
    w = text_width(label, "KR-Bold", size) + 2 * pad_x
    rounded_box(c, x, y, w, h, fill=fill, stroke=None, radius=h / 2)
    c.setFont("KR-Bold", size)
    c.setFillColor(color)
    c.drawCentredString(x + w / 2, y + (h - size) / 2 + 1.5, label)
    return w


def circle_label(
    c: canvas.Canvas,
    x: float,
    y: float,
    r: float,
    text: str,
    *,
    fill: Color,
    text_color: Color = INK,
    size: float = 8,
    stroke: Color | None = None,
) -> None:
    c.saveState()
    c.setFillColor(fill)
    c.setStrokeColor(stroke or fill)
    c.circle(x, y, r, fill=1, stroke=1 if stroke else 0)
    lines = wrap_text(text, r * 1.55, "KR-Bold", size)
    total = len(lines) * size * 1.25
    yy = y + total / 2 - size
    c.setFont("KR-Bold", size)
    c.setFillColor(text_color)
    for line in lines:
        c.drawCentredString(x, yy, line)
        yy -= size * 1.25
    c.restoreState()


def arrow(
    c: canvas.Canvas,
    x1: float,
    y1: float,
    x2: float,
    y2: float,
    *,
    color: Color = MUTED,
    width: float = 1.6,
    head: float = 7,
    dashed: bool = False,
) -> None:
    c.saveState()
    c.setStrokeColor(color)
    c.setFillColor(color)
    c.setLineWidth(width)
    if dashed:
        c.setDash(4, 3)
    c.line(x1, y1, x2, y2)
    angle = atan2(y2 - y1, x2 - x1)
    a1 = angle + pi * 0.84
    a2 = angle - pi * 0.84
    path = c.beginPath()
    path.moveTo(x2, y2)
    path.lineTo(x2 + head * cos(a1), y2 + head * sin(a1))
    path.lineTo(x2 + head * cos(a2), y2 + head * sin(a2))
    path.close()
    c.drawPath(path, fill=1, stroke=0)
    c.restoreState()


def image_contain(
    c: canvas.Canvas,
    path: Path,
    x: float,
    y: float,
    w: float,
    h: float,
    *,
    background: Color | None = None,
) -> None:
    if not path.exists():
        raise FileNotFoundError(path)
    image = ImageReader(str(path))
    iw, ih = image.getSize()
    scale = min(w / iw, h / ih)
    dw, dh = iw * scale, ih * scale
    if background:
        c.setFillColor(background)
        c.rect(x, y, w, h, fill=1, stroke=0)
    c.drawImage(
        image,
        x + (w - dw) / 2,
        y + (h - dh) / 2,
        width=dw,
        height=dh,
        preserveAspectRatio=True,
        mask="auto",
    )


def image_cover(c: canvas.Canvas, path: Path, x: float, y: float, w: float, h: float) -> None:
    if not path.exists():
        raise FileNotFoundError(path)
    image = ImageReader(str(path))
    iw, ih = image.getSize()
    scale = max(w / iw, h / ih)
    dw, dh = iw * scale, ih * scale
    c.saveState()
    clip = c.beginPath()
    clip.roundRect(x, y, w, h, 10)
    c.clipPath(clip, stroke=0, fill=0)
    c.drawImage(
        image,
        x + (w - dw) / 2,
        y + (h - dh) / 2,
        width=dw,
        height=dh,
        preserveAspectRatio=True,
        mask="auto",
    )
    c.restoreState()


def section_header(
    c: canvas.Canvas,
    page: int,
    kicker: str,
    title: str,
    subtitle: str | None = None,
) -> float:
    c.setFillColor(PAPER)
    c.rect(0, 0, W, H, fill=1, stroke=0)
    chip(c, kicker.upper(), M, H - 66, fill=NAVY, color=LIME, size=7.5, h=19)
    title = safe_text(title)
    title_size = 25.0
    while title_size > 18 and text_width(title, "KR-Bold", title_size) > CW:
        title_size -= 0.5
    c.setFont("KR-Bold", title_size)
    c.setFillColor(INK)
    c.drawString(M, H - 110, title)
    y = H - 132
    if subtitle:
        subtitle = safe_text(subtitle)
        subtitle_size = 9.4
        while subtitle_size > 7.4 and text_width(subtitle, "KR", subtitle_size) > CW:
            subtitle_size -= 0.25
        c.setFont("KR", subtitle_size)
        c.setFillColor(MUTED)
        c.drawString(M, H - 127, subtitle)
        y = H - 141
    c.setStrokeColor(LINE)
    c.setLineWidth(0.8)
    c.line(M, 42, W - M, 42)
    c.setFont("KR", 7.4)
    c.setFillColor(MUTED)
    c.drawString(M, 25, "DoodleUp AI Development System / NAN 2026 submission review")
    c.setFont("KR-Bold", 8)
    c.drawRightString(W - M, 25, f"{page:02d}")
    key = f"page-{page}"
    c.bookmarkPage(key)
    c.addOutlineEntry(safe_text(title), key, level=0, closed=False)
    return y - 4


def metric_card(
    c: canvas.Canvas,
    x: float,
    y: float,
    w: float,
    h: float,
    value: str,
    label: str,
    *,
    accent: Color = LIME,
    note: str | None = None,
) -> None:
    rounded_box(c, x, y, w, h, fill=white, stroke=LINE, radius=10)
    c.setFillColor(accent)
    c.roundRect(x, y + h - 5, w, 5, 3, fill=1, stroke=0)
    c.setFont("KR-Bold", 19)
    c.setFillColor(INK)
    c.drawString(x + 12, y + h - 32, safe_text(value))
    draw_text(c, label, x + 12, y + h - 51, w - 24, font="KR-Bold", size=8, color=MUTED, leading=11, max_lines=2)
    if note:
        draw_text(c, note, x + 12, y + 14, w - 24, size=6.8, color=MUTED, leading=9, max_lines=2)


def pass_row(c: canvas.Canvas, x: float, y: float, w: float, label: str, value: str) -> None:
    c.setFillColor(SOFT_LIME)
    c.circle(x + 6, y + 3, 6, fill=1, stroke=0)
    c.setFont("KR-Bold", 7)
    c.setFillColor(LIME_DARK)
    c.drawCentredString(x + 6, y + 0.5, "P")
    c.setFont("KR", 8.5)
    c.setFillColor(INK)
    c.drawString(x + 18, y, safe_text(label))
    c.setFont("KR-Bold", 8.5)
    c.setFillColor(LIME_DARK)
    c.drawRightString(x + w, y, safe_text(value))


def draw_table(
    c: canvas.Canvas,
    x: float,
    y_top: float,
    widths: Sequence[float],
    headers: Sequence[str],
    rows: Sequence[Sequence[str]],
    *,
    row_heights: Sequence[float] | float,
    font_size: float = 7.6,
    header_height: float = 30,
    header_fill: Color = NAVY,
) -> float:
    total_w = sum(widths)
    c.setFillColor(header_fill)
    c.roundRect(x, y_top - header_height, total_w, header_height, 8, fill=1, stroke=0)
    xx = x
    for width, header in zip(widths, headers):
        draw_text(
            c,
            header,
            xx + 7,
            y_top - 12,
            width - 14,
            font="KR-Bold",
            size=7.5,
            color=white,
            leading=10,
            max_lines=2,
        )
        xx += width
    y = y_top - header_height
    heights = [row_heights] * len(rows) if isinstance(row_heights, (int, float)) else list(row_heights)
    for index, (row, rh) in enumerate(zip(rows, heights)):
        fill = white if index % 2 == 0 else PAPER_2
        c.setFillColor(fill)
        c.rect(x, y - rh, total_w, rh, fill=1, stroke=0)
        c.setStrokeColor(LINE)
        c.setLineWidth(0.45)
        c.line(x, y - rh, x + total_w, y - rh)
        xx = x
        for col_index, (width, cell) in enumerate(zip(widths, row)):
            if col_index:
                c.line(xx, y, xx, y - rh)
            draw_text(
                c,
                cell,
                xx + 7,
                y - 14,
                width - 14,
                font="KR-Bold" if col_index == 0 else "KR",
                size=font_size,
                color=INK if col_index == 0 else MUTED,
                leading=font_size * 1.42,
                max_lines=max(2, int((rh - 10) / (font_size * 1.42))),
            )
            xx += width
        y -= rh
    c.setStrokeColor(LINE)
    c.roundRect(x, y, total_w, y_top - y, 8, fill=0, stroke=1)
    return y


def ratio_bar(
    c: canvas.Canvas,
    x: float,
    y: float,
    w: float,
    label: str,
    ratio: float,
    value: str,
    *,
    color: Color = LIME,
    label_color: Color = MUTED,
    value_color: Color = INK,
) -> None:
    c.setFont("KR", 7.8)
    c.setFillColor(label_color)
    c.drawString(x, y + 11, safe_text(label))
    c.setFont("KR-Bold", 7.8)
    c.setFillColor(value_color)
    c.drawRightString(x + w, y + 11, safe_text(value))
    c.setFillColor(PAPER_2)
    c.roundRect(x, y, w, 7, 3.5, fill=1, stroke=0)
    c.setFillColor(color)
    c.roundRect(x, y, max(7, w * max(0, min(1, ratio))), 7, 3.5, fill=1, stroke=0)


def draw_cover(c: canvas.Canvas) -> None:
    c.setFillColor(NAVY)
    c.rect(0, 0, W, H, fill=1, stroke=0)
    c.setFillColor(NAVY_2)
    for gx in range(28, int(W), 28):
        for gy in range(26, int(H), 28):
            if (gx + gy) % 84 == 0:
                c.circle(gx, gy, 0.7, fill=1, stroke=0)
    c.setFillColor(LIME)
    c.rect(0, H - 9, W, 9, fill=1, stroke=0)

    chip(c, "NAN 2026 / GAME X AI", M, H - 72, fill=NAVY_3, color=LIME, size=8, h=22)
    c.setFont("KR-Bold", 34)
    c.setFillColor(white)
    c.drawString(M, H - 142, "DoodleUp")
    c.setFont("KR-Bold", 30)
    c.setFillColor(LIME)
    c.drawString(M, H - 184, "AI 개발 시스템 기술서")
    draw_text(
        c,
        "인간 디렉터의 의도를 기억하고, 전문 역할로 구현하며, Unity 증거로 검증하는 AI-native 게임 개발 조직",
        M,
        H - 220,
        430,
        size=12,
        color=HexColor("#C7D7E6"),
        leading=18,
        max_lines=3,
    )

    rounded_box(c, M, 108, 333, 382, fill=NAVY_2, stroke=NAVY_3, radius=18)
    c.setFont("KR-Bold", 9)
    c.setFillColor(CYAN)
    c.drawString(M + 20, 465, "THE CLOSED DEVELOPMENT LOOP")

    nodes = [
        (M + 66, 391, "인간\n디렉터", LIME),
        (M + 166, 391, "AgentDesk\n오케스트레이션", CYAN),
        (M + 266, 391, "역할형\nAI 에이전트", VIOLET),
        (M + 266, 265, "컴파일\n자동 테스트", YELLOW),
        (M + 166, 265, "독립 QA\n증거 검토", RED),
        (M + 66, 265, "플레이\n승인", LIME),
    ]
    for index, (x, y, label, color) in enumerate(nodes):
        circle_label(c, x, y, 37, label, fill=Color(color.red, color.green, color.blue, alpha=0.15), text_color=white, size=7.5, stroke=color)
        nx, ny, _, _ = nodes[(index + 1) % len(nodes)]
        vx, vy = nx - x, ny - y
        length = max((vx * vx + vy * vy) ** 0.5, 1)
        arrow(
            c,
            x + vx / length * 41,
            y + vy / length * 41,
            nx - vx / length * 41,
            ny - vy / length * 41,
            color=color,
            width=1.5,
            head=6,
        )

    rounded_box(c, M + 37, 145, 260, 55, fill=NAVY_3, stroke=None, radius=10)
    c.setFont("KR-Bold", 10)
    c.setFillColor(LIME)
    c.drawString(M + 52, 177, "AnchorMind / Memento")
    c.setFont("KR", 8)
    c.setFillColor(HexColor("#C7D7E6"))
    c.drawString(M + 52, 159, "결정 · 오류 · 절차 · 선호 · 작업 이력을 세션 사이에 유지")

    rounded_box(c, 400, 108, 153, 382, fill=HexColor("#111F31"), stroke=NAVY_3, radius=18)
    image_contain(c, LIME_NEUTRAL, 412, 189, 129, 245, background=HexColor("#111F31"))
    c.setFont("KR-Bold", 10)
    c.setFillColor(white)
    c.drawString(416, 165, "LAST SHIFT")
    c.setFont("KR", 7.5)
    c.setFillColor(HexColor("#AFC1D3"))
    c.drawString(416, 148, "tracked rig evidence")
    c.drawString(416, 135, "Blender -> FBX -> Unity")

    c.setFont("KR", 8)
    c.setFillColor(HexColor("#9DB1C5"))
    c.drawString(M, 73, "제출 검토본 v1.0  |  2026.08.10  |  기준 HEAD 1d9a04c")
    c.setFont("KR-Bold", 8)
    c.setFillColor(LIME)
    c.drawRightString(W - M, 73, "01 / 13")
    c.bookmarkPage("page-1")
    c.addOutlineEntry("표지", "page-1", level=0, closed=False)
    c.showPage()


def draw_executive_summary(c: canvas.Canvas) -> None:
    section_header(
        c,
        2,
        "01 / EXECUTIVE SUMMARY",
        "결과물이 아니라, AI로 운영되는 개발 시스템",
        "NAN 2026의 사전 과제인 AI를 활용한 게임 제작에 대해 DoodleUp이 제시하는 답",
    )

    rounded_box(c, M, 620, CW, 78, fill=NAVY, stroke=None, radius=14)
    chip(c, "OFFICIAL EVENT FIT", M + 16, 662, fill=NAVY_3, color=LIME, size=7, h=18)
    c.setFont("KR-Bold", 14)
    c.setFillColor(white)
    c.drawString(M + 16, 638, "AI의 다음 단계를 설계할 디렉터")
    draw_text(
        c,
        "모집 2026.07.10-08.10 · 10팀 선발 · 2026.09.04-09.06 · NHN 사옥 48시간 오프라인",
        M + 275,
        663,
        CW - 291,
        size=7.8,
        color=HexColor("#C7D7E6"),
        leading=12,
        max_lines=3,
    )

    rounded_box(c, M, 409, 304, 204, fill=white, stroke=LINE, radius=14)
    c.setFont("KR-Bold", 11)
    c.setFillColor(LIME_DARK)
    c.drawString(M + 18, 584, "한 문장 요약")
    draw_text(
        c,
        "인간 디렉터가 목표와 플레이 감각을 정하면 AgentDesk가 작업을 분해하고, 기획·기술·아트·QA AI가 격리된 Git 작업공간에서 구현한다. AnchorMind/Memento는 프로젝트 기억을 유지하고, Unity 컴파일·테스트·수치 검증·독립 QA가 결과를 증명한다.",
        M + 18,
        556,
        268,
        size=10.2,
        color=INK,
        leading=16,
        max_lines=9,
    )
    draw_text(
        c,
        "핵심: AI가 게임을 대신 판단하지 않는다. 인간의 창작 의도를 전문 에이전트가 구현하고 자동 증거로 검증한다.",
        M + 18,
        452,
        268,
        font="KR-Bold",
        size=8.6,
        color=LIME_DARK,
        leading=13,
        max_lines=3,
    )

    rounded_box(c, 360, 409, 193, 204, fill=NAVY_2, stroke=None, radius=14)
    image_cover(c, RESULT_NOMINAL, 371, 443, 171, 132)
    c.setFont("KR-Bold", 8.5)
    c.setFillColor(white)
    c.drawString(373, 424, "LAST SHIFT 결과 화면 디자인 시안")
    c.setFont("KR", 6.8)
    c.setFillColor(HexColor("#AFC1D3"))
    c.drawString(373, 411, "tracked project artifact / docs/art/mockups")

    principles = [
        ("01", "기억", "결정과 실패를 세션 사이에 유지", LIME, SOFT_LIME),
        ("02", "역할", "기획·구현·QA의 판단 기준을 분리", CYAN, SOFT_CYAN),
        ("03", "증거", "말이 아니라 build·test·raw data로 완료", VIOLET, SOFT_VIOLET),
    ]
    gap = 10
    pw = (CW - 2 * gap) / 3
    for index, (num, title, body, accent, fill) in enumerate(principles):
        x = M + index * (pw + gap)
        rounded_box(c, x, 286, pw, 97, fill=fill, stroke=None, radius=12)
        c.setFont("KR-Bold", 8)
        c.setFillColor(accent)
        c.drawString(x + 13, 358, num)
        c.setFont("KR-Bold", 13)
        c.setFillColor(INK)
        c.drawString(x + 13, 334, title)
        draw_text(c, body, x + 13, 313, pw - 26, size=7.6, color=MUTED, leading=11, max_lines=2)

    values = [
        ("321", "main commits", LIME),
        ("968", "tracked files", CYAN),
        ("63", "docs Markdown", VIOLET),
        ("67", "test C# sources", YELLOW),
        ("618", "test annotations", RED),
    ]
    mgap = 8
    mw = (CW - 4 * mgap) / 5
    for index, (value, label, accent) in enumerate(values):
        metric_card(c, M + index * (mw + mgap), 165, mw, 96, value, label, accent=accent)

    rounded_box(c, M, 80, CW, 62, fill=SOFT_YELLOW, stroke=None, radius=10)
    c.setFont("KR-Bold", 8.5)
    c.setFillColor(HexColor("#8A6212"))
    c.drawString(M + 14, 121, "증거 해석 원칙")
    draw_text(
        c,
        "618은 현재 전체 PASS 수가 아니라 소스의 Test/UnityTest annotation 수다. 단계별 QA PASS와 저장소 규모 지표를 섞지 않는다. 시간 절감률·API 비용은 원본 기록을 확보하기 전까지 기재하지 않는다.",
        M + 14,
        102,
        CW - 28,
        size=7.5,
        color=HexColor("#785D25"),
        leading=11,
        max_lines=3,
    )
    c.showPage()


def draw_architecture(c: canvas.Canvas) -> None:
    section_header(
        c,
        3,
        "02 / SYSTEM ARCHITECTURE",
        "의도에서 플레이 승인까지 닫히는 개발 루프",
        "AgentDesk가 작업 흐름을 조율하고, Memento가 모든 단계를 가로지르는 장기기억 계층으로 작동한다.",
    )

    top_y = 611
    boxes = [
        (M, top_y, 103, 86, "1", "인간 디렉터", "목표 · 우선순위\n플레이 감각", LIME, SOFT_LIME),
        (M + 125, top_y, 113, 86, "2", "AgentDesk", "카드 · 의존성\n역할 배정", CYAN, SOFT_CYAN),
        (M + 260, top_y, 120, 86, "3", "전문 AI", "기획 · 기술\n아트 · QA", VIOLET, SOFT_VIOLET),
        (M + 402, top_y, 109, 86, "4", "격리 실행", "branch · worktree\nUnity · Blender", YELLOW, SOFT_YELLOW),
    ]
    for index, (x, y, w, h, num, title, body, accent, fill) in enumerate(boxes):
        rounded_box(c, x, y, w, h, fill=fill, stroke=None, radius=12)
        c.setFillColor(accent)
        c.circle(x + 17, y + h - 18, 9, fill=1, stroke=0)
        c.setFont("KR-Bold", 7)
        c.setFillColor(NAVY)
        c.drawCentredString(x + 17, y + h - 20.5, num)
        c.setFont("KR-Bold", 10)
        c.setFillColor(INK)
        c.drawString(x + 12, y + h - 41, title)
        draw_text(c, body, x + 12, y + h - 59, w - 24, size=7.4, color=MUTED, leading=10, max_lines=2)
        if index < len(boxes) - 1:
            nx = boxes[index + 1][0]
            arrow(c, x + w + 4, y + h / 2, nx - 5, y + h / 2, color=MUTED, width=1.4, head=6)

    rounded_box(c, M, 413, CW, 166, fill=white, stroke=LINE, radius=14)
    c.setFont("KR-Bold", 9)
    c.setFillColor(INK)
    c.drawString(M + 18, 551, "검증과 승인")
    stages = [
        ("컴파일", "C# error 0", CYAN),
        ("자동 테스트", "EditMode · PlayMode", VIOLET),
        ("정량 검증", "좌표 · 상태 · hash", YELLOW),
        ("독립 QA", "raw 재계산", RED),
        ("인간 승인", "Editor play · feel", LIME),
    ]
    sx = M + 30
    sy = 484
    step = 98
    for index, (title, body, accent) in enumerate(stages):
        circle_label(c, sx + index * step, sy, 30, title, fill=white, text_color=INK, size=7.3, stroke=accent)
        draw_text(c, body, sx + index * step - 39, sy - 46, 78, size=6.8, color=MUTED, leading=9, max_lines=2, align="center")
        if index < len(stages) - 1:
            arrow(c, sx + index * step + 34, sy, sx + (index + 1) * step - 34, sy, color=accent, width=1.5, head=6)

    rounded_box(c, M, 285, CW, 101, fill=NAVY, stroke=None, radius=14)
    c.setFont("KR-Bold", 11)
    c.setFillColor(LIME)
    c.drawString(M + 18, 356, "AnchorMind / Memento memory rail")
    memory_items = ["결정", "오류", "절차", "선호", "관계", "episode"]
    xx = M + 18
    for index, item in enumerate(memory_items):
        fill = NAVY_3 if index % 2 == 0 else HexColor("#1A3850")
        width = chip(c, item, xx, 315, fill=fill, color=white, size=7.2, h=21)
        xx += width + 8
    draw_text(
        c,
        "작업 전 recall -> 현재 코드와 대조 -> 확정 사실만 remember -> 장기 작업을 reflect",
        M + 18,
        302,
        CW - 36,
        size=7.7,
        color=HexColor("#BED0DF"),
        leading=11,
        max_lines=2,
    )

    left_w = 248
    rounded_box(c, M, 91, left_w, 164, fill=SOFT_CYAN, stroke=None, radius=12)
    c.setFont("KR-Bold", 10)
    c.setFillColor(INK)
    c.drawString(M + 14, 229, "자동화가 닫는 범위")
    auto_items = ["컴파일·예외", "상태 전이·경계값", "좌표·충돌·network authority", "build와 회귀 로그"]
    yy = 204
    for item in auto_items:
        c.setFillColor(CYAN)
        c.circle(M + 19, yy + 3, 3, fill=1, stroke=0)
        c.setFont("KR", 8)
        c.setFillColor(INK)
        c.drawString(M + 29, yy, item)
        yy -= 25

    rounded_box(c, M + left_w + 15, 91, CW - left_w - 15, 164, fill=SOFT_LIME, stroke=None, radius=12)
    rx = M + left_w + 29
    c.setFont("KR-Bold", 10)
    c.setFillColor(INK)
    c.drawString(rx, 229, "인간이 끝까지 책임지는 범위")
    human_items = ["조작감·가독성·재미", "animation 생동감·character성", "기능 우선순위·범위", "출시·제출·권리 판단"]
    yy = 204
    for item in human_items:
        c.setFillColor(LIME)
        c.circle(rx + 5, yy + 3, 3, fill=1, stroke=0)
        c.setFont("KR", 8)
        c.setFillColor(INK)
        c.drawString(rx + 15, yy, item)
        yy -= 25
    c.showPage()


def draw_memory(c: canvas.Canvas) -> None:
    section_header(
        c,
        4,
        "03 / ANCHORMIND MEMORY",
        "세션을 넘어 이어지는 프로젝트 기억",
        "장기기억은 답을 대신하는 데이터베이스가 아니라, 현재 저장소와 대조해야 하는 탐색 가능한 프로젝트 맥락이다.",
    )

    rounded_box(c, M, 335, 275, 363, fill=white, stroke=LINE, radius=14)
    c.setFont("KR-Bold", 10)
    c.setFillColor(INK)
    c.drawString(M + 16, 670, "기억 사용 폐쇄 루프")
    center_x, center_y, radius = M + 138, 510, 102
    lifecycle = [
        ("RECALL", "관련 기억 검색", 90, LIME),
        ("COMPARE", "현재 코드 대조", 18, CYAN),
        ("ACT", "구현·검증", -54, VIOLET),
        ("REMEMBER", "확정 사실 저장", -126, YELLOW),
        ("REFLECT", "흐름 정리", 162, RED),
    ]
    positions: list[tuple[float, float]] = []
    for _, _, deg, _ in lifecycle:
        angle = deg * pi / 180
        positions.append((center_x + radius * cos(angle), center_y + radius * sin(angle)))
    for index, (title, body, _, accent) in enumerate(lifecycle):
        x, y = positions[index]
        nx, ny = positions[(index + 1) % len(positions)]
        vx, vy = nx - x, ny - y
        length = max((vx * vx + vy * vy) ** 0.5, 1)
        arrow(c, x + vx / length * 30, y + vy / length * 30, nx - vx / length * 30, ny - vy / length * 30, color=accent, width=1.3, head=5)
        circle_label(c, x, y, 27, title, fill=white, text_color=INK, size=6.7, stroke=accent)
        draw_text(c, body, x - 36, y - 40, 72, size=6.2, color=MUTED, leading=8, max_lines=2, align="center")
    circle_label(c, center_x, center_y, 36, "DoodleUp\n현재 상태", fill=NAVY, text_color=white, size=8)

    rounded_box(c, 334, 335, 219, 363, fill=NAVY, stroke=None, radius=14)
    c.setFont("KR-Bold", 10)
    c.setFillColor(LIME)
    c.drawString(350, 670, "기억 단위")
    memory_types = [
        ("fact", "환경·확정 사실", LIME),
        ("decision", "선택과 근거", CYAN),
        ("error", "재현·원인·해결", RED),
        ("preference", "인간의 작업 선호", VIOLET),
        ("procedure", "재실행 절차", YELLOW),
        ("relation", "기능·결정 연결", CYAN),
        ("episode", "목표·사건·결과", LIME),
    ]
    yy = 630
    for label, body, accent in memory_types:
        c.setFillColor(accent)
        c.roundRect(350, yy - 2, 62, 19, 9, fill=1, stroke=0)
        c.setFont("KR-Bold", 6.9)
        c.setFillColor(NAVY)
        c.drawCentredString(381, yy + 4, label)
        c.setFont("KR", 8)
        c.setFillColor(white)
        c.drawString(424, yy + 3, body)
        yy -= 39

    rounded_box(c, M, 224, CW, 105, fill=SOFT_LIME, stroke=None, radius=12)
    c.setFont("KR-Bold", 10)
    c.setFillColor(LIME_DARK)
    c.drawString(M + 15, 301, "DoodleUp에서 실제로 재사용되는 문맥")
    examples = [
        ("Unity 6000.4.0f1", "환경"),
        ("owner-authoritative transform", "network 결정"),
        ("DU-02 reset 차단 요인", "오류"),
        ("실제 Editor play는 인간 checkpoint", "선호·절차"),
    ]
    exw = (CW - 30) / 4
    for index, (value, label) in enumerate(examples):
        x = M + 15 + index * exw
        c.setFont("KR-Bold", 7.8)
        c.setFillColor(INK)
        draw_text(c, value, x, 274, exw - 12, font="KR-Bold", size=7.8, color=INK, leading=10, max_lines=2)
        c.setFont("KR", 6.7)
        c.setFillColor(MUTED)
        c.drawString(x, 243, label)

    rounded_box(c, M, 83, CW, 115, fill=SOFT_RED, stroke=None, radius=12)
    c.setFont("KR-Bold", 10)
    c.setFillColor(HexColor("#A03937"))
    c.drawString(M + 15, 171, "안전 가드레일")
    guardrails = [
        "기억은 현재 코드와 실행 상태보다 우선하지 않는다.",
        "오래된 기억은 commit·검증 시각과 대조한다.",
        "비밀번호·token·인증 header·개인정보는 저장하지 않는다.",
        "추측·임시 log·쉽게 재생성되는 출력은 장기기억에서 제외한다.",
    ]
    yy = 145
    for index, item in enumerate(guardrails):
        col = index % 2
        row = index // 2
        x = M + 15 + col * 250
        y = yy - row * 38
        c.setFillColor(RED)
        c.circle(x + 4, y + 3, 3, fill=1, stroke=0)
        draw_text(c, item, x + 14, y + 7, 222, size=7.3, color=INK, leading=10, max_lines=2)
    c.showPage()


def draw_roles_workflow(c: canvas.Canvas) -> None:
    section_header(
        c,
        5,
        "04 / OPERATING MODEL",
        "역할의 분리와 카드 기반 실행",
        "같은 모델을 여러 번 부르는 것이 아니라, 각 역할에 다른 판단 기준과 완료 책임을 부여한다.",
    )

    roles = [
        ("PM", "project-manager", "범위 · 우선순위\n의존성 · 칸반", LIME, SOFT_LIME),
        ("PLAN", "game-planning", "동기 · 규칙\nUX · 구현 부담", CYAN, SOFT_CYAN),
        ("TECH", "game-tech-director", "runtime · network\n도구 · test", VIOLET, SOFT_VIOLET),
        ("ART", "game-art", "visual · Blender\nhandoff", YELLOW, SOFT_YELLOW),
        ("QA", "game-qa", "재현 · edge\n독립 증거", RED, SOFT_RED),
    ]
    gap = 8
    rw = (CW - 4 * gap) / 5
    for index, (short, title, body, accent, fill) in enumerate(roles):
        x = M + index * (rw + gap)
        rounded_box(c, x, 579, rw, 119, fill=fill, stroke=None, radius=12)
        c.setFont("KR-Bold", 7.2)
        c.setFillColor(accent)
        c.drawString(x + 10, 677, short)
        draw_text(c, title, x + 10, 652, rw - 20, font="KR-Bold", size=7.8, color=INK, leading=10, max_lines=2)
        draw_text(c, body, x + 10, 619, rw - 20, size=7, color=MUTED, leading=10, max_lines=3)

    c.setFont("KR-Bold", 10)
    c.setFillColor(INK)
    c.drawString(M, 548, "표준 개발 흐름")
    steps = [
        ("01", "의도 정의", "목표와 feel"),
        ("02", "카드 분해", "범위·수용 기준"),
        ("03", "기억 검색", "결정·오류 recall"),
        ("04", "격리 구현", "branch·worktree"),
        ("05", "도구 실행", "Unity·Blender"),
        ("06", "자동 검증", "compile·test"),
        ("07", "독립 QA", "raw 재계산"),
        ("08", "인간 승인", "play·수정 카드"),
    ]
    sw = (CW - 3 * 10) / 4
    sh = 93
    for index, (num, title, body) in enumerate(steps):
        row = index // 4
        col = index % 4
        x = M + col * (sw + 10)
        y = 431 - row * 115
        accent = [LIME, CYAN, VIOLET, YELLOW, CYAN, VIOLET, RED, LIME][index]
        rounded_box(c, x, y, sw, sh, fill=white, stroke=LINE, radius=11)
        c.setFillColor(accent)
        c.roundRect(x + 10, y + sh - 26, 28, 17, 8, fill=1, stroke=0)
        c.setFont("KR-Bold", 6.7)
        c.setFillColor(NAVY)
        c.drawCentredString(x + 24, y + sh - 21, num)
        c.setFont("KR-Bold", 9.4)
        c.setFillColor(INK)
        c.drawString(x + 10, y + 43, title)
        draw_text(c, body, x + 10, y + 25, sw - 20, size=7, color=MUTED, leading=9, max_lines=2)
        if col < 3:
            arrow(c, x + sw + 2, y + sh / 2, x + sw + 8, y + sh / 2, color=accent, head=4)

    rounded_box(c, M, 88, CW, 108, fill=NAVY, stroke=None, radius=13)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(LIME)
    c.drawString(M + 16, 171, "카드에서 commit까지 provenance")
    provenance = ["CARD ID", "ACCEPTANCE", "WORKTREE", "DIFF", "TEST LOG", "COMMIT"]
    px = M + 16
    py = 125
    item_w = 70
    for index, item in enumerate(provenance):
        rounded_box(c, px + index * 83, py, item_w, 27, fill=NAVY_3, stroke=None, radius=7)
        c.setFont("KR-Bold", 6.7)
        c.setFillColor(white)
        c.drawCentredString(px + index * 83 + item_w / 2, py + 9, item)
        if index < len(provenance) - 1:
            arrow(c, px + index * 83 + item_w + 2, py + 13.5, px + (index + 1) * 83 - 3, py + 13.5, color=LIME, head=4)
    draw_text(
        c,
        "완료는 자연어 선언이 아니라 카드·수용 기준·변경·검증·commit이 연결됐을 때 성립한다.",
        M + 16,
        111,
        CW - 32,
        size=7.2,
        color=HexColor("#BDD0DE"),
        leading=10,
        max_lines=2,
    )
    c.showPage()


def draw_evidence(c: canvas.Canvas) -> None:
    section_header(
        c,
        6,
        "05 / VERIFICATION EVIDENCE",
        "자연어 완료 선언을 기계 판정 가능한 증거로",
        "두 개발 단계의 공식 QA 보고서 수치를 그대로 분리해 제시한다. 전체 저장소의 현재 PASS 수로 확대 해석하지 않는다.",
    )

    panel_w = (CW - 14) / 2
    rounded_box(c, M, 412, panel_w, 292, fill=white, stroke=LINE, radius=14)
    chip(c, "DU-02 / FINAL QA", M + 16, 670, fill=SOFT_LIME, color=LIME_DARK, size=7, h=20)
    c.setFont("KR-Bold", 14)
    c.setFillColor(INK)
    c.drawString(M + 16, 639, "리셋 가능한 솔로 코스")
    draw_text(c, "구현 역할과 QA 역할을 분리하고 raw CSV·report·실행 파일 hash를 독립 재계산", M + 16, 618, panel_w - 32, size=7.6, color=MUTED, leading=11, max_lines=3)
    rows = [
        ("Compile / scene / build", "PASS"),
        ("EditMode", "12 / 12"),
        ("PlayMode", "2 / 2"),
        ("Standalone sampling", "3 / 3"),
        ("Reset paths", "6 / 6"),
        ("Runtime task-state", "4 / 4"),
    ]
    yy = 548
    for label, value in rows:
        pass_row(c, M + 18, yy, panel_w - 36, label, value)
        yy -= 23
    c.setFont("KR", 5.9)
    c.setFillColor(MUTED)
    c.drawString(M + 18, 418, "Evidence: docs/qa/reports/2026-07-31-doodleup-du02-acceptance-review.md")

    rx = M + panel_w + 14
    rounded_box(c, rx, 412, panel_w, 292, fill=NAVY, stroke=None, radius=14)
    chip(c, "DU-03B/C / LIVE EDITOR", rx + 16, 670, fill=NAVY_3, color=CYAN, size=7, h=20)
    c.setFont("KR-Bold", 14)
    c.setFillColor(white)
    c.drawString(rx + 16, 639, "Aim · Trajectory 입력 통합")
    draw_text(c, "같은 StrokeSession 규칙을 공유하고 입력 edge와 release frame event 순서를 자동 검증", rx + 16, 618, panel_w - 32, size=7.6, color=HexColor("#BFD0DF"), leading=11, max_lines=3)
    ratio_bar(c, rx + 18, 548, panel_w - 36, "EditMode", 1, "41 / 41 PASS", color=LIME, label_color=HexColor("#AFC1D3"), value_color=white)
    ratio_bar(c, rx + 18, 499, panel_w - 36, "PlayMode", 1, "9 / 9 PASS", color=CYAN, label_color=HexColor("#AFC1D3"), value_color=white)
    ratio_bar(c, rx + 18, 450, panel_w - 36, "Mapping tolerance", 1, "<= 1e-5u", color=VIOLET, label_color=HexColor("#AFC1D3"), value_color=white)
    c.setFont("KR", 6.7)
    c.setFillColor(HexColor("#AFC1D3"))
    c.drawString(rx + 18, 427, "Evidence: docs/qa/du-03bc-verification.md")

    rounded_box(c, M, 272, CW, 111, fill=SOFT_CYAN, stroke=None, radius=13)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(INK)
    c.drawString(M + 15, 354, "DU-02 evidence chain")
    chain = [
        ("STATE", "baseline"),
        ("PERTURB", "before"),
        ("RESET", "after"),
        ("RAW", "CSV"),
        ("HASH", "SHA256"),
        ("QA", "recompute"),
    ]
    xx = M + 20
    for index, (title, body) in enumerate(chain):
        rounded_box(c, xx + index * 82, 299, 65, 36, fill=white, stroke=CYAN, radius=8)
        c.setFont("KR-Bold", 6.9)
        c.setFillColor(INK)
        c.drawCentredString(xx + index * 82 + 32.5, 319, title)
        c.setFont("KR", 6.3)
        c.setFillColor(MUTED)
        c.drawCentredString(xx + index * 82 + 32.5, 307, body)
        if index < len(chain) - 1:
            arrow(c, xx + index * 82 + 67, 317, xx + (index + 1) * 82 - 3, 317, color=CYAN, head=4)
    draw_text(c, "beforeHash != baselineHash · afterHash == baselineHash · 실제 교란과 실제 복원을 동시에 증명", M + 15, 288, CW - 30, size=7, color=MUTED, leading=10, max_lines=2)

    values = [
        ("321", "commits"),
        ("968", "tracked files"),
        ("63", "docs"),
        ("67", "test sources"),
        ("618", "annotations"),
    ]
    mw = (CW - 32) / 5
    for index, (value, label) in enumerate(values):
        x = M + index * (mw + 8)
        rounded_box(c, x, 150, mw, 84, fill=white, stroke=LINE, radius=10)
        c.setFont("KR-Bold", 17)
        c.setFillColor([LIME_DARK, CYAN, VIOLET, YELLOW, RED][index])
        c.drawString(x + 10, 202, value)
        c.setFont("KR", 7)
        c.setFillColor(MUTED)
        c.drawString(x + 10, 180, label)
        c.setFont("KR", 6)
        c.setFillColor(MUTED)
        c.drawString(x + 10, 161, "HEAD 1d9a04c")

    rounded_box(c, M, 82, CW, 43, fill=SOFT_YELLOW, stroke=None, radius=9)
    draw_text(
        c,
        "검증되지 않은 지표: AI 시간 절감률 · 인간 개입 시간 · 재작업률 · API 비용. AgentDesk export와 Git 이력으로 산정한 값만 최종본에 사용한다.",
        M + 13,
        110,
        CW - 26,
        size=7.1,
        color=HexColor("#735A24"),
        leading=10,
        max_lines=2,
    )
    c.showPage()


def draw_cases(c: canvas.Canvas) -> None:
    section_header(
        c,
        7,
        "06 / DOODLEUP CASES",
        "기획 · 입력 · 레벨 · 네트워크 · 캐릭터, 하나의 체계로",
        "각 사례는 AI 기여, 재실행 가능한 증거, 인간 승인 지점을 함께 가진다.",
    )

    cards = [
        ("DU-02", "상태 리셋과 QA", "AI: scene bootstrap · runtime probe\n증거: 12/12 + 2/2 + raw hash\n인간: 범위와 최종 수용", LIME, SOFT_LIME),
        ("DU-03B/C", "두 입력 방식", "AI: adapter · latch · mapping\n증거: 41/41 + 9/9\n인간: mouse/keyboard feel", CYAN, SOFT_CYAN),
        ("PLAZA", "정량 레벨 설계", "AI: 좌표·가시성 규칙화\n증거: 51,200-point scan\n인간: 동선·공간 경험", VIOLET, SOFT_VIOLET),
        ("NETWORK", "협동과 방 코드", "AI: authority · lobby · UDP discovery\n증거: verifier · PlayMode\n인간: 접속 UX와 범위", YELLOW, SOFT_YELLOW),
        ("LIME ALIEN", "아트에서 runtime까지", "AI: rig 반복 · import · Animator wiring\n증거: pose · weight · validator\n인간: silhouette · deformation · feel", RED, SOFT_RED),
    ]
    positions = [
        (M, 523, 249, 175),
        (M + 262, 523, 249, 175),
        (M, 334, 249, 175),
        (M + 262, 334, 249, 175),
        (M, 145, 249, 175),
    ]
    for (code, title, body, accent, fill), (x, y, w, h) in zip(cards, positions):
        rounded_box(c, x, y, w, h, fill=fill, stroke=None, radius=13)
        chip(c, code, x + 14, y + h - 34, fill=white, color=accent, size=6.8, h=19)
        c.setFont("KR-Bold", 12)
        c.setFillColor(INK)
        c.drawString(x + 14, y + h - 63, title)
        draw_text(c, body, x + 14, y + h - 88, w - 28, size=7.5, color=MUTED, leading=12, max_lines=6)

    rounded_box(c, M + 262, 145, 249, 175, fill=NAVY, stroke=None, radius=13)
    image_cover(c, RESULT_NOMINAL, M + 274, 188, 225, 104)
    c.setFont("KR-Bold", 8.4)
    c.setFillColor(white)
    c.drawString(M + 276, 170, "LAST SHIFT 결과 화면 디자인 시안")
    c.setFont("KR", 6.5)
    c.setFillColor(HexColor("#AFC1D3"))
    c.drawString(M + 276, 157, "정량 결과를 플레이어가 이해할 수 있는 화면으로 번역")

    rounded_box(c, M, 82, CW, 40, fill=NAVY_2, stroke=None, radius=9)
    c.setFont("KR-Bold", 8)
    c.setFillColor(LIME)
    c.drawCentredString(W / 2, 98, "공통 규칙  |  목표 -> 역할형 구현 -> 자동 증거 -> 독립 QA -> 인간 플레이 승인")
    c.showPage()


def draw_plaza_map(c: canvas.Canvas, x: float, y: float, w: float, h: float) -> None:
    rooms = {
        "중앙 광장": (-6, 6, -6, 6, LIME),
        "조종석": (-14, -6, -3, 3, CYAN),
        "산소실": (6, 14, -3, 3, CYAN),
        "전력실": (-3, 3, -11, -6, VIOLET),
        "냉각실": (-3, 3, 6, 11, VIOLET),
        "에어록": (-11, -3, -12, -6, YELLOW),
        "숙소": (3, 9, 6, 10, YELLOW),
    }
    xmin, xmax, zmin, zmax = -14, 14, -12, 11
    pad = 12
    sx = (w - 2 * pad) / (xmax - xmin)
    sz = (h - 2 * pad) / (zmax - zmin)
    scale = min(sx, sz)
    ox = x + (w - (xmax - xmin) * scale) / 2
    oy = y + (h - (zmax - zmin) * scale) / 2

    def px(v: float) -> float:
        return ox + (v - xmin) * scale

    def py(v: float) -> float:
        return oy + (v - zmin) * scale

    c.saveState()
    c.setFillColor(HexColor("#F8FBFA"))
    c.roundRect(x, y, w, h, 10, fill=1, stroke=0)
    c.setStrokeColor(HexColor("#E2EAE7"))
    c.setLineWidth(0.25)
    for gx in range(xmin, xmax + 1, 2):
        c.line(px(gx), py(zmin), px(gx), py(zmax))
    for gz in range(zmin, zmax + 1, 2):
        c.line(px(xmin), py(gz), px(xmax), py(gz))

    for name, (x0, x1, z0, z1, accent) in rooms.items():
        fill = Color(accent.red, accent.green, accent.blue, alpha=0.16)
        c.setFillColor(fill)
        c.setStrokeColor(accent)
        c.setLineWidth(1.2)
        c.rect(px(x0), py(z0), (x1 - x0) * scale, (z1 - z0) * scale, fill=1, stroke=1)
        c.setFont("KR-Bold", 6.5 if name != "중앙 광장" else 7.5)
        c.setFillColor(INK)
        c.drawCentredString((px(x0) + px(x1)) / 2, (py(z0) + py(z1)) / 2 - 2, name)

    c.setFillColor(NAVY)
    c.setStrokeColor(NAVY)
    c.rect(px(-2), py(-2), 4 * scale, 4 * scale, fill=1, stroke=0)
    c.setFont("KR-Bold", 5.8)
    c.setFillColor(white)
    c.drawCentredString(px(0), py(0) - 2, "4x4 CORE")

    doors = [
        ("x", -6, 0),
        ("x", 6, 0),
        ("z", -6, 0),
        ("z", 6, 0),
        ("z", -6, -4.5),
        ("z", 6, 4.5),
    ]
    c.setStrokeColor(RED)
    c.setLineWidth(3.2)
    for axis, plane, center in doors:
        if axis == "x":
            c.line(px(plane), py(center - 0.8), px(plane), py(center + 0.8))
        else:
            c.line(px(center - 0.8), py(plane), px(center + 0.8), py(plane))

    c.setStrokeColor(CYAN)
    c.setLineWidth(0.7)
    c.setDash(3, 2)
    for gx, gz in [(0, -11), (0, 11), (14, 0)]:
        c.line(px(gx), py(gz), px(0), py(0))
    c.restoreState()


def draw_plaza(c: canvas.Canvas) -> None:
    section_header(
        c,
        8,
        "07 / QUANTITATIVE LEVEL DESIGN",
        "중앙 광장: 감각적 배치를 정량 관문으로",
        "기획 좌표를 검산 스크립트와 Unity EditMode 검사로 옮겨 가시성·겹침·이탈 시간을 반복 검증한다.",
    )

    rounded_box(c, M, 260, 356, 444, fill=white, stroke=LINE, radius=14)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(INK)
    c.drawString(M + 14, 678, "LAST SHIFT central plaza / top-down schematic")
    draw_plaza_map(c, M + 14, 292, 328, 363)
    chip(c, "ROOM", M + 18, 273, fill=SOFT_CYAN, color=CYAN, size=6.5, h=17)
    chip(c, "CORE", M + 86, 273, fill=NAVY, color=white, size=6.5, h=17)
    chip(c, "DOOR", M + 152, 273, fill=SOFT_RED, color=RED, size=6.5, h=17)
    chip(c, "VISIBILITY", M + 218, 273, fill=SOFT_CYAN, color=CYAN, size=6.5, h=17)

    metrics = [
        ("21", "room pairs", "overlap 0", LIME),
        ("6", "doors", "boundary OK", CYAN),
        ("51,200", "sample points", "triple visibility 0", VIOLET),
        ("4.26s", "worst egress", "limit 10s", YELLOW),
    ]
    yy = 611
    for value, label, note, accent in metrics:
        metric_card(c, 414, yy, 139, 91, value, label, accent=accent, note=note)
        yy -= 105

    rounded_box(c, M, 108, CW, 124, fill=NAVY, stroke=None, radius=13)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(LIME)
    c.drawString(M + 15, 204, "재실행 가능한 설계 증거")
    pipeline = [
        ("기획", "좌표표"),
        ("SCRIPT", "plaza_hub_check.py"),
        ("UNITY", "EditMode"),
        ("GATE", "PASS / FAIL"),
    ]
    xx = M + 24
    for index, (title, body) in enumerate(pipeline):
        rounded_box(c, xx + index * 122, 144, 96, 40, fill=NAVY_3, stroke=None, radius=8)
        c.setFont("KR-Bold", 7.2)
        c.setFillColor(white)
        c.drawCentredString(xx + index * 122 + 48, 168, title)
        c.setFont("KR", 6.2)
        c.setFillColor(HexColor("#BFD0DF"))
        c.drawCentredString(xx + index * 122 + 48, 154, body)
        if index < len(pipeline) - 1:
            arrow(c, xx + index * 122 + 100, 164, xx + (index + 1) * 122 - 5, 164, color=LIME, head=5)
    draw_text(
        c,
        "Evidence: docs/central-plaza-hub-layout-v1.md · docs/tools/plaza_hub_check.py",
        M + 15,
        126,
        CW - 30,
        size=6.8,
        color=HexColor("#AFC1D3"),
        leading=9,
        max_lines=2,
    )
    c.showPage()


def draw_lime_alien(c: canvas.Canvas) -> None:
    section_header(
        c,
        9,
        "08 / ART TO RUNTIME",
        "라임 외계인: Blender에서 Unity까지",
        "사람은 silhouette·deformation·character feel을 판단하고, AI 에이전트는 반복 제작·import·controller wiring·검증을 담당한다.",
    )

    card_w = (CW - 14) / 2
    rounded_box(c, M, 421, card_w, 284, fill=NAVY, stroke=None, radius=14)
    image_contain(c, LIME_NEUTRAL, M + 18, 458, card_w - 36, 205, background=NAVY)
    chip(c, "NEUTRAL", M + 16, 434, fill=NAVY_3, color=LIME, size=6.8, h=19)
    c.setFont("KR", 6.6)
    c.setFillColor(HexColor("#AFC1D3"))
    c.drawRightString(M + card_w - 16, 440, "runtime rest pose")

    rx = M + card_w + 14
    rounded_box(c, rx, 421, card_w, 284, fill=NAVY, stroke=None, radius=14)
    image_contain(c, LIME_STRESS, rx + 18, 458, card_w - 36, 205, background=NAVY)
    chip(c, "STRESS", rx + 16, 434, fill=NAVY_3, color=YELLOW, size=6.8, h=19)
    c.setFont("KR", 6.6)
    c.setFillColor(HexColor("#AFC1D3"))
    c.drawRightString(rx + card_w - 16, 440, "deformation evidence")

    rounded_box(c, M, 264, CW, 130, fill=white, stroke=LINE, radius=13)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(INK)
    c.drawString(M + 15, 368, "AI-assisted asset pipeline")
    stages = [
        ("BLENDER", "rig · weights", LIME),
        ("FBX", "exchange", CYAN),
        ("GENERIC", "avatar", VIOLET),
        ("8 CLIPS", "loop metadata", YELLOW),
        ("ANIMATOR", "base + carry", RED),
        ("NETWORK", "state drive", LIME),
    ]
    xx = M + 15
    for index, (title, body, accent) in enumerate(stages):
        rounded_box(c, xx + index * 84, 304, 69, 43, fill=Color(accent.red, accent.green, accent.blue, alpha=0.14), stroke=accent, radius=8)
        c.setFont("KR-Bold", 6.5)
        c.setFillColor(INK)
        c.drawCentredString(xx + index * 84 + 34.5, 329, title)
        c.setFont("KR", 5.9)
        c.setFillColor(MUTED)
        c.drawCentredString(xx + index * 84 + 34.5, 316, body)
        if index < len(stages) - 1:
            arrow(c, xx + index * 84 + 72, 326, xx + (index + 1) * 84 - 4, 326, color=accent, head=4)
    draw_text(
        c,
        "LimeAlienAnimatorSetup.Build: import metadata -> Generic avatar -> Base Layer -> upper-body Carry Override -> prefab -> preview -> validation",
        M + 15,
        286,
        CW - 30,
        size=6.8,
        color=MUTED,
        leading=9,
        max_lines=2,
    )

    facts = [
        ("5,772", "weighted vertices"),
        ("<= 4", "bone influences"),
        ("2-bone", "arm·leg IK"),
        ("8", "FBX clips"),
        ("2", "Animator layers"),
    ]
    gap = 8
    fw = (CW - 4 * gap) / 5
    for index, (value, label) in enumerate(facts):
        metric_card(c, M + index * (fw + gap), 151, fw, 88, value, label, accent=[LIME, CYAN, VIOLET, YELLOW, RED][index])

    rounded_box(c, M, 81, CW, 45, fill=SOFT_LIME, stroke=None, radius=9)
    draw_text(
        c,
        "Evidence: docs/art/last-shift-lime-alien-rig-v1.md · Assets/DoodleUp/Editor/LimeAlienAnimatorSetup.cs · commits 10acf30, 3e2dd29",
        M + 13,
        109,
        CW - 26,
        size=6.7,
        color=LIME_DARK,
        leading=9,
        max_lines=2,
    )
    c.showPage()


def draw_network(c: canvas.Canvas) -> None:
    section_header(
        c,
        10,
        "09 / NETWORK CO-OP",
        "소유자 입력, 서버 검증, 상태 복제",
        "AI가 빠르게 골격을 만들고 권한·보간·message ordering·화면 소유권의 경계를 test와 follow-up commit으로 닫았다.",
    )

    rounded_box(c, M, 334, CW, 365, fill=NAVY, stroke=None, radius=15)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(M + 17, 674, "AUTHORITY MAP")

    circle_label(c, M + 94, 563, 48, "OWNER\nCLIENT A", fill=NAVY_3, text_color=white, size=8, stroke=LIME)
    circle_label(c, W - M - 94, 563, 48, "REMOTE\nCLIENT B", fill=NAVY_3, text_color=white, size=8, stroke=CYAN)
    rounded_box(c, W / 2 - 78, 503, 156, 121, fill=HexColor("#1A3A4C"), stroke=VIOLET, radius=13)
    c.setFont("KR-Bold", 12)
    c.setFillColor(white)
    c.drawCentredString(W / 2, 588, "SERVER")
    c.setFont("KR", 7.5)
    c.setFillColor(HexColor("#C2D3E1"))
    c.drawCentredString(W / 2, 568, "grab · drop · placement")
    c.drawCentredString(W / 2, 552, "status validation")
    c.drawCentredString(W / 2, 536, "NetworkVariable write")
    arrow(c, M + 145, 568, W / 2 - 84, 568, color=LIME, width=2, head=7)
    arrow(c, W / 2 + 84, 558, W - M - 145, 558, color=CYAN, width=2, head=7)
    c.setFont("KR", 6.8)
    c.setFillColor(LIME)
    c.drawCentredString(M + 191, 583, "input / request")
    c.setFillColor(CYAN)
    c.drawCentredString(W - M - 191, 573, "replicated state")

    rounded_box(c, M + 52, 380, CW - 104, 79, fill=NAVY_3, stroke=None, radius=11)
    items = [
        ("PLAYER", "owner-authoritative transform"),
        ("ITEM", "owner-authoritative transform"),
        ("STATE", "server-write NetworkVariable"),
    ]
    iw = (CW - 140) / 3
    for index, (title, body) in enumerate(items):
        x = M + 67 + index * (iw + 12)
        c.setFont("KR-Bold", 7)
        c.setFillColor([LIME, CYAN, VIOLET][index])
        c.drawString(x, 435, title)
        draw_text(c, body, x, 415, iw, size=7.1, color=white, leading=10, max_lines=2)

    rounded_box(c, M, 195, CW, 124, fill=white, stroke=LINE, radius=13)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(INK)
    c.drawString(M + 15, 292, "6자리 room code / LAN discovery")
    discovery = [
        ("LOBBY", "host 대기"),
        ("CODE", "6자리 발급"),
        ("QUERY", "client broadcast"),
        ("REPLY", "host unicast"),
        ("JOIN", "game port"),
    ]
    xx = M + 18
    for index, (title, body) in enumerate(discovery):
        accent = [LIME, CYAN, VIOLET, YELLOW, RED][index]
        rounded_box(c, xx + index * 100, 231, 78, 42, fill=Color(accent.red, accent.green, accent.blue, alpha=0.14), stroke=accent, radius=8)
        c.setFont("KR-Bold", 6.9)
        c.setFillColor(INK)
        c.drawCentredString(xx + index * 100 + 39, 255, title)
        c.setFont("KR", 6.1)
        c.setFillColor(MUTED)
        c.drawCentredString(xx + index * 100 + 39, 242, body)
        if index < len(discovery) - 1:
            arrow(c, xx + index * 100 + 81, 252, xx + (index + 1) * 100 - 4, 252, color=accent, head=4)
    draw_text(
        c,
        "외부 service 의존 없이 같은 LAN에서 host를 찾는다. client는 임시 port에서 질의해 한 PC host/client 개발 환경의 port 충돌을 피한다.",
        M + 15,
        214,
        CW - 30,
        size=6.8,
        color=MUTED,
        leading=9,
        max_lines=2,
    )

    rounded_box(c, M, 85, CW, 83, fill=SOFT_CYAN, stroke=None, radius=11)
    c.setFont("KR-Bold", 8.5)
    c.setFillColor(INK)
    c.drawString(M + 14, 144, "구현 근거")
    draw_text(
        c,
        "LastShiftOwnerNetworkTransform · LastShiftNetworkSceneVerifier · LastShiftRoomLobbyPlayModeTests · LastShiftRoomDiscovery",
        M + 14,
        124,
        CW - 28,
        size=6.9,
        color=MUTED,
        leading=9,
        max_lines=2,
    )
    draw_text(
        c,
        "commits bfa63ff · 6f64fb8 · aa4be45 · 303db5c",
        M + 14,
        98,
        CW - 28,
        font="KR-Bold",
        size=6.9,
        color=CYAN,
        leading=9,
        max_lines=1,
    )
    c.showPage()


def draw_failures(c: canvas.Canvas) -> None:
    section_header(
        c,
        11,
        "10 / FAILURE-DRIVEN IMPROVEMENT",
        "느린 game-tech 작업도 시스템 설계 데이터로",
        "지연과 실패를 관측해 다음 scheduling과 context policy로 환류한다.",
    )

    rounded_box(c, M, 520, CW, 181, fill=NAVY, stroke=None, radius=14)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(M + 16, 675, "RECENT LATENCY PATTERN")
    circle_label(c, M + 86, 600, 38, "CARD A\nUnity", fill=NAVY_3, text_color=white, size=7, stroke=CYAN)
    circle_label(c, M + 190, 600, 38, "CARD B\nUnity", fill=NAVY_3, text_color=white, size=7, stroke=VIOLET)
    arrow(c, M + 126, 600, M + 280, 600, color=CYAN, width=2)
    arrow(c, M + 230, 600, M + 280, 600, color=VIOLET, width=2)
    rounded_box(c, M + 286, 558, 120, 84, fill=HexColor("#213D50"), stroke=YELLOW, radius=11)
    c.setFont("KR-Bold", 11)
    c.setFillColor(white)
    c.drawCentredString(M + 346, 612, "SHARED UNITY")
    c.setFont("KR", 7)
    c.setFillColor(HexColor("#CAD8E4"))
    c.drawCentredString(M + 346, 592, "fresh worktrees")
    c.drawCentredString(M + 346, 577, "cold import · compile")
    arrow(c, M + 410, 600, M + 455, 600, color=YELLOW, width=2)
    circle_label(c, M + 478, 600, 34, "긴\n대기", fill=Color(RED.red, RED.green, RED.blue, alpha=0.2), text_color=white, size=8, stroke=RED)
    draw_text(
        c,
        "같은 역할의 max concurrency가 2여도 Unity import·compile 같은 shared resource 비용은 두 배의 처리량이 아니라 cold-cache 경쟁이 될 수 있다.",
        M + 16,
        545,
        CW - 32,
        size=7.1,
        color=HexColor("#BFD0DF"),
        leading=10,
        max_lines=2,
    )

    rows = [
        ("game-tech 지연", "동일 Unity project 카드 2개 병행 · fresh worktree cold cache", "active/queue/log를 분리 확인", "project affinity scheduler"),
        ("38 failures 묶음", "수정과 전체 회귀를 한 카드에 결합", "대상 test와 full regression 분리", "failure cluster child card"),
        ("image context 팽창", "screenshot이 대화 문맥을 빠르게 차지", "log·file·수치 요약 우선", "artifact link와 image budget"),
        ("stale memory", "기억이 특정 commit 시점에 머묾", "recall 뒤 현재 코드와 대조", "commit·검증 시각 metadata"),
        ("test PASS ≠ 재미", "정량 검증이 feel을 직접 판단 못함", "Editor play human gate 유지", "정성 playtest 기록 연결"),
    ]
    draw_table(
        c,
        M,
        487,
        [90, 158, 132, 131],
        ["관찰", "원인", "현재 대응", "다음 개선"],
        rows,
        row_heights=[61, 58, 58, 58, 58],
        font_size=7.1,
    )

    rounded_box(c, M, 82, CW, 54, fill=SOFT_LIME, stroke=None, radius=10)
    c.setFont("KR-Bold", 8.3)
    c.setFillColor(LIME_DARK)
    c.drawString(M + 13, 116, "운영 원칙")
    draw_text(
        c,
        "동시성 숫자보다 resource affinity를 먼저 본다. 큰 카드의 실패는 cluster별로 쪼갠다. screenshot보다 machine-readable evidence를 우선한다.",
        M + 13,
        98,
        CW - 26,
        size=7,
        color=INK,
        leading=10,
        max_lines=2,
    )
    c.showPage()


def draw_tools(c: canvas.Canvas) -> None:
    section_header(
        c,
        12,
        "11 / TOOL DISCLOSURE",
        "AI 도구와 지원 도구를 구분해 공개",
        "NAN 2026 약관은 출품작 개발에 사용한 주요 AI 도구의 명칭과 활용 방식 고지를 요구한다.",
    )

    rows = [
        ("AgentDesk", "칸반·역할 배정·상태 관리", "카드·세션·이력", "확인됨"),
        ("AnchorMind / Memento", "결정·오류·절차 장기기억", "memory fragments", "확인됨"),
        ("Claude 계열 agent", "역할별 기획·구현·검토", "code·docs·analysis", "모델명 확인"),
        ("OpenAI Codex", "repo 분석·구현·문서·검증", "diff·tests·PDF", "확인됨"),
        ("Unity 6", "compile·build·EditMode·PlayMode", "build·logs·results", "지원 도구"),
        ("Blender", "model·rig·weights·animation", ".blend · .fbx", "지원 도구"),
        ("Git worktree", "변경 격리·provenance", "branch·commit", "지원 도구"),
        ("Tripo / SF3D", "3D 후보 산출물", "image·mesh 후보", "제출 전 확인"),
    ]
    draw_table(
        c,
        M,
        700,
        [121, 163, 130, 97],
        ["도구 / 계층", "활용 방식", "주요 출력", "상태"],
        rows,
        row_heights=[48, 52, 52, 49, 49, 49, 49, 52],
        font_size=7.2,
    )

    rounded_box(c, M, 130, CW, 112, fill=NAVY, stroke=None, radius=13)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(M + 15, 216, "ASSET LEDGER / 제출 전 필수")
    ledger = [
        ("SOURCE", "원본 입력"),
        ("TOOL", "제품·모델"),
        ("DATE", "생성·수정일"),
        ("LICENSE", "상업 이용"),
        ("HUMAN", "수정·승인"),
        ("PATH", "project 경로"),
    ]
    xx = M + 15
    for index, (title, body) in enumerate(ledger):
        rounded_box(c, xx + index * 82, 158, 68, 37, fill=NAVY_3, stroke=None, radius=7)
        c.setFont("KR-Bold", 6.3)
        c.setFillColor(white)
        c.drawCentredString(xx + index * 82 + 34, 180, title)
        c.setFont("KR", 5.8)
        c.setFillColor(HexColor("#BFD0DF"))
        c.drawCentredString(xx + index * 82 + 34, 168, body)
    draw_text(
        c,
        "도구명·모델·입력 출처·license가 확인되지 않은 생성 asset은 최종 제출물에서 제외한다.",
        M + 15,
        145,
        CW - 30,
        size=6.8,
        color=HexColor("#BFD0DF"),
        leading=9,
        max_lines=2,
    )

    rounded_box(c, M, 80, CW, 34, fill=SOFT_YELLOW, stroke=None, radius=8)
    c.setFont("KR-Bold", 7.2)
    c.setFillColor(HexColor("#785D25"))
    c.drawCentredString(W / 2, 92, "정확한 Claude 모델명 · Tripo/SF3D 생성 경로 · 비용·사용량은 원본 실행 기록으로 최종 확정")
    c.showPage()


def draw_governance(c: canvas.Canvas) -> None:
    section_header(
        c,
        13,
        "12 / GOVERNANCE & CONCLUSION",
        "기존 DoodleUp IP와 본선 신규 결과물의 경계",
        "도구 공개, 권리 확인, 인간 승인과 evidence provenance를 제출 품질의 일부로 다룬다.",
    )

    rounded_box(c, M, 530, CW, 173, fill=white, stroke=LINE, radius=14)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(INK)
    c.drawString(M + 15, 677, "SUBMISSION BOUNDARY")
    rounded_box(c, M + 18, 563, 202, 83, fill=SOFT_CYAN, stroke=None, radius=11)
    c.setFont("KR-Bold", 10)
    c.setFillColor(INK)
    c.drawString(M + 31, 622, "PRE-EXISTING")
    draw_text(c, "DoodleUp repository · LAST SHIFT code · 기존 art·docs·tests", M + 31, 601, 176, size=7.1, color=MUTED, leading=10, max_lines=3)
    rounded_box(c, W - M - 220, 563, 202, 83, fill=SOFT_LIME, stroke=None, radius=11)
    c.setFont("KR-Bold", 10)
    c.setFillColor(INK)
    c.drawString(W - M - 207, 622, "HACKATHON NEW")
    draw_text(c, "본선 기간 신규 code · asset · prompt · build · 발표 자료", W - M - 207, 601, 176, size=7.1, color=MUTED, leading=10, max_lines=3)
    arrow(c, M + 226, 604, W - M - 226, 604, color=VIOLET, width=2, head=7)
    c.setFont("KR-Bold", 5.8)
    c.setFillColor(VIOLET)
    c.drawCentredString(W / 2, 626, "PROVENANCE")

    rounded_box(c, M, 327, CW, 176, fill=NAVY, stroke=None, radius=14)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(LIME)
    c.drawString(M + 15, 477, "NAN 2026 TERMS / 최종 제출 전 재확인")
    terms = [
        ("AI DISCLOSURE", "주요 AI 도구 명칭과 활용 방식 고지"),
        ("LICENSE", "생성형 AI·open source·외부 API 조건 준수"),
        ("IP", "출품작 저작권·지식재산권은 원칙적으로 참가자 귀속"),
        ("WINNER USE", "수상작 홍보 이용: 접수 시작일부터 1년"),
        ("NEGOTIATION", "수상작 사업화 우선협상권: 종료일부터 4개월"),
    ]
    yy = 444
    for index, (title, body) in enumerate(terms):
        col = index % 2
        row = index // 2
        x = M + 16 + col * 248
        y = yy - row * 43
        c.setFont("KR-Bold", 6.8)
        c.setFillColor([LIME, CYAN, VIOLET, YELLOW, RED][index])
        c.drawString(x, y, title)
        draw_text(c, body, x, y - 14, 226, size=6.7, color=white, leading=9, max_lines=2)
    c.setFont("KR", 6.2)
    c.setFillColor(HexColor("#AFC1D3"))
    c.drawString(M + 16, 334, "Source: https://nan2026.nhn.com/terms  |  이 문서는 법률 자문을 대신하지 않는다.")
    c.linkURL("https://nan2026.nhn.com/terms", (M + 16, 330, M + 300, 341), relative=0)

    rounded_box(c, M, 192, CW, 108, fill=SOFT_LIME, stroke=None, radius=13)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(LIME_DARK)
    c.drawString(M + 15, 273, "FINAL CLAIM")
    draw_text(
        c,
        "DoodleUp은 AI에게 게임을 맡긴 프로젝트가 아니다. 인간 디렉터의 의도를 기억하고, 전문 역할로 구현하며, Unity 증거로 검증하는 AI 개발 조직을 만든 프로젝트다.",
        M + 15,
        247,
        CW - 30,
        font="KR-Bold",
        size=11.3,
        color=INK,
        leading=17,
        max_lines=4,
        align="center",
    )

    rounded_box(c, M, 82, CW, 83, fill=white, stroke=LINE, radius=11)
    c.setFont("KR-Bold", 8)
    c.setFillColor(INK)
    c.drawString(M + 13, 144, "Evidence index")
    evidence = [
        "E1  docs/qa/reports/2026-07-31-doodleup-du02-acceptance-review.md",
        "E2  docs/qa/du-03bc-verification.md",
        "E3  docs/central-plaza-hub-layout-v1.md · docs/tools/plaza_hub_check.py",
        "E4  docs/art/last-shift-lime-alien-rig-v1.md · LimeAlienAnimatorSetup.cs",
        "E5  LastShiftNetworkSceneVerifier.cs · LastShiftRoomDiscovery.cs",
        "WEB https://nan2026.nhn.com/ · https://nan2026.nhn.com/terms",
    ]
    for index, item in enumerate(evidence):
        col = index % 2
        row = index // 2
        draw_text(c, item, M + 13 + col * 252, 125 - row * 18, 238, size=5.9, color=MUTED, leading=8, max_lines=2)
    c.linkURL("https://nan2026.nhn.com/", (M + 265, 82, M + 500, 107), relative=0)
    c.showPage()


def build_pdf() -> Path:
    register_fonts()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(OUTPUT), pagesize=A4, pageCompression=1)
    c.setTitle("DoodleUp AI 개발 시스템 기술서 - NAN 2026")
    c.setAuthor("DoodleUp")
    c.setSubject("NAN 2026 Game X AI Hackathon 참가신청용 AI 개발 시스템 기술서")
    c.setKeywords("DoodleUp, NAN 2026, Game AI, AgentDesk, AnchorMind, Memento, Unity")
    c.setCreator("DoodleUp reproducible ReportLab generator")

    draw_cover(c)
    draw_executive_summary(c)
    draw_architecture(c)
    draw_memory(c)
    draw_roles_workflow(c)
    draw_evidence(c)
    draw_cases(c)
    draw_plaza(c)
    draw_lime_alien(c)
    draw_network(c)
    draw_failures(c)
    draw_tools(c)
    draw_governance(c)

    c.save()
    return OUTPUT


if __name__ == "__main__":
    result = build_pdf()
    print(result)
