// Draws the game menu as a world-space panel in front of the player. Placed when the menu opens and then left
// still (not head-locked), so it is comfortable to read. From Magic Tiles.
// EPIC-012: the page is laid out by MenuLayout (wrapped text, big buttons with gaps, two columns for long pages);
// the panel grows with its content. Rays from the controllers (drawn here) hit buttons via Hit(); the highlighted
// button is yellow, a click flashes it. Text is measured once from the font, never shrunk to fit.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fruktstand.Game
{
    public sealed class MenuPanel : MonoBehaviour
    {
        const float Distance = 0.7f;
        const float BelowEyes = 0.14f;  // a little below the eyes: comfortable seated
        const float LineHeight = 0.022f; // ≈ 1.8° at the panel's distance
        const float RayLength = 2f;
        const float FlashSeconds = 0.18f;

        static readonly Color Background = new(0.97f, 0.96f, 0.92f);
        static readonly Color ButtonColour = new(0.84f, 0.89f, 0.97f);
        static readonly Color GoColour = new(0.72f, 0.9f, 0.72f);
        static readonly Color DangerColour = new(0.97f, 0.72f, 0.7f);
        static readonly Color MutedColour = new(0.9f, 0.9f, 0.9f);
        static readonly Color SelectedColour = new(1f, 0.85f, 0.25f);
        static readonly Color FlashColour = Color.white;
        static readonly Color Ink = new(0.12f, 0.12f, 0.16f);
        static readonly Color MutedInk = new(0.45f, 0.45f, 0.5f);
        static readonly Color RayColour = new(1f, 0.92f, 0.5f);

        [SerializeField] TextMesh text; // the template: font, size and material for every row
        [SerializeField] Material overlay;     // EPIC-012: Fruktstand/MenuOverlay, drawn on top of the world
        [SerializeField] Material overlayText; // the same shader for TextMesh text (alpha from the font atlas)
        Material textMaterial;
        bool shown;
        Transform background, content;
        MenuLayout layout;
        string drawn;
        float characterSize = -1, charWidth;
        readonly Dictionary<Color, Material> materials = new();
        readonly Transform[] rays = new Transform[2];
        readonly Transform[] dots = new Transform[2];
        int flashItem = -1;
        float flashUntil;

        public MenuLayout Layout => layout;
        public float CharWidth => charWidth;
        public float LineHeightMetres => LineHeight;

        public void Show(GameMenu menu, Transform head)
        {
            if (!menu.IsOpen)
            {
                if (shown) gameObject.SetActive(false);
                shown = false;
                drawn = null;
                return;
            }
            if (!shown)
            {
                var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
                forward.Normalize();
                transform.SetPositionAndRotation(head.position + forward * Distance + Vector3.down * BelowEyes, Quaternion.LookRotation(forward));
                gameObject.SetActive(true);
                shown = true;
            }
            Draw(menu);
        }

        // Builds the page again when its rows changed (text, selection, flash).
        void Draw(GameMenu menu)
        {
            Measure();
            var rows = menu.Rows();
            var flashing = Time.unscaledTime < flashUntil ? flashItem : -1;
            var key = flashing + "|" + string.Join("|", System.Linq.Enumerable.Select(rows, r => $"{r.Kind}{r.Item}{r.Selected}{r.Style}{r.Text}"));
            if (key == drawn) return;
            drawn = key;
            layout = MenuLayout.Build(rows, MenuLayout.PanelWidth, LineHeight, charWidth);

            text.text = ""; // the template draws nothing itself
            if (content != null)
            {
                content.gameObject.SetActive(false); // hidden now, destroyed at the end of the frame
                Destroy(content.gameObject);
            }
            content = new GameObject("Rows").transform;
            content.SetParent(transform, false);
            if (background == null) background = transform.Find("Panel");
            if (background != null)
            {
                background.localPosition = new Vector3(0, 0, 0.004f);
                background.localScale = new Vector3(layout.Size.x, layout.Size.y, 1);
                if (overlay != null) background.GetComponent<MeshRenderer>().sharedMaterial = Mat(Background, 4000);
            }
            var n = 0;
            foreach (var b in layout.Boxes)
            {
                n++;
                if (b.Kind == RowKind.Button)
                {
                    var colour = b.Item == flashing ? FlashColour : b.Selected ? SelectedColour : b.Style switch
                    {
                        RowStyle.Go => GoColour,
                        RowStyle.Danger => DangerColour,
                        RowStyle.Muted => MutedColour,
                        _ => ButtonColour,
                    };
                    Quad($"Row {n} Button", b.Rect, 0.002f, colour);
                    if (b.Selected) Frame($"Row {n} Frame", b.Rect, 0.003f, new Color(0.75f, 0.55f, 0f));
                }
                var lineStep = LineHeight * (b.Kind == RowKind.Title ? 1.25f : 1.1f);
                var blockHeight = b.Lines.Length * LineHeight;
                var top = b.Kind == RowKind.Button ? b.Rect.center.y + blockHeight / 2 : b.Rect.yMax;
                for (var i = 0; i < b.Lines.Length; i++)
                {
                    if (string.IsNullOrEmpty(b.Lines[i])) continue; // a blank line is only space
                    var x = b.Kind == RowKind.Title ? b.Rect.center.x : b.Kind == RowKind.Button ? b.Rect.xMin + MenuLayout.ButtonPad : b.Rect.xMin;
                    var y = top - (b.Kind == RowKind.Button ? LineHeight : lineStep) * i;
                    var t = Text($"Row {n} Text", b.Lines[i], new Vector3(x, y, 0), b.Kind == RowKind.Title ? TextAnchor.UpperCenter : TextAnchor.UpperLeft,
                        b.Style == RowStyle.Muted && b.Kind != RowKind.Button ? MutedInk : Ink, b.Kind == RowKind.Title ? 1.2f : 1f);
                    if (b.Kind == RowKind.Title) t.fontStyle = FontStyle.Bold;
                }
            }
        }

        // The pointer: which button a ray from `origin` along `direction` hits (world space); -1 on the panel between
        // buttons; null off it (or behind, or too far).
        public int? Hit(Vector3 origin, Vector3 direction)
        {
            if (!shown || layout == null) return null;
            var normal = transform.forward; // the panel faces away from the player (TextMesh reads that way)
            var denom = Vector3.Dot(direction, normal);
            if (denom < 1e-3f) return null;
            var t = Vector3.Dot(transform.position - origin, normal) / denom;
            if (t < 0 || t > RayLength) return null;
            var local = transform.InverseTransformPoint(origin + direction * t);
            return layout.Hit(new Vector2(local.x, local.y));
        }

        // Draws a hand's ray, ending on the panel when it hits it.
        public void SetRay(int hand, bool visible, Vector3 origin, Vector3 direction)
        {
            if (rays[hand] == null)
            {
                rays[hand] = Cube($"Ray {hand}", RayColour, null, 3000);
                dots[hand] = Cube($"Ray Dot {hand}", SelectedColour, null, 4004);
            }
            var show = visible && shown;
            rays[hand].gameObject.SetActive(show);
            dots[hand].gameObject.SetActive(false);
            if (!show) return;
            var length = RayLength;
            var normal = transform.forward;
            var denom = Vector3.Dot(direction, normal);
            if (denom > 1e-3f)
            {
                var t = Vector3.Dot(transform.position - origin, normal) / denom;
                var local = transform.InverseTransformPoint(origin + direction * t);
                if (t > 0 && t < RayLength && layout != null && layout.Hit(new Vector2(local.x, local.y)) != null)
                {
                    length = t;
                    dots[hand].gameObject.SetActive(true);
                    dots[hand].SetPositionAndRotation(origin + direction * t, transform.rotation);
                    dots[hand].localScale = new Vector3(0.012f, 0.012f, 0.002f);
                }
            }
            rays[hand].SetPositionAndRotation(origin + direction * (length / 2), Quaternion.LookRotation(direction));
            rays[hand].localScale = new Vector3(0.003f, 0.003f, length);
        }

        public void Flash(int item)
        {
            flashItem = item;
            flashUntil = Time.unscaledTime + FlashSeconds;
        }

        // ---- drawing helpers ----

        // The font is measured once: characterSize so a line is LineHeight tall, and the widest common character.
        void Measure()
        {
            if (characterSize > 0) return;
            var probe = new GameObject("Probe").AddComponent<TextMesh>();
            probe.transform.SetParent(transform, false);
            probe.font = text.font;
            probe.fontSize = text.fontSize;
            probe.characterSize = 1;
            probe.anchor = TextAnchor.UpperLeft;
            probe.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            probe.text = "Åg\nÅg";
            var two = probe.GetComponent<MeshRenderer>().bounds.size.y;
            probe.text = "Åg";
            var one = probe.GetComponent<MeshRenderer>().bounds.size.y;
            var advance = two - one;
            const string sample = "Starta nästa omgång Återställ beställningen Höger spak MW";
            probe.text = sample;
            var width = probe.GetComponent<MeshRenderer>().bounds.size.x / sample.Length;
            Destroy(probe.gameObject);
            if (advance > 1e-4f && width > 1e-5f)
            {
                characterSize = LineHeight / advance;
                charWidth = width * characterSize * 1.08f; // a little margin for wide letters
            }
            else
            {
                characterSize = 0.0033f; // no font metrics (batch mode without glyphs): the 2026 look
                charWidth = 0.0115f;
            }
        }

        TextMesh Text(string name, string value, Vector3 position, TextAnchor anchor, Color colour, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(content, false);
            go.transform.localPosition = position + new Vector3(0, 0, -0.001f);
            var t = go.AddComponent<TextMesh>();
            t.font = text.font;
            t.fontSize = text.fontSize;
            t.characterSize = characterSize * scale;
            t.anchor = anchor;
            t.alignment = TextAlignment.Left;
            t.color = colour;
            t.text = value;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = TextMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;
            return t;
        }

        // Text on top of the buttons, which are on top of the panel, which is on top of the world.
        Material TextMaterial()
        {
            if (textMaterial != null) return textMaterial;
            if (overlayText == null) return textMaterial = text.GetComponent<MeshRenderer>().sharedMaterial;
            textMaterial = new Material(overlayText) { mainTexture = text.font.material.mainTexture, renderQueue = 4003 };
            return textMaterial;
        }

        void Quad(string name, Rect rect, float behind, Color colour)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = name;
            q.transform.SetParent(content, false);
            q.transform.localPosition = new Vector3(rect.center.x, rect.center.y, behind);
            q.transform.localScale = new Vector3(rect.width, rect.height, 1);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = Mat(colour);
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        void Frame(string name, Rect rect, float behind, Color colour)
        {
            const float w = 0.004f;
            var grown = new Rect(rect.x - w, rect.y - w, rect.width + 2 * w, rect.height + 2 * w);
            Quad(name, grown, behind, colour);
        }

        Transform Cube(string name, Color colour, Transform parent, int queue)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(c.GetComponent<Collider>());
            c.name = name;
            c.transform.SetParent(parent, false);
            var r = c.GetComponent<MeshRenderer>();
            r.sharedMaterial = Mat(colour, queue);
            r.shadowCastingMode = ShadowCastingMode.Off;
            c.SetActive(false);
            return c.transform;
        }

        Material Mat(Color colour, int queue = 4001)
        {
            var key = new Color(colour.r, colour.g, colour.b, queue / 10000f);
            if (materials.TryGetValue(key, out var m)) return m;
            var template = overlay != null ? overlay : background != null ? background.GetComponent<MeshRenderer>().sharedMaterial : null;
            m = template != null ? new Material(template) : new Material(Shader.Find("Unlit/Color"));
            m.color = colour;
            if (overlay != null) m.renderQueue = queue;
            materials[key] = m;
            return m;
        }

        void OnDestroy()
        {
            foreach (var r in rays) if (r != null) Destroy(r.gameObject);
            foreach (var d in dots) if (d != null) Destroy(d.gameObject);
        }
    }
}
