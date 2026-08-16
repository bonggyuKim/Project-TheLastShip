"""Assets/DoodleUp/Resources/Fonts/NotoSansKR-Regular.ttf 를 다시 굽는다.

원본은 Windows 에 딸려 오는 가변 폰트 ``NotoSansKR-VF.ttf`` (SIL OFL 1.1) 다.
그대로 넣으면 10.4MB 이고 기본 인스턴스가 Thin(wght=100) 이라 본문에 못 쓴다.
그래서 두 단계를 거친다.

  1. wght=400 으로 인스턴스 → Regular
  2. 한글 음절 전체 + 라틴/기호로 서브셋 → 한자를 버려 2.5MB

**한글 음절 블록은 통째로 남긴다.** 지금 쓰는 989 자만 넣으면 대사 한 줄이 늘 때마다
빠진 글자가 OS 폴백으로 떨어져 단어 중간에서 서체가 바뀐다 — 이 폰트를 번들한 이유가
바로 그 현상이라 부분 커버리지는 문제를 줄이는 게 아니라 옮기는 것이다.

    python Tools/build_korean_font.py            # 기본 경로로 굽는다
    python Tools/build_korean_font.py <출력.ttf>

fontTools 가 필요하다 (``pip install fonttools``).
"""

import os
import sys

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

SOURCE = r"C:\Windows\Fonts\NotoSansKR-VF.ttf"
DEFAULT_OUTPUT = os.path.join(
    "Assets", "DoodleUp", "Resources", "Fonts", "NotoSansKR-Regular.ttf")

# (시작, 끝) 포함 구간. 게임 문자열을 훑어 실제로 쓰이는 비-ASCII 를 모은 뒤
# 그 문자가 속한 블록 단위로 넓혀 둔 것이다.
RANGES = [
    (0x0020, 0x007E),   # ASCII
    (0x00A0, 0x00FF),   # § ° ± ² ³ · ×
    (0x0391, 0x03C9),   # ε π
    (0x2010, 0x2027),   # – — … 따옴표 · 불릿
    (0x2030, 0x205E),
    (0x2190, 0x21FF),   # ← ↑ → ↔ ↗
    (0x2200, 0x22FF),   # ∈ − √ ≈ ≤ ≥
    (0x2500, 0x257F),   # ─ ├ ┤
    (0x25A0, 0x25FF),   # □ ▲ ○
    (0x2600, 0x26FF),   # ⚠ ⚡
    (0x3000, 0x303F),   # CJK 문장부호
    (0x3130, 0x318F),   # 호환용 자모 (ㄱ ㄴ ㄷ 단독 표기)
    (0xAC00, 0xD7A3),   # 한글 음절 11172 자
]


def build(source_path, output_path):
    unicodes = set()
    for first, last in RANGES:
        unicodes.update(range(first, last + 1))

    font = instancer.instantiateVariableFont(
        TTFont(source_path), {"wght": 400}, updateFontNames=True, inplace=False)

    options = subset.Options()
    options.layout_features = ["*"]
    options.name_IDs = ["*"]      # 라이선스(nameID 13·14)를 남긴다 — OFL 요구사항이다
    options.name_legacy = True
    options.notdef_outline = True
    options.hinting = True
    options.glyph_names = False

    subsetter = subset.Subsetter(options=options)
    subsetter.populate(unicodes=unicodes)
    subsetter.subset(font)

    os.makedirs(os.path.dirname(output_path) or ".", exist_ok=True)
    font.save(output_path)
    return output_path


def main(argv):
    output = argv[1] if len(argv) > 1 else DEFAULT_OUTPUT
    if not os.path.exists(SOURCE):
        print(f"원본 폰트를 못 찾았다: {SOURCE}")
        return 1

    build(SOURCE, output)
    built = TTFont(output)
    print(f"{output}  {os.path.getsize(output):,} bytes  "
          f"{built['maxp'].numGlyphs} glyphs")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
