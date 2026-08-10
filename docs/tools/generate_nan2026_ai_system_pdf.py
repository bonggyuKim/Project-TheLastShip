"""Generate the workflow-focused DoodleUp AI development system brief."""

from __future__ import annotations

from math import atan2, cos, pi, sin
from pathlib import Path
from typing import Sequence

from reportlab.lib.colors import Color, HexColor, white
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "output" / "pdf" / "doodleup-nan2026-ai-development-system.pdf"
FONT_REGULAR = Path(r"C:\Windows\Fonts\malgun.ttf")
FONT_BOLD = Path(r"C:\Windows\Fonts\malgunbd.ttf")

W, H = A4
M = 42
CW = W - 2 * M
PAGE_COUNT = 10

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
    return (
        value.replace("\u2011", "-")
        .replace("\u2012", "-")
        .replace("\u2013", "-")
        .replace("\u2014", "-")
    )


def text_width(text: str, font: str, size: float) -> float:
    return pdfmetrics.stringWidth(safe_text(text), font, size)


def split_token(token: str, max_width: float, font: str, size: float) -> list[str]:
    chunks: list[str] = []
    current = ""
    for char in token:
        candidate = current + char
        if current and text_width(candidate, font, size) > max_width:
            chunks.append(current)
            current = char
        else:
            current = candidate
    if current:
        chunks.append(current)
    return chunks


def wrap_text(text: str, max_width: float, font: str, size: float) -> list[str]:
    lines: list[str] = []
    for paragraph in safe_text(text).splitlines() or [""]:
        if not paragraph:
            lines.append("")
            continue
        current = ""
        for word in paragraph.split(" "):
            if text_width(word, font, size) > max_width:
                if current:
                    lines.append(current)
                    current = ""
                parts = split_token(word, max_width, font, size)
                lines.extend(parts[:-1])
                current = parts[-1]
                continue
            candidate = word if not current else f"{current} {word}"
            if current and text_width(candidate, font, size) > max_width:
                lines.append(current)
                current = word
            else:
                current = candidate
        if current:
            lines.append(current)
    return lines


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
    c.setLineWidth(line_width)
    if stroke is None:
        c.setStrokeColor(fill)
        stroke_flag = 0
    else:
        c.setStrokeColor(stroke)
        stroke_flag = 1
    c.roundRect(x, y, w, h, radius, fill=1, stroke=stroke_flag)
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
    width = text_width(label, "KR-Bold", size) + 2 * pad_x
    rounded_box(c, x, y, width, h, fill=fill, stroke=None, radius=h / 2)
    c.setFont("KR-Bold", size)
    c.setFillColor(color)
    c.drawCentredString(x + width / 2, y + (h - size) / 2 + 1.5, label)
    return width


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


def circle_label(
    c: canvas.Canvas,
    x: float,
    y: float,
    r: float,
    text: str,
    *,
    fill: Color,
    text_color: Color = INK,
    stroke: Color | None = None,
    size: float = 8,
) -> None:
    c.setFillColor(fill)
    c.setStrokeColor(stroke or fill)
    c.setLineWidth(1.2)
    c.circle(x, y, r, fill=1, stroke=1 if stroke else 0)
    lines = wrap_text(text, r * 1.55, "KR-Bold", size)
    yy = y + len(lines) * size * 0.55
    c.setFont("KR-Bold", size)
    c.setFillColor(text_color)
    for line in lines:
        c.drawCentredString(x, yy, line)
        yy -= size * 1.25


def fit_single_line(text: str, font: str, preferred: float, minimum: float, width: float) -> float:
    size = preferred
    while size > minimum and text_width(text, font, size) > width:
        size -= 0.5
    return size


def section_header(
    c: canvas.Canvas,
    page: int,
    kicker: str,
    title: str,
    subtitle: str,
) -> None:
    c.setFillColor(PAPER)
    c.rect(0, 0, W, H, fill=1, stroke=0)
    chip(c, kicker.upper(), M, H - 66, fill=NAVY, color=LIME, size=7.4, h=19)
    title = safe_text(title)
    title_size = fit_single_line(title, "KR-Bold", 25, 18, CW)
    c.setFont("KR-Bold", title_size)
    c.setFillColor(INK)
    c.drawString(M, H - 110, title)
    subtitle = safe_text(subtitle)
    subtitle_size = fit_single_line(subtitle, "KR", 9.2, 7.3, CW)
    c.setFont("KR", subtitle_size)
    c.setFillColor(MUTED)
    c.drawString(M, H - 127, subtitle)
    c.setStrokeColor(LINE)
    c.line(M, 42, W - M, 42)
    c.setFont("KR", 7.2)
    c.setFillColor(MUTED)
    c.drawString(M, 25, "DoodleUp AI Development System / workflow & capabilities")
    c.setFont("KR-Bold", 8)
    c.drawRightString(W - M, 25, f"{page:02d}")
    key = f"page-{page}"
    c.bookmarkPage(key)
    c.addOutlineEntry(title, key, level=0, closed=False)


def benefit_strip(
    c: canvas.Canvas,
    items: Sequence[tuple[str, str, Color]],
    *,
    y: float = 82,
    h: float = 88,
) -> None:
    gap = 9
    width = (CW - gap * (len(items) - 1)) / len(items)
    for index, (title, body, accent) in enumerate(items):
        x = M + index * (width + gap)
        rounded_box(c, x, y, width, h, fill=white, stroke=LINE, radius=10)
        c.setFillColor(accent)
        c.roundRect(x, y + h - 5, width, 5, 3, fill=1, stroke=0)
        c.setFont("KR-Bold", 9)
        c.setFillColor(INK)
        c.drawString(x + 11, y + h - 29, title)
        draw_text(c, body, x + 11, y + h - 48, width - 22, size=6.9, color=MUTED, leading=9.5, max_lines=3)


