# Generates every candy model (EPIC-010): art/candy/candy.blend, one FBX per ware in
# UnityProject/Assets/Fruktstand/Models/<Ware>.fbx and art/candy/contact_sheet.png.
# Run from the repo root:  blender -b --factory-startup -P art/candy/make_candy.py [-- GummyBear ...]
# The shared building blocks, export and contact sheet are in art/kit.py (the Unity contract is described there).
# Candy stands up (+z here = Unity up); the flat ones face the viewer (Blender -y = Unity -z, the bubbles' side).
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

HERE = os.path.dirname(os.path.abspath(__file__))

# sRGB preview colours (mirror Colors / ModelColours in Models.cs).
PALETTE.update({
    "CandyPink": (1.0, 0.45, 0.72), "CandyPinkLight": (1.0, 0.8, 0.9), "StickWhite": (0.97, 0.96, 0.92),
    "CaneRed": (0.9, 0.1, 0.15), "BagBlue": (0.55, 0.75, 1.0), "BagDot": (1.0, 0.85, 0.2),
    "Gummy": (0.35, 0.85, 0.35), "Ink": (0.12, 0.12, 0.16),
    "Chocolate": (0.42, 0.24, 0.13), "Wrapper": (0.85, 0.15, 0.2), "Foil": (0.85, 0.82, 0.7),
    "Toffee": (1.0, 0.6, 0.12), "Paper": (1.0, 0.88, 0.3),
    "Red": (0.93, 0.2, 0.2), "OrangeBand": (1.0, 0.58, 0.1), "Yellow": (1.0, 0.88, 0.2),
    "Green": (0.35, 0.8, 0.35), "Blue": (0.3, 0.55, 0.95), "Purple": (0.6, 0.35, 0.85),
    "RingBand": (0.4, 0.85, 0.85),
})

def lollipop(bm):
    # a round swirl disc facing the viewer on a white stick
    lathe(bm, [(0, -0.006), (0.034, -0.006), (0.038, -0.003), (0.038, 0.003), (0.034, 0.006), (0, 0.006)], 0, sides=20,
          axis=Vector((0, 1, 0)), origin=Vector((0, 0, 0.03)))
    for side in (-1, 1):  # light swirl rings on both faces
        for r in (0.026, 0.013):
            torus(bm, Vector((0, side * 0.0062, 0.03)), Vector((0, 1, 0)), r, 0.0028, 1, segments=14, sides=4)
    stick(bm, Vector((0, 0, -0.008)), Vector((0, 0, -0.085)), 0.004, 2, sides=6)
    return ["CandyPink", "CandyPinkLight", "StickWhite"]

def polkagris(bm):
    # a striped cane: a straight stick with a crook at the top, red helix stripes on white
    pts = [Vector((0, 0, -0.07 + 0.012 * i)) for i in range(11)]
    for k in range(1, 7):  # the crook
        a = k * math.pi / 6
        pts.append(Vector((0.02 - math.cos(a) * 0.02, 0, 0.05 + math.sin(a) * 0.02)))
    tube(bm, pts, 0.0095, 8, lambda i, j: 1 if (j + i) % 4 < 2 else 0)
    return ["StickWhite", "CaneRed"]

def candybag(bm):
    wob = lambda a: 1 + 0.05 * math.cos(7 * a)
    lathe(bm, [(0, 0), (0.028, 0.001), (0.034, 0.012), (0.035, 0.035), (0.03, 0.055), (0.014, 0.068), (0.011, 0.072),
               (0.02, 0.082), (0.024, 0.09), (0.016, 0.092), (0, 0.088)], 0, sides=14, wobble=wob)
    for side in (-1, 1):  # the bow at the neck
        blob(bm, Vector((side * 0.016, -0.012, 0.07)), (0.014, 0.004, 0.008), 1, Matrix.Rotation(side * 0.4, 3, 'Y'))
    blob(bm, Vector((0, -0.013, 0.07)), (0.005, 0.005, 0.005), 1)
    for i, (x, z) in enumerate(((-0.014, 0.02), (0.01, 0.014), (0.018, 0.034), (-0.006, 0.04), (0.0, 0.026))):
        blob(bm, Vector((x, -0.034 if abs(x) < 0.02 else -0.03, z)), (0.005, 0.002, 0.005), 2)
    return ["BagBlue", "CandyPink", "BagDot"]

