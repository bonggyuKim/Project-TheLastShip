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
PAGE_COUNT = 7

NAVY = HexColor("#131715")
NAVY_2 = HexColor("#1F2621")
NAVY_3 = HexColor("#2B382E")
PAPER = HexColor("#F7F6F0")
PAPER_2 = HexColor("#EEF0EA")
INK = HexColor("#151A18")
MUTED = HexColor("#68716E")
LINE = HexColor("#C8CEC7")
LIME = HexColor("#B8EF54")
LIME_DARK = HexColor("#4C7423")
CYAN = HexColor("#86D4D6")
VIOLET = HexColor("#B9A9EE")
YELLOW = HexColor("#F1CC73")
RED = HexColor("#E8756D")
SOFT_LIME = HexColor("#EAF7D9")
SOFT_CYAN = HexColor("#E4F4F2")
SOFT_VIOLET = HexColor("#EEEAFB")
SOFT_YELLOW = HexColor("#FBF1D6")
SOFT_RED = HexColor("#F8E6E2")


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
    radius = min(radius, 5)
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
    c.setStrokeColor(INK)
    c.setLineWidth(0.8)
    c.line(M, H - 48, W - M, H - 48)
    c.setFont("KR-Bold", 7.5)
    c.setFillColor(MUTED)
    c.drawString(M, H - 35, f"{page - 1:02d}  /  {safe_text(kicker)}")
    c.setFillColor(LIME)
    c.rect(W - M - 8, H - 40, 8, 8, fill=1, stroke=0)
    title_size = fit_single_line(title, 27, 18, CW)
    c.setFont("KR-Bold", title_size)
    c.setFillColor(INK)
    c.drawString(M, H - 100, safe_text(title))
    draw_text(c, subtitle, M, H - 120, CW, size=8.7, color=MUTED, max_lines=2)
    c.setStrokeColor(LINE)
    c.setLineWidth(0.8)
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
    rounded_box(c, x, y, width, height, fill=white, stroke=LINE, radius=4, line_width=0.7)
    c.setFillColor(accent)
    c.rect(x, y, 4, height, fill=1, stroke=0)
    draw_text(
        c,
        title,
        x + 16,
        y + height - 25,
        width - 28,
        font="KR-Bold",
        size=title_size,
        color=INK,
        leading=title_size * 1.3,
        max_lines=2,
    )
    draw_text(
        c,
        body,
        x + 16,
        y + height - 50,
        width - 28,
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
        c.setStrokeColor(LINE)
        c.setLineWidth(0.8)
        c.line(x, y + height, x + width, y + height)
        c.setFillColor(accent)
        c.rect(x, y + height - 4, 28, 4, fill=1, stroke=0)
        c.setFont("KR-Bold", 7.2)
        c.setFillColor(MUTED)
        c.drawString(x, y + height - 20, f"0{index + 1}")
        c.setFont("KR-Bold", 8.7)
        c.setFillColor(INK)
        c.drawString(x + 27, y + height - 20, title)
        draw_text(
            c,
            body,
            x,
            y + height - 43,
            width,
            size=6.8,
            color=MUTED,
            leading=9.5,
            max_lines=3,
        )


def draw_cover(c: canvas.Canvas) -> None:
    c.setFillColor(PAPER)
    c.rect(0, 0, W, H, fill=1, stroke=0)
    c.setFillColor(LIME)
    c.rect(0, H - 12, W, 12, fill=1, stroke=0)
    c.setFillColor(INK)
    c.rect(M, H / 2 - 108, 8, 216, fill=1, stroke=0)
    c.setFont("KR-Bold", 38)
    c.drawString(M + 28, H / 2 + 28, "AI 활용을 위한")
    c.setFont("KR-Bold", 42)
    c.drawString(M + 28, H / 2 - 30, "게임 개발 구조화")
    c.setFillColor(LIME)
    c.rect(M + 28, H / 2 - 62, 184, 7, fill=1, stroke=0)
    c.setStrokeColor(LINE)
    c.setLineWidth(0.7)
    c.line(M + 28, H / 2 - 110, W - M, H / 2 - 110)
    c.bookmarkPage("page-1")
    c.addOutlineEntry("표지", "page-1", level=0, closed=False)
    c.showPage()


def draw_why_structure(c: canvas.Canvas) -> None:
    section_header(
        c,
        2,
        "WHY STRUCTURE",
        "AI 활용을 위해 먼저 개발 구조를 설계한다",
        "AI는 빠르게 생성하지만, 목표·범위·맥락·완료 기준이 없으면 빠르게 잘못된 결과를 만든다.",
    )

    c.setFont("KR-Bold", 9)
    c.setFillColor(INK)
    c.drawString(M, 686, "AI를 쓰면서 실제로 생기는 문제와 구조적 대응")

    problems = [
        (
            "모호한 요청",
            "무엇을 만들지보다 왜 필요한지, 어디까지가 범위인지, 무엇이 완료인지 먼저 정해야 한다.",
            "Planning Agent",
            LIME,
            SOFT_LIME,
        ),
        (
            "큰 작업과 충돌",
            "코드·아트·씬·테스트를 한 에이전트가 동시에 다루면 변경 경계와 책임이 무너진다.",
            "역할·격리",
            CYAN,
            SOFT_CYAN,
        ),
        (
            "컨텍스트 단절",
            "세션이 바뀔 때마다 결정·오류·절차를 다시 설명하면 같은 실수가 반복된다.",
            "AnchorMind",
            VIOLET,
            SOFT_VIOLET,
        ),
        (
            "완료를 믿기 어려움",
            "AI의 완료 문장만으로는 빌드·테스트·플레이 품질과 범위 준수를 확인할 수 없다.",
            "증거·QA·승인",
            YELLOW,
            SOFT_YELLOW,
        ),
    ]
    card_width = (CW - 10) / 2
    for index, (title, body, response, accent, fill) in enumerate(problems):
        row, col = divmod(index, 2)
        x = M + col * (card_width + 10)
        y = 535 - row * 130
        accent_card(c, x, y, card_width, 112, title, body, accent, fill, title_size=9.4, body_size=7.1)
        c.setFont("KR-Bold", 7.2)
        c.setFillColor(accent)
        c.drawRightString(x + card_width - 12, y + 13, f"→ {response}")

    rounded_box(c, M, 188, CW, 129, fill=NAVY, stroke=None, radius=13)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(M + 15, 291, "설계 원칙")
    c.setFont("KR-Bold", 10)
    c.setFillColor(white)
    c.drawCentredString(W / 2, 264, "의도  →  범위  →  역할  →  증거  →  승인  →  기억")
    c.setFont("KR", 7.4)
    c.setFillColor(HexColor("#C0D2DF"))
    draw_text(
        c,
        "AI를 한 명의 자율적인 개발자로 가정하지 않고, 명확한 경계와 검증 가능한 인수인계를 따라 일하는 역할 집합으로 사용한다.",
        M + 28,
        235,
        CW - 56,
        size=7.4,
        color=HexColor("#C0D2DF"),
        align="center",
        max_lines=2,
    )

    benefit_cards(
        c,
        [
            ("빠르게 만들기 전에 나눈다", "AI가 처리할 단위와 완료 조건을 먼저 고정한다.", LIME),
            ("생성보다 인수인계가 중요하다", "다음 역할이 같은 기준으로 이어받을 수 있게 남긴다.", CYAN),
            ("자동화와 통제를 함께 둔다", "반복 검증은 자동화하고 방향과 품질은 사람이 승인한다.", VIOLET),
        ],
        y=66,
        height=91,
    )
    c.showPage()


def draw_overview(c: canvas.Canvas) -> None:
    section_header(
        c,
        3,
        "SYSTEM OVERVIEW",
        "AI 활용을 위한 게임 개발 구조화",
        "사람의 의도는 Planning으로 분해하고, 역할·격리·검증·기억을 연결해 AI가 안정적으로 일하도록 만든다.",
    )

    rounded_box(c, M, 362, CW, 342, fill=white, stroke=LINE, radius=14)
    c.setFont("KR-Bold", 9)
    c.setFillColor(INK)
    c.drawString(M + 16, 678, "전체 구조")

    top_cards = [
        (M + 8, 560, 112, 76, "Discord Bot", "의도 · 승인", CYAN, SOFT_CYAN),
        (M + 139, 560, 112, 76, "Planning Agent", "목표 · 분해", LIME, SOFT_LIME),
        (M + 270, 560, 112, 76, "AgentDesk", "역할 · 격리", CYAN, SOFT_CYAN),
        (M + 401, 560, 112, 76, "Kanban", "상태 · 리뷰", VIOLET, SOFT_VIOLET),
    ]
    for x, y, width, height, title, body, accent, fill in top_cards:
        rounded_box(c, x, y, width, height, fill=fill, stroke=accent, radius=10)
        c.setFont("KR-Bold", 9)
        c.setFillColor(INK)
        c.drawCentredString(x + width / 2, y + height - 26, title)
        draw_text(c, body, x + 10, y + height - 48, width - 20, size=6.8, color=MUTED, leading=9.5, align="center", max_lines=2)
    for left, right, color in zip(top_cards, top_cards[1:], (CYAN, LIME, CYAN)):
        arrow(c, left[0] + left[2] + 2, 598, right[0] - 2, 598, color=color)

    c.setStrokeColor(LIME)
    c.setLineWidth(1.4)
    agentdesk_center = top_cards[2][0] + top_cards[2][2] / 2
    c.line(agentdesk_center, 548, agentdesk_center, 524)
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
    c.drawString(M + 110, 403, "개발 흐름의 단기·장기 기억을 모든 역할이 함께 사용")
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
        ("AgentDesk", "역할 배정, 격리된 작업 공간, 상태와 리뷰 흐름 관리", "담당자 · 작업 공간 · 상태"),
        ("Discord Bot", "사람의 요청·확인·승인을 개발 흐름에 연결", "의도 · 승인 · 알림"),
        ("AnchorMind", "결정·오류·절차를 저장·회상·공유", "기억 · 기준 · 인수인계"),
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
        4,
        "AGENTDESK",
        "AI가 일할 수 있도록 역할과 경계를 나눈다",
        "Planning Agent가 작업 단위를 정하고, 각 역할은 격리된 변경과 독립된 검증 결과를 남긴다.",
    )

    c.setFont("KR-Bold", 9)
    c.setFillColor(INK)
    c.drawString(M, 686, "역할 분담")
    roles = [
        ("Planning Agent", "사람의 목표를 실행 가능한 카드로 바꾸고 우선순위·의존성·완료 조건을 정한다.", LIME, SOFT_LIME),
        ("Tech Agent", "격리된 작업 공간에서 구현하고 변경·검증 결과를 남긴다.", CYAN, SOFT_CYAN),
        ("Art Agent", "필요한 자산을 만들고 적용 경로와 품질 기준을 확인한다.", VIOLET, SOFT_VIOLET),
        ("Reviewer / QA", "구현자와 분리된 관점에서 테스트·증거·완료 조건을 검토한다.", YELLOW, SOFT_YELLOW),
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
        ("01", "의도 정의", "사람의 목표"),
        ("02", "Planning", "카드·조건"),
        ("03", "역할 배정", "격리된 작업"),
        ("04", "구현·생성", "Tech·Art"),
        ("05", "검증", "QA·플레이"),
        ("06", "반영·기억", "Git·기준"),
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
            ("작업 단위가 명확함", "AI가 처리할 범위와 완료 조건을 먼저 고정한다.", LIME),
            ("변경이 격리됨", "역할별 작업 공간에서 충돌과 책임을 분리한다.", CYAN),
            ("검증이 연결됨", "코드·테스트·플레이 결과를 같은 카드에 남긴다.", VIOLET),
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
        5,
        "DISCORD BOT",
        "사람의 요청을 실행 가능한 AI 작업으로 바꾼다",
        "자연어 요청은 Planning·역할 배정·리뷰·승인으로 변환되어 추적 가능한 개발 흐름이 된다.",
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
        "DISCORD BOT",
        "목표와 완료 조건을 확인했습니다.\nPlanning 카드로 분해합니다.\n상태: PLANNING",
        fill=white,
        accent=LIME_DARK,
    )
    message_bubble(
        c,
        M + 18,
        392,
        248,
        84,
        "DISCORD BOT",
        "Planning 결과를 Tech·Art Agent에 배정했습니다.\n각 작업은 격리된 공간에서 진행합니다.\n결과는 REVIEW에서 확인합니다.",
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
        "DISCORD BOT",
        "검증을 통과했습니다.\n변경과 증거를 기록하고, 결정·절차를 AnchorMind에 공유합니다.",
        fill=SOFT_LIME,
        accent=LIME_DARK,
    )

    side_x = M + 332
    side_width = CW - 332
    capabilities = [
        ("요청", "목표·완료 조건 접수", CYAN, SOFT_CYAN),
        ("Planning", "카드·의존성 분해", LIME, SOFT_LIME),
        ("진행", "담당·작업 공간 확인", CYAN, SOFT_CYAN),
        ("리뷰", "테스트·증거 검토", VIOLET, SOFT_VIOLET),
        ("승인", "사람의 최종 승인", YELLOW, SOFT_YELLOW),
    ]
    for index, (title, body, accent, fill) in enumerate(capabilities):
        y = 602 - index * 83
        accent_card(c, side_x, y, side_width, 70, title, body, accent, fill, title_size=8.5, body_size=6.4)

    benefit_cards(
        c,
        [
            ("자연어로 시작", "사람은 목표와 의도를 대화로 전달한다.", CYAN),
            ("구조화되어 추적", "카드·담당·상태·증거가 한 흐름에 남는다.", LIME),
            ("사람이 승인", "AI 결과는 리뷰와 실제 플레이 뒤 반영한다.", VIOLET),
        ],
        y=66,
        height=92,
    )
    c.showPage()