def draw_table(
    c: canvas.Canvas,
    x: float,
    y_top: float,
    widths: Sequence[float],
    headers: Sequence[str],
    rows: Sequence[Sequence[str]],
    *,
    row_heights: Sequence[float] | float,
    font_size: float = 7.3,
    header_height: float = 30,
) -> float:
    total = sum(widths)
    c.setFillColor(NAVY)
    c.roundRect(x, y_top - header_height, total, header_height, 8, fill=1, stroke=0)
    xx = x
    for width, header in zip(widths, headers):
        draw_text(c, header, xx + 7, y_top - 12, width - 14, font="KR-Bold", size=7.2, color=white, leading=9, max_lines=2)
        xx += width
    heights = [row_heights] * len(rows) if isinstance(row_heights, (int, float)) else list(row_heights)
    y = y_top - header_height
    for index, (row, rh) in enumerate(zip(rows, heights)):
        c.setFillColor(white if index % 2 == 0 else PAPER_2)
        c.rect(x, y - rh, total, rh, fill=1, stroke=0)
        c.setStrokeColor(LINE)
        c.setLineWidth(0.45)
        c.line(x, y - rh, x + total, y - rh)
        xx = x
        for col, (width, cell) in enumerate(zip(widths, row)):
            if col:
                c.line(xx, y, xx, y - rh)
            draw_text(
                c,
                cell,
                xx + 7,
                y - 14,
                width - 14,
                font="KR-Bold" if col == 0 else "KR",
                size=font_size,
                color=INK if col == 0 else MUTED,
                leading=font_size * 1.42,
                max_lines=max(2, int((rh - 10) / (font_size * 1.42))),
            )
            xx += width
        y -= rh
    c.setStrokeColor(LINE)
    c.roundRect(x, y, total, y_top - y, 8, fill=0, stroke=1)
    return y


def draw_cover(c: canvas.Canvas) -> None:
    c.setFillColor(NAVY)
    c.rect(0, 0, W, H, fill=1, stroke=0)
    c.setFillColor(LIME)
    c.rect(0, H - 9, W, 9, fill=1, stroke=0)
    c.setFillColor(NAVY_2)
    for gx in range(28, int(W), 28):
        for gy in range(28, int(H), 28):
            if (gx + gy) % 84 == 0:
                c.circle(gx, gy, 0.7, fill=1, stroke=0)

    chip(c, "NAN 2026 / GAME X AI", M, H - 72, fill=NAVY_3, color=LIME, size=8, h=22)
    c.setFont("KR-Bold", 34)
    c.setFillColor(white)
    c.drawString(M, H - 142, "DoodleUp")
    c.setFont("KR-Bold", 30)
    c.setFillColor(LIME)
    c.drawString(M, H - 184, "AI 개발 시스템 기술서")
    draw_text(
        c,
        "작업 흐름과 범용 기능을 중심으로 설명하는 AI-native 개발 운영 체계",
        M,
        H - 220,
        440,
        size=12,
        color=HexColor("#C6D5E3"),
        leading=18,
        max_lines=2,
    )

    rounded_box(c, M, 145, CW, 365, fill=NAVY_2, stroke=NAVY_3, radius=18)
    c.setFont("KR-Bold", 9)
    c.setFillColor(CYAN)
    c.drawString(M + 19, 482, "SYSTEM, NOT A SINGLE MODEL")

    cx, cy = W / 2, 330
    circle_label(c, cx, cy, 61, "AgentDesk\nORCHESTRATOR", fill=HexColor("#1B3A4B"), text_color=white, stroke=LIME, size=9)
    nodes = [
        (cx - 170, cy + 88, "작업 흐름", CYAN),
        (cx, cy + 126, "장기기억", LIME),
        (cx + 170, cy + 88, "역할 협업", VIOLET),
        (cx + 190, cy - 60, "격리·추적", YELLOW),
        (cx + 78, cy - 95, "도구 실행", CYAN),
        (cx - 78, cy - 95, "검증", RED),
        (cx - 190, cy - 60, "관측·복구", VIOLET),
        (cx - 170, cy + 10, "인간 통제", LIME),
    ]
    for x, y, label, accent in nodes:
        arrow(c, cx, cy, x, y, color=Color(accent.red, accent.green, accent.blue, alpha=0.75), width=1.2, head=5)
        circle_label(c, x, y, 33, label, fill=NAVY_3, text_color=white, stroke=accent, size=7)

    rounded_box(c, M + 38, 151, CW - 76, 47, fill=NAVY_3, stroke=None, radius=10)
    c.setFont("KR-Bold", 10)
    c.setFillColor(LIME)
    c.drawCentredString(W / 2, 178, "의도 -> 실행 -> 증거 -> 승인 -> 학습")
    c.setFont("KR", 7.5)
    c.setFillColor(HexColor("#BDD0DE"))
    c.drawCentredString(W / 2, 161, "AI는 결정을 대신하지 않고, 팀이 더 일관되고 검증 가능하게 움직이도록 만든다.")

    c.setFont("KR", 8)
    c.setFillColor(HexColor("#9DB1C5"))
    c.drawString(M, 82, "제출 검토본 v2.0  |  2026.08.10")
    c.setFont("KR-Bold", 8)
    c.setFillColor(LIME)
    c.drawRightString(W - M, 82, f"01 / {PAGE_COUNT:02d}")
    c.bookmarkPage("page-1")
    c.addOutlineEntry("표지", "page-1", level=0, closed=False)
    c.showPage()


