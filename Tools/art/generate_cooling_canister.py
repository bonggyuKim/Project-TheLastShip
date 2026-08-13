import bpy
import math
import sys
from pathlib import Path


def bevel(obj, width=0.015, segments=2):
    modifier = obj.modifiers.new("EdgeSoftening", "BEVEL")
    modifier.width = width
    modifier.segments = segments


def material(name, color, metallic=0.0, roughness=0.45):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.metallic = metallic
    mat.roughness = roughness
    return mat


def cube(name, location, scale, mat, bevel_width=0.012):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bevel(obj, bevel_width)
    obj.data.materials.append(mat)
    return obj


def cylinder(name, location, radius, depth, mat, vertices=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location)
    obj = bpy.context.object
    obj.name = name
    bevel(obj, 0.012)
    obj.data.materials.append(mat)
    return obj


def build(output_root: Path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    shell = material("Canister_Shell", (0.12, 0.19, 0.22), metallic=0.7, roughness=0.28)
    coolant = material("Canister_Coolant", (0.05, 0.72, 0.82), metallic=0.1, roughness=0.2)
    rubber = material("Canister_Grip", (0.025, 0.035, 0.04), roughness=0.82)
    warning = material("Canister_Warning", (0.95, 0.52, 0.06), metallic=0.05, roughness=0.4)

    root = bpy.data.objects.new("LPK_CoolingCanister", None)
    scene.collection.objects.link(root)
    parts = []
    parts.append(cylinder("PressureVessel", (0, 0, 0.46), 0.19, 0.72, shell, 32))
    parts.append(cylinder("CoolantBand", (0, 0, 0.48), 0.198, 0.19, coolant, 32))
    parts.append(cylinder("FootRing", (0, 0, 0.105), 0.22, 0.07, rubber, 32))
    parts.append(cylinder("ShoulderRing", (0, 0, 0.825), 0.215, 0.075, rubber, 32))
    parts.append(cylinder("Valve", (0, 0, 0.91), 0.07, 0.10, warning, 20))
    parts.append(cube("HandleTop", (0, 0, 1.035), (0.17, 0.035, 0.035), shell))
    parts.append(cube("HandleLeft", (-0.145, 0, 0.965), (0.025, 0.035, 0.08), shell))
    parts.append(cube("HandleRight", (0.145, 0, 0.965), (0.025, 0.035, 0.08), shell))
    parts.append(cube("ReadabilityStripe", (0, -0.193, 0.61), (0.115, 0.012, 0.035), warning, 0.006))
    for side in (-1, 1):
        parts.append(cube(f"Guard_{side:+d}", (side * 0.205, 0, 0.47), (0.018, 0.13, 0.31), shell))
    for obj in parts:
        obj.parent = root

    output_root.mkdir(parents=True, exist_ok=True)
    blend_path = output_root / "LPK_CoolingCanister.blend"
    fbx_path = output_root / "LPK_CoolingCanister.fbx"
    if blend_path.exists():
        blend_path.unlink()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.export_scene.fbx(filepath=str(fbx_path), use_selection=False, object_types={"EMPTY", "MESH"},
        apply_unit_scale=True, bake_space_transform=False, axis_forward="-Z", axis_up="Y",
        use_mesh_modifiers=True, add_leaf_bones=False, path_mode="AUTO")

    # Neutral product render used for silhouette and value separation review.
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    world = bpy.data.worlds.new("ReviewWorld")
    scene.world = world
    world.color = (0.018, 0.025, 0.035)
    bpy.ops.object.camera_add(location=(1.65, -2.35, 1.35))
    camera = bpy.context.object
    scene.camera = camera
    direction = mathutils.Vector((0, 0, 0.52)) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.ops.object.light_add(type="AREA", location=(-1.2, -1.4, 2.2))
    bpy.context.object.data.energy = 700
    bpy.context.object.data.shape = "DISK"
    bpy.context.object.data.size = 2.0
    bpy.ops.object.light_add(type="AREA", location=(1.4, 0.6, 1.3))
    bpy.context.object.data.energy = 450
    bpy.context.object.data.color = (0.15, 0.65, 1.0)
    bpy.context.object.data.size = 1.2
    scene.render.filepath = str(output_root / "LPK_CoolingCanister_review.png")
    bpy.ops.render.render(write_still=True)
    print(f"[COOLING_CANISTER] blend={blend_path} fbx={fbx_path} size=0.44x0.44x1.07m")


if __name__ == "__main__":
    import mathutils
    args = sys.argv[sys.argv.index("--") + 1:]
    build(Path(args[0]))
