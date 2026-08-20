"""Apply the production jelly surface to the LAST SHIFT lime alien.

The canonical authoring mesh keeps all shape keys.  Compatible triangle pairs
are joined without crossing UV, seam, sharp, vertex-color, or material
boundaries, and a non-destructive Catmull-Clark level is stored in the output
blend.  Runtime FBXs receive a baked copy with interpolated skin weights.

Usage::

    blender -b LastShiftLimeAlien_UnityExport_LeftToeFixed.blend \
        -P apply_lime_alien_jelly.py -- \
        --output LastShiftLimeAlien_UnityExport_Jelly.blend \
        --fbx-output LastShiftLimeAlien_RigifyDeform.fbx \
        --fbx-output LastShiftLimeAlien_RigifySoft.fbx \
        --evidence-dir docs/art/evidence/last-shift-lime-alien-jelly-production
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Quaternion, Vector


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from render_lime_alien_subdivision_preview import (  # noqa: E402
    add_text,
    duplicate_materials,
    render,
    set_camera_to_bounds,
    world_bounds,
)
from render_lime_alien_vertex_relax_preview import (  # noqa: E402
    connectivity,
    laplacian_mean,
)


BODY_NAME = "LastShift_LimeAlien_Body"
EYES_NAME = "LastShift_LimeAlien_Eyes"
RIG_NAME = "LastShift_LimeAlien_Rig"
MODIFIER_NAME = "Jelly_Surface_Subdivision_L1"


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--fbx-output", type=Path, action="append", default=[])
    parser.add_argument("--evidence-dir", type=Path, required=True)
    parser.add_argument("--quad-angle", type=float, default=60.0)
    parser.add_argument("--subdivision-level", type=int, default=1)
    parser.add_argument("--relax-iterations", type=int, default=12)
    parser.add_argument("--relax-lambda", type=float, default=0.8)
    parser.add_argument("--relax-mu", type=float, default=-0.82)
    parser.add_argument("--min-displacement-ratio", type=float, default=0.020)
    parser.add_argument("--max-displacement-ratio", type=float, default=0.025)
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
    if rig.data.pose_position != "REST":
        raise RuntimeError("Canonical rig must be saved in REST pose")
    return body, eyes, rig


def face_metrics(mesh: bpy.types.Mesh) -> dict[str, object]:
    mesh.calc_loop_triangles()
    sizes: dict[str, int] = {}
    for polygon in mesh.polygons:
        key = str(len(polygon.vertices))
        sizes[key] = sizes.get(key, 0) + 1
    quads = sizes.get("4", 0)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    boundary_edges = sum(1 for edge in bm.edges if edge.is_boundary)
    interior_nonmanifold_edges = sum(
        1 for edge in bm.edges if not edge.is_manifold and not edge.is_boundary
    )
    loose_edges = sum(1 for edge in bm.edges if not edge.link_faces)
    bm.free()
    return {
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "polygons": len(mesh.polygons),
        "triangles": len(mesh.loop_triangles),
        "face_sizes": sizes,
        "quad_ratio": quads / len(mesh.polygons) if mesh.polygons else 0.0,
        "smooth_polygons": sum(polygon.use_smooth for polygon in mesh.polygons),
        "flat_polygons": sum(not polygon.use_smooth for polygon in mesh.polygons),
        "boundary_edges": boundary_edges,
        "interior_nonmanifold_edges": interior_nonmanifold_edges,
        "loose_edges": loose_edges,
    }


def shade_surface_smooth(obj: bpy.types.Object) -> None:
    """Use one continuous normal treatment across the jelly surface."""
    if obj.type != "MESH":
        raise RuntimeError(f"Cannot smooth non-mesh object: {obj.name}")
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj.data.update()


def require_fully_smooth(obj: bpy.types.Object, label: str) -> None:
    flat = sum(not polygon.use_smooth for polygon in obj.data.polygons)
    if flat:
        raise RuntimeError(f"{label} contains {flat} flat-shaded polygons")


def evaluated_metrics(obj: bpy.types.Object) -> dict[str, object]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=depsgraph)
    try:
        return face_metrics(mesh)
    finally:
        evaluated.to_mesh_clear()


def coordinate_digest(obj: bpy.types.Object) -> str:
    digest = hashlib.sha256()
    for vertex in obj.data.vertices:
        digest.update(f"{vertex.co.x:.9f},{vertex.co.y:.9f},{vertex.co.z:.9f};".encode())
    if obj.data.shape_keys:
        for key in obj.data.shape_keys.key_blocks:
            digest.update(key.name.encode())
            for point in key.data:
                digest.update(f"{point.co.x:.9f},{point.co.y:.9f},{point.co.z:.9f};".encode())
    return digest.hexdigest()


def activate(obj: bpy.types.Object) -> None:
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_shape_preserving_relax(
    body: bpy.types.Object,
    *,
    iterations: int,
    lambda_factor: float,
    mu_factor: float,
    min_displacement_ratio: float,
    max_displacement_ratio: float,
) -> dict[str, object]:
    """Relax the basis while preserving every relative shape-key delta.

    Open boundary vertices (the mouth opening) stay pinned.  Each non-Basis
    key receives the exact same per-vertex displacement as Basis, so facial
    expressions do not acquire a different silhouette or lip gap.
    """
    if not 1 <= iterations <= 12:
        raise RuntimeError("--relax-iterations must be in [1, 12]")
    if not 0.0 < lambda_factor < 1.0:
        raise RuntimeError("--relax-lambda must be in (0, 1)")
    if not -1.0 < mu_factor < 0.0:
        raise RuntimeError("--relax-mu must be in (-1, 0)")
    if not 0.0 <= min_displacement_ratio <= max_displacement_ratio:
        raise RuntimeError("Invalid production displacement range")

    activate(body)
    body.active_shape_key_index = 0
    shape_keys = body.data.shape_keys.key_blocks if body.data.shape_keys else []
    if shape_keys and any(abs(key.value) > 1.0e-8 for key in shape_keys):
        raise RuntimeError("All shape keys must be zero before vertex relaxation")

    mesh = body.data
    neighbors, boundary = connectivity(mesh)
    basis_points = shape_keys[0].data if shape_keys else mesh.vertices
    original = [point.co.copy() for point in basis_points]
    coordinates = [value.copy() for value in original]
    shape_deltas = [
        [point.co.copy() - original[index] for index, point in enumerate(key.data)]
        for key in shape_keys[1:]
    ]
    before_laplacian = laplacian_mean(coordinates, neighbors)
    movable = [
        index for index, linked in enumerate(neighbors)
        if linked and index not in boundary
    ]

    for _ in range(iterations):
        for factor in (lambda_factor, mu_factor):
            current = [value.copy() for value in coordinates]
            for index in movable:
                linked = neighbors[index]
                average = sum((current[item] for item in linked), Vector()) / len(linked)
                coordinates[index] = current[index] + factor * (average - current[index])

    displacements = [
        (after - before).length for before, after in zip(original, coordinates)
    ]
    moved = sorted(value for value in displacements if value > 1.0e-7)
    percentile_index = max(0, math.ceil(len(moved) * 0.95) - 1)
    boundary_max = max((displacements[index] for index in boundary), default=0.0)
    minimum = Vector(tuple(min(value[axis] for value in original) for axis in range(3)))
    maximum = Vector(tuple(max(value[axis] for value in original) for axis in range(3)))
    source_diagonal = (maximum - minimum).length
    max_ratio = max(displacements, default=0.0) / source_diagonal

    if not moved:
        raise RuntimeError("Production vertex relaxation moved no vertices")
    if boundary_max >= 1.0e-8:
        raise RuntimeError("Production vertex relaxation moved an open boundary")
    if not min_displacement_ratio <= max_ratio <= max_displacement_ratio:
        raise RuntimeError(
            f"Production displacement ratio {max_ratio:.6f} is outside "
            f"[{min_displacement_ratio:.6f}, {max_displacement_ratio:.6f}]"
        )

    if shape_keys:
        for index, coordinate in enumerate(coordinates):
            shape_keys[0].data[index].co = coordinate
            mesh.vertices[index].co = coordinate
        for key, deltas in zip(shape_keys[1:], shape_deltas):
            for index, delta in enumerate(deltas):
                key.data[index].co = coordinates[index] + delta
    else:
        for vertex, coordinate in zip(mesh.vertices, coordinates):
            vertex.co = coordinate
    mesh.update()

    max_shape_delta_error = 0.0
    for key, deltas in zip(shape_keys[1:], shape_deltas):
        for index, expected in enumerate(deltas):
            actual = key.data[index].co - shape_keys[0].data[index].co
            max_shape_delta_error = max(max_shape_delta_error, (actual - expected).length)
    if max_shape_delta_error > 1.0e-6:
        raise RuntimeError(
            f"Shape-key relative delta changed during relaxation: {max_shape_delta_error}"
        )

    after_laplacian = laplacian_mean(coordinates, neighbors)
    roughness_reduction = 1.0 - after_laplacian / before_laplacian
    if roughness_reduction <= 0.0:
        raise RuntimeError("Production vertex relaxation did not reduce roughness")
    return {
        "iterations": iterations,
        "lambda_factor": lambda_factor,
        "mu_factor": mu_factor,
        "movable_vertices": len(movable),
        "boundary_vertices_pinned": len(boundary),
        "moved_vertices": len(moved),
        "mean_displacement": sum(moved) / len(moved),
        "p95_displacement": moved[percentile_index],
        "max_displacement": max(displacements),
        "max_displacement_ratio": max_ratio,
        "boundary_max_displacement": boundary_max,
        "laplacian_mean_before": before_laplacian,
        "laplacian_mean_after": after_laplacian,
        "laplacian_roughness_reduction": roughness_reduction,
        "shape_keys_translated": max(0, len(shape_keys) - 1),
        "max_shape_delta_error": max_shape_delta_error,
    }


def quadify_preserving_boundaries(body: bpy.types.Object, angle_degrees: float) -> None:
    if not 0.0 < angle_degrees <= 90.0:
        raise RuntimeError("--quad-angle must be in (0, 90]")
    if body.data.shape_keys and any(
        abs(key.value) > 1.0e-8 for key in body.data.shape_keys.key_blocks
    ):
        raise RuntimeError("All canonical shape keys must be zero before topology work")

    activate(body)
    body.active_shape_key_index = 0
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.tris_convert_to_quads(
        face_threshold=math.radians(angle_degrees),
        shape_threshold=math.radians(angle_degrees),
        uvs=True,
        vcols=True,
        seam=True,
        sharp=True,
        materials=True,
    )
    bpy.ops.object.mode_set(mode="OBJECT")
    shade_surface_smooth(body)


def add_authoring_subdivision(body: bpy.types.Object, level: int) -> None:
    if level != 1:
        raise RuntimeError("Production contract currently permits subdivision level 1 only")
    old = body.modifiers.get(MODIFIER_NAME)
    if old:
        body.modifiers.remove(old)
    modifier = body.modifiers.new(MODIFIER_NAME, "SUBSURF")
    modifier.subdivision_type = "CATMULL_CLARK"
    modifier.levels = level
    modifier.render_levels = level
    modifier.quality = 3
    modifier.show_only_control_edges = True
    modifier.use_creases = True


def mesh_without_modifiers(obj: bpy.types.Object) -> bpy.types.Mesh:
    """Return evaluated basis geometry without copying the source Key datablock."""
    states = [
        (modifier, modifier.show_viewport, modifier.show_render)
        for modifier in obj.modifiers
    ]
    try:
        for modifier, _, _ in states:
            modifier.show_viewport = False
            modifier.show_render = False
        bpy.context.view_layer.update()
        depsgraph = bpy.context.evaluated_depsgraph_get()
        return bpy.data.meshes.new_from_object(
            obj.evaluated_get(depsgraph),
            preserve_all_data_layers=True,
            depsgraph=depsgraph,
        )
    finally:
        for modifier, show_viewport, show_render in states:
            modifier.show_viewport = show_viewport
            modifier.show_render = show_render
        bpy.context.view_layer.update()


def make_baked_body(
    body: bpy.types.Object, rig: bpy.types.Object, level: int
) -> bpy.types.Object:
    baked = body.copy()
    baked.data = mesh_without_modifiers(body)
    baked.name = f"{BODY_NAME}_JellyBake"
    bpy.context.scene.collection.objects.link(baked)
    baked.animation_data_clear()
    baked.modifiers.clear()

    subdivision = baked.modifiers.new("Jelly_Bake_Subdivision", "SUBSURF")
    subdivision.subdivision_type = "CATMULL_CLARK"
    subdivision.levels = level
    subdivision.render_levels = level
    subdivision.quality = 3
    subdivision.use_creases = True
    bpy.context.view_layer.update()

    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = baked.evaluated_get(depsgraph)
    baked_mesh = bpy.data.meshes.new_from_object(
        evaluated, preserve_all_data_layers=True, depsgraph=depsgraph
    )
    old_mesh = baked.data
    baked.data = baked_mesh
    baked.modifiers.clear()
    if old_mesh.users == 0:
        bpy.data.meshes.remove(old_mesh)

    shade_surface_smooth(baked)
    armature = baked.modifiers.new("Armature", "ARMATURE")
    armature.object = rig
    armature.use_deform_preserve_volume = True
    bpy.context.view_layer.update()
    return baked


def skin_metrics(obj: bpy.types.Object) -> dict[str, object]:
    weighted = 0
    total_assignments = 0
    min_sum = float("inf")
    max_sum = 0.0
    for vertex in obj.data.vertices:
        weight_sum = sum(group.weight for group in vertex.groups)
        total_assignments += len(vertex.groups)
        if weight_sum > 1.0e-8:
            weighted += 1
        min_sum = min(min_sum, weight_sum)
        max_sum = max(max_sum, weight_sum)
    return {
        "vertex_groups": len(obj.vertex_groups),
        "weighted_vertices": weighted,
        "unweighted_vertices": len(obj.data.vertices) - weighted,
        "weight_assignments": total_assignments,
        "min_weight_sum": 0.0 if min_sum == float("inf") else min_sum,
        "max_weight_sum": max_sum,
    }


def export_fbx(
    body: bpy.types.Object,
    authoring_body: bpy.types.Object,
    eyes: bpy.types.Object,
    rig: bpy.types.Object,
    path: Path,
) -> None:
    original_authoring_name = authoring_body.name
    original_baked_name = body.name
    authoring_body.name = f"{BODY_NAME}_Authoring"
    body.name = BODY_NAME
    try:
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
    finally:
        body.name = original_baked_name
        authoring_body.name = original_authoring_name


def create_evidence(
    body: bpy.types.Object,
    baked: bpy.types.Object,
    eyes: bpy.types.Object,
    output_dir: Path,
) -> dict[str, object]:
    output_dir.mkdir(parents=True, exist_ok=True)
    collection = bpy.data.collections.new("LimeAlien_Jelly_Production_Evidence")
    bpy.context.scene.collection.children.link(collection)

    base = body.copy()
    base.data = mesh_without_modifiers(body)
    base.name = "EVIDENCE_Base_Body"
    collection.objects.link(base)
    base.modifiers.clear()
    final = baked.copy()
    final.data = baked.data.copy()
    final.name = "EVIDENCE_Jelly_Body"
    collection.objects.link(final)
    final.modifiers.clear()

    base_eyes = eyes.copy()
    base_eyes.data = eyes.data.copy()
    base_eyes.name = "EVIDENCE_Base_Eyes"
    collection.objects.link(base_eyes)
    final_eyes = eyes.copy()
    final_eyes.data = eyes.data.copy()
    final_eyes.name = "EVIDENCE_Jelly_Eyes"
    collection.objects.link(final_eyes)

    for obj in bpy.context.scene.objects:
        obj.hide_render = True
    for obj in (base, final, base_eyes, final_eyes):
        obj.hide_render = False
        obj.hide_viewport = False
        obj.hide_set(False)
        obj.parent = None
        obj.matrix_parent_inverse.identity()
        obj.animation_data_clear()

    minimum, maximum = world_bounds([base, base_eyes])
    width = maximum.x - minimum.x
    height = maximum.z - minimum.z
    center_x = (minimum.x + maximum.x) * 0.5
    spacing = width * 1.62
    for obj in (base, base_eyes):
        obj.location.x += -spacing * 0.5 - center_x
    for obj in (final, final_eyes):
        obj.location.x += spacing * 0.5 - center_x

    duplicate_materials(base, "BASE", (0.34, 0.58, 0.13, 1.0))
    duplicate_materials(final, "JELLY_PROD", (0.48, 0.88, 0.18, 1.0))
    labels = [
        add_text(
            collection,
            "BASE / 11.4K TRI",
            (-spacing * 0.5, minimum.y - width * 0.18, minimum.z - height * 0.12),
            height * 0.04,
            (0.78, 0.86, 0.92, 1.0),
        ),
        add_text(
            collection,
            "JELLY PROD / 47.7K TRI",
            (spacing * 0.5, minimum.y - width * 0.18, minimum.z - height * 0.12),
            height * 0.04,
            (0.64, 0.96, 0.40, 1.0),
        ),
    ]

    camera_data = bpy.data.cameras.new("EVIDENCE_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("EVIDENCE_Camera", camera_data)
    collection.objects.link(camera)
    camera.hide_render = False
    front = output_dir / "lime-alien-jelly-production-front.png"
    oblique = output_dir / "lime-alien-jelly-production-oblique.png"
    render(
        front,
        camera,
        [base, final],
        [base_eyes, final_eyes],
        labels,
        turntable_degrees=0.0,
        fit_camera=True,
        show_labels=True,
    )
    render(
        oblique,
        camera,
        [base, final],
        [base_eyes, final_eyes],
        labels,
        turntable_degrees=24.0,
        fit_camera=False,
        show_labels=False,
    )

    # A slightly elevated three-quarter view and a real receiver plane make the
    # cast shadow readable.  The comparison views above remain unchanged so
    # silhouette regressions can still be compared with the previous run.
    for obj in (base, base_eyes):
        obj.hide_render = True
    for label in labels:
        label.hide_render = True
    hero_minimum, hero_maximum = world_bounds([final, final_eyes])
    hero_center_x = (hero_minimum.x + hero_maximum.x) * 0.5
    for obj in (final, final_eyes):
        obj.location.x -= hero_center_x
        obj.rotation_euler.z = math.radians(24.0)
    bpy.context.view_layer.update()
    hero_minimum, hero_maximum = world_bounds([final, final_eyes])
    hero_width = hero_maximum.x - hero_minimum.x
    hero_depth = hero_maximum.y - hero_minimum.y
    hero_height = hero_maximum.z - hero_minimum.z
    hero_center_y = (hero_minimum.y + hero_maximum.y) * 0.5
    floor_z = hero_minimum.z - hero_height * 0.008
    receiver_mesh = bpy.data.meshes.new("EVIDENCE_ShadowReceiver_Mesh")
    receiver_mesh.from_pydata(
        [
            (-hero_width * 1.45, hero_center_y - hero_depth * 1.8, floor_z),
            (hero_width * 1.45, hero_center_y - hero_depth * 1.8, floor_z),
            (hero_width * 1.45, hero_center_y + hero_depth * 3.2, floor_z),
            (-hero_width * 1.45, hero_center_y + hero_depth * 3.2, floor_z),
        ],
        [],
        [(0, 1, 2, 3)],
    )
    receiver_mesh.update()
    receiver = bpy.data.objects.new("EVIDENCE_ShadowReceiver", receiver_mesh)
    collection.objects.link(receiver)
    receiver.hide_render = False
    receiver.hide_viewport = False
    receiver.hide_set(False)
    receiver_material = bpy.data.materials.new("EVIDENCE_ShadowReceiver_Material")
    receiver_material.diffuse_color = (0.055, 0.072, 0.09, 1.0)
    receiver.data.materials.append(receiver_material)

    center = (hero_minimum + hero_maximum) * 0.5
    camera.data.ortho_scale = hero_height * 1.90
    camera.location = Vector(
        (center.x, hero_minimum.y - max(hero_width * 2.4, 8.0), center.z + hero_height * 0.28)
    )
    target = Vector((center.x, center.y, center.z))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    shadow = output_dir / "lime-alien-jelly-production-shadow.png"
    render(
        shadow,
        camera,
        [final],
        [final_eyes],
        labels,
        turntable_degrees=24.0,
        fit_camera=False,
        show_labels=False,
    )
    if not shadow.exists() or shadow.stat().st_size < 10_000:
        raise RuntimeError("Shadow evidence render was not written correctly")

    for obj in tuple(collection.objects):
        data = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        if data and data.users == 0:
            if isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
            elif isinstance(data, bpy.types.Curve):
                bpy.data.curves.remove(data)
            elif isinstance(data, bpy.types.Camera):
                bpy.data.cameras.remove(data)
    bpy.data.collections.remove(collection)
    body.hide_render = False
    eyes.hide_render = False
    return {
        "renders": [front.name, oblique.name, shadow.name],
        "shadow_render": {
            "engine": "BLENDER_WORKBENCH",
            "receiver": "EVIDENCE_ShadowReceiver",
            "receiver_visible": True,
            "show_shadows": True,
            "camera_elevation_ratio": 0.28,
            "bytes": shadow.stat().st_size,
        },
    }


def create_joint_pose_evidence(
    baked: bpy.types.Object,
    eyes: bpy.types.Object,
    rig: bpy.types.Object,
    output_dir: Path,
) -> dict[str, object]:
    """Render one asymmetric stress pose to expose elbow and knee pinching."""
    collection = bpy.data.collections.new("LimeAlien_Jelly_JointPose_Evidence")
    bpy.context.scene.collection.children.link(collection)
    pose_body = baked.copy()
    pose_body.data = baked.data.copy()
    pose_body.name = "EVIDENCE_Jelly_JointPose_Body"
    collection.objects.link(pose_body)
    pose_eyes = eyes.copy()
    pose_eyes.data = eyes.data.copy()
    pose_eyes.name = "EVIDENCE_Jelly_JointPose_Eyes"
    collection.objects.link(pose_eyes)
    duplicate_materials(pose_body, "JELLY_JOINT_POSE", (0.48, 0.88, 0.18, 1.0))

    for obj in bpy.context.scene.objects:
        obj.hide_render = True
    for obj in (pose_body, pose_eyes):
        obj.hide_render = False
        obj.hide_viewport = False
        obj.hide_set(False)

    camera_data = bpy.data.cameras.new("EVIDENCE_JointPose_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("EVIDENCE_JointPose_Camera", camera_data)
    collection.objects.link(camera)
    camera.hide_render = False

    controls = {
        "thigh_fk.L": (Vector((0.0, 0.0, 1.0)), -34.0),
        "shin_fk.L": (Vector((0.0, 0.0, 1.0)), 68.0),
        "upper_arm_fk.R": (Vector((0.0, 0.0, 1.0)), 28.0),
        "forearm_fk.R": (Vector((0.0, 0.0, 1.0)), -76.0),
    }
    missing = [name for name in controls if name not in rig.pose.bones]
    if missing:
        raise RuntimeError(f"Missing joint-pose controls: {missing}")
    old_pose_position = rig.data.pose_position
    old_rotations = {
        name: (rig.pose.bones[name].rotation_mode, rig.pose.bones[name].rotation_quaternion.copy())
        for name in controls
    }
    parent_controls = ("thigh_parent.L", "upper_arm_parent.R")
    old_ik_fk = {name: rig.pose.bones[name].get("IK_FK", 1.0) for name in parent_controls}
    try:
        rig.data.pose_position = "POSE"
        for name in parent_controls:
            rig.pose.bones[name]["IK_FK"] = 1.0
        for name, (axis, angle) in controls.items():
            control = rig.pose.bones[name]
            control.rotation_mode = "QUATERNION"
            control.rotation_quaternion = Quaternion(axis, math.radians(angle))
        bpy.context.view_layer.update()

        bpy.context.view_layer.update()
        minimum, maximum = world_bounds([pose_body, pose_eyes])
        set_camera_to_bounds(camera, minimum, maximum, horizontal_padding=1.62, vertical_padding=1.90)
        output = output_dir / "lime-alien-jelly-production-joint-pose.png"
        render(
            output,
            camera,
            [pose_body],
            [pose_eyes],
            [],
            turntable_degrees=0.0,
            fit_camera=False,
            show_labels=False,
        )
        if not output.exists() or output.stat().st_size < 10_000:
            raise RuntimeError("Joint-pose evidence render was not written correctly")
        return {
            "path": output.name,
            "bytes": output.stat().st_size,
            "controls_degrees": {name: angle for name, (_, angle) in controls.items()},
            "ik_fk": "FK",
        }
    finally:
        for name, (mode, rotation) in old_rotations.items():
            control = rig.pose.bones[name]
            control.rotation_mode = mode
            control.rotation_quaternion = rotation
        for name, value in old_ik_fk.items():
            rig.pose.bones[name]["IK_FK"] = value
        rig.data.pose_position = old_pose_position
        bpy.context.view_layer.update()
        for obj in tuple(collection.objects):
            data = obj.data
            bpy.data.objects.remove(obj, do_unlink=True)
            if data and data.users == 0:
                if isinstance(data, bpy.types.Mesh):
                    bpy.data.meshes.remove(data)
                elif isinstance(data, bpy.types.Camera):
                    bpy.data.cameras.remove(data)
        bpy.data.collections.remove(collection)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    args = parse_args()
    body, eyes, rig = require_scene()
    source_path = Path(bpy.data.filepath).resolve()
    try:
        source_label = source_path.relative_to(Path.cwd().resolve()).as_posix()
    except ValueError:
        source_label = str(source_path)
    source_digest = sha256(source_path)
    shape_key_names = (
        [key.name for key in body.data.shape_keys.key_blocks]
        if body.data.shape_keys
        else []
    )
    source_coordinate_digest = coordinate_digest(body)
    before = face_metrics(body.data)
    shade_surface_smooth(eyes)
    require_fully_smooth(eyes, "Eyes")

    relaxation = apply_shape_preserving_relax(
        body,
        iterations=args.relax_iterations,
        lambda_factor=args.relax_lambda,
        mu_factor=args.relax_mu,
        min_displacement_ratio=args.min_displacement_ratio,
        max_displacement_ratio=args.max_displacement_ratio,
    )
    relaxed_coordinate_digest = coordinate_digest(body)
    if relaxed_coordinate_digest == source_coordinate_digest:
        raise RuntimeError("Production relaxation did not change authoring coordinates")

    quadify_preserving_boundaries(body, args.quad_angle)
    after_quadify = face_metrics(body.data)
    require_fully_smooth(body, "Quadified authoring body")
    if after_quadify["vertices"] != before["vertices"]:
        raise RuntimeError("Quad conversion changed canonical vertex count")
    if coordinate_digest(body) != relaxed_coordinate_digest:
        raise RuntimeError("Quad conversion changed relaxed or shape-key coordinates")
    if body.data.shape_keys and [
        key.name for key in body.data.shape_keys.key_blocks
    ] != shape_key_names:
        raise RuntimeError("Quad conversion changed canonical shape-key inventory")

    add_authoring_subdivision(body, args.subdivision_level)
    baked = make_baked_body(body, rig, args.subdivision_level)
    baked_metrics = face_metrics(baked.data)
    require_fully_smooth(baked, "Runtime baked body")
    baked_skin = skin_metrics(baked)
    if baked_skin["unweighted_vertices"]:
        raise RuntimeError("Baked jelly mesh contains unweighted vertices")

    evidence = create_evidence(body, baked, eyes, args.evidence_dir.resolve())
    joint_pose = create_joint_pose_evidence(
        baked, eyes, rig, args.evidence_dir.resolve()
    )
    fbx_outputs: list[dict[str, object]] = []
    for output in args.fbx_output:
        export_fbx(baked, body, eyes, rig, output)
        resolved = output.resolve()
        fbx_outputs.append(
            {
                "path": output.as_posix(),
                "bytes": resolved.stat().st_size,
                "sha256": sha256(resolved),
            }
        )

    bpy.data.objects.remove(baked, do_unlink=True)
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(output), check_existing=False, compress=True)

    if sha256(source_path) != source_digest:
        raise RuntimeError("Canonical source file changed during production application")
    report = {
        "source": source_label,
        "source_sha256": source_digest,
        "source_unchanged": True,
        "output": args.output.as_posix(),
        "quad_angle_degrees": args.quad_angle,
        "subdivision_level": args.subdivision_level,
        "vertex_relaxation": relaxation,
        "authoring_coordinate_digest": relaxed_coordinate_digest,
        "shape_keys_preserved_in_blend": len(shape_key_names),
        "authoring_modifier": MODIFIER_NAME,
        "before": before,
        "after_quadify": after_quadify,
        "runtime_baked": baked_metrics,
        "runtime_skin": baked_skin,
        "fbx_outputs": fbx_outputs,
        "surface_smoothing": {
            "authoring_body_all_smooth": after_quadify["flat_polygons"] == 0,
            "runtime_body_all_smooth": baked_metrics["flat_polygons"] == 0,
            "eyes_all_smooth": all(polygon.use_smooth for polygon in eyes.data.polygons),
        },
        "renders": evidence["renders"],
        "shadow_render": evidence["shadow_render"],
        "joint_pose_render": joint_pose,
        "regression_checks": {
            "source_unchanged": True,
            "canonical_vertex_count_preserved": after_quadify["vertices"] == before["vertices"],
            "shape_keys_preserved": len(shape_key_names),
            "shape_key_relative_deltas_preserved": relaxation["max_shape_delta_error"] <= 1.0e-6,
            "open_boundaries_pinned": relaxation["boundary_max_displacement"] < 1.0e-8,
            "surface_roughness_reduced": relaxation["laplacian_roughness_reduction"] > 0.0,
            "runtime_all_vertices_weighted": baked_skin["unweighted_vertices"] == 0,
            "runtime_interior_nonmanifold_edges": baked_metrics["interior_nonmanifold_edges"],
            "runtime_loose_edges": baked_metrics["loose_edges"],
        },
        "tradeoff": (
            "Bounded control-vertex relaxation changes the rest surface while preserving "
            f"all relative expression deltas. Level 1 raises body triangles from "
            f"{before['triangles']:,} to {baked_metrics['triangles']:,} "
            f"({baked_metrics['triangles'] / before['triangles']:.2f}x); level 2 is "
            "intentionally excluded from production."
        ),
    }
    report_path = args.evidence_dir.resolve() / "report.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ADK_JELLY_PRODUCTION_REPORT=" + json.dumps(report, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