def draw_theme_map(c: canvas.Canvas) -> None:
    section_header(
        c,
        2,
        "00 / THEME MAP",
        "한 테마는 하나의 질문만 책임진다",
        "기능을 겹쳐 설명하지 않고, 시스템의 각 책임을 독립된 관점으로 나눈다.",
    )

    rounded_box(c, M, 635, CW, 67, fill=NAVY, stroke=None, radius=12)
    c.setFont("KR-Bold", 11)
    c.setFillColor(LIME)
    c.drawString(M + 16, 674, "SYSTEM THESIS")
    c.setFont("KR-Bold", 10)
    c.setFillColor(white)
    c.drawString(M + 16, 650, "사람이 방향을 정하고, AI 시스템이 실행의 연속성·속도·증거를 만든다.")

    themes = [
        ("01", "오케스트레이션", "무엇이 언제 움직이는가", "카드·의존성·상태", LIME, SOFT_LIME),
        ("02", "장기기억", "무엇이 세션 뒤에도 남는가", "결정·오류·절차", CYAN, SOFT_CYAN),
        ("03", "역할 협업", "누가 어떤 기준으로 판단하는가", "소유권·handoff·review", VIOLET, SOFT_VIOLET),
        ("04", "격리·추적", "어디서 안전하게 변경하는가", "worktree·diff·commit", YELLOW, SOFT_YELLOW),
        ("05", "도구 실행", "의도가 어떻게 산출물이 되는가", "inspect·edit·execute", CYAN, SOFT_CYAN),
        ("06", "검증", "완료를 어떻게 증명하는가", "gate·evidence·QA", RED, SOFT_RED),
        ("07", "관측·복구", "장시간 작업을 어떻게 통제하는가", "state·log·resume·budget", VIOLET, SOFT_VIOLET),
        ("08", "인간 통제", "최종 권한과 책임은 누구에게 있는가", "approval·safety·rights", LIME, SOFT_LIME),
    ]
    card_w = (CW - 3 * 10) / 4
    card_h = 180
    for index, (num, title, question, scope, accent, fill) in enumerate(themes):
        row, col = divmod(index, 4)
        x = M + col * (card_w + 10)
        y = 425 - row * 196
        rounded_box(c, x, y, card_w, card_h, fill=fill, stroke=None, radius=12)
        c.setFont("KR-Bold", 7.2)
        c.setFillColor(accent)
        c.drawString(x + 11, y + card_h - 25, num)
        c.setFont("KR-Bold", 10)
        c.setFillColor(INK)
        draw_text(c, title, x + 11, y + card_h - 49, card_w - 22, font="KR-Bold", size=10, color=INK, leading=13, max_lines=2)
        draw_text(c, question, x + 11, y + card_h - 84, card_w - 22, size=7.4, color=MUTED, leading=11, max_lines=3)
        c.setStrokeColor(Color(accent.red, accent.green, accent.blue, alpha=0.5))
        c.line(x + 11, y + 45, x + card_w - 11, y + 45)
        c.setFont("KR", 6.4)
        c.setFillColor(MUTED)
        c.drawString(x + 11, y + 27, scope)

    rounded_box(c, M, 82, CW, 55, fill=SOFT_YELLOW, stroke=None, radius=10)
    c.setFont("KR-Bold", 8.2)
    c.setFillColor(HexColor("#785D25"))
    c.drawCentredString(W / 2, 105, "설명 규칙  |  각 장은 작동 방식 -> 핵심 기능 -> 고유한 장점 순서로만 구성")
    c.showPage()


def draw_orchestration(c: canvas.Canvas) -> None:
    section_header(
        c,
        3,
        "01 / ORCHESTRATION",
        "무엇이 언제 움직이는가",
        "목표를 카드로 바꾸고, 의존성과 상태를 관리하며, 적절한 역할과 실행 순서를 연결한다.",
    )

    rounded_box(c, M, 474, CW, 230, fill=white, stroke=LINE, radius=14)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(INK)
    c.drawString(M + 16, 677, "END-TO-END WORKFLOW")
    steps = [
        ("01", "목표 접수", "의도·제약"),
        ("02", "범위화", "완료 조건"),
        ("03", "의존성", "선후 관계"),
        ("04", "역할 배정", "owner·reviewer"),
        ("05", "실행", "격리 작업"),
        ("06", "증거 수집", "log·artifact"),
        ("07", "검토", "gate·QA"),
        ("08", "승인·학습", "human·reflect"),
    ]
    sw = (CW - 3 * 10 - 32) / 4
    for index, (num, title, body) in enumerate(steps):
        row, col = divmod(index, 4)
        x = M + 16 + col * (sw + 10)
        y = 581 - row * 88
        accent = [LIME, CYAN, VIOLET, YELLOW, CYAN, VIOLET, RED, LIME][index]
        rounded_box(c, x, y, sw, 68, fill=Color(accent.red, accent.green, accent.blue, alpha=0.12), stroke=accent, radius=9)
        c.setFont("KR-Bold", 6.6)
        c.setFillColor(accent)
        c.drawString(x + 9, y + 48, num)
        c.setFont("KR-Bold", 8.3)
        c.setFillColor(INK)
        c.drawString(x + 9, y + 30, title)
        c.setFont("KR", 6.4)
        c.setFillColor(MUTED)
        c.drawString(x + 9, y + 13, body)
        if col < 3:
            arrow(c, x + sw + 2, y + 34, x + sw + 8, y + 34, color=accent, head=4)

    rounded_box(c, M, 350, CW, 94, fill=NAVY, stroke=None, radius=12)
    c.setFont("KR-Bold", 8.5)
    c.setFillColor(LIME)
    c.drawString(M + 15, 418, "CARD STATE")
    states = ["BACKLOG", "READY", "ACTIVE", "REVIEW", "BLOCKED", "DONE"]
    xx = M + 16
    for index, state in enumerate(states):
        width = 68 if state != "BLOCKED" else 73
        rounded_box(c, xx, 376, width, 27, fill=NAVY_3, stroke=None, radius=7)
        c.setFont("KR-Bold", 6.5)
        c.setFillColor(RED if state == "BLOCKED" else white)
        c.drawCentredString(xx + width / 2, 385, state)
        if index < len(states) - 1:
            arrow(c, xx + width + 3, 389, xx + width + 12, 389, color=LIME if state != "BLOCKED" else RED, head=4)
        xx += width + 15

    rows = [
        ("Kanban 상태", "실제 실행 상태와 보드를 맞춘다", "진행과 대기를 혼동하지 않는다"),
        ("의존성 graph", "선행 작업이 닫힌 뒤 후속 작업을 연다", "재작업과 충돌을 줄인다"),
        ("수용 기준", "시작 전에 완료 조건과 금지 범위를 적는다", "scope drift를 억제한다"),
        ("자원 인식 배정", "역할뿐 아니라 shared resource 비용을 본다", "안전한 병렬성과 처리량을 얻는다"),
    ]
    draw_table(c, M, 320, [115, 207, 189], ["기능", "작동 방식", "고유한 장점"], rows, row_heights=36, font_size=6.7)

    benefit_strip(
        c,
        [
            ("예측 가능성", "다음 상태와 종료 조건이 보인다.", LIME),
            ("조정 비용 감소", "누가 무엇을 기다리는지 자동으로 드러난다.", CYAN),
            ("안전한 병렬화", "의존성과 자원을 함께 보고 실행한다.", VIOLET),
        ],
        y=58,
        h=76,
    )
    c.showPage()


