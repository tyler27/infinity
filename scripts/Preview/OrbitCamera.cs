using Godot;

namespace LazerSystem.Preview
{
    /// <summary>
    /// Orbit camera for the 3D laser preview.
    /// Left-drag to orbit, scroll to zoom, middle-drag to pan.
    /// Supports preset positions for quick views.
    /// </summary>
    public partial class OrbitCamera : Camera3D
    {
        /// <summary>The point the camera orbits around.</summary>
        public Vector3 FocusPoint { get; set; } = new Vector3(0f, 4f, -10f);

        /// <summary>Distance from the focus point.</summary>
        public float Distance { get; set; } = 22f;

        /// <summary>Horizontal angle in degrees (0 = looking along -Z).</summary>
        public float Azimuth { get; set; } = 0f;

        /// <summary>Vertical angle in degrees (positive = above).</summary>
        public float Elevation { get; set; } = 12f;

        // Limits
        private const float MinDistance = 2f;
        private const float MaxDistance = 60f;
        private const float MinElevation = -85f;
        private const float MaxElevation = 85f;

        // Sensitivity
        private const float OrbitSensitivity = 0.3f;
        private const float PanSensitivity = 0.02f;
        private const float ZoomSensitivity = 1.5f;

        // Drag state
        private bool _orbiting;
        private bool _panning;
        private Vector2 _lastMousePos;

        public override void _Ready()
        {
            Fov = 60f;
            Current = true;
            UpdateTransform();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // Only handle input if this camera's viewport is focused
            // (works in both inline SubViewport and popped-out panel)

            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    _orbiting = mb.Pressed;
                    _lastMousePos = mb.Position;
                    if (mb.Pressed) GetViewport().SetInputAsHandled();
                }
                else if (mb.ButtonIndex == MouseButton.Middle)
                {
                    _panning = mb.Pressed;
                    _lastMousePos = mb.Position;
                    if (mb.Pressed) GetViewport().SetInputAsHandled();
                }
                else if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    Distance = Mathf.Max(MinDistance, Distance - ZoomSensitivity * (Distance * 0.1f));
                    UpdateTransform();
                    GetViewport().SetInputAsHandled();
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    Distance = Mathf.Min(MaxDistance, Distance + ZoomSensitivity * (Distance * 0.1f));
                    UpdateTransform();
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventMouseMotion mm)
            {
                Vector2 delta = mm.Position - _lastMousePos;
                _lastMousePos = mm.Position;

                if (_orbiting)
                {
                    Azimuth -= delta.X * OrbitSensitivity;
                    Elevation += delta.Y * OrbitSensitivity;
                    Elevation = Mathf.Clamp(Elevation, MinElevation, MaxElevation);
                    UpdateTransform();
                    GetViewport().SetInputAsHandled();
                }
                else if (_panning)
                {
                    // Pan in the camera's local right/up plane
                    float panScale = PanSensitivity * Distance * 0.1f;
                    Vector3 right = GlobalTransform.Basis.X;
                    Vector3 up = GlobalTransform.Basis.Y;
                    FocusPoint -= right * delta.X * panScale;
                    FocusPoint += up * delta.Y * panScale;
                    UpdateTransform();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        /// <summary>Recalculate camera position from orbit parameters.</summary>
        public void UpdateTransform()
        {
            float azRad = Mathf.DegToRad(Azimuth);
            float elRad = Mathf.DegToRad(Elevation);

            // Spherical to cartesian (relative to focus point)
            float cosEl = Mathf.Cos(elRad);
            Vector3 offset = new Vector3(
                Mathf.Sin(azRad) * cosEl,
                Mathf.Sin(elRad),
                Mathf.Cos(azRad) * cosEl
            ) * Distance;

            Position = FocusPoint + offset;
            LookAt(FocusPoint, Vector3.Up);
        }

        // ── Preset positions ──

        public void SetFront()
        {
            FocusPoint = new Vector3(0f, 4f, -10f);
            Distance = 25f;
            Azimuth = 0f;
            Elevation = 10f;
            UpdateTransform();
        }

        public void SetTop()
        {
            FocusPoint = new Vector3(0f, 0f, -10f);
            Distance = 30f;
            Azimuth = 0f;
            Elevation = 85f;
            UpdateTransform();
        }

        public void SetSide()
        {
            FocusPoint = new Vector3(0f, 4f, -10f);
            Distance = 25f;
            Azimuth = 90f;
            Elevation = 5f;
            UpdateTransform();
        }

        public void SetProjectorView()
        {
            FocusPoint = new Vector3(0f, 4f, -8f);
            Distance = 10f;
            Azimuth = 0f;
            Elevation = 15f;
            UpdateTransform();
        }

        public void Reset()
        {
            FocusPoint = new Vector3(0f, 4f, -10f);
            Distance = 22f;
            Azimuth = 0f;
            Elevation = 12f;
            UpdateTransform();
        }
    }
}
