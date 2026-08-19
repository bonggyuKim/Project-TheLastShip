"""Remove the isolated lime-colored triangle exposed inside the alien mouth.

Run against the Blender source::

    blender -b LastShiftLimeAlien_Rigify_Test.blend -P this_script.py -- --save-blend

Run against the Unity FBX exchange file::

    blender -b --factory-startup -P this_script.py -- \
        --fbx-input LastShiftLimeAlien_Rigify_Test.fbx \
        --fbx-output LastShiftLimeAlien_Rigify_Test.fbx

The target is deliberately identified by topology and a tight local-space
location window. No connected skin surface, bone weights, or materials change.
"""

from __future__ import annotations

import argparse
import json
import os
import sys

import bmesh
import bpy


MESH_NAME = "LastShift_LimeAlien_RigifyMesh"
RIG_NAME = "rig"
EYE_NAME = "Eye_Pupil_Rigify"
EXPORT_COLLECTION_NAME = "CHARACTER_EXPORT"

TARGET_CENTER = (-0.014025, -0.211263, 0.577113)
TARGET_TOLERANCE = (0.02, 0.02, 0.015)


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--save-blend", action="store_true")
    parser.add_argument("--fbx-input")
    parser.add_argument("--fbx-output")
    parser.add_argument("--validate-only", action="store_true")
    return parser.parse_args(argv)


def load_fbx(filepath: str) -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=os.path.abspath(filepath))


def in_target_window(center) -> bool:
    return all(
        abs(float(center[axis]) - TARGET_CENTER[axis]) <= TARGET_TOLERANCE[axis]
        for axis in range(3)
    )


def find_artifact_faces(mesh) -> list[int]:
    vertex_face_counts = [0] * len(mesh.vertices)
    for polygon in mesh.polygons:
        for vertex_index in polygon.vertices:
            vertex_face_counts[vertex_index] += 1

    matches = []
    for polygon in mesh.polygons:
        if len(polygon.vertices) != 3:
            continue
        if any(vertex_face_counts[index] != 1 for index in polygon.vertices):
            continue
        center = sum((mesh.vertices[index].co for index in polygon.vertices), start=mesh.vertices[polygon.vertices[0]].co.copy() * 0.0) / 3.0
        if in_target_window(center):
            matches.append(polygon.index)
    return matches


def remove_artifact(validate_only: bool) -> dict[str, object]:
    obj = bpy.data.objects.get(MESH_NAME)
    if obj is None or obj.type != "MESH":
        raise RuntimeError(f"Missing mesh object: {MESH_NAME}")

    mesh = obj.data
    before = {
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "polygons": len(mesh.polygons),
    }
    matches = find_artifact_faces(mesh)
    if len(matches) > 1:
        raise RuntimeError(f"Ambiguous mouth artifacts: {matches}")
    if validate_only and matches:
        raise RuntimeError(f"Mouth artifact is still present: {matches}")

    removed = False
    removed_face_index = matches[0] if matches else None
    if matches and not validate_only:
        bm = bmesh.new()
        try:
            bm.from_mesh(mesh)
            bm.faces.ensure_lookup_table()
            target_face = bm.faces[matches[0]]
            target_vertices = list(target_face.verts)
            if any(len(vertex.link_faces) != 1 for vertex in target_vertices):
                raise RuntimeError("Target triangle is no longer isolated")
            bmesh.ops.delete(bm, geom=target_vertices, context="VERTS")
            bm.to_mesh(mesh)
            mesh.validate(verbose=True)
            mesh.update(calc_edges=True)
            removed = True
        finally:
            bm.free()

    remaining = find_artifact_faces(mesh)
    after = {
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "polygons": len(mesh.polygons),
    }
    if not validate_only and (remaining or not removed):
        raise RuntimeError(
            f"Mouth artifact removal failed: removed={removed}, remaining={remaining}"
        )

    return {
        "mesh": obj.name,
        "before": before,
        "after": after,
        "removed": removed,
        "removed_face_index": removed_face_index,
        "remaining_artifact_faces": remaining,
    }


def save_fbx(filepath: str) -> None:
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    export_collection = bpy.data.collections.get(EXPORT_COLLECTION_NAME)
    names = (RIG_NAME, MESH_NAME, EYE_NAME)
    # Blender's FBX importer flattens the source collection hierarchy. Exact
    # object names remain stable, so imported exchange files use that fallback.
    object_lookup = export_collection.objects if export_collection else bpy.data.objects
    export_objects = [object_lookup.get(name) for name in names]
    if any(obj is None for obj in export_objects):
        missing = [name for name, obj in zip(names, export_objects) if obj is None]
        raise RuntimeError(f"Missing FBX export objects: {missing}")

    for obj in export_objects:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
        obj.select_set(True)
    armature = object_lookup[RIG_NAME]
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

    report = remove_artifact(args.validate_only)

    if args.save_blend and not args.validate_only:
        bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
    if args.fbx_output and not args.validate_only:
        save_fbx(args.fbx_output)

    report["blend_saved"] = bool(args.save_blend and not args.validate_only)
    report["fbx_output"] = os.path.abspath(args.fbx_output) if args.fbx_output else None
    print("ADK_MOUTH_ARTIFACT_REPORT:" + json.dumps(report, sort_keys=True))


if __name__ == "__main__":
    main()