def draw_memory(c: canvas.Canvas) -> None:
    section_header(
        c,
        4,
        "02 / LONG-TERM MEMORY",
        "무엇이 세션 뒤에도 남는가",
        "AnchorMind/Memento가 결정·오류·절차·선호를 검색 가능한 프로젝트 기억으로 유지한다.",
    )

    rounded_box(c, M, 365, 278, 339, fill=white, stroke=LINE, radius=14)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(INK)
    c.drawString(M + 16, 678, "MEMORY LIFECYCLE")
    cx, cy, radius = M + 139, 524, 91
    stages = [
        ("RECALL", "관련 기억 검색", 90, LIME),
        ("COMPARE", "현재 상태 대조", 18, CYAN),
        ("APPLY", "작업에 사용", -54, VIOLET),
        ("REMEMBER", "확정 사실 저장", -126, YELLOW),
        ("REFLECT", "흐름 정리", 162, RED),
    ]
    positions: list[tuple[float, float]] = []
    for _, _, deg, _ in stages:
        angle = deg * pi / 180
        positions.append((cx + radius * cos(angle), cy + radius * sin(angle)))
    for index, (title, body, _, accent) in enumerate(stages):
        x, y = positions[index]
        nx, ny = positions[(index + 1) % len(positions)]
        vx, vy = nx - x, ny - y
        length = max((vx * vx + vy * vy) ** 0.5, 1)
        arrow(c, x + vx / length * 29, y + vy / length * 29, nx - vx / length * 29, ny - vy / length * 29, color=accent, head=5)
        circle_label(c, x, y, 27, title, fill=white, stroke=accent, size=6.7)
        draw_text(c, body, x - 36, y - 39, 72, size=6.2, color=MUTED, leading=8, max_lines=2, align="center")
    circle_label(c, cx, cy, 35, "CURRENT\nPROJECT", fill=NAVY, text_color=white, size=7.5)

    rounded_box(c, 334, 365, 219, 339, fill=NAVY, stroke=None, radius=14)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(LIME)
    c.drawString(350, 678, "기억 단위")
    memory_types = [
        ("fact", "확인된 사실", LIME),
        ("decision", "선택과 근거", CYAN),
        ("error", "재현·원인·해결", RED),
        ("preference", "사용자 선호", VIOLET),
        ("procedure", "재실행 절차", YELLOW),
        ("relation", "항목 간 연결", CYAN),
        ("episode", "목표·사건·결과", LIME),
    ]
    yy = 642
    for label, body, accent in memory_types:
        rounded_box(c, 350, yy - 2, 68, 19, fill=accent, stroke=None, radius=9)
        c.setFont("KR-Bold", 6.4)
        c.setFillColor(NAVY)
        c.drawCentredString(384, yy + 4, label)
        c.setFont("KR", 7.4)
        c.setFillColor(white)
        c.drawString(430, yy + 3, body)
        yy -= 39

    rows = [
        ("프로젝트 scope", "공용 결정·절차", "팀 전체가 같은 기준을 사용"),
        ("역할 전용 scope", "전문 role의 작업 문맥", "불필요한 기억 혼합을 줄임"),
        ("세션 scope", "임시 가설·진행 정보", "장기기억 오염을 방지"),
    ]
    draw_table(c, M, 334, [122, 188, 201], ["범위", "저장 대상", "고유한 장점"], rows, row_heights=36, font_size=6.9)

    rounded_box(c, M, 160, CW, 44, fill=SOFT_RED, stroke=None, radius=10)
    c.setFont("KR-Bold", 8.3)
    c.setFillColor(HexColor("#A03937"))
    c.drawString(M + 13, 185, "가드레일")
    draw_text(
        c,
        "기억은 현재 코드보다 우선하지 않는다 · 비밀값과 개인정보는 저장하지 않는다 · 추측과 임시 log는 장기기억에서 제외한다.",
        M + 13,
        171,
        CW - 26,
        size=6.5,
        color=INK,
        leading=8,
        max_lines=2,
    )

    benefit_strip(
        c,
        [
            ("문맥 연속성", "긴 프로젝트를 다시 설명하는 비용이 줄어든다.", LIME),
            ("실패 재사용", "해결한 오류와 절차를 다음 작업에서 회수한다.", CYAN),
            ("prompt 절약", "필요한 기억만 검색해 context를 작게 유지한다.", VIOLET),
        ],
        y=82,
        h=76,
    )
    c.showPage()


