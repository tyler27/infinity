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
        private static readonly Color WallColor = new Color(0.08f, 0.08f, 0.1f, 0.15f);
        private static readonly Color ProjectorMarkerColor = new Color(0.5f, 0.5f, 0.6f, 0.6f);

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

        private void Build()
        {
            _mesh.ClearSurfaces();

            // Read projector positions from scene
            var preview3D = GetParent();
            Vector3[] projPositions = new Vector3[4];
            float maxBeamLen = 20f;

            for (int i = 0; i < 4; i++)
            {
                var projNode = preview3D?.GetNodeOrNull<Node3D>($"Projector{i + 1}");
                if (projNode != null)
                {
                    projPositions[i] = projNode.Position;
                    var renderer = projNode as LaserPreviewRenderer;
                    if (renderer != null)
                        maxBeamLen = renderer.MaxBeamLength;
                }
                else
                {
                    projPositions[i] = new Vector3((i - 1.5f) * 2f, 4f, 0f);
                }
            }

            float wallZ = -maxBeamLen;
            float avgY = 0;
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var p in projPositions)
            {
                avgY += p.Y;
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
            }
            avgY /= 4f;

            _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

            // ── Ground plane grid (y=0) ──
            float groundExtent = Mathf.Max(maxBeamLen * 1.2f, 20f);
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

            // ── Back wall outline ──
            float wallHalfW = maxBeamLen * 0.6f;
            float wallHalfH = maxBeamLen * 0.4f;
            float wallCenterY = avgY;

            Vector3 wTL = new Vector3(-wallHalfW, wallCenterY + wallHalfH, wallZ);
            Vector3 wTR = new Vector3(wallHalfW, wallCenterY + wallHalfH, wallZ);
            Vector3 wBR = new Vector3(wallHalfW, wallCenterY - wallHalfH, wallZ);
            Vector3 wBL = new Vector3(-wallHalfW, wallCenterY - wallHalfH, wallZ);

            AddLine(wTL, wTR, WallColor, 0.03f);
            AddLine(wTR, wBR, WallColor, 0.03f);
            AddLine(wBR, wBL, WallColor, 0.03f);
            AddLine(wBL, wTL, WallColor, 0.03f);

            // Floor-to-wall edges
            var floorWallColor = new Color(0.08f, 0.08f, 0.1f, 0.08f);
            AddLine(new Vector3(-wallHalfW, 0, 0), wBL, floorWallColor, 0.015f);
            AddLine(new Vector3(wallHalfW, 0, 0), wBR, floorWallColor, 0.015f);

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
    }
}
