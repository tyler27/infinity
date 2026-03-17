using Godot;

namespace LazerSystem.Preview
{
    /// <summary>
    /// Draws a 3D venue environment for the laser preview.
    /// Rebuilds when projector positions or projection distance change.
    /// </summary>
    public partial class VenueGrid : Node3D
    {
        private ImmediateMesh _mesh;
        private MeshInstance3D _meshInstance;
        private bool _dirty = true;

        private static readonly Color GridColor = new Color(0.12f, 0.12f, 0.16f, 0.4f);
        private static readonly Color AxisX = new Color(0.8f, 0.2f, 0.2f, 0.7f);
        private static readonly Color AxisY = new Color(0.2f, 0.8f, 0.2f, 0.7f);
        private static readonly Color AxisZ = new Color(0.2f, 0.2f, 0.8f, 0.7f);
        private static readonly Color ProjectorMarkerColor = new Color(0.5f, 0.5f, 0.6f, 0.6f);
        private static readonly Color BoundsEdgeColor = new Color(0.9f, 0.5f, 0.1f, 0.6f);
        private static readonly Color BoundsFillColor = new Color(0.9f, 0.5f, 0.1f, 0.04f);

        private bool _showBounds;

        private static readonly Color[] ProjColors = {
            new Color(0.9f, 0.3f, 0.3f, 0.5f),
            new Color(0.3f, 0.9f, 0.4f, 0.5f),
            new Color(0.3f, 0.5f, 0.9f, 0.5f),
            new Color(0.9f, 0.85f, 0.3f, 0.5f)
        };

        public override void _Ready()
        {
            _mesh = new ImmediateMesh();
            _meshInstance = new MeshInstance3D();
            _meshInstance.Mesh = _mesh;
            _meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.VertexColorUseAsAlbedo = true;
            mat.NoDepthTest = false;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _meshInstance.MaterialOverride = mat;
            AddChild(_meshInstance);
        }

        public override void _Process(double delta)
        {
            if (_dirty)
            {
                _dirty = false;
                Build();
            }
        }

        /// <summary>Call when projector positions or distance changed.</summary>
        public void MarkDirty()
        {
            _dirty = true;
        }

        /// <summary>Toggle visibility of the raycast boundary surfaces.</summary>
        public void SetShowBounds(bool show)
        {
            _showBounds = show;
            _dirty = true;
        }

