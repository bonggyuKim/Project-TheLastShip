"""Generate LAST SHIFT UI sprites and a contact sheet.

The kit is deliberately shape-first: every resource stays identifiable when color-blind
or viewed at 32 px. Run from the repository root with Python 3 + Pillow.
"""
from __future__ import annotations

import hashlib
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/DoodleUp/Art/UI/LastShift"
PREVIEW = ROOT / "docs/art/mockups/last-shift-ui-kit-v1.png"
S = 4

C = {
    "ink": "#16263A", "panel": "#1A2E48", "line": "#304C6D",
    "ivory": "#F3E8CF", "muted": "#8FA6BF", "orange": "#F29B4B",
    "lime": "#8EDB7B", "cyan": "#63D4D1", "yellow": "#FFDB59",
    "red": "#FF5A4D", "white": "#FFFFFF", "brown": "#8B6B54",
}


def rgba(hex_color: str, alpha: int = 255):
    h = hex_color.lstrip("#")
    return tuple(int(h[i:i+2], 16) for i in (0, 2, 4)) + (alpha,)


def canvas(size=(128, 128)):
    return Image.new("RGBA", (size[0] * S, size[1] * S), (0, 0, 0, 0))


def save(im: Image.Image, name: str):
    im.resize((im.width // S, im.height // S), Image.Resampling.LANCZOS).save(OUT / name)


def icon_pair(name, accent, draw_symbol):
    """Write aligned empty/full sprites for a Vertical Filled Image overlay."""
    base = canvas(); bd = ImageDraw.Draw(base)
    bd.rounded_rectangle((10*S, 10*S, 118*S, 118*S), 28*S, fill=rgba(C["panel"], 238), outline=rgba(C["ivory"]), width=5*S)
    draw_symbol(bd, C["muted"], False)
    save(base, f"icon_gauge_{name}_base.png")

    fill = canvas(); fd = ImageDraw.Draw(fill)
    draw_symbol(fd, accent, True)
    save(fill, f"icon_gauge_{name}_fill.png")


def generate_icons():
    def wrench(d, a, filled):
        d.line((39*S, 91*S, 88*S, 42*S), fill=rgba(a), width=15*S)
        d.ellipse((29*S, 82*S, 51*S, 104*S), outline=rgba(a), width=8*S)
        d.polygon([(78*S,47*S),(80*S,27*S),(92*S,38*S),(104*S,36*S),(96*S,55*S)], fill=rgba(a))
    def crate(d, a, filled):
        d.rounded_rectangle((31*S,39*S,97*S,96*S),8*S,fill=rgba(a) if filled else None,outline=rgba(a),width=8*S)
        d.line((37*S,48*S,91*S,89*S),fill=rgba(C["ink"]),width=7*S); d.line((91*S,48*S,37*S,89*S),fill=rgba(C["ink"]),width=7*S)
    def oxygen(d, a, filled):
        d.rounded_rectangle((45*S,31*S,82*S,96*S),16*S,fill=rgba(a) if filled else None,outline=rgba(a),width=8*S)
        d.rectangle((54*S,23*S,73*S,35*S),fill=rgba(a)); d.ellipse((84*S,31*S,100*S,47*S),outline=rgba(a),width=6*S)
        d.ellipse((28*S,63*S,39*S,74*S),fill=rgba(a)); d.ellipse((27*S,42*S,34*S,49*S),fill=rgba(a))
    def food(d, a, filled):
        # A single apple reads more reliably than the old plate/leaf composite at 32 px.
        d.ellipse((31*S,43*S,97*S,103*S),fill=rgba(a) if filled else None,outline=rgba(a),width=8*S)
        d.line((64*S,47*S,68*S,25*S),fill=rgba(a),width=8*S)
        d.ellipse((68*S,24*S,91*S,39*S),fill=rgba(a) if filled else None,outline=rgba(a),width=6*S)
    def dock(d, a, filled):
        # Opposed clamps + a central ship communicate docking without fine detail.
        d.arc((24*S,27*S,104*S,107*S),45,150,fill=rgba(a),width=14*S); d.arc((24*S,27*S,104*S,107*S),210,315,fill=rgba(a),width=14*S)
        d.polygon([(64*S,25*S),(82*S,58*S),(76*S,94*S),(52*S,94*S),(46*S,58*S)],fill=rgba(a) if filled else None,outline=rgba(a))
        d.line((45*S,66*S,83*S,66*S),fill=rgba(a),width=7*S)
    def thrust(d, a, filled):
        d.polygon([(64*S,23*S),(86*S,63*S),(77*S,87*S),(51*S,87*S),(42*S,63*S)],fill=rgba(a) if filled else None,outline=rgba(a))
        d.ellipse((55*S,48*S,73*S,66*S),fill=rgba(a)); d.polygon([(52*S,87*S),(64*S,110*S),(76*S,87*S)],fill=rgba(C["red"]))
    def interact(d, a, filled):
        d.rounded_rectangle((48*S,49*S,86*S,95*S),12*S,fill=rgba(a) if filled else None,outline=rgba(a),width=6*S); d.line((51*S,58*S,51*S,31*S),fill=rgba(a),width=10*S)
        d.line((64*S,53*S,64*S,25*S),fill=rgba(a),width=10*S); d.line((77*S,57*S,77*S,34*S),fill=rgba(a),width=10*S)
        d.polygon([(49*S,77*S),(31*S,65*S),(27*S,76*S),(54*S,101*S)],fill=rgba(a))
    def warning(d, a, filled):
        d.polygon([(64*S,24*S),(105*S,99*S),(23*S,99*S)],fill=rgba(a) if filled else None,outline=rgba(a)); d.rectangle((59*S,49*S,69*S,76*S),fill=rgba(C["ink"] if filled else a)); d.ellipse((59*S,82*S,69*S,92*S),fill=rgba(C["ink"] if filled else a))
    specs = [("maintenance",C["orange"],wrench),("materials",C["yellow"],crate),("oxygen",C["cyan"],oxygen),("food",C["lime"],food),("docking",C["cyan"],dock),("thrust",C["orange"],thrust),("interact",C["lime"],interact),("warning",C["red"],warning)]
    for spec in specs: icon_pair(*spec)


def generate_chrome():
    im=canvas((128,128)); d=ImageDraw.Draw(im); d.rounded_rectangle((2*S,2*S,126*S,126*S),22*S,fill=rgba(C["panel"],242),outline=rgba(C["ivory"]),width=4*S); d.line((22*S,14*S,106*S,14*S),fill=rgba(C["orange"]),width=5*S); save(im,"panel_9slice.png")
    im=canvas((192,64)); d=ImageDraw.Draw(im); d.rounded_rectangle((2*S,2*S,190*S,62*S),18*S,fill=rgba(C["ink"],235),outline=rgba(C["ivory"]),width=4*S); d.polygon([(20*S,18*S),(34*S,32*S),(20*S,46*S)],fill=rgba(C["lime"])); save(im,"prompt_plate.png")
    im=canvas((64,64)); d=ImageDraw.Draw(im); d.rounded_rectangle((3*S,3*S,61*S,61*S),15*S,fill=rgba(C["ivory"]),outline=rgba(C["ink"]),width=5*S); d.rounded_rectangle((12*S,12*S,52*S,52*S),9*S,outline=rgba(C["orange"]),width=4*S); save(im,"keycap.png")


def write_meta(path: Path):
    """guid 만 든 최소 .meta 를 쓴다. 이미 있으면 손대지 않는다.

    v1 은 임포터 설정까지 전부 손으로 적었는데 그 YAML 을 유니티가 못 읽어
    ("could not be parsed") 그림 17장이 통째로 AssetDatabase 에서 무시됐다.
    스프라이트 여부·9-slice 경계·메시 종류는 이제 유니티 쪽 빌더가 정한다
    (Assets/DoodleUp/Editor/LastShiftUiKitBuilder.cs). 여기서 하는 일은 그림을
    다시 구워도 guid 가 안 바뀌게 고정해 두는 것뿐이다.
    """
    meta = path.with_suffix(path.suffix + ".meta")
    if meta.exists():
        return
    guid = hashlib.md5(("last-shift-ui:" + path.name).encode()).hexdigest()
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "TextureImporter:\n"
        "  externalObjects: {}\n"
        "  userData:\n"
        "  assetBundleName:\n"
        "  assetBundleVariant:\n",
        encoding="utf-8")


def contact_sheet():
    files=sorted(OUT.glob("*.png")); w,h=1024,720
    out=Image.new("RGB",(w,h),rgba(C["ink"])[:3]); d=ImageDraw.Draw(out)
    font_path = Path("C:/Windows/Fonts/arial.ttf")
    bold_path = Path("C:/Windows/Fonts/arialbd.ttf")
    label_font = ImageFont.truetype(str(font_path), 15) if font_path.exists() else ImageFont.load_default()
    title_font = ImageFont.truetype(str(bold_path), 28) if bold_path.exists() else label_font
    d.text((42,32),"LAST SHIFT  /  UI KIT v1",font=title_font,fill=rgba(C["ivory"])[:3])
    for i,p in enumerate(files):
        im=Image.open(p); im.thumbnail((180,92),Image.Resampling.LANCZOS); x=42+(i%5)*194; y=92+(i//5)*142
        d.rounded_rectangle((x-8,y-8,x+180,y+104),14,fill=rgba(C["panel"])[:3],outline=rgba(C["line"])[:3],width=2)
        out.paste(im,(x+(172-im.width)//2,y),im); d.text((x,y+94),p.stem.replace("icon_","").replace("gauge_",""),font=label_font,fill=rgba(C["muted"])[:3])
    PREVIEW.parent.mkdir(parents=True,exist_ok=True); out.save(PREVIEW)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    generate_icons(); generate_chrome()
    for p in OUT.glob("*.png"): write_meta(p)
    contact_sheet()


if __name__ == "__main__": main()
