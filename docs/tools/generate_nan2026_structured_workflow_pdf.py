"""Generate the finalized NAN2026 AI game-development workflow report."""

from __future__ import annotations

from pathlib import Path

from reportlab.lib.colors import Color, HexColor, white
from reportlab.lib.pagesizes import A4
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "output" / "pdf" / "nan2026-ai-game-development-workflow.pdf"
ASSET = ROOT / "docs" / "evidence" / "nan2026"
REDACTED = ASSET / "redacted"

FONT_REGULAR = Path(r"C:\Windows\Fonts\malgun.ttf")
FONT_BOLD = Path(r"C:\Windows\Fonts\malgunbd.ttf")

W, H = A4
M = 42
CW = W - M * 2
PAGE_COUNT = 13

PAPER = HexColor("#F5F4EE")
PAPER_ALT = HexColor("#EBEEE8")
INK = HexColor("#151A18")
MUTED = HexColor("#66706B")
LINE = HexColor("#C8CEC7")
NAVY = HexColor("#111715")
NAVY_2 = HexColor("#1D2823")
LIME = HexColor("#B9F258")
LIME_DARK = HexColor("#426C20")
CYAN = HexColor("#6FCBD0")
VIOLET = HexColor("#AA98EA")
YELLOW = HexColor("#EEC86D")
RED = HexColor("#E4776F")
SOFT_LIME = HexColor("#E6F5D3")
SOFT_CYAN = HexColor("#E0F2F1")
SOFT_VIOLET = HexColor("#EDE9F8")
SOFT_YELLOW = HexColor("#F8EECF")


def register_fonts() -> None:
    pdfmetrics.registerFont(TTFont("KR", str(FONT_REGULAR)))
    pdfmetrics.registerFont(TTFont("KR-Bold", str(FONT_BOLD)))


def safe(text: str) -> str:
    return text.replace("\u2011", "-").replace("\u2012", "-").replace("\u2013", "-").replace("\u2014", "-")


def tw(text: str, font: str, size: float) -> float:
    return pdfmetrics.stringWidth(safe(text), font, size)


def wrap(text: str, width: float, font: str, size: float) -> list[str]:
    result: list[str] = []
    for para in safe(text).splitlines() or [""]:
        if not para:
            result.append("")
            continue
        line = ""
        for token in para.split(" "):
            candidate = token if not line else f"{line} {token}"
            if line and tw(candidate, font, size) > width:
                result.append(line)
                line = token
            elif tw(token, font, size) > width:
                if line:
                    result.append(line)
                    line = ""
                piece = ""
                for char in token:
                    if piece and tw(piece + char, font, size) > width:
                        result.append(piece)
                        piece = char
                    else:
                        piece += char
                line = piece
            else:
                line = candidate
        if line:
            result.append(line)
    return result


def text(
    c: canvas.Canvas,
    value: str,
    x: float,
    y: float,
    width: float,
    *,
    font: str = "KR",
    size: float = 8,
    color: Color = INK,
    leading: float | None = None,
    max_lines: int | None = None,
    align: str = "left",
) -> float:
    leading = leading or size * 1.48
    lines = wrap(value, width, font, size)
    if max_lines and len(lines) > max_lines:
        lines = lines[:max_lines]
        last = lines[-1]
        while last and tw(last + "...", font, size) > width:
            last = last[:-1]
        lines[-1] = last + "..."
    c.setFont(font, size)
    c.setFillColor(color)
    yy = y
    for line in lines:
        if align == "center":
            c.drawCentredString(x + width / 2, yy, line)
        elif align == "right":
            c.drawRightString(x + width, yy, line)
        else:
            c.drawString(x, yy, line)
        yy -= leading
    return yy


def box(c: canvas.Canvas, x: float, y: float, w: float, h: float, fill: Color = white, stroke: Color | None = LINE) -> None:
    c.saveState()
    c.setFillColor(fill)
    if stroke is None:
        c.setStrokeColor(fill)
        flag = 0
    else:
        c.setStrokeColor(stroke)
        flag = 1
    c.setLineWidth(0.7)
    c.roundRect(x, y, w, h, 6, fill=1, stroke=flag)
    c.restoreState()


def divider(c: canvas.Canvas, x: float, y: float, w: float, color: Color = LINE) -> None:
    c.setStrokeColor(color)
    c.setLineWidth(0.8)
    c.line(x, y, x + w, y)


