using System;
using System.Collections.Generic;
using System.Linq;
using OpenSekai.MaterialPacks;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Sekai
{
    public sealed class LiveBackgroundSkin : LiveSpriteSkin
    {
        private readonly MaterialBackgroundScene _scene;

        public LiveBackgroundSkin(MaterialBackgroundScene scene) => _scene = scene;
        internal bool PreferVideo => _scene.PreferVideo;
        internal bool UsesJacket => _scene.Layers.Any(layer => layer.Type == "jacket");

        internal LiveBackgroundComposition Create(Camera reference, Transform parent, Material material, Texture2D jacket)
        {
            var composition = new LiveBackgroundComposition(reference, parent, material);
            for (int i = 0; i < _scene.Layers.Length; i++)
            {
                var layer = _scene.Layers[i];
                string key = MaterialBackgroundScene.LayerKey(i);
                var masks = layer.Masks?.Select((m, j) => Resolve(MaterialBackgroundScene.MaskKey(i, j), null)?.Sprite).ToArray();
                if (masks != null && masks.Any(mask => mask == null)) continue;
                Sprite sprite = layer.Type == "jacket" ? composition.JacketSprite(jacket, layer.Pivot) : Resolve(key, null)?.Sprite;
                if (sprite != null) composition.Add(key, i, layer, sprite, masks);
            }
            return composition;
        }
    }

    internal sealed class LiveBackgroundComposition : IDisposable
    {
        private readonly GameObject _root;
        private readonly Camera _camera;
        private readonly int _layer;
        private readonly Material _material;
        private readonly List<(Transform transform, MaterialBackgroundLayer layout, Sprite sprite)> _planes = new();
        private readonly Dictionary<Vector2, Sprite> _jackets = new();
        private bool _needsMaskWarmup;
        internal bool HasVisibleLayers => _planes.Count > 0;

        internal LiveBackgroundComposition(Camera reference, Transform parent, Material material)
        {
            _root = new GameObject("Background layers");
            _root.transform.SetParent(parent, false);
            _camera = Object.Instantiate(reference, _root.transform, false);
            _camera.enabled = false;
            _camera.tag = "Untagged";
            _layer = reference.gameObject.layer;
            _material = material;
        }

        internal Sprite JacketSprite(Texture2D texture, float[] pivot)
        {
            if (texture == null) return null;
            var anchor = pivot == null ? Vector2.one * .5f : new Vector2(pivot[0], pivot[1]);
            if (_jackets.TryGetValue(anchor, out var sprite)) return sprite;
            int side = Mathf.Min(texture.width, texture.height);
            var rect = new Rect((texture.width - side) * .5f, (texture.height - side) * .5f, side, side);
            sprite = Sprite.Create(texture, rect, anchor, MaterialGlyph.REFERENCE_PPU, 0, SpriteMeshType.FullRect);
            _jackets.Add(anchor, sprite);
            return sprite;
        }

        internal void Add(string key, int index, MaterialBackgroundLayer layer, Sprite sprite, Sprite[] masks)
        {
            var group = new GameObject(key) { layer = _layer }.AddComponent<SortingGroup>();
            group.transform.SetParent(_root.transform, false);
            group.sortingOrder = index;
            var renderer = Plane(group.transform, key, layer, sprite).AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 3 * index + 2;
            renderer.sharedMaterial = _material;
            renderer.sprite = sprite;
            renderer.flipX = layer.FlipX;
            renderer.flipY = layer.FlipY;
            if (layer.Color != null) renderer.color = new Color(layer.Color[0], layer.Color[1], layer.Color[2], layer.Color[3]);
            if (masks == null) return;
            _needsMaskWarmup = true;
            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            for (int i = 0; i < masks.Length; i++)
            {
                var mask = Plane(group.transform, key + ".mask", layer.Masks[i], masks[i]).AddComponent<SpriteMask>();
                mask.isCustomRangeActive = true;
                mask.backSortingOrder = renderer.sortingOrder - 1;
                mask.frontSortingOrder = renderer.sortingOrder + 1;
                mask.sprite = masks[i];
                mask.alphaCutoff = layer.Masks[i].Cutoff;
            }
        }

        private GameObject Plane(Transform parent, string name, MaterialBackgroundLayer layout, Sprite sprite)
        {
            var plane = new GameObject(name) { layer = _layer };
            plane.transform.SetParent(parent, false);
            _planes.Add((plane.transform, layout, sprite));
            return plane;
        }

        internal void Render(RenderTexture target)
        {
            _camera.targetTexture = target;
            _camera.aspect = (float)target.width / target.height;
            _camera.ResetProjectionMatrix();
            float distance = -_camera.transform.localPosition.z;
            float height = 2f * distance * Mathf.Tan(.5f * _camera.fieldOfView * Mathf.Deg2Rad);
            float units = height / 1080f;
            foreach (var (plane, layout, sprite) in _planes)
            {
                var box = layout.Size == null ? new Vector2(height * _camera.aspect, height) : Vector(layout.Size) * units;
                if (layout.Fit != null || layout.Size == null)
                {
                    Vector2 ratio = box / sprite.rect.size;
                    box = sprite.rect.size * (layout.Fit == "contain" ? Mathf.Min(ratio.x, ratio.y) : Mathf.Max(ratio.x, ratio.y));
                }
                if (layout.Scale != null) box *= Vector(layout.Scale);
                Vector2 scale = box * sprite.pixelsPerUnit / sprite.rect.size;
                plane.localScale = new Vector3(scale.x, scale.y, 1);
                Vector2 pos = layout.Position == null ? Vector2.zero : Vector(layout.Position);
                plane.localPosition = new Vector3(pos.x, pos.y, layout.Depth) * units;
                if (layout.Rotation != null) plane.localRotation = Quaternion.Euler(layout.Rotation[0], layout.Rotation[1], layout.Rotation[2]);
            }
            SortingGroup.UpdateAllSortingGroups();
            if (_needsMaskWarmup)
            {
                // A cold camera draw can miss the sprite stencil masks. Prime them before
                // baking the texture that the live scene will sample.
                _camera.Render();
                _needsMaskWarmup = false;
            }
            _camera.Render();
        }

        private static Vector2 Vector(float[] pair) => new(pair[0], pair[1]);

        public void Dispose()
        {
            if (_root != null)
            {
                _root.SetActive(false);
                if (_camera != null) _camera.targetTexture = null;
                Object.Destroy(_root);
            }
            foreach (var sprite in _jackets.Values) Object.Destroy(sprite);
        }
    }
}
