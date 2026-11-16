// EPIC-012: the menu panel's layout (pure): wrapping to the panel's width, big buttons with gaps (two columns when a
// page has many), and the pointer's hit test.
using System.Collections.Generic;
using System.Linq;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class MenuLayoutTests
    {
        const float LineHeight = 0.022f, CharWidth = 0.011f;

        static MenuLayout Layout(GameMenu m) => MenuLayout.Build(m.Rows(), MenuLayout.PanelWidth, LineHeight, CharWidth);

        [Test]
        public void Layout_WrapsWithinTheWidth()
        {
            var lines = MenuLayout.Wrap("Föräldern har pausat spelet. En vuxen fortsätter det.", 20);
            Assert.Greater(lines.Length, 1);
            foreach (var l in lines) Assert.LessOrEqual(l.Length, 20, l);
            Assert.AreEqual("Föräldern har pausat spelet. En vuxen fortsätter det.", string.Join(" ", lines), "no word lost");
            Assert.AreEqual(new[] { "Kort" }, MenuLayout.Wrap("Kort", 20));
            Assert.IsTrue(MenuLayout.Wrap("Jätteväldigtlångtordutanmellanslag", 10).All(l => l.Length <= 10), "a long word is cut");
        }

        [Test]
        public void Layout_EveryPageFitsAndButtonsDontTouch()
        {
            var m = new GameMenu { BuildLine = "Bygge 2026-11-01_173907_1df05da-dirty", ParentLines = new[] { "Omgången: 79 av 80 pengar, 31 köp, 24 minuter", "Stjärnor i dag: 3" } };
            foreach (var rest in new[] { Fruktstand.Core.RestCause.None, Fruktstand.Core.RestCause.Child, Fruktstand.Core.RestCause.Parent, Fruktstand.Core.RestCause.Party })
            {
                m.RestBy = rest;
                m.Open();
                Check(Layout(m), $"main ({rest})", m.ItemCount);
                m.OpenParent();
                Check(Layout(m), $"parent ({rest})", m.ItemCount);
            }
            m.RestBy = Fruktstand.Core.RestCause.None;
            m.Open();
            m.SelectItem(m.Rows().First(r => r.Text == "Visa kontroller").Item);
            for (var page = 0; page < 3 && m.ShowingControls; page++)
            {
                Check(Layout(m), $"controls {page}", m.ItemCount);
                var next = m.Rows().FirstOrDefault(r => r.Text == "Nästa sida");
                if (next.Text == null) break;
                m.SelectItem(next.Item);
            }
            m.Open();
            m.SelectItem(m.Rows().First(r => r.Text == "Nytt spel").Item);
            Assert.IsTrue(m.Confirming);
            Check(Layout(m), "confirm new game", m.ItemCount);
        }

        static void Check(MenuLayout l, string page, int buttons)
        {
            var boxes = l.Boxes;
            Assert.AreEqual(buttons, boxes.Count(b => b.Kind == RowKind.Button), $"{page}: every button is drawn");
            Assert.LessOrEqual(l.Size.y, MenuLayout.MaxHeight, $"{page}: not taller than can be read seated");
            var maxChars = Mathf.FloorToInt((MenuLayout.PanelWidth - 2 * MenuLayout.Padding) / CharWidth);
            foreach (var b in boxes)
            {
                Assert.GreaterOrEqual(b.Rect.xMin, -l.Size.x / 2 + 1e-4f, $"{page}: {b.Lines[0]} inside left");
                Assert.LessOrEqual(b.Rect.xMax, l.Size.x / 2 - 1e-4f, $"{page}: {b.Lines[0]} inside right");
                Assert.GreaterOrEqual(b.Rect.yMin, -l.Size.y / 2 + 1e-4f, $"{page}: {b.Lines[0]} inside bottom");
                Assert.LessOrEqual(b.Rect.yMax, l.Size.y / 2 - 1e-4f, $"{page}: {b.Lines[0]} inside top");
                var textWidth = b.Lines.Max(t => t.Length) * CharWidth;
                Assert.LessOrEqual(textWidth, b.Rect.width - (b.Kind == RowKind.Button ? 2 * MenuLayout.ButtonPad : 0) + 1e-4f, $"{page}: '{string.Join(" / ", b.Lines)}' fits its box");
                Assert.GreaterOrEqual(b.Rect.height, b.Lines.Length * LineHeight - 1e-4f, $"{page}: tall enough for its lines");
                if (b.Kind == RowKind.Button) Assert.GreaterOrEqual(b.Rect.height, MenuLayout.MinButtonHeight - 1e-4f, $"{page}: a big target");
            }
            var list = boxes.ToList();
            for (var i = 0; i < list.Count; i++)
            for (var j = i + 1; j < list.Count; j++)
            {
                var gap = list[i].Kind == RowKind.Button && list[j].Kind == RowKind.Button ? MenuLayout.Gap : 0;
                var a = list[i].Rect;
                var b = list[j].Rect;
                var apart = a.xMax + gap <= b.xMin + 1e-4f || b.xMax + gap <= a.xMin + 1e-4f || a.yMax + gap <= b.yMin + 1e-4f || b.yMax + gap <= a.yMin + 1e-4f;
                Assert.IsTrue(apart, $"{page}: '{list[i].Lines[0]}' and '{list[j].Lines[0]}' overlap or touch");
            }
        }

        [Test]
        public void Layout_HitFindsTheButton()
        {
            var m = new GameMenu { BuildLine = "" };
            m.Open();
            var l = Layout(m);
            foreach (var b in l.Boxes.Where(b => b.Kind == RowKind.Button))
            {
                Assert.AreEqual(b.Item, l.Hit(b.Rect.center), b.Lines[0]);
                Assert.AreEqual(b.Item, l.Hit(new Vector2(b.Rect.xMin + 0.002f, b.Rect.yMax - 0.002f)), "the whole button is the target");
            }
            var title = l.Boxes.First(b => b.Kind == RowKind.Title);
            Assert.AreEqual(-1, l.Hit(title.Rect.center), "on the panel, not a button");
            Assert.IsNull(l.Hit(new Vector2(l.Size.x, 0)), "beside the panel");
            Assert.AreEqual(2, MenuLayout.ColumnsFor(m.ItemCount), "the main page: two columns of big buttons");
            Assert.AreEqual(1, MenuLayout.ColumnsFor(2), "a confirmation: one column");
        }
    }
}
