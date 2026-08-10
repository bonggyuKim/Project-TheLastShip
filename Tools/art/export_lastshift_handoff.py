import bpy
from pathlib import Path

source = Path(r"D:/Assets/LastShift_LowPolyKit.blend")
output = Path(r"Assets/DoodleUp/Art/Props/LastShiftReal")
names = ["LP_AirlockDoor", "LP_VentFan", "LP_EmergencyBeacon"]

bpy.ops.wm.open_mainfile(filepath=str(source))
output.mkdir(parents=True, exist_ok=True)
for name in names:
    root = bpy.data.objects.get(name)
    if root is None:
        raise RuntimeError(name)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    for obj in root.children_recursive:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=str(output / f"{name}.fbx"),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        add_leaf_bones=False,
        apply_unit_scale=True,
        path_mode="COPY",
        embed_textures=True,
        axis_forward="-Z",
        axis_up="Y",
    )
