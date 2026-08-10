import bpy
import math
import sys
from pathlib import Path
from mathutils import Vector


SPECS = {
    "control panel 3d model.glb": ("LSReal_ControlPanel", (1.35, 1.10, 0.55)),
    "futuristic crate 3d model.glb": ("LSReal_CargoCrate", (0.95, 0.75, 0.80)),
    "oxygen+tank+3d+model.glb": ("LSReal_OxygenTank", (0.55, 1.35, 0.55)),
    "portable+battery+3d+model.glb": ("LSReal_PortableBattery", (0.55, 0.75, 0.40)),
    "toolbox+3d+model.glb": ("LSReal_Toolbox", (0.75, 0.42, 0.42)),
    "stylized+lamp+3d+model.glb": ("LSReal_WorkLamp", (0.42, 0.90, 0.42)),
}


def bounds(objects):
    corners = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    low = Vector((min(v.x for v in corners), min(v.y for v in corners), min(v.z for v in corners)))
    high = Vector((max(v.x for v in corners), max(v.y for v in corners), max(v.z for v in corners)))
    return low, high


def convert(source_root: Path, output_root: Path, source_name: str, asset_name: str, target):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source_root / source_name))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"no mesh in {source_name}")

    low, high = bounds(meshes)
    size = high - low
    scale = min(target[i] / size[i] for i in range(3) if size[i] > 1e-5)
    center = (low + high) * 0.5
    for obj in bpy.context.scene.objects:
        obj.location = (obj.location - center) * scale
        obj.scale *= scale
    low, _ = bounds(meshes)
    for obj in bpy.context.scene.objects:
        obj.location.z -= low.z

    root = bpy.data.objects.new(asset_name, None)
    bpy.context.collection.objects.link(root)
    for obj in list(bpy.context.scene.objects):
        if obj != root and obj.parent is None:
            obj.parent = root

    bpy.context.view_layer.objects.active = root
    root.select_set(True)
    output_root.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output_root / f"{asset_name}.blend"))
    bpy.ops.export_scene.fbx(
        filepath=str(output_root / f"{asset_name}.fbx"),
        use_selection=False,
        apply_unit_scale=True,
        bake_space_transform=False,
        object_types={"EMPTY", "MESH"},
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=True,
        axis_forward="-Z",
        axis_up="Y",
    )
    final_low, final_high = bounds(meshes)
    print(f"[LS_REAL_PROP] {asset_name} size={tuple(round(v, 3) for v in final_high-final_low)}")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:]
    source_root, output_root = map(Path, args)
    for source_name, (asset_name, target) in SPECS.items():
        convert(source_root, output_root, source_name, asset_name, target)
