"""Turn the lime alien's neutral mouth indentation into a smooth rounded rim.

The operation preserves vertex count, topology, skin weights, and all relative
shape-key deltas.  The closed mouth rim is evenly redistributed on an
area-preserving circle, while three surrounding face loops are locally
redistributed and constrained-faired into the fixed outer face surface.
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
ENTRANCE_MARKER = "ADK_MouthEntranceCircleRework_v4_subtle_oval"
ENTRANCE_PROPERTY = "ADK_MouthEntranceBoundaryIndices"
ENTRANCE_TARGET_ASPECT = 1.08
ENTRANCE_ASPECT_TOLERANCE = 0.015
TRANSITION_MARKER = "ADK_MouthFaceTransitionRetopology_v4_concentric_loops"
TRANSITION_PROPERTY = "ADK_MouthFaceTransitionIndices"
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


def select_mouth_ring_stack(
    mesh: bpy.types.Mesh,
    inner_loop: set[int],
    neighbors: list[set[int]],
) -> tuple[set[int], int, set[int]]:
    """Return the skin/cavity junction and the recessed fan center.

    The first 20-vertex ring outside ``inner_loop`` is still inside the mouth
    tube.  Two further front-facing closed edge loops lead to the 24-vertex
    skin/cavity junction that is actually visible as the entrance.
    """
    adjacent = set().union(*(neighbors[index] for index in inner_loop)) - inner_loop
    center = max(adjacent, key=lambda index: len(neighbors[index] & inner_loop))
    throat = adjacent - {center}
    if len(throat) != len(inner_loop):
        raise RuntimeError(
            f"Mouth throat does not match inner ring: inner={len(inner_loop)}, "
            f"throat={len(throat)}, center={center}"
        )
    if len(neighbors[center] & inner_loop) < len(inner_loop) // 2:
        raise RuntimeError("Could not identify the recessed mouth fan center")

    def advance_outward(current: set[int], blocked: set[int]) -> set[int]:
        candidates = {
            linked
            for index in current
            for linked in neighbors[index]
            if linked not in current
            and linked not in blocked
            and mesh.vertices[linked].co.y < mesh.vertices[index].co.y - 1.0e-3
        }
        closed = []
        for group in components(candidates, neighbors):
            degrees = {index: len(neighbors[index] & group) for index in group}
            if 16 <= len(group) <= 32 and all(degree == 2 for degree in degrees.values()):
                closed.append(group)
        if not closed:
            raise RuntimeError(
                "Could not trace the next closed mouth loop: "
                f"current={sorted(current)}, candidates={sorted(candidates)}"
            )
        return min(
            closed,
            key=lambda group: sum(
                (mesh.vertices[index].co - MOUTH_HINT).length_squared for index in group
            )
            / len(group),
        )

    first_outer = advance_outward(throat, inner_loop | {center})
    entrance = advance_outward(first_outer, inner_loop | throat | {center})
    if len(entrance) != 24:
        raise RuntimeError(f"Expected a 24-vertex outer mouth entrance, got {len(entrance)}")
    return entrance, center, first_outer


def select_mouth_entrance(
    mesh: bpy.types.Mesh,
    inner_loop: set[int],
    neighbors: list[set[int]],
) -> tuple[set[int], int]:
    entrance, center, _ = select_mouth_ring_stack(mesh, inner_loop, neighbors)
    return entrance, center


def select_mouth_face_rings(
    mesh: bpy.types.Mesh,
    inner_loop: set[int],
    neighbors: list[set[int]],
) -> tuple[set[int], set[int], set[int], set[int]]:
    """Select four successive face-side rings outside the visible rim."""
    entrance, _, first_outer = select_mouth_ring_stack(mesh, inner_loop, neighbors)

    def in_face_patch(index: int, outer: bool = False) -> bool:
        point = mesh.vertices[index].co
        x_limit = (-0.10, 0.08) if outer else (-0.09, 0.07)
        z_limit = (0.44, 0.59) if outer else (0.45, 0.58)
        return x_limit[0] < point.x < x_limit[1] and z_limit[0] < point.z < z_limit[1]

    first_face = {
        linked
        for index in entrance
        for linked in neighbors[index]
        if linked not in entrance and linked not in first_outer and in_face_patch(linked)
    }
    second_face = {
        linked
        for index in first_face
        for linked in neighbors[index]
        if linked not in first_face
        and linked not in entrance
        and linked not in first_outer
        and in_face_patch(linked, outer=True)
    }
    third_face = {
        linked
        for index in second_face
        for linked in neighbors[index]
        if linked not in first_face
        and linked not in second_face
        and linked not in entrance
        and linked not in first_outer
        and -0.12 < mesh.vertices[linked].co.x < 0.10
        and 0.42 < mesh.vertices[linked].co.z < 0.61
    }
    fourth_face = {
        linked
        for index in third_face
        for linked in neighbors[index]
        if linked not in first_face
        and linked not in second_face
        and linked not in third_face
        and linked not in entrance
        and linked not in first_outer
        and -0.14 < mesh.vertices[linked].co.x < 0.12
        and 0.40 < mesh.vertices[linked].co.z < 0.63
    }
    sizes = tuple(map(len, (first_face, second_face, third_face, fourth_face)))
    if sizes != (24, 24, 23, 23):
        raise RuntimeError(
            "Could not isolate the four mouth-to-face transition rings: "
            f"sizes={sizes}"
        )
    return first_face, second_face, third_face, fourth_face


def ordered_by_angle(mesh: bpy.types.Mesh, indices: set[int]) -> list[int]:
    points = [mesh.vertices[index].co for index in indices]
    center_x = sum(point.x for point in points) / len(points)
    center_z = sum(point.z for point in points) / len(points)
    return sorted(
        indices,
        key=lambda index: math.atan2(
            mesh.vertices[index].co.z - center_z,
            mesh.vertices[index].co.x - center_x,
        ),
    )


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
    blocked_indices: set[int] | None = None,
    target_aspect: float = 1.0,
) -> tuple[list[Vector], dict[str, object]]:
    result = [point.copy() for point in coordinates]
    blocked = blocked_indices or set()
    points = [coordinates[index] for index in loop]
    center_x = (min(point.x for point in points) + max(point.x for point in points)) * 0.5
    center_z = (min(point.z for point in points) + max(point.z for point in points)) * 0.5
    half_width = (max(point.x for point in points) - min(point.x for point in points)) * 0.5
    half_height = (max(point.z for point in points) - min(point.z for point in points)) * 0.5
    radius = math.sqrt(half_width * half_height) * radius_scale
    radius_x = radius * math.sqrt(target_aspect)
    radius_z = radius / math.sqrt(target_aspect)

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
                center_x + radius_x * math.cos(angle),
                depths[position],
                center_z + radius_z * math.sin(angle),
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
            and linked not in blocked
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
        "target_aspect": target_aspect,
        "boundary_vertices": len(loop),
        "surrounding_ring_vertices": ring_sizes,
        "moved_vertices": sum(value > 1.0e-8 for value in displacement),
        "mean_displacement": sum(displacement) / len(displacement),
        "max_displacement": max(displacement),
    }


def relax_mouth_face_transition(
    mesh: bpy.types.Mesh,
    coordinates: list[Vector],
    inner_loop: set[int],
    neighbors: list[set[int]],
    strength: float = 1.0,
) -> tuple[list[Vector], dict[str, object]]:
    """Redistribute and fair the mouth-to-face loops without changing topology."""
    if not 0.0 <= strength <= 1.0:
        raise ValueError("Mouth face-transition strength must be in [0, 1]")
    first_face, second_face, third_face, fourth_face = select_mouth_face_rings(
        mesh, inner_loop, neighbors
    )
    _, _, inner_lip_ring = select_mouth_ring_stack(mesh, inner_loop, neighbors)
    movable_rings = (first_face, second_face, third_face)
    movable = set().union(*movable_rings)
    ring_lookup = {
        index: ring_number
        for ring_number, ring in enumerate(movable_rings)
        for index in ring
    }
    base_equalization_factors = (0.70, 0.52, 0.28)
    base_fairing_factors = (0.55, 0.75, 0.55)
    base_final_spacing_factors = (0.72, 0.48, 0.24)
    base_inner_lip_equalization = 0.80
    equalization_factors = tuple(value * strength for value in base_equalization_factors)
    fairing_factors = tuple(value * strength for value in base_fairing_factors)
    final_spacing_factors = tuple(value * strength for value in base_final_spacing_factors)
    displacement_cap = 0.005 * strength
    inner_lip_displacement_cap = 0.0035 * strength

    def ordered_ring(ring: set[int]) -> list[int]:
        return ordered_by_angle(mesh, ring)

    def smooth_periodic(values: list[float], iterations: int = 6) -> list[float]:
        result = list(values)
        for _ in range(iterations):
            result = [
                result[index] * 0.5
                + result[(index - 1) % len(result)] * 0.25
                + result[(index + 1) % len(result)] * 0.25
                for index in range(len(result))
            ]
        return result

    def ellipse_targets(points: list[Vector], ring: set[int]) -> dict[int, Vector]:
        ordered = ordered_ring(ring)
        ring_points = [points[index] for index in ordered]
        center_x = (
            min(point.x for point in ring_points) + max(point.x for point in ring_points)
        ) * 0.5
        center_z = (
            min(point.z for point in ring_points) + max(point.z for point in ring_points)
        ) * 0.5
        radius_x = (
            max(point.x for point in ring_points) - min(point.x for point in ring_points)
        ) * 0.5
        radius_z = (
            max(point.z for point in ring_points) - min(point.z for point in ring_points)
        ) * 0.5
        observed_angles = [
            math.atan2(
                (point.z - center_z) / max(radius_z, 1.0e-9),
                (point.x - center_x) / max(radius_x, 1.0e-9),
            )
            for point in ring_points
        ]
        step = 2.0 * math.pi / len(ordered)
        phase = math.atan2(
            sum(
                math.sin(angle - position * step)
                for position, angle in enumerate(observed_angles)
            ),
            sum(
                math.cos(angle - position * step)
                for position, angle in enumerate(observed_angles)
            ),
        )
        depths = smooth_periodic([point.y for point in ring_points])
        return {
            index: Vector(
                (
                    center_x + radius_x * math.cos(phase + position * step),
                    depths[position],
                    center_z + radius_z * math.sin(phase + position * step),
                )
            )
            for position, index in enumerate(ordered)
        }

    def edge_length_cv(points: list[Vector], ring: set[int]) -> float:
        ordered = ordered_ring(ring)
        lengths = [
            (points[ordered[(position + 1) % len(ordered)]] - points[index]).length
            for position, index in enumerate(ordered)
        ]
        mean = sum(lengths) / len(lengths)
        return math.sqrt(
            sum((length - mean) ** 2 for length in lengths) / len(lengths)
        ) / mean

    def roughness(points: list[Vector]) -> float:
        values = []
        for index in movable:
            average = sum((points[linked] for linked in neighbors[index]), Vector()) / len(
                neighbors[index]
            )
            values.append((points[index] - average).length)
        return sum(values) / len(values)

    equalized = [point.copy() for point in coordinates]
    for ring, factor in zip(movable_rings, equalization_factors):
        for index, target in ellipse_targets(coordinates, ring).items():
            equalized[index] = coordinates[index].lerp(target, factor)

    ordered_loops = [ordered_ring(ring) for ring in movable_rings]
    loop_neighbors = {}
    for loop in ordered_loops:
        for position, index in enumerate(loop):
            loop_neighbors[index] = (
                loop[(position - 1) % len(loop)],
                loop[(position + 1) % len(loop)],
            )

    work = [point.copy() for point in equalized]
    for _ in range(36):
        updated = [point.copy() for point in work]
        for index, ring_number in ring_lookup.items():
            previous, following = loop_neighbors[index]
            tangent_target = (work[previous] + work[following]) * 0.5
            radial_neighbors = [
                linked
                for linked in neighbors[index]
                if linked not in movable_rings[ring_number]
            ]
            radial_target = (
                sum((work[linked] for linked in radial_neighbors), Vector())
                / len(radial_neighbors)
                if radial_neighbors
                else work[index]
            )
            target = tangent_target * 0.38 + radial_target * 0.62
            updated[index] = work[index].lerp(target, 0.32)
        work = updated

    result = [point.copy() for point in coordinates]
    for index, ring_number in ring_lookup.items():
        target = equalized[index].lerp(work[index], fairing_factors[ring_number])
        delta = target - coordinates[index]
        if delta.length > displacement_cap:
            delta.normalize()
            delta *= displacement_cap
        result[index] = coordinates[index] + delta

    spacing_source = [point.copy() for point in result]
    for ring, factor in zip(movable_rings, final_spacing_factors):
        for index, target in ellipse_targets(spacing_source, ring).items():
            candidate = spacing_source[index].lerp(target, factor)
            delta = candidate - coordinates[index]
            if delta.length > displacement_cap:
                delta.normalize()
                delta *= displacement_cap
            result[index] = coordinates[index] + delta

    edge_cv_before = [edge_length_cv(coordinates, ring) for ring in movable_rings]
    edge_cv_after = [edge_length_cv(result, ring) for ring in movable_rings]
    inner_lip_cv_before = edge_length_cv(coordinates, inner_lip_ring)
    inner_lip_targets = ellipse_targets(coordinates, inner_lip_ring)
    for index, target in inner_lip_targets.items():
        candidate = coordinates[index].lerp(
            target, base_inner_lip_equalization * strength
        )
        delta = candidate - coordinates[index]
        if delta.length > inner_lip_displacement_cap:
            delta.normalize()
            delta *= inner_lip_displacement_cap
        result[index] = coordinates[index] + delta
    inner_lip_cv_after = edge_length_cv(result, inner_lip_ring)

    before_roughness = roughness(coordinates)
    after_roughness = roughness(result)
    all_moved = movable | inner_lip_ring
    displacements = [(result[index] - coordinates[index]).length for index in all_moved]
    return result, {
        "method": "concentric_loop_redistribution_and_constrained_fairing",
        "fairing_iterations": 36,
        "fairing_step": 0.32,
        "tangential_weight": 0.38,
        "radial_weight": 0.62,
        "application_strength": strength,
        "first_face_ring_vertices": sorted(first_face),
        "second_face_ring_vertices": sorted(second_face),
        "third_face_ring_vertices": sorted(third_face),
        "fourth_face_boundary_vertices": sorted(fourth_face),
        "inner_lip_ring_vertices": sorted(inner_lip_ring),
        "equalization_factors": list(base_equalization_factors),
        "fairing_factors": list(base_fairing_factors),
        "final_spacing_factors": list(base_final_spacing_factors),
        "applied_equalization_factors": list(equalization_factors),
        "applied_fairing_factors": list(fairing_factors),
        "applied_final_spacing_factors": list(final_spacing_factors),
        "inner_lip_equalization_factor": base_inner_lip_equalization,
        "applied_inner_lip_equalization_factor": base_inner_lip_equalization * strength,
        "inner_lip_ring_edge_length_cv_before": inner_lip_cv_before,
        "inner_lip_ring_edge_length_cv_after": inner_lip_cv_after,
        "ring_edge_length_cv_before": edge_cv_before,
        "ring_edge_length_cv_after": edge_cv_after,
        "moved_vertices": sum(value > 1.0e-8 for value in displacements),
        "mean_displacement": sum(displacements) / len(displacements),
        "max_displacement": max(displacements),
        "displacement_cap": displacement_cap,
        "laplacian_roughness_before": before_roughness,
        "laplacian_roughness_after": after_roughness,
        "laplacian_roughness_reduction": 1.0 - after_roughness / before_roughness,
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
    if body.get(TRANSITION_MARKER):
        raise RuntimeError("Mouth-to-face transition relaxation is already applied")
    if body.data.shape_keys and any(abs(key.value) > 1.0e-8 for key in body.data.shape_keys.key_blocks):
        raise RuntimeError("All shape keys must be zero before circular mouth rework")

    source = Path(bpy.data.filepath).resolve()
    source_sha = sha256(source)
    mesh = body.data
    topology_before = topology_digest(mesh)
    weights_before = weight_digest(body)
    coordinates_before = [vertex.co.copy() for vertex in mesh.vertices]
    neighbors = connectivity(mesh)
    if body.get(MARKER) and body.get(BOUNDARY_PROPERTY):
        loop = [int(index) for index in body[BOUNDARY_PROPERTY]]
        apply_inner = False
    else:
        mouth = select_mouth_contour(mesh, neighbors)
        loop = ordered_contour(mouth, neighbors)
        apply_inner = True
    entrance, fan_center = select_mouth_entrance(mesh, set(loop), neighbors)
    entrance_loop = ordered_by_angle(mesh, entrance)
    apply_entrance = not body.get(ENTRANCE_MARKER)
    inner_before = loop_metrics(coordinates_before, loop)
    entrance_before = loop_metrics(coordinates_before, entrance_loop)
    coordinates_work = coordinates_before
    inner_operation = None
    if apply_inner:
        coordinates_work, inner_operation = circularize(
            coordinates_work,
            loop,
            neighbors,
            args.radius_scale,
            blocked_indices=set(entrance_loop),
        )
    coordinates_after = coordinates_work
    entrance_operation = None
    if apply_entrance:
        coordinates_after, entrance_operation = circularize(
            coordinates_work,
            entrance_loop,
            neighbors,
            args.radius_scale,
            blocked_indices=set(loop) | {fan_center},
            target_aspect=ENTRANCE_TARGET_ASPECT,
        )
    coordinates_after, transition_operation = relax_mouth_face_transition(
        mesh,
        coordinates_after,
        set(loop),
        neighbors,
    )
    inner_after = loop_metrics(coordinates_after, loop)
    entrance_after = loop_metrics(coordinates_after, entrance_loop)
    report = {
        "source": source.as_posix(),
        "source_sha256_before": source_sha,
        "shape_keys": len(mesh.shape_keys.key_blocks) if mesh.shape_keys else 0,
        "mouth_boundary_indices": loop,
        "mouth_entrance_indices": entrance_loop,
        "mouth_fan_center_index": fan_center,
        "inner": {
            "applied": apply_inner,
            "before": inner_before,
            "operation": inner_operation,
            "after": inner_after,
        },
        "entrance": {
            "applied": apply_entrance,
            "before": entrance_before,
            "operation": entrance_operation,
            "after": entrance_after,
        },
        "face_transition": transition_operation,
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
    if abs(inner_after["width_to_height"] - 1.0) > 0.03 or inner_after["radius_cv"] > 0.03:
        raise RuntimeError("Inner mouth ring did not remain circular")
    if (
        abs(entrance_after["width_to_height"] - ENTRANCE_TARGET_ASPECT)
        > ENTRANCE_ASPECT_TOLERANCE
    ):
        raise RuntimeError(
            "Visible mouth entrance missed the subtle oval target: "
            f"{entrance_after['width_to_height']}"
        )
    render_mouth(body, evidence / "mouth-circle-after.png")
    render_mouth(body, evidence / "mouth-circle-wire-after.png", wire=True)

    body[MARKER] = True
    body[BOUNDARY_PROPERTY] = loop
    body[ENTRANCE_MARKER] = True
    body[ENTRANCE_PROPERTY] = entrance_loop
    body[TRANSITION_MARKER] = True
    body[TRANSITION_PROPERTY] = (
        transition_operation["first_face_ring_vertices"]
        + transition_operation["second_face_ring_vertices"]
        + transition_operation["third_face_ring_vertices"]
        + transition_operation["fourth_face_boundary_vertices"]
    )
    report["regression_checks"] = {
        "topology_unchanged": topology_digest(mesh) == topology_before,
        "skin_weights_unchanged": weight_digest(body) == weights_before,
        "shape_key_relative_delta_max_error": shape_error,
        "inner_circle_aspect_within_3_percent": abs(inner_after["width_to_height"] - 1.0) <= 0.03,
        "inner_circle_radius_cv_within_3_percent": inner_after["radius_cv"] <= 0.03,
        "entrance_aspect_matches_subtle_oval": abs(
            entrance_after["width_to_height"] - ENTRANCE_TARGET_ASPECT
        )
        <= ENTRANCE_ASPECT_TOLERANCE,
        "face_transition_roughness_reduced": transition_operation[
            "laplacian_roughness_reduction"
        ]
        >= 0.25,
        "face_transition_loop_spacing_improved": all(
            after < before
            for before, after in zip(
                transition_operation["ring_edge_length_cv_before"],
                transition_operation["ring_edge_length_cv_after"],
            )
        ),
        "inner_lip_loop_spacing_improved": transition_operation[
            "inner_lip_ring_edge_length_cv_after"
        ]
        < transition_operation["inner_lip_ring_edge_length_cv_before"],
        "face_transition_max_displacement_bounded": transition_operation[
            "max_displacement"
        ]
        <= 0.005 + 1.0e-8,
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
