"""Rework the LAST SHIFT lime alien's left hip and upper-mouth rest surface.

The preceding boundary diagnosis proved that both visible defects already
exist in REST pose.  This pass therefore changes only Basis coordinates in two
tight topology neighborhoods, translates every relative shape key by the same
per-vertex displacement, and leaves topology and skin weights untouched.

Run against the canonical ``LastShiftLimeAlien_UnityExport_LeftToeFixed.blend``::

    blender -b <canonical.blend> -P rework_last_shift_hip_mouth_geometry.py -- \
        --output <canonical.blend> --evidence-dir <dir> --report <report.json>
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from collections import deque
from pathlib import Path

import bpy
import bmesh
from mathutils import Quaternion, Vector


BODY_NAME = "LastShift_LimeAlien_Body"
RIG_NAME = "LastShift_LimeAlien_Rig"
HIP_ROOT = "DEF-thigh.L"
MOUTH_CENTER = Vector((-0.014025, -0.211263, 0.577113))
MOUTH_HALF_EXTENT = Vector((0.075, 0.055, 0.045))
REWORK_MARKER = "ADK_HipMouthGeometryRework_v1"


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path)
    parser.add_argument("--evidence-dir", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--analyze-only", action="store_true")
    parser.add_argument("--iterations", type=int, default=4)
    parser.add_argument("--lambda-factor", type=float, default=0.32)
    parser.add_argument("--mu-factor", type=float, default=-0.34)
    parser.add_argument("--hip-rings", type=int, default=3)
    parser.add_argument("--mouth-rings", type=int, default=4)
    parser.add_argument("--hip-max-displacement", type=float, default=0.006)
    parser.add_argument("--mouth-max-displacement", type=float, default=0.0035)
    return parser.parse_args(raw)


def require_scene() -> tuple[bpy.types.Object, bpy.types.Object]:
    body = bpy.data.objects.get(BODY_NAME)
    rig = bpy.data.objects.get(RIG_NAME)
    if body is None or body.type != "MESH":
        raise RuntimeError(f"Missing canonical body: {BODY_NAME}")
    if rig is None or rig.type != "ARMATURE":
        raise RuntimeError(f"Missing canonical rig: {RIG_NAME}")
    armatures = [m.object for m in body.modifiers if m.type == "ARMATURE"]
    if armatures != [rig]:
        raise RuntimeError(f"Body armature mismatch: {armatures}")
    if body.get(REWORK_MARKER):
        raise RuntimeError("Hip/mouth geometry rework is already applied")
    return body, rig


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def path_label(path: Path) -> str:
    try:
        return path.resolve().relative_to(Path.cwd().resolve()).as_posix()
    except ValueError:
        return path.resolve().as_posix()


def topology_digest(mesh: bpy.types.Mesh) -> str:
    digest = hashlib.sha256()
    for edge in mesh.edges:
        digest.update(f"e:{int(edge.vertices[0])},{int(edge.vertices[1])};".encode())
    for polygon in mesh.polygons:
        digest.update(("p:" + ",".join(map(str, polygon.vertices)) + ";").encode())
    return digest.hexdigest()


def weight_digest(body: bpy.types.Object) -> str:
    digest = hashlib.sha256()
    for vertex in body.data.vertices:
        for item in sorted(vertex.groups, key=lambda value: value.group):
            digest.update(f"{vertex.index}:{item.group}:{item.weight:.9f};".encode())
    return digest.hexdigest()


def connectivity(mesh: bpy.types.Mesh) -> tuple[list[set[int]], set[int]]:
    neighbors = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = map(int, edge.vertices)
        neighbors[a].add(b)
        neighbors[b].add(a)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    boundary = {vertex.index for vertex in bm.verts if vertex.is_boundary}
    bm.free()
    return neighbors, boundary


def deform_weights(body: bpy.types.Object, rig: bpy.types.Object) -> list[dict[str, float]]:
    deform = {bone.name for bone in rig.data.bones if bone.use_deform}
    names = {
        group.index: group.name for group in body.vertex_groups if group.name in deform
    }
    return [
        {
            names[item.group]: float(item.weight)
            for item in vertex.groups
            if item.group in names and item.weight > 1.0e-8
        }
        for vertex in body.data.vertices
    ]


def descendants(rig: bpy.types.Object, root_name: str) -> set[str]:
    root = rig.data.bones.get(root_name)
    if root is None:
        raise RuntimeError(f"Missing deform root: {root_name}")
    return {
        bone.name
        for bone in rig.data.bones
        if bone.use_deform and (bone == root or root in bone.parent_recursive)
    }


def graph_distances(
    seeds: set[int], neighbors: list[set[int]], allowed: set[int], limit: int
) -> dict[int, int]:
    distances = {index: 0 for index in seeds if index in allowed}
    queue = deque(distances)
    while queue:
        index = queue.popleft()
        if distances[index] >= limit:
            continue
        for linked in neighbors[index]:
            if linked in allowed and linked not in distances:
                distances[linked] = distances[index] + 1
                queue.append(linked)
    return distances


def select_hip_region(
    body: bpy.types.Object,
    rig: bpy.types.Object,
    neighbors: list[set[int]],
    weights: list[dict[str, float]],
    rings: int,
) -> tuple[dict[int, float], dict[str, object]]:
    hip = rig.data.bones.get(HIP_ROOT)
    if hip is None:
        raise RuntimeError(f"Missing hip bone: {HIP_ROOT}")
    joint = hip.head_local
    minimum = Vector((joint.x - 0.11, joint.y - 0.13, joint.z - 0.08))
    maximum = Vector((joint.x + 0.055, joint.y + 0.13, joint.z + 0.105))
    thigh_names = descendants(rig, HIP_ROOT)
    allowed: set[int] = set()
    moving_fraction: dict[int, float] = {}
    for vertex, values in zip(body.data.vertices, weights):
        if not all(minimum[axis] <= vertex.co[axis] <= maximum[axis] for axis in range(3)):
            continue
        total = sum(values.values())
        thigh = sum(value for name, value in values.items() if name in thigh_names)
        pelvis = values.get("DEF-pelvis.L", 0.0)
        if total <= 1.0e-8 or max(thigh, pelvis) < 0.035:
            continue
        dominant = max(values, key=values.get) if values else ""
        if "upper_arm" in dominant or "shoulder" in dominant or "forearm" in dominant:
            continue
        allowed.add(vertex.index)
        moving_fraction[vertex.index] = thigh / total

    seam_vertices: set[int] = set()
    seam_edges = 0
    for a in allowed:
        for b in neighbors[a]:
            if a < b and b in allowed:
                if abs(moving_fraction[a] - moving_fraction[b]) >= 0.075:
                    seam_vertices.update((a, b))
                    seam_edges += 1
    if not seam_vertices:
        raise RuntimeError("No left hip transition seam was found")
    distances = graph_distances(seam_vertices, neighbors, allowed, rings)
    local_boundary = {
        index for index in distances if any(linked not in allowed for linked in neighbors[index])
    }
    # Keep the outer leg contour and the final graph ring fixed.  The remaining
    # two to three rings redistribute the long triangular patch internally.
    silhouette = {
        index
        for index in distances
        if body.data.vertices[index].co.x <= joint.x - 0.088
    }
    movable = {
        index: math.sin(math.pi * (distance + 1) / (rings + 1))
        for index, distance in distances.items()
        if distance < rings and index not in local_boundary and index not in silhouette
    }
    if len(movable) < 8:
        raise RuntimeError(f"Hip selection is too small: {len(movable)}")
    return movable, {
        "bounds": [tuple(minimum), tuple(maximum)],
        "allowed_vertices": len(allowed),
        "seam_edges": seam_edges,
        "seam_vertices": len(seam_vertices),
        "graph_vertices": len(distances),
        "pinned_local_boundary": len(local_boundary),
        "pinned_outer_silhouette": len(silhouette),
        "movable_vertices": len(movable),
        "rings": rings,
    }


def select_upper_mouth_region(
    body: bpy.types.Object,
    neighbors: list[set[int]],
    boundary: set[int],
    rings: int,
) -> tuple[dict[int, float], set[int], dict[str, object]]:
    minimum = MOUTH_CENTER - MOUTH_HALF_EXTENT
    maximum = MOUTH_CENTER + MOUTH_HALF_EXTENT
    allowed = {
        vertex.index
        for vertex in body.data.vertices
        if all(minimum[axis] <= vertex.co[axis] <= maximum[axis] for axis in range(3))
    }
    opening = boundary & allowed
    if len(opening) < 8:
        raise RuntimeError(f"Mouth boundary selection is too small: {len(opening)}")
    median_z = sorted(body.data.vertices[index].co.z for index in opening)[len(opening) // 2]
    upper_opening = {
        index for index in opening if body.data.vertices[index].co.z >= median_z
    }
    distances = graph_distances(upper_opening, neighbors, allowed, rings)
    # The opening loop is the silhouette contract.  The final ring is also
    # fixed so the correction blends back into the cheek without a new ridge.
    movable = {
        index: math.sin(math.pi * distance / rings)
        for index, distance in distances.items()
        if 0 < distance < rings and index not in opening
    }
    if len(movable) < 8:
        raise RuntimeError(f"Upper-mouth selection is too small: {len(movable)}")
    return movable, opening, {
        "bounds": [tuple(minimum), tuple(maximum)],
        "open_boundary_vertices": len(opening),
        "upper_boundary_seeds": len(upper_opening),
        "graph_vertices": len(distances),
        "pinned_frontier_vertices": sum(distance == rings for distance in distances.values()),
        "movable_vertices": len(movable),
        "rings": rings,
    }


def laplacian_roughness(
    coordinates: list[Vector], neighbors: list[set[int]], indices: set[int]
) -> float:
    values = []
    for index in indices:
        linked = neighbors[index]
        if linked:
            average = sum((coordinates[item] for item in linked), Vector()) / len(linked)
            values.append((coordinates[index] - average).length)
    return sum(values) / len(values) if values else 0.0


def triangle_aspect(
    mesh: bpy.types.Mesh, coordinates: list[Vector], indices: set[int]
) -> dict[str, float | int]:
    mesh.calc_loop_triangles()
    ratios = []
    for triangle in mesh.loop_triangles:
        vertices = tuple(map(int, triangle.vertices))
        if not all(index in indices for index in vertices):
            continue
        lengths = [
            (coordinates[vertices[0]] - coordinates[vertices[1]]).length,
            (coordinates[vertices[1]] - coordinates[vertices[2]]).length,
            (coordinates[vertices[2]] - coordinates[vertices[0]]).length,
        ]
        if min(lengths) > 1.0e-9:
            ratios.append(max(lengths) / min(lengths))
    ratios.sort()
    if not ratios:
        return {"triangles": 0, "mean_longest_to_shortest": 0.0, "p95": 0.0, "max": 0.0}
    p95 = ratios[max(0, math.ceil(len(ratios) * 0.95) - 1)]
    return {
        "triangles": len(ratios),
        "mean_longest_to_shortest": sum(ratios) / len(ratios),
        "p95": p95,
        "max": max(ratios),
    }


def masked_taubin(
    coordinates: list[Vector],
    neighbors: list[set[int]],
    masks: dict[int, float],
    *,
    iterations: int,
    lambda_factor: float,
    mu_factor: float,
    max_displacement: float,
) -> tuple[list[Vector], dict[str, object]]:
    original = [value.copy() for value in coordinates]
    result = [value.copy() for value in coordinates]
    target = set(masks)
    before = laplacian_roughness(original, neighbors, target)
    for _ in range(iterations):
        for factor in (lambda_factor, mu_factor):
            current = [value.copy() for value in result]
            for index, mask in masks.items():
                linked = neighbors[index]
                average = sum((current[item] for item in linked), Vector()) / len(linked)
                result[index] = current[index] + factor * mask * (average - current[index])
    clamped = 0
    for index in target:
        delta = result[index] - original[index]
        if delta.length > max_displacement:
            result[index] = original[index] + delta.normalized() * max_displacement
            clamped += 1
    displacements = sorted((result[index] - original[index]).length for index in target)
    after = laplacian_roughness(result, neighbors, target)
    if after >= before:
        raise RuntimeError(f"Local roughness did not improve: {before} -> {after}")
    return result, {
        "iterations": iterations,
        "lambda_factor": lambda_factor,
        "mu_factor": mu_factor,
        "max_displacement_limit": max_displacement,
        "moved_vertices": sum(value > 1.0e-8 for value in displacements),
        "clamped_vertices": clamped,
        "mean_displacement": sum(displacements) / len(displacements),
        "p95_displacement": displacements[max(0, math.ceil(len(displacements) * 0.95) - 1)],
        "max_displacement": max(displacements),
        "laplacian_mean_before": before,
        "laplacian_mean_after": after,
        "roughness_reduction": 1.0 - after / before,
    }


def apply_to_shape_keys(
    body: bpy.types.Object, before: list[Vector], after: list[Vector]
) -> float:
    keys = body.data.shape_keys.key_blocks if body.data.shape_keys else []
    original_deltas = [
        [point.co.copy() - before[index] for index, point in enumerate(key.data)]
        for key in keys[1:]
    ]
    displacement = [new - old for old, new in zip(before, after)]
    if keys:
        for key in keys:
            for index, delta in enumerate(displacement):
                key.data[index].co += delta
        for index, coordinate in enumerate(after):
            body.data.vertices[index].co = coordinate
    else:
        for vertex, coordinate in zip(body.data.vertices, after):
            vertex.co = coordinate
    body.data.update()
    error = 0.0
    for key, deltas in zip(keys[1:], original_deltas):
        for index, expected in enumerate(deltas):
            actual = key.data[index].co - keys[0].data[index].co
            error = max(error, (actual - expected).length)
    return error


def set_hip_stress_pose(rig: bpy.types.Object) -> dict[str, object]:
    # Rigify UI scripts are deliberately disabled in background Blender, so
    # authoring control rotations do not propagate through their driver setup.
    # Rotate the exported deform chain directly; this is the same skinning
    # boundary Unity evaluates and uses the production FK evidence angles.
    controls = {
        "DEF-thigh.L": (Vector((0.0, 0.0, 1.0)), -34.0),
        "DEF-shin.L": (Vector((0.0, 0.0, 1.0)), 68.0),
    }
    missing = [name for name in controls if name not in rig.pose.bones]
    if missing:
        raise RuntimeError(f"Missing FK controls: {missing}")
    state = {
        "pose_position": rig.data.pose_position,
        "rotations": {
            name: (rig.pose.bones[name].rotation_mode, rig.pose.bones[name].rotation_quaternion.copy())
            for name in controls
        },
        "constraint_mutes": {
            name: [constraint.mute for constraint in rig.pose.bones[name].constraints]
            for name in controls
        },
    }
    rig.data.pose_position = "POSE"
    for name, (axis, angle) in controls.items():
        bone = rig.pose.bones[name]
        for constraint in bone.constraints:
            constraint.mute = True
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = Quaternion(axis, math.radians(angle))
    bpy.context.view_layer.update()
    return state


def restore_pose(rig: bpy.types.Object, state: dict[str, object]) -> None:
    for name, (mode, quaternion) in state["rotations"].items():
        bone = rig.pose.bones[name]
        bone.rotation_mode = mode
        bone.rotation_quaternion = quaternion
        for constraint, mute in zip(bone.constraints, state["constraint_mutes"][name]):
            constraint.mute = mute
    rig.data.pose_position = state["pose_position"]
    bpy.context.view_layer.update()


def evaluated_coordinates(body: bpy.types.Object) -> list[Vector]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = body.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=depsgraph)
    try:
        if len(mesh.vertices) != len(body.data.vertices):
            raise RuntimeError("Evaluated canonical topology changed")
        return [vertex.co.copy() for vertex in mesh.vertices]
    finally:
        evaluated.to_mesh_clear()


def pose_metric(
    body: bpy.types.Object,
    rig: bpy.types.Object,
    neighbors: list[set[int]],
    indices: set[int],
) -> dict[str, float]:
    old_pose = rig.data.pose_position
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    rest = evaluated_coordinates(body)
    state = set_hip_stress_pose(rig)
    try:
        posed = evaluated_coordinates(body)
    finally:
        restore_pose(rig, state)
        rig.data.pose_position = old_pose
        bpy.context.view_layer.update()
    rest_roughness = laplacian_roughness(rest, neighbors, indices)
    pose_roughness = laplacian_roughness(posed, neighbors, indices)
    edge_stretch = []
    for index in indices:
        for linked in neighbors[index]:
            if index < linked and linked in indices:
                base = (rest[index] - rest[linked]).length
                if base > 1.0e-9:
                    edge_stretch.append((posed[index] - posed[linked]).length / base)
    return {
        "rest_laplacian_mean": rest_roughness,
        "pose_laplacian_mean": pose_roughness,
        "pose_added_roughness": pose_roughness - rest_roughness,
        "max_edge_stretch_ratio": max(edge_stretch, default=1.0),
        "mean_edge_stretch_ratio": sum(edge_stretch) / len(edge_stretch) if edge_stretch else 1.0,
    }


def render_closeup(
    body: bpy.types.Object,
    rig: bpy.types.Object,
    path: Path,
    *,
    view: str,
    stress_pose: bool = False,
) -> None:
    scene = bpy.context.scene
    original_camera = scene.camera
    original_engine = scene.render.engine
    original_filepath = scene.render.filepath
    original_resolution = (scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage)
    original_transparent = scene.render.film_transparent
    original_color = body.color[:]
    original_shading = (
        scene.display.shading.light,
        scene.display.shading.color_type,
        scene.display.shading.show_shadows,
        scene.display.shading.show_cavity,
    )
    hidden = {obj.name: obj.hide_render for obj in bpy.data.objects}
    camera_data = bpy.data.cameras.new("ADK_HipMouthEvidenceCamera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("ADK_HipMouthEvidenceCamera", camera_data)
    scene.collection.objects.link(camera)
    old_pose = rig.data.pose_position
    pose_state = None
    try:
        for obj in bpy.data.objects:
            obj.hide_render = obj not in {body, camera}
        if stress_pose:
            pose_state = set_hip_stress_pose(rig)
        else:
            rig.data.pose_position = "REST"
            bpy.context.view_layer.update()
        if view == "hip":
            target = Vector((-0.07, -0.07, 0.255))
            camera.location = target + Vector((0.34, -1.55, 0.16))
            camera.data.ortho_scale = 0.36
        elif view == "mouth":
            target = Vector((0.0, -0.22, 0.575))
            camera.location = target + Vector((0.0, -2.0, 0.0))
            camera.data.ortho_scale = 0.21
        else:
            raise RuntimeError(f"Unknown evidence view: {view}")
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.camera = camera
        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.shading.light = "STUDIO"
        scene.display.shading.color_type = "OBJECT"
        scene.display.shading.show_shadows = True
        scene.display.shading.show_cavity = True
        body.color = (0.43, 0.72, 0.16, 1.0)
        scene.render.resolution_x = 640
        scene.render.resolution_y = 640
        scene.render.resolution_percentage = 100
        scene.render.film_transparent = False
        scene.render.image_settings.file_format = "PNG"
        path = path.resolve()
        path.parent.mkdir(parents=True, exist_ok=True)
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        if not path.exists() or path.stat().st_size < 10_000:
            raise RuntimeError(f"Evidence render was not written: {path}")
    finally:
        if pose_state is not None:
            restore_pose(rig, pose_state)
        rig.data.pose_position = old_pose
        scene.camera = original_camera
        scene.render.engine = original_engine
        scene.render.filepath = original_filepath
        scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = original_resolution
        scene.render.film_transparent = original_transparent
        body.color = original_color
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


def main() -> None:
    args = parse_args()
    if not 1 <= args.iterations <= 8:
        raise RuntimeError("--iterations must be in [1, 8]")
    if not 0.0 < args.lambda_factor < 1.0 or not -1.0 < args.mu_factor < 0.0:
        raise RuntimeError("Invalid Taubin factors")
    if not args.analyze_only and args.output is None:
        raise RuntimeError("--output is required unless --analyze-only is used")

    body, rig = require_scene()
    mesh = body.data
    source = Path(bpy.data.filepath).resolve()
    source_sha = sha256(source)
    topology_before = topology_digest(mesh)
    weights_before = weight_digest(body)
    neighbors, boundary = connectivity(mesh)
    weights = deform_weights(body, rig)
    hip_masks, hip_selection = select_hip_region(body, rig, neighbors, weights, args.hip_rings)
    mouth_masks, mouth_opening, mouth_selection = select_upper_mouth_region(
        body, neighbors, boundary, args.mouth_rings
    )
    if set(hip_masks) & set(mouth_masks):
        raise RuntimeError("Hip and mouth correction regions overlap")

    before_coordinates = [vertex.co.copy() for vertex in mesh.vertices]
    hip_indices = set(hip_masks)
    mouth_indices = set(mouth_masks)
    report: dict[str, object] = {
        "source": path_label(source),
        "source_sha256_before": source_sha,
        "body": body.name,
        "rig": rig.name,
        "shape_keys": len(mesh.shape_keys.key_blocks) if mesh.shape_keys else 0,
        "selection": {"left_hip_groin": hip_selection, "upper_mouth": mouth_selection},
        "before": {
            "left_hip_groin": {
                "triangle_aspect": triangle_aspect(mesh, before_coordinates, hip_indices),
                "pose": pose_metric(body, rig, neighbors, hip_indices),
            },
            "upper_mouth": {
                "triangle_aspect": triangle_aspect(mesh, before_coordinates, mouth_indices),
            },
        },
    }
    if args.analyze_only:
        args.report.resolve().parent.mkdir(parents=True, exist_ok=True)
        args.report.resolve().write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print("ADK_HIP_MOUTH_REWORK_ANALYSIS=" + json.dumps(report, ensure_ascii=False), flush=True)
        return

    evidence = args.evidence_dir.resolve()
    render_closeup(body, rig, evidence / "hip-rest-before.png", view="hip")
    render_closeup(body, rig, evidence / "hip-fk-before.png", view="hip", stress_pose=True)
    render_closeup(body, rig, evidence / "mouth-before.png", view="mouth")

    hip_coordinates, hip_relax = masked_taubin(
        before_coordinates,
        neighbors,
        hip_masks,
        iterations=args.iterations,
        lambda_factor=args.lambda_factor,
        mu_factor=args.mu_factor,
        max_displacement=args.hip_max_displacement,
    )
    mouth_coordinates, mouth_relax = masked_taubin(
        hip_coordinates,
        neighbors,
        mouth_masks,
        iterations=args.iterations,
        lambda_factor=args.lambda_factor,
        mu_factor=args.mu_factor,
        max_displacement=args.mouth_max_displacement,
    )
    shape_error = apply_to_shape_keys(body, before_coordinates, mouth_coordinates)
    if shape_error > 1.0e-6:
        raise RuntimeError(f"Relative shape-key delta changed: {shape_error}")
    if topology_digest(mesh) != topology_before:
        raise RuntimeError("Topology changed during geometry relaxation")
    if weight_digest(body) != weights_before:
        raise RuntimeError("Skin weights changed during geometry relaxation")
    opening_displacement = max(
        (mouth_coordinates[index] - before_coordinates[index]).length
        for index in mouth_opening
    )
    if opening_displacement > 1.0e-8:
        raise RuntimeError(f"Mouth opening silhouette moved: {opening_displacement}")

    render_closeup(body, rig, evidence / "hip-rest-after.png", view="hip")
    render_closeup(body, rig, evidence / "hip-fk-after.png", view="hip", stress_pose=True)
    render_closeup(body, rig, evidence / "mouth-after.png", view="mouth")
    after_coordinates = [vertex.co.copy() for vertex in mesh.vertices]
    pose_after = pose_metric(body, rig, neighbors, hip_indices)
    pose_before = report["before"]["left_hip_groin"]["pose"]
    if pose_after["pose_laplacian_mean"] > pose_before["pose_laplacian_mean"] + 1.0e-6:
        raise RuntimeError("Absolute FK-pose roughness regressed after the geometry correction")

    body[REWORK_MARKER] = True
    report["relaxation"] = {
        "left_hip_groin": hip_relax,
        "upper_mouth": mouth_relax,
    }
    report["after"] = {
        "left_hip_groin": {
            "triangle_aspect": triangle_aspect(mesh, after_coordinates, hip_indices),
            "pose": pose_after,
        },
        "upper_mouth": {
            "triangle_aspect": triangle_aspect(mesh, after_coordinates, mouth_indices),
        },
    }
    report["regression_checks"] = {
        "topology_unchanged": topology_digest(mesh) == topology_before,
        "skin_weights_unchanged": weight_digest(body) == weights_before,
        "shape_key_relative_delta_max_error": shape_error,
        "mouth_opening_max_displacement": opening_displacement,
        "hip_rest_roughness_reduced": hip_relax["roughness_reduction"] > 0.0,
        "upper_mouth_roughness_reduced": mouth_relax["roughness_reduction"] > 0.0,
        "fk_pose_roughness_not_regressed": pose_after["pose_laplacian_mean"] <= pose_before["pose_laplacian_mean"] + 1.0e-6,
        "weight_redistribution_needed": False,
    }
    report["evidence"] = [
        "hip-rest-before.png",
        "hip-rest-after.png",
        "hip-fk-before.png",
        "hip-fk-after.png",
        "mouth-before.png",
        "mouth-after.png",
    ]
    report["decision"] = (
        "Absolute roughness in the identical FK pose improves after the rest-surface "
        "correction, so DEF-thigh.L/pelvis weights remain unchanged."
    )

    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(output), check_existing=False, compress=True)
    report["output"] = path_label(output)
    report["output_sha256"] = sha256(output)
    args.report.resolve().parent.mkdir(parents=True, exist_ok=True)
    args.report.resolve().write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ADK_HIP_MOUTH_REWORK=" + json.dumps(report, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