def draw_verification(c: canvas.Canvas) -> None:
    section_header(
        c,
        6,
        "EVIDENCE & CONTROL",
        "AI 결과를 증거와 인간 판단으로 닫는다",
        "자동 검증은 반복 가능한 사실을 확인하고, 사람은 게임의 감각과 방향을 최종 승인한다.",
    )

    rounded_box(c, M, 470, CW, 205, fill=NAVY, stroke=None, radius=13)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(M + 15, 646, "검증 루프")

    checks = [
        ("변경", "Git diff\n격리된 작업 공간", CYAN, SOFT_CYAN),
        ("자동 검증", "compile · test\n정량 검산", LIME, SOFT_LIME),
        ("독립 QA", "증거 확인\n완료 조건 검토", VIOLET, SOFT_VIOLET),
        ("인간 승인", "실제 플레이\n방향·품질 판단", YELLOW, SOFT_YELLOW),
    ]
    check_gap = 11
    check_width = (CW - 30 - check_gap * 3) / 4
    for index, (title, body, accent, fill) in enumerate(checks):
        x = M + 15 + index * (check_width + check_gap)
        rounded_box(c, x, 525, check_width, 82, fill=fill, stroke=None, radius=9)
        c.setFont("KR-Bold", 8.2)
        c.setFillColor(INK)
        c.drawCentredString(x + check_width / 2, 582, title)
        draw_text(
            c,
            body,
            x + 8,
            562,
            check_width - 16,
            size=6.7,
            color=MUTED,
            leading=9,
            align="center",
            max_lines=3,
        )
        if index < 3:
            arrow(c, x + check_width + 2, 566, x + check_width + check_gap - 2, 566, color=accent, head=4)

    c.setFont("KR", 7.2)
    c.setFillColor(HexColor("#C0D2DF"))
    draw_text(
        c,
        "완료 선언이 아니라 변경·검증·판정의 흔적을 남기는 것이 AI 활용 구조의 종료 조건이다.",
        M + 24,
        500,
        CW - 48,
        size=7.2,
        color=HexColor("#C0D2DF"),
        align="center",
        max_lines=1,
    )

    c.setFont("KR-Bold", 9)
    c.setFillColor(INK)
    c.drawString(M, 438, "실제 작업에서 분리한 책임")
    widths = [115, 245, 151]
    headers = ["질문", "남기는 증거", "판단 주체"]
    table_top = 418
    c.setFillColor(NAVY)
    c.roundRect(M, table_top - 27, CW, 27, 8, fill=1, stroke=0)
    xx = M
    for width, header in zip(widths, headers):
        draw_text(c, header, xx + 8, table_top - 11, width - 16, font="KR-Bold", size=7.1, color=white, max_lines=1)
        xx += width

    rows = [
        ("변경이 의도한 범위인가?", "카드·완료 조건·Git diff", "Planning · Human"),
        ("구현이 실제로 동작하는가?", "compile · EditMode/PlayMode · raw evidence", "Tech · QA"),
        ("게임으로서 괜찮은가?", "실제 플레이 · 시각 검토 · 방향 피드백", "Human"),
        ("다음 작업이 안전한가?", "결정·오류·절차의 기억 기록", "AnchorMind"),
    ]
    y = table_top - 27
    for index, row in enumerate(rows):
        row_h = 42
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
                xx + 8,
                y - 16,
                width - 16,
                font="KR-Bold" if col == 0 else "KR",
                size=6.7,
                color=INK if col == 0 else MUTED,
                leading=9,
                max_lines=2,
            )
            xx += width
        y -= row_h
    c.setStrokeColor(LINE)
    c.roundRect(M, y, CW, table_top - y, 8, fill=0, stroke=1)

    benefit_cards(
        c,
        [
            ("UI 변경", "아트 자산·UGUI 연결·회귀 테스트·실제 플레이를 한 흐름으로 확인한다.", CYAN),
            ("씬·배치 변경", "기획 도안·기술 재빌드·정량 검산·시각 검토를 분리한다.", LIME),
            ("네트워크 작업", "host-client 범위와 후속 Relay/Lobby를 분리해 완료 기준을 고정한다.", VIOLET),
        ],
        y=66,
        height=91,
    )
    c.showPage()