def arrow(c: canvas.Canvas, x1: float, y1: float, x2: float, y2: float, color: Color = MUTED) -> None:
    c.setStrokeColor(color)
    c.setFillColor(color)
    c.setLineWidth(1.4)
    c.line(x1, y1, x2, y2)
    c.line(x2, y2, x2 - 5, y2 + 3)
    c.line(x2, y2, x2 - 5, y2 - 3)


def header(c: canvas.Canvas, page: int, kicker: str, title: str, subtitle: str) -> None:
    c.setFillColor(PAPER)
    c.rect(0, 0, W, H, fill=1, stroke=0)
    divider(c, M, H - 48, CW, INK)
    c.setFont("KR-Bold", 7.2)
    c.setFillColor(MUTED)
    c.drawString(M, H - 35, f"{page:02d}  /  {kicker}")
    c.setFillColor(LIME)
    c.rect(W - M - 8, H - 40, 8, 8, fill=1, stroke=0)
    title_size = 24.0
    while title_size > 17 and tw(title, "KR-Bold", title_size) > CW:
        title_size -= 0.5
    c.setFont("KR-Bold", title_size)
    c.setFillColor(INK)
    c.drawString(M, H - 91, safe(title))
    text(c, subtitle, M, H - 114, CW, size=8.2, color=MUTED, max_lines=2)
    divider(c, M, 42, CW)
    c.setFont("KR", 6.7)
    c.setFillColor(MUTED)
    c.drawString(M, 25, "AI GAME DEVELOPMENT WORKFLOW")
    c.setFont("KR-Bold", 7.4)
    c.drawRightString(W - M, 25, f"{page:02d} / {PAGE_COUNT:02d}")
    key = f"page-{page}"
    c.bookmarkPage(key)
    c.addOutlineEntry(title, key, level=0, closed=False)


def end_page(c: canvas.Canvas) -> None:
    c.showPage()


def label(c: canvas.Canvas, value: str, x: float, y: float, color: Color = LIME_DARK) -> None:
    c.setFont("KR-Bold", 7)
    c.setFillColor(color)
    c.drawString(x, y, safe(value))


def bullet_list(
    c: canvas.Canvas,
    items: list[str],
    x: float,
    y: float,
    width: float,
    *,
    size: float = 7.4,
    gap: float = 10,
    text_color: Color = INK,
    dot_color: Color = LIME_DARK,
) -> float:
    yy = y
    for item in items:
        c.setFillColor(dot_color)
        c.circle(x + 3, yy + 2, 2.2, fill=1, stroke=0)
        yy = text(c, item, x + 13, yy + 5, width - 13, size=size, color=text_color, leading=size * 1.48) - gap
    return yy


def card(c: canvas.Canvas, x: float, y: float, w: float, h: float, number: str, title: str, body: str, accent: Color) -> None:
    box(c, x, y, w, h, white, LINE)
    c.setFillColor(accent)
    c.rect(x, y, 4, h, fill=1, stroke=0)
    c.setFont("KR-Bold", 6.8)
    c.setFillColor(MUTED)
    c.drawString(x + 14, y + h - 20, number)
    c.setFont("KR-Bold", 9.4)
    c.setFillColor(INK)
    c.drawString(x + 43, y + h - 21, safe(title))
    text(c, body, x + 14, y + h - 43, w - 28, size=7, color=MUTED, leading=10, max_lines=4)


def draw_image(c: canvas.Canvas, path: Path, x: float, y: float, w: float, h: float, *, stroke: bool = True) -> tuple[float, float, float, float]:
    img = ImageReader(str(path))
    iw, ih = img.getSize()
    scale = min(w / iw, h / ih)
    dw, dh = iw * scale, ih * scale
    dx, dy = x + (w - dw) / 2, y + (h - dh) / 2
    c.drawImage(img, dx, dy, dw, dh, preserveAspectRatio=True, mask="auto")
    if stroke:
        c.setStrokeColor(LINE)
        c.setLineWidth(0.6)
        c.rect(dx, dy, dw, dh, fill=0, stroke=1)
    return dx, dy, dw, dh


