# Shared Blender building blocks for the ware models (art/fruit, art/candy, art/toys): bmesh shapes, materials,
# the Unity export and a contact sheet. Each make_*.py adds its own wares and palette and calls run().
#
# Unity contract (docs/SPIKE-BLENDER.md, Models): metres; pivot = bounds centre (the grab point); up = +y; long wares
# along +x. Material slots, in the order a ware lists them, become Unity submeshes 0..n (faces are sorted by material)
# and are coloured in code (Models.ModelColours); the colours here are only for the preview. Unity's FBX import
# mirrors x, so every model is turned 180 degrees about up before export.
import math, os, sys, bpy, bmesh
from mathutils import Vector, Matrix

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS = os.path.join(ROOT, "UnityProject", "Assets", "Fruktstand", "Models")
PALETTE = {"Label": (0.12, 0.12, 0.16)}  # name -> sRGB; each script adds its own

def lin(c):
    return tuple(x ** 2.2 for x in c)

_mats = {}
def mat(name):
    if name not in _mats:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = (*lin(PALETTE[name]), 1)
        bsdf.inputs["Roughness"].default_value = 0.75
        m.diffuse_color = (*lin(PALETTE[name]), 1)
        _mats[name] = m
    return _mats[name]

# ---------------------------------------------------------------- building blocks (bmesh, Blender frame: z up)

def ring(bm, centre, axis, radius, sides, phase=0.0, wobble=None):
    axis = axis.normalized()
    u = axis.orthogonal().normalized()
    v = axis.cross(u).normalized()
    out = []
    for s in range(sides):
        a = phase + s * 2 * math.pi / sides
        r = radius * (wobble(a) if wobble else 1)
        out.append(bm.verts.new(centre + (math.cos(a) * u + math.sin(a) * v) * r))
    return out

