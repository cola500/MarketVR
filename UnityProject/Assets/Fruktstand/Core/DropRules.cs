// Where a released item goes. Generous zones so precision is never the challenge. Positions are in counter-top
// space (y = 0 on the counter surface). Anything not caught by a zone goes home (produce to its crate, the coin
// tray back to its spot): nothing is ever dropped on the floor.
using UnityEngine;

namespace Fruktstand.Core
{
    public enum DropTarget { Home, Basket, Register }

    public readonly struct DropZone
    {
        public readonly Vector3 Centre;
        public readonly float Radius; // horizontal (x/z) catch radius
        public readonly float Below;  // how far below the zone centre still counts
        public readonly float Above;  // how far above the zone centre still counts

        public DropZone(Vector3 centre, float radius, float below, float above)
        {
            Centre = centre;
            Radius = radius;
            Below = below;
            Above = above;
        }

        public float HorizontalDistance(Vector3 p) => new Vector2(p.x - Centre.x, p.z - Centre.z).magnitude;

        public bool Contains(Vector3 p)
        {
            var dy = p.y - Centre.y;
            return HorizontalDistance(p) <= Radius && dy >= -Below && dy <= Above;
        }
    }

    public static class DropRules
    {
        public static DropTarget Resolve(bool isCoin, Vector3 position, DropZone basket, DropZone register) =>
            isCoin
                ? register.Contains(position) ? DropTarget.Register : DropTarget.Home
                : basket.Contains(position) ? DropTarget.Basket : DropTarget.Home;

        // The slot whose (counter-space) position is nearest to where the item was released.
        public static int NearestSlot(Vector3 position, Vector3[] slotPositions)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < slotPositions.Length; i++)
            {
                var d = new Vector2(position.x - slotPositions[i].x, position.z - slotPositions[i].z).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