def cover(c: canvas.Canvas) -> None:
    c.setFillColor(PAPER)
    c.rect(0, 0, W, H, fill=1, stroke=0)
    c.setFillColor(LIME)
    c.rect(0, H - 13, W, 13, fill=1, stroke=0)
    c.setFillColor(INK)
    c.rect(M, H / 2 - 116, 7, 232, fill=1, stroke=0)
    c.setFont("KR-Bold", 39)
    c.setFillColor(INK)
    c.drawString(M + 28, H / 2 + 29, "AI 활용을 위한")
    c.setFont("KR-Bold", 43)
    c.drawString(M + 28, H / 2 - 32, "게임 개발 구조화")
    c.setFillColor(LIME)
    c.rect(M + 28, H / 2 - 66, 202, 7, fill=1, stroke=0)
    c.bookmarkPage("page-1")
    c.addOutlineEntry("표지", "page-1", level=0, closed=False)
    end_page(c)


def page_why(c: canvas.Canvas) -> None:
    header(c, 2, "WHY STRUCTURE", "AI를 활용하려면 개발을 먼저 구조화해야 한다", "생성 속도보다 중요한 것은 목표·범위·책임·증거를 다음 역할까지 끊기지 않게 연결하는 일이다.")
    items = [
        ("모호한 요청", "PM이 목표·범위·완료 조건을 카드로 고정한다.", LIME),
        ("큰 작업과 충돌", "역할과 격리된 작업 공간으로 변경 경계를 나눈다.", CYAN),
        ("세션 간 맥락 단절", "AnchorMind가 결정·오류·절차를 다음 작업에 전달한다.", VIOLET),
        ("완료 선언의 불확실성", "컴파일·테스트·독립 QA·인간 승인을 같은 흐름에 묶는다.", YELLOW),
    ]
    cw = (CW - 12) / 2
    for i, (title_, body, accent) in enumerate(items):
        row, col = divmod(i, 2)
        card(c, M + col * (cw + 12), 522 - row * 126, cw, 106, f"0{i+1}", title_, body, accent)
    box(c, M, 185, CW, 142, NAVY, None)
    label(c, "DESIGN PRINCIPLE", M + 18, 298, LIME)
    c.setFont("KR-Bold", 14)
    c.setFillColor(white)
    c.drawCentredString(W / 2, 257, "의도  →  범위  →  역할  →  증거  →  승인  →  기억")
    text(c, "AI를 한 명의 자율 개발자로 가정하지 않는다. 명확한 경계와 검증 가능한 인수인계를 따라 일하는 역할 집합으로 사용한다.", M + 38, 220, CW - 76, size=7.7, color=HexColor("#C8D2CC"), align="center", max_lines=2)
    label(c, "CORE CLAIM", M, 142)
    c.setFont("KR-Bold", 13)
    c.setFillColor(INK)
    c.drawString(M, 112, "AI가 개발을 구조화하는 것이 아니라,")
    c.drawString(M, 88, "AI를 활용하기 위해 게임 개발을 먼저 구조화한다.")
    end_page(c)


def page_map(c: canvas.Canvas) -> None:
    header(c, 3, "SYSTEM MAP", "사람의 의도가 실제 변경으로 이어지는 경로", "각 단계는 다음 역할이 확인할 수 있는 산출물과 증거를 남긴다.")
    stages = [
        ("사람", "의도·피드백·승인", CYAN),
        ("Discord Bot", "접수·상태·알림", CYAN),
        ("PM", "목표·범위·완료 조건", LIME),
        ("AgentDesk", "카드·역할·격리", LIME),
        ("Planning / Tech / Art", "계획·구현·자산", VIOLET),
        ("QA / 사람", "증거·플레이·판정", YELLOW),
        ("AnchorMind", "결정·오류·절차", VIOLET),
    ]
    y = 636
    for i, (title_, body, accent) in enumerate(stages):
        x = M + (18 if i % 2 else 0)
        w = CW - (36 if i % 2 else 0)
        box(c, x, y - 56, w, 50, white if i not in (2, 3) else SOFT_LIME, accent)
        c.setFillColor(accent)
        c.rect(x, y - 56, 5, 50, fill=1, stroke=0)
        c.setFont("KR-Bold", 9.5)
        c.setFillColor(INK)
        c.drawString(x + 18, y - 27, title_)
        c.setFont("KR", 7.2)
        c.setFillColor(MUTED)
        c.drawRightString(x + w - 16, y - 27, body)
        if i < len(stages) - 1:
            c.setStrokeColor(LINE)
            c.line(W / 2, y - 61, W / 2, y - 74)
        y -= 76
    box(c, M, 75, CW, 72, NAVY, None)
    c.setFont("KR-Bold", 10.5)
    c.setFillColor(LIME)
    c.drawCentredString(W / 2, 116, "추적 가능한 변환")
    c.setFont("KR", 7.5)
    c.setFillColor(white)
    c.drawCentredString(W / 2, 92, "자연어 요청 → 카드와 역할 → 구현과 증거 → 승인 → 다음 작업의 기억")
    end_page(c)


