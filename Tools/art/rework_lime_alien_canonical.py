"""Inspect and rework the canonical LAST SHIFT lime-alien skin.

The canonical source is ``LastShiftLimeAlien_UnityExport_LeftToeFixed.blend``.
This tool deliberately targets the renamed 5,711-vertex body and 232-bone rig;
the older ``Rigify_Test`` objects that still live in the authoring scene are
never selected or exported.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Quaternion, Vector


BODY_NAME = "LastShift_LimeAlien_Body"
EYES_NAME = "LastShift_LimeAlien_Eyes"
RIG_NAME = "LastShift_LimeAlien_Rig"
NECK_BONE = "DEF-spine.006"
NECK_GROUPS = ("DEF-spine.003", "DEF-spine.004", "DEF-spine.005", NECK_BONE)
MIN_REST_EDGE = 0.003
TORN_RATIO = 3.0
POSE_ANGLES = (20, 46, 90)


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=("inspect", "apply"), default="inspect")
    parser.add_argument("--report", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--rings", type=int, default=4)
    parser.add_argument("--iterations", type=int, default=20)
    parser.add_argument("--strength", type=float, default=0.6)
    parser.add_argument("--evidence-dir", type=Path)
    parser.add_argument("--fbx-output", action="append", type=Path, default=[])
    return parser.parse_args(raw)


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
    modifiers = [m.object for m in body.modifiers if m.type == "ARMATURE"]
    if modifiers != [rig]:
        raise RuntimeError(f"Canonical body armature mismatch: {modifiers}")
    return body, eyes, rig


def evaluated_coordinates(obj: bpy.types.Object) -> np.ndarray:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    evaluated_mesh = evaluated.to_mesh(
        preserve_all_data_layers=True, depsgraph=depsgraph
    )
    try:
        return np.asarray(
            [tuple(vertex.co) for vertex in evaluated_mesh.vertices], dtype=np.float64
        )
    finally:
        evaluated.to_mesh_clear()


def triangles(mesh: bpy.types.Mesh) -> np.ndarray:
    mesh.calc_loop_triangles()
    return np.asarray(
        [tuple(item.vertices) for item in mesh.loop_triangles], dtype=np.int64
    )


def edge_metrics(
    rest: np.ndarray, posed: np.ndarray, tris: np.ndarray
) -> tuple[np.ndarray, np.ndarray]:
    edges = np.stack((tris, np.roll(tris, -1, axis=1)), axis=-1)
    rest_lengths = np.linalg.norm(
        rest[edges[:, :, 0]] - rest[edges[:, :, 1]], axis=2
    )
    posed_lengths = np.linalg.norm(
        posed[edges[:, :, 0]] - posed[edges[:, :, 1]], axis=2
    )
    ratios = np.divide(
        posed_lengths,
        rest_lengths,
        out=np.zeros_like(posed_lengths),
        where=rest_lengths >= MIN_REST_EDGE,
    )
    return ratios.max(axis=1), rest_lengths.max(axis=1)


def weight_array(obj: bpy.types.Object, group_name: str) -> np.ndarray:
    group = obj.vertex_groups.get(group_name)
    if group is None:
        raise RuntimeError(f"Missing vertex group: {group_name}")
    result = np.zeros(len(obj.data.vertices), dtype=np.float64)
    for vertex in obj.data.vertices:
        for item in vertex.groups:
            if item.group == group.index:
                result[vertex.index] = item.weight
                break
    return result


def vertex_weights(obj: bpy.types.Object, index: int) -> dict[str, float]:
    names = {group.index: group.name for group in obj.vertex_groups}
    return {
        names[item.group]: round(float(item.weight), 6)
        for item in sorted(obj.data.vertices[index].groups, key=lambda value: value.group)
    }


def pose_metrics(
    body: bpy.types.Object, rig: bpy.types.Object
) -> dict[str, dict[str, object]]:
    armature = rig.data
    original_pose_position = armature.pose_position
    bone = rig.pose.bones.get(NECK_BONE)
    if bone is None:
        raise RuntimeError(f"Missing neck bone: {NECK_BONE}")
    original_mode = bone.rotation_mode
    original_quaternion = bone.rotation_quaternion.copy()
    original_constraint_mutes = [constraint.mute for constraint in bone.constraints]
    tris = triangles(body.data)
    descendant_names = {
        candidate.name
        for candidate in rig.data.bones
        if candidate == rig.data.bones[NECK_BONE]
        or rig.data.bones[NECK_BONE] in candidate.parent_recursive
    }
    moving_weight = np.zeros(len(body.data.vertices), dtype=np.float64)
    for name in descendant_names:
        group = body.vertex_groups.get(name)
        if group is not None:
            moving_weight += weight_array(body, name)
    try:
        armature.pose_position = "POSE"
        # Unity evaluates exported DEF transforms without Blender's Rigify
        # constraints.  Mute only this bone's authoring constraints so the
        # direct rotation below measures the same skinning boundary.
        for constraint in bone.constraints:
            constraint.mute = True
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = Quaternion()
        bpy.context.view_layer.update()
        rest = evaluated_coordinates(body)
        result: dict[str, dict[str, object]] = {}
        for axis_name, axis in (
            ("X", (1.0, 0.0, 0.0)),
            ("Y", (0.0, 1.0, 0.0)),
            ("Z", (0.0, 0.0, 1.0)),
        ):
            for sign in (-1, 1):
                for degrees in POSE_ANGLES:
                    bone.rotation_quaternion = Quaternion(
                        axis, math.radians(degrees * sign)
                    )
                    bpy.context.view_layer.update()
                    posed = evaluated_coordinates(body)
                    ratios, _ = edge_metrics(rest, posed, tris)
                    torn_indices = np.flatnonzero(ratios >= TORN_RATIO)
                    worst_indices = np.argsort(ratios)[-8:][::-1]
                    key = f"{axis_name}{sign:+d}_{degrees}"
                    result[key] = {
                        "torn_triangles": int(len(torn_indices)),
                        "worst_stretch_ratio": float(ratios.max()),
                        "torn_vertices": (
                            [
                                {
                                    "vertex": int(index),
                                    "coordinate": list(body.data.vertices[int(index)].co),
                                    "moving_weight": float(moving_weight[int(index)]),
                                    "weights": vertex_weights(body, int(index)),
                                }
                                for index in np.unique(tris[torn_indices])
                            ]
                            if key == "X+1_20"
                            else []
                        ),
                        "worst": ([
                            {
                                "triangle": int(index),
                                "vertices": tris[index].tolist(),
                                "ratio": float(ratios[index]),
                                "weights": {
                                    str(int(vertex)): vertex_weights(body, int(vertex))
                                    for vertex in tris[index]
                                },
                            }
                            for index in worst_indices
                        ] if key == "X+1_20" else []),
                    }
        return result
    finally:
        bone.rotation_quaternion = original_quaternion
        bone.rotation_mode = original_mode
        for constraint, mute in zip(bone.constraints, original_constraint_mutes):
            constraint.mute = mute
        armature.pose_position = original_pose_position
        bpy.context.view_layer.update()


def connected_components(mesh: bpy.types.Mesh) -> list[list[int]]:
    neighbors = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        neighbors[a].append(b)
        neighbors[b].append(a)
    unseen = set(range(len(mesh.vertices)))
    components: list[list[int]] = []
    while unseen:
        seed = unseen.pop()
        stack = [seed]
        component = [seed]
        while stack:
            for candidate in neighbors[stack.pop()]:
                if candidate not in unseen:
                    continue
                unseen.remove(candidate)
                stack.append(candidate)
                component.append(candidate)
        components.append(component)
    return sorted(components, key=len, reverse=True)


def isolated_mouth_faces(mesh: bpy.types.Mesh) -> list[dict[str, object]]:
    face_counts = np.zeros(len(mesh.vertices), dtype=np.int64)
    for polygon in mesh.polygons:
        face_counts[list(polygon.vertices)] += 1
    found = []
    for polygon in mesh.polygons:
        vertices = list(polygon.vertices)
        if len(vertices) != 3 or not all(face_counts[index] == 1 for index in vertices):
            continue
        center = np.mean(
            [tuple(mesh.vertices[index].co) for index in vertices], axis=0
        )
        if -0.06 <= center[0] <= 0.04 and -0.25 <= center[1] <= -0.17 and 0.54 <= center[2] <= 0.61:
            found.append(
                {
                    "polygon": polygon.index,
                    "vertices": vertices,
                    "center": center.tolist(),
                }
            )
    return found


def mesh_digest(obj: bpy.types.Object) -> str:
    digest = hashlib.sha256()
    for vertex in obj.data.vertices:
        digest.update(np.asarray(tuple(vertex.co), dtype=np.float64).tobytes())
        for item in sorted(vertex.groups, key=lambda value: value.group):
            digest.update(f"{item.group}:{item.weight:.9f};".encode())
    return digest.hexdigest()


def deform_weight_arrays(
    obj: bpy.types.Object, rig: bpy.types.Object
) -> tuple[dict[str, np.ndarray], np.ndarray, np.ndarray]:
    names = [bone.name for bone in rig.data.bones if bone.use_deform]
    arrays = {
        name: weight_array(obj, name)
        for name in names
        if obj.vertex_groups.get(name) is not None
    }
    total = np.sum(list(arrays.values()), axis=0)
    neck = rig.data.bones[NECK_BONE]
    moving_names = {
        bone.name
        for bone in rig.data.bones
        if bone.use_deform and (bone == neck or neck in bone.parent_recursive)
    }
    moving = np.sum(
        [values for name, values in arrays.items() if name in moving_names], axis=0
    )
    return arrays, total, moving


def neck_candidates(
    obj: bpy.types.Object,
    fraction: np.ndarray,
    rings: int,
) -> tuple[set[int], list[list[int]], list[tuple[int, int]]]:
    neighbors = [[] for _ in obj.data.vertices]
    seams: list[tuple[int, int]] = []
    for edge in obj.data.edges:
        a, b = map(int, edge.vertices)
        neighbors[a].append(b)
        neighbors[b].append(a)
        midpoint_z = (obj.data.vertices[a].co.z + obj.data.vertices[b].co.z) * 0.5
        if 0.43 <= midpoint_z <= 0.57 and abs(fraction[a] - fraction[b]) >= 0.45:
            seams.append((a, b))
    if not seams:
        raise RuntimeError("No abrupt canonical neck seam was found")
    candidates = {index for edge in seams for index in edge}
    frontier = set(candidates)
    for _ in range(rings):
        frontier = {
            candidate
            for index in frontier
            for candidate in neighbors[index]
            if 0.40 <= obj.data.vertices[candidate].co.z <= 0.60
        } - candidates
        candidates.update(frontier)
    return candidates, neighbors, seams


def set_group_weight(
    obj: bpy.types.Object, group_name: str, vertex: int, value: float
) -> None:
    group = obj.vertex_groups.get(group_name)
    if group is None:
        raise RuntimeError(f"Missing target group: {group_name}")
    group.remove([vertex])
    if value > 1.0e-8:
        group.add([vertex], float(value), "REPLACE")


def apply_neck_transition(
    obj: bpy.types.Object,
    rig: bpy.types.Object,
    *,
    rings: int,
    iterations: int,
    strength: float,
) -> dict[str, object]:
    if rings < 0 or iterations < 1 or not 0.0 < strength <= 1.0:
        raise RuntimeError("Invalid smoothing parameters")
    arrays, total, moving = deform_weight_arrays(obj, rig)
    fraction = np.divide(
        moving, total, out=np.zeros_like(total), where=total > 1.0e-8
    )
    candidates, neighbors, seams = neck_candidates(obj, fraction, rings)
    target = fraction.copy()
    for _ in range(iterations):
        previous = target.copy()
        for index in candidates:
            adjacent = neighbors[index]
            if not adjacent:
                continue
            average = float(np.mean(previous[adjacent]))
            target[index] = previous[index] + strength * (average - previous[index])

    descendant_names = {
        bone.name
        for bone in rig.data.bones
        if bone.use_deform
        and (
            bone == rig.data.bones[NECK_BONE]
            or rig.data.bones[NECK_BONE] in bone.parent_recursive
        )
    }
    donor_names = ("DEF-spine.003", "DEF-spine.004", "DEF-spine.005")
    before_target = {
        name: weight_array(obj, name).copy()
        for name in set(donor_names) | descendant_names
        if obj.vertex_groups.get(name) is not None
    }
    changed: list[int] = []
    clamped = 0
    for index in sorted(candidates):
        desired = float(target[index] * total[index])
        current = float(moving[index])
        delta = desired - current
        if abs(delta) <= 1.0e-7:
            continue
        if delta > 0.0:
            donors = {
                name: float(arrays[name][index])
                for name in donor_names
                if name in arrays and arrays[name][index] > 0.0
            }
            available = sum(donors.values())
            applied = min(delta, available)
            clamped += int(applied + 1.0e-8 < delta)
            if applied <= 1.0e-8:
                continue
            for name, value in donors.items():
                set_group_weight(obj, name, index, value * (1.0 - applied / available))
            set_group_weight(
                obj,
                NECK_BONE,
                index,
                float(arrays[NECK_BONE][index]) + applied,
            )
        else:
            receivers = {
                name: float(arrays[name][index])
                for name in descendant_names
                if name in arrays and arrays[name][index] > 0.0
            }
            available = sum(receivers.values())
            applied = min(-delta, available)
            clamped += int(applied + 1.0e-8 < -delta)
            if applied <= 1.0e-8:
                continue
            for name, value in receivers.items():
                set_group_weight(obj, name, index, value * (1.0 - applied / available))
            set_group_weight(
                obj,
                "DEF-spine.005",
                index,
                float(arrays["DEF-spine.005"][index]) + applied,
            )
        changed.append(index)

    after_arrays, after_total, after_moving = deform_weight_arrays(obj, rig)
    target_groups = set(before_target)
    non_target_before = {
        name: values for name, values in arrays.items() if name not in target_groups
    }
    non_target_after = {
        name: values for name, values in after_arrays.items() if name not in target_groups
    }
    non_target_delta = max(
        float(np.max(np.abs(non_target_after[name] - values)))
        for name, values in non_target_before.items()
    )
    total_delta = float(np.max(np.abs(after_total - total)))
    if non_target_delta > 1.0e-8:
        raise RuntimeError(f"Non-target deform weight changed: {non_target_delta}")
    if total_delta > 1.0e-6:
        raise RuntimeError(f"Total deform weight changed: {total_delta}")
    return {
        "seam_edges": len(seams),
        "candidate_vertices": len(candidates),
        "changed_vertices": len(changed),
        "clamped_vertices": clamped,
        "rings": rings,
        "iterations": iterations,
        "strength": strength,
        "moving_fraction_before": {
            "min": float(fraction[list(candidates)].min()),
            "max": float(fraction[list(candidates)].max()),
        },
        "moving_fraction_after": {
            "min": float(
                np.divide(
                    after_moving,
                    after_total,
                    out=np.zeros_like(after_total),
                    where=after_total > 1.0e-8,
                )[list(candidates)].min()
            ),
            "max": float(
                np.divide(
                    after_moving,
                    after_total,
                    out=np.zeros_like(after_total),
                    where=after_total > 1.0e-8,
                )[list(candidates)].max()
            ),
        },
        "total_deform_weight_delta_max": total_delta,
        "non_target_deform_weight_delta_max": non_target_delta,
    }


def render_closeup(
    body: bpy.types.Object,
    eyes: bpy.types.Object,
    rig: bpy.types.Object,
    path: Path,
    *,
    view: str,
    pose_degrees: float = 0.0,
) -> None:
    scene = bpy.context.scene
    bone = rig.pose.bones[NECK_BONE]
    original_pose_position = rig.data.pose_position
    original_mode = bone.rotation_mode
    original_quaternion = bone.rotation_quaternion.copy()
    original_constraint_mutes = [constraint.mute for constraint in bone.constraints]
    original_camera = scene.camera
    original_engine = scene.render.engine
    original_filepath = scene.render.filepath
    original_resolution = (
        scene.render.resolution_x,
        scene.render.resolution_y,
        scene.render.resolution_percentage,
    )
    original_transparent = scene.render.film_transparent
    original_shading = (
        scene.display.shading.light,
        scene.display.shading.color_type,
        scene.display.shading.show_shadows,
        scene.display.shading.show_cavity,
    )
    hidden = {obj.name: obj.hide_render for obj in bpy.data.objects}
    camera_data = bpy.data.cameras.new("ADK_CanonicalEvidenceCamera")
    camera = bpy.data.objects.new("ADK_CanonicalEvidenceCamera", camera_data)
    scene.collection.objects.link(camera)
    try:
        for obj in bpy.data.objects:
            obj.hide_render = obj not in {body, eyes, camera}
        rig.data.pose_position = "POSE"
        for constraint in bone.constraints:
            constraint.mute = True
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = Quaternion(
            (1.0, 0.0, 0.0), math.radians(pose_degrees)
        )
        bpy.context.view_layer.update()

        scene.camera = camera
        camera_data.type = "ORTHO"
        if view == "neck":
            target = Vector((0.0, -0.11, 0.52))
            camera.location = target + Vector((1.4, 0.0, 0.0))
            camera.data.ortho_scale = 0.55
        elif view == "mouth":
            target = Vector((0.0, -0.22, 0.575))
            camera.location = target + Vector((0.0, -2.0, 0.0))
            camera.data.ortho_scale = 0.29
        else:
            raise RuntimeError(f"Unknown evidence view: {view}")
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()

        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.shading.light = "STUDIO"
        scene.display.shading.color_type = "MATERIAL"
        scene.display.shading.show_shadows = True
        scene.display.shading.show_cavity = True
        scene.render.resolution_x = 640
        scene.render.resolution_y = 640
        scene.render.resolution_percentage = 100
        scene.render.film_transparent = False
        scene.render.image_settings.file_format = "PNG"
        path = path.resolve()
        path.parent.mkdir(parents=True, exist_ok=True)
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
    finally:
        bone.rotation_quaternion = original_quaternion
        bone.rotation_mode = original_mode
        for constraint, mute in zip(bone.constraints, original_constraint_mutes):
            constraint.mute = mute
        rig.data.pose_position = original_pose_position
        scene.camera = original_camera
        scene.render.engine = original_engine
        scene.render.filepath = original_filepath
        (
            scene.render.resolution_x,
            scene.render.resolution_y,
            scene.render.resolution_percentage,
        ) = original_resolution
        scene.render.film_transparent = original_transparent
        (
            scene.display.shading.light,
            scene.display.shading.color_type,
            scene.display.shading.show_shadows,
            scene.display.shading.show_cavity,
        ) = original_shading
        for name, state in hidden.items():
            if name in bpy.data.objects:
                bpy.data.objects[name].hide_render = state
        bpy.data.objects.remove(camera, do_unlink=True)
        bpy.data.cameras.remove(camera_data)
        bpy.context.view_layer.update()


def export_canonical_fbx(
    body: bpy.types.Object,
    eyes: bpy.types.Object,
    rig: bpy.types.Object,
    path: Path,
) -> None:
    if rig.data.pose_position != "REST":
        raise RuntimeError("Canonical rig must be in REST pose for export")
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (body, eyes, rig):
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    path = path.resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        armature_nodetype="NULL",
        use_armature_deform_only=False,
        bake_anim=False,
    )


def main() -> None:
    args = parse_args()
    body, eyes, rig = require_scene()
    components = connected_components(body.data)
    pose_before = pose_metrics(body, rig)
    digest_before = mesh_digest(body)
    report: dict[str, object] = {
        "source": bpy.data.filepath,
        "canonical_objects": {
            "body": body.name,
            "eyes": eyes.name,
            "rig": rig.name,
        },
        "mesh": {
            "vertices": len(body.data.vertices),
            "edges": len(body.data.edges),
            "polygons": len(body.data.polygons),
            "triangles": len(triangles(body.data)),
            "components": [len(component) for component in components],
            "smooth_polygons": sum(poly.use_smooth for poly in body.data.polygons),
            "digest": mesh_digest(body),
        },
        "rig": {
            "bones": len(rig.data.bones),
            "deform_bones": sum(bone.use_deform for bone in rig.data.bones),
        },
        "mouth": {"isolated_faces": isolated_mouth_faces(body.data)},
        "neck_group_totals": {
            name: float(weight_array(body, name).sum()) for name in NECK_GROUPS
        },
        "neck_poses": pose_before,
    }
    if args.mode == "apply":
        if args.output is None:
            raise RuntimeError("--output is required in apply mode")
        positions_before = np.asarray([tuple(v.co) for v in body.data.vertices])
        if args.evidence_dir:
            evidence = args.evidence_dir.resolve()
            render_closeup(
                body,
                eyes,
                rig,
                evidence / "neck-before-46deg.png",
                view="neck",
                pose_degrees=46.0,
            )
            render_closeup(
                body,
                eyes,
                rig,
                evidence / "mouth-smooth-before.png",
                view="mouth",
            )
        report["neck_rework"] = apply_neck_transition(
            body,
            rig,
            rings=args.rings,
            iterations=args.iterations,
            strength=args.strength,
        )
        positions_after = np.asarray([tuple(v.co) for v in body.data.vertices])
        if not np.array_equal(positions_before, positions_after):
            raise RuntimeError("Canonical mesh positions changed")
        pose_after = pose_metrics(body, rig)
        report["neck_poses_after"] = pose_after
        for key, before in pose_before.items():
            after = pose_after[key]
            if after["torn_triangles"] > before["torn_triangles"]:
                raise RuntimeError(f"Canonical neck tear count regressed at {key}")
            if after["worst_stretch_ratio"] > before["worst_stretch_ratio"] + 1.0e-6:
                raise RuntimeError(f"Canonical worst stretch regressed at {key}")
        if pose_after["X+1_20"]["torn_triangles"] > 5:
            raise RuntimeError("Canonical X+20 neck tear count remains over budget")
        if args.evidence_dir:
            render_closeup(
                body,
                eyes,
                rig,
                evidence / "neck-after-46deg.png",
                view="neck",
                pose_degrees=46.0,
            )
            render_closeup(
                body,
                eyes,
                rig,
                evidence / "mouth-smooth-after.png",
                view="mouth",
            )
        report["mesh_digest_before"] = digest_before
        report["mesh_digest_after"] = mesh_digest(body)
        output = args.output.resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(output), check_existing=False)
        report["output"] = str(output)
        report["fbx_outputs"] = []
        for fbx_output in args.fbx_output:
            export_canonical_fbx(body, eyes, rig, fbx_output)
            report["fbx_outputs"].append(str(fbx_output.resolve()))
    if args.report:
        path = args.report.resolve()
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ADK_CANONICAL_REPORT=" + json.dumps(report, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
