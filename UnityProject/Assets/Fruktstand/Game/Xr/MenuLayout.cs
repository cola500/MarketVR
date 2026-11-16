// EPIC-012: where the rows of a menu page go on the panel (pure, unit-tested). Panel space: metres, x right, y up,
// origin at the panel's centre. Text is wrapped to its box (never shrunk); buttons are big targets with a gap between
// them, in two columns when a page has many, so the panel stays short enough to read seated. Hit() is the pointer's
// test: a button number, -1 on the panel between buttons, null off the panel.
using System.Collections.Generic;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class MenuLayout
    {
        public const float PanelWidth = 0.62f;
        public const float Padding = 0.03f;
        public const float Gap = 0.014f;           // between buttons
        public const float ButtonPad = 0.014f;     // text inside a button
        public const float MinButtonHeight = 0.05f; // a comfortable target at arm's length
        public const float MaxHeight = 0.62f;      // taller than this is hard to read seated at the panel's distance
        public const int TwoColumnsFrom = 7;       // buttons on a page

        public readonly struct Box
        {
            public Box(RowKind kind, int item, Rect rect, string[] lines, RowStyle style, bool selected)
            {
                Kind = kind;
                Item = item;
                Rect = rect;
                Lines = lines;
                Style = style;
                Selected = selected;
            }

            public RowKind Kind { get; }
            public int Item { get; }
            public Rect Rect { get; }
            public string[] Lines { get; }
            public RowStyle Style { get; }
            public bool Selected { get; }
        }

        readonly List<Box> boxes = new();
        public IReadOnlyList<Box> Boxes => boxes;
        public Vector2 Size { get; private set; }
        public float LineHeight { get; private set; }

        public static int ColumnsFor(int buttons) => buttons >= TwoColumnsFrom ? 2 : 1;

        public static MenuLayout Build(IReadOnlyList<MenuRow> rows, float width, float lineHeight, float charWidth)
        {
            var l = new MenuLayout { LineHeight = lineHeight };
            var inner = width - 2 * Padding;
            var placed = new List<(RowKind kind, int item, float x, float top, float w, float h, string[] lines, RowStyle style, bool selected)>();
            var y = Padding; // from the top, downwards
            var i = 0;
            while (i < rows.Count)
            {
                var r = rows[i];
                if (r.Kind != RowKind.Button)
                {
                    var lines = Wrap(r.Text, Mathf.Max(1, Mathf.FloorToInt(inner / charWidth)));
                    var h = lines.Length * lineHeight * (r.Kind == RowKind.Title ? 1.25f : 1.1f);
                    placed.Add((r.Kind, -1, -inner / 2, y, inner, h, lines, r.Style, false));
                    y += h + lineHeight * 0.4f;
                    i++;
                    continue;
                }
                // a run of buttons: a grid, filled row by row
                var run = new List<MenuRow>();
                while (i < rows.Count && rows[i].Kind == RowKind.Button) run.Add(rows[i++]);
                var columns = ColumnsFor(run.Count);
                var bw = (inner - (columns - 1) * Gap) / columns;
                var chars = Mathf.Max(1, Mathf.FloorToInt((bw - 2 * ButtonPad) / charWidth));
                y += lineHeight * 0.2f;
                for (var k = 0; k < run.Count; k += columns)
                {
                    var rowLines = new List<string[]>();
                    var rowHeight = MinButtonHeight;
                    for (var c = 0; c < columns && k + c < run.Count; c++)
                    {
                        var lines = Wrap(run[k + c].Text, chars);
                        rowLines.Add(lines);
                        rowHeight = Mathf.Max(rowHeight, lines.Length * lineHeight + 2 * ButtonPad);
                    }
                    for (var c = 0; c < rowLines.Count; c++)
                    {
                        var b = run[k + c];
                        placed.Add((RowKind.Button, b.Item, -inner / 2 + c * (bw + Gap), y, bw, rowHeight, rowLines[c], b.Style, b.Selected));
                    }
                    y += rowHeight + Gap;
                }
                y += lineHeight * 0.2f;
            }
            var height = y + Padding;
            l.Size = new Vector2(width, height);
            foreach (var p in placed)
                l.boxes.Add(new Box(p.kind, p.item, new Rect(p.x, height / 2 - p.top - p.h, p.w, p.h), p.lines, p.style, p.selected));
            return l;
        }

        public int? Hit(Vector2 local)
        {
            if (Mathf.Abs(local.x) > Size.x / 2 || Mathf.Abs(local.y) > Size.y / 2) return null;
            foreach (var b in boxes)
                if (b.Kind == RowKind.Button && b.Rect.Contains(local)) return b.Item;
            return -1;
        }

        // Greedy word wrap to at most maxChars per line; a word longer than a line is cut.
        public static string[] Wrap(string text, int maxChars)
        {
            var lines = new List<string>();
            var line = "";
            foreach (var raw in (text ?? "").Split(' '))
            {
                var word = raw;
                while (word.Length > maxChars)
                {
                    if (line.Length > 0)
                    {
                        lines.Add(line);
                        line = "";
                    }
                    lines.Add(word.Substring(0, maxChars));
                    word = word.Substring(maxChars);
                }
                if (line.Length == 0) line = word;
                else if (line.Length + 1 + word.Length <= maxChars) line += " " + word;
                else
                {
                    lines.Add(line);
                    line = word;
                }
            }
            if (line.Length > 0 || lines.Count == 0) lines.Add(line);
            return lines.ToArray();
        }
    }
}