def page_pm_prompt(c: canvas.Canvas) -> None:
    header(c, 4, "PM & PROMPT", "PM은 요청을 카드와 역할별 프롬프트로 바꾼다", "큰 요청을 한 번에 넘기지 않고, AI가 판단할 수 있는 범위와 검증할 수 있는 완료 조건으로 나눈다.")
    label(c, "PM CARD", M, 674)
    fields = [
        "플레이어에게 보이는 목표",
        "변경 허용 범위와 금지 범위",
        "선행·후속 의존성",
        "compile·test·play 수용 기준",
        "담당 역할과 검토 역할",
        "커밋·로그·문서 링크",
    ]
    yy = 646
    for i, f in enumerate(fields):
        x = M + (i % 2) * (CW / 2 + 4)
        if i and i % 2 == 0:
            yy -= 58
        box(c, x, yy - 42, CW / 2 - 8, 39, white, LINE)
        c.setFont("KR-Bold", 7)
        c.setFillColor(LIME_DARK)
        c.drawString(x + 12, yy - 18, f"0{i+1}")
        c.setFont("KR", 7.3)
        c.setFillColor(INK)
        c.drawString(x + 38, yy - 18, f)
    label(c, "ROLE PROMPT FRAME", M, 452)
    prompt = [
        ("역할", "PM / Planning / Tech / Art / QA 중 이번 책임"),
        ("목표", "플레이어에게 보여야 하는 변화"),
        ("현재 문맥", "카드·기존 결정·선행 작업·현재 상태"),
        ("허용·금지 범위", "바꿔도 되는 파일·씬·자산과 건드리지 않을 영역"),
        ("완료 조건", "완료를 판정하는 compile·test·play 기준"),
        ("보고 형식", "변경 파일·명령·결과·위험·인수인계 순서"),
    ]
    yy = 421
    for i, (head, body) in enumerate(prompt):
        c.setFillColor(LIME if i in (0, 4) else CYAN)
        c.rect(M, yy - 4, 4, 28, fill=1, stroke=0)
        c.setFont("KR-Bold", 8.2)
        c.setFillColor(INK)
        c.drawString(M + 15, yy + 7, head)
        c.setFont("KR", 7.2)
        c.setFillColor(MUTED)
        c.drawString(M + 115, yy + 7, body)
        divider(c, M, yy - 12, CW)
        yy -= 48
    box(c, M, 77, CW, 70, SOFT_LIME, None)
    c.setFont("KR-Bold", 9.2)
    c.setFillColor(LIME_DARK)
    c.drawString(M + 16, 119, "왜 이렇게 썼는가")
    text(c, "범위 확장을 막고, 자연어 완료 선언을 줄이며, 다음 역할이 같은 문맥으로 이어받을 수 있게 하기 위해서다.", M + 16, 96, CW - 32, size=7.3, color=INK, max_lines=2)
    end_page(c)


def page_agentdesk(c: canvas.Canvas) -> None:
    header(c, 5, "AGENTDESK", "카드·역할·격리된 작업 공간이 협업의 경계를 만든다", "에이전트는 무제한으로 저장소를 바꾸지 않고, 카드와 worktree가 허용한 범위 안에서 결과를 다음 역할에 넘긴다.")
    roles = [
        ("Planning", "플레이 흐름·규칙·UX·구현 순서를 구체화", LIME),
        ("Tech", "격리된 공간에서 코드 구현·컴파일·테스트", CYAN),
        ("Art", "자산 규격·적용 경로·시각 품질을 확인", VIOLET),
        ("Reviewer / QA", "수용 기준과 raw evidence를 독립 재현", YELLOW),
    ]
    cw = (CW - 12) / 2
    for i, (title_, body, accent) in enumerate(roles):
        row, col = divmod(i, 2)
        card(c, M + col * (cw + 12), 527 - row * 112, cw, 96, f"0{i+1}", title_, body, accent)
    label(c, "HANDOFF LOOP", M, 370)
    handoffs = [
        ("Planning → Tech / Art", "실행 기준·구현 순서·자산 조건"),
        ("Tech / Art → 카드", "제약·누락 규칙·커밋·검증 결과"),
        ("Reviewer / QA → 담당", "PASS/FAIL·반려 사유·후속 수정 범위"),
        ("PM / 사람 → 전체", "기획 충돌·실제 플레이 감각·최종 범위 판단"),
    ]
    yy = 336
    for i, (left, right) in enumerate(handoffs):
        c.setFont("KR-Bold", 8.2)
        c.setFillColor(INK)
        c.drawString(M, yy, left)
        c.setFont("KR", 7.2)
        c.setFillColor(MUTED)
        c.drawString(M + 185, yy, right)
        divider(c, M, yy - 14, CW)
        yy -= 48
    box(c, M, 80, CW, 72, NAVY, None)
    c.setFont("KR-Bold", 9)
    c.setFillColor(LIME)
    c.drawString(M + 16, 123, "협업의 실제 매개")
    c.setFont("KR", 7.6)
    c.setFillColor(white)
    c.drawString(M + 16, 98, "AgentDesk 카드 · worktree · 커밋 · 테스트 · QA · AnchorMind 기억")
    end_page(c)


