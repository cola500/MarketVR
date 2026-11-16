// A friendly customer made of primitives, standing on the ground across the counter of the stall for the order's
// category and facing the player (EPIC-003). Appearance varies per customer number; toy customers are children
// (smaller). Candy customers are sad (a tear) until they have their candy (The child's wish). Movement follows the sale
// state: walks in, waits with a gentle bob, beams when the order is right, hops and waves when paid, walks out.
// A child who bought a toy is taken over by the play area (PlayTableView) the moment the sale is registered.
// EPIC-016: on its way in and out it keeps its distance to the park's visitors and the children (Pacer).
using System.Collections.Generic;
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class CustomerView
    {
        public static readonly Vector3 Spot = new(0.12f, 0, 0.5f); // stand space (ground), across the counter
        public const float WalkDistance = 1.6f; // in from the side, out to the other (stall space, along x)
        public const float ChildScale = 0.72f;

        readonly Art art;
        readonly Transform root;
        readonly Transform figureRoot;
        CustomerFigure figure;
        int builtFor = -1;

        public Transform Root => root;
        public bool IsChild { get; private set; }
        public Mood Mood => figure?.Mood ?? Mood.Neutral;
        public Transform Stall { get; private set; }
        public Portrait Portrait { get; set; } // the phone customer's photo (EPIC-004), set by FruitStand
        public bool IsHuman { get; private set; }
        public bool Cheering { get; private set; } // EPIC-008: back at the stall, waving at the market party

        // During the market party: the last customer stands at the stall, happy, hopping a little and waving.
        public void Cheer(bool on, float time)
        {
            Cheering = on;
            if (!on || figure == null) return;
            root.gameObject.SetActive(true);
            root.SetLocalPositionAndRotation(Spot + Vector3.up * (Mathf.Abs(Mathf.Sin(time * 5)) * 0.03f), Quaternion.identity);
            figure.SetMood(Mood.Happy);
            figure.Wave(Mathf.Sin(time * 9) * 30);
        }

        public CustomerView(Art art, Transform anyParent)
        {
            this.art = art;
            root = art.Group(anyParent, "Customer", Spot);
            figureRoot = art.Group(root, "Figure");
        }

        void Build(int number, bool child)
        {
            if (figure != null) Art.Discard(figure.Body);
            builtFor = number;
            IsChild = child;
            figureRoot.localScale = Vector3.one * (child ? ChildScale : 1f);
            figure = new CustomerFigure(art, figureRoot, number);
            Portrait?.Attach(figure.Body);
        }

        // Moves the customer to the stall of the current order's category.
        public void AtStall(Transform stallRoot)
        {
            if (Stall == stallRoot) return;
            Stall = stallRoot;
            root.SetParent(stallRoot, false);
        }

        // EPIC-015: the customer walks in along the park's paths (ParkRoutes.Arrival) and steps out along the counter
        // (Departure) while the counter is still theirs; the park visitors take over from there. Walking advances only
        // when the market is not held (party, rest), like the sale itself.
        // EPIC-016: the walk keeps its distance to the park's visitors and the children (Pacer); the customer has priority
        // over them at a meeting, so the sales go on, and is never held for ever either.
        public const int Priority = int.MaxValue - 1;
        Polyline route;
        int routeFor = -1;
        bool routeIsDeparture;
        float walked, step;
        Pacer pacer = new();
        Vector3 direction = Vector3.forward;
        Quaternion heading = Quaternion.identity;
        static readonly Walker[] Nobody = new Walker[0];

        public bool AtCounter { get; private set; }   // arrived: the shop may take the order
        public bool Near { get; private set; }        // close enough to say hello
        public float Walked => walked;                // how far along the current route
        public bool Walking { get; private set; }      // on a route (in or out), not standing at the counter
        public float Speed => Walking ? pacer.Speed : 0;
        public Vector3 Velocity => direction * Speed; // market space
        public float Side => Walking ? pacer.Side : 0; // the step aside from the route, for tests and the log

        public void Show(Shop shop, float time, float dt = 0, bool held = false, Transform market = null, float stallDistance = MarketLayout.DefaultStallDistance,
            IReadOnlyList<Walker> others = null)
        {
            others ??= Nobody;
            Walking = false;
            if (shop.CustomerNumber != builtFor) Build(shop.CustomerNumber, shop.CustomerIsChild);
            IsHuman = shop.CustomerIsHuman;
            Portrait?.Show(IsHuman);
            var t = shop.StateTime;
            var offset = Vector3.zero;
            var happy = false;
            var wave = 0f;
            // Closed: nobody at the counter. A child who bought a toy is now walking to the play area instead.
            var toySold = shop.Order.Category == Category.Toys && shop.State is SaleState.Thanking or SaleState.CustomerLeaving;
            root.gameObject.SetActive(shop.State != SaleState.Closed && !toySold);
            var sadUntilServed = shop.Order.Category == Category.Candy;
            switch (shop.State)
            {
                case SaleState.CustomerArriving:
                    if (routeFor != shop.CustomerNumber || routeIsDeparture)
                    {
                        route = ParkRoutes.Arrival(shop.Order.Category, stallDistance);
                        routeFor = shop.CustomerNumber;
                        routeIsDeparture = false;
                        walked = 0;
                        pacer = new Pacer();
                        heading = Quaternion.LookRotation(-route.At(0).direction);
                    }
                    WalkAlong(market, dt, held, turnToCounter: true, others);
                    AtCounter = walked >= route.Length - 1e-4f;
                    Near = route.Length - walked < 1.0f;
                    figure.SetMood(sadUntilServed ? Mood.Sad : Mood.Neutral);
                    figure.Wave(0);
                    return;
                case SaleState.Ordering:
                    offset.y = Mathf.Sin(time * 2) * 0.006f;
                    break;
                case SaleState.AwaitingPayment:
                    happy = true;
                    offset.y = Mathf.Sin(time * 3) * 0.008f;
                    break;
                case SaleState.Thanking:
                    happy = true;
                    offset.y = Mathf.Abs(Mathf.Sin(t * 6)) * 0.07f * Mathf.Clamp01(2 - t);
                    wave = Mathf.Sin(t * 10) * 25;
                    break;
                case SaleState.CustomerLeaving:
                    if (!routeIsDeparture)
                    {
                        route = ParkRoutes.Departure(shop.Order.Category, stallDistance, Stroll.Exit);
                        routeIsDeparture = true;
                        walked = 0;
                        pacer = new Pacer();
                        heading = Quaternion.LookRotation(-route.At(0.01f).direction);
                    }
                    WalkAlong(market, dt, held, turnToCounter: false, others);
                    figure.SetMood(Mood.Happy);
                    figure.Wave(10);
                    AtCounter = false;
                    return;
            }
            AtCounter = Near = true;
            root.localPosition = Spot + offset;
            root.localRotation = Quaternion.identity;
            figure.SetMood(happy ? Mood.Happy : sadUntilServed ? Mood.Sad : Mood.Neutral);
            figure.Wave(wave);
        }

        void WalkAlong(Transform market, float dt, bool held, bool turnToCounter, IReadOnlyList<Walker> others)
        {
            Walking = true;
            if (!held)
            {
                var (at, along) = route.At(walked);
                var move = pacer.Step(dt, ParkRoutes.WalkSpeed, at, along, Priority, others, route.Length - walked, ParkLayout.OnPath);
                walked = Mathf.Min(route.Length, walked + move);
                step += move / (IsChild ? 0.4f : 0.55f) * Mathf.PI; // the bob follows the distance walked
            }
            var (p, dir) = route.At(walked);
            direction = dir;
            p += pacer.Offset(dir);
            var left = route.Length - walked;
            var target = Quaternion.LookRotation(-dir);
            if (turnToCounter && left < 0.4f && Stall != null && market != null)
                target = Quaternion.Inverse(market.rotation) * Stall.rotation; // face the player at the counter
            if (!held) heading = Quaternion.Slerp(heading, target, 1 - Mathf.Exp(-7f * dt));
            var bob = Mathf.Abs(Mathf.Sin(step)) * 0.025f * (IsChild ? ChildScale : 1f) * pacer.Pace(ParkRoutes.WalkSpeed);
            if (market == null)
            {
                root.localPosition = Spot;
                return;
            }
            root.SetPositionAndRotation(market.TransformPoint(p + Vector3.up * bob), market.rotation * heading);
        }
    }
}