def lathe(bm, profile, slot, sides=14, axis=Vector((0, 0, 1)), origin=Vector(), wobble=None, smooth=True):
    """profile: [(radius, height)] from start to end along axis; radius 0 = a pole."""
    axis = axis.normalized()
    layers = []
    for r, h in profile:
        c = origin + axis * h
        layers.append([bm.verts.new(c)] if r <= 1e-6 else ring(bm, c, axis, r, sides, wobble=wobble))
    for a, b in zip(layers, layers[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        for s in range(sides):
            if len(a) == 1:
                f = bm.faces.new((a[0], b[(s + 1) % sides], b[s]))
            elif len(b) == 1:
                f = bm.faces.new((a[s], a[(s + 1) % sides], b[0]))
            else:
                f = bm.faces.new((a[s], a[(s + 1) % sides], b[(s + 1) % sides], b[s]))
            f.material_index = slot
            f.smooth = smooth
    return layers

def blob(bm, centre, size, slot, rot=Matrix.Identity(3), smooth=False):
    """A flattened octahedron (leaf, seed, calyx point): 8 triangles, reads from both sides."""
    sx, sy, sz = size
    pts = [Vector((sx, 0, 0)), Vector((-sx, 0, 0)), Vector((0, sy, 0)), Vector((0, -sy, 0)), Vector((0, 0, sz)), Vector((0, 0, -sz))]
    v = [bm.verts.new(centre + rot @ p) for p in pts]
    for a, b in ((0, 2), (2, 1), (1, 3), (3, 0)):
        for pole in (4, 5):
            f = bm.faces.new((v[a], v[b], v[pole]) if pole == 4 else (v[b], v[a], v[pole]))
            f.material_index = slot
            f.smooth = smooth

def stick(bm, start, end, radius, slot, sides=5, end_radius=None):
    axis = end - start
    lathe(bm, [(0, 0), (radius, 0), (end_radius if end_radius is not None else radius, axis.length), (0, axis.length)],
          slot, sides=sides, axis=axis, origin=start, smooth=False)

def along(d):
    """Rotation taking +x to d (its flat side then faces roughly +-y)."""
    d = d.normalized()
    side = d.cross(Vector((0, 1, 0)))
    if side.length < 1e-6:
        side = d.cross(Vector((0, 0, 1)))
    side.normalize()
    up = side.cross(d).normalized()
    return Matrix((d, up, side)).transposed()


def box(bm, centre, size, slot, rot=Matrix.Identity(3), smooth=False):
    """An axis box (size = full extents), optionally rotated about its centre."""
    hx, hy, hz = (v / 2 for v in size)
    corners = [Vector((x, y, z)) for x in (-hx, hx) for y in (-hy, hy) for z in (-hz, hz)]
    v = [bm.verts.new(centre + rot @ c) for c in corners]
    for quad in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
        f = bm.faces.new([v[i] for i in quad])
        f.material_index = slot
        f.smooth = smooth

def torus(bm, centre, axis, radius, tube, slot, segments=16, sides=8):
    """A ring round `axis` (the hole's direction)."""
    axis = axis.normalized()
    u = axis.orthogonal().normalized()
    w = axis.cross(u).normalized()
    rings = []
    for i in range(segments):
        a = i * 2 * math.pi / segments
        out = math.cos(a) * u + math.sin(a) * w
        c = centre + out * radius
        rings.append([bm.verts.new(c + (math.cos(b) * out + math.sin(b) * axis) * tube)
                      for b in (j * 2 * math.pi / sides for j in range(sides))])
    for i in range(segments):
        a, b = rings[i], rings[(i + 1) % segments]
        for j in range(sides):
            f = bm.faces.new((a[j], b[j], b[(j + 1) % sides], a[(j + 1) % sides]))
            f.material_index = slot
            f.smooth = True

def tube(bm, points, radius, sides, slot_of, caps=True, smooth=True):
    """A round tube along a polyline. slot_of(ring, side) -> material slot of that face (stripes)."""
    rings = []
    for i, p in enumerate(points):
        d = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
        rings.append(ring(bm, p, d, radius(i) if callable(radius) else radius, sides))
    for i in range(len(rings) - 1):
        for j in range(sides):
            f = bm.faces.new((rings[i][j], rings[i][(j + 1) % sides], rings[i + 1][(j + 1) % sides], rings[i + 1][j]))
            f.material_index = slot_of(i, j)
            f.smooth = smooth
    if caps:
        for r, flip in ((rings[0], True), (rings[-1], False)):
            f = bm.faces.new(list(reversed(r)) if flip else r)
            f.material_index = slot_of(0 if flip else len(rings) - 2, 0)

def rot_to(direction):
    return Vector((0, 0, 1)).rotation_difference(direction.normalized()).to_matrix()

# ---------------------------------------------------------------- build, check, export

def build(name, fn):
    bm = bmesh.new()
    slots = fn(bm)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for s in slots:
        mesh.materials.append(mat(s))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.sort_elements(type='MATERIAL', elements={'FACE'})
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.rotation_euler = (0, 0, math.pi)  # Unity's import mirrors x
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    obj.location = (0, 0, 0)
    tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
    used = sorted({p.material_index for p in mesh.polygons})
    assert used == list(range(len(slots))), f"{name}: every material slot is used ({used})"
    d = obj.dimensions
    print(f"[model] {name}: triangles={tris} materials={len(slots)} size_m=({d.x:.3f}, {d.y:.3f}, {d.z:.3f})")
    return obj, tris

def export(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    path = os.path.join(MODELS, obj.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False, path_mode='STRIP')

def contact_sheet(objs, path, columns=4, spacing=(0.24, 0.22), scale=1.0, turns=None):
    scene = bpy.context.scene
    for i, obj in enumerate(objs):
        rows = (len(objs) + columns - 1) // columns
        obj.location = ((i % columns - (columns - 1) / 2) * spacing[0], 0, ((rows - 1) / 2 - i // columns) * spacing[1])
        obj.rotation_euler = (0, 0, (turns or {}).get(obj.name, math.pi + 0.35))  # turned to the camera, a little to the side
        txt = bpy.data.curves.new(obj.name + " label", 'FONT')
        txt.body = obj.name
        txt.size = 0.018
        txt.align_x = 'CENTER'
        label = bpy.data.objects.new(obj.name + " label", txt)
        label.location = (obj.location.x, -0.05, obj.location.z - 0.095)
        label.rotation_euler = (math.radians(90), 0, 0)
        label.data.materials.append(mat("Label"))
        scene.collection.objects.link(label)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = scale
    cam.location = (0, -1.0, 0.0)
    cam.rotation_euler = (math.radians(90), 0, 0)
    scene.collection.objects.link(cam)
    scene.camera = cam
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(60), math.radians(-20), math.radians(20))
    scene.collection.objects.link(sun)
    world = bpy.data.worlds.new("World"); scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.85, 0.9, 1.0, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    engines = {e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items}
    scene.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in engines else 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x, scene.render.resolution_y = 1400, 760
    scene.render.filepath = path
    for flag in [f for f in dir(scene.render) if f.startswith("use_stamp")]:
        setattr(scene.render, flag, False)  # no file path or date in the PNG's metadata (the repo is public)
    bpy.ops.render.render(write_still=True)


def run(wares, here, sheet_columns=4, sheet_spacing=(0.24, 0.22), sheet_scale=1.0, extras=(), sheet_turns=None):
    """Builds and exports every ware (or the names after `--`), saves <here>/<folder>.blend and a contact sheet.
    extras: names of play pieces that are exported but not on the sheet."""
    only = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(wares)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear()
    bpy.context.scene.unit_settings.system = 'METRIC'
    objs, total = [], 0
    for name in only:
        obj, tris = build(name, wares[name])
        export(obj)
        if name in extras:
            obj.hide_render = True  # a play piece: exported, not on the sheet
        else:
            objs.append(obj)
        total += tris
    print(f"[model] exported {len(only)} models, {total} triangles in all")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(here, os.path.basename(here) + ".blend"))
    contact_sheet(objs, os.path.join(here, "contact_sheet.png"), sheet_columns, sheet_spacing, sheet_scale, sheet_turns)
    print("[model] contact sheet", os.path.join(here, "contact_sheet.png"))