def page_agentdesk_evidence(c: canvas.Canvas) -> None:
    header(c, 6, "OPERATING EVIDENCE", "칸반과 카드 상세가 현재 상태와 책임을 드러낸다", "실제 운영 화면의 프로젝트 식별자는 제출용으로 비식별 처리했다.")
    draw_image(c, REDACTED / "agentdesk-kanban-redacted.png", M, 370, CW, 310)
    label(c, "AGENTDESK KANBAN", M, 350)
    text(c, "BACKLOG·READY·DOING·REVIEW 상태와 담당 제공자, 카드 수, 파이프라인 단계를 한 화면에서 확인한다.", M, 334, CW, size=7, color=MUTED, max_lines=2)
    draw_image(c, REDACTED / "agentdesk-card-detail-redacted.png", M, 66, 154, 248)
    x = M + 176
    label(c, "CARD DETAIL", x, 300)
    bullet_list(c, [
        "작업 설명·담당·우선순위·상태를 카드에 고정한다.",
        "연결된 세션과 실행 타임라인으로 인수인계를 추적한다.",
        "토큰·도구·오류 수는 카드 단위 운영 신호로 확인한다.",
        "REVIEW → DOING은 실패가 아니라 보완을 위한 정상 상태 전이다.",
    ], x, 272, CW - 176, size=7.2, gap=9)
    end_page(c)


def page_timeline_context(c: canvas.Canvas) -> None:
    header(c, 7, "TIMELINE & CONTEXT", "시간축과 컨텍스트를 함께 관리한다", "실행 단계를 시간으로 보고, 긴 로그와 이미지가 문맥을 잠식하지 않도록 인수인계 단위를 줄인다.")
    draw_image(c, REDACTED / "agentdesk-timeline-redacted.png", M, 357, CW, 318)
    label(c, "PROJECT → CARD → EXECUTION STAGE", M, 337)
    text(c, "일·주·월 단위로 시작·종료·상태를 비교하고, 카드·디스패치 기록과 함께 병목과 재작업 구간을 확인한다.", M, 320, CW, size=7, color=MUTED, max_lines=2)
    col = (CW - 18) / 2
    card(c, M, 178, col, 112, "01", "관찰한 문제", "스크린샷·대형 로그 누적은 compact 반복과 문맥 비용을 키웠다. 무거운 Unity 카드를 병렬 실행하면 cold import·compile 비용도 함께 커졌다.", RED)
    card(c, M + col + 18, 178, col, 112, "02", "실제 대응", "긴 명령은 로그 파일에 남기고 결론·수치·변경 파일만 전달했다. 장기 결정·오류·절차는 AnchorMind로 넘겼다.", LIME)
    box(c, M, 74, CW, 72, PAPER_ALT, None)
    text(c, "현재 기록에는 카드별 공식 토큰 예산·잔여량이나 제공자별 API 비용이 일관되게 저장되어 있지 않다. 따라서 절감률을 주장하지 않고, 관찰한 문제와 대응 원칙만 제시한다.", M + 16, 119, CW - 32, font="KR-Bold", size=7.2, color=INK, leading=10.8, max_lines=3)
    end_page(c)


