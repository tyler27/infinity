using Godot;

namespace LazerSystem.Preview
{
    /// <summary>
    /// Draws a grid + zone boundary on the projection wall in 3D.
    /// Grid lines at 0.5 intervals, outer boundary with corner brackets,
    /// center crosshair. Turns red when overflow is detected.
    /// </summary>
    public partial class ZoneBoundaryDisplay : Node3D
    {
        [Export] public Color NormalColor = new Color(0.25f, 0.3f, 0.4f, 0.5f);
        [Export] public Color OverflowColor = new Color(1f, 0.15f, 0.1f, 0.8f);

        private ImmediateMesh _mesh;
        private MeshInstance3D _meshInstance;
        private bool _overflow;

        // Projection plane constants (matching LaserPreviewRenderer)
        private const float HalfW = 10f;
        private const float CenterY = 4f;
        private const float HalfH = 10f;
        private const float PlaneZ = -20f;

        public override void _Ready()
        {
            _mesh = new ImmediateMesh();
            _meshInstance = new MeshInstance3D();
            _meshInstance.Mesh = _mesh;
            _meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.VertexColorUseAsAlbedo = true;
            mat.NoDepthTest = true;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _meshInstance.MaterialOverride = mat;
            AddChild(_meshInstance);

            Rebuild();
        }

        public void SetOverflow(bool overflow)
        {
            if (overflow != _overflow)
            {
                _overflow = overflow;
                Rebuild();
            }
        }

        private Vector3 NormToWorld(float nx, float ny)
        {
            return new Vector3(nx * HalfW, CenterY + ny * HalfH, PlaneZ);
        }

        private void Rebuild()
        {
            if (_mesh == null) return;
            _mesh.ClearSurfaces();

            Color borderColor = _overflow ? OverflowColor : NormalColor;
            Color gridColor = new Color(0.15f, 0.15f, 0.2f, 0.25f);
            Color centerColor = new Color(0.25f, 0.25f, 0.3f, 0.35f);

            // All geometry in one surface
            _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

            // Grid lines at 0.5 intervals
            for (int i = -2; i <= 2; i++)
            {
                float norm = i * 0.5f;
                Color lc = (i == 0) ? centerColor : gridColor;
                float lw = (i == 0) ? 0.04f : 0.025f;

                // Horizontal
                AddQuadLine(NormToWorld(-1f, norm), NormToWorld(1f, norm), lc, lw);
                // Vertical
                AddQuadLine(NormToWorld(norm, -1f), NormToWorld(norm, 1f), lc, lw);
            }

            // Outer boundary (thicker)
            float bw = _overflow ? 0.07f : 0.04f;
            Vector3 tl = NormToWorld(-1f,  1f);
            Vector3 tr = NormToWorld( 1f,  1f);
            Vector3 br = NormToWorld( 1f, -1f);
            Vector3 bl = NormToWorld(-1f, -1f);

            AddQuadLine(tl, tr, borderColor, bw);
            AddQuadLine(tr, br, borderColor, bw);
            AddQuadLine(br, bl, borderColor, bw);
            AddQuadLine(bl, tl, borderColor, bw);

            // Corner brackets
            float bracketLen = 1.5f;
            float bw2 = bw * 1.5f;

            AddQuadLine(tl, tl + new Vector3(bracketLen, 0, 0), borderColor, bw2);
            AddQuadLine(tl, tl + new Vector3(0, -bracketLen, 0), borderColor, bw2);

            AddQuadLine(tr, tr + new Vector3(-bracketLen, 0, 0), borderColor, bw2);
            AddQuadLine(tr, tr + new Vector3(0, -bracketLen, 0), borderColor, bw2);

            AddQuadLine(br, br + new Vector3(-bracketLen, 0, 0), borderColor, bw2);
            AddQuadLine(br, br + new Vector3(0, bracketLen, 0), borderColor, bw2);

            AddQuadLine(bl, bl + new Vector3(bracketLen, 0, 0), borderColor, bw2);
            AddQuadLine(bl, bl + new Vector3(0, bracketLen, 0), borderColor, bw2);

            // Center crosshair
            float crossLen = 0.6f;
            Vector3 c = NormToWorld(0, 0);
            AddQuadLine(c - new Vector3(crossLen, 0, 0), c + new Vector3(crossLen, 0, 0), centerColor, 0.04f);
            AddQuadLine(c - new Vector3(0, crossLen, 0), c + new Vector3(0, crossLen, 0), centerColor, 0.04f);

            _mesh.SurfaceEnd();
        }

        /// <summary>Adds a camera-facing quad line segment to the current mesh surface.</summary>
        private void AddQuadLine(Vector3 a, Vector3 b, Color color, float width)
        {
            Vector3 dir = (b - a).Normalized();
            Vector3 forward = new Vector3(0, 0, 1);
            Vector3 side = dir.Cross(forward).Normalized() * width;

            // If side is degenerate (dir parallel to forward), use up
            if (side.LengthSquared() < 0.0001f)
                side = dir.Cross(Vector3.Up).Normalized() * width;

            // Triangle 1
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(a - side);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(a + side);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(b + side);

            // Triangle 2
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(a - side);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(b + side);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(b - side);
        }
    }
}
