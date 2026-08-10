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


def icon(name, accent, draw_symbol):
    im = canvas(); d = ImageDraw.Draw(im)
    d.rounded_rectangle((10*S, 10*S, 118*S, 118*S), 28*S, fill=rgba(C["panel"]), outline=rgba(C["ivory"]), width=5*S)
    d.rounded_rectangle((18*S, 18*S, 110*S, 110*S), 22*S, outline=rgba(accent), width=5*S)
    draw_symbol(d, accent)
    save(im, name)


def generate_icons():
    def wrench(d, a):
        d.line((39*S, 91*S, 88*S, 42*S), fill=rgba(a), width=15*S)
        d.ellipse((29*S, 82*S, 51*S, 104*S), outline=rgba(a), width=8*S)
        d.polygon([(78*S,47*S),(80*S,27*S),(92*S,38*S),(104*S,36*S),(96*S,55*S)], fill=rgba(a))
    def crate(d, a):
        d.rounded_rectangle((31*S,39*S,97*S,96*S),8*S,fill=rgba(a),outline=rgba(C["ivory"]),width=5*S)
        d.line((37*S,48*S,91*S,89*S),fill=rgba(C["ink"]),width=7*S); d.line((91*S,48*S,37*S,89*S),fill=rgba(C["ink"]),width=7*S)
    def oxygen(d, a):
        d.rounded_rectangle((45*S,31*S,82*S,96*S),16*S,fill=rgba(a),outline=rgba(C["ivory"]),width=5*S)
        d.rectangle((54*S,23*S,73*S,35*S),fill=rgba(C["ivory"])); d.ellipse((84*S,31*S,100*S,47*S),outline=rgba(a),width=5*S)
        d.ellipse((28*S,63*S,39*S,74*S),fill=rgba(a)); d.ellipse((27*S,42*S,34*S,49*S),fill=rgba(a))
    def food(d, a):
        d.ellipse((29*S,70*S,99*S,95*S),fill=rgba(C["ivory"])); d.rectangle((35*S,65*S,93*S,78*S),fill=rgba(a))
        d.arc((40*S,28*S,88*S,76*S),190,350,fill=rgba(a),width=8*S); d.line((64*S,35*S,64*S,68*S),fill=rgba(a),width=7*S)
        d.polygon([(64*S,46*S),(82*S,29*S),(83*S,50*S)],fill=rgba(a))
    def dock(d, a):
        d.arc((25*S,29*S,103*S,105*S),35,145,fill=rgba(a),width=13*S); d.arc((25*S,29*S,103*S,105*S),215,325,fill=rgba(a),width=13*S)
        d.rectangle((53*S,44*S,75*S,84*S),fill=rgba(C["ivory"])); d.polygon([(64*S,25*S),(51*S,48*S),(77*S,48*S)],fill=rgba(a))
    def thrust(d, a):
        d.polygon([(64*S,23*S),(86*S,63*S),(77*S,87*S),(51*S,87*S),(42*S,63*S)],fill=rgba(C["ivory"]),outline=rgba(a))
        d.ellipse((55*S,48*S,73*S,66*S),fill=rgba(a)); d.polygon([(52*S,87*S),(64*S,110*S),(76*S,87*S)],fill=rgba(C["red"]))
    def interact(d, a):
        d.rounded_rectangle((48*S,49*S,86*S,95*S),12*S,fill=rgba(a)); d.line((51*S,58*S,51*S,31*S),fill=rgba(C["ivory"]),width=10*S)
        d.line((64*S,53*S,64*S,25*S),fill=rgba(C["ivory"]),width=10*S); d.line((77*S,57*S,77*S,34*S),fill=rgba(C["ivory"]),width=10*S)
        d.polygon([(49*S,77*S),(31*S,65*S),(27*S,76*S),(54*S,101*S)],fill=rgba(a))
    def warning(d, a):
        d.polygon([(64*S,24*S),(105*S,99*S),(23*S,99*S)],fill=rgba(a),outline=rgba(C["ivory"])); d.rectangle((59*S,49*S,69*S,76*S),fill=rgba(C["ink"])); d.ellipse((59*S,82*S,69*S,92*S),fill=rgba(C["ink"]))
    specs = [("icon_maintenance.png",C["orange"],wrench),("icon_materials.png",C["yellow"],crate),("icon_oxygen.png",C["cyan"],oxygen),("icon_food.png",C["lime"],food),("icon_docking.png",C["cyan"],dock),("icon_thrust.png",C["orange"],thrust),("icon_interact.png",C["lime"],interact),("icon_warning.png",C["red"],warning)]
    for spec in specs: icon(*spec)