def page_discord(c: canvas.Canvas) -> None:
    header(c, 8, "DISCORD BOT", "자연어 대화를 추적 가능한 작업으로 바꾼다", "사람은 새로운 시스템 문법을 배우지 않고 요청하며, Bot은 요청·진행·리뷰·승인을 같은 흐름에 남긴다.")
    flow = [
        ("사람", "자연어 요청과 방향 제시", CYAN),
        ("Discord Bot", "요청을 PM에게 전달", CYAN),
        ("PM", "목표·범위·완료 조건을 카드로 구조화", LIME),
        ("AgentDesk", "카드·역할·격리 공간을 Planning·Tech·Art에 배정", LIME),
        ("Reviewer / QA", "결과와 증거를 독립 검토", VIOLET),
        ("사람", "실제 플레이 후 승인 또는 수정 지시", YELLOW),
        ("Discord Bot", "상태를 알리고 결정·절차를 기억에 공유", CYAN),
    ]
    yy = 650
    for i, (actor, body, accent) in enumerate(flow):
        c.setFillColor(accent)
        c.circle(M + 12, yy - 7, 5, fill=1, stroke=0)
        if i < len(flow) - 1:
            c.setStrokeColor(LINE)
            c.line(M + 12, yy - 13, M + 12, yy - 61)
        c.setFont("KR-Bold", 8.8)
        c.setFillColor(INK)
        c.drawString(M + 34, yy - 4, actor)
        c.setFont("KR", 7.4)
        c.setFillColor(MUTED)
        c.drawString(M + 145, yy - 4, body)
        yy -= 71
    box(c, M, 91, CW, 85, NAVY, None)
    label(c, "WHY IT MATTERS", M + 16, 150, LIME)
    text(c, "자연어 요청을 카드·담당·상태·증거로 변환하고, 승인 전후의 문맥을 나중에도 다시 추적할 수 있게 한다.", M + 16, 124, CW - 32, font="KR-Bold", size=8.3, color=white, max_lines=2)
    end_page(c)


def page_verification(c: canvas.Canvas) -> None:
    header(c, 9, "EVIDENCE & HUMAN CONTROL", "AI 결과는 증거와 인간 판단으로 닫힌다", "완료 문장이 아니라 변경·검증·판정의 흔적이 다음 단계로 넘어가는 조건이다.")
    stages = [
        ("변경", "Git diff\n격리 worktree", CYAN),
        ("자동 검증", "compile·test\n정량 검산", LIME),
        ("독립 QA", "수용 기준\nraw evidence", VIOLET),
        ("인간 승인", "실제 플레이\n방향·품질", YELLOW),
    ]
    gap = 12
    sw = (CW - gap * 3) / 4
    for i, (title_, body, accent) in enumerate(stages):
        x = M + i * (sw + gap)
        box(c, x, 508, sw, 112, white, accent)
        c.setFillColor(accent)
        c.rect(x, 616, sw, 4, fill=1, stroke=0)
        c.setFont("KR-Bold", 9)
        c.setFillColor(INK)
        c.drawCentredString(x + sw / 2, 575, title_)
        text(c, body, x + 8, 548, sw - 16, size=7, color=MUTED, align="center", max_lines=2)
        if i < 3:
            arrow(c, x + sw + 2, 564, x + sw + gap - 2, 564, accent)
    label(c, "INDEPENDENT QUESTIONS", M, 468)
    checks = [
        ("변경이 의도한 범위인가?", "카드·완료 조건·Git diff"),
        ("구현이 실제로 동작하는가?", "compile·EditMode/PlayMode·raw evidence"),
        ("게임으로서 괜찮은가?", "실제 플레이·시각 검토·방향 피드백"),
        ("다음 작업이 안전한가?", "결정·오류·절차의 기억 기록"),
    ]
    yy = 430
    for q, e in checks:
        c.setFont("KR-Bold", 8.4)
        c.setFillColor(INK)
        c.drawString(M, yy, q)
        c.setFont("KR", 7.1)
        c.setFillColor(MUTED)
        c.drawRightString(W - M, yy, e)
        divider(c, M, yy - 15, CW)
        yy -= 55
    box(c, M, 95, CW, 88, SOFT_YELLOW, None)
    c.setFont("KR-Bold", 11)
    c.setFillColor(INK)
    c.drawString(M + 16, 145, "자동 테스트 PASS ≠ 재미있는 게임")
    text(c, "기술 조건과 플레이 품질을 분리하기 위해 실제 플레이와 인간 승인을 별도의 관문으로 둔다.", M + 16, 119, CW - 32, size=7.4, color=MUTED, max_lines=2)
    end_page(c)


