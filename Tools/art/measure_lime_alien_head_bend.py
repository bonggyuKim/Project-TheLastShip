"""Measure how Rigify's head control distributes rotation to neck DEF bones.

Run with Blender 4.5 LTS:
  blender -b LastShiftLimeAlien_Rigify_Test.blend --python measure_lime_alien_head_bend.py
"""

import json
import math
import sys

import bpy
from mathutils import Quaternion


CONTROL = "head"
TARGETS = ("DEF-spine.004", "DEF-spine.005")
TEST_DEGREES = 30.0


def angle_degrees(rotation: Quaternion) -> float:
    return math.degrees(rotation.angle)


armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
rig = next((obj for obj in armatures if CONTROL in obj.pose.bones), None)
if rig is None:
    raise RuntimeError(f"No armature contains the {CONTROL!r} control")

missing = [name for name in TARGETS if name not in rig.pose.bones]
if missing:
    raise RuntimeError(f"Missing target bones: {missing}")

control = rig.pose.bones[CONTROL]
original_mode = control.rotation_mode
original_quaternion = control.rotation_quaternion.copy()
results = {}

try:
    control.rotation_mode = "QUATERNION"
    control.rotation_quaternion = Quaternion()
    bpy.context.view_layer.update()
    baseline = {name: rig.pose.bones[name].matrix.copy() for name in TARGETS}

    for axis_name, axis in (("X", (1, 0, 0)), ("Y", (0, 1, 0)), ("Z", (0, 0, 1))):
        control.rotation_quaternion = Quaternion(axis, math.radians(TEST_DEGREES))
        bpy.context.view_layer.update()
        samples = {}
        for name in TARGETS:
            delta = baseline[name].to_quaternion().rotation_difference(
                rig.pose.bones[name].matrix.to_quaternion()
            )
            degrees = angle_degrees(delta)
            samples[name] = {
                "rotation_degrees": round(degrees, 6),
                "ratio_to_head": round(degrees / TEST_DEGREES, 6),
                "delta_axis": [round(value, 6) for value in delta.axis],
            }
        results[axis_name] = samples
finally:
    control.rotation_quaternion = original_quaternion
    control.rotation_mode = original_mode
    bpy.context.view_layer.update()

payload = {
    "blend_file": bpy.data.filepath,
    "rig": rig.name,
    "control": CONTROL,
    "input_degrees": TEST_DEGREES,
    "targets": list(TARGETS),
    "target_constraints": {
        name: [
            {
                "name": constraint.name,
                "type": constraint.type,
                "influence": round(constraint.influence, 6),
                "subtarget": getattr(constraint, "subtarget", ""),
            }
            for constraint in rig.pose.bones[name].constraints
        ]
        for name in TARGETS
    },
    "results": results,
}
print("HEAD_BEND_MEASUREMENT=" + json.dumps(payload, ensure_ascii=False, sort_keys=True))

# The Rigify head control bends the upper neck through ORG-spine.005 only.
# Blender's evaluated matrices land just below the exact 0.5 ratio because of
# float/evaluation precision, so keep a narrow tolerance around the measured rule.
bend = results["Y"]
if abs(bend["DEF-spine.004"]["ratio_to_head"]) > 0.001:
    sys.exit("DEF-spine.004 unexpectedly receives head bend")
if abs(bend["DEF-spine.005"]["ratio_to_head"] - 0.5) > 0.001:
    sys.exit("DEF-spine.005 head bend ratio is no longer 0.5")