def draw_memory(c: canvas.Canvas) -> None:
    section_header(
        c,
        7,
        "ANCHORMIND",
        "AI가 같은 기준으로 이어서 일하도록 기억을 공유한다",
        "작업 문맥과 장기 지식을 분리해 저장하고, 다음 역할이 검증된 기준을 이어받는다.",
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
        "개발 기억  |  장기",
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
    c.drawCentredString(W / 2, 466, "개발 흐름의 공용 기억 저장소")

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
    c.drawCentredString(W / 2, 133, "AI는 같은 기준으로 일하고, 사람은 방향과 품질을 통제한다.")
    c.setFont("KR-Bold", 10)
    c.setFillColor(INK)
    c.drawCentredString(W / 2, 101, "사람이 의도를 정하고, Planning이 쪼개고, 역할이 구현하고, 증거와 기억이 다음 작업을 잇는다.")
    c.showPage()


def flat_panel(
    c: canvas.Canvas,
    x: float,
    y: float,
    width: float,
    height: float,
    *,
    fill: Color = white,
    stroke: Color | None = LINE,
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
    c.rect(x, y, width, height, fill=1, stroke=stroke_flag)
    c.restoreState()


def section_header_v3(
    c: canvas.Canvas,
    page: int,
    kicker: str,
    title: str,
    subtitle: str,
) -> None:
    c.setFillColor(PAPER)
    c.rect(0, 0, W, H, fill=1, stroke=0)
    c.setStrokeColor(INK)
    c.setLineWidth(0.8)
    c.line(M, H - 48, W - M, H - 48)
    c.setFont("KR-Bold", 7.5)
    c.setFillColor(MUTED)
    c.drawString(M, H - 35, f"{page - 1:02d}  /  {safe_text(kicker)}")
    c.setFillColor(LIME)
    c.rect(W - M - 8, H - 40, 8, 8, fill=1, stroke=0)
    title_size = fit_single_line(title, 28, 18, CW)
    c.setFont("KR-Bold", title_size)
    c.setFillColor(INK)
    c.drawString(M, H - 100, safe_text(title))
    draw_text(c, subtitle, M, H - 120, CW, size=8.7, color=MUTED, max_lines=2)
    c.setStrokeColor(LINE)
    c.setLineWidth(0.8)
    c.line(M, 42, W - M, 42)
    c.setFont("KR", 7.2)
    c.setFillColor(MUTED)
    c.drawString(M, 25, "AI Game Development Workflow")
    c.setFont("KR-Bold", 8)
    c.drawRightString(W - M, 25, f"{page:02d} / {PAGE_COUNT:02d}")
    key = f"page-{page}"
    c.bookmarkPage(key)
    c.addOutlineEntry(title, key, level=0, closed=False)


def draw_rule_label(
    c: canvas.Canvas,
    x: float,
    y: float,
    number: str,
    title: str,
    body: str,
    accent: Color,
    width: float,
) -> None:
    c.setStrokeColor(LINE)
    c.setLineWidth(0.7)
    c.line(x, y, x + width, y)
    c.setFillColor(accent)
    c.rect(x, y - 4, 28, 4, fill=1, stroke=0)
    c.setFont("KR-Bold", 7.2)
    c.setFillColor(MUTED)
    c.drawString(x, y - 22, number)
    c.setFont("KR-Bold", 9.2)
    c.setFillColor(INK)
    c.drawString(x + 30, y - 22, title)
    draw_text(c, body, x, y - 42, width, size=6.9, color=MUTED, leading=9.5, max_lines=2)


def draw_node_v3(
    c: canvas.Canvas,
    x: float,
    y: float,
    width: float,
    height: float,
    number: str,
    title: str,
    body: str,
    accent: Color,
    *,
    dark: bool = False,
) -> None:
    fill = NAVY if dark else white
    stroke = None if dark else LINE
    flat_panel(c, x, y, width, height, fill=fill, stroke=stroke)
    c.setFillColor(accent)
    c.rect(x, y + height - 5, width, 5, fill=1, stroke=0)
    c.setFont("KR-Bold", 7)
    c.setFillColor(accent if dark else MUTED)
    c.drawString(x + 12, y + height - 22, number)
    c.setFont("KR-Bold", 9.2)
    c.setFillColor(white if dark else INK)
    c.drawString(x + 12, y + height - 42, title)
    draw_text(
        c,
        body,
        x + 12,
        y + height - 64,
        width - 24,
        size=6.8,
        color=HexColor("#C7D0CA") if dark else MUTED,
        leading=9,
        max_lines=3,
    )


def draw_cover_v3(c: canvas.Canvas) -> None:
    c.setFillColor(PAPER)
    c.rect(0, 0, W, H, fill=1, stroke=0)
    c.setFillColor(LIME)
    c.rect(0, H - 12, W, 12, fill=1, stroke=0)
    c.setFillColor(INK)
    c.rect(M, H / 2 - 115, 8, 230, fill=1, stroke=0)
    c.setFont("KR-Bold", 38)
    c.drawString(M + 28, H / 2 + 30, "AI 활용을 위한")
    c.setFont("KR-Bold", 42)
    c.drawString(M + 28, H / 2 - 28, "게임 개발 구조화")
    c.setFillColor(LIME)
    c.rect(M + 28, H / 2 - 60, 184, 7, fill=1, stroke=0)
    c.setStrokeColor(LINE)
    c.setLineWidth(0.7)
    c.line(M + 28, H / 2 - 108, W - M, H / 2 - 108)
    c.bookmarkPage("page-1")
    c.addOutlineEntry("표지", "page-1", level=0, closed=False)
    c.showPage()


def draw_why_structure_v3(c: canvas.Canvas) -> None:
    section_header_v3(
        c,
        2,
        "WHY STRUCTURE",
        "AI 활용을 위해 먼저 개발 구조를 설계한다",
        "생성 속도를 높이는 것보다, 무엇을 생성해도 흔들리지 않는 경계를 먼저 만든다.",
    )

    flat_panel(c, M, 446, 305, 190, fill=NAVY, stroke=None)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(M + 18, 610, "핵심 전환")
    c.setFont("KR-Bold", 21)
    c.setFillColor(white)
    draw_text(
        c,
        "AI를 한 명의\n개발자로 보지 않는다.",
        M + 18,
        568,
        265,
        font="KR-Bold",
        size=21,
        color=white,
        leading=28,
        max_lines=3,
    )
    draw_text(
        c,
        "명확한 경계와 검증 가능한 인수인계를 따라 일하는 역할 집합으로 사용한다.",
        M + 18,
        500,
        265,
        size=7.3,
        color=HexColor("#C7D0CA"),
        leading=10,
        max_lines=3,
    )

    items = [
        ("01", "모호한 요청", "Planning Agent", "목표·범위·완료 조건을 카드로 고정한다.", LIME),
        ("02", "큰 작업과 충돌", "역할·격리", "코드·아트·씬·테스트의 변경 경계를 나눈다.", CYAN),
        ("03", "컨텍스트 단절", "AnchorMind", "결정·오류·절차를 다음 역할에 넘긴다.", VIOLET),
        ("04", "완료를 믿기 어려움", "증거·QA·승인", "자동 검증과 사람의 최종 판단을 연결한다.", YELLOW),
    ]
    x = M + 332
    for index, (number, title, response, body, accent) in enumerate(items):
        y = 620 - index * 66
        draw_rule_label(c, x, y, number, title, f"{response}  |  {body}", accent, CW - 332)

    c.setFont("KR-Bold", 8)
    c.setFillColor(INK)
    c.drawString(M, 389, "구조가 고정하는 순서")
    sequence = ["의도", "범위", "역할", "증거", "승인", "기억"]
    seq_width = (CW - 25) / len(sequence)
    for index, label in enumerate(sequence):
        x = M + index * seq_width
        c.setFont("KR-Bold", 8.8)
        c.setFillColor(INK)
        c.drawString(x, 355, label)
        c.setStrokeColor(LIME if index < 3 else CYAN)
        c.setLineWidth(2.4)
        c.line(x, 340, x + seq_width - 12, 340)
        if index < len(sequence) - 1:
            arrow(c, x + seq_width - 8, 347, x + seq_width + 2, 347, color=MUTED, width=0.9, head=3.2)

    flat_panel(c, M, 164, CW, 120, fill=white, stroke=LINE)
    c.setFont("KR-Bold", 8)
    c.setFillColor(LIME_DARK)
    c.drawString(M + 16, 260, "결과")
    c.setFont("KR-Bold", 15)
    c.setFillColor(INK)
    c.drawString(M + 16, 224, "빠르게 만드는 구조가 아니라,")
    c.drawString(M + 16, 198, "AI를 안전하게 활용할 수 있는 구조를 만든다.")
    c.setFont("KR", 7)
    c.setFillColor(MUTED)
    c.drawRightString(W - M - 16, 181, "자동화는 속도를 높이고, 구조는 방향을 지킨다.")
    c.showPage()


def draw_overview_v3(c: canvas.Canvas) -> None:
    section_header_v3(
        c,
        3,
        "SYSTEM MAP",
        "사람의 의도가 작업 단위와 증거로 변환되는 경로",
        "각 구성요소는 한 가지 책임만 맡고, 결과는 다음 단계가 확인할 수 있는 형태로 남긴다.",
    )

    c.setFont("KR-Bold", 8)
    c.setFillColor(MUTED)
    c.drawString(M, 640, "REQUEST  ->  EXECUTION  ->  EVIDENCE")
    nodes = [
        ("01", "Discord Bot", "의도·승인", CYAN),
        ("02", "Planning Agent", "목표·분해", LIME),
        ("03", "AgentDesk", "역할·격리", CYAN),
        ("04", "Kanban", "상태·리뷰", VIOLET),
    ]
    node_gap = 10
    node_width = (CW - node_gap * 3) / 4
    node_y = 510
    for index, (number, title, body, accent) in enumerate(nodes):
        x = M + index * (node_width + node_gap)
        draw_node_v3(c, x, node_y, node_width, 104, number, title, body, accent)
        if index < len(nodes) - 1:
            arrow(c, x + node_width + 2, node_y + 52, x + node_width + node_gap - 2, node_y + 52, color=accent, head=4)

    c.setStrokeColor(LIME)
    c.setLineWidth(1.5)
    center_x = M + 2.5 * node_width + 2 * node_gap
    c.line(center_x, node_y, center_x, 470)
    c.line(M + 68, 470, W - M - 68, 470)
    roles = [
        ("Planning Agent", "계획·카드 분해"),
        ("Tech", "구현·기술 판단"),
        ("Art", "자산·시각 품질"),
        ("Review", "검토·완료 판정"),
    ]
    role_width = (CW - 50) / 4
    for index, (title, body) in enumerate(roles):
        x = M + 12 + index * (role_width + 8)
        c.line(x + role_width / 2, 470, x + role_width / 2, 445)
        c.setFont("KR-Bold", 8.8)
        c.setFillColor(INK)
        c.drawCentredString(x + role_width / 2, 422, title)
        c.setFont("KR", 6.8)
        c.setFillColor(MUTED)
        c.drawCentredString(x + role_width / 2, 404, body)
        c.setStrokeColor(LINE)
        c.line(x, 388, x + role_width, 388)

    c.setFont("KR-Bold", 8)
    c.setFillColor(INK)
    c.drawString(M, 345, "개발 중 보이는 결과")
    artifacts = [
        ("카드", "목표·완료 조건·상태"),
        ("작업 공간", "담당 역할·변경 경계"),
        ("검증 기록", "compile·test·실제 플레이"),
        ("기억", "결정·오류·절차"),
    ]
    artifact_width = (CW - 24) / 4
    for index, (title, body) in enumerate(artifacts):
        x = M + index * (artifact_width + 8)
        c.setFont("KR-Bold", 9)
        c.setFillColor(LIME_DARK if index == 0 else INK)
        c.drawString(x, 313, title)
        draw_text(c, body, x, 293, artifact_width, size=7, color=MUTED, leading=10, max_lines=2)
        c.setStrokeColor(LINE)
        c.line(x, 271, x + artifact_width, 271)

    flat_panel(c, M, 154, CW, 78, fill=NAVY, stroke=None)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(M + 16, 207, "핵심 원칙")
    c.setFont("KR-Bold", 12)
    c.setFillColor(white)
    c.drawString(M + 16, 180, "AI의 결과는 다음 역할이 읽고, 확인하고, 이어서 쓸 수 있어야 한다.")
    c.showPage()


def draw_agentdesk_v3(c: canvas.Canvas) -> None:
    section_header_v3(
        c,
        4,
        "ROLE BOUNDARIES",
        "역할을 나누는 이유는 책임을 선명하게 만들기 위해서다",
        "한 에이전트가 모든 것을 바꾸는 대신, 각 역할이 담당 범위와 검증 결과를 분리해 남긴다.",
    )

    c.setFont("KR-Bold", 8)
    c.setFillColor(MUTED)
    c.drawString(M, 641, "ONE TASK  /  FOUR RESPONSIBILITIES")
    roles = [
        ("01", "Planning Agent", "목표를 카드로 바꾸고 우선순위·의존성·완료 조건을 정한다.", LIME),
        ("02", "Tech Agent", "격리된 공간에서 구현하고 변경·검증 결과를 남긴다.", CYAN),
        ("03", "Art Agent", "자산을 만들고 적용 경로와 시각 품질 기준을 확인한다.", VIOLET),
        ("04", "Reviewer / QA", "구현자와 분리된 관점에서 증거와 완료 조건을 검토한다.", YELLOW),
    ]
    left_x = M
    for index, (number, title, body, accent) in enumerate(roles):
        y = 574 - index * 92
        c.setFillColor(accent)
        c.rect(left_x, y, 5, 62, fill=1, stroke=0)
        c.setFont("KR-Bold", 7.2)
        c.setFillColor(MUTED)
        c.drawString(left_x + 16, y + 45, number)
        c.setFont("KR-Bold", 10)
        c.setFillColor(INK)
        c.drawString(left_x + 46, y + 44, title)
        draw_text(c, body, left_x + 46, y + 25, 232, size=7, color=MUTED, leading=10, max_lines=2)
        c.setStrokeColor(LINE)
        c.line(left_x, y - 10, left_x + 278, y - 10)

    right_x = M + 315
    flat_panel(c, right_x, 302, CW - 315, 290, fill=NAVY, stroke=None)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(right_x + 16, 565, "칸반은 상태 머신이다")
    states = [
        ("BACKLOG", "요청 대기"),
        ("PLANNING", "계획·분해"),
        ("READY", "조건 확인"),
        ("DOING", "작업 중"),
        ("REVIEW", "증거 검토"),
        ("DONE", "완료"),
    ]
    for index, (title, body) in enumerate(states):
        y = 520 - index * 34
        c.setFillColor(LIME if index in (1, 4) else HexColor("#53655A"))
        c.circle(right_x + 25, y + 2, 4, fill=1, stroke=0)
        c.setFont("KR-Bold", 7.2)
        c.setFillColor(white)
        c.drawString(right_x + 40, y, title)
        c.setFont("KR", 6.7)
        c.setFillColor(HexColor("#C7D0CA"))
        c.drawRightString(W - M - 16, y, body)
        if index < len(states) - 1:
            c.setStrokeColor(HexColor("#53655A"))
            c.setLineWidth(0.8)
            c.line(right_x + 25, y - 4, right_x + 25, y - 26)
    c.setFont("KR", 6.7)
    c.setFillColor(RED)
    c.drawString(right_x + 16, 319, "REVIEW -> DOING  /  보완이 필요한 경우")

    c.setFont("KR-Bold", 8)
    c.setFillColor(INK)
    c.drawString(M, 173, "역할 분리가 만드는 효과")
    effects = [
        ("작업 단위", "AI가 처리할 범위와 완료 조건을 먼저 고정한다.", LIME),
        ("변경 경계", "역할별 공간에서 충돌과 책임을 분리한다.", CYAN),
        ("검증 연결", "코드·테스트·플레이 결과를 같은 카드에 남긴다.", VIOLET),
    ]
    effect_width = (CW - 18) / 3
    for index, (title, body, accent) in enumerate(effects):
        x = M + index * (effect_width + 9)
        c.setFillColor(accent)
        c.rect(x, 146, 28, 4, fill=1, stroke=0)
        c.setFont("KR-Bold", 8.8)
        c.setFillColor(INK)
        c.drawString(x, 125, title)
        draw_text(c, body, x, 105, effect_width, size=6.8, color=MUTED, leading=9.5, max_lines=3)
    c.showPage()


def draw_discord_v3(c: canvas.Canvas) -> None:
    section_header_v3(
        c,
        5,
        "DISCORD BOT",
        "대화를 추적 가능한 작업으로 바꾼다",
        "사람은 자연어로 시작하고, 시스템은 목표·담당·상태·증거가 남는 개발 흐름으로 변환한다.",
    )

    c.setFont("KR-Bold", 8)
    c.setFillColor(MUTED)
    c.drawString(M, 639, "A REQUEST BECOMES A TRACE")
    left_width = 327
    flat_panel(c, M, 192, left_width, 410, fill=NAVY, stroke=None)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(M + 16, 573, "대화에서 작업으로")
    messages = [
        ("01", "USER", "새 기능 개발을 시작해줘.", SOFT_CYAN, HexColor("#167F99")),
        ("02", "DISCORD BOT", "목표와 완료 조건을 확인했습니다.\nPlanning 카드로 분해합니다.", white, LIME_DARK),
        ("03", "DISCORD BOT", "Planning 결과를 Tech·Art Agent에 배정했습니다.\n각 작업은 격리된 공간에서 진행합니다.", white, LIME_DARK),
        ("04", "USER", "확인했어. 승인할게.", SOFT_VIOLET, HexColor("#6757BE")),
        ("05", "DISCORD BOT", "검증을 통과했습니다.\n변경과 증거를 기록하고 기억에 공유합니다.", SOFT_LIME, LIME_DARK),
    ]
    for index, (number, sender, body, fill, accent) in enumerate(messages):
        y = 518 - index * 72
        c.setFillColor(accent)
        c.circle(M + 22, y + 20, 4, fill=1, stroke=0)
        c.setStrokeColor(HexColor("#53655A"))
        if index < len(messages) - 1:
            c.setLineWidth(0.8)
            c.line(M + 22, y + 16, M + 22, y - 46)
        flat_panel(c, M + 40, y - 8, 260 if sender != "USER" else 215, 50 if index not in (1, 2) else 57, fill=fill, stroke=None)
        c.setFont("KR-Bold", 6.6)
        c.setFillColor(accent)
        c.drawString(M + 52, y + 26, f"{number}  {sender}")
        draw_text(c, body, M + 52, y + 9, 234 if sender != "USER" else 189, size=6.8, color=INK, leading=9.2, max_lines=3)

    right_x = M + left_width + 27
    c.setFont("KR-Bold", 8)
    c.setFillColor(INK)
    c.drawString(right_x, 573, "변환되는 책임")
    transform = [
        ("사람의 문장", "목표·완료 조건", CYAN),
        ("Planning", "카드·의존성", LIME),
        ("AgentDesk", "담당·격리 공간", CYAN),
        ("Review", "테스트·증거", VIOLET),
        ("승인", "사람의 최종 판단", YELLOW),
    ]
    for index, (title, body, accent) in enumerate(transform):
        y = 518 - index * 67
        c.setFillColor(accent)
        c.rect(right_x, y + 12, 6, 35, fill=1, stroke=0)
        c.setFont("KR-Bold", 9)
        c.setFillColor(INK)
        c.drawString(right_x + 17, y + 33, title)
        c.setFont("KR", 7)
        c.setFillColor(MUTED)
        c.drawString(right_x + 17, y + 16, body)
        if index < len(transform) - 1:
            c.setStrokeColor(LINE)
            c.setLineWidth(0.8)
            c.line(right_x + 3, y + 6, right_x + 3, y - 48)
    flat_panel(c, right_x, 192, CW - left_width - 27, 70, fill=white, stroke=LINE)
    c.setFont("KR-Bold", 8.5)
    c.setFillColor(LIME_DARK)
    c.drawString(right_x + 14, 237, "Discord Bot의 역할")
    draw_text(c, "요청을 접수하고, 상태를 알리고, 승인과 증거를 같은 흐름에 묶는다.", right_x + 14, 217, CW - left_width - 55, size=6.8, color=MUTED, leading=9.5, max_lines=3)
    c.showPage()


def draw_verification_v3(c: canvas.Canvas) -> None:
    section_header_v3(
        c,
        6,
        "EVIDENCE & CONTROL",
        "AI 결과는 증거와 인간 판단으로 닫힌다",
        "반복 가능한 사실은 자동으로 확인하고, 게임의 감각과 방향은 사람이 최종 승인한다.",
    )

    flat_panel(c, M, 425, CW, 190, fill=NAVY, stroke=None)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(M + 16, 585, "검증 루프")
    checks = [
        ("01", "변경", "Git diff\n격리된 작업 공간", CYAN),
        ("02", "자동 검증", "compile · test\n정량 검산", LIME),
        ("03", "독립 QA", "증거 확인\n완료 조건 검토", VIOLET),
        ("04", "인간 승인", "실제 플레이\n방향·품질 판단", YELLOW),
    ]
    check_gap = 11
    check_width = (CW - 30 - check_gap * 3) / 4
    for index, (number, title, body, accent) in enumerate(checks):
        x = M + 15 + index * (check_width + check_gap)
        draw_node_v3(c, x, 477, check_width, 78, number, title, body, accent)
        if index < 3:
            arrow(c, x + check_width + 2, 516, x + check_width + check_gap - 2, 516, color=accent, head=4)
    c.setFont("KR", 7)
    c.setFillColor(HexColor("#C7D0CA"))
    c.drawCentredString(W / 2, 447, "완료 선언이 아니라 변경·검증·판정의 흔적을 남기는 것이 종료 조건이다.")

    c.setFont("KR-Bold", 8)
    c.setFillColor(INK)
    c.drawString(M, 383, "실제 작업에서 분리한 질문")
    questions = [
        ("변경이 의도한 범위인가?", "카드·완료 조건·Git diff", "Planning · Human"),
        ("구현이 실제로 동작하는가?", "compile · EditMode/PlayMode · raw evidence", "Tech · QA"),
        ("게임으로서 괜찮은가?", "실제 플레이 · 시각 검토 · 방향 피드백", "Human"),
    ]
    for index, (question, evidence, owner) in enumerate(questions):
        y = 338 - index * 60
        c.setStrokeColor(LINE)
        c.setLineWidth(0.7)
        c.line(M, y, W - M, y)
        c.setFont("KR-Bold", 8.5)
        c.setFillColor(INK)
        c.drawString(M, y - 24, question)
        c.setFont("KR", 7)
        c.setFillColor(MUTED)
        c.drawString(M + 185, y - 24, evidence)
        c.setFont("KR-Bold", 7)
        c.setFillColor(LIME_DARK)
        c.drawRightString(W - M, y - 24, owner)
    c.setStrokeColor(LINE)
    c.line(M, 158, W - M, 158)

    c.setFont("KR-Bold", 8)
    c.setFillColor(INK)
    c.drawString(M, 130, "실제 작업에 적용한 방식")
    examples = [
        ("UI", "아트 자산·UGUI 연결·회귀 테스트·실제 플레이", CYAN),
        ("씬·배치", "기획 도안·기술 재빌드·정량 검산·시각 검토", LIME),
        ("네트워크", "host-client 범위와 후속 Relay/Lobby를 분리", VIOLET),
    ]
    for index, (title, body, accent) in enumerate(examples):
        x = M + index * ((CW + 10) / 3)
        c.setFillColor(accent)
        c.rect(x, 96, 28, 4, fill=1, stroke=0)
        c.setFont("KR-Bold", 9)
        c.setFillColor(INK)
        c.drawString(x, 78, title)
        draw_text(c, body, x, 59, CW / 3 - 14, size=6.7, color=MUTED, leading=9, max_lines=3)
    c.showPage()


def draw_memory_v3(c: canvas.Canvas) -> None:
    section_header_v3(
        c,
        7,
        "ANCHORMIND",
        "다음 역할이 같은 기준으로 이어서 일하도록 기억을 공유한다",
        "현재 카드의 문맥과 팀의 장기 지식을 분리해 저장하고, 검증된 기준을 다음 작업으로 넘긴다.",
    )

    c.setFont("KR-Bold", 8)
    c.setFillColor(MUTED)
    c.drawString(M, 636, "TWO TYPES OF MEMORY  /  ONE SHARED CONTEXT")
    column_gap = 22
    column_width = (CW - column_gap) / 2
    streams = [
        ("작업 기억  |  단기", "현재 카드의 목표, 진행 상태, 남은 문제, 다음 역할에 넘길 내용을 저장한다.", CYAN),
        ("개발 기억  |  장기", "확정된 결정, 팀 규칙, 해결한 오류, 반복해서 사용할 절차를 보존한다.", VIOLET),
    ]
    for index, (title, body, accent) in enumerate(streams):
        x = M + index * (column_width + column_gap)
        c.setFillColor(accent)
        c.rect(x, 566, 36, 5, fill=1, stroke=0)
        c.setFont("KR-Bold", 11)
        c.setFillColor(INK)
        c.drawString(x, 535, title)
        draw_text(c, body, x, 505, column_width, size=7.3, color=MUTED, leading=10, max_lines=3)
        c.setStrokeColor(LINE)
        c.line(x, 468, x + column_width, 468)

    flat_panel(c, M + 76, 354, CW - 152, 76, fill=NAVY, stroke=None)
    c.setFont("KR-Bold", 14)
    c.setFillColor(LIME)
    c.drawCentredString(W / 2, 395, "AnchorMind")
    c.setFont("KR", 7.2)
    c.setFillColor(white)
    c.drawCentredString(W / 2, 374, "개발 흐름의 공용 기억 저장소")
    arrow(c, M + column_width / 2, 468, W / 2 - 55, 430, color=CYAN)
    arrow(c, M + column_width + column_gap + column_width / 2, 468, W / 2 + 55, 430, color=VIOLET)

    c.setStrokeColor(LIME)
    c.setLineWidth(1.4)
    c.line(W / 2, 354, W / 2, 320)
    c.line(M + 58, 320, W - M - 58, 320)
    agents = ["Planning Agent", "Tech", "Art", "Review"]
    agent_width = (CW - 72) / 4
    for index, title in enumerate(agents):
        x = M + 18 + index * (agent_width + 12)
        c.line(x + agent_width / 2, 320, x + agent_width / 2, 295)
        c.setFont("KR-Bold", 8.2)
        c.setFillColor(INK)
        c.drawCentredString(x + agent_width / 2, 276, title)
        c.setStrokeColor(LINE)
        c.line(x, 261, x + agent_width, 261)

    c.setFont("KR-Bold", 8)
    c.setFillColor(INK)
    c.drawString(M, 222, "기억이 이어지는 순서")
    cycle = [
        ("01", "회상", "관련 결정과 절차를 불러온다.", CYAN),
        ("02", "작업", "같은 문맥으로 맡은 일을 수행한다.", LIME),
        ("03", "저장", "확인된 결과와 해결 절차를 남긴다.", VIOLET),
        ("04", "공유", "다음 역할이 같은 기준을 이어받는다.", YELLOW),
    ]
    cycle_width = (CW - 24) / 4
    for index, (number, title, body, accent) in enumerate(cycle):
        x = M + index * (cycle_width + 8)
        c.setFillColor(accent)
        c.rect(x, 179, 24, 4, fill=1, stroke=0)
        c.setFont("KR-Bold", 7)
        c.setFillColor(MUTED)
        c.drawString(x, 159, number)
        c.setFont("KR-Bold", 9)
        c.setFillColor(INK)
        c.drawString(x + 25, 159, title)
        draw_text(c, body, x, 139, cycle_width, size=6.6, color=MUTED, leading=9, max_lines=2)
        if index < 3:
            arrow(c, x + cycle_width - 4, 163, x + cycle_width + 4, 163, color=MUTED, width=0.9, head=3.2)

    flat_panel(c, M, 67, CW, 45, fill=SOFT_LIME, stroke=None)
    c.setFont("KR-Bold", 9.5)
    c.setFillColor(LIME_DARK)
    c.drawCentredString(W / 2, 86, "사람이 의도를 정하고, 역할이 구현하고, 증거와 기억이 다음 작업을 잇는다.")
    c.showPage()


def build_pdf() -> Path:
    register_fonts()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(OUTPUT), pagesize=A4, pageCompression=1)
    c.setTitle("AI 기반 게임 개발 작업 체계")
    c.setAuthor("AI Game Development Workflow")
    c.setSubject("Planning Agent, Discord Bot, AgentDesk, AnchorMind 기반의 구조적 AI 게임 개발 흐름")
    c.setKeywords("Planning Agent, Discord Bot, AgentDesk, AnchorMind, Kanban, AI game development")
    c.setCreator("Reproducible ReportLab generator")

    draw_cover_v3(c)
    draw_why_structure_v3(c)
    draw_overview_v3(c)
    draw_agentdesk_v3(c)
    draw_discord_v3(c)
    draw_verification_v3(c)
    draw_memory_v3(c)

    c.save()
    return OUTPUT


if __name__ == "__main__":
    result = build_pdf()
    print(result)
