"""Recenter the Rigify left-knee skin transition and validate the result.

Run against the Blender source:
    blender -b LastShiftLimeAlien_Rigify_Test.blend -P this_script.py -- --save-blend

Run against the Unity FBX exchange file:
    blender -b --factory-startup -P this_script.py -- \
        --fbx-input LastShiftLimeAlien_Rigify_Test.fbx \
        --fbx-output LastShiftLimeAlien_Rigify_Test.fbx

Only DEF-thigh.L.001 and DEF-shin.L are redistributed. Their combined weight is
preserved per vertex, so unrelated influences and normalization remain intact.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import sys

import bpy


MESH_NAME = "LastShift_LimeAlien_RigifyMesh"
RIG_NAME = "rig"
THIGH_GROUP = "DEF-thigh.L.001"
SHIN_GROUP = "DEF-shin.L"

# The two adjacent DEF segments are 55 mm and 38 mm long. A 50 mm half-band
# spans the visible knee volume without reaching the hip or ankle transitions.
TRANSITION_HALF_WIDTH = 0.05
RADIAL_LIMIT = 0.12
MIN_PAIR_WEIGHT = 0.02
EPSILON = 1.0e-6


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--save-blend", action="store_true")
    parser.add_argument("--fbx-input")
    parser.add_argument("--fbx-output")
    parser.add_argument("--validate-only", action="store_true")
    return parser.parse_args(argv)


def smootherstep(value: float) -> float:
    value = min(1.0, max(0.0, value))
    return value**3 * (value * (value * 6.0 - 15.0) + 10.0)


def vertex_weight(vertex: bpy.types.MeshVertex, group_index: int) -> float:
    return next(
        (membership.weight for membership in vertex.groups if membership.group == group_index),
        0.0,
    )


def load_fbx(filepath: str) -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=os.path.abspath(filepath))


def patch_weights(validate_only: bool) -> dict[str, object]:
    mesh = bpy.data.objects.get(MESH_NAME)
    rig = bpy.data.objects.get(RIG_NAME)
    if mesh is None or mesh.type != "MESH":
        raise RuntimeError(f"Missing mesh: {MESH_NAME}")
    if rig is None or rig.type != "ARMATURE":
        raise RuntimeError(f"Missing armature: {RIG_NAME}")

    thigh_group = mesh.vertex_groups.get(THIGH_GROUP)
    shin_group = mesh.vertex_groups.get(SHIN_GROUP)
    thigh_bone = rig.data.bones.get(THIGH_GROUP)
    shin_bone = rig.data.bones.get(SHIN_GROUP)
    if None in (thigh_group, shin_group, thigh_bone, shin_bone):
        raise RuntimeError("Left-knee DEF bones or vertex groups are missing")

    joint = shin_bone.head_local.copy()
    axis = (shin_bone.tail_local - thigh_bone.head_local).normalized()
    rows: list[dict[str, float | int]] = []
    changed = 0
    max_delta = 0.0
    max_pair_error = 0.0
    influences_before = {
        vertex.index: sum(
            1 for membership in vertex.groups if membership.weight > EPSILON
        )
        for vertex in mesh.data.vertices
    }

    for vertex in mesh.data.vertices:
        thigh_before = vertex_weight(vertex, thigh_group.index)
        shin_before = vertex_weight(vertex, shin_group.index)
        pair_total = thigh_before + shin_before
        offset = vertex.co - joint
        longitudinal = offset.dot(axis)
        radial = (offset - axis * longitudinal).length

        if (
            pair_total <= MIN_PAIR_WEIGHT
            or abs(longitudinal) > TRANSITION_HALF_WIDTH
            or radial > RADIAL_LIMIT
        ):
            continue

        blend = smootherstep(
            (longitudinal + TRANSITION_HALF_WIDTH)
            / (2.0 * TRANSITION_HALF_WIDTH)
        )
        thigh_after = pair_total * (1.0 - blend)
        shin_after = pair_total * blend
        delta = max(abs(thigh_after - thigh_before), abs(shin_after - shin_before))

        if not validate_only and delta > EPSILON:
            thigh_group.add([vertex.index], thigh_after, "REPLACE")
            shin_group.add([vertex.index], shin_after, "REPLACE")
            changed += 1

        max_delta = max(max_delta, delta)
        max_pair_error = max(max_pair_error, abs(pair_total - thigh_after - shin_after))
        rows.append(
            {
                "index": vertex.index,
                "longitudinal": longitudinal,
                "pair_total": pair_total,
                "before_ratio": shin_before / pair_total,
                "target_ratio": blend,
            }
        )

    if not rows:
        raise RuntimeError("No vertices found in the left-knee transition band")
    if max_pair_error > EPSILON:
        raise RuntimeError(f"Pair-weight preservation failed: {max_pair_error}")

    influences_after = {
        vertex.index: sum(
            1 for membership in vertex.groups if membership.weight > EPSILON
        )
        for vertex in mesh.data.vertices
    }
    max_influences_before = max(influences_before.values())
    max_influences_after = max(influences_after.values())
    vertices_with_new_influence = sum(
        influences_after[index] > count
        for index, count in influences_before.items()
    )
    max_influence_growth = max(
        influences_after[index] - count
        for index, count in influences_before.items()
    )
    if max_influences_after > max_influences_before or max_influence_growth > 1:
        raise RuntimeError(
            "Knee patch worsened the existing influence ceiling: "
            f"{max_influences_before} -> {max_influences_after}"
        )

    rows.sort(key=lambda row: float(row["longitudinal"]))
    monotonic_violations = sum(
        float(current["target_ratio"]) + EPSILON < float(previous["target_ratio"])
        for previous, current in zip(rows, rows[1:])
    )
    if monotonic_violations:
        raise RuntimeError(f"Non-monotonic knee transition: {monotonic_violations}")

    return {
        "mesh": MESH_NAME,
        "groups": [THIGH_GROUP, SHIN_GROUP],
        "eligible_vertices": len(rows),
        "changed_vertices": changed,
        "transition_half_width_m": TRANSITION_HALF_WIDTH,
        "radial_limit_m": RADIAL_LIMIT,
        "max_weight_delta": max_delta,
        "max_pair_sum_error": max_pair_error,
        "max_influences_before": max_influences_before,
        "max_influences_after": max_influences_after,
        "vertices_with_new_influence": vertices_with_new_influence,
        "monotonic_violations": monotonic_violations,
        "joint": list(joint),
        "axis": list(axis),
    }


def save_fbx(filepath: str) -> None:
    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
    bpy.ops.object.select_all(action="DESELECT")
    export_objects = [
        obj for obj in bpy.context.scene.objects if obj.type in {"ARMATURE", "MESH"}
    ]
    for obj in export_objects:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
        obj.select_set(True)
    armature = next((obj for obj in export_objects if obj.type == "ARMATURE"), None)
    if armature is None:
        raise RuntimeError("No armature available for FBX export")
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=os.path.abspath(filepath),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        use_armature_deform_only=False,
        bake_anim=False,
        path_mode="AUTO",
    )


def main() -> None:
    args = parse_args()
    if args.fbx_input:
        load_fbx(args.fbx_input)

    report = patch_weights(args.validate_only)

    if args.save_blend and not args.validate_only:
        bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
    if args.fbx_output and not args.validate_only:
        save_fbx(args.fbx_output)

    report["blend_saved"] = bool(args.save_blend and not args.validate_only)
    report["fbx_output"] = os.path.abspath(args.fbx_output) if args.fbx_output else None
    print("ADK_KNEE_WEIGHT_REPORT:" + json.dumps(report, sort_keys=True))


if __name__ == "__main__":
    main()
