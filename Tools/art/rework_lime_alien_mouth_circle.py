"""Turn the lime alien's neutral mouth indentation into a smooth circular rim.

The operation preserves vertex count, topology, skin weights, and all relative
shape-key deltas.  The closed mouth rim is evenly redistributed on an
area-preserving circle, while three surrounding graph rings inherit a tapered
version of the boundary displacement so the rim does not form a hard shelf.
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
from mathutils import Vector


BODY_NAME = "LastShift_LimeAlien_Body"
RIG_NAME = "LastShift_LimeAlien_Rig"
MOUTH_HINT = Vector((-0.011, -0.160, 0.506))
MARKER = "ADK_MouthCircleRework_v1"
BOUNDARY_PROPERTY = "ADK_MouthCircleBoundaryIndices"
RING_FALLOFF = (0.66, 0.36, 0.16)


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path)
    parser.add_argument("--evidence-dir", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--analyze-only", action="store_true")
    parser.add_argument("--radius-scale", type=float, default=1.0)
    return parser.parse_args(raw)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


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


def connectivity(mesh: bpy.types.Mesh) -> list[set[int]]:
    result = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = map(int, edge.vertices)
        result[a].add(b)
        result[b].add(a)
    return result


def components(indices: set[int], neighbors: list[set[int]]) -> list[set[int]]:
    remaining = set(indices)
    components = []
    while remaining:
        seed = remaining.pop()
        component = {seed}
        queue = deque([seed])
        while queue:
            index = queue.popleft()
            for linked in neighbors[index]:
                if linked in remaining:
                    remaining.remove(linked)
                    component.add(linked)
                    queue.append(linked)
        components.append(component)
    return components


def select_mouth_contour(
    mesh: bpy.types.Mesh, neighbors: list[set[int]]
) -> set[int]:
    """Extract the closed lip ring from the recessed mouth patch.

    The eye socket is the only nearby open mesh boundary, so boundary flags are
    intentionally not used.  Instead, isolate the mouth's recessed patch by
    its stable local bounds and take the largest connected component of that
    patch's induced boundary.
    """
    cavity = {
        vertex.index
        for vertex in mesh.vertices
        if -0.090 <= vertex.co.x <= 0.070
        and -0.165 <= vertex.co.y <= -0.115
        and 0.455 <= vertex.co.z <= 0.555
    }
    candidates = {
        index for index in cavity if any(linked not in cavity for linked in neighbors[index])
    }
    groups = components(candidates, neighbors)
    mouth = max(groups, key=len) if groups else set()
    degrees = {index: len(neighbors[index] & mouth) for index in mouth}
    if not 16 <= len(mouth) <= 24 or any(degree != 2 for degree in degrees.values()):
        raise RuntimeError(
            "Could not isolate a simple closed mouth rim: "
            f"count={len(mouth)}, degrees={degrees}, candidates={sorted(candidates)}"
        )
    return mouth


def ordered_contour(indices: set[int], neighbors: list[set[int]]) -> list[int]:
    start = min(indices)
    loop = [start]
    previous = None
    current = start
    while True:
        choices = sorted((neighbors[current] & indices) - ({previous} if previous is not None else set()))
        if not choices:
            raise RuntimeError("Mouth rim traversal terminated before closing")
        following = choices[0]
        if following == start:
            if len(loop) != len(indices):
                raise RuntimeError("Mouth rim closed before visiting every vertex")
            return loop
        if following in loop:
            raise RuntimeError("Mouth rim traversal self-intersected")
        loop.append(following)
        previous, current = current, following


def loop_metrics(coordinates: list[Vector], loop: list[int]) -> dict[str, float]:
    points = [coordinates[index] for index in loop]
    center_x = (min(point.x for point in points) + max(point.x for point in points)) * 0.5
    center_z = (min(point.z for point in points) + max(point.z for point in points)) * 0.5
    radii = [math.hypot(point.x - center_x, point.z - center_z) for point in points]
    edges = [(points[(index + 1) % len(points)] - point).length for index, point in enumerate(points)]
    mean_radius = sum(radii) / len(radii)
    mean_edge = sum(edges) / len(edges)
    width = max(point.x for point in points) - min(point.x for point in points)
    height = max(point.z for point in points) - min(point.z for point in points)
    return {
        "center_x": center_x,
        "center_z": center_z,
        "width": width,
        "height": height,
        "width_to_height": width / height,
        "mean_radius": mean_radius,
        "radius_cv": (
            math.sqrt(sum((radius - mean_radius) ** 2 for radius in radii) / len(radii))
            / mean_radius
        ),
        "mean_edge_length": mean_edge,
        "edge_length_cv": (
            math.sqrt(sum((edge - mean_edge) ** 2 for edge in edges) / len(edges))
            / mean_edge
        ),
        "depth_range": max(point.y for point in points) - min(point.y for point in points),
    }


def circularize(
    coordinates: list[Vector],
    loop: list[int],
    neighbors: list[set[int]],
    radius_scale: float,
) -> tuple[list[Vector], dict[str, object]]:
    result = [point.copy() for point in coordinates]
    points = [coordinates[index] for index in loop]
    center_x = (min(point.x for point in points) + max(point.x for point in points)) * 0.5
    center_z = (min(point.z for point in points) + max(point.z for point in points)) * 0.5
    half_width = (max(point.x for point in points) - min(point.x for point in points)) * 0.5
    half_height = (max(point.z for point in points) - min(point.z for point in points)) * 0.5
    radius = math.sqrt(half_width * half_height) * radius_scale

    depths = [point.y for point in points]
    for _ in range(4):
        depths = [
            depths[index] * 0.5
            + depths[(index - 1) % len(depths)] * 0.25
            + depths[(index + 1) % len(depths)] * 0.25
            for index in range(len(depths))
        ]

    observed_angles = [math.atan2(point.z - center_z, point.x - center_x) for point in points]
    base_step = 2.0 * math.pi / len(loop)
    alignments = []
    for direction in (1.0, -1.0):
        step = direction * base_step
        phase = math.atan2(
            sum(math.sin(angle - position * step) for position, angle in enumerate(observed_angles)),
            sum(math.cos(angle - position * step) for position, angle in enumerate(observed_angles)),
        )
        error = sum(
            abs(math.atan2(math.sin(angle - phase - position * step), math.cos(angle - phase - position * step)))
            for position, angle in enumerate(observed_angles)
        )
        alignments.append((error, phase, step, direction))
    _, phase, step, direction = min(alignments)

    deltas: dict[int, Vector] = {}
    for position, index in enumerate(loop):
        angle = phase + position * step
        target = Vector(
            (
                center_x + radius * math.cos(angle),
                depths[position],
                center_z + radius * math.sin(angle),
            )
        )
        deltas[index] = target - coordinates[index]
        result[index] = target

    visited = set(loop)
    frontier = set(loop)
    ring_sizes = []
    for falloff in RING_FALLOFF:
        ring = {
            linked
            for index in frontier
            for linked in neighbors[index]
            if linked not in visited
            and abs(coordinates[linked].x - center_x) <= half_width * 2.2
            and abs(coordinates[linked].z - center_z) <= max(half_height * 3.2, radius * 2.0)
            and abs(coordinates[linked].y - sum(point.y for point in points) / len(points)) <= 0.10
        }
        propagated = {}
        for index in ring:
            sources = [deltas[linked] for linked in neighbors[index] if linked in deltas]
            if sources:
                propagated[index] = sum(sources, Vector()) / len(sources) * falloff
        for index, delta in propagated.items():
            result[index] = coordinates[index] + delta
        deltas.update(propagated)
        visited.update(ring)
        frontier = ring
        ring_sizes.append(len(ring))

    displacement = [(result[index] - coordinates[index]).length for index in deltas]
    return result, {
        "target_radius": radius,
        "target_phase_radians": phase,
        "target_winding": int(direction),
        "radius_scale": radius_scale,
        "boundary_vertices": len(loop),
        "surrounding_ring_vertices": ring_sizes,
        "moved_vertices": sum(value > 1.0e-8 for value in displacement),
        "mean_displacement": sum(displacement) / len(displacement),
        "max_displacement": max(displacement),
    }


def apply_to_shape_keys(
    body: bpy.types.Object,
    before: list[Vector],
    after: list[Vector],
) -> float:
    mesh = body.data
    keys = mesh.shape_keys.key_blocks if mesh.shape_keys else []
    relative_deltas = [
        [point.co.copy() - before[index] for index, point in enumerate(key.data)]
        for key in keys[1:]
    ]
    for index, coordinate in enumerate(after):
        mesh.vertices[index].co = coordinate
        if keys:
            keys[0].data[index].co = coordinate
    for key, deltas in zip(keys[1:], relative_deltas):
        for index, delta in enumerate(deltas):
            key.data[index].co = after[index] + delta
    mesh.update()
    error = 0.0
    for key, deltas in zip(keys[1:], relative_deltas):
        for index, expected in enumerate(deltas):
            error = max(error, (key.data[index].co - keys[0].data[index].co - expected).length)
    return error


def render_mouth(body: bpy.types.Object, path: Path, *, wire: bool = False) -> None:
    preview = body.copy()
    preview.data = body.data.copy()
    preview.name = "EVIDENCE_MouthCircle"
    bpy.context.scene.collection.objects.link(preview)
    preview.animation_data_clear()
    preview.parent = None
    preview.matrix_parent_inverse.identity()
    preview.modifiers.clear()
    preview.color = (0.43, 0.72, 0.16, 1.0)
    subdivision = preview.modifiers.new("EVIDENCE_Subdivision", "SUBSURF")
    subdivision.subdivision_type = "CATMULL_CLARK"
    subdivision.levels = 1
    subdivision.render_levels = 1
    if wire:
        modifier = preview.modifiers.new("EVIDENCE_Wireframe", "WIREFRAME")
        modifier.thickness = 0.00012
        modifier.use_replace = True
        modifier.use_even_offset = True

    scene = bpy.context.scene
    hidden = {obj.name: obj.hide_render for obj in scene.objects}
    camera_data = bpy.data.cameras.new("EVIDENCE_MouthCircleCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.19
    camera = bpy.data.objects.new("EVIDENCE_MouthCircleCamera", camera_data)
    scene.collection.objects.link(camera)
    target = MOUTH_HINT.copy()
    camera.location = target + Vector((0.0, -2.0, 0.0))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    original_camera = scene.camera
    original_engine = scene.render.engine
    original_filepath = scene.render.filepath
    original_resolution = (scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage)
    original_transparent = scene.render.film_transparent
    original_shading = (
        scene.display.shading.light,
        scene.display.shading.color_type,
        scene.display.shading.show_shadows,
        scene.display.shading.show_cavity,
        scene.display.shading.background_type,
        tuple(scene.display.shading.background_color),
    )
    try:
        for obj in scene.objects:
            obj.hide_render = obj not in {preview, camera}
        preview.hide_render = False
        scene.camera = camera
        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.shading.light = "STUDIO"
        scene.display.shading.color_type = "OBJECT"
        scene.display.shading.show_shadows = not wire
        scene.display.shading.show_cavity = False
        scene.display.shading.background_type = "VIEWPORT"
        scene.display.shading.background_color = (0.012, 0.016, 0.022)
        scene.render.resolution_x = 768
        scene.render.resolution_y = 768
        scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = "PNG"
        scene.render.film_transparent = False
        path = path.resolve()
        path.parent.mkdir(parents=True, exist_ok=True)
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        if not path.exists() or path.stat().st_size < 10_000:
            raise RuntimeError(f"Mouth evidence was not written: {path}")
    finally:
        scene.camera = original_camera
        scene.render.engine = original_engine
        scene.render.filepath = original_filepath
        scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = original_resolution
        scene.render.film_transparent = original_transparent
        (
            scene.display.shading.light,
            scene.display.shading.color_type,
            scene.display.shading.show_shadows,
            scene.display.shading.show_cavity,
            scene.display.shading.background_type,
            scene.display.shading.background_color,
        ) = original_shading
        for name, state in hidden.items():
            if name in bpy.data.objects:
                bpy.data.objects[name].hide_render = state
        bpy.data.objects.remove(preview, do_unlink=True)
        bpy.data.objects.remove(camera, do_unlink=True)


def main() -> None:
    args = parse_args()
    if not 0.85 <= args.radius_scale <= 1.15:
        raise RuntimeError("--radius-scale must be in [0.85, 1.15]")
    if not args.analyze_only and args.output is None:
        raise RuntimeError("--output is required unless --analyze-only is used")
    body = bpy.data.objects.get(BODY_NAME)
    rig = bpy.data.objects.get(RIG_NAME)
    if body is None or body.type != "MESH" or rig is None or rig.type != "ARMATURE":
        raise RuntimeError("Canonical lime alien body or rig is missing")
    if body.get(MARKER):
        raise RuntimeError("Circular mouth rework is already applied")
    if body.data.shape_keys and any(abs(key.value) > 1.0e-8 for key in body.data.shape_keys.key_blocks):
        raise RuntimeError("All shape keys must be zero before circular mouth rework")

    source = Path(bpy.data.filepath).resolve()
    source_sha = sha256(source)
    mesh = body.data
    topology_before = topology_digest(mesh)
    weights_before = weight_digest(body)
    coordinates_before = [vertex.co.copy() for vertex in mesh.vertices]
    neighbors = connectivity(mesh)
    mouth = select_mouth_contour(mesh, neighbors)
    loop = ordered_contour(mouth, neighbors)
    before_metrics = loop_metrics(coordinates_before, loop)
    coordinates_after, operation = circularize(
        coordinates_before, loop, neighbors, args.radius_scale
    )
    after_metrics = loop_metrics(coordinates_after, loop)
    report = {
        "source": source.as_posix(),
        "source_sha256_before": source_sha,
        "shape_keys": len(mesh.shape_keys.key_blocks) if mesh.shape_keys else 0,
        "mouth_boundary_indices": loop,
        "before": before_metrics,
        "operation": operation,
        "after": after_metrics,
    }
    if args.analyze_only:
        args.report.resolve().parent.mkdir(parents=True, exist_ok=True)
        args.report.resolve().write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print("ADK_MOUTH_CIRCLE_ANALYSIS=" + json.dumps(report, ensure_ascii=False), flush=True)
        return

    evidence = args.evidence_dir.resolve()
    render_mouth(body, evidence / "mouth-circle-before.png")
    shape_error = apply_to_shape_keys(body, coordinates_before, coordinates_after)
    if shape_error > 1.0e-6:
        raise RuntimeError(f"Relative shape-key delta changed: {shape_error}")
    if topology_digest(mesh) != topology_before:
        raise RuntimeError("Topology changed during circular mouth rework")
    if weight_digest(body) != weights_before:
        raise RuntimeError("Skin weights changed during circular mouth rework")
    if abs(after_metrics["width_to_height"] - 1.0) > 0.03:
        raise RuntimeError(f"Mouth did not become circular: {after_metrics['width_to_height']}")
    if after_metrics["radius_cv"] > 0.03:
        raise RuntimeError(f"Circular mouth radius variation is too high: {after_metrics['radius_cv']}")
    render_mouth(body, evidence / "mouth-circle-after.png")
    render_mouth(body, evidence / "mouth-circle-wire-after.png", wire=True)

    body[MARKER] = True
    body[BOUNDARY_PROPERTY] = loop
    report["regression_checks"] = {
        "topology_unchanged": topology_digest(mesh) == topology_before,
        "skin_weights_unchanged": weight_digest(body) == weights_before,
        "shape_key_relative_delta_max_error": shape_error,
        "circle_aspect_within_3_percent": abs(after_metrics["width_to_height"] - 1.0) <= 0.03,
        "circle_radius_cv_within_3_percent": after_metrics["radius_cv"] <= 0.03,
    }
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(output), check_existing=False, compress=True)
    report["output"] = output.as_posix()
    report["output_sha256"] = sha256(output)
    args.report.resolve().parent.mkdir(parents=True, exist_ok=True)
    args.report.resolve().write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ADK_MOUTH_CIRCLE_REWORK=" + json.dumps(report, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
