"""Generate the concise AI game development workflow brief."""

from __future__ import annotations

from math import atan2, cos, pi, sin
from pathlib import Path

from reportlab.lib.colors import Color, HexColor, white
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "output" / "pdf" / "nan2026-ai-game-development-workflow.pdf"
FONT_REGULAR = Path(r"C:\Windows\Fonts\malgun.ttf")
FONT_BOLD = Path(r"C:\Windows\Fonts\malgunbd.ttf")

W, H = A4
M = 42
CW = W - M * 2
PAGE_COUNT = 5

NAVY = HexColor("#0B1728")
NAVY_2 = HexColor("#142A43")
NAVY_3 = HexColor("#203A55")
PAPER = HexColor("#F4F7F5")
PAPER_2 = HexColor("#E8EFEC")
INK = HexColor("#142033")
MUTED = HexColor("#66758A")
LINE = HexColor("#CCD8D4")
LIME = HexColor("#5EE2A2")
LIME_DARK = HexColor("#16875F")
CYAN = HexColor("#58C8E6")
VIOLET = HexColor("#9A8DF2")
YELLOW = HexColor("#F5C75B")
RED = HexColor("#F26C68")
SOFT_LIME = HexColor("#DDF8EA")
SOFT_CYAN = HexColor("#DFF5FA")
SOFT_VIOLET = HexColor("#ECE9FD")
SOFT_YELLOW = HexColor("#FFF3D2")
SOFT_RED = HexColor("#FDE6E4")


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
    size: float = 9,
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
    width: float,
    height: float,
    *,
    fill: Color = white,
    stroke: Color | None = LINE,
    radius: float = 11,
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
    c.roundRect(x, y, width, height, radius, fill=1, stroke=stroke_flag)
    c.restoreState()


def chip(
    c: canvas.Canvas,
    text: str,
    x: float,
    y: float,
    *,
    fill: Color = NAVY,
    color: Color = LIME,
    size: float = 7.5,
    height: float = 20,
) -> float:
    label = safe_text(text)
    width = text_width(label, "KR-Bold", size) + 18
    rounded_box(c, x, y, width, height, fill=fill, stroke=None, radius=height / 2)
    c.setFont("KR-Bold", size)
    c.setFillColor(color)
    c.drawCentredString(x + width / 2, y + (height - size) / 2 + 1.4, label)
    return width