def draw_roles(c: canvas.Canvas) -> None:
    section_header(
        c,
        5,
        "03 / ROLE COLLABORATION",
        "누가 어떤 기준으로 판단하는가",
        "역할은 model 수를 늘리는 장치가 아니라 판단 기준·소유권·review 책임을 분리하는 장치다.",
    )

    roles = [
        ("DIRECTOR", "방향·우선순위·최종 승인", LIME, SOFT_LIME),
        ("PM", "범위·의존성·상태 정합", CYAN, SOFT_CYAN),
        ("SPECIALIST", "전문 구현·도구 실행", VIOLET, SOFT_VIOLET),
        ("QA", "반례·경계·증거 검토", RED, SOFT_RED),
        ("REVIEWER", "통합 가능성·위험 판단", YELLOW, SOFT_YELLOW),
    ]
    gap = 8
    rw = (CW - 4 * gap) / 5
    for index, (title, body, accent, fill) in enumerate(roles):
        x = M + index * (rw + gap)
        rounded_box(c, x, 568, rw, 133, fill=fill, stroke=None, radius=12)
        c.setFont("KR-Bold", 7.1)
        c.setFillColor(accent)
        c.drawString(x + 10, 674, title)
        draw_text(c, body, x + 10, 642, rw - 20, font="KR-Bold", size=7.5, color=INK, leading=11, max_lines=4)

    rounded_box(c, M, 397, CW, 139, fill=NAVY, stroke=None, radius=13)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(M + 15, 509, "HANDOFF CONTRACT")
    handoffs = [
        ("INTENT", "왜·무엇"),
        ("SPEC", "범위·AC"),
        ("ARTIFACT", "diff·output"),
        ("EVIDENCE", "log·result"),
        ("DECISION", "approve·revise"),
    ]
    xx = M + 18
    for index, (title, body) in enumerate(handoffs):
        rounded_box(c, xx + index * 100, 435, 78, 45, fill=NAVY_3, stroke=None, radius=8)
        c.setFont("KR-Bold", 6.8)
        c.setFillColor(white)
        c.drawCentredString(xx + index * 100 + 39, 461, title)
        c.setFont("KR", 6.2)
        c.setFillColor(HexColor("#BFD0DF"))
        c.drawCentredString(xx + index * 100 + 39, 447, body)
        if index < len(handoffs) - 1:
            arrow(c, xx + index * 100 + 81, 457, xx + (index + 1) * 100 - 4, 457, color=LIME, head=4)
    draw_text(c, "handoff에는 설명뿐 아니라 다음 역할이 판정할 수 있는 산출물과 증거가 포함된다.", M + 15, 415, CW - 30, size=6.9, color=HexColor("#BFD0DF"), leading=9, max_lines=2)

    rows = [
        ("단일 owner", "카드마다 구현 책임자를 한 명 둔다", "책임 공백과 중복 작업을 막음"),
        ("독립 reviewer", "구현자와 다른 기준으로 결과를 본다", "확증 편향과 자기 승인 감소"),
        ("명시적 handoff", "입력·출력·완료 조건을 함께 전달한다", "역할 사이 정보 손실 감소"),
        ("human escalation", "범위·권리·감각 판단은 사람에게 올린다", "자동화의 권한 과잉 방지"),
    ]
    draw_table(c, M, 366, [120, 205, 186], ["기능", "작동 방식", "고유한 장점"], rows, row_heights=40, font_size=6.9)

    benefit_strip(
        c,
        [
            ("전문화", "각 역할이 자신의 판단 기준에 집중한다.", LIME),
            ("맹점 감소", "구현과 검토를 다른 관점으로 분리한다.", CYAN),
            ("소유권 명확화", "누가 결정하고 누가 승인하는지 남는다.", VIOLET),
        ],
    )
    c.showPage()


def draw_isolation(c: canvas.Canvas) -> None:
    section_header(
        c,
        6,
        "04 / ISOLATION & PROVENANCE",
        "어디서 안전하게 변경하는가",
        "카드별 branch와 worktree가 변경을 격리하고, diff·test·commit이 변경의 출처를 보존한다.",
    )

    rounded_box(c, M, 479, CW, 225, fill=NAVY, stroke=None, radius=14)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(LIME)
    c.drawString(M + 16, 678, "PARALLEL WORK, SEPARATE FILESYSTEMS")
    circle_label(c, W / 2, 617, 35, "MAIN\nBASE", fill=NAVY_3, text_color=white, stroke=LIME, size=7.5)
    branches = [
        (M + 93, 555, "CARD A\nWORKTREE", CYAN),
        (W / 2, 535, "CARD B\nWORKTREE", VIOLET),
        (W - M - 93, 555, "CARD C\nWORKTREE", YELLOW),
    ]
    for x, y, label, accent in branches:
        arrow(c, W / 2, 579, x, y + 39, color=accent, head=5)
        circle_label(c, x, y, 38, label, fill=NAVY_3, text_color=white, stroke=accent, size=6.8)
    c.setFont("KR", 7)
    c.setFillColor(HexColor("#BFD0DF"))
    c.drawCentredString(W / 2, 500, "같은 저장소를 공유하되 수정 경로와 commit history는 카드별로 분리")

    rounded_box(c, M, 353, CW, 96, fill=white, stroke=LINE, radius=12)
    c.setFont("KR-Bold", 8.5)
    c.setFillColor(INK)
    c.drawString(M + 14, 422, "PROVENANCE CHAIN")
    chain = ["CARD", "BRANCH", "WORKTREE", "DIFF", "TEST", "COMMIT", "MERGE"]
    xx = M + 15
    for index, item in enumerate(chain):
        width = 56 if item != "WORKTREE" else 68
        rounded_box(c, xx, 381, width, 28, fill=SOFT_CYAN if index % 2 == 0 else SOFT_VIOLET, stroke=None, radius=7)
        c.setFont("KR-Bold", 6.4)
        c.setFillColor(INK)
        c.drawCentredString(xx + width / 2, 390, item)
        if index < len(chain) - 1:
            arrow(c, xx + width + 1, 395, xx + width + 7, 395, color=CYAN, head=3.5)
        xx += width + 11

    rows = [
        ("변경 격리", "다른 카드의 미완성 변경과 섞지 않는다", "오염 없는 review와 선택적 통합"),
        ("원자 commit", "하나의 기능 단위와 증거를 함께 기록", "rollback과 원인 추적이 쉬움"),
        ("명시적 staging", "변경 경로를 지정해 필요한 파일만 포함", "사용자 작업과 타 agent 변경을 보호"),
        ("통합 gate", "diff와 검증 결과를 보고 main에 반영", "merge 자체가 승인 기록이 됨"),
    ]
    rounded_box(c, M, 294, CW, 40, fill=SOFT_YELLOW, stroke=None, radius=9)
    c.setFont("KR-Bold", 6.8)
    c.setFillColor(HexColor("#785D25"))
    c.drawCentredString(W / 2, 309, "주의  |  worktree는 파일을 격리하지만 CPU·memory·engine cache 같은 공유 자원까지 격리하지는 않는다.")

    draw_table(c, M, 280, [120, 205, 186], ["기능", "작동 방식", "고유한 장점"], rows, row_heights=31, font_size=6.4)

    benefit_strip(
        c,
        [
            ("안전한 동시 작업", "서로의 미완성 변경을 직접 덮지 않는다.", LIME),
            ("복구 가능성", "작은 commit 단위로 되돌리고 비교한다.", CYAN),
            ("감사 가능성", "카드·변경·검증·통합이 한 이력으로 연결된다.", VIOLET),
        ],
        y=52,
        h=65,
    )
    c.showPage()


