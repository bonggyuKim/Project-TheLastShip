import bpy
import math
import mathutils
import sys
from pathlib import Path


ASSETS = {
    "LPK_Power_BusPanel": (0.92, 1.94, 0.46),
    "LPK_Cooling_HeatExchanger": (1.16, 1.70, 0.56),
    "LPK_LifeSupport_ScrubberHero": (1.42, 1.83, 0.58),
}


def mat(name, rgb, metallic=0.0, roughness=0.55):
    value = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    value.diffuse_color = (*rgb, 1.0)
    value.metallic = metallic
    value.roughness = roughness
    return value


def finish(obj, material, bevel=0.012):
    if bevel:
        mod = obj.modifiers.new("ReadableEdge", "BEVEL")
        mod.width = bevel
        mod.segments = 2
    obj.data.materials.append(material)
    return obj


def cube(name, loc, size, material, bevel=0.012):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = tuple(v * 0.5 for v in size)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, material, bevel)


def cyl(name, loc, radius, depth, material, rotation=(0, 0, 0), vertices=20):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc,
                                       rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, material, 0.01)


def torus(name, loc, major, minor, material, rotation=(math.pi / 2, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=20,
                                    minor_segments=8, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    return obj


def parent_asset(name, parts):
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    for part in parts:
        part.parent = root
    return root


def fit_authored_bounds(root, target):
    # Freeze edge softening first, then fit the complete visible silhouette into the
    # legacy dressing envelope. Detail may project forward, but never grows the slot.
    for child in root.children_recursive:
        if child.type != "MESH":
            continue
        bpy.context.view_layer.objects.active = child
        for modifier in list(child.modifiers):
            bpy.ops.object.modifier_apply(modifier=modifier.name)
    corners = [child.matrix_world @ mathutils.Vector(corner)
               for child in root.children_recursive if child.type == "MESH" for corner in child.bound_box]
    low = mathutils.Vector(tuple(min(v[i] for v in corners) for i in range(3)))
    high = mathutils.Vector(tuple(max(v[i] for v in corners) for i in range(3)))
    current = high - low
    factors = mathutils.Vector((target[0] / current.x, target[2] / current.y, target[1] / current.z))
    for child in root.children_recursive:
        child.location = mathutils.Vector((child.location.x * factors.x,
                                           child.location.y * factors.y,
                                           child.location.z * factors.z))
        child.scale = mathutils.Vector((child.scale.x * factors.x,
                                        child.scale.y * factors.y,
                                        child.scale.z * factors.z))


def bus_panel(materials):
    shell, dark, orange, screen, rubber = materials
    p = [
        cube("CabinetShell", (0, 0, 0.97), (0.92, 0.40, 1.94), shell, 0.025),
        cube("Recess", (0, -0.216, 1.18), (0.76, 0.045, 1.14), dark, 0.015),
        cube("Header", (0, -0.242, 1.78), (0.62, 0.025, 0.10), orange, 0.006),
        cube("KickPlate", (0, -0.238, 0.18), (0.72, 0.03, 0.22), dark, 0.008),
        cube("BatteryDock", (0, -0.285, 0.55), (0.55, 0.14, 0.34), rubber, 0.018),
        cube("DockGlow", (0, -0.362, 0.55), (0.36, 0.018, 0.07), orange, 0.004),
    ]
    for row in range(3):
        for col in range(4):
            x = -0.27 + col * 0.18
            y = 1.52 - row * 0.25
            p += [cube(f"Breaker_{row}_{col}", (x, -0.27, y), (0.115, 0.075, 0.13), dark, 0.01),
                  cube(f"Status_{row}_{col}", (x, -0.312, y + 0.08), (0.07, 0.012, 0.025),
                       screen if (row + col) % 3 else orange, 0.003)]
    for x in (-0.38, 0.38):
        p.append(cube(f"DoorRail_{x:+.0f}", (x, -0.245, 1.12), (0.035, 0.035, 1.36), rubber, 0.006))
    p.append(cube("ServiceHandle", (0.34, -0.32, 1.08), (0.055, 0.08, 0.34), orange, 0.012))
    return parent_asset("LPK_Power_BusPanel", p)


def heat_exchanger(materials):
    shell, dark, cyan, screen, rubber = materials
    p = [cube("BackFrame", (0, 0.13, 0.85), (1.16, 0.22, 1.70), shell, 0.025),
         cube("CoilRecess", (0, -0.01, 0.90), (0.92, 0.10, 1.32), dark, 0.018),
         cube("HeaderTop", (0, -0.12, 1.53), (1.03, 0.18, 0.14), cyan, 0.018),
         cube("HeaderBottom", (0, -0.12, 0.20), (1.03, 0.18, 0.14), cyan, 0.018)]
    for i in range(7):
        z = 0.34 + i * 0.17
        p.append(cyl(f"Coil_{i}", (0, -0.15, z), 0.055, 0.88, cyan,
                     rotation=(0, math.pi / 2, 0), vertices=16))
    for x in (-0.50, 0.50):
        p.append(cube(f"Guard_{x:+.0f}", (x, -0.20, 0.86), (0.075, 0.10, 1.43), rubber, 0.014))
    p += [torus("ValveWheel", (0.37, -0.31, 1.39), 0.13, 0.023, screen),
          cyl("ValveHub", (0.37, -0.31, 1.39), 0.045, 0.10, screen,
              rotation=(math.pi / 2, 0, 0), vertices=16),
          cube("FlowMark", (-0.30, -0.25, 1.62), (0.30, 0.025, 0.05), screen, 0.004)]
    return parent_asset("LPK_Cooling_HeatExchanger", p)


def scrubber(materials):
    shell, dark, lime, screen, rubber = materials
    p = [cube("Backplate", (0, 0.15, 0.91), (1.42, 0.22, 1.82), shell, 0.028),
         cube("Base", (0, -0.03, 0.09), (1.38, 0.54, 0.18), dark, 0.018),
         cube("Manifold", (0, -0.10, 1.67), (1.32, 0.30, 0.18), lime, 0.018)]
    for i, x in enumerate((-0.46, 0, 0.46)):
        p += [cyl(f"Canister_{i}", (x, -0.13, 0.89), 0.18, 1.25, dark, vertices=20),
              torus(f"ClampUpper_{i}", (x, -0.13, 1.26), 0.18, 0.025, lime, rotation=(0, 0, 0)),
              torus(f"ClampLower_{i}", (x, -0.13, 0.53), 0.18, 0.025, lime, rotation=(0, 0, 0)),
              cube(f"FilterWindow_{i}", (x, -0.325, 0.90), (0.13, 0.025, 0.42), screen, 0.012),
              cube(f"Latch_{i}", (x, -0.36, 1.40), (0.10, 0.055, 0.13), rubber, 0.009)]
    p += [cube("CO2Readout", (0.46, -0.29, 1.66), (0.24, 0.03, 0.075), screen, 0.005),
          cube("ServiceStripe", (-0.35, -0.29, 1.66), (0.38, 0.03, 0.055), lime, 0.004)]
    return parent_asset("LPK_LifeSupport_ScrubberHero", p)


def export_one(output_root, root):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    asset_dir = output_root / root.name
    asset_dir.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(asset_dir / f"{root.name}.blend"))
    bpy.ops.export_scene.fbx(filepath=str(asset_dir / f"{root.name}.fbx"), use_selection=True,
        object_types={"EMPTY", "MESH"}, apply_unit_scale=True, bake_space_transform=False,
        axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True, add_leaf_bones=False, path_mode="AUTO")


def render_review(output_root, roots):
    for i, root in enumerate(roots):
        root.location.x = (i - 1) * 1.9
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x, scene.render.resolution_y = 1400, 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    world = bpy.data.worlds.new("ReviewWorld")
    world.color = (0.015, 0.022, 0.032)
    scene.world = world
    bpy.ops.object.camera_add(location=(4.9, -8.3, 3.6))
    camera = bpy.context.object
    camera.data.lens = 62
    camera.rotation_euler = (mathutils.Vector((0, 0, 0.95)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(-3.5, -4, 5))
    bpy.context.object.data.energy, bpy.context.object.data.size = 1100, 5.0
    bpy.ops.object.light_add(type="AREA", location=(4, 0, 2.5))
    bpy.context.object.data.energy, bpy.context.object.data.size = 800, 3.0
    bpy.context.object.data.color = (0.20, 0.55, 1.0)
    scene.render.filepath = str(output_root / "LastShift_SystemHeroes_review.png")
    bpy.ops.render.render(write_still=True)


def main(output_root):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    shared = (mat("LSH_Shell", (0.17, 0.22, 0.27), 0.2, 0.58),
              mat("LSH_Recess", (0.035, 0.055, 0.07), 0.1, 0.72),
              mat("LSH_Accent", (0.95, 0.38, 0.06), 0.05, 0.48),
              mat("LSH_Readout", (0.18, 0.90, 0.92), 0.0, 0.28),
              mat("LSH_Rubber", (0.018, 0.026, 0.032), 0.0, 0.88))
    roots = [bus_panel(shared), heat_exchanger((shared[0], shared[1], mat("LSH_Cyan", (0.05, 0.53, 0.68), 0.15, 0.42), shared[3], shared[4])),
             scrubber((shared[0], shared[1], mat("LSH_Lime", (0.38, 0.72, 0.18), 0.05, 0.50), shared[3], shared[4]))]
    for root in roots:
        fit_authored_bounds(root, ASSETS[root.name])
        export_one(output_root, root)
    render_review(output_root, roots)
    print("[LAST_SHIFT_SYSTEM_HEROES] assets=3 result=PASS")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:]
    main(Path(args[0]))
