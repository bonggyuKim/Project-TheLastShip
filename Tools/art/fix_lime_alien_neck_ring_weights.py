"""Rebuild the LAST SHIFT lime alien neck-ring transition weights.

The Rigify controls measured for this asset respond to a head bend at 0%, 50%,
and 100% on DEF-spine.004, .005, and .006.  The existing weights hand off in
two linear segments.  This tool replaces that cusp with the quadratic
Bernstein basis while preserving each vertex's total neck influence and every
non-target weight.

Run with Blender 4.5 LTS, for example:
  blender -b LastShiftLimeAlien_Rigify_Test.blend --python this_script.py -- \
    --mode apply --output LastShiftLimeAlien_Rigify_Test_NeckRingFixed.blend
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Quaternion, Vector


MESH_NAME = "LastShift_LimeAlien_RigifyMesh"
RIG_NAME = "rig"
CONTROL_NAME = "head"
TARGETS = ("DEF-spine.004", "DEF-spine.005", "DEF-spine.006")
RESPONSE = np.asarray((0.0, 0.5, 1.0), dtype=np.float64)
EPSILON = 1.0e-8
STRESS_DEGREES = 60.0


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=("inspect", "apply"), default="inspect")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--evidence-dir", type=Path)
    return parser.parse_args(raw)


def group_array(obj: bpy.types.Object, name: str) -> np.ndarray:
    group = obj.vertex_groups.get(name)
    if group is None:
        raise RuntimeError(f"Missing vertex group: {name}")
    values = np.zeros(len(obj.data.vertices), dtype=np.float64)
    for vertex in obj.data.vertices:
        for membership in vertex.groups:
            if membership.group == group.index:
                values[vertex.index] = membership.weight
                break
    return values


def target_arrays(obj: bpy.types.Object) -> np.ndarray:
    return np.column_stack([group_array(obj, name) for name in TARGETS])


def non_target_digest(obj: bpy.types.Object) -> str:
    names = {group.index: group.name for group in obj.vertex_groups}
    digest = hashlib.sha256()
    for vertex in obj.data.vertices:
        for membership in sorted(vertex.groups, key=lambda item: item.group):
            name = names[membership.group]
            if name in TARGETS:
                continue
            digest.update(f"{vertex.index}:{name}:{membership.weight:.9f};".encode())
    return digest.hexdigest()


def build_solution(
    weights: np.ndarray,
) -> tuple[np.ndarray, np.ndarray, np.ndarray, dict[str, object]]:
    total = weights.sum(axis=1)
    ratios = np.divide(
        weights,
        total[:, None],
        out=np.zeros_like(weights),
        where=total[:, None] > EPSILON,
    )
    # The measured effective head response is the dot product of these ratios
    # with (0, .5, 1).  Keep that response exactly, but use one C1-continuous
    # three-bone curve instead of the old two-segment hand-off.
    response = ratios @ RESPONSE
    basis = np.column_stack(
        (
            (1.0 - response) ** 2,
            2.0 * response * (1.0 - response),
            response**2,
        )
    )
    solution = basis * total[:, None]
    changed = np.max(np.abs(solution - weights), axis=1) > 1.0e-7
    transition = (total > EPSILON) & (response > EPSILON) & (response < 1.0 - EPSILON)
    report: dict[str, object] = {
        "response_model": dict(zip(TARGETS, RESPONSE.tolist())),
        "basis": {
            TARGETS[0]: "(1-t)^2",
            TARGETS[1]: "2t(1-t)",
            TARGETS[2]: "t^2",
        },
        "transition_vertices": int(transition.sum()),
        "changed_vertices": int(changed.sum()),
        "target_totals_before": dict(zip(TARGETS, weights.sum(axis=0).tolist())),
        "target_totals_after": dict(zip(TARGETS, solution.sum(axis=0).tolist())),
        "per_vertex_target_total_delta_max": float(
            np.max(np.abs(solution.sum(axis=1) - total))
        ),
        "effective_response_delta_max": float(
            np.max(
                np.abs(
                    np.divide(
                        solution @ RESPONSE,
                        total,
                        out=np.zeros_like(total),
                        where=total > EPSILON,
                    )
                    - response
                )
            )
        ),
    }
    return solution, changed, response, report


def apply_solution(
    obj: bpy.types.Object, solution: np.ndarray, changed: np.ndarray
) -> None:
    indices = np.flatnonzero(changed).tolist()
    for column, name in enumerate(TARGETS):
        group = obj.vertex_groups[name]
        group.remove(indices)
        for index in np.flatnonzero(changed & (solution[:, column] > EPSILON)):
            group.add([int(index)], float(solution[index, column]), "REPLACE")


def evaluated_coordinates(obj: bpy.types.Object) -> np.ndarray:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    evaluated_mesh = evaluated.to_mesh(
        preserve_all_data_layers=True, depsgraph=depsgraph
    )
    try:
        return np.asarray(
            [tuple(vertex.co) for vertex in evaluated_mesh.vertices],
            dtype=np.float64,
        )
    finally:
        evaluated.to_mesh_clear()


def transition_edges(
    obj: bpy.types.Object, total: np.ndarray, response: np.ndarray
) -> np.ndarray:
    edges = np.asarray([tuple(edge.vertices) for edge in obj.data.edges], dtype=np.int64)
    coordinates = np.asarray([tuple(vertex.co) for vertex in obj.data.vertices])
    midpoint_response = response[edges].mean(axis=1)
    midpoint_z = coordinates[edges, 2].mean(axis=1)
    selected = (
        (total[edges].min(axis=1) > 0.5)
        & (midpoint_response > 0.02)
        & (midpoint_response < 0.98)
        & (midpoint_z > 0.42)
        & (midpoint_z < 0.60)
    )
    result = edges[selected]
    if len(result) < 100:
        raise RuntimeError(f"Neck transition selection is unexpectedly small: {len(result)}")
    return result


def stress_metrics(
    obj: bpy.types.Object,
    rig: bpy.types.Object,
    edges: np.ndarray,
) -> dict[str, dict[str, float | int]]:
    control = rig.pose.bones.get(CONTROL_NAME)
    if control is None:
        raise RuntimeError(f"Missing pose control: {CONTROL_NAME}")
    original_mode = control.rotation_mode
    original_quaternion = control.rotation_quaternion.copy()
    try:
        control.rotation_mode = "QUATERNION"
        control.rotation_quaternion = Quaternion()
        bpy.context.view_layer.update()
        neutral = evaluated_coordinates(obj)
        base_lengths = np.linalg.norm(
            neutral[edges[:, 0]] - neutral[edges[:, 1]], axis=1
        )
        if np.any(base_lengths <= EPSILON):
            raise RuntimeError("Degenerate edge in neck stress selection")
        results: dict[str, dict[str, float | int]] = {}
        for axis_name, axis in (
            ("X", (1.0, 0.0, 0.0)),
            ("Y", (0.0, 1.0, 0.0)),
            ("Z", (0.0, 0.0, 1.0)),
        ):
            control.rotation_quaternion = Quaternion(
                axis, math.radians(STRESS_DEGREES)
            )
            bpy.context.view_layer.update()
            posed = evaluated_coordinates(obj)
            ratios = np.linalg.norm(
                posed[edges[:, 0]] - posed[edges[:, 1]], axis=1
            ) / base_lengths
            results[axis_name] = {
                "edges": int(len(edges)),
                "max_stretch_ratio": float(ratios.max()),
                "min_stretch_ratio": float(ratios.min()),
                "p95_absolute_strain": float(np.quantile(np.abs(ratios - 1.0), 0.95)),
                "mean_absolute_strain": float(np.mean(np.abs(ratios - 1.0))),
                "edges_over_1p5x": int(np.sum(ratios > 1.5)),
                "edges_under_0p67x": int(np.sum(ratios < 0.67)),
            }
        return results
    finally:
        control.rotation_quaternion = original_quaternion
        control.rotation_mode = original_mode
        bpy.context.view_layer.update()


def look_at(camera: bpy.types.Object, target: Vector) -> None:
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def render_evidence(
    obj: bpy.types.Object,
    rig: bpy.types.Object,
    path: Path,
    *,
    weight_map: bool,
    pose_axis: str | None,
) -> None:
    scene = bpy.context.scene
    control = rig.pose.bones[CONTROL_NAME]
    old_mode = control.rotation_mode
    old_quaternion = control.rotation_quaternion.copy()
    old_camera = scene.camera
    old_engine = scene.render.engine
    old_filepath = scene.render.filepath
    old_resolution = (
        scene.render.resolution_x,
        scene.render.resolution_y,
        scene.render.resolution_percentage,
    )
    old_transparent = scene.render.film_transparent
    old_shading = (
        scene.display.shading.light,
        scene.display.shading.color_type,
        scene.display.shading.show_shadows,
        scene.display.shading.show_cavity,
    )
    hidden = {item.name: item.hide_render for item in bpy.data.objects}
    camera_data = bpy.data.cameras.new("ADK_NeckEvidence_Camera")
    camera = bpy.data.objects.new("ADK_NeckEvidence_Camera", camera_data)
    scene.collection.objects.link(camera)
    color_attribute = None
    try:
        for item in bpy.data.objects:
            item.hide_render = not (
                item.type == "MESH"
                and item.name in {MESH_NAME, "Eye_Pupil_Rigify"}
            )
        if weight_map:
            for item in bpy.data.objects:
                item.hide_render = item != obj
            arrays = target_arrays(obj)
            totals = arrays.sum(axis=1)
            colors = np.divide(
                arrays,
                totals[:, None],
                out=np.zeros_like(arrays),
                where=totals[:, None] > EPSILON,
            )
            color_attribute = obj.data.color_attributes.new(
                name="ADK_NeckWeightEvidence", type="FLOAT_COLOR", domain="CORNER"
            )
            obj.data.color_attributes.active_color = color_attribute
            for polygon in obj.data.polygons:
                for loop_index in polygon.loop_indices:
                    vertex_index = obj.data.loops[loop_index].vertex_index
                    color_attribute.data[loop_index].color = (
                        *colors[vertex_index],
                        1.0,
                    )

        scene.camera = camera
        camera_data.type = "ORTHO"
        camera_data.ortho_scale = 0.44 if weight_map else 0.58
        camera.location = Vector((1.0, -0.06, 0.55))
        look_at(camera, Vector((0.0, -0.06, 0.55)))
        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.shading.light = "STUDIO"
        scene.display.shading.color_type = "VERTEX" if weight_map else "MATERIAL"
        scene.display.shading.show_shadows = not weight_map
        scene.display.shading.show_cavity = True
        scene.render.resolution_x = 640
        scene.render.resolution_y = 640
        scene.render.resolution_percentage = 100
        scene.render.film_transparent = False
        scene.render.image_settings.file_format = "PNG"
        scene.render.filepath = str(path.resolve())
        path.parent.mkdir(parents=True, exist_ok=True)

        control.rotation_mode = "QUATERNION"
        control.rotation_quaternion = Quaternion()
        if pose_axis is not None:
            axis = {
                "X": (1.0, 0.0, 0.0),
                "Y": (0.0, 1.0, 0.0),
                "Z": (0.0, 0.0, 1.0),
            }[pose_axis]
            control.rotation_quaternion = Quaternion(
                axis, math.radians(STRESS_DEGREES)
            )
        bpy.context.view_layer.update()
        bpy.ops.render.render(write_still=True)
    finally:
        control.rotation_quaternion = old_quaternion
        control.rotation_mode = old_mode
        scene.camera = old_camera
        scene.render.engine = old_engine
        scene.render.filepath = old_filepath
        (
            scene.render.resolution_x,
            scene.render.resolution_y,
            scene.render.resolution_percentage,
        ) = old_resolution
        scene.render.film_transparent = old_transparent
        (
            scene.display.shading.light,
            scene.display.shading.color_type,
            scene.display.shading.show_shadows,
            scene.display.shading.show_cavity,
        ) = old_shading
        for name, state in hidden.items():
            if name in bpy.data.objects:
                bpy.data.objects[name].hide_render = state
        if color_attribute is not None:
            obj.data.color_attributes.remove(color_attribute)
        bpy.data.objects.remove(camera, do_unlink=True)
        bpy.data.cameras.remove(camera_data)
        bpy.context.view_layer.update()


def main() -> None:
    args = parse_args()
    obj = bpy.data.objects.get(MESH_NAME)
    rig = bpy.data.objects.get(RIG_NAME)
    if obj is None or obj.type != "MESH":
        raise RuntimeError(f"Missing mesh: {MESH_NAME}")
    if rig is None or rig.type != "ARMATURE":
        raise RuntimeError(f"Missing rig: {RIG_NAME}")
    missing = [name for name in TARGETS if obj.vertex_groups.get(name) is None]
    if missing:
        raise RuntimeError(f"Missing target groups: {missing}")

    weights_before = target_arrays(obj)
    total_before = weights_before.sum(axis=1)
    solution, changed, response, solution_report = build_solution(weights_before)
    edges = transition_edges(obj, total_before, response)
    vertices_before = np.asarray([tuple(vertex.co) for vertex in obj.data.vertices])
    non_target_before = non_target_digest(obj)
    report: dict[str, object] = {
        "source": bpy.data.filepath,
        "mode": args.mode,
        "mesh": MESH_NAME,
        "rig": RIG_NAME,
        "control": CONTROL_NAME,
        "stress_degrees": STRESS_DEGREES,
        "solution": solution_report,
        "stress_before": stress_metrics(obj, rig, edges),
    }

    if args.mode == "apply":
        if args.output is None:
            raise RuntimeError("--output is required for apply mode")
        if args.evidence_dir:
            evidence = args.evidence_dir.expanduser().resolve()
            render_evidence(
                obj,
                rig,
                evidence / "neck-weights-before.png",
                weight_map=True,
                pose_axis=None,
            )
            render_evidence(
                obj,
                rig,
                evidence / "neck-bend-before.png",
                weight_map=False,
                pose_axis="X",
            )

        apply_solution(obj, solution, changed)
        weights_after = target_arrays(obj)
        vertices_after = np.asarray([tuple(vertex.co) for vertex in obj.data.vertices])
        non_target_after = non_target_digest(obj)
        target_total_delta = np.max(
            np.abs(weights_after.sum(axis=1) - weights_before.sum(axis=1))
        )
        if target_total_delta > 1.0e-6:
            raise RuntimeError(f"Per-vertex target total changed: {target_total_delta}")
        if non_target_before != non_target_after:
            raise RuntimeError("A non-target vertex-group weight changed; refusing to save")
        if not np.array_equal(vertices_before, vertices_after):
            raise RuntimeError("Mesh positions changed; refusing to save")

        stress_after = stress_metrics(obj, rig, edges)
        for axis in ("X", "Y", "Z"):
            if (
                stress_after[axis]["max_stretch_ratio"]
                > report["stress_before"][axis]["max_stretch_ratio"] + 1.0e-6
            ):
                raise RuntimeError(f"{axis}-axis maximum neck stretch regressed")
            if (
                stress_after[axis]["mean_absolute_strain"]
                > report["stress_before"][axis]["mean_absolute_strain"] + 1.0e-6
            ):
                raise RuntimeError(f"{axis}-axis mean neck strain regressed")

        if args.evidence_dir:
            render_evidence(
                obj,
                rig,
                evidence / "neck-weights-after.png",
                weight_map=True,
                pose_axis=None,
            )
            render_evidence(
                obj,
                rig,
                evidence / "neck-bend-after.png",
                weight_map=False,
                pose_axis="X",
            )

        output = args.output.expanduser().resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(output), check_existing=False)
        report.update(
            {
                "output": str(output),
                "stress_after": stress_after,
                "per_vertex_target_total_delta_max_after_apply": float(
                    target_total_delta
                ),
                "non_target_weights_unchanged": True,
                "mesh_positions_unchanged": True,
            }
        )

    if args.report:
        report_path = args.report.expanduser().resolve()
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(
            json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8"
        )
    print(
        "NECK_RING_WEIGHT_FIX="
        + json.dumps(report, ensure_ascii=False, sort_keys=True),
        flush=True,
    )


if __name__ == "__main__":
    main()