def page_memory(c: canvas.Canvas) -> None:
    header(c, 10, "ANCHORMIND", "운영 상태와 기억 구조를 함께 본다", "기억을 저장하는 데서 끝내지 않고 세션·지연·오류·도구 호출과 지식 연결을 실제 화면에서 확인한다.")
    draw_image(c, ASSET / "anchormind-dashboard.jpg", M, 397, CW, 270)
    label(c, "OPERATIONS DASHBOARD", M, 378)
    text(c, "활성 세션, RPC 지연, 오류·인증 상태와 remember·recall·context·reflect 호출을 관찰한다. 수치는 캡처 시점 상태다.", M, 361, CW, size=6.8, color=MUTED, max_lines=2)
    draw_image(c, REDACTED / "anchormind-knowledge-graph-redacted.png", M, 105, CW, 220)
    label(c, "KNOWLEDGE GRAPH", M, 86)
    text(c, "사실·결정·오류·절차·선호·에피소드의 연결을 탐색한다. 프로젝트 식별자는 제출용으로 비식별 처리했다.", M + 120, 86, CW - 120, size=6.8, color=MUTED, max_lines=1)
    end_page(c)


def page_memory_cycle(c: canvas.Canvas) -> None:
    header(c, 11, "MEMORY HANDOFF", "기억은 회상·대조·저장·공유의 순서로 이어진다", "과거 기억을 현재 사실처럼 믿지 않고, 현재 코드와 실행 상태에 대조한 뒤 확인된 정보만 다음 역할에 넘긴다.")
    two = [
        ("작업 기억 / 단기", "현재 카드의 목표·상태·남은 문제·다음 역할에 넘길 내용", CYAN),
        ("개발 기억 / 장기", "확정된 결정·팀 규칙·해결한 오류·반복해서 사용할 절차", VIOLET),
    ]
    cw = (CW - 18) / 2
    for i, (title_, body, accent) in enumerate(two):
        card(c, M + i * (cw + 18), 512, cw, 118, f"0{i+1}", title_, body, accent)
    cycle = [
        ("회상", "관련 결정과 절차를 불러온다."),
        ("대조", "현재 코드와 실행 상태로 확인한다."),
        ("저장", "확인된 결과와 해결 절차만 남긴다."),
        ("공유", "다음 역할이 같은 기준을 이어받는다."),
    ]
    yy = 445
    for i, (title_, body) in enumerate(cycle):
        c.setFillColor(LIME if i in (0, 2) else CYAN)
        c.circle(M + 13, yy - 6, 7, fill=1, stroke=0)
        c.setFont("KR-Bold", 9)
        c.setFillColor(INK)
        c.drawString(M + 40, yy - 4, title_)
        c.setFont("KR", 7.4)
        c.setFillColor(MUTED)
        c.drawString(M + 140, yy - 4, body)
        if i < 3:
            c.setStrokeColor(LINE)
            c.line(M + 13, yy - 14, M + 13, yy - 58)
        yy -= 73
    box(c, M, 103, CW, 104, NAVY, None)
    label(c, "SAFETY", M + 16, 180, LIME)
    bullet_list(c, [
        "기억은 현재 저장소와 실행 상태보다 우선하지 않는다.",
        "비밀번호·토큰·인증 헤더·개인정보는 저장하지 않는다.",
        "추측과 쉽게 재생성되는 출력은 장기기억으로 남기지 않는다.",
    ], M + 16, 154, CW - 32, size=7, gap=3, text_color=white, dot_color=LIME)
    end_page(c)


