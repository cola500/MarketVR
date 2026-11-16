// EPIC-012 A10: every menu page, with its longest texts, fits its panel in the real scene with the real font:
// each line inside its box, every box inside the panel, nothing overlapping. Names match docs/EPIC-012.md.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fruktstand.Tests
{
    public class MenuPanelSceneTests
    {
        FruitStand s;
        MenuPanel panel;
        Transform head;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneLoad.Fresh(x => s = x);
            panel = Object.FindAnyObjectByType<MenuPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "the rig has a menu panel");
            head = new GameObject("Test Head").transform;
            head.position = s.Stand.ToWorld(new Vector3(0, 0.4f, -0.4f));
            head.rotation = Quaternion.LookRotation(s.Stand.Top.forward);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (head != null) Object.Destroy(head.gameObject);
            yield return null;
        }

        // Every page the player or the parent can see, with the longest texts we know of.
        public static IEnumerable<(string name, GameMenu menu)> Pages()
        {
            GameMenu M(RestCause rest) => new()
            {
                RestBy = rest,
                BuildLine = "Bygge 2026-11-01_173907_1df05da-dirty",
                ParentLines = new[] { "Omgången: 79 av 80 pengar, 31 köp, 24 minuter", "Stjärnor i dag: 3" },
            };
            foreach (var rest in new[] { RestCause.None, RestCause.Child, RestCause.Parent, RestCause.Party })
            {
                var m = M(rest);
                m.Open();
                yield return ($"main-{rest}", m);
                var p = M(rest);
                p.OpenParent();
                yield return ($"parent-{rest}", p);
            }
            var c = M(RestCause.None);
            c.Open();
            c.SelectItem(c.Rows().First(r => r.Text == "Visa kontroller").Item);
            var page = 1;
            while (true)
            {
                var copy = M(RestCause.None);
                copy.Open();
                copy.SelectItem(copy.Rows().First(r => r.Text == "Visa kontroller").Item);
                for (var k = 1; k < page; k++) copy.SelectItem(copy.Rows().First(r => r.Text == "Nästa sida").Item);
                yield return ($"controls-{page}", copy);
                if (!copy.Rows().Any(r => r.Text == "Nästa sida")) break;
                page++;
            }
            var n = M(RestCause.None);
            n.Open();
            n.SelectItem(n.Rows().First(r => r.Text == "Nytt spel").Item);
            yield return ("confirm-new-game", n);
            var r2 = M(RestCause.Party);
            r2.OpenParent();
            r2.SelectItem(r2.Rows().First(r => r.Text == "Starta nästa omgång").Item);
            yield return ("confirm-next-round", r2);
            var cal = M(RestCause.None);
            cal.Calibration = new Calibration();
            cal.OpenCalibration();
            yield return ("calibrate", cal);
        }

        [UnityTest]
        public IEnumerator Scene_MenuPagesFitTheirPanel()
        {
            foreach (var (name, menu) in Pages())
            {
                panel.Show(new GameMenu(), head); // closed: placed again for the next page
                panel.Show(menu, head);
                yield return null;
                var size = panel.Layout.Size;
                Assert.LessOrEqual(size.y, MenuLayout.MaxHeight, $"{name}: short enough to read seated");
                var rows = panel.transform.Find("Rows");
                Assert.IsNotNull(rows, name);
                var texts = rows.GetComponentsInChildren<TextMesh>();
                Assert.Greater(texts.Length, 0, name);
                foreach (var t in texts)
                {
                    var b = t.GetComponent<MeshRenderer>().bounds;
                    Assert.Greater(b.size.x, 0, $"{name}: '{t.text}' is drawn with real glyphs");
                    var box = BoxOf(t.name);
                    foreach (var corner in Corners(b))
                    {
                        var local = panel.transform.InverseTransformPoint(corner);
                        Assert.LessOrEqual(Mathf.Abs(local.x), size.x / 2 + 1e-3f, $"{name}: '{t.text}' inside the panel sideways");
                        Assert.LessOrEqual(Mathf.Abs(local.y), size.y / 2 + 1e-3f, $"{name}: '{t.text}' inside the panel up/down");
                        if (box.Kind == RowKind.Button)
                        {
                            Assert.GreaterOrEqual(local.x, box.Rect.xMin + 1e-3f, $"{name}: '{t.text}' inside its button (left)");
                            Assert.LessOrEqual(local.x, box.Rect.xMax - 1e-3f, $"{name}: '{t.text}' inside its button (right)");
                            Assert.GreaterOrEqual(local.y, box.Rect.yMin - 1e-3f, $"{name}: '{t.text}' inside its button (bottom)");
                            Assert.LessOrEqual(local.y, box.Rect.yMax + 1e-3f, $"{name}: '{t.text}' inside its button (top)");
                        }
                    }
                }
                var lines = texts.Select(t => (t, b: t.GetComponent<MeshRenderer>().bounds)).ToList();
                for (var i = 0; i < lines.Count; i++)
                for (var j = i + 1; j < lines.Count; j++)
                {
                    var a = Flat(lines[i].b);
                    var c = Flat(lines[j].b);
                    Assert.IsFalse(a.Overlaps(c) && Area(Intersection(a, c)) > 2e-5f, $"{name}: '{lines[i].t.text}' and '{lines[j].t.text}' overlap");
                }
            }
            panel.Show(new GameMenu(), head);
        }

        MenuLayout.Box BoxOf(string rowName)
        {
            var n = int.Parse(rowName.Split(' ')[1]);
            return panel.Layout.Boxes[n - 1];
        }

        Rect Flat(Bounds b)
        {
            var min = panel.transform.InverseTransformPoint(b.min);
            var max = panel.transform.InverseTransformPoint(b.max);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        static Rect Intersection(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));
        static float Area(Rect r) => Mathf.Max(0, r.width) * Mathf.Max(0, r.height);

        static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (var i = 0; i < 8; i++)
                yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
        }
    }
}
