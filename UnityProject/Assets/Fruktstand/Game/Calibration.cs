// EPIC-007: aligning the market to the room. Pure state: the area being previewed (tracking space: centre, yaw and
// width x length), the fit for it (SpaceFit) and the parent's adjustments. Nothing moves until Confirm, and only
// when the layout fits; a boundary change only suggests calibrating again.
using System.Collections.Generic;
using UnityEngine;

namespace Fruktstand.Game
{
    public enum CalibItem { Confirm, Width, Length, Rotate, Here, Face, UseSystem, Cancel }

    public sealed class Calibration
    {
        public const float SizeStep = 0.1f;
        public const float MinSize = 1.0f;
        public const float MaxSize = 6.0f;
        public const float RotateStep = 15f;
        public static readonly Vector2 ManualDefault = new(1.5f, 2.0f); // EPIC-003's assumed play space

        Vector2? systemSize;

        public bool Active { get; private set; }
        public bool FromSystem { get; private set; }
        public Vector2 Centre { get; private set; } // tracking space (x, z)
        public float Yaw { get; private set; }      // degrees; the fruit stall's direction
        public Vector2 Size { get; private set; }   // width (x) x length (z) of the available area
        public FitResult Fit { get; private set; } = new();
        public bool SuggestRecalibration { get; private set; }

        public IReadOnlyList<CalibItem> Items
        {
            get
            {
                var items = new List<CalibItem> { CalibItem.Confirm, CalibItem.Width, CalibItem.Length, CalibItem.Rotate, CalibItem.Here, CalibItem.Face };
                if (systemSize.HasValue) items.Add(CalibItem.UseSystem);
                items.Add(CalibItem.Cancel);
                return items;
            }
        }

        // systemSize: the system's play area (width x, length z), centred at the tracking origin along its axes [A];
        // null when the system gives nothing: then a manual area where the parent stands, facing where they look.
        public void Start(Vector2? system, Vector2 head, float headYaw)
        {
            systemSize = system;
            SuggestRecalibration = false;
            Active = true;
            if (system.HasValue) UseSystem();
            else
            {
                FromSystem = false;
                Centre = head;
                Yaw = headYaw;
                Size = ManualDefault;
            }
            Refit();
        }

        public void Adjust(CalibItem item, int dir, Vector2 head, float headYaw)
        {
            if (!Active) return;
            switch (item)
            {
                case CalibItem.Width:
                    Size = new Vector2(Step(Size.x, dir), Size.y);
                    break;
                case CalibItem.Length:
                    Size = new Vector2(Size.x, Step(Size.y, dir));
                    break;
                case CalibItem.Rotate:
                    Yaw = Mathf.Repeat(Yaw + dir * RotateStep, 360f);
                    break;
                case CalibItem.Here:
                    Centre = head;
                    break;
                case CalibItem.Face:
                    Yaw = Mathf.Repeat(headYaw, 360f);
                    break;
                case CalibItem.UseSystem:
                    if (!systemSize.HasValue) return;
                    UseSystem();
                    Refit();
                    return;
                default:
                    return;
            }
            FromSystem = false;
            Refit();
        }

        // The placement to apply, only if the layout fits; otherwise null and the preview stays.
        public RoomPlacement? Confirm()
        {
            if (!Active || !Fit.Fits) return null;
            Active = false;
            return new RoomPlacement(Fit.Centre, Fit.Yaw, Fit.StallDistance);
        }

        public void Cancel() => Active = false;

        public void BoundaryChanged() => SuggestRecalibration = true;

        public IEnumerable<string> StatusLines()
        {
            yield return $"{(FromSystem ? "Systemets spelyta" : "Inställd yta")}: {Sv(Size.x)} × {Sv(Size.y)} m";
            yield return Fit.Fits ? $"Får plats (stånden {Sv(Fit.StallDistance, "0.00")} m från mitten)" : Fit.Reason;
        }

        void UseSystem()
        {
            FromSystem = true;
            Centre = Vector2.zero;
            Yaw = 0;
            Size = systemSize.Value;
        }

        // Swedish decimal comma, the same on every device (no culture data needed).
        public static string Sv(float v, string format = "0.0") =>
            v.ToString(format, System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

        static float Step(float v, int dir) => Mathf.Clamp(Mathf.Round((v + dir * SizeStep) * 100) / 100, MinSize, MaxSize);

        void Refit() => Fit = SpaceFit.Fit(SpaceFit.RectPolygon(Centre, Yaw, Size), Yaw, Centre);
    }
}