def page_docs_code(c: canvas.Canvas) -> None:
    header(c, 12, "PLANNING DOCS & CODE", "기획 확정안과 구현 결과를 분리하고 다시 연결한다", "문서는 무엇·왜·수용 기준을 고정하고, 코드는 격리된 구현·커밋·테스트·QA 증거로 그 기준을 닫는다.")
    left = M
    right = M + CW / 2 + 12
    width = CW / 2 - 12
    label(c, "PLANNING DOCUMENT", left, 670)
    bullet_list(c, [
        "플레이 목표·규칙·흐름·수용 기준을 정본으로 기록",
        "버전·변경 날짜·이유·결정 주체·폐기 결정을 함께 보존",
        "좌표·코드 구조·씬 적용은 Tech·Art 후속 카드로 분리",
        "구현 제약이 기획과 충돌하면 PM과 사람에게 다시 보고",
    ], left, 640, width, size=7.1, gap=8)
    label(c, "CODE & CHANGE SOURCE", right, 670)
    bullet_list(c, [
        "카드별 branch·worktree로 역할 간 동시 수정 충돌 완화",
        "기능 단위 커밋에 논리적 변경·테스트·문서 갱신을 묶음",
        "compile·대상 테스트·회귀·정량 검산과 raw evidence 기록",
        "커밋 수가 아니라 카드·diff·테스트·QA의 일치로 품질 판정",
    ], right, 640, width, size=7.1, gap=8)
    divider(c, M + CW / 2, 430, 0, LINE)
    label(c, "CHANGE FLOW", M, 407)
    flow = [
        ("기획 문서", "무엇·왜·수용 기준", LIME),
        ("PM 카드", "범위·역할·완료 조건", LIME),
        ("격리 worktree", "구현 방법·변경 경계", CYAN),
        ("commit + test", "diff·로그·raw evidence", VIOLET),
        ("QA / 사람", "재현·플레이·승인", YELLOW),
    ]
    yy = 368
    for i, (title_, body, accent) in enumerate(flow):
        box(c, M + 44, yy - 44, CW - 88, 42, white, accent)
        c.setFillColor(accent)
        c.rect(M + 44, yy - 44, 5, 42, fill=1, stroke=0)
        c.setFont("KR-Bold", 8.3)
        c.setFillColor(INK)
        c.drawString(M + 63, yy - 22, title_)
        c.setFont("KR", 7.1)
        c.setFillColor(MUTED)
        c.drawRightString(W - M - 63, yy - 22, body)
        if i < len(flow) - 1:
            c.setStrokeColor(LINE)
            c.line(W / 2, yy - 48, W / 2, yy - 58)
        yy -= 62
    end_page(c)


def page_close(c: canvas.Canvas) -> None:
    header(c, 13, "OUTCOME", "사람은 방향을 통제하고, AI는 구조 안에서 반복한다", "실제 작업에 사용한 구조는 AI의 생성 능력을 역할·증거·기억과 연결해 게임 개발의 반복 가능성을 높였다.")
    cases = [
        ("UI 변경", "아트 자산·UGUI 연결·회귀 테스트·실제 플레이를 하나의 완료 흐름으로 묶었다.", CYAN),
        ("씬·배치 변경", "기획 규칙·씬 재빌드·겹침·연결성·이동 거리 검산과 시각 검토를 함께 사용했다.", LIME),
        ("네트워크 작업", "host-client 권한 경계를 먼저 닫고 후속 Relay/Lobby 범위를 분리했다.", VIOLET),
    ]
    yy = 610
    for i, (title_, body, accent) in enumerate(cases):
        card(c, M, yy - 92, CW, 82, f"0{i+1}", title_, body, accent)
        yy -= 108
    label(c, "FIVE DIFFERENCES", M, 274)
    points = [
        "PM이 구조화하고 AgentDesk가 Planning·Tech·Art·Review로 라우팅한다.",
        "자연어 요청을 카드·담당·상태·증거로 변환한다.",
        "역할별 작업 공간으로 변경 충돌과 책임을 분리한다.",
        "컴파일·테스트·정량 검증·실제 플레이를 한 흐름으로 연결한다.",
        "결정·오류·절차를 기억해 다음 작업의 출발점으로 재사용한다.",
    ]
    bullet_list(c, points, M, 245, CW, size=7.5, gap=7)
    box(c, M, 74, CW, 78, NAVY, None)
    c.setFont("KR-Bold", 12.3)
    c.setFillColor(LIME)
    c.drawCentredString(W / 2, 119, "AI를 위한 구조가 곧 개발의 품질 경계가 된다.")
    c.setFont("KR", 7.3)
    c.setFillColor(white)
    c.drawCentredString(W / 2, 94, "방향과 품질은 사람이, 구조화된 구현과 반복 검증은 AI가 맡는다.")
    end_page(c)


def build() -> Path:
    register_fonts()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(OUTPUT), pagesize=A4, pageCompression=1)
    c.setTitle("AI 활용을 위한 게임 개발 구조화")
    c.setAuthor("AI Game Development Workflow")
    c.setSubject("PM, Discord Bot, AgentDesk, AnchorMind를 활용한 구조적 AI 게임 개발 흐름")
    c.setKeywords("PM, Discord Bot, AgentDesk, AnchorMind, Kanban, AI game development")
    for draw in (
        cover,
        page_why,
        page_map,
        page_pm_prompt,
        page_agentdesk,
        page_agentdesk_evidence,
        page_timeline_context,
        page_discord,
        page_verification,
        page_memory,
        page_memory_cycle,
        page_docs_code,
        page_close,
    ):
        draw(c)
    c.save()
    return OUTPUT


if __name__ == "__main__":
    print(build())
