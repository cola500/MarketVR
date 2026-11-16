// One thing the player can pick up or press: a ware at a stall (fruit, vegetable, candy or toy), the payment token,
// a toy at the play area (with physics), a picture button, or a frog's rear (pressed and let go, EPIC-018). Lives under a counter top or the play table, so it
// moves with the counter height. Its Place says where it is; FruitStand moves it between places.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    // Toy states (SPEC §17): Crate = Shop, Basket = Handover, Carried, Play (child's play / free), Held = PlayerHeld,
    // Returning = Resetting.
    public enum ItemPlace { Crate, Held, Basket, Returning, Tray, Paid, Gone, Carried, Play, Fixed }

    public enum ItemKind { Ware, Coin, Toy, Button, Press }

    public sealed class Item : IGrabbable
    {
        public readonly ItemKind Kind;
        public readonly Ware Ware;
        public readonly Transform Transform;
        public readonly System.Action OnPress;   // buttons only
        public System.Action OnArrive;           // runs once when the current tween finishes
        public Rigidbody Body;                   // play toys only
        public int Index;                        // block number for building blocks
        public float LostTime;                   // EPIC-011: seconds on the floor (play toys; one per piece)
        public Vector3 LastValid;                // EPIC-011: table-space place before the player took it
        readonly float radius;

        public ItemPlace Place;
        public int Slot = -1;
        public bool Grabbable;

        // Simple tween in the parent's space.
        Vector3 from, to;
        Quaternion fromRot, toRot;
        float tween = 1, tweenSeconds;
        float scaleTo = 1;

        Item(Transform transform, ItemKind kind, Ware ware, float radius, System.Action onPress)
        {
            Transform = transform;
            Kind = kind;
            Ware = ware;
            this.radius = radius;
            OnPress = onPress;
        }

        public static Item ForWare(Transform t, Ware w) => new(t, ItemKind.Ware, w, Models.Radius(w), null);
        public static Item ForCoin(Transform t) => new(t, ItemKind.Coin, Ware.Apple, Models.CoinRadius, null);
        public static Item ForToy(Transform t, Ware w, float radius) => new(t, ItemKind.Toy, w, radius, null) { Place = ItemPlace.Play };
        public static Item ForButton(Transform t, float radius, System.Action onPress) => new(t, ItemKind.Button, Ware.Apple, radius, onPress) { Place = ItemPlace.Fixed };
        // EPIC-018: held down while the grip is held and acting when it is let go (a frog's rear); it never moves.
        public static Item ForPress(Transform t, Ware w, float radius) => new(t, ItemKind.Press, w, radius, null) { Place = ItemPlace.Fixed };

        public bool IsCoin => Kind == ItemKind.Coin;
        public bool IsWare => Kind == ItemKind.Ware;
        public bool IsToy => Kind == ItemKind.Toy;
        public bool IsButton => Kind == ItemKind.Button;
        public bool IsPress => Kind == ItemKind.Press;
        public Category Category => WareInfo.CategoryOf(Ware);
        public Vector3 Position => Transform.position;
        public float Radius => radius;
        public bool CanGrab => Grabbable && Transform.gameObject.activeInHierarchy;
        public bool Moving => tween < 1;

        public void MoveTo(Vector3 local, Quaternion rotation, float seconds, float scale = 1)
        {
            from = Transform.localPosition;
            fromRot = Transform.localRotation;
            to = local;
            toRot = rotation;
            scaleTo = scale;
            tweenSeconds = Mathf.Max(0.0001f, seconds);
            tween = 0;
        }

        public void Snap(Vector3 local, Quaternion rotation)
        {
            Transform.SetLocalPositionAndRotation(local, rotation);
            tween = 1;
        }

        public void StopTween() => tween = 1;

        // Returns true on the frame the tween finishes.
        public bool Animate(float dt)
        {
            if (tween >= 1) return false;
            tween = Mathf.Min(1, tween + dt / tweenSeconds);
            var e = tween * tween * (3 - 2 * tween);
            var arc = Mathf.Sin(tween * Mathf.PI) * 0.04f;
            Transform.SetLocalPositionAndRotation(Vector3.Lerp(from, to, e) + Vector3.up * arc, Quaternion.Slerp(fromRot, toRot, e));
            if (scaleTo != 1) Transform.localScale = Vector3.one * Mathf.Lerp(1, scaleTo, e);
            if (tween < 1) return false;
            var arrive = OnArrive;
            OnArrive = null;
            arrive?.Invoke();
            return true;
        }
    }
}