def draw_tools(c: canvas.Canvas) -> None:
    section_header(
        c,
        7,
        "05 / TOOL EXECUTION",
        "의도가 어떻게 실제 산출물이 되는가",
        "에이전트는 답변만 생성하지 않고, 저장소와 제작 도구를 inspect·edit·execute·capture 순서로 사용한다.",
    )

    rounded_box(c, M, 521, CW, 183, fill=white, stroke=LINE, radius=14)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(INK)
    c.drawString(M + 16, 678, "TOOL INVOCATION CYCLE")
    stages = [
        ("INSPECT", "파일·상태 확인", LIME),
        ("EDIT", "작은 변경 적용", CYAN),
        ("EXECUTE", "도구·명령 실행", VIOLET),
        ("CAPTURE", "log·artifact 보존", YELLOW),
        ("REPORT", "결과·한계 전달", RED),
    ]
    xx = M + 22
    for index, (title, body, accent) in enumerate(stages):
        rounded_box(c, xx + index * 97, 586, 76, 54, fill=Color(accent.red, accent.green, accent.blue, alpha=0.14), stroke=accent, radius=9)
        c.setFont("KR-Bold", 6.8)
        c.setFillColor(INK)
        c.drawCentredString(xx + index * 97 + 38, 618, title)
        c.setFont("KR", 6.2)
        c.setFillColor(MUTED)
        c.drawCentredString(xx + index * 97 + 38, 602, body)
        if index < len(stages) - 1:
            arrow(c, xx + index * 97 + 79, 613, xx + (index + 1) * 97 - 4, 613, color=accent, head=4)
    draw_text(c, "도구 결과는 다음 판단의 입력이 되고, 실패하면 log를 근거로 범위를 줄여 다시 실행한다.", M + 16, 550, CW - 32, size=7.1, color=MUTED, leading=10, max_lines=2)

    categories = [
        ("REPOSITORY", "search · diff · patch · Git", LIME, SOFT_LIME),
        ("RUNTIME", "compile · test · build · editor", CYAN, SOFT_CYAN),
        ("CONTENT", "asset · scene · document", VIOLET, SOFT_VIOLET),
        ("CONNECTORS", "memory · issue · communication", YELLOW, SOFT_YELLOW),
    ]
    gap = 10
    cw = (CW - 3 * gap) / 4
    for index, (title, body, accent, fill) in enumerate(categories):
        x = M + index * (cw + gap)
        rounded_box(c, x, 379, cw, 114, fill=fill, stroke=None, radius=11)
        c.setFont("KR-Bold", 7)
        c.setFillColor(accent)
        c.drawString(x + 11, 467, title)
        draw_text(c, body, x + 11, 435, cw - 22, size=7.2, color=INK, leading=11, max_lines=4)

    rows = [
        ("상태 기반 실행", "먼저 현재 파일·process·tool 상태를 읽는다", "추측성 수정과 잘못된 대상 작업 감소"),
        ("작은 patch", "변경 범위를 최소화하고 diff로 확인한다", "review와 rollback 비용 감소"),
        ("machine-readable 출력", "가능하면 JSON·XML·log·hash로 결과를 남긴다", "다른 agent와 사람이 재검증 가능"),
        ("권한 경계", "배포·외부 전송·파괴적 변경은 승인 후 실행", "자동화가 scope 밖으로 확장되는 것을 방지"),
    ]
    draw_table(c, M, 347, [120, 209, 182], ["기능", "작동 방식", "고유한 장점"], rows, row_heights=36, font_size=6.7)

    benefit_strip(
        c,
        [
            ("답변에서 산출물로", "자연어가 실제 파일·build·report로 이어진다.", LIME),
            ("반복 가능성", "같은 절차를 다시 실행하고 비교할 수 있다.", CYAN),
            ("handoff 감소", "사람이 도구 사이에서 결과를 옮기는 일이 줄어든다.", VIOLET),
        ],
        y=82,
        h=72,
    )
    c.showPage()


