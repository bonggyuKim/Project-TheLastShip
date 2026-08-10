"""LastShiftLimeAlien 폴더에서 .meta 가 빠진 자산에 결정론적 GUID meta 를 생성한다.

- 애니메이션 FBX: 기존 형제 meta(Walk_Loop) 설정을 그대로 따라간다. clipAnimations 는
  프레임 구간을 임의로 지어내지 않고 비워 두어 Unity 가 take 기본 클립을 만들게 한다.
- 리그 FBX: Rigged.fbx meta 설정(아바타를 이 모델에서 생성)을 따른다.
- .blend 원본: 런타임 자산이 아니므로 애니메이션/머티리얼 임포트를 끈 최소 설정.
- .png 검증 렌더: guid 만 고정하고 나머지는 Unity 가 기본값으로 채우게 둔다.

GUID 는 자산 경로의 md5 로 뽑아, 재실행해도 같은 값이 나오도록 한다.
"""

import hashlib
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
DIR = ROOT / "Assets/DoodleUp/Art/Characters/LastShiftLimeAlien"
SIBLING_ANIM = DIR / "LastShiftLimeAlien_Walk_Loop.fbx.meta"
SIBLING_RIG = DIR / "LastShiftLimeAlien_Rigged.fbx.meta"

ANIM_FBX = [
    "LastShiftLimeAlien_BroadJump.fbx",
    "LastShiftLimeAlien_Carry_Walk_Loop.fbx",
    "LastShiftLimeAlien_Fall_Loop.fbx",
    "LastShiftLimeAlien_Jump.fbx",
]
RIG_FBX = ["LastShiftLimeAlien_Rigify_Test.fbx"]
BLEND = [
    "LastShiftLimeAlien_Rigged.blend",
    "LastShiftLimeAlien_Rigify_Test.blend",
]
PNG = [
    "LastShiftLimeAlien_Rigify_Neutral.png",
    "LastShiftLimeAlien_Rigify_Overhead.png",
    "LastShiftLimeAlien_Rig_NeutralPose.png",
    "LastShiftLimeAlien_Rig_NeutralPose_Side.png",
    "LastShiftLimeAlien_Rig_StressPose.png",
    "LastShiftLimeAlien_WeightFinal_Arm_L.png",
    "LastShiftLimeAlien_WeightFinal_Arm_R.png",
    "LastShiftLimeAlien_WeightFinal_Head.png",
    "LastShiftLimeAlien_WeightFinal_Leg_L.png",
    "LastShiftLimeAlien_WeightFinal_Leg_R.png",
]

PNG_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def guid_for(name: str) -> str:
    seed = "Project-DoodleUp/Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/" + name
    return hashlib.md5(seed.encode("utf-8")).hexdigest()


def read_lines(path: pathlib.Path) -> list[str]:
    return path.read_text(encoding="utf-8").splitlines(keepends=True)


def replace_guid(lines: list[str], guid: str) -> list[str]:
    out = list(lines)
    for i, line in enumerate(out):
        if line.startswith("guid: "):
            out[i] = f"guid: {guid}\n"
            break
    return out


def drop_clip_animations(lines: list[str]) -> list[str]:
    """`clipAnimations:` 블록을 빈 리스트로 치환한다."""
    out = []
    skipping = False
    for line in lines:
        if skipping:
            # 같은 들여쓰기(4칸)의 다음 키를 만나면 블록 종료.
            if re.match(r"^ {4}[A-Za-z_]", line):
                skipping = False
            else:
                continue
        if line.startswith("    clipAnimations:"):
            out.append("    clipAnimations: []\n")
            skipping = True
            continue
        out.append(line)
    return out


def set_scalar(lines: list[str], key: str, value: str) -> list[str]:
    out = list(lines)
    for i, line in enumerate(out):
        stripped = line.lstrip()
        indent = line[: len(line) - len(stripped)]
        if stripped.startswith(key + ":"):
            out[i] = f"{indent}{key}: {value}\n"
    return out


def write_meta(asset: str, lines: list[str]) -> None:
    target = DIR / (asset + ".meta")
    if target.exists():
        # 이미 있는 meta 는 Unity 가 정규화한 내용이므로 절대 덮어쓰지 않는다.
        print(f"skip {target.name} (already exists)")
        return
    target.write_text("".join(lines), encoding="utf-8", newline="\n")
    print(f"wrote {target.name}")


def main() -> None:
    anim_base = read_lines(SIBLING_ANIM)
    rig_base = read_lines(SIBLING_RIG)

    for asset in ANIM_FBX:
        lines = drop_clip_animations(replace_guid(anim_base, guid_for(asset)))
        write_meta(asset, lines)

    for asset in RIG_FBX:
        write_meta(asset, replace_guid(rig_base, guid_for(asset)))

    for asset in BLEND:
        lines = replace_guid(rig_base, guid_for(asset))
        lines = set_scalar(lines, "materialImportMode", "0")
        lines = set_scalar(lines, "importAnimation", "0")
        lines = set_scalar(lines, "animationType", "0")
        lines = set_scalar(lines, "avatarSetup", "0")
        write_meta(asset, lines)

    for asset in PNG:
        path = DIR / asset
        if not path.exists():
            continue
        write_meta(asset, [PNG_TEMPLATE.format(guid=guid_for(asset))])


if __name__ == "__main__":
    main()
