import bpy
import sys
from mathutils import Vector


fbx_path = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx_path)
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
corners = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
low = Vector((min(v.x for v in corners), min(v.y for v in corners), min(v.z for v in corners)))
high = Vector((max(v.x for v in corners), max(v.y for v in corners), max(v.z for v in corners)))
triangles = sum(len(poly.vertices) - 2 for obj in meshes for poly in obj.data.polygons)
size = high - low
print(f"[VERIFY_FBX] meshes={len(meshes)} tris={triangles} bounds={tuple(round(v, 3) for v in size)}")
assert 0.42 <= size.x <= 0.56
assert 0.42 <= size.y <= 0.56
assert 1.0 <= size.z <= 1.10
assert triangles < 10000