def gummybear(bm):
    lathe(bm, [(0, 0.0), (0.016, 0.002), (0.022, 0.014), (0.022, 0.026), (0.016, 0.036), (0, 0.04)], 0, sides=12)  # body
    lathe(bm, [(0, 0.036), (0.012, 0.039), (0.016, 0.05), (0.012, 0.061), (0, 0.064)], 0, sides=12)  # head
    for side in (-1, 1):
        blob(bm, Vector((side * 0.012, 0, 0.063)), (0.006, 0.005, 0.006), 0, smooth=True)       # ears
        blob(bm, Vector((side * 0.022, -0.003, 0.026)), (0.007, 0.007, 0.009), 0, smooth=True)  # arms
        blob(bm, Vector((side * 0.011, -0.006, 0.002)), (0.008, 0.009, 0.006), 0, smooth=True)  # feet
        blob(bm, Vector((side * 0.0055, -0.0155, 0.053)), (0.0018, 0.001, 0.0022), 1)            # eyes
    blob(bm, Vector((0, -0.016, 0.047)), (0.003, 0.0015, 0.002), 1)                              # nose
    return ["Gummy", "Ink"]

def chocolate(bm):
    # a bar standing on its long edge, facing the viewer: six squares, the right half in a red wrapper
    W, H, T = 0.096, 0.056, 0.012
    box(bm, Vector((0, 0, 0)), (W, T, H), 0)
    for i in range(3):
        for j in range(2):
            box(bm, Vector((-W / 2 + W / 6 * (2 * i + 1), -T / 2 - 0.0015, -H / 4 + H / 2 * j)), (W / 3 - 0.006, 0.003, H / 2 - 0.006), 0)
    box(bm, Vector((W * 0.22, 0, 0)), (W * 0.56 + 0.002, T + 0.008, H + 0.004), 1)
    box(bm, Vector((W * 0.22 - W * 0.28, 0, 0)), (0.004, T + 0.01, H + 0.006), 2)  # foil edge
    return ["Chocolate", "Wrapper", "Foil"]

def wrappedcandy(bm):
    # a round toffee along x with twisted paper ends
    lathe(bm, [(0, -0.024), (0.01, -0.022), (0.017, -0.012), (0.018, 0.0), (0.017, 0.012), (0.01, 0.022), (0, 0.024)], 0,
          sides=12, axis=Vector((1, 0, 0)))
    pleat = lambda a: 1 + 0.25 * math.cos(6 * a)
    for side in (-1, 1):
        lathe(bm, [(0.0, side * 0.018), (0.004, side * 0.024), (0.006, side * 0.03), (0.016, side * 0.045), (0.012, side * 0.047),
                   (0, side * 0.044)], 1, sides=12, axis=Vector((1, 0, 0)), wobble=pleat, smooth=False)
    return ["Toffee", "Paper"]

def rainbowstrip(bm):
    # a flat strip along x with a gentle wave, six colour bands across its width, facing the viewer
    L, Wd, T, N = 0.13, 0.042, 0.004, 14
    bands = 6
    def p(i, k, y):
        x = -L / 2 + L * i / N
        z = -Wd / 2 + Wd * k / bands + 0.008 * math.sin(i / N * math.pi * 2)
        return Vector((x, y, z))
    for k in range(bands):
        for i in range(N):
            for y, flip in ((-T / 2, False), (T / 2, True)):
                vs = [bm.verts.new(p(i, k, y)), bm.verts.new(p(i + 1, k, y)), bm.verts.new(p(i + 1, k + 1, y)), bm.verts.new(p(i, k + 1, y))]
                f = bm.faces.new(list(reversed(vs)) if flip else vs)
                f.material_index = k
        for i in range(N):  # top and bottom edges
            for kk, y0 in ((0, None), (bands, None)):
                pass
    for i in range(N):
        for kk in (0, bands):
            vs = [bm.verts.new(p(i, kk, -T / 2)), bm.verts.new(p(i, kk, T / 2)), bm.verts.new(p(i + 1, kk, T / 2)), bm.verts.new(p(i + 1, kk, -T / 2))]
            f = bm.faces.new(vs if kk == 0 else list(reversed(vs)))
            f.material_index = 0 if kk == 0 else bands - 1
    return ["Red", "OrangeBand", "Yellow", "Green", "Blue", "Purple"]

def candyring(bm):
    # a ring standing up, facing the viewer, with a big faceted candy gem on top
    torus(bm, Vector((0, 0, 0)), Vector((0, 1, 0)), 0.022, 0.0055, 0, segments=18, sides=8)
    lathe(bm, [(0, 0.022), (0.012, 0.027), (0.017, 0.036), (0.015, 0.046), (0.008, 0.051), (0, 0.052)], 1, sides=8, smooth=False)
    return ["RingBand", "CandyPink"]

WARES = {"Lollipop": lollipop, "Polkagris": polkagris, "CandyBag": candybag, "GummyBear": gummybear,
         "Chocolate": chocolate, "WrappedCandy": wrappedcandy, "RainbowStrip": rainbowstrip, "CandyRing": candyring}

run(WARES, HERE, sheet_columns=4, sheet_spacing=(0.2, 0.2), sheet_scale=0.86)
