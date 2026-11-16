# Generates every fruit/veg model (EPIC-009): art/fruit/fruit.blend, one FBX per ware in
# UnityProject/Assets/Fruktstand/Models/<Ware>.fbx, and a contact sheet render (art/fruit/contact_sheet.png).
# Run from the repo root:  blender -b --factory-startup -P art/fruit/make_fruit.py [-- Pear Apple ...]
#
# The shared building blocks, export and contact sheet are in art/kit.py (the Unity contract is described there).
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403  (bpy, bmesh, Vector, Matrix, lathe, blob, stick, along, rot_to, run, PALETTE)

HERE = os.path.dirname(os.path.abspath(__file__))

# sRGB preview colours (mirror Colors in Models.cs).
PALETTE.update({
    "Apple": (0.86, 0.11, 0.12), "Stem": (0.38, 0.24, 0.12), "Leaf": (0.2, 0.62, 0.18),
    "Banana": (0.98, 0.84, 0.16), "BananaTip": (0.22, 0.14, 0.07), "Carrot": (1.0, 0.5, 0.06),
    "Pear": (0.72, 0.84, 0.25), "Orange": (1.0, 0.58, 0.08), "Strawberry": (0.93, 0.13, 0.22),
    "StrawberrySeed": (1.0, 0.92, 0.55), "Grape": (0.52, 0.24, 0.62), "GrapeStem": (0.45, 0.42, 0.18),
    "MelonFlesh": (0.97, 0.3, 0.36), "MelonRind": (0.18, 0.55, 0.22), "MelonRindLight": (0.82, 0.93, 0.7),
    "MelonSeed": (0.12, 0.12, 0.16),
})

# ---------------------------------------------------------------- the wares

def apple(bm):
    lathe(bm, [(0, 0.008), (0.016, 0.001), (0.03, 0.003), (0.04, 0.012), (0.045, 0.026), (0.045, 0.04), (0.042, 0.055), (0.035, 0.067), (0.022, 0.074), (0.01, 0.073), (0, 0.066)], 0, sides=16)
    stick(bm, Vector((0, 0, 0.064)), Vector((0.004, 0, 0.088)), 0.003, 1)
    blob(bm, Vector((0.016, 0, 0.083)), (0.016, 0.008, 0.002), 2, Matrix.Rotation(0.5, 3, 'Y'))
    return ["Apple", "Stem", "Leaf"]

def banana(bm):
    R, ARC, THICK, RINGS, SIDES = 0.13, 0.75, 0.024, 16, 10
    def arc(t):
        a = (t * 2 - 1) * ARC
        return Vector((math.sin(a) * R, 0, R - math.cos(a) * R)), Vector((math.cos(a), 0, math.sin(a)))
    rings = []
    for r in range(RINGS + 1):
        t = r / RINGS
        c, tan = arc(t)
        rad = THICK * (0.25 + 0.75 * math.sin(math.pi * t) ** 0.6)
        side = Vector((0, 1, 0)); up = side.cross(tan).normalized() * -1
        rings.append([bm.verts.new(c + (math.cos(a) * up + math.sin(a) * side) * rad * (1 + 0.05 * math.cos(5 * a)))
                      for a in (s * 2 * math.pi / SIDES for s in range(SIDES))])
    for r in range(RINGS):
        for s in range(SIDES):
            f = bm.faces.new((rings[r][s], rings[r][(s + 1) % SIDES], rings[r + 1][(s + 1) % SIDES], rings[r + 1][s]))
            f.material_index = 2 if r == 0 else 0
            f.smooth = True
    c0, t0 = arc(0)
    tip = bm.verts.new(c0 - t0 * THICK * 0.35)
    for s in range(SIDES):
        f = bm.faces.new((rings[0][(s + 1) % SIDES], rings[0][s], tip)); f.material_index = 2; f.smooth = True
    c1, t1 = arc(1)
    cap = bm.verts.new(c1)
    for s in range(SIDES):
        f = bm.faces.new((rings[-1][s], rings[-1][(s + 1) % SIDES], cap)); f.material_index = 0; f.smooth = True
    d = (t1 + Vector((0, 0, 0.35))).normalized()
    stick(bm, c1 - d * 0.004, c1 + d * 0.022, 0.0065, 1, sides=6, end_radius=0.0052)
    return ["Banana", "Stem", "BananaTip"]

