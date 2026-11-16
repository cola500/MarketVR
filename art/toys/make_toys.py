# Generates every toy model (EPIC-010): art/toys/toys.blend, one FBX per toy in
# UnityProject/Assets/Fruktstand/Models/<Ware>.fbx, the play pieces Ring.fbx, RingPeg.fbx and Frog.fbx (EPIC-018), and
# art/toys/contact_sheet.png.
# Run from the repo root:  blender -b --factory-startup -P art/toys/make_toys.py [-- Top ...]
# The shared building blocks, export and contact sheet are in art/kit.py (the Unity contract is described there).
# Here +y = Unity +z: the car and the train drive towards +y. Sizes keep the toys' existing colliders: the ball's radius
# (Models.BallRadius 0.045) and the car's box (Models.CarSize 0.07 x 0.045 x 0.12, wheels below it).
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

HERE = os.path.dirname(os.path.abspath(__file__))

# sRGB preview colours (mirror Colors / ModelColours in Models.cs).
PALETTE.update({
    "ToyRed": (0.95, 0.25, 0.25), "ToyYellow": (1.0, 0.82, 0.2), "ToyBlue": (0.25, 0.5, 0.95), "ToyWhite": (0.97, 0.96, 0.92),
    "ToyGreen": (0.35, 0.78, 0.4), "Window": (0.8, 0.92, 1.0), "Tyre": (0.12, 0.12, 0.16), "Wood": (0.85, 0.66, 0.42),
    "Block0": (0.95, 0.3, 0.3), "Block1": (1.0, 0.75, 0.2), "Block2": (0.35, 0.7, 0.95), "Block3": (0.45, 0.8, 0.4),
    "Block4": (0.75, 0.5, 0.9), "Block5": (1.0, 0.55, 0.75), "Ring": (0.95, 0.3, 0.3),
    "FrogGreen": (0.35, 0.78, 0.3), "FrogBelly": (0.85, 0.95, 0.6), "EyeWhite": (0.98, 0.98, 0.96), "Pupil": (0.1, 0.1, 0.14),
    "Spring": (1.0, 0.82, 0.2), "FrogOrange": (1.0, 0.55, 0.2), "LilyPad": (0.2, 0.6, 0.3),
})

R = 0.045  # Models.BallRadius

def ball(bm):
    # a beach ball: six coloured segments and white caps
    rows = 10
    profile = [(math.sin(math.pi * i / rows) * R, -math.cos(math.pi * i / rows) * R) for i in range(rows + 1)]
    layers = lathe(bm, profile, 0, sides=12)
    for f in bm.faces:
        c = f.calc_center_median()
        if abs(c.z) > R * 0.85:
            f.material_index = 3
        else:
            a = math.atan2(c.y, c.x) % (2 * math.pi)
            f.material_index = int(a / (2 * math.pi) * 6) % 3
    return ["ToyRed", "ToyYellow", "ToyBlue", "ToyWhite"]

def car(bm):
    W, H, L = 0.07, 0.045, 0.12  # Models.CarSize; centre at 0, forward +y
    box(bm, Vector((0, 0, -0.004)), (W, L, H * 0.62), 0)                      # body
    box(bm, Vector((0, L * 0.47, -0.004)), (W * 0.9, 0.012, H * 0.5), 0)      # rounded-ish nose
    box(bm, Vector((0, -0.008, H * 0.38)), (W * 0.84, L * 0.46, H * 0.42), 0)  # cabin
    box(bm, Vector((0, -0.008, H * 0.6)), (W * 0.86, L * 0.48, 0.004), 3)     # white roof
    box(bm, Vector((0, L * 0.17, H * 0.42)), (W * 0.74, 0.003, H * 0.38), 1)  # windscreen
    box(bm, Vector((0, -L * 0.255, H * 0.42)), (W * 0.74, 0.003, H * 0.38), 1)
    for side in (-1, 1):
        box(bm, Vector((side * W * 0.42, -0.008, H * 0.42)), (0.003, L * 0.42, H * 0.36), 1)  # side windows
        for y in (-L * 0.32, L * 0.32):
            lathe(bm, [(0, -0.009), (0.016, -0.009), (0.016, 0.009), (0, 0.009)], 2, sides=12, axis=Vector((1, 0, 0)),
                  origin=Vector((side * W * 0.5, y, -H * 0.45)))
            lathe(bm, [(0, 0.0), (0.007, 0.0), (0.006, 0.002), (0, 0.002)], 3, sides=8, axis=Vector((side, 0, 0)),
                  origin=Vector((side * (W * 0.5 + 0.009), y, -H * 0.45)))
        blob(bm, Vector((side * W * 0.3, L * 0.5 + 0.006, 0.002)), (0.008, 0.002, 0.006), 4)  # headlights
    return ["ToyBlue", "Window", "Tyre", "ToyWhite", "ToyYellow"]