def arrow(
    c: canvas.Canvas,
    x1: float,
    y1: float,
    x2: float,
    y2: float,
    *,
    color: Color = MUTED,
    width: float = 1.5,
    head: float = 6,
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


def fit_single_line(text: str, preferred: float, minimum: float, width: float) -> float:
    size = preferred
    while size > minimum and text_width(text, "KR-Bold", size) > width:
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
    chip(c, kicker, M, H - 65)
    title_size = fit_single_line(title, 25, 18, CW)
    c.setFont("KR-Bold", title_size)
    c.setFillColor(INK)
    c.drawString(M, H - 108, safe_text(title))
    draw_text(c, subtitle, M, H - 127, CW, size=9, color=MUTED, max_lines=2)
    c.setStrokeColor(LINE)
    c.line(M, 42, W - M, 42)
    c.setFont("KR", 7.2)
    c.setFillColor(MUTED)
    c.drawString(M, 25, "AI Game Development Workflow")
    c.setFont("KR-Bold", 8)
    c.drawRightString(W - M, 25, f"{page:02d} / {PAGE_COUNT:02d}")
    key = f"page-{page}"
    c.bookmarkPage(key)
    c.addOutlineEntry(title, key, level=0, closed=False)


def accent_card(
    c: canvas.Canvas,
    x: float,
    y: float,
    width: float,
    height: float,
    title: str,
    body: str,
    accent: Color,
    fill: Color,
    *,
    title_size: float = 10,
    body_size: float = 7.4,
) -> None:
    rounded_box(c, x, y, width, height, fill=fill, stroke=None, radius=11)
    c.setFillColor(accent)
    c.roundRect(x, y + height - 5, width, 5, 3, fill=1, stroke=0)
    draw_text(
        c,
        title,
        x + 12,
        y + height - 29,
        width - 24,
        font="KR-Bold",
        size=title_size,
        color=INK,
        leading=title_size * 1.3,
        max_lines=2,
    )
    draw_text(
        c,
        body,
        x + 12,
        y + height - 55,
        width - 24,
        size=body_size,
        color=MUTED,
        leading=body_size * 1.45,
        max_lines=4,
    )


def benefit_cards(c: canvas.Canvas, items: list[tuple[str, str, Color]], y: float, height: float) -> None:
    gap = 9
    width = (CW - gap * (len(items) - 1)) / len(items)
    for index, (title, body, accent) in enumerate(items):
        x = M + index * (width + gap)
        rounded_box(c, x, y, width, height, fill=white, stroke=LINE, radius=10)
        c.setFillColor(accent)
        c.circle(x + 14, y + height - 18, 3.2, fill=1, stroke=0)
        c.setFont("KR-Bold", 8.5)
        c.setFillColor(INK)
        c.drawString(x + 24, y + height - 22, title)
        draw_text(
            c,
            body,
            x + 12,
            y + height - 44,
            width - 24,
            size=6.8,
            color=MUTED,
            leading=9.5,
            max_lines=3,
        )


def draw_cover(c: canvas.Canvas) -> None:
    c.setFillColor(NAVY)
    c.rect(0, 0, W, H, fill=1, stroke=0)
    c.setFillColor(LIME)
    c.rect(0, H - 9, W, 9, fill=1, stroke=0)

    c.setFillColor(NAVY_2)
    for gx in range(28, int(W), 28):
        for gy in range(28, int(H), 28):
            if (gx + gy) % 112 == 0:
                c.circle(gx, gy, 0.65, fill=1, stroke=0)

    chip(c, "NAN 2026 / GAME X AI", M, H - 72, fill=NAVY_3, color=LIME, size=8, height=22)
    c.setFont("KR-Bold", 31)
    c.setFillColor(white)
    c.drawString(M, H - 143, "AI 기반 게임 개발")
    c.setFont("KR-Bold", 29)
    c.setFillColor(LIME)
    c.drawString(M, H - 184, "구조와 작업 흐름")
    draw_text(
        c,
        "Planning, 역할 분담, 리뷰와 기억으로 게임 개발을 구조화하는 방식",
        M,
        H - 219,
        CW,
        size=11.5,
        color=HexColor("#C4D3E1"),
        max_lines=2,
    )

    rounded_box(c, M, 258, CW, 278, fill=NAVY_2, stroke=NAVY_3, radius=17)
    card_gap = 12
    card_width = (CW - card_gap * 2 - 32) / 3
    cards = [
        ("AgentDesk Bot", "요청 · 확인 · 승인", "Discord 접점", CYAN),
        ("AgentDesk", "분해 · 배정 · 칸반", "작업 중심", LIME),
        ("AnchorMind", "기억 · 회상 · 공유", "문맥 기반", VIOLET),
    ]
    for index, (title, body, role, accent) in enumerate(cards):
        x = M + 16 + index * (card_width + card_gap)
        rounded_box(c, x, 363, card_width, 132, fill=NAVY_3, stroke=None, radius=12)
        c.setFillColor(accent)
        c.circle(x + 17, 472, 4, fill=1, stroke=0)
        c.setFont("KR-Bold", 10.5)
        c.setFillColor(white)
        c.drawString(x + 29, 467, title)
        draw_text(c, body, x + 13, 429, card_width - 26, font="KR-Bold", size=8.2, color=white, align="center")
        c.setFont("KR", 7)
        c.setFillColor(HexColor("#B8CADA"))
        c.drawCentredString(x + card_width / 2, 389, role)
        if index < 2:
            arrow(c, x + card_width + 2, 429, x + card_width + card_gap - 2, 429, color=accent, head=4)

    rounded_box(c, M + 27, 282, CW - 54, 53, fill=HexColor("#24415F"), stroke=None, radius=10)
    c.setFont("KR-Bold", 11)
    c.setFillColor(LIME)
    c.drawCentredString(W / 2, 311, "요청  ->  Planning  ->  역할 배정  ->  실행  ->  리뷰  ->  기억")
    c.setFont("KR", 7.4)
    c.setFillColor(HexColor("#C0D2DF"))
    c.drawCentredString(W / 2, 292, "세 도구가 하나의 작업 흐름으로 연결된다.")

    c.setFont("KR", 8)
    c.setFillColor(HexColor("#9DB1C5"))
    c.drawString(M, 82, "제출 검토본 v4.0  |  2026.08.10")
    c.setFont("KR-Bold", 8)
    c.setFillColor(LIME)
    c.drawRightString(W - M, 82, f"01 / {PAGE_COUNT:02d}")
    c.bookmarkPage("page-1")
    c.addOutlineEntry("표지", "page-1", level=0, closed=False)
    c.showPage()


def draw_overview(c: canvas.Canvas) -> None:
    section_header(
        c,
        2,
        "SYSTEM OVERVIEW",
        "AI가 게임 개발을 구조화하는 방식",
        "Discord는 접점, AgentDesk는 Planning과 작업 흐름, AnchorMind는 공유 문맥을 담당한다.",
    )

    rounded_box(c, M, 362, CW, 342, fill=white, stroke=LINE, radius=14)
    c.setFont("KR-Bold", 9)
    c.setFillColor(INK)
    c.drawString(M + 16, 678, "전체 구조")

    top_cards = [
        (M + 18, 560, 126, 76, "AgentDesk Bot", "요청 · 승인 · 알림", CYAN, SOFT_CYAN),
        (M + 193, 548, 126, 100, "AgentDesk", "분해 · 배정\n상태 · 리뷰", LIME, SOFT_LIME),
        (M + 368, 560, 126, 76, "Kanban", "진행 · 대기 · 완료", VIOLET, SOFT_VIOLET),
    ]
    for x, y, width, height, title, body, accent, fill in top_cards:
        rounded_box(c, x, y, width, height, fill=fill, stroke=accent, radius=10)
        c.setFont("KR-Bold", 9)
        c.setFillColor(INK)
        c.drawCentredString(x + width / 2, y + height - 26, title)
        draw_text(c, body, x + 10, y + height - 48, width - 20, size=6.8, color=MUTED, leading=9.5, align="center", max_lines=2)
    arrow(c, M + 147, 598, M + 188, 598, color=CYAN)
    arrow(c, M + 322, 598, M + 363, 598, color=LIME)

    c.setStrokeColor(LIME)
    c.setLineWidth(1.4)
    c.line(W / 2, 548, W / 2, 524)
    c.line(M + 72, 524, W - M - 72, 524)

    roles = [
        ("Planning Agent", "계획·카드 분해"),
        ("Tech", "구현·기술 판단"),
        ("Art", "자산·시각 품질"),
        ("Review", "검토·완료 판정"),
    ]
    role_width = 104
    role_gap = 20
    for index, (title, body) in enumerate(roles):
        x = M + 18 + index * (role_width + role_gap)
        c.setStrokeColor(LIME)
        c.line(x + role_width / 2, 524, x + role_width / 2, 503)
        rounded_box(c, x, 451, role_width, 52, fill=PAPER_2, stroke=None, radius=9)
        c.setFont("KR-Bold", 8.2)
        c.setFillColor(INK)
        c.drawCentredString(x + role_width / 2, 482, title)
        c.setFont("KR", 6.4)
        c.setFillColor(MUTED)
        c.drawCentredString(x + role_width / 2, 465, body)

    rounded_box(c, M + 18, 385, CW - 36, 45, fill=NAVY, stroke=None, radius=9)
    c.setFont("KR-Bold", 8.5)
    c.setFillColor(LIME)
    c.drawString(M + 32, 403, "AnchorMind")
    c.setFont("KR", 7.2)
    c.setFillColor(white)
    c.drawString(M + 110, 403, "프로젝트의 단기·장기 기억을 모든 역할이 함께 사용")
    for index in range(4):
        x = M + 18 + index * (role_width + role_gap) + role_width / 2
        arrow(c, x, 430, x, 447, color=VIOLET, head=4, dashed=True)

    table_top = 326
    widths = [112, 251, 148]
    headers = ["구성", "담당", "개발 중 보이는 결과"]
    c.setFillColor(NAVY)
    c.roundRect(M, table_top - 29, CW, 29, 8, fill=1, stroke=0)
    xx = M
    for width, header in zip(widths, headers):
        draw_text(c, header, xx + 9, table_top - 12, width - 18, font="KR-Bold", size=7.2, color=white, max_lines=1)
        xx += width
    rows = [
        ("AgentDesk", "작업 분해, 역할 배정, 칸반 상태와 리뷰 흐름 관리", "카드 · 담당자 · 상태"),
        ("AgentDesk Bot", "Discord에서 요청 접수, 진행 확인, 승인과 알림", "대화 · 결과 알림"),
        ("AnchorMind", "현재 작업 문맥과 프로젝트 지식을 저장·회상·공유", "결정 · 오류 · 절차"),
    ]
    y = table_top - 29
    for index, row in enumerate(rows):
        row_h = 61
        c.setFillColor(white if index % 2 == 0 else PAPER_2)
        c.rect(M, y - row_h, CW, row_h, fill=1, stroke=0)
        c.setStrokeColor(LINE)
        c.line(M, y - row_h, W - M, y - row_h)
        xx = M
        for col, (width, value) in enumerate(zip(widths, row)):
            if col:
                c.line(xx, y, xx, y - row_h)
            draw_text(
                c,
                value,
                xx + 9,
                y - 20,
                width - 18,
                font="KR-Bold" if col == 0 else "KR",
                size=7.2,
                color=INK if col == 0 else MUTED,
                leading=10.5,
                max_lines=3,
            )
            xx += width
        y -= row_h
    c.setStrokeColor(LINE)
    c.roundRect(M, y, CW, table_top - y, 8, fill=0, stroke=1)
    c.showPage()


def draw_agentdesk(c: canvas.Canvas) -> None:
    section_header(
        c,
        3,
        "AGENTDESK",
        "계획부터 리뷰까지 역할을 나눠 개발한다",
        "Planning Agent가 작업을 구조화하고, 전문 역할이 실행한 결과를 리뷰한 뒤 완료한다.",
    )

    c.setFont("KR-Bold", 9)
    c.setFillColor(INK)
    c.drawString(M, 686, "역할 분담")
    roles = [
        ("Planning Agent", "게임의 목표를 계획과 카드로 나누고 우선순위·의존성·완료 조건을 정한다.", LIME, SOFT_LIME),
        ("Tech Agent", "기술 판단과 구현을 맡고 변경 내용과 검증 결과를 남긴다.", CYAN, SOFT_CYAN),
        ("Art Agent", "필요한 자산과 시각 결과를 만들고 품질 기준을 확인한다.", VIOLET, SOFT_VIOLET),
        ("Reviewer / QA", "구현자와 분리된 관점에서 결과와 완료 조건을 검토한다.", YELLOW, SOFT_YELLOW),
    ]
    card_width = (CW - 10) / 2
    for index, (title, body, accent, fill) in enumerate(roles):
        row, col = divmod(index, 2)
        x = M + col * (card_width + 10)
        y = 580 - row * 92
        accent_card(c, x, y, card_width, 80, title, body, accent, fill, title_size=9.2, body_size=7)

    rounded_box(c, M, 337, CW, 125, fill=NAVY, stroke=None, radius=13)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(M + 15, 435, "칸반 흐름")
    states = [
        ("BACKLOG", "요청 대기"),
        ("PLANNING", "계획·분해"),
        ("READY", "조건 확인"),
        ("DOING", "작업 중"),
        ("REVIEW", "검토"),
        ("DONE", "완료"),
    ]
    state_width = 72
    state_gap = 13
    start_x = M + 9
    for index, (title, body) in enumerate(states):
        x = start_x + index * (state_width + state_gap)
        rounded_box(c, x, 375, state_width, 43, fill=NAVY_3, stroke=None, radius=8)
        c.setFont("KR-Bold", 6.8)
        c.setFillColor(white)
        c.drawCentredString(x + state_width / 2, 400, title)
        c.setFont("KR", 6.2)
        c.setFillColor(HexColor("#BFD0DF"))
        c.drawCentredString(x + state_width / 2, 384, body)
        if index < 5:
            arrow(c, x + state_width + 2, 397, x + state_width + state_gap - 2, 397, color=LIME, head=4)
    c.setFont("KR", 6.6)
    c.setFillColor(RED)
    c.drawRightString(W - M - 15, 353, "보완 필요 시 REVIEW -> DOING")

    rounded_box(c, M, 171, CW, 139, fill=white, stroke=LINE, radius=12)
    c.setFont("KR-Bold", 9)
    c.setFillColor(INK)
    c.drawString(M + 15, 283, "이 구조로 개발하는 순서")
    steps = [
        ("01", "목표 접수", "요구 확인"),
        ("02", "Planning", "계획·분해"),
        ("03", "역할 배정", "카드 전달"),
        ("04", "개발", "Tech·Art"),
        ("05", "리뷰", "검토·보완"),
        ("06", "완료", "결과·기억"),
    ]
    step_gap = 8
    step_width = (CW - 30 - step_gap * 5) / 6
    for index, (num, title, body) in enumerate(steps):
        x = M + 15 + index * (step_width + step_gap)
        rounded_box(c, x, 205, step_width, 57, fill=PAPER_2, stroke=None, radius=8)
        c.setFont("KR-Bold", 6.2)
        c.setFillColor(LIME_DARK)
        c.drawString(x + 8, 244, num)
        c.setFont("KR-Bold", 7.3)
        c.setFillColor(INK)
        c.drawCentredString(x + step_width / 2, 226, title)
        c.setFont("KR", 5.8)
        c.setFillColor(MUTED)
        c.drawCentredString(x + step_width / 2, 211, body)
        if index < 5:
            arrow(c, x + step_width + 1, 233, x + step_width + step_gap - 1, 233, color=CYAN, head=3.5)
    draw_text(
        c,
        "Planning 결과, 담당 역할, 완료 조건, 현재 상태, 리뷰 결과가 카드에 함께 남는다.",
        M + 15,
        190,
        CW - 30,
        size=6.8,
        color=MUTED,
        align="center",
        max_lines=1,
    )

    benefit_cards(
        c,
        [
            ("계획이 먼저 보임", "Planning에서 범위·순서·완료 조건을 먼저 정한다.", LIME),
            ("역할과 진행이 보임", "칸반에서 담당·작업·대기·리뷰 상태를 한눈에 본다.", CYAN),
            ("리뷰가 흐름에 포함됨", "완료 전에 별도 역할이 결과를 확인한다.", VIOLET),
        ],
        y=66,
        height=79,
    )
    c.showPage()


def message_bubble(
    c: canvas.Canvas,
    x: float,
    y: float,
    width: float,
    height: float,
    sender: str,
    body: str,
    *,
    fill: Color,
    accent: Color,
    align_right: bool = False,
) -> None:
    rounded_box(c, x, y, width, height, fill=fill, stroke=None, radius=11)
    c.setFont("KR-Bold", 6.6)
    c.setFillColor(accent)
    if align_right:
        c.drawRightString(x + width - 11, y + height - 18, sender)
    else:
        c.drawString(x + 11, y + height - 18, sender)
    draw_text(
        c,
        body,
        x + 11,
        y + height - 38,
        width - 22,
        size=7,
        color=INK,
        leading=10.5,
        max_lines=4,
        align="right" if align_right else "left",
    )


def draw_discord(c: canvas.Canvas) -> None:
    section_header(
        c,
        4,
        "DISCORD BOT",
        "Discord에서 AI 개발 흐름을 바로 사용한다",
        "AgentDesk Bot으로 요청하고 Planning, 역할 배정, 리뷰와 승인을 대화에서 확인한다.",
    )

    rounded_box(c, M, 188, 318, 516, fill=NAVY, stroke=None, radius=14)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(M + 16, 677, "사용 예")
    c.setFont("KR", 6.7)
    c.setFillColor(HexColor("#BFD0DF"))
    c.drawRightString(M + 300, 677, "#ai-game-dev")

    message_bubble(
        c,
        M + 90,
        603,
        207,
        54,
        "USER",
        "새 기능 개발을 시작해줘.",
        fill=SOFT_CYAN,
        accent=HexColor("#167F99"),
        align_right=True,
    )
    message_bubble(
        c,
        M + 18,
        500,
        248,
        82,
        "AGENTDESK BOT",
        "카드를 만들었습니다.\n담당: Planning Agent\n상태: PLANNING",
        fill=white,
        accent=LIME_DARK,
    )
    message_bubble(
        c,
        M + 18,
        392,
        248,
        84,
        "AGENTDESK BOT",
        "계획을 카드로 나누고 Tech·Art Agent를 배정했습니다.\n결과는 REVIEW에서 확인합니다.",
        fill=white,
        accent=LIME_DARK,
    )
    message_bubble(
        c,
        M + 132,
        318,
        165,
        51,
        "USER",
        "확인했어. 승인할게.",
        fill=SOFT_VIOLET,
        accent=HexColor("#6757BE"),
        align_right=True,
    )
    message_bubble(
        c,
        M + 18,
        220,
        248,
        73,
        "AGENTDESK BOT",
        "DONE 처리했습니다.\n검증된 결정과 절차는 AnchorMind에 공유됩니다.",
        fill=SOFT_LIME,
        accent=LIME_DARK,
    )

    side_x = M + 332
    side_width = CW - 332
    capabilities = [
        ("요청", "새 작업 등록", CYAN, SOFT_CYAN),
        ("Planning", "계획·카드 분해 확인", LIME, SOFT_LIME),
        ("진행", "담당·상태 확인", CYAN, SOFT_CYAN),
        ("리뷰", "승인·보완 요청", VIOLET, SOFT_VIOLET),
        ("알림", "완료·차단 알림", YELLOW, SOFT_YELLOW),
    ]
    for index, (title, body, accent, fill) in enumerate(capabilities):
        y = 602 - index * 83
        accent_card(c, side_x, y, side_width, 70, title, body, accent, fill, title_size=8.5, body_size=6.4)

    benefit_cards(
        c,
        [
            ("접근이 쉬움", "PC나 모바일의 Discord에서 바로 사용할 수 있다.", CYAN),
            ("확인이 빠름", "상태와 결과 알림이 같은 채널에 모인다.", LIME),
            ("승인이 간단함", "대화를 이어가며 리뷰와 보완 요청을 남긴다.", VIOLET),
        ],
        y=66,
        height=92,
    )
    c.showPage()


def draw_memory(c: canvas.Canvas) -> None:
    section_header(
        c,
        5,
        "ANCHORMIND",
        "게임 개발의 기억을 에이전트가 공유한다",
        "현재 작업의 짧은 문맥과 게임 개발에 오래 남아야 할 지식을 구분해 관리한다.",
    )

    memory_width = (CW - 14) / 2
    accent_card(
        c,
        M,
        559,
        memory_width,
        137,
        "작업 기억  |  단기",
        "현재 카드의 목표, 진행 상태, 남은 문제, 다음 역할에 넘길 내용을 저장한다.",
        CYAN,
        SOFT_CYAN,
        title_size=10,
        body_size=7.6,
    )
    accent_card(
        c,
        M + memory_width + 14,
        559,
        memory_width,
        137,
        "프로젝트 기억  |  장기",
        "확정된 결정, 팀 규칙, 해결한 오류, 반복해서 사용할 절차를 오래 보존한다.",
        VIOLET,
        SOFT_VIOLET,
        title_size=10,
        body_size=7.6,
    )

    arrow(c, M + memory_width / 2, 555, W / 2 - 42, 520, color=CYAN)
    arrow(c, M + memory_width + 14 + memory_width / 2, 555, W / 2 + 42, 520, color=VIOLET)
    rounded_box(c, M + 96, 448, CW - 192, 72, fill=NAVY, stroke=None, radius=12)
    c.setFont("KR-Bold", 12)
    c.setFillColor(LIME)
    c.drawCentredString(W / 2, 486, "AnchorMind")
    c.setFont("KR", 7.2)
    c.setFillColor(white)
    c.drawCentredString(W / 2, 466, "프로젝트 범위의 공용 기억 저장소")

    c.setStrokeColor(LIME)
    c.setLineWidth(1.3)
    c.line(W / 2, 448, W / 2, 425)
    c.line(M + 57, 425, W - M - 57, 425)
    agents = [("Planning Agent", 0), ("Tech", 1), ("Art", 2), ("Review", 3)]
    agent_width = 94
    agent_gap = 34
    for title, index in agents:
        x = M + 14 + index * (agent_width + agent_gap)
        c.line(x + agent_width / 2, 425, x + agent_width / 2, 407)
        rounded_box(c, x, 361, agent_width, 46, fill=PAPER_2, stroke=None, radius=8)
        c.setFont("KR-Bold", 8)
        c.setFillColor(INK)
        c.drawCentredString(x + agent_width / 2, 379, title)

    rounded_box(c, M, 184, CW, 143, fill=white, stroke=LINE, radius=12)
    c.setFont("KR-Bold", 9)
    c.setFillColor(INK)
    c.drawString(M + 15, 300, "에이전트 사이에서 기억이 이어지는 순서")
    cycle = [
        ("01", "회상", "작업 전에 관련 결정과 절차를 불러온다.", CYAN, SOFT_CYAN),
        ("02", "작업", "같은 문맥을 기준으로 맡은 일을 수행한다.", LIME, SOFT_LIME),
        ("03", "저장", "확인된 결과와 해결 절차를 남긴다.", VIOLET, SOFT_VIOLET),
        ("04", "공유", "다음 에이전트가 같은 기억을 이어받는다.", YELLOW, SOFT_YELLOW),
    ]
    cycle_width = (CW - 30 - 9 * 3) / 4
    for index, (num, title, body, accent, fill) in enumerate(cycle):
        x = M + 15 + index * (cycle_width + 9)
        rounded_box(c, x, 213, cycle_width, 67, fill=fill, stroke=None, radius=8)
        c.setFont("KR-Bold", 6)
        c.setFillColor(accent)
        c.drawString(x + 8, 262, num)
        c.setFont("KR-Bold", 8)
        c.setFillColor(INK)
        c.drawString(x + 8, 245, title)
        draw_text(c, body, x + 8, 228, cycle_width - 16, size=5.9, color=MUTED, leading=8.3, max_lines=3)
        if index < 3:
            arrow(c, x + cycle_width + 1, 246, x + cycle_width + 8, 246, color=accent, head=3.5)

    rounded_box(c, M, 68, CW, 89, fill=SOFT_LIME, stroke=None, radius=12)
    c.setFont("KR-Bold", 8.5)
    c.setFillColor(LIME_DARK)
    c.drawCentredString(W / 2, 133, "반복 설명은 줄이고, 결정 기준은 맞추고, 중단된 작업은 이어서 진행한다.")
    c.setFont("KR-Bold", 10)
    c.setFillColor(INK)
    c.drawCentredString(W / 2, 101, "Discord에서 요청하고, Planning이 나누고, AgentDesk가 배정하고, AnchorMind가 기억한다.")
    c.showPage()


def build_pdf() -> Path:
    register_fonts()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(OUTPUT), pagesize=A4, pageCompression=1)
    c.setTitle("AI 기반 게임 개발 작업 체계")
    c.setAuthor("NAN 2026 참가팀")
    c.setSubject("AgentDesk Planning, Discord Bot, AnchorMind 기반의 구조적 AI 게임 개발 흐름")
    c.setKeywords("NAN 2026, AgentDesk, Planning Agent, Discord Bot, AnchorMind, Kanban, AI game development")
    c.setCreator("Reproducible ReportLab generator")

    draw_cover(c)
    draw_overview(c)
    draw_agentdesk(c)
    draw_discord(c)
    draw_memory(c)

    c.save()
    return OUTPUT


if __name__ == "__main__":
    result = build_pdf()
    print(result)
