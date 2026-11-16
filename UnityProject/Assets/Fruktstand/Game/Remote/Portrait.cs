// The phone customer's photo as a simple framed portrait in front of the customer figure's face (EPIC-004).
// No 3D avatar, no face animation: a flat picture with a white frame, facing the player like the face does.
// Without a photo nothing is shown and the usual figure stays as it is.
using System;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class Portrait
    {
        public const float Size = 0.22f; // a little larger than the head (0.26 sphere seen from the front)
        static readonly Vector3 FaceCentre = new(0, 0.97f, -0.145f); // figure space, just in front of the head

        readonly Art art;
        readonly Material baseMaterial;
        readonly Func<Texture2D> photo;
        Transform root;
        Transform body;
        Material material;
        Texture2D shown;

        public Portrait(Art art, Material photoMaterial, Func<Texture2D> currentPhoto)
        {
            this.art = art;
            baseMaterial = photoMaterial;
            photo = currentPhoto;
        }

        public bool Visible => root != null && root.gameObject.activeSelf;
        public Texture2D Shown => Visible ? shown : null;
        public Transform Root => root;

        // A new figure was built: the portrait belongs to its body (so it moves, hops and scales with it).
        public void Attach(Transform figureBody)
        {
            if (material == null)
            {
                material = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("Unlit/Texture"));
                material.name = "Phone photo";
            }
            body = figureBody;
            root = art.Group(figureBody, "Portrait", FaceCentre);
            art.Part(root, "Frame", art.Quad, art.Unlit(Color.white), new Vector3(0, 0, 0.002f), Vector3.one * (Size + 0.025f), null, false);
            art.Part(root, "Photo", art.Quad, material, Vector3.zero, Vector3.one * Size, null, false);
            root.gameObject.SetActive(false);
            shown = null;
        }

        public void Show(bool humanCustomer)
        {
            if (root == null) return;
            var tex = humanCustomer ? photo?.Invoke() : null;
            if (tex != shown)
            {
                shown = tex;
                material.mainTexture = tex;
            }
            if (root.gameObject.activeSelf == (tex != null)) return;
            root.gameObject.SetActive(tex != null);
            // A hat would stick up behind the picture: hidden while the portrait shows.
            foreach (Transform part in body)
                if (part.name is "Brim" or "Crown") part.gameObject.SetActive(tex == null);
        }
    }
}
