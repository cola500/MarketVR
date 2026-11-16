// VR input for both Quest Touch controllers, read through UnityEngine.XR.InputDevices (as in Magic Tiles and
// CampfireVR: no XR Interaction Toolkit, no Input System). Produces the same ShopFrame as the desktop.
// Focus awareness (Meta VRC.Quest.Input.4): input focus (Unity.XR.Oculus.InputFocus + OnApplicationFocus), pause
// and headset presence feed XrInputRouter, which blocks all play, hides the hands and sends held items home while
// any is lost. The Meta/system button is never read; the Menu button is read from both hands.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Fruktstand.Game
{
    public sealed class XrRig : MonoBehaviour
    {
        [Tooltip("Grab point relative to the controller's device pose (metres, controller space).")]
        [SerializeField] Vector3 handOffset = new(0, -0.015f, 0.03f);
        [SerializeField] MenuPanel menuPanel;

        readonly XrInputRouter router = new();
        readonly Calibration calibration = new(); // EPIC-007: aligning the market to the room, menu-driven
        readonly StandPlacement placement = new();
        float wornSeconds;
        Vector3 lastHeadPosition;
        Quaternion lastHeadRotation = Quaternion.identity;
        bool haveLastHead;
        readonly List<XRInputSubsystem> subsystems = new();
        bool appFocus = true;
        bool xrFocus = true;
        Transform head;
        FruitStand stand;

        public XrInputRouter Router => router;

        public void Activate(Camera camera, FruitStand fruitStand)
        {
            stand = fruitStand;
            router.Menu.Calibration = calibration;
            router.PointAt = (hand, state) =>
            {
                var (origin, direction) = PointerRay(state);
                return menuPanel.Hit(origin, direction);
            };
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // tracking space == world space
            head = camera.transform;
            head.SetParent(transform, false);
            camera.nearClipPlane = 0.02f;
            stand.Haptic += SendHaptic;
            SetFloorOriginAndRecenter();
            // Holding the Meta button recenters the view; bring the stands in front of the player when that happens.
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems)
            {
                s.trackingOriginUpdated += OnTrackingOriginUpdated;
                s.boundaryChanged += OnBoundaryChanged; // SPIKE-007: logged only, never moves the market
            }
            Application.onBeforeRender += UpdateHead;
        }

        void OnEnable()
        {
            Unity.XR.Oculus.InputFocus.InputFocusLost += OnXrFocusLost;
            Unity.XR.Oculus.InputFocus.InputFocusAcquired += OnXrFocusAcquired;
        }

        void OnDisable()
        {
            Unity.XR.Oculus.InputFocus.InputFocusLost -= OnXrFocusLost;
            Unity.XR.Oculus.InputFocus.InputFocusAcquired -= OnXrFocusAcquired;
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= UpdateHead;
            if (stand != null) stand.Haptic -= SendHaptic;
            foreach (var s in subsystems)
            {
                s.trackingOriginUpdated -= OnTrackingOriginUpdated;
                s.boundaryChanged -= OnBoundaryChanged;
            }
        }

        // A changed boundary never moves the market; the menu suggests calibrating again (EPIC-007).
        void OnBoundaryChanged(XRInputSubsystem _)
        {
            BoundaryProbe.Log("boundaryChanged");
            calibration.BoundaryChanged();
        }

        void OnTrackingOriginUpdated(XRInputSubsystem _)
        {
            BoundaryProbe.Log("tracking origin updated");
            placement.Request();
            Debug.Log("[Fruktstand] tracking origin updated (recenter): stands will be placed in front");
        }

        void OnXrFocusLost() => SetFocus(appFocus, false);
        void OnXrFocusAcquired() => SetFocus(appFocus, true);
        void OnApplicationFocus(bool hasFocus) => SetFocus(hasFocus, xrFocus);

        void OnApplicationPause(bool paused)
        {
            if (paused == router.Paused) return;
            router.SetPaused(paused);
            Debug.Log($"[Fruktstand] app {(paused ? "paused" : "resumed")}");
        }

        void SetFocus(bool app, bool xr)
        {
            appFocus = app;
            xrFocus = xr;
            var focus = app && xr;
            if (focus == router.HasFocus) return;
            router.SetFocus(focus);
            Debug.Log($"[Fruktstand] input focus {(focus ? "acquired" : "lost")} (app={app}, xr={xr})");
        }

        // Returns the presence reading (null if the headset doesn't report it) and whether it just changed to worn.
        bool? UpdatePresence(out bool justPutOn)
        {
            justPutOn = false;
            var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!hmd.isValid || !hmd.TryGetFeatureValue(CommonUsages.userPresence, out var present)) return null;
            if (present != router.Present)
            {
                router.SetPresent(present);
                justPutOn = present;
                Debug.Log($"[Fruktstand] headset {(present ? "on" : "off")}");
            }
            return present;
        }

        static void SetFloorOriginAndRecenter()
        {
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems)
            {
                s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                s.TryRecenter();
            }
        }

        void UpdateHead()
        {
            if (head == null) return;
            var device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!device.isValid) return;
            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out var p)) head.localPosition = p;
            if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out var r)) head.localRotation = r;
        }

        float sinceActivate;
        int probesLogged;

        public ShopFrame Read(float dt)
        {
            sinceActivate += dt;
            if (probesLogged < 2 && sinceActivate > (probesLogged == 0 ? 3f : 15f))
            {
                BoundaryProbe.Log($"{sinceActivate:0} s after start (head {head.localPosition:0.00})");
                probesLogged++;
            }
            UpdateHead();
            DetectRecenter(dt);
            var worn = UpdatePresence(out var justPutOn);
            wornSeconds = worn == true ? wornSeconds + dt : 0;
            PlaceStandsIfNeeded(worn, justPutOn);

            var state = new XrState { Left = ReadHand(XRNode.LeftHand), Right = ReadHand(XRNode.RightHand) };
            lastState = state;
            router.Menu.StandOpen = stand.Shop.IsOpen;
            router.Menu.RestBy = stand.Market.Round.RestBy; // EPIC-008/012: which rest, so the menu offers the right way out
            router.Menu.ParentLines = stand.PartyParentLines();
            router.Menu.NatureSound = stand.NatureSound; // EPIC-014
            var wasOpen = router.Menu.IsOpen;
            var frame = router.Route(state, dt, out var action);
            if (router.ClickedHand >= 0)
            {
                // EPIC-012: a pointer or trigger click in the menu: a short flash and a light buzz on that hand.
                menuPanel.Flash(router.Menu.Selected);
                MenuHaptic(router.ClickedHand);
            }
            if (router.Menu.IsOpen != wasOpen) Debug.Log($"[Fruktstand] game menu {(router.Menu.IsOpen ? "opened" : "closed")}");
            if (action != MenuAction.None) Debug.Log($"[Fruktstand] menu action {action}");
            if (action == MenuAction.PlaceInFront) placement.Request();
            Calibrate(action);
            // Seated play (EPIC-005): the rig is turned around the head and walked; the market stays put. The rig's
            // move is passed on so hand speed (throws) is measured without it.
            var rigBefore = transform.localToWorldMatrix;
            if (frame.Turn != 0)
            {
                Locomotion.Turn(transform, head, frame.Turn * XrInputRouter.TurnDegrees);
                Debug.Log($"[Fruktstand] snap turn {frame.Turn * XrInputRouter.TurnDegrees:+0;-0}°");
            }
            if (frame.Walk != Vector2.zero) Locomotion.Walk(transform, head, frame.Walk, dt, stand.ConstrainWalk);
            if (frame.Turn != 0 || frame.Walk != Vector2.zero) frame.RigDelta = transform.localToWorldMatrix * rigBefore.inverse;
            frame.Left = ToWorld(frame.Left);
            frame.Right = ToWorld(frame.Right);
            return frame;
        }

        Vector2 HeadInRoom => new(head.localPosition.x, head.localPosition.z); // tracking space = the real room
        float HeadYawInRoom => head.localEulerAngles.y;

        // EPIC-007: the menu's calibration page. Nothing moves until a confirmation that fits.
        void Calibrate(MenuAction action)
        {
            switch (action)
            {
                case MenuAction.Calibrate:
                    var reading = BoundaryProbe.Log("calibration");
                    Vector2? system = reading.Configured == true && reading.PlayArea is Vector3 p && p.x > 0.5f && p.z > 0.5f ? new Vector2(p.x, p.z) : null;
                    calibration.Start(system, HeadInRoom, HeadYawInRoom);
                    Debug.Log($"[Fruktstand] calibration started ({(system.HasValue ? "system play area" : "manual")}): {string.Join(" / ", calibration.StatusLines())}");
                    break;
                case MenuAction.CalibWidth:
                    calibration.Adjust(CalibItem.Width, router.Menu.AdjustDirection, HeadInRoom, HeadYawInRoom);
                    break;
                case MenuAction.CalibLength:
                    calibration.Adjust(CalibItem.Length, router.Menu.AdjustDirection, HeadInRoom, HeadYawInRoom);
                    break;
                case MenuAction.CalibRotate:
                    calibration.Adjust(CalibItem.Rotate, router.Menu.AdjustDirection, HeadInRoom, HeadYawInRoom);
                    break;
                case MenuAction.CalibHere:
                    calibration.Adjust(CalibItem.Here, 0, HeadInRoom, HeadYawInRoom);
                    break;
                case MenuAction.CalibFace:
                    calibration.Adjust(CalibItem.Face, 0, HeadInRoom, HeadYawInRoom);
                    break;
                case MenuAction.CalibUseSystem:
                    calibration.Adjust(CalibItem.UseSystem, 0, HeadInRoom, HeadYawInRoom);
                    break;
                case MenuAction.CalibConfirm:
                    if (calibration.Confirm() is RoomPlacement placed)
                    {
                        var (position, yaw) = placed.ToWorld(transform);
                        stand.ApplyPlacement(position, yaw, placed.StallDistance);
                        router.Menu.Close();
                        Debug.Log($"[Fruktstand] calibration confirmed: room centre {placed.Centre:0.00}, room yaw {placed.Yaw:0}, stalls {placed.StallDistance:0.00} m");
                    }
                    else Debug.Log($"[Fruktstand] calibration not confirmed: {calibration.Fit.Reason}");
                    break;
                case MenuAction.CalibCancel:
                    calibration.Cancel();
                    break;
            }
            if (calibration.Active && !router.Menu.Calibrating) calibration.Cancel(); // the menu was closed another way
            if (calibration.Active)
            {
                var fit = calibration.Fit;
                var (centre, yaw) = new RoomPlacement(fit.Centre, fit.Yaw, fit.StallDistance).ToWorld(transform);
                stand.ShowPreview(centre, yaw, fit.StallDistance, fit.Fits);
                var (areaCentre, areaYaw) = new RoomPlacement(calibration.Centre, calibration.Yaw, 0).ToWorld(transform);
                stand.ShowAvailableArea(areaCentre, areaYaw, calibration.Size);
            }
            else stand.HidePreview();
        }

        public void AfterStep()
        {
            menuPanel.Show(router.Menu, head);
            for (var h = 0; h < 2; h++)
            {
                var hand = h == 0 ? lastState.Left : lastState.Right;
                var (origin, direction) = PointerRay(hand);
                menuPanel.SetRay(h, router.Menu.IsOpen && router.InputAllowed && hand.Tracked, origin, direction);
            }
            stand.SetStatus(Status());
            AudioListener.pause = !router.InputAllowed; // quiet behind the system menu / while paused
        }

        // EPIC-012: the menu pointer. A ray along the controller's forward (the Oculus plugin's device pose points roughly
        // where the hand points), tilted by PointerPitch if the headset test asks for it.
        public const float PointerPitch = 0f;
        XrState lastState;

        (Vector3 origin, Vector3 direction) PointerRay(in XrHandState hand)
        {
            var rotation = transform.rotation * hand.Rotation * Quaternion.Euler(PointerPitch, 0, 0);
            return (transform.TransformPoint(hand.Position), rotation * Vector3.forward);
        }

        void MenuHaptic(int hand)
        {
            if (!router.InputAllowed) return;
            var device = InputDevices.GetDeviceAtXRNode(hand == 0 ? XRNode.LeftHand : XRNode.RightHand);
            if (device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse) device.SendHapticImpulse(0, 0.25f, 0.03f);
        }

        HandFrame ToWorld(HandFrame h)
        {
            if (!h.Tracked) return h;
            var rotation = transform.rotation * h.Rotation;
            return new HandFrame
            {
                Tracked = true,
                Position = transform.TransformPoint(h.Position) + rotation * handOffset,
                Rotation = rotation,
                Grab = h.Grab,
            };
        }

        static XrHandState ReadHand(XRNode node)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            var s = new XrHandState();
            if (!device.isValid) return s;
            var hasPos = device.TryGetFeatureValue(CommonUsages.devicePosition, out s.Position);
            var hasRot = device.TryGetFeatureValue(CommonUsages.deviceRotation, out s.Rotation);
            s.Tracked = hasPos && hasRot;
            device.TryGetFeatureValue(CommonUsages.gripButton, out s.Grip);
            device.TryGetFeatureValue(CommonUsages.triggerButton, out s.Trigger);
            device.TryGetFeatureValue(CommonUsages.primaryButton, out s.Primary);
            device.TryGetFeatureValue(CommonUsages.secondaryButton, out s.Secondary);
            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out s.Stick);
            device.TryGetFeatureValue(CommonUsages.menuButton, out s.Menu);
            return s;
        }

        void SendHaptic(int hand, float amplitude, float seconds)
        {
            if (!router.PlayActive) return;
            var device = InputDevices.GetDeviceAtXRNode(hand == 0 ? XRNode.LeftHand : XRNode.RightHand);
            if (device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse) device.SendHapticImpulse(0, amplitude, seconds);
        }

        void DetectRecenter(float dt)
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.devicePosition, out var p) || !device.TryGetFeatureValue(CommonUsages.deviceRotation, out var r))
            {
                haveLastHead = false;
                return;
            }
            if (haveLastHead && StandPlacement.LooksLikeRecenter(lastHeadPosition, lastHeadRotation, p, r, dt))
            {
                placement.Request();
                Debug.Log($"[Fruktstand] recenter detected from head pose jump ({Vector3.Distance(lastHeadPosition, p):0.00} m): stands will be placed in front");
            }
            lastHeadPosition = p;
            lastHeadRotation = r;
            haveLastHead = true;
        }

        void PlaceStandsIfNeeded(bool? worn, bool justPutOn)
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            var hasPose = device.isValid && device.TryGetFeatureValue(CommonUsages.devicePosition, out var p) && p != Vector3.zero;
            // The counter height comes from the eye height above the floor, so wait until the floor origin is active.
            var origin = TrackingOriginModeFlags.Unknown;
            foreach (var sub in subsystems) origin = sub.GetTrackingOriginMode();
            hasPose &= origin == TrackingOriginModeFlags.Floor || subsystems.Count == 0;
            var reason = !placement.Placed ? "start" : "recenter/menu";
            if (!placement.ShouldPlace(hasPose, worn, wornSeconds)) return;
            stand.PlaceInFront(head.position, head.forward);
            Debug.Log($"[Fruktstand] stands placed in front ({reason}, worn={worn?.ToString() ?? "unknown"} for {wornSeconds:0.0} s, origin={origin}, eye y={head.position.y:0.00} m), counter at {stand.Stand.Height:0.00} m");
        }

        string Status() =>
            !router.InputAllowed ? "Pausad"
            : router.Menu.IsOpen ? ""
            : router.WaitingForNeutral ? "Släpp knapparna"
            : "";
    }
}