def carrot(bm):
    # along x: leaves at -x, tip at +x (Unity +x after the turn)
    wob = lambda a: 1 + 0.04 * math.cos(3 * a)
    lathe(bm, [(0, -0.07), (0.016, -0.072), (0.023, -0.064), (0.022, -0.035), (0.018, 0.0), (0.012, 0.035), (0.006, 0.065), (0, 0.08)],
          0, sides=10, axis=Vector((1, 0, 0)), wobble=wob)
    base = Vector((-0.068, 0, 0))
    for ang, tilt in ((-0.5, 0.3), (0.0, -0.2), (0.5, 0.25)):  # three bushy fronds
        d = Vector((-math.cos(ang), math.sin(tilt) * 0.5, math.sin(ang))).normalized()
        stick(bm, base, base + d * 0.035, 0.0025, 1, sides=4)
        for k, (t, size) in enumerate(((0.022, 0.009), (0.034, 0.011), (0.046, 0.012))):
            for twist in (0.0, math.pi / 2):  # a cross of two flat leaves reads from every side
                blob(bm, base + d * t, (size, 0.0015, size * 0.45), 1, along(d) @ Matrix.Rotation(twist, 3, 'X'))
    return ["Carrot", "Leaf"]

def pear(bm):
    lathe(bm, [(0, 0.004), (0.022, 0.0), (0.036, 0.007), (0.044, 0.02), (0.046, 0.034), (0.041, 0.048), (0.031, 0.061), (0.024, 0.074), (0.02, 0.087), (0.015, 0.098), (0.007, 0.104), (0, 0.105)], 0, sides=16)
    stick(bm, Vector((0, 0, 0.1)), Vector((-0.004, 0, 0.122)), 0.0028, 1)
    blob(bm, Vector((-0.015, 0, 0.115)), (0.014, 0.007, 0.002), 2, Matrix.Rotation(-0.5, 3, 'Y'))
    return ["Pear", "Stem", "Leaf"]

def orange(bm):
    lathe(bm, [(0, 0.002), (0.017, 0.004), (0.031, 0.012), (0.04, 0.024), (0.044, 0.04), (0.042, 0.055), (0.034, 0.068), (0.02, 0.078), (0, 0.082)], 0, sides=18)
    for i in range(5):  # small green calyx star on top
        a = i * 2 * math.pi / 5
        blob(bm, Vector((math.cos(a) * 0.006, math.sin(a) * 0.006, 0.082)), (0.006, 0.0025, 0.0015), 1, Matrix.Rotation(a, 3, 'Z'))
    return ["Orange", "Leaf"]

def strawberry(bm):
    lathe(bm, [(0, 0.0), (0.012, 0.008), (0.025, 0.026), (0.033, 0.045), (0.032, 0.056), (0.02, 0.063), (0, 0.064)], 0, sides=12)
    for i in range(6):
        a = i * 2 * math.pi / 6
        blob(bm, Vector((math.cos(a) * 0.016, math.sin(a) * 0.016, 0.064)), (0.014, 0.006, 0.002), 1, Matrix.Rotation(a, 3, 'Z') @ Matrix.Rotation(-0.25, 3, 'Y'))
    stick(bm, Vector((0, 0, 0.062)), Vector((0, 0, 0.076)), 0.0025, 1, sides=4)
    for row, (h, r, n) in enumerate(((0.016, 0.017, 5), (0.03, 0.027, 7), (0.044, 0.032, 8))):
        for i in range(n):
            a = (i + 0.5 * row) * 2 * math.pi / n
            normal = Vector((math.cos(a), math.sin(a), 0.25)).normalized()
            blob(bm, Vector((math.cos(a) * r, math.sin(a) * r, h)) + normal * 0.0006, (0.0018, 0.0018, 0.0012), 2, rot_to(normal))
    return ["Strawberry", "Leaf", "StrawberrySeed"]

