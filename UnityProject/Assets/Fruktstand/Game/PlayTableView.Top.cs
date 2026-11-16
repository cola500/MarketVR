// The spinning top at the play table (EPIC-010): the child sets it spinning; it slows down, rests a moment and is spun
// again. Put down on the table by the player, it spins up again.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed partial class PlayTableView
    {
        public bool Spinning(Ware toy) => ChildOf(toy)?.Play is TopPlay top && top.SpinLeft > 0;

        sealed class TopPlay : ToyPlay
        {
            public float SpinLeft { get; private set; } // spinning while > 0
            float act;

            public TopPlay(PlayTableView view, Child child) : base(view, child) { }

            Item Spinner => c.Toys[0];

            public override void PutOnTable() =>
                PutCarried(Models.Radius(Ware.Top), Quaternion.identity, t => AddBody(t, TopSize, 0.1f, 0.4f, 0.5f, freezeTilt: true)); // stands on its point

            public override void AtRest()
            {
                if (v.OnTable(Spinner)) StartSpin(); // put down on the table: it spins up again
            }

            public override void Play(float dt)
            {
                var spinner = Spinner;
                if (spinner.Moving) return;
                if (SpinLeft > 0)
                {
                    SpinLeft -= dt;
                    var speed = 900f * Mathf.Clamp01(SpinLeft / SpinSeconds) + 120f; // degrees per second
                    var wobble = 3f * Mathf.Clamp01(1 - SpinLeft / SpinSeconds);
                    var yaw = spinner.Transform.localEulerAngles.y + speed * dt;
                    spinner.Transform.localRotation = Quaternion.Euler(wobble * Mathf.Sin(yaw * 0.05f), yaw, wobble * Mathf.Cos(yaw * 0.05f));
                    if (SpinLeft <= 0)
                    {
                        spinner.Transform.localRotation = Quaternion.Euler(0, yaw, 0);
                        spinner.Body.isKinematic = false;
                        act = 0;
                    }
                    return;
                }
                act += dt;
                if (act < SpinRestSeconds || !v.OnTable(spinner)) return; // off the table: the lost-toy rule brings it back first
                StartSpin();
                c.Figure.Wave(-20);
                v.sounds.Tick();
            }

            void StartSpin()
            {
                Spinner.Body.isKinematic = true; // turns in place on its point
                SpinLeft = SpinSeconds;
            }

            public override void Taken(Item toy) => SpinLeft = 0; // a top stops spinning in the hand

            public override void Returning(Item toy) => SpinLeft = 0;
        }
    }
}
