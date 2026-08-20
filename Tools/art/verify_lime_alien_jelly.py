"""Regression checks for the production LAST SHIFT lime alien jelly asset.

Run this against ``LastShiftLimeAlien_UnityExport_Jelly.blend`` after the
production application script.  It validates the authoring mesh, both runtime
FBXs, and the cast-shadow evidence image, then writes a compact JSON receipt.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
import bmesh


BODY_NAME = "LastShift_LimeAlien_Body"
EYES_NAME = "LastShift_LimeAlien_Eyes"
RIG_NAME = "LastShift_LimeAlien_Rig"
MODIFIER_NAME = "Jelly_Surface_Subdivision_L1"


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--production-report", type=Path, required=True)
    parser.add_argument("--fbx", type=Path, action="append", default=[])
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args(raw)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


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


def topology_metrics(obj: bpy.types.Object) -> dict[str, int]:
    mesh = obj.data
    mesh.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    metrics = {
        "vertices": len(mesh.vertices),
        "polygons": len(mesh.polygons),
        "triangles": len(mesh.loop_triangles),
        "smooth_polygons": sum(polygon.use_smooth for polygon in mesh.polygons),
        "flat_polygons": sum(not polygon.use_smooth for polygon in mesh.polygons),
        "boundary_edges": sum(edge.is_boundary for edge in bm.edges),
        "interior_nonmanifold_edges": sum(
            not edge.is_manifold and not edge.is_boundary for edge in bm.edges
        ),
        "loose_edges": sum(not edge.link_faces for edge in bm.edges),
    }
    bm.free()
    return metrics


def verify_shadow_image(path: Path) -> dict[str, object]:
    require(path.exists(), f"Missing shadow evidence: {path}")
    try:
        path_label = path.resolve().relative_to(Path.cwd().resolve()).as_posix()
    except ValueError:
        path_label = path.as_posix()
    image = bpy.data.images.load(str(path.resolve()), check_existing=False)
    try:
        width, height = image.size
        require(width >= 1200 and height >= 800, "Shadow evidence resolution regressed")
        pixels = image.pixels[:]
        luminance: list[float] = []
        # Sample the lower half where the feet, receiver plane, and cast shadow meet.
        for y in range(height // 2, height, 24):
            for x in range(0, width, 24):
                index = (y * width + x) * 4
                r, g, b = pixels[index : index + 3]
                luminance.append(0.2126 * r + 0.7152 * g + 0.0722 * b)
        spread = max(luminance) - min(luminance)
        require(spread > 0.08, "Shadow receiver region has insufficient value separation")
        return {
            "path": path_label,
            "bytes": path.stat().st_size,
            "width": width,
            "height": height,
            "lower_half_luminance_spread": spread,
        }
    finally:
        bpy.data.images.remove(image)


def verify_fbx(
    path: Path,
    expected: dict[str, object],
    expected_runtime: dict[str, object],
) -> dict[str, object]:
    resolved = path.resolve()
    require(resolved.exists(), f"Missing runtime FBX: {path}")
    require(sha256(resolved) == expected["sha256"], f"FBX digest mismatch: {path}")
    existing = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(resolved), use_anim=False)
    imported = [obj for obj in bpy.data.objects if obj not in existing]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    armatures = [obj for obj in imported if obj.type == "ARMATURE"]
    require(meshes, f"FBX has no meshes: {path}")
    require(armatures, f"FBX has no armature: {path}")
    body = max(meshes, key=lambda obj: len(obj.data.vertices))
    metrics = topology_metrics(body)
    require(
        metrics["vertices"] == expected_runtime["vertices"],
        f"Runtime vertex count differs from production report: {path}",
    )
    require(
        metrics["triangles"] == expected_runtime["triangles"],
        f"Runtime triangle count differs from production report: {path}",
    )
    require(metrics["flat_polygons"] == 0, f"Runtime FBX lost smooth shading: {path}")
    require(metrics["interior_nonmanifold_edges"] == 0, f"Runtime FBX is non-manifold: {path}")
    weighted_vertices = sum(bool(vertex.groups) for vertex in body.data.vertices)
    require(weighted_vertices == len(body.data.vertices), f"Runtime FBX has unweighted vertices: {path}")
    require(len(body.vertex_groups) == 45, f"Runtime FBX skin-group count regressed: {path}")
    result = {
        "path": path.as_posix(),
        "sha256": expected["sha256"],
        "body": metrics,
        "vertex_groups": len(body.vertex_groups),
        "weighted_vertices": weighted_vertices,
        "armature_bones": len(armatures[0].data.bones),
    }
    for obj in imported:
        bpy.data.objects.remove(obj, do_unlink=True)
    return result


def main() -> None:
    args = parse_args()
    report_path = args.production_report.resolve()
    report = json.loads(report_path.read_text(encoding="utf-8"))
    body = bpy.data.objects.get(BODY_NAME)
    eyes = bpy.data.objects.get(EYES_NAME)
    rig = bpy.data.objects.get(RIG_NAME)
    require(body is not None and body.type == "MESH", "Missing production body")
    require(eyes is not None and eyes.type == "MESH", "Missing production eyes")
    require(rig is not None and rig.type == "ARMATURE", "Missing production rig")
    require(rig.data.pose_position == "REST", "Production rig is not in REST pose")

    authoring = topology_metrics(body)
    require(authoring["vertices"] == 5711, "Authoring vertex count regressed")
    require(authoring["triangles"] == 11382, "Authoring triangle count regressed")
    require(authoring["flat_polygons"] == 0, "Authoring body lost smooth shading")
    require(authoring["interior_nonmanifold_edges"] == 0, "Authoring body is non-manifold")
    modifier = body.modifiers.get(MODIFIER_NAME)
    require(modifier is not None and modifier.type == "SUBSURF", "Missing jelly subdivision")
    require(modifier.levels == 1 and modifier.render_levels == 1, "Jelly subdivision level regressed")
    shape_keys = len(body.data.shape_keys.key_blocks) if body.data.shape_keys else 0
    require(shape_keys == 92, "Shape-key inventory regressed")
    require(all(polygon.use_smooth for polygon in eyes.data.polygons), "Eyes lost smooth shading")
    require(report["source_unchanged"], "Production report says canonical source changed")
    require(report["runtime_skin"]["unweighted_vertices"] == 0, "Runtime has unweighted vertices")
    relaxation = report.get("vertex_relaxation")
    require(relaxation is not None, "Production report has no integrated vertex relaxation")
    require(relaxation["moved_vertices"] > 0, "Production vertex relaxation moved no vertices")
    local_cleanup = report.get("local_topology_cleanup")
    require(local_cleanup is not None, "Production report has no local topology cleanup")
    require(
        local_cleanup["converted_triangle_pairs"] > 0,
        "Local topology cleanup converted no triangle pairs",
    )
    mouth_circle = report.get("mouth_circle_post_relaxation")
    require(mouth_circle is not None and mouth_circle.get("applied"), "Circular mouth was not restored after relaxation")
    require(
        abs(mouth_circle["after"]["width_to_height"] - 1.0) <= 0.03
        and mouth_circle["after"]["radius_cv"] <= 0.03,
        "Final authoring inner mouth ring is not circular",
    )
    mouth_entrance = mouth_circle.get("entrance")
    require(
        mouth_entrance is not None
        and abs(mouth_entrance["after"]["width_to_height"] - 1.08) <= 0.015,
        "Final visible mouth entrance is not the intended subtle oval",
    )
    mouth_transition = mouth_circle.get("face_transition")
    require(
        mouth_transition is not None
        and mouth_transition["laplacian_roughness_reduction"] >= 0.05
        and mouth_transition["max_displacement"] <= 0.0018 + 1.0e-8
        and mouth_transition["ring_influence"] == [0.75, 0.50, 0.25, 0.0],
        "Final mouth-to-face transition relaxation regressed",
    )
    mouth_cleanup = report.get("mouth_topology_cleanup")
    require(
        mouth_cleanup is not None and mouth_cleanup["converted_triangle_pairs"] > 0,
        "Mouth topology cleanup converted no triangle pairs",
    )
    require(
        0.0195 <= relaxation["max_displacement_ratio"] <= 0.025,
        "Production vertex relaxation exceeded its silhouette guardrail",
    )
    require(
        relaxation["boundary_max_displacement"] < 1.0e-8,
        "Open mesh boundary moved during production relaxation",
    )
    require(
        relaxation["laplacian_roughness_reduction"] > 0.40,
        "Production surface roughness reduction regressed",
    )
    require(
        relaxation["shape_keys_translated"] == 91
        and relaxation["max_shape_delta_error"] <= 1.0e-6,
        "Relative shape-key deltas changed during production relaxation",
    )
    require(
        coordinate_digest(body) == report["authoring_coordinate_digest"],
        "Production authoring coordinates differ from the report",
    )

    shadow_name = report["renders"][2]
    shadow = verify_shadow_image(report_path.parent / shadow_name)
    joint_pose = report.get("joint_pose_render")
    require(joint_pose is not None, "Production report has no joint-pose evidence")
    joint_pose_path = report_path.parent / joint_pose["path"]
    require(
        joint_pose_path.exists() and joint_pose_path.stat().st_size == joint_pose["bytes"],
        "Joint-pose evidence is missing or changed",
    )
    expected_fbxs = {Path(item["path"]).name: item for item in report["fbx_outputs"]}
    runtime = []
    for path in args.fbx:
        expected = expected_fbxs.get(path.name)
        require(expected is not None, f"FBX is absent from production report: {path}")
        runtime.append(verify_fbx(path, expected, report["runtime_baked"]))
    require(len(runtime) == 2, "Both runtime FBXs must be regression-tested")

    receipt = {
        "passed": True,
        "production_blend": report["output"],
        "authoring_body": authoring,
        "shape_keys": shape_keys,
        "subdivision": {"name": modifier.name, "levels": modifier.levels},
        "vertex_relaxation": relaxation,
        "mouth_circle_post_relaxation": mouth_circle,
        "mouth_topology_cleanup": mouth_cleanup,
        "eyes_all_smooth": True,
        "shadow_evidence": shadow,
        "joint_pose_evidence": joint_pose,
        "runtime_fbxs": runtime,
        "checks": [
            "authoring topology and smooth shading",
            "92 shape keys and REST rig",
            "bounded vertex relaxation and preserved relative shape-key deltas",
            "1:1 inner ring, 1.08:1 subtle oval entrance, and four-ring face transition falloff",
            "Catmull-Clark L1 contract",
            "cast-shadow evidence value separation",
            "asymmetric FK elbow and knee stress-pose evidence",
            "both FBX digests, topology, smooth shading, skin groups, and armature",
        ],
    }
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8")
    print("ADK_JELLY_REGRESSION_REPORT=" + json.dumps(receipt, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
