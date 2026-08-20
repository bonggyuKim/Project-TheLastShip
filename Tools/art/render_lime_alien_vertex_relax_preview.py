"""Render an isolated, bounded vertex-relax preview for the lime alien.

This is deliberately a preview-only operation.  It relaxes the copied basis
mesh's actual coordinates while keeping open boundaries fixed, verifies that
the canonical source did not change, and records displacement/smoothness data.

Usage::

    blender -b LastShiftLimeAlien_UnityExport_LeftToeFixed.blend \
        -P render_lime_alien_vertex_relax_preview.py -- \
        --output-dir docs/art/evidence/last-shift-lime-alien-vertex-relax-preview \
        --blend-output LastShiftLimeAlien_VertexRelaxPreview.blend
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from render_lime_alien_subdivision_preview import (  # noqa: E402
    add_text,
    duplicate_materials,
    mesh_digest,
    render,
    require_scene,
    world_bounds,
)


CANONICAL_SOURCE = (
    "ArtSource/Characters/LastShiftLimeAlien/"
    "LastShiftLimeAlien_UnityExport_LeftToeFixed.blend"
)
BODY_NAME = "LastShift_LimeAlien_Body"


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--blend-output", type=Path, required=True)
    parser.add_argument("--iterations", type=int, default=12)
    parser.add_argument("--lambda-factor", type=float, default=0.80)
    parser.add_argument("--mu-factor", type=float, default=-0.82)
    parser.add_argument("--min-displacement-ratio", type=float, default=0.020)
    parser.add_argument("--max-displacement-ratio", type=float, default=0.025)
    return parser.parse_args(raw)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def topology_metrics(obj: bpy.types.Object) -> dict[str, int]:
    mesh = obj.data
    mesh.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    result = {
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "polygons": len(mesh.polygons),
        "triangles": len(mesh.loop_triangles),
        "boundary_edges": sum(edge.is_boundary for edge in bm.edges),
        "interior_nonmanifold_edges": sum(
            not edge.is_manifold and not edge.is_boundary for edge in bm.edges
        ),
        "loose_edges": sum(not edge.link_faces for edge in bm.edges),
    }
    bm.free()
    return result


def basis_copy(
    source: bpy.types.Object,
    collection: bpy.types.Collection,
    name: str,
) -> bpy.types.Object:
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name = name
    collection.objects.link(obj)
    obj.animation_data_clear()
    obj.parent = None
    obj.matrix_parent_inverse.identity()
    obj.modifiers.clear()
    if obj.data.shape_keys:
        obj.shape_key_clear()
    require(obj.data.shape_keys is None, f"Preview copy still has shape keys: {name}")
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj.data.update()
    return obj


def connectivity(mesh: bpy.types.Mesh) -> tuple[list[list[int]], set[int]]:
    neighbors = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        neighbors[a].add(b)
        neighbors[b].add(a)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    boundary = {vertex.index for edge in bm.edges if edge.is_boundary for vertex in edge.verts}
    bm.free()
    return [sorted(items) for items in neighbors], boundary


def laplacian_mean(coordinates: list[Vector], neighbors: list[list[int]]) -> float:
    magnitudes = []
    for index, linked in enumerate(neighbors):
        if not linked:
            continue
        average = sum((coordinates[item] for item in linked), Vector()) / len(linked)
        magnitudes.append((average - coordinates[index]).length)
    return sum(magnitudes) / len(magnitudes) if magnitudes else 0.0


def taubin_relax(
    obj: bpy.types.Object,
    *,
    iterations: int,
    lambda_factor: float,
    mu_factor: float,
) -> dict[str, object]:
    require(1 <= iterations <= 12, "--iterations must be in [1, 12]")
    require(0.0 < lambda_factor < 1.0, "--lambda-factor must be in (0, 1)")
    require(-1.0 < mu_factor < 0.0, "--mu-factor must be in (-1, 0)")
    mesh = obj.data
    neighbors, boundary = connectivity(mesh)
    original = [vertex.co.copy() for vertex in mesh.vertices]
    coordinates = [value.copy() for value in original]
    before_laplacian = laplacian_mean(coordinates, neighbors)

    movable = [index for index, linked in enumerate(neighbors) if linked and index not in boundary]
    for _ in range(iterations):
        for factor in (lambda_factor, mu_factor):
            current = [value.copy() for value in coordinates]
            for index in movable:
                linked = neighbors[index]
                average = sum((current[item] for item in linked), Vector()) / len(linked)
                coordinates[index] = current[index] + factor * (average - current[index])

    for vertex, coordinate in zip(mesh.vertices, coordinates):
        vertex.co = coordinate
    mesh.update()

    displacements = [(after - before).length for before, after in zip(original, coordinates)]
    moved = [value for value in displacements if value > 1.0e-7]
    sorted_moved = sorted(moved)
    percentile_index = max(0, math.ceil(len(sorted_moved) * 0.95) - 1)
    boundary_max = max((displacements[index] for index in boundary), default=0.0)
    return {
        "iterations": iterations,
        "lambda_factor": lambda_factor,
        "mu_factor": mu_factor,
        "movable_vertices": len(movable),
        "boundary_vertices_pinned": len(boundary),
        "moved_vertices": len(moved),
        "mean_displacement": sum(moved) / len(moved) if moved else 0.0,
        "p95_displacement": sorted_moved[percentile_index] if sorted_moved else 0.0,
        "max_displacement": max(displacements, default=0.0),
        "boundary_max_displacement": boundary_max,
        "laplacian_mean_before": before_laplacian,
        "laplacian_mean_after": laplacian_mean(coordinates, neighbors),
    }


def main() -> None:
    args = parse_args()
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    body, source_eyes, rig = require_scene()
    source_digest_before = mesh_digest(body)
    source_modifiers = [(modifier.name, modifier.type) for modifier in body.modifiers]
    rig_position = rig.data.pose_position
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()

    source_topology = topology_metrics(body)
    source_minimum, source_maximum = world_bounds([body, source_eyes])
    source_diagonal = (source_maximum - source_minimum).length
    source_width = source_maximum.x - source_minimum.x
    source_height = source_maximum.z - source_minimum.z
    source_center_x = (source_minimum.x + source_maximum.x) * 0.5
    spacing = source_width * 1.62

    collection = bpy.data.collections.new("LimeAlien_Vertex_Relax_Preview")
    bpy.context.scene.collection.children.link(collection)
    for obj in bpy.context.scene.objects:
        obj.hide_render = True
        obj.hide_viewport = True
        if obj.name in bpy.context.view_layer.objects:
            obj.hide_set(True)

    base = basis_copy(body, collection, "PREVIEW_BASE_Body")
    relaxed = basis_copy(body, collection, "PREVIEW_VERTEX_RELAX_Body")
    base_eyes = basis_copy(source_eyes, collection, "PREVIEW_BASE_Eyes")
    relaxed_eyes = basis_copy(source_eyes, collection, "PREVIEW_VERTEX_RELAX_Eyes")
    relax_metrics = taubin_relax(
        relaxed,
        iterations=args.iterations,
        lambda_factor=args.lambda_factor,
        mu_factor=args.mu_factor,
    )
    relaxed_topology = topology_metrics(relaxed)

    max_ratio = relax_metrics["max_displacement"] / source_diagonal
    smoothness_reduction = 1.0 - (
        relax_metrics["laplacian_mean_after"] / relax_metrics["laplacian_mean_before"]
    )
    require(relaxed_topology == source_topology, "Vertex relaxation changed topology")
    require(
        0.0 <= args.min_displacement_ratio <= args.max_displacement_ratio,
        "Displacement ratio limits must satisfy 0 <= min <= max",
    )
    require(relax_metrics["moved_vertices"] > 0, "Vertex relaxation moved no vertices")
    require(relax_metrics["boundary_max_displacement"] < 1.0e-8, "Open boundary moved")
    require(max_ratio >= args.min_displacement_ratio, "Vertex displacement missed preview target")
    require(max_ratio <= args.max_displacement_ratio, "Vertex displacement exceeded preview limit")
    require(smoothness_reduction > 0.0, "Vertex relaxation did not reduce Laplacian roughness")

    bodies = [base, relaxed]
    eyes = [base_eyes, relaxed_eyes]
    for index, pair in enumerate(zip(bodies, eyes)):
        offset = (index - 0.5) * spacing - source_center_x
        for obj in pair:
            obj.location.x += offset
            obj.hide_render = False
            obj.hide_viewport = False
            obj.hide_set(False)
    duplicate_materials(base, "BASE", (0.34, 0.58, 0.13, 1.0))
    duplicate_materials(relaxed, "VERTEX_RELAX", (0.48, 0.88, 0.18, 1.0))

    labels = [
        add_text(
            collection,
            "BASE / 11.4K TRI",
            Vector((-spacing * 0.5, source_minimum.y - source_width * 0.18, source_minimum.z - source_height * 0.13)),
            source_height * 0.04,
            (0.78, 0.86, 0.92, 1.0),
        ),
        add_text(
            collection,
            "ACTUAL VERTEX RELAX / 11.4K TRI",
            Vector((spacing * 0.5, source_minimum.y - source_width * 0.18, source_minimum.z - source_height * 0.13)),
            source_height * 0.04,
            (0.64, 0.96, 0.40, 1.0),
        ),
    ]

    camera_data = bpy.data.cameras.new("PREVIEW_VertexRelax_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("PREVIEW_VertexRelax_Camera", camera_data)
    collection.objects.link(camera)
    camera.hide_render = False

    front = output_dir / "lime-alien-vertex-relax-front.png"
    oblique = output_dir / "lime-alien-vertex-relax-oblique.png"
    wire = output_dir / "lime-alien-vertex-relax-wire.png"
    render(front, camera, bodies, eyes, labels, turntable_degrees=0.0, fit_camera=True, show_labels=True)
    render(oblique, camera, bodies, eyes, labels, turntable_degrees=24.0, fit_camera=False, show_labels=False)
    wire_modifiers = []
    for obj in bodies:
        modifier = obj.modifiers.new("PREVIEW_Wireframe", "WIREFRAME")
        modifier.thickness = source_diagonal * 0.00045
        modifier.use_replace = True
        modifier.use_even_offset = True
        wire_modifiers.append((obj, modifier))
    render(wire, camera, bodies, eyes, labels, turntable_degrees=24.0, fit_camera=False, show_labels=False)

    for obj, modifier in wire_modifiers:
        obj.modifiers.remove(modifier)
    for obj in bodies:
        obj.rotation_euler.z = 0.0
    for obj in eyes:
        obj.rotation_euler.z = 0.0
    for label in labels:
        label.hide_render = False
    rig.data.pose_position = rig_position
    bpy.context.view_layer.update()

    source_digest_after = mesh_digest(body)
    require(source_digest_after == source_digest_before, "Canonical body changed during preview")
    require(
        [(modifier.name, modifier.type) for modifier in body.modifiers] == source_modifiers,
        "Canonical modifier stack changed during preview",
    )

    blend_output = args.blend_output.resolve()
    blend_output.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_output), check_existing=False, compress=True)

    report = {
        "source": CANONICAL_SOURCE,
        "canonical_body": BODY_NAME,
        "source_digest_before": source_digest_before,
        "source_digest_after": source_digest_after,
        "source_unchanged": True,
        "guardrail_profile": {
            "relaxed": "preview copy may change actual basis coordinates",
            "retained": [
                "canonical source remains byte-for-byte geometrically unchanged",
                "topology counts remain identical",
                "open mouth boundary vertices remain pinned",
                (
                    "maximum displacement stays within "
                    f"{args.min_displacement_ratio:.1%}-{args.max_displacement_ratio:.1%} "
                    "of source diagonal"
                ),
            ],
        },
        "topology_before": source_topology,
        "topology_after": relaxed_topology,
        "relaxation": {
            **relax_metrics,
            "source_diagonal": source_diagonal,
            "max_displacement_ratio": max_ratio,
            "laplacian_roughness_reduction": smoothness_reduction,
        },
        "renders": [front.name, oblique.name, wire.name],
        "visual_review": {
            "silhouette": "head, belly, hands, and feet remain readable at front and 24-degree oblique views",
            "surface": "eye surround, neck, forearms, and thighs show reduced faceting on the relaxed copy",
            "retained_detail": "eye rim, open mouth, fingers, and toes remain distinct",
        },
        "blend_output": args.blend_output.as_posix(),
        "decision_note": (
            "This preview isolates actual control-vertex relaxation at the original 11.4K-triangle cost. "
            "It is evidence for visual review, not a replacement for the current production FBXs."
        ),
    }
    report_path = output_dir / "report.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ADK_VERTEX_RELAX_REPORT=" + json.dumps(report, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