def blocks(bm):
    # the little set in the shop: six coloured blocks, 2 x 3 (it becomes six full-size blocks at the table)
    for i in range(6):
        box(bm, Vector((-0.022 + 0.022 * (i % 3), 0, -0.011 + 0.022 * (i // 3))), (0.021, 0.021, 0.021), i)
    return ["Block0", "Block1", "Block2", "Block3", "Block4", "Block5"]

def top(bm):
    # a spinning top: point at the bottom, a wide striped body, a wooden handle
    profile = [(0, 0.0), (0.006, 0.004), (0.02, 0.014), (0.033, 0.026), (0.037, 0.031), (0.034, 0.036), (0.022, 0.044),
               (0.006, 0.048), (0.006, 0.072), (0, 0.074)]
    lathe(bm, profile, 0, sides=16)
    for f in bm.faces:
        z = f.calc_center_median().z
        f.material_index = 3 if z > 0.047 else 1 if 0.024 < z < 0.04 else 0 if z < 0.024 else 2
    return ["ToyRed", "ToyYellow", "ToyBlue", "Wood"]

def train(bm):
    # an engine with a chimney and a cab, coupled to a little open wagon; drives towards +y
    W = 0.05
    # engine (front, +y)
    box(bm, Vector((0, 0.04, -0.008)), (W, 0.07, 0.014), 0)                                  # chassis
    lathe(bm, [(0, 0.0), (0.017, 0.0), (0.017, 0.05), (0, 0.05)], 0, sides=12, axis=Vector((0, 1, 0)), origin=Vector((0, 0.022, 0.016)))  # boiler
    box(bm, Vector((0, 0.012, 0.022)), (W * 0.95, 0.024, 0.04), 1)                           # cab
    box(bm, Vector((0, 0.012, 0.044)), (W * 1.05, 0.03, 0.005), 0)                           # cab roof
    lathe(bm, [(0, 0.0), (0.007, 0.0), (0.006, 0.016), (0.01, 0.022), (0, 0.022)], 2, sides=8, origin=Vector((0, 0.058, 0.028)))  # chimney
    # wagon (back, -y)
    box(bm, Vector((0, -0.045, -0.008)), (W, 0.06, 0.014), 0)
    for (c, s) in (((0, -0.045, 0.012), (W, 0.06, 0.004)), ((0, -0.016, 0.02), (W, 0.003, 0.02)), ((0, -0.074, 0.02), (W, 0.003, 0.02)),
                   ((W / 2, -0.045, 0.02), (0.003, 0.06, 0.02)), ((-W / 2, -0.045, 0.02), (0.003, 0.06, 0.02))):
        box(bm, Vector(c), s, 3)
    box(bm, Vector((0, -0.006, -0.008)), (0.008, 0.016, 0.006), 2)                            # coupling
    for side in (-1, 1):
        for y in (0.065, 0.025, -0.03, -0.06):
            lathe(bm, [(0, -0.005), (0.011, -0.005), (0.011, 0.005), (0, 0.005)], 2, sides=10, axis=Vector((1, 0, 0)),
                  origin=Vector((side * (W / 2 + 0.002), y, -0.016)))
    return ["ToyRed", "ToyYellow", "Tyre", "ToyGreen"]

def stackingrings(bm):
    # the shop set: a wooden base and peg with three rings, the biggest at the bottom
    lathe(bm, [(0, 0.0), (0.038, 0.0), (0.04, 0.006), (0.038, 0.012), (0, 0.012)], 0, sides=16)
    stick(bm, Vector((0, 0, 0.012)), Vector((0, 0, 0.09)), 0.0075, 0, sides=8)
    blob(bm, Vector((0, 0, 0.092)), (0.009, 0.009, 0.007), 0, smooth=True)
    for i, (r, t) in enumerate(((0.03, 0.01), (0.025, 0.009), (0.02, 0.008))):
        torus(bm, Vector((0, 0, 0.022 + i * 0.02)), Vector((0, 0, 1)), r, t, 1 + i, segments=16, sides=8)
    return ["Wood", "ToyRed", "ToyYellow", "ToyBlue"]

def ring_piece(bm):
    # one full-size stacking ring for the play table (scaled down for the smaller two)
    torus(bm, Vector((0, 0, 0)), Vector((0, 0, 1)), 0.03, 0.011, 0, segments=18, sides=8)
    return ["Ring"]

def peg_piece(bm):
    # the rings' fixed base and peg at the table
    lathe(bm, [(0, 0.0), (0.042, 0.0), (0.044, 0.007), (0.042, 0.013), (0, 0.013)], 0, sides=18)
    stick(bm, Vector((0, 0, 0.013)), Vector((0, 0, 0.1)), 0.008, 0, sides=8)
    blob(bm, Vector((0, 0, 0.102)), (0.01, 0.01, 0.008), 0, smooth=True)
    return ["Wood"]

def ellipsoid(bm, centre, radii, slot, rows=6, sides=10):
    """A smooth ellipsoid (a lathed unit sphere, scaled)."""
    before = set(bm.verts)
    lathe(bm, [(math.sin(math.pi * i / rows), -math.cos(math.pi * i / rows)) for i in range(rows + 1)], slot, sides=sides)
    for v in bm.verts:
        if v not in before:
            v.co = Vector((centre.x + v.co.x * radii[0], centre.y + v.co.y * radii[1], centre.z + v.co.z * radii[2]))
    return [f for f in bm.faces if all(v not in before for v in f.verts)]

def frog_at(bm, o, k=1.0, body=0):
    """EPIC-018: a stylised hopping frog, facing +y, sitting on z = 0: a broad body with a pale belly, big eyes, folded
    hind legs and a clear springy rear (a yellow press pad on the rump and a coil under it). Slots: body colour `body`,
    1 belly, 2 eye white, 3 pupil and mouth, 4 spring."""
    def at(x, y, z):
        return o + Vector((x, y, z)) * k
    def r(x, y, z):
        return (x * k, y * k, z * k)
    torso = ellipsoid(bm, at(0, 0, 0.022), r(0.037, 0.042, 0.021), body, rows=8, sides=14)
    for f in torso:
        if f.calc_center_median().z < o.z + 0.012 * k:
            f.material_index = 1                                                   # pale belly
    ellipsoid(bm, at(0, 0.03, 0.031), r(0.031, 0.024, 0.018), body)                # wide head
    for side in (-1, 1):
        ellipsoid(bm, at(side * 0.019, 0.034, 0.05), r(0.014, 0.014, 0.014), 2)     # big eyes
        ellipsoid(bm, at(side * 0.021, 0.046, 0.054), r(0.0065, 0.004, 0.0075), 3, rows=4, sides=8)
        ellipsoid(bm, at(side * 0.026, 0.04, 0.006), r(0.008, 0.013, 0.006), body, rows=4, sides=8)   # front legs
        ellipsoid(bm, at(side * 0.036, -0.016, 0.014), r(0.013, 0.025, 0.013), body)   # folded hind legs
        ellipsoid(bm, at(side * 0.043, 0.006, 0.003), r(0.013, 0.018, 0.003), body, rows=4, sides=8)  # feet
    ellipsoid(bm, at(0, 0.053, 0.026), r(0.017, 0.0025, 0.0025), 3, rows=4, sides=8)  # smile
    ellipsoid(bm, at(0, -0.032, 0.038), r(0.018, 0.015, 0.007), 4)                   # the press pad on the rump
    for i in range(3):                                                               # the spring under the rear
        torus(bm, at(0, -0.044, 0.004 + i * 0.006), Vector((0, 0, 1)), 0.011 * k, 0.0028 * k, 4, segments=10, sides=5)

def frog_piece(bm):
    # one frog for the play table (coloured per owner in Unity)
    frog_at(bm, Vector((0, 0, 0)))
    return ["FrogGreen", "FrogBelly", "EyeWhite", "Pupil", "Spring"]

def frogs(bm):
    # the shop set: the same two frogs on a little lily pad
    pad = lathe(bm, [(0, 0.0), (0.062, 0.0), (0.062, 0.004), (0, 0.004)], 6, sides=20, smooth=False)
    frog_at(bm, Vector((-0.03, -0.004, 0.004)), 0.72, body=0)
    frog_at(bm, Vector((0.032, 0.008, 0.004)), 0.72, body=5)
    return ["FrogGreen", "FrogBelly", "EyeWhite", "Pupil", "Spring", "FrogOrange", "LilyPad"]

WARES = {"Ball": ball, "Car": car, "Blocks": blocks, "Top": top, "Train": train, "StackingRings": stackingrings,
         "Frogs": frogs, "Ring": ring_piece, "RingPeg": peg_piece, "Frog": frog_piece}

run(WARES, HERE, sheet_columns=3, sheet_spacing=(0.22, 0.2), sheet_scale=0.92, extras=("Ring", "RingPeg", "Frog"),
    sheet_turns={"Car": math.pi / 2 + 0.4, "Train": math.pi / 2 + 0.4, "Frogs": math.pi + 0.7})  # the vehicles from the side