def draw_verification(c: canvas.Canvas) -> None:
    section_header(
        c,
        8,
        "06 / VERIFICATION",
        "완료를 어떻게 증명하는가",
        "자연어 완료 선언을 여러 층의 자동 gate와 독립 review가 확인할 수 있는 evidence bundle로 바꾼다.",
    )

    rounded_box(c, M, 451, 302, 253, fill=NAVY, stroke=None, radius=14)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(LIME)
    c.drawString(M + 16, 678, "LAYERED GATES")
    layers = [
        ("HUMAN", "감각·우선순위·출시", LIME, 232),
        ("INDEPENDENT REVIEW", "수용 기준·반례", RED, 208),
        ("INTEGRATION", "전체 흐름·상호작용", VIOLET, 184),
        ("TARGETED TEST", "상태·경계값", CYAN, 160),
        ("COMPILE / STATIC", "문법·형식·기본 무결성", YELLOW, 136),
    ]
    base_x = M + 35
    yy = 623
    for index, (title, body, accent, width) in enumerate(layers):
        x = base_x + (232 - width) / 2
        rounded_box(c, x, yy - index * 39, width, 30, fill=Color(accent.red, accent.green, accent.blue, alpha=0.18), stroke=accent, radius=7)
        c.setFont("KR-Bold", 6.6)
        c.setFillColor(white)
        c.drawString(x + 9, yy + 11 - index * 39, title)
        c.setFont("KR", 5.9)
        c.setFillColor(HexColor("#C5D5E2"))
        c.drawRightString(x + width - 9, yy + 11 - index * 39, body)

    rounded_box(c, 357, 451, 196, 253, fill=white, stroke=LINE, radius=14)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(INK)
    c.drawString(373, 678, "EVIDENCE BUNDLE")
    bundle = [
        ("INPUT", "요청·수용 기준", LIME),
        ("CHANGE", "diff·artifact", CYAN),
        ("RUN", "command·환경", VIOLET),
        ("RESULT", "log·raw output", YELLOW),
        ("IDENTITY", "commit·hash", RED),
        ("REVIEW", "판정·한계", LIME),
    ]
    yy = 641
    for title, body, accent in bundle:
        c.setFillColor(accent)
        c.circle(379, yy + 3, 3, fill=1, stroke=0)
        c.setFont("KR-Bold", 6.7)
        c.setFillColor(INK)
        c.drawString(389, yy, title)
        c.setFont("KR", 6.8)
        c.setFillColor(MUTED)
        c.drawString(442, yy, body)
        yy -= 31

    rows = [
        ("자동 gate", "compile·test·validator·build", "빠르고 반복 가능", "재미·감각은 판단 못함"),
        ("독립 review", "수용 기준·raw evidence·edge case", "자기 승인 위험 감소", "review 품질에 의존"),
        ("인간 승인", "감각·범위·권리·출시 판단", "목적과 책임 유지", "시간과 주의가 필요"),
    ]
    draw_table(c, M, 420, [105, 169, 129, 108], ["판정 주체", "담당 범위", "고유한 장점", "경계"], rows, row_heights=54, font_size=6.9)

    rounded_box(c, M, 202, CW, 50, fill=SOFT_YELLOW, stroke=None, radius=9)
    c.setFont("KR-Bold", 7.7)
    c.setFillColor(HexColor("#785D25"))
    c.drawCentredString(W / 2, 221, "핵심  |  test PASS는 기술적 위험을 줄였다는 뜻이지, 결과가 재미있거나 옳다는 뜻은 아니다.")

    benefit_strip(
        c,
        [
            ("거짓 완료 감소", "완료 선언에 재현 가능한 증거가 필요하다.", LIME),
            ("회귀 조기 발견", "작은 gate부터 실패 지점을 좁힌다.", CYAN),
            ("신뢰 형성", "사람과 agent가 같은 evidence를 보고 판단한다.", VIOLET),
        ],
        y=82,
        h=95,
    )
    c.showPage()


def draw_observability(c: canvas.Canvas) -> None:
    section_header(
        c,
        9,
        "07 / OBSERVABILITY & RECOVERY",
        "장시간 작업을 어떻게 통제하는가",
        "실행 상태·queue·log·heartbeat·context budget을 분리해 running, waiting, stuck를 구별하고 재개한다.",
    )

    rounded_box(c, M, 487, CW, 217, fill=NAVY, stroke=None, radius=14)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(LIME)
    c.drawString(M + 16, 678, "CONTROL SURFACE")
    signals = [
        ("STATE", "active · idle · blocked", LIME),
        ("QUEUE", "waiting · dependency", CYAN),
        ("PROGRESS", "tool call · artifact", VIOLET),
        ("LOG", "stdout · error · summary", YELLOW),
        ("HEARTBEAT", "alive · recover", RED),
        ("BUDGET", "context · image · cost", LIME),
    ]
    gap = 10
    sw = (CW - 32 - 2 * gap) / 3
    for index, (title, body, accent) in enumerate(signals):
        row, col = divmod(index, 3)
        x = M + 16 + col * (sw + gap)
        y = 592 - row * 73
        rounded_box(c, x, y, sw, 57, fill=NAVY_3, stroke=None, radius=9)
        c.setFont("KR-Bold", 6.8)
        c.setFillColor(accent)
        c.drawString(x + 10, y + 35, title)
        c.setFont("KR", 6.8)
        c.setFillColor(white)
        c.drawString(x + 10, y + 17, body)

    rounded_box(c, M, 334, CW, 123, fill=white, stroke=LINE, radius=12)
    c.setFont("KR-Bold", 8.7)
    c.setFillColor(INK)
    c.drawString(M + 14, 431, "RESOURCE CONTENTION PATTERN")
    circle_label(c, M + 85, 380, 29, "TASK A", fill=SOFT_CYAN, stroke=CYAN, size=7)
    circle_label(c, M + 174, 380, 29, "TASK B", fill=SOFT_VIOLET, stroke=VIOLET, size=7)
    arrow(c, M + 116, 380, M + 275, 380, color=CYAN, head=5)
    arrow(c, M + 205, 380, M + 275, 380, color=VIOLET, head=5)
    rounded_box(c, M + 282, 352, 120, 57, fill=SOFT_YELLOW, stroke=YELLOW, radius=9)
    c.setFont("KR-Bold", 8)
    c.setFillColor(INK)
    c.drawCentredString(M + 342, 385, "SHARED RESOURCE")
    c.setFont("KR", 6.2)
    c.setFillColor(MUTED)
    c.drawCentredString(M + 342, 368, "cache · CPU · memory")
    arrow(c, M + 406, 380, M + 455, 380, color=YELLOW, head=5)
    circle_label(c, M + 479, 380, 26, "SLOW", fill=SOFT_RED, stroke=RED, size=7)
    c.setFont("KR", 6.8)
    c.setFillColor(MUTED)
    c.drawString(M + 14, 346, "파일 격리와 실행 자원 격리는 다르므로 concurrency보다 resource affinity를 먼저 본다.")

    rows = [
        ("진행 상태", "card와 session 상태를 별도 표시", "작업 중·대기·정지를 구별"),
        ("log-first", "큰 출력은 파일로 남기고 요약만 context에 넣음", "문맥 팽창과 재확인 비용 감소"),
        ("checkpoint·resume", "card·worktree·commit·memory에서 재개", "중단 뒤 처음부터 반복하지 않음"),
        ("watchdog", "heartbeat와 실패 상태로 재시작·escalation", "장시간 무응답을 방치하지 않음"),
        ("budget policy", "image·context·tool time에 상한과 우선순위", "비용과 latency를 예측 가능하게 함"),
    ]
    draw_table(c, M, 326, [113, 216, 182], ["기능", "작동 방식", "고유한 장점"], rows, row_heights=29, font_size=6.2)

    benefit_strip(
        c,
        [
            ("상태 가시성", "실제로 느린지, 기다리는지, 멈췄는지 안다.", LIME),
            ("복구력", "중단된 지점의 증거와 문맥에서 이어간다.", CYAN),
            ("자원 효율", "공유 자원과 context를 기준으로 동시성을 조절한다.", VIOLET),
        ],
        y=58,
        h=72,
    )
    c.showPage()


