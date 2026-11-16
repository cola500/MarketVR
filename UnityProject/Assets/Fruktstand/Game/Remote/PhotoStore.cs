// The phone customer's photo (EPIC-004), in memory only for this app session: never written to disk.
// The phone has already cropped and scaled it; it arrives once as raw RGBA rows (top row first, as a browser canvas
// gives them), so the Quest decodes no image file format. Strict limits: square, 16..128 px, exactly w*h*4 bytes.
// At most a few photos are kept; older textures are destroyed.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class PhotoStore
    {
        public const int MaxSide = 128;
        public const int MinSide = 16;
        public const int MaxPhotos = 3;

        readonly Dictionary<string, Texture2D> textures = new();
        readonly List<string> order = new();

        public int Count => textures.Count;
        public Texture2D Get(string id) => id != null && textures.TryGetValue(id, out var t) ? t : null;
        public string[] Ids() => order.ToArray();

        public bool TryAdd(string id, int w, int h, string base64)
        {
            if (!Core.RemoteCustomer.ValidId(id)) return false;
            if (textures.ContainsKey(id)) return true; // already have it: a resend changes nothing
            if (w != h || w < MinSide || w > MaxSide || base64 == null) return false;
            var expected = w * h * 4;
            if (base64.Length != (expected + 2) / 3 * 4) return false; // checked before decoding anything
            byte[] rgba;
            try { rgba = Convert.FromBase64String(base64); }
            catch (FormatException) { return false; }
            if (rgba.Length != expected) return false;

            // Canvas rows go top -> bottom, texture rows bottom -> top.
            var flipped = new byte[expected];
            var row = w * 4;
            for (var y = 0; y < h; y++) Buffer.BlockCopy(rgba, y * row, flipped, (h - 1 - y) * row, row);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Phone photo", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.LoadRawTextureData(flipped);
            tex.Apply(false, false); // kept readable (64 kB) so tests and renders can check the pixels

            textures[id] = tex;
            order.Add(id);
            while (order.Count > MaxPhotos)
            {
                var old = order[0];
                order.RemoveAt(0);
                if (textures.TryGetValue(old, out var t)) Discard(t);
                textures.Remove(old);
            }
            return true;
        }

        static void Discard(Texture2D t)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(t);
            else UnityEngine.Object.DestroyImmediate(t);
        }

        public void Clear()
        {
            foreach (var t in textures.Values) Discard(t);
            textures.Clear();
            order.Clear();
        }
    }
}