        private void Build()
        {
            _mesh.ClearSurfaces();

            // Read projector positions from scene
            var preview3D = GetParent();
            Vector3[] projPositions = new Vector3[4];

            for (int i = 0; i < 4; i++)
            {
                var projNode = preview3D?.GetNodeOrNull<Node3D>($"Projector{i + 1}");
                if (projNode != null)
                    projPositions[i] = projNode.GlobalPosition;
                else
                    projPositions[i] = new Vector3((i - 1.5f) * 2f, 4f, 0f);
            }

            _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

            // ── Ground plane grid (y=0) ──
            float groundExtent = 20f;
            float step = 2f;
            for (float x = -groundExtent; x <= groundExtent; x += step)
            {
                AddLine(new Vector3(x, 0, -groundExtent), new Vector3(x, 0, groundExtent), GridColor, 0.02f);
            }
            for (float z = -groundExtent; z <= groundExtent; z += step)
            {
                AddLine(new Vector3(-groundExtent, 0, z), new Vector3(groundExtent, 0, z), GridColor, 0.02f);
            }

            // ── X/Y/Z Axes ──
            float axisLen = 3f;
            float axisThick = 0.04f;

            AddLine(Vector3.Zero, new Vector3(axisLen, 0, 0), AxisX, axisThick);
            AddLine(new Vector3(axisLen, 0, 0), new Vector3(axisLen - 0.3f, 0.15f, 0), AxisX, axisThick);
            AddLine(new Vector3(axisLen, 0, 0), new Vector3(axisLen - 0.3f, -0.15f, 0), AxisX, axisThick);

            AddLine(Vector3.Zero, new Vector3(0, axisLen, 0), AxisY, axisThick);
            AddLine(new Vector3(0, axisLen, 0), new Vector3(0.15f, axisLen - 0.3f, 0), AxisY, axisThick);
            AddLine(new Vector3(0, axisLen, 0), new Vector3(-0.15f, axisLen - 0.3f, 0), AxisY, axisThick);

            AddLine(Vector3.Zero, new Vector3(0, 0, -axisLen), AxisZ, axisThick);
            AddLine(new Vector3(0, 0, -axisLen), new Vector3(0.15f, 0, -axisLen + 0.3f), AxisZ, axisThick);
            AddLine(new Vector3(0, 0, -axisLen), new Vector3(-0.15f, 0, -axisLen + 0.3f), AxisZ, axisThick);

            // ── Projector markers ──
            for (int i = 0; i < 4; i++)
            {
                Vector3 pos = projPositions[i];
                float s = 0.3f;

                AddLine(pos - new Vector3(s, 0, 0), pos + new Vector3(s, 0, 0), ProjColors[i], 0.03f);
                AddLine(pos - new Vector3(0, s, 0), pos + new Vector3(0, s, 0), ProjColors[i], 0.03f);
                AddLine(pos - new Vector3(0, 0, s), pos + new Vector3(0, 0, s), ProjColors[i], 0.03f);

                // Vertical pole from ground
                AddLine(new Vector3(pos.X, 0, pos.Z), pos, ProjectorMarkerColor, 0.015f);
            }

            // ── Truss connecting projectors ──
            if (projPositions.Length >= 2)
            {
                for (int i = 0; i < projPositions.Length - 1; i++)
                {
                    AddLine(projPositions[i], projPositions[i + 1], ProjectorMarkerColor, 0.02f);
                }
            }


            // ── Raycast boundary surfaces (matches LaserPreviewRenderer hit planes) ──
            if (_showBounds)
            {
                // Boundary dimensions match the raycast planes in LaserPreviewRenderer
                float bxMin = -20f, bxMax = 20f;
                float byMin = 0f, byMax = 15f;
                float bzMin = -25f, bzMax = 0f;

                // Back wall: z = -25
                AddQuad(
                    new Vector3(bxMin, byMin, bzMin), new Vector3(bxMax, byMin, bzMin),
                    new Vector3(bxMax, byMax, bzMin), new Vector3(bxMin, byMax, bzMin),
                    BoundsFillColor);
                AddLineLoop(
                    new Vector3(bxMin, byMin, bzMin), new Vector3(bxMax, byMin, bzMin),
                    new Vector3(bxMax, byMax, bzMin), new Vector3(bxMin, byMax, bzMin),
                    BoundsEdgeColor, 0.02f);

                // Floor: y = 0
                AddQuad(
                    new Vector3(bxMin, byMin, bzMax), new Vector3(bxMax, byMin, bzMax),
                    new Vector3(bxMax, byMin, bzMin), new Vector3(bxMin, byMin, bzMin),
                    BoundsFillColor);
                AddLineLoop(
                    new Vector3(bxMin, byMin, bzMax), new Vector3(bxMax, byMin, bzMax),
                    new Vector3(bxMax, byMin, bzMin), new Vector3(bxMin, byMin, bzMin),
                    BoundsEdgeColor, 0.02f);

                // Ceiling: y = 15
                AddQuad(
                    new Vector3(bxMin, byMax, bzMax), new Vector3(bxMax, byMax, bzMax),
                    new Vector3(bxMax, byMax, bzMin), new Vector3(bxMin, byMax, bzMin),
                    BoundsFillColor);
                AddLineLoop(
                    new Vector3(bxMin, byMax, bzMax), new Vector3(bxMax, byMax, bzMax),
                    new Vector3(bxMax, byMax, bzMin), new Vector3(bxMin, byMax, bzMin),
                    BoundsEdgeColor, 0.02f);

                // Left wall: x = -20
                AddQuad(
                    new Vector3(bxMin, byMin, bzMax), new Vector3(bxMin, byMin, bzMin),
                    new Vector3(bxMin, byMax, bzMin), new Vector3(bxMin, byMax, bzMax),
                    BoundsFillColor);
                AddLineLoop(
                    new Vector3(bxMin, byMin, bzMax), new Vector3(bxMin, byMin, bzMin),
                    new Vector3(bxMin, byMax, bzMin), new Vector3(bxMin, byMax, bzMax),
                    BoundsEdgeColor, 0.02f);

                // Right wall: x = 20
                AddQuad(
                    new Vector3(bxMax, byMin, bzMax), new Vector3(bxMax, byMin, bzMin),
                    new Vector3(bxMax, byMax, bzMin), new Vector3(bxMax, byMax, bzMax),
                    BoundsFillColor);
                AddLineLoop(
                    new Vector3(bxMax, byMin, bzMax), new Vector3(bxMax, byMin, bzMin),
                    new Vector3(bxMax, byMax, bzMin), new Vector3(bxMax, byMax, bzMax),
                    BoundsEdgeColor, 0.02f);
            }

            _mesh.SurfaceEnd();
        }

        private void AddLine(Vector3 a, Vector3 b, Color color, float width)
        {
            Vector3 dir = (b - a);
            if (dir.LengthSquared() < 0.0001f) return;
            dir = dir.Normalized();

            Vector3 side;
            if (Mathf.Abs(dir.Y) > 0.9f)
                side = new Vector3(width, 0, 0);
            else
            {
                side = dir.Cross(Vector3.Up).Normalized() * width;
                if (side.LengthSquared() < 0.0001f)
                    side = dir.Cross(Vector3.Right).Normalized() * width;
            }

            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(a - side);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(a + side);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(b + side);

            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(a - side);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(b + side);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(b - side);
        }

        /// <summary>Draws a filled quad from 4 corners (two triangles).</summary>
        private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(a);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(b);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(c);

            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(a);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(c);
            _mesh.SurfaceSetColor(color);
            _mesh.SurfaceAddVertex(d);
        }

        /// <summary>Draws 4 edge lines around a quad.</summary>
        private void AddLineLoop(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float width)
        {
            AddLine(a, b, color, width);
            AddLine(b, c, color, width);
            AddLine(c, d, color, width);
            AddLine(d, a, color, width);
        }
    }
}