def draw_governance(c: canvas.Canvas) -> None:
    section_header(
        c,
        10,
        "08 / HUMAN GOVERNANCE",
        "최종 권한과 책임은 누구에게 있는가",
        "AI가 실행 범위를 넓혀도 목표·우선순위·감각·외부 공개·권리 판단은 인간의 승인 아래 둔다.",
    )

    rounded_box(c, M, 512, CW, 192, fill=white, stroke=LINE, radius=14)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(INK)
    c.drawString(M + 16, 678, "AUTHORITY MAP")
    authority = [
        ("목표·우선순위", "HUMAN", LIME, SOFT_LIME),
        ("작업 분해·실행", "AI SYSTEM", CYAN, SOFT_CYAN),
        ("기계 검증", "AUTOMATION", VIOLET, SOFT_VIOLET),
        ("감각·품질 승인", "HUMAN", YELLOW, SOFT_YELLOW),
        ("외부 공개·배포", "HUMAN", RED, SOFT_RED),
        ("권리·license 판단", "HUMAN", LIME, SOFT_LIME),
    ]
    aw = (CW - 32 - 2 * 10) / 3
    for index, (label, owner, accent, fill) in enumerate(authority):
        row, col = divmod(index, 3)
        x = M + 16 + col * (aw + 10)
        y = 598 - row * 68
        rounded_box(c, x, y, aw, 53, fill=fill, stroke=None, radius=9)
        c.setFont("KR-Bold", 7.4)
        c.setFillColor(INK)
        c.drawString(x + 10, y + 31, label)
        c.setFont("KR-Bold", 6.2)
        c.setFillColor(accent)
        c.drawString(x + 10, y + 14, owner)

    rows = [
        ("비밀정보", "token·password·개인정보를 memory·repo·prompt에서 제외", "노출 위험 감소"),
        ("범위 통제", "파괴적 변경·배포·외부 전송은 명시적 승인 후 수행", "자동화의 권한 과잉 방지"),
        ("AI 공개", "주요 AI 도구명과 활용 방식을 제출물에 고지", "투명성과 규정 준수"),
        ("asset ledger", "원본·도구·모델·날짜·license·수정자·경로 기록", "권리 provenance 확보"),
        ("제출 경계", "기존 IP와 행사 기간 신규 결과물을 tag·README로 구분", "권리와 성과 범위 명확화"),
    ]
    draw_table(c, M, 482, [112, 263, 136], ["통제 항목", "작동 방식", "고유한 장점"], rows, row_heights=38, font_size=6.7)

    rounded_box(c, M, 204, CW, 59, fill=NAVY, stroke=None, radius=10)
    c.setFont("KR-Bold", 7.5)
    c.setFillColor(LIME)
    c.drawString(M + 13, 240, "NAN 2026 TERMS")
    c.setFont("KR", 6.8)
    c.setFillColor(white)
    c.drawString(M + 13, 222, "주요 AI 도구·활용 방식 고지 · 생성형 AI·open source·외부 API license 준수 · 제출 권리 확인")
    c.setFont("KR", 5.9)
    c.setFillColor(HexColor("#AFC1D3"))
    c.drawRightString(W - M - 13, 209, "https://nan2026.nhn.com/terms")
    c.linkURL("https://nan2026.nhn.com/terms", (W - M - 180, 205, W - M - 10, 220), relative=0)

    rounded_box(c, M, 82, CW, 94, fill=SOFT_LIME, stroke=None, radius=12)
    c.setFont("KR-Bold", 8.2)
    c.setFillColor(LIME_DARK)
    c.drawString(M + 14, 151, "FINAL CLAIM")
    draw_text(
        c,
        "DoodleUp의 AI 개발 시스템은 사람을 대체하는 자동 제작기가 아니다. 작업을 구조화하고, 문맥을 보존하고, 전문 역할을 연결하고, 실행 결과를 증거로 바꾸면서도 최종 권한은 사람에게 남기는 개발 운영 체계다.",
        M + 14,
        128,
        CW - 28,
        font="KR-Bold",
        size=9.6,
        color=INK,
        leading=14,
        max_lines=4,
        align="center",
    )
    c.showPage()


def build_pdf() -> Path:
    register_fonts()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(OUTPUT), pagesize=A4, pageCompression=1)
    c.setTitle("DoodleUp AI 개발 시스템 기술서 - Workflow & Capabilities")
    c.setAuthor("DoodleUp")
    c.setSubject("NAN 2026 참가신청용 AI 개발 작업 흐름과 범용 기능 설명")
    c.setKeywords("DoodleUp, NAN 2026, AgentDesk, AnchorMind, Memento, workflow, AI development system")
    c.setCreator("DoodleUp reproducible ReportLab generator")

    draw_cover(c)
    draw_theme_map(c)
    draw_orchestration(c)
    draw_memory(c)
    draw_roles(c)
    draw_isolation(c)
    draw_tools(c)
    draw_verification(c)
    draw_observability(c)
    draw_governance(c)

    c.save()
    return OUTPUT


if __name__ == "__main__":
    result = build_pdf()
    print(result)
