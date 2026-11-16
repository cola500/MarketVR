// EPIC-007: the game's own floor marking, built from the same layout as the market (MarketLayout/MarketCollision
// at spacing d), so the marking and the stalls can never disagree. Used twice: as the market's marking (a child of
// the market root) and as the calibration preview (a separate root at the proposed pose). Shown only while
// calibrating: it marks the layout's area, it is no promise of free floor in the room.
using UnityEngine;

namespace Fruktstand.Game
{
    public static class LayoutMarking
    {
        const float Lift = 0.004f;

        public static Transform Build(Art art, Transform parent, string name, float d, Color tint)
        {
            var root = art.Group(parent, name);
            var need = SpaceFit.NeededSize(d);
            var body = need - Vector2.one * 2 * SpaceFit.Margin;
            art.Part(root, "Area", art.Cube, art.Unlit(Color.Lerp(tint, Color.white, 0.55f)), new Vector3(0, Lift, 0), new Vector3(need.x, 0.002f, need.y), null, false);
            art.Part(root, "Body", art.Cube, art.Unlit(Color.Lerp(tint, Color.white, 0.25f)), new Vector3(0, Lift + 0.001f, 0), new Vector3(body.x, 0.002f, body.y), null, false);
            foreach (var f in MarketCollision.FootprintsAt(d))
            {
                var piece = art.Part(root, $"Footprint {f.Name}", art.Cube, art.Unlit(tint * 0.75f), new Vector3(f.Centre.x, Lift + 0.002f, f.Centre.z),
                    new Vector3(f.Half.x * 2, 0.002f, f.Half.y * 2), Quaternion.Euler(0, f.Yaw, 0), false);
                piece.name = $"Footprint {f.Name}";
            }
            // An arrow towards the fruit stall (the market's front).
            art.Part(root, "Arrow", art.Cube, art.Unlit(tint * 0.6f), new Vector3(0, Lift + 0.003f, 0.12f), new Vector3(0.03f, 0.002f, 0.2f), null, false);
            foreach (var s in new[] { -1f, 1f })
                art.Part(root, "Arrow Head", art.Cube, art.Unlit(tint * 0.6f), new Vector3(s * 0.03f, Lift + 0.003f, 0.2f), new Vector3(0.03f, 0.002f, 0.1f), Quaternion.Euler(0, -s * 40, 0), false);
            return root;
        }
    }
}
