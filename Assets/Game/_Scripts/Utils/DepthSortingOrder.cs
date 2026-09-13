using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Sorts a character along the vertical axis: the lower on screen it stands, the closer to the
    /// camera it draws. Replaces the tie-break race that happens when several units share one
    /// sorting order and Unity is free to draw them in any order (which is why creatures
    /// occasionally swapped depth mid-fight).
    ///
    /// Works for both of this project's visual kinds without configuration — it resolves its own
    /// target in Awake(): a Spine <see cref="SkeletonAnimation"/>'s renderer if there is one (every
    /// prefab under _Prefabs/Characters is Spine-based), otherwise a plain SpriteRenderer, otherwise
    /// any Renderer. Put it on the prefab ROOT, not on the Visual/Character child: depth must come
    /// from where the unit stands (its slot), not from a visual child that animates/bobs or sits at
    /// an authored offset (floating creatures would otherwise sort as if they stood further back).
    ///
    /// It drives the whole visual group, not just the main renderer — every Renderer under the main
    /// renderer's own parent that shares its sorting layer, each keeping the offset it was authored
    /// with. That is what keeps a unit's Shadow behind its own body: without it, shadows would keep
    /// their static order (1) while bodies moved to large negative orders, and every shadow would
    /// draw on top of every creature. The two filters (same parent + same sorting layer) are what
    /// keep the ~22 status/hit ParticleSystemRenderers on the Shield/UI layers out of it — those
    /// live outside the visual container and are deliberately ordered by StatusesManager.
    /// </summary>
    [DisallowMultipleComponent]
    public class DepthSortingOrder : MonoBehaviour
    {
        [Tooltip("Multiplier turning world Y into sorting order (order = -y * precision). Higher " +
                 "values separate units standing at nearly the same height more finely, at the cost " +
                 "of head-room before the order exceeds Unity's -32768..32767 sorting range. 150 " +
                 "keeps a full screen of characters comfortably inside it.")]
        [SerializeField] private float precision = 150f;

        [Tooltip("Optional. The transform whose world Y decides the depth. Leave empty to use this " +
                 "GameObject — correct for every character prefab, where the root sits at the " +
                 "unit's feet. Only set this for something whose own origin isn't its ground point.")]
        [SerializeField] private Transform depthSource;

        [Tooltip("Optional. Forces which renderer counts as the main visual instead of the " +
                 "auto-detected one (Spine skeleton, else sprite, else first renderer). Leave empty " +
                 "unless a prefab has several renderers and auto-detection picks the wrong one.")]
        [SerializeField] private Renderer mainRendererOverride;

        private Renderer _main;
        private Transform _depthSource;
        private readonly List<Renderer> _group = new List<Renderer>();
        private readonly List<int> _offsets = new List<int>();
        private int _lastOrder = int.MinValue;

        /// <summary>The order last written to the main renderer. Surfaced by the custom Editor.</summary>
        public int CurrentOrder => _lastOrder;
        public Renderer Main => _main;
        public IReadOnlyList<Renderer> Group => _group;
        public IReadOnlyList<int> Offsets => _offsets;

        private void Awake()
        {
            _depthSource = depthSource != null ? depthSource : transform;
            _main = mainRendererOverride != null ? mainRendererOverride : DetectMainRenderer();
            CacheGroup();
        }

        private Renderer DetectMainRenderer()
        {
            var skeleton = GetComponentInChildren<SkeletonAnimation>(true);
            if (skeleton != null) return skeleton.GetComponent<Renderer>();

            var sprite = GetComponentInChildren<SpriteRenderer>(true);
            if (sprite != null) return sprite;

            return GetComponentInChildren<Renderer>(true);
        }

        /// <summary>
        /// Records the main renderer's companions and how far each was authored from it, so the
        /// group can be moved as a unit while preserving the artist's intended internal layering.
        /// </summary>
        private void CacheGroup()
        {
            Transform container = _main.transform.parent != null ? _main.transform.parent : _main.transform;
            int layer = _main.sortingLayerID;
            int baseOrder = _main.sortingOrder;

            foreach (var r in container.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sortingLayerID != layer) continue;
                _group.Add(r);
                _offsets.Add(r.sortingOrder - baseOrder);
            }
        }

        private void LateUpdate()
        {
            int order = OrderForCurrentDepth();
            if (order == _lastOrder) return;

            _lastOrder = order;
            for (int i = 0; i < _group.Count; i++)
                _group[i].sortingOrder = order + _offsets[i];
        }

        private int OrderForCurrentDepth()
        {
            float raw = -_depthSource.position.y * precision;
            // Unity stores sorting order as a short; wrapping would silently invert depth.
            return Mathf.Clamp(Mathf.RoundToInt(raw), short.MinValue, short.MaxValue);
        }
    }
}
