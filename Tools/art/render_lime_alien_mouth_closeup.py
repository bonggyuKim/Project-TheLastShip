"""Render a deterministic front close-up of the lime alien mouth.

Pass the output path after ``--``. The loaded ``.blend`` is not saved.
"""

from __future__ import annotations

import sys
import math
from pathlib import Path

import bpy
from mathutils import Quaternion, Vector


def script_args() -> list[str]:
    if "--" not in sys.argv:
        raise SystemExit("Expected: -- OUTPUT.png")
    return sys.argv[sys.argv.index("--") + 1 :]


def main() -> None:
    args = script_args()
    if not args or len(args) > 3:
        raise SystemExit("Expected: OUTPUT.png [front|oblique] [X|Y|Z]")

    output = Path(args[0]).resolve()
    view = args[1] if len(args) == 2 else "front"
    if view not in {"front", "oblique"}:
        raise SystemExit(f"Unknown view: {view}")
    pose_axis = args[2].upper() if len(args) == 3 else None
    if pose_axis not in {None, "X", "Y", "Z"}:
        raise SystemExit(f"Unknown pose axis: {pose_axis}")
    output.parent.mkdir(parents=True, exist_ok=True)

    scene = bpy.context.scene
    if pose_axis:
        rig = bpy.data.objects["rig"]
        control = rig.pose.bones["head"]
        control.rotation_mode = "QUATERNION"
        axis = {
            "X": (1.0, 0.0, 0.0),
            "Y": (0.0, 1.0, 0.0),
            "Z": (0.0, 0.0, 1.0),
        }[pose_axis]
        control.rotation_quaternion = Quaternion(axis, math.radians(30.0))
        bpy.context.view_layer.update()
    camera = bpy.data.objects["Front_Reference_Camera"]
    scene.camera = camera
    if view == "front":
        camera.location = (-0.055, -6.0, 0.565)
        camera.rotation_euler = (1.570797, 0.0, 0.0)
        camera.data.ortho_scale = 0.28
    else:
        target = Vector((-0.014, -0.22, 0.575))
        camera.location = target + Vector((-1.7, -5.8, 0.35))
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
        camera.data.ortho_scale = 0.22
    camera.data.type = "ORTHO"

    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output)
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"

    bpy.ops.render.render(write_still=True)
    print(f"MOUTH_RENDER={output}")


if __name__ == "__main__":
    main()
