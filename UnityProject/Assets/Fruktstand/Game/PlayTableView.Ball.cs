// The ball at the play table (EPIC-003, EPIC-017): the child rolls it to the player, fetches it when it is out of reach
// or has waited long on the player's side, and catches a soft toss and rolls it straight back.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed partial class PlayTableView
    {
        const float RollAfterSeconds = 0.8f; // the ball lies still this long before the child rolls it

        sealed class BallPlay : ToyPlay
        {
            bool fetching;
            float wait;
            bool tossed;             // EPIC-017: in a toss's arc (no air damping)
            float tossTime, tossFlight;
            float holdLeft;          // EPIC-017: the child holds the ball it caught

            public BallPlay(PlayTableView view, Child child) : base(view, child) { }

            Item Ball => c.Toys[0];

            public override void PutOnTable() => PutCarried(Models.BallRadius, Quaternion.identity, AddBall);

            // EPIC-017: a tossed ball flies without air damping; one that comes near the child's hands is caught.
            public override void Tick(PlayArea area, float dt)
            {
                if (!tossed) return;
                var ball = Ball;
                tossTime += dt;
                var near = Vector3.Distance(v.top.InverseTransformPoint(ball.Position), CatchPoint) < CatchRadius;
                if (near && area.Control(Ware.Ball) == ToyControl.Free)
                {
                    EndToss();
                    ball.Body.isKinematic = true;
                    ball.Transform.localPosition = CatchPoint;
                    area.ToyAtRest(Ware.Ball); // caught: the child has it
                    holdLeft = CatchHoldSeconds;
                    c.Rest = 0;
                    wait = 0;
                    v.BallCatches++;
                    c.Figure.Wave(-45);
                    v.sounds.Pop();
                    Debug.Log($"[Fruktstand] ball caught ({v.BallCatches})");
                }
                else if (tossTime > tossFlight + 0.4f) EndToss();
            }

            void EndToss()
            {
                if (!tossed) return;
                tossed = false;
                Ball.Body.linearDamping = BallDamping;
            }

            public override void Play(float dt)
            {
                var ball = Ball;
                if (holdLeft > 0)
                {
                    // Holds the caught ball a moment, puts it in the ball corner and rolls it back at once.
                    holdLeft -= dt;
                    if (holdLeft > 0) return;
                    Fetch(ball, BallHome);
                    var arrived = ball.OnArrive;
                    ball.OnArrive = () =>
                    {
                        arrived?.Invoke();
                        c.Rest = RollAfterSeconds;
                    };
                    return;
                }
                if (ball.Moving || fetching) return;
                var local = v.top.InverseTransformPoint(ball.Position);
                var speed = ball.Body.linearVelocity.magnitude;
                if (speed > RestSpeed) { c.Rest = 0; wait = 0; return; }
                c.Rest += dt;
                if (c.Rest < RollAfterSeconds) return;
                if (local.z > 0.02f && Vector3.Distance(local, BallHome) < 0.14f)
                {
                    // Roll it over the table to the player's side.
                    var dir = (BallTarget - local).normalized;
                    ball.Body.linearVelocity = v.top.TransformDirection(dir) * ChildPushSpeed;
                    c.Figure.Wave(-30);
                    v.sounds.Tick();
                    c.Rest = 0;
                    wait = 0;
                    return;
                }
                // On the child's half but out of reach, or waiting on the player's side for a long time: fetch it.
                wait += dt;
                if (local.z > 0.02f || wait > FetchAfterSeconds) Fetch(ball, BallHome);
            }

            void Fetch(Item toy, Vector3 home)
            {
                fetching = true;
                toy.Body.isKinematic = true;
                toy.MoveTo(home, Quaternion.identity, 0.6f);
                toy.OnArrive = () =>
                {
                    fetching = false;
                    toy.Body.isKinematic = false;
                    toy.Body.linearVelocity = Vector3.zero;
                    wait = 0;
                };
            }

            public override void Taken(Item toy)
            {
                fetching = false;
                holdLeft = 0; // EPIC-017: taken from the child's hands
                EndToss();
            }

            // EPIC-017: lifted and let go moving forward: a soft toss (to the child's hands, or along the movement).
            public override void AfterPlace(Item toy, Vector3 handVelocity, ref Vector3 velocity)
            {
                var toss = BallToss.Plan(v.top.InverseTransformPoint(toy.Position), v.top.InverseTransformDirection(handVelocity), CatchPoint);
                if (toss.Kind == BallToss.Kind.None) return;
                Debug.Log($"[Fruktstand] ball toss: {toss.Kind} to {toss.Target:F2} in {toss.Flight:F2} s (hand {handVelocity.magnitude:F2} m/s)");
                velocity = v.top.TransformDirection(toss.Velocity);
                tossed = true;
                tossTime = 0;
                tossFlight = toss.Flight;
                toy.Body.linearDamping = 0;
            }

            public override void Returning(Item toy)
            {
                fetching = false;
                EndToss();
                holdLeft = 0;
            }
        }
    }
}
