using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Preview
{
    /// <summary>
    /// Singleton manager for the 3D laser preview system.
    /// Manages up to 4 projector preview renderers, preview camera, and bloom post-processing.
    /// </summary>
    public partial class LaserPreviewManager : Node
    {
        private static LaserPreviewManager _instance;

        public static LaserPreviewManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GD.PushError("[LaserPreviewManager] No instance found in scene.");
                }
                return _instance;
            }
        }

        [ExportGroup("Projector Previews")]
        [Export] private LaserPreviewRenderer[] _projectorRenderers = new LaserPreviewRenderer[4];

        [ExportGroup("Preview Camera")]
        [Export] private Camera3D _previewCamera;

        [ExportGroup("Post-Processing")]
        [Export] private float _bloomIntensity = 5f;
        [Export] private float _bloomThreshold = 0.5f;

        [ExportGroup("State")]
        [Export] private bool _previewEnabled = true;

        public bool PreviewEnabled
        {
            get => _previewEnabled;
            set
            {
                _previewEnabled = value;
                ApplyPreviewState();
            }
        }

        public float BloomIntensity
        {
            get => _bloomIntensity;
            set => _bloomIntensity = value;
        }

        public float BloomThreshold
        {
            get => _bloomThreshold;
            set => _bloomThreshold = value;
        }

        public override void _Ready()
        {
            if (_instance != null && _instance != this)
            {
                GD.PushWarning("[LaserPreviewManager] Duplicate instance destroyed.");
                QueueFree();
                return;
            }
            _instance = this;
            AutoDiscoverRenderers();
            ApplyPreviewState();
        }

        /// <summary>
        /// Finds Projector1-4 sibling nodes and assigns them if the export array is empty.
        /// </summary>
        private void AutoDiscoverRenderers()
        {
            var parent = GetParent();
            if (parent == null) return;

            for (int i = 0; i < _projectorRenderers.Length; i++)
            {
                if (_projectorRenderers[i] != null) continue;

                var renderer = parent.GetNodeOrNull<LaserPreviewRenderer>($"Projector{i + 1}");
                if (renderer != null)
                {
                    _projectorRenderers[i] = renderer;
                }
            }
        }

        public override void _ExitTree()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Updates the preview for a specific zone/projector index.
        /// </summary>
        public void UpdatePreview(int zoneIndex, List<LaserPoint> points)
        {
            if (!_previewEnabled) return;

            if (zoneIndex < 0 || zoneIndex >= _projectorRenderers.Length)
            {
                GD.PushWarning($"[LaserPreviewManager] Zone index {zoneIndex} out of range (0-{_projectorRenderers.Length - 1}).");
                return;
            }

            LaserPreviewRenderer renderer = _projectorRenderers[zoneIndex];
            if (renderer == null)
            {
                GD.PushWarning($"[LaserPreviewManager] No renderer assigned for zone {zoneIndex}.");
                return;
            }

            renderer.RenderFrame(points);
        }

        /// <summary>Clears all projector previews.</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _projectorRenderers.Length; i++)
            {
                if (_projectorRenderers[i] != null)
                    _projectorRenderers[i].Clear();
            }
        }

        /// <summary>Toggles the preview on/off.</summary>
        public void TogglePreview()
        {
            PreviewEnabled = !_previewEnabled;
        }

        /// <summary>Sets the preview camera reference at runtime.</summary>
        public void SetPreviewCamera(Camera3D camera)
        {
            _previewCamera = camera;
            ApplyPreviewState();
        }

        /// <summary>Returns the renderer for the given projector index, or null.</summary>
        public LaserPreviewRenderer GetRenderer(int index)
        {
            if (index < 0 || index >= _projectorRenderers.Length)
                return null;
            return _projectorRenderers[index];
        }

        private void ApplyPreviewState()
        {
            if (_previewCamera != null)
            {
                _previewCamera.Current = _previewEnabled;
            }

            for (int i = 0; i < _projectorRenderers.Length; i++)
            {
                if (_projectorRenderers[i] != null)
                {
                    _projectorRenderers[i].Visible = _previewEnabled;
                }
            }

            if (!_previewEnabled)
            {
                ClearAll();
            }
        }
    }
}
