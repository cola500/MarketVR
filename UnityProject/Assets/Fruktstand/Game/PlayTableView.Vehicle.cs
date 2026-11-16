// The car and the train at the play table (EPIC-003, EPIC-010): the child drives them round the road; let go by the
// player, they roll along their length only and are put back on the road at its nearest point.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed partial class PlayTableView
    {
        static float RoadLength => Mathf.PI * (3 * (RoadRadii.x + RoadRadii.y) - Mathf.Sqrt((3 * RoadRadii.x + RoadRadii.y) * (RoadRadii.x + 3 * RoadRadii.y)));
        static Quaternion RoadRotation(float t) => Quaternion.LookRotation(RoadPoint(t + 0.01f) - RoadPoint(t));

        sealed class VehiclePlay : ToyPlay
        {
            readonly float speed;
            float roadT;
            bool gliding; // let go by the player: back onto the road first

            public VehiclePlay(PlayTableView view, Child child, float speed) : base(view, child) => this.speed = speed;

            public override void PutOnTable()
            {
                var t = PutCarried(0.07f, RoadRotation(0.75f), c.Toy == Ware.Train
                    ? body => AddBody(body, TrainSize, 0.25f, 0.03f, 0.6f, freezeTilt: true)
                    : body => AddBody(body, Models.CarSize + new Vector3(0, 0.02f, 0), 0.2f, 0.03f, 0.6f, freezeTilt: true)); // rolls on its wheels: low friction
                if (t != null) roadT = 0.75f;
            }

            public override void AtRest() => gliding = true;

            public override void Play(float dt)
            {
                var car = c.Toys[0];
                if (car.Moving) return;
                car.Body.isKinematic = true;
                if (gliding)
                {
                    // Back on the road at the nearest point before driving on.
                    gliding = false;
                    roadT = NearestRoadT(v.top.InverseTransformPoint(car.Position));
                    car.MoveTo(RoadPoint(roadT, car.Ware), RoadRotation(roadT), 0.5f);
                    return;
                }
                roadT = Mathf.Repeat(roadT + speed * dt / RoadLength, 1);
                car.Transform.SetLocalPositionAndRotation(RoadPoint(roadT, car.Ware), RoadRotation(roadT));
            }

            public override void HomeArrived() => roadT = 0.75f;
        }
    }
}