def grapes(bm):
    g = 0.0155
    rows = [(4, 0.0), (3, -0.024), (3, -0.046), (2, -0.068), (1, -0.088)]
    for ri, (n, z) in enumerate(rows):
        for i in range(n):
            x = (i - (n - 1) / 2) * g * 1.85
            y = (0.008 if (i + ri) % 2 else -0.008)
            lathe(bm, [(0, -g), (g * 0.7, -g * 0.7), (g, 0), (g * 0.7, g * 0.7), (0, g)], 0, sides=8, origin=Vector((x, y, z)))
    stick(bm, Vector((0, 0, 0.008)), Vector((0.004, 0, 0.034)), 0.003, 1)
    stick(bm, Vector((0.004, 0, 0.034)), Vector((0.016, 0, 0.04)), 0.0025, 1, sides=4)
    return ["Grape", "GrapeStem"]

def watermelon(bm):
    # a half disc standing up, the straight cut on top, the rind round the bottom; faces +-y
    R, T, N = 0.07, 0.026, 14
    bands = [(0.0, 0.054, 0), (0.054, 0.061, 2), (0.061, R, 1)]  # flesh, light rind, green rind
    def p(r, a, y):
        return Vector((math.cos(a) * r, y, math.sin(a) * r))
    angles = [math.pi + i * math.pi / N for i in range(N + 1)]
    for y, flip in ((-T / 2, False), (T / 2, True)):
        for r0, r1, slot in bands:
            for a0, a1 in zip(angles, angles[1:]):
                if r0 == 0:
                    vs = [bm.verts.new(p(0, a0, y)), bm.verts.new(p(r1, a0, y)), bm.verts.new(p(r1, a1, y))]
                else:
                    vs = [bm.verts.new(p(r0, a0, y)), bm.verts.new(p(r1, a0, y)), bm.verts.new(p(r1, a1, y)), bm.verts.new(p(r0, a1, y))]
                f = bm.faces.new(list(reversed(vs)) if flip else vs)
                f.material_index = slot
    for a0, a1 in zip(angles, angles[1:]):  # outer rind
        f = bm.faces.new((bm.verts.new(p(R, a0, -T / 2)), bm.verts.new(p(R, a0, T / 2)), bm.verts.new(p(R, a1, T / 2)), bm.verts.new(p(R, a1, -T / 2))))
        f.material_index = 1
        f.smooth = True
    for r0, r1, slot in bands:  # the flat cut on top, both halves
        for sgn in (-1, 1):
            vs = [bm.verts.new(Vector((sgn * r0, -T / 2, 0))), bm.verts.new(Vector((sgn * r1, -T / 2, 0))),
                  bm.verts.new(Vector((sgn * r1, T / 2, 0))), bm.verts.new(Vector((sgn * r0, T / 2, 0)))]
            f = bm.faces.new(vs if sgn > 0 else list(reversed(vs)))
            f.material_index = slot
    for side in (-1, 1):  # seeds on both faces
        for (r, a) in ((0.03, 1.25), (0.03, 1.55), (0.03, 1.85), (0.042, 1.1), (0.042, 1.45), (0.042, 1.75), (0.042, 2.05)):
            c = Vector((math.cos(math.pi + a) * r, side * (T / 2 + 0.0008), math.sin(math.pi + a) * r))
            blob(bm, c, (0.0022, 0.0035, 0.0009), 3, Matrix.Rotation(math.pi / 2, 3, 'X') @ Matrix.Rotation(a, 3, 'Z'))
    return ["MelonFlesh", "MelonRind", "MelonRindLight", "MelonSeed"]

WARES = {"Apple": apple, "Banana": banana, "Carrot": carrot, "Pear": pear, "Orange": orange,
         "Strawberry": strawberry, "Grapes": grapes, "Watermelon": watermelon}

run(WARES, HERE, sheet_columns=4, sheet_spacing=(0.24, 0.22), sheet_scale=1.0)
