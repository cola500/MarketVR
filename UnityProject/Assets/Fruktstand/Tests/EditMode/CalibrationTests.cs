// EPIC-007: the calibration flow (preview, adjust, confirm/cancel) and its menu page. Names match docs/EPIC-007.md.
using System.Linq;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class CalibrationTests
    {
        static readonly Vector2 QuestPlayArea = new(1.71f, 1.86f); // SPIKE-007

        [Test]
        public void Calib_ManualWhenNoData()
        {
            var c = new Calibration();
            c.Start(null, new Vector2(1, 2), 40);
            Assert.IsTrue(c.Active);
            Assert.IsFalse(c.FromSystem, "no system data: manual");
            Assert.AreEqual(new Vector2(1, 2), c.Centre, "centred where the parent stands");
            Assert.AreEqual(40, c.Yaw, 1e-4, "facing where the parent looks");
            Assert.AreEqual(Calibration.ManualDefault, c.Size, "today's assumed 1.5 x 2.0 m");
            Assert.IsTrue(c.Fit.Fits);
            CollectionAssert.DoesNotContain(c.Items, CalibItem.UseSystem, "nothing to use");
        }

        [Test]
        public void Calib_PreviewThenConfirm()
        {
            var c = new Calibration();
            c.Start(QuestPlayArea, new Vector2(-2.3f, -2.5f), 120);
            Assert.IsTrue(c.FromSystem);
            Assert.AreEqual(Vector2.zero, c.Centre, "the system's play area, centred at the tracking origin [A]");
            Assert.AreEqual(0, c.Yaw, 1e-4);
            Assert.AreEqual(QuestPlayArea, c.Size);
            Assert.IsTrue(c.Fit.Fits);
            var placement = c.Confirm();
            Assert.IsTrue(placement.HasValue);
            Assert.AreEqual(0.75f, placement.Value.StallDistance, 1e-4);
            Assert.AreEqual(0, placement.Value.Centre.magnitude, 0.06f);
            Assert.IsFalse(c.Active, "done");

            var d = new Calibration();
            d.Start(QuestPlayArea, Vector2.zero, 0);
            d.Cancel();
            Assert.IsFalse(d.Active);
            Assert.IsFalse(d.Confirm().HasValue, "nothing after cancelling");
        }

        [Test]
        public void Calib_ManualAdjustments()
        {
            var c = new Calibration();
            c.Start(QuestPlayArea, Vector2.zero, 0);
            c.Adjust(CalibItem.Width, +1, Vector2.zero, 0);
            Assert.AreEqual(1.81f, c.Size.x, 1e-4, "wider by 10 cm");
            c.Adjust(CalibItem.Length, -1, Vector2.zero, 0);
            Assert.AreEqual(1.76f, c.Size.y, 1e-4, "shorter by 10 cm");
            c.Adjust(CalibItem.Rotate, +1, Vector2.zero, 0);
            c.Adjust(CalibItem.Rotate, +1, Vector2.zero, 0);
            Assert.AreEqual(30, c.Yaw, 1e-4, "15° a step");
            c.Adjust(CalibItem.Here, 0, new Vector2(0.4f, -0.2f), 77);
            Assert.AreEqual(new Vector2(0.4f, -0.2f), c.Centre, "the middle where I stand");
            Assert.AreEqual(30, c.Yaw, 1e-4, "without turning");
            c.Adjust(CalibItem.Face, 0, new Vector2(9, 9), 77);
            Assert.AreEqual(77, c.Yaw, 1e-4, "turned where I look");
            Assert.AreEqual(new Vector2(0.4f, -0.2f), c.Centre, "without moving");
            Assert.IsFalse(c.FromSystem, "adjusted by hand");
            c.Adjust(CalibItem.UseSystem, 0, Vector2.zero, 0);
            Assert.IsTrue(c.FromSystem);
            Assert.AreEqual(QuestPlayArea, c.Size, "back to the system's play area");
            Assert.AreEqual(Vector2.zero, c.Centre);
            for (var i = 0; i < 100; i++) c.Adjust(CalibItem.Width, -1, Vector2.zero, 0);
            Assert.AreEqual(Calibration.MinSize, c.Size.x, 1e-4, "never below the minimum");
        }

        [Test]
        public void Calib_TooSmallCannotBeConfirmed()
        {
            var c = new Calibration();
            c.Start(null, Vector2.zero, 0);
            for (var i = 0; i < 6; i++) c.Adjust(CalibItem.Width, -1, Vector2.zero, 0); // down to the 1.0 m minimum
            Assert.IsFalse(c.Fit.Fits);
            Assert.IsFalse(c.Confirm().HasValue, "Bekräfta is refused");
            Assert.IsTrue(c.Active, "the preview stays so it can be adjusted");
            StringAssert.Contains("behöver minst", string.Join("\n", c.StatusLines()));
        }

        [Test]
        public void Calib_BoundaryChangeOnlySuggests()
        {
            var c = new Calibration();
            Assert.IsFalse(c.SuggestRecalibration);
            c.BoundaryChanged();
            Assert.IsTrue(c.SuggestRecalibration, "suggests a new calibration");
            Assert.IsFalse(c.Active, "never starts or moves anything by itself");
            Assert.IsFalse(c.Confirm().HasValue);
            c.Start(QuestPlayArea, Vector2.zero, 0);
            Assert.IsFalse(c.SuggestRecalibration, "cleared when the parent calibrates");
        }

        [Test]
        public void Menu_CalibrationPage()
        {
            var cal = new Calibration();
            var m = new GameMenu { Calibration = cal };
            m.Open();
            CollectionAssert.Contains(m.Lines(), "   Rikta in marknaden");
            var main = m.Lines().Where(l => l.StartsWith("> ") || l.StartsWith("   ")).Select(l => l.Substring(2).Trim()).ToList();
            m.Navigate(main.IndexOf("Rikta in marknaden"));
            Assert.AreEqual(MenuAction.Calibrate, m.Select(), "starts the calibration");
            cal.Start(QuestPlayArea, Vector2.zero, 0);
            Assert.IsTrue(m.Calibrating);
            var text = string.Join("\n", m.Lines());
            StringAssert.Contains("Bekräfta", text);
            StringAssert.Contains("Bredd", text);
            StringAssert.Contains("1,7", text, "shows the width");
            StringAssert.Contains("Får plats", text);
            Assert.AreEqual(MenuAction.CalibConfirm, m.Select(), "Bekräfta is the first item");
            Assert.IsTrue(m.IsOpen, "the caller closes it once the confirmation went through");
            m.Navigate(1);
            Assert.AreEqual(MenuAction.CalibWidth, m.Adjust(+1));
            Assert.AreEqual(+1, m.AdjustDirection);
            Assert.AreEqual(MenuAction.None, m.Select(), "selecting a value row does nothing; the stick changes it");
            m.Back();
            Assert.AreEqual(MenuAction.CalibCancel, m.LastAction, "B cancels the calibration");
            Assert.IsFalse(m.Calibrating);
        }

        [Test]
        public void Router_StickSidewaysAdjustsCalibration()
        {
            var r = new XrInputRouter();
            var cal = new Calibration();
            r.Menu.Calibration = cal;
            var idle = new XrState();
            r.Route(idle, 0.01f, out _);
            var menu = idle;
            menu.Left.Menu = true;
            r.Route(menu, 0.01f, out _);
            r.Route(idle, 0.01f, out _);
            r.Menu.OpenCalibration();
            cal.Start(QuestPlayArea, Vector2.zero, 0);
            var down = idle;
            down.Right.Stick = new Vector2(0, -1);
            r.Route(down, 0.01f, out _); // to Bredd
            r.Route(idle, 0.01f, out _);
            var right = idle;
            right.Right.Stick = new Vector2(1, 0);
            r.Route(right, 0.01f, out var action);
            Assert.AreEqual(MenuAction.CalibWidth, action);
            Assert.AreEqual(+1, r.Menu.AdjustDirection);
            r.Route(right, 0.01f, out action);
            Assert.AreEqual(MenuAction.None, action, "one step per flick");
        }
    }
}
