"""Render a deterministic two-step subdivision preview for the lime alien.

The loaded canonical ``.blend`` is treated as read-only.  Three temporary
copies (base, Catmull-Clark level 1, and level 2) are arranged side by side,
rendered, measured, and then discarded when Blender exits.

Usage::

    blender -b LastShiftLimeAlien_UnityExport_LeftToeFixed.blend \
        -P render_lime_alien_subdivision_preview.py -- \
        --output-dir docs/art/evidence/last-shift-lime-alien-subdivision-preview
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


BODY_NAME = "LastShift_LimeAlien_Body"
EYES_NAME = "LastShift_LimeAlien_Eyes"
RIG_NAME = "LastShift_LimeAlien_Rig"
CANONICAL_SOURCE = (
    "ArtSource/Characters/LastShiftLimeAlien/"
    "LastShiftLimeAlien_UnityExport_LeftToeFixed.blend"
)
STAGES = (
    ("BASE", 0, (0.34, 0.58, 0.13, 1.0)),
    ("JELLY 1", 1, (0.42, 0.78, 0.16, 1.0)),
    ("JELLY 2", 2, (0.52, 0.95, 0.22, 1.0)),
)


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--blend-output", type=Path)
    return parser.parse_args(raw)


def mesh_digest(obj: bpy.types.Object) -> str:
    digest = hashlib.sha256()
    for vertex in obj.data.vertices:
        digest.update(f"{vertex.co.x:.9f},{vertex.co.y:.9f},{vertex.co.z:.9f};".encode())
    for polygon in obj.data.polygons:
        digest.update((",".join(map(str, polygon.vertices)) + ";").encode())
    return digest.hexdigest()


def require_scene() -> tuple[bpy.types.Object, bpy.types.Object, bpy.types.Object]:
    body = bpy.data.objects.get(BODY_NAME)
    eyes = bpy.data.objects.get(EYES_NAME)
    rig = bpy.data.objects.get(RIG_NAME)
    if body is None or body.type != "MESH":
        raise RuntimeError(f"Missing canonical body: {BODY_NAME}")
    if eyes is None or eyes.type != "MESH":
        raise RuntimeError(f"Missing canonical eyes: {EYES_NAME}")
    if rig is None or rig.type != "ARMATURE":
        raise RuntimeError(f"Missing canonical rig: {RIG_NAME}")
    return body, eyes, rig


def evaluated_metrics(obj: bpy.types.Object) -> dict[str, int]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=depsgraph)
    try:
        mesh.calc_loop_triangles()
        return {
            "vertices": len(mesh.vertices),
            "edges": len(mesh.edges),
            "polygons": len(mesh.polygons),
            "triangles": len(mesh.loop_triangles),
        }
    finally:
        evaluated.to_mesh_clear()


def duplicate_materials(
    obj: bpy.types.Object, stage_name: str, lime_color: tuple[float, float, float, float]
) -> None:
    for index, slot in enumerate(obj.material_slots):
        material = slot.material
        if material is None:
            continue
        copied = material.copy()
        copied.name = f"PREVIEW_{stage_name}_{material.name}"
        lower_name = material.name.lower()
        if "lime" in lower_name or "alien" in lower_name or "uniform" in lower_name:
            copied.diffuse_color = lime_color
        obj.material_slots[index].material = copied


def add_text(
    collection: bpy.types.Collection,
    body: str,
    location: Vector,
    size: float,
    color: tuple[float, float, float, float],
) -> bpy.types.Object:
    curve = bpy.data.curves.new(f"PREVIEW_Text_{body}", "FONT")
    curve.body = body
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    curve.size = size
    curve.extrude = size * 0.008
    material = bpy.data.materials.new(f"PREVIEW_TextMat_{body}")
    material.diffuse_color = color
    curve.materials.append(material)
    obj = bpy.data.objects.new(f"PREVIEW_Label_{body}", curve)
    collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler.x = math.radians(90.0)
    return obj


def world_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def set_camera_to_bounds(
    camera: bpy.types.Object,
    minimum: Vector,
    maximum: Vector,
    *,
    horizontal_padding: float = 1.12,
    vertical_padding: float = 1.26,
) -> None:
    center = (minimum + maximum) * 0.5
    width = maximum.x - minimum.x
    height = maximum.z - minimum.z
    aspect = bpy.context.scene.render.resolution_x / bpy.context.scene.render.resolution_y
    camera.data.ortho_scale = max(height * vertical_padding, width / aspect * horizontal_padding)
    camera.location = Vector((center.x, minimum.y - 8.0, center.z))
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()


def render(
    path: Path,
    camera: bpy.types.Object,
    bodies: list[bpy.types.Object],
    eyes: list[bpy.types.Object],
    labels: list[bpy.types.Object],
    *,
    turntable_degrees: float,
    fit_camera: bool,
    show_labels: bool,
) -> None:
    for body, eye in zip(bodies, eyes):
        body.rotation_euler.z = math.radians(turntable_degrees)
        eye.rotation_euler.z = math.radians(turntable_degrees)
    for label in labels:
        label.hide_render = not show_labels
    bpy.context.view_layer.update()

    if fit_camera:
        minimum, maximum = world_bounds(bodies + eyes + labels)
        set_camera_to_bounds(camera, minimum, maximum)

    scene = bpy.context.scene
    scene.camera = camera
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "paint.sl"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "BOTH"
    scene.display.shading.curvature_ridge_factor = 1.35
    scene.display.shading.curvature_valley_factor = 1.15
    scene.display.shading.background_type = "VIEWPORT"
    scene.display.shading.background_color = (0.015, 0.022, 0.038)
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.filepath = str(path.resolve())
    bpy.ops.render.render(write_still=True)


def main() -> None:
    args = parse_args()
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    body, source_eyes, rig = require_scene()
    digest_before = mesh_digest(body)
    source_modifiers = [(modifier.name, modifier.type) for modifier in body.modifiers]
    rig_position = rig.data.pose_position
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()

    preview_collection = bpy.data.collections.new("LimeAlien_Subdivision_Preview")
    bpy.context.scene.collection.children.link(preview_collection)
    for obj in bpy.context.scene.objects:
        obj.hide_render = True
        obj.hide_viewport = True
        if obj.name in bpy.context.view_layer.objects:
            obj.hide_set(True)

    source_min, source_max = world_bounds([body, source_eyes])
    source_width = source_max.x - source_min.x
    source_height = source_max.z - source_min.z
    source_center_x = (source_min.x + source_max.x) * 0.5
    source_center_z = (source_min.z + source_max.z) * 0.5
    spacing = source_width * 1.62

    bodies: list[bpy.types.Object] = []
    eyes: list[bpy.types.Object] = []
    labels: list[bpy.types.Object] = []
    metrics: list[dict[str, object]] = []

    for index, (stage_name, level, color) in enumerate(STAGES):
        offset_x = (index - 1) * spacing - source_center_x

        stage_body = body.copy()
        stage_body.data = body.data.copy()
        stage_body.name = f"PREVIEW_{stage_name.replace(' ', '_')}_Body"
        preview_collection.objects.link(stage_body)
        stage_body.location.x += offset_x
        stage_body.hide_render = False
        stage_body.hide_viewport = False
        stage_body.hide_set(False)
        stage_body.animation_data_clear()
        stage_body.parent = None
        stage_body.matrix_parent_inverse.identity()
        stage_body.modifiers.clear()
        duplicate_materials(stage_body, stage_name, color)
        if level:
            modifier = stage_body.modifiers.new(f"Jelly_Subdivision_{level}", "SUBSURF")
            modifier.subdivision_type = "CATMULL_CLARK"
            modifier.levels = level
            modifier.render_levels = level
            modifier.show_only_control_edges = True

        stage_eyes = source_eyes.copy()
        stage_eyes.data = source_eyes.data.copy()
        stage_eyes.name = f"PREVIEW_{stage_name.replace(' ', '_')}_Eyes"
        preview_collection.objects.link(stage_eyes)
        stage_eyes.location.x += offset_x
        stage_eyes.hide_render = False
        stage_eyes.hide_viewport = False
        stage_eyes.hide_set(False)
        stage_eyes.animation_data_clear()
        stage_eyes.parent = None
        stage_eyes.matrix_parent_inverse.identity()
        stage_eyes.modifiers.clear()

        bpy.context.view_layer.update()
        stage_metrics = evaluated_metrics(stage_body)
        stage_metrics.update({"name": stage_name, "subdivision_level": level})
        metrics.append(stage_metrics)
        bodies.append(stage_body)
        eyes.append(stage_eyes)

        label_x = (index - 1) * spacing
        title = add_text(
            preview_collection,
            stage_name.replace("JELLY ", "JELLY L"),
            Vector((label_x, source_min.y - source_width * 0.18, source_min.z - source_height * 0.13)),
            source_height * 0.055,
            (0.88, 0.94, 1.0, 1.0),
        )
        detail = add_text(
            preview_collection,
            f"SUBD {level}  /  {stage_metrics['triangles'] / 1000.0:.1f}K TRI",
            Vector((label_x, source_min.y - source_width * 0.18, source_min.z - source_height * 0.21)),
            source_height * 0.026,
            (0.50, 0.62, 0.74, 1.0),
        )
        labels.extend((title, detail))

    camera_data = bpy.data.cameras.new("PREVIEW_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("PREVIEW_Camera", camera_data)
    preview_collection.objects.link(camera)
    camera.hide_render = False

    front = output_dir / "lime-alien-subdivision-front.png"
    oblique = output_dir / "lime-alien-subdivision-oblique.png"
    render(
        front,
        camera,
        bodies,
        eyes,
        labels,
        turntable_degrees=0.0,
        fit_camera=True,
        show_labels=True,
    )
    render(
        oblique,
        camera,
        bodies,
        eyes,
        labels,
        turntable_degrees=24.0,
        fit_camera=False,
        show_labels=False,
    )

    rig.data.pose_position = rig_position
    digest_after = mesh_digest(body)
    modifiers_after = [(modifier.name, modifier.type) for modifier in body.modifiers]
    if digest_after != digest_before:
        raise RuntimeError("Canonical body data changed while generating preview")
    if modifiers_after != source_modifiers:
        raise RuntimeError("Canonical body modifier stack changed while generating preview")

    blend_output = None
    if args.blend_output:
        for stage_body, stage_eyes in zip(bodies, eyes):
            stage_body.rotation_euler.z = 0.0
            stage_eyes.rotation_euler.z = 0.0
        for label in labels:
            label.hide_render = False
        bpy.context.view_layer.update()
        blend_output = args.blend_output.resolve()
        blend_output.parent.mkdir(parents=True, exist_ok=True)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(
            filepath=str(blend_output), check_existing=False, compress=True
        )

    report = {
        "source": CANONICAL_SOURCE,
        "canonical_body": BODY_NAME,
        "source_digest_before": digest_before,
        "source_digest_after": digest_after,
        "source_unchanged": True,
        "stages": metrics,
        "renders": [front.name, oblique.name],
        "blend_output": args.blend_output.as_posix() if blend_output else None,
        "decision_note": (
            "Level 1 is the production candidate. Level 2 is a silhouette reference only; "
            "its approximately 22x triangle expansion is not a runtime recommendation."
        ),
    }
    report_path = output_dir / "report.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ADK_SUBDIVISION_REPORT=" + json.dumps(report, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
