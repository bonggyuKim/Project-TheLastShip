"""Diagnose the LAST SHIFT lime alien's hip and upper-mouth skin boundaries.

The two visible defects are easy to misclassify as weight-paint seams.  This
tool measures deform-weight continuity in the exact local regions without
changing the blend file.  Run it against both the canonical cage and the
production jelly blend::

    blender -b <blend> -P diagnose_last_shift_hip_mouth_boundaries.py -- \
        --report <report.json>

The report intentionally separates skin evidence from rest-surface evidence.
The latter still requires a viewport/render check because a rest-pose wrinkle
cannot be inferred from weights alone.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy


BODY_NAME = "LastShift_LimeAlien_Body"
RIG_NAME = "LastShift_LimeAlien_Rig"
HIP_ROOT = "DEF-thigh.L"
MOUTH_GROUP = "DEF-head.soft.eye"

# The mouth target is the same stable local landmark used by the isolated
# mouth-face repair.  The box covers the upper lip, not the eye opening above.
MOUTH_CENTER = (-0.014025, -0.211263, 0.577113)
MOUTH_HALF_EXTENT = (0.075, 0.055, 0.045)


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--report", type=Path, required=True)
    return parser.parse_args(raw)


def require_scene() -> tuple[bpy.types.Object, bpy.types.Object]:
    body = bpy.data.objects.get(BODY_NAME)
    rig = bpy.data.objects.get(RIG_NAME)
    if body is None or body.type != "MESH":
        raise RuntimeError(f"Missing body: {BODY_NAME}")
    if rig is None or rig.type != "ARMATURE":
        raise RuntimeError(f"Missing rig: {RIG_NAME}")
    armatures = [modifier.object for modifier in body.modifiers if modifier.type == "ARMATURE"]
    if armatures != [rig]:
        raise RuntimeError(f"Body armature mismatch: {armatures}")
    return body, rig


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def deform_group_indices(
    body: bpy.types.Object, rig: bpy.types.Object
) -> dict[int, str]:
    deform_names = {bone.name for bone in rig.data.bones if bone.use_deform}
    return {
        group.index: group.name
        for group in body.vertex_groups
        if group.name in deform_names
    }


def descendants(rig: bpy.types.Object, root_name: str) -> set[str]:
    root = rig.data.bones.get(root_name)
    if root is None:
        raise RuntimeError(f"Missing deform root: {root_name}")
    return {
        bone.name
        for bone in rig.data.bones
        if bone.use_deform and (bone == root or root in bone.parent_recursive)
    }


def vertex_deform_weights(
    vertex: bpy.types.MeshVertex, group_names: dict[int, str]
) -> dict[str, float]:
    return {
        group_names[item.group]: float(item.weight)
        for item in vertex.groups
        if item.group in group_names and item.weight > 1.0e-8
    }


def box_vertices(
    body: bpy.types.Object,
    minimum: tuple[float, float, float],
    maximum: tuple[float, float, float],
) -> list[int]:
    return [
        vertex.index
        for vertex in body.data.vertices
        if all(minimum[axis] <= vertex.co[axis] <= maximum[axis] for axis in range(3))
    ]


def region_metrics(
    body: bpy.types.Object,
    group_names: dict[int, str],
    indices: list[int],
    moving_names: set[str],
) -> dict[str, object]:
    if not indices:
        raise RuntimeError("Diagnostic region contains no vertices")
    selected = set(indices)
    weights = {
        index: vertex_deform_weights(body.data.vertices[index], group_names)
        for index in indices
    }
    deform_totals = {index: sum(values.values()) for index, values in weights.items()}
    moving_fraction = {
        index: (
            sum(value for name, value in weights[index].items() if name in moving_names)
            / deform_totals[index]
            if deform_totals[index] > 1.0e-8
            else 0.0
        )
        for index in indices
    }
    region_edges = [
        tuple(map(int, edge.vertices))
        for edge in body.data.edges
        if int(edge.vertices[0]) in selected and int(edge.vertices[1]) in selected
    ]
    gradients = [abs(moving_fraction[a] - moving_fraction[b]) for a, b in region_edges]
    aggregate: dict[str, float] = {}
    for values in weights.values():
        for name, value in values.items():
            aggregate[name] = aggregate.get(name, 0.0) + value
    dominant = [
        max(values.items(), key=lambda item: item[1])[0] if values else None
        for values in weights.values()
    ]
    dominant_counts = {
        name: dominant.count(name)
        for name in sorted({name for name in dominant if name is not None})
    }
    return {
        "vertices": len(indices),
        "internal_edges": len(region_edges),
        "moving_groups": sorted(moving_names),
        "moving_fraction": {
            "min": min(moving_fraction.values()),
            "max": max(moving_fraction.values()),
            "mean": sum(moving_fraction.values()) / len(moving_fraction),
        },
        "edge_gradient": {
            "max": max(gradients, default=0.0),
            "mean": sum(gradients) / len(gradients) if gradients else 0.0,
            "edges_over_0_25": sum(value >= 0.25 for value in gradients),
            "edges_over_0_50": sum(value >= 0.50 for value in gradients),
        },
        "dominant_group_counts": dominant_counts,
        "aggregate_deform_weights": dict(
            sorted(aggregate.items(), key=lambda item: item[1], reverse=True)
        ),
        "all_vertices_weighted": all(value > 1.0e-8 for value in deform_totals.values()),
    }


def main() -> None:
    args = parse_args()
    body, rig = require_scene()
    source = Path(bpy.data.filepath).resolve()
    try:
        source_label = source.relative_to(Path.cwd().resolve()).as_posix()
    except ValueError:
        source_label = str(source)
    groups = deform_group_indices(body, rig)

    hip = rig.data.bones.get(HIP_ROOT)
    if hip is None:
        raise RuntimeError(f"Missing hip bone: {HIP_ROOT}")
    joint = hip.head_local
    hip_min = (joint.x - 0.11, joint.y - 0.13, joint.z - 0.08)
    hip_max = (joint.x + 0.055, joint.y + 0.13, joint.z + 0.105)
    mouth_min = tuple(
        MOUTH_CENTER[axis] - MOUTH_HALF_EXTENT[axis] for axis in range(3)
    )
    mouth_max = tuple(
        MOUTH_CENTER[axis] + MOUTH_HALF_EXTENT[axis] for axis in range(3)
    )

    original_pose = rig.data.pose_position
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    try:
        report = {
            "blend": source_label,
            "sha256": sha256(source),
            "body": BODY_NAME,
            "rig": RIG_NAME,
            "pose_position_during_measurement": rig.data.pose_position,
            "regions": {
                "left_hip_groin": {
                    "bounds": [hip_min, hip_max],
                    "metrics": region_metrics(
                        body,
                        groups,
                        box_vertices(body, hip_min, hip_max),
                        descendants(rig, HIP_ROOT),
                    ),
                },
                "upper_mouth": {
                    "bounds": [mouth_min, mouth_max],
                    "metrics": region_metrics(
                        body,
                        groups,
                        box_vertices(body, mouth_min, mouth_max),
                        {MOUTH_GROUP},
                    ),
                },
            },
            "interpretation_contract": {
                "weight_boundary": "edge_gradient measures only deform-weight discontinuity",
                "rest_surface": "a visible REST-pose dent or wrinkle is geometry/topology evidence, not skin-weight evidence",
                "manual_evidence_required": True,
            },
        }
    finally:
        rig.data.pose_position = original_pose
        bpy.context.view_layer.update()

    args.report.resolve().parent.mkdir(parents=True, exist_ok=True)
    args.report.resolve().write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print("ADK_HIP_MOUTH_DIAGNOSIS=" + json.dumps(report, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