def generate_gauges():
    im=canvas((512,64)); d=ImageDraw.Draw(im); d.rounded_rectangle((2*S,2*S,510*S,62*S),22*S,fill=rgba(C["panel"]),outline=rgba(C["ivory"]),width=4*S); d.rounded_rectangle((12*S,12*S,500*S,52*S),15*S,outline=rgba(C["line"]),width=4*S); save(im,"gauge_frame.png")
    for name,color,kind in [("maintenance",C["orange"],0),("materials",C["yellow"],1),("oxygen",C["cyan"],2),("food",C["lime"],3),("docking",C["cyan"],4)]:
        im=canvas((480,40)); d=ImageDraw.Draw(im); d.rounded_rectangle((0,0,480*S,40*S),15*S,fill=rgba(color))
        for x in range(28,480,48):
            if kind in (0,4): d.line((x*S,7*S,(x+18)*S,33*S),fill=rgba(C["white"],70),width=6*S)
            elif kind==1: d.rectangle((x*S,9*S,(x+20)*S,31*S),outline=rgba(C["ink"],70),width=4*S)
            elif kind==2: d.ellipse((x*S,9*S,(x+18)*S,27*S),outline=rgba(C["white"],110),width=4*S)
            else: d.polygon([(x*S,28*S),(x+9*S,9*S),(x+18*S,28*S)],fill=rgba(C["white"],80))
        save(im,f"gauge_fill_{name}.png")


def generate_chrome():
    im=canvas((128,128)); d=ImageDraw.Draw(im); d.rounded_rectangle((2*S,2*S,126*S,126*S),22*S,fill=rgba(C["panel"],242),outline=rgba(C["ivory"]),width=4*S); d.line((22*S,14*S,106*S,14*S),fill=rgba(C["orange"]),width=5*S); save(im,"panel_9slice.png")
    im=canvas((192,64)); d=ImageDraw.Draw(im); d.rounded_rectangle((2*S,2*S,190*S,62*S),18*S,fill=rgba(C["ink"],235),outline=rgba(C["ivory"]),width=4*S); d.polygon([(20*S,18*S),(34*S,32*S),(20*S,46*S)],fill=rgba(C["lime"])); save(im,"prompt_plate.png")
    im=canvas((64,64)); d=ImageDraw.Draw(im); d.rounded_rectangle((3*S,3*S,61*S,61*S),15*S,fill=rgba(C["ivory"]),outline=rgba(C["ink"]),width=5*S); d.rounded_rectangle((12*S,12*S,52*S,52*S),9*S,outline=rgba(C["orange"]),width=4*S); save(im,"keycap.png")


def write_meta(path: Path):
    guid=hashlib.md5(("last-shift-ui:"+path.name).encode()).hexdigest()
    border="16, 16, 16, 16" if path.name=="panel_9slice.png" else "0, 0, 0, 0"
    path.with_suffix(path.suffix+".meta").write_text(f'''fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n  internalIDToNameTable: []\n  externalObjects: {{}}\n  serializedVersion: 13\n  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n  isReadable: 0\n  streamingMipmaps: 0\n  sRGBTexture: 1\n  alphaIsTransparency: 1\n  textureType: 8\n  textureShape: 1\n  singleChannelComponent: 0\n  spriteMode: 1\n  spritePixelsToUnits: 100\n  spriteBorder: {{{border}}}\n  spriteGenerateFallbackPhysicsShape: 0\n  alphaUsage: 1\n  wrapU: 1\n  wrapV: 1\n  wrapW: 1\n  filterMode: 1\n  aniso: 1\n  textureCompression: 0\n  maxTextureSize: 2048\n  platformSettings: []\n  spriteSheet:\n    serializedVersion: 2\n    sprites: []\n    outline: []\n    physicsShape: []\n    bones: []\n    spriteID: 5e97eb03825dee720800000000000000\n    internalID: 0\n    vertices: []\n    indices:\n    edges: []\n    weights: []\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n''',encoding="utf-8")


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
    generate_icons(); generate_gauges(); generate_chrome()
    for p in OUT.glob("*.png"): write_meta(p)
    contact_sheet()


if __name__ == "__main__": main()
