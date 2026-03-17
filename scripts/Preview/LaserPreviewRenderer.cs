using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Preview
{
    /// <summary>
    /// Renders laser beams and per-projector zone boundary in 3D.
    /// </summary>
    public partial class LaserPreviewRenderer : Node3D
    {
        [ExportGroup("Beam Settings")]
        [Export] public float BeamWidth = 0.03f;
        [Export] public float MaxBeamLength = 20f;

        [ExportGroup("Fog / Haze")]
        [Export] public float HazeIntensity = 2f;

        [ExportGroup("Source Beams")]
        [Export] public bool ShowSourceBeams = false;
        [Export] public float SourceBeamWidth = 0.01f;
        [Export] public float SourceBeamAlpha = 0.15f;

        [ExportGroup("Zone Boundary")]
        [Export] public Color ZoneColor = new Color(0.3f, 0.5f, 0.4f, 0.5f);

        private ImmediateMesh _beamMesh;
        private MeshInstance3D _beamMeshInstance;
        private StandardMaterial3D _beamMaterial;

        private ImmediateMesh _sourceMesh;
        private MeshInstance3D _sourceMeshInstance;
        private StandardMaterial3D _sourceMaterial;

        private ImmediateMesh _zoneMesh;
        private MeshInstance3D _zoneMeshInstance;
        private StandardMaterial3D _zoneMaterial;

        private bool _showZoneBoundary;
        private bool _zoneDirty = true;

        public override void _Ready()
        {
            // Beam mesh (additive blend for glow)
            _beamMesh = new ImmediateMesh();
            _beamMeshInstance = new MeshInstance3D();
            _beamMeshInstance.Mesh = _beamMesh;
            _beamMeshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

            _beamMaterial = new StandardMaterial3D();
            _beamMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _beamMaterial.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
            _beamMaterial.VertexColorUseAsAlbedo = true;
            _beamMaterial.NoDepthTest = true;
            _beamMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            _beamMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _beamMeshInstance.MaterialOverride = _beamMaterial;
            AddChild(_beamMeshInstance);

            // Source beam mesh (thin lines from projector to wall points)
            _sourceMesh = new ImmediateMesh();
            _sourceMeshInstance = new MeshInstance3D();
            _sourceMeshInstance.Mesh = _sourceMesh;
            _sourceMeshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _sourceMeshInstance.Visible = false;

            _sourceMaterial = new StandardMaterial3D();
            _sourceMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _sourceMaterial.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
            _sourceMaterial.VertexColorUseAsAlbedo = true;
            _sourceMaterial.NoDepthTest = true;
            _sourceMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            _sourceMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _sourceMeshInstance.MaterialOverride = _sourceMaterial;
            AddChild(_sourceMeshInstance);

            // Zone boundary mesh (alpha blend, no additive)
            _zoneMesh = new ImmediateMesh();
            _zoneMeshInstance = new MeshInstance3D();
            _zoneMeshInstance.Mesh = _zoneMesh;
            _zoneMeshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _zoneMeshInstance.Visible = false;

            _zoneMaterial = new StandardMaterial3D();
            _zoneMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _zoneMaterial.VertexColorUseAsAlbedo = true;
            _zoneMaterial.NoDepthTest = true;
            _zoneMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            _zoneMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _zoneMeshInstance.MaterialOverride = _zoneMaterial;
            AddChild(_zoneMeshInstance);
        }

        /// <summary>Toggle zone boundary visibility for this projector.</summary>
        public void SetShowZoneBoundary(bool show)
        {
            if (show == _showZoneBoundary) return;
            _showZoneBoundary = show;
            _zoneMeshInstance.Visible = show;
            if (show) _zoneDirty = true;
        }

        /// <summary>Set the zone color (matches projector color).</summary>
        public void SetZoneColor(Color color)
        {
            ZoneColor = color;
            _zoneDirty = true;
        }

        /// <summary>Force rebuild of zone boundary (call when zone config changes).</summary>
        public void MarkZoneDirty()
        {
            _zoneDirty = true;
        }

        public override void _Process(double delta)
        {
            if (_showZoneBoundary && _zoneDirty)
            {
                _zoneDirty = false;
                RebuildZoneBoundary();
            }
        }

        public void RenderFrame(List<LaserPoint> points)
        {
            _beamMesh.ClearSurfaces();

            if (points == null || points.Count < 2)
                return;

            // Collect visible segments
            var segments = new List<(int start, int end)>();
            int segStart = -1;

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].blanking)
                {
                    if (segStart >= 0 && i - segStart >= 2)
                        segments.Add((segStart, i - 1));
                    segStart = -1;
                }
                else
                {
                    if (segStart < 0) segStart = i;
                }
            }
            if (segStart >= 0 && points.Count - segStart >= 2)
                segments.Add((segStart, points.Count - 1));

            if (segments.Count == 0) return;

            var camera = GetViewport()?.GetCamera3D();
            Vector3 camPos = camera != null ? camera.GlobalPosition : new Vector3(0, 5, 15);

            _beamMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

            foreach (var seg in segments)
            {
                for (int i = seg.start; i < seg.end; i++)
                {
                    LaserPoint p0 = points[i];
                    LaserPoint p1 = points[i + 1];

                    Vector3 w0 = LaserPointToWorld(p0);
                    Vector3 w1 = LaserPointToWorld(p1);

                    Vector3 lineDir = (w1 - w0).Normalized();
                    Vector3 camDir = ((camPos - w0) + (camPos - w1)).Normalized();
                    Vector3 side = lineDir.Cross(camDir).Normalized() * BeamWidth;

                    Color c0 = PointColor(p0);
                    Color c1 = PointColor(p1);

                    _beamMesh.SurfaceSetColor(c0);
                    _beamMesh.SurfaceAddVertex(w0 - side);
                    _beamMesh.SurfaceSetColor(c0);
                    _beamMesh.SurfaceAddVertex(w0 + side);
                    _beamMesh.SurfaceSetColor(c1);
                    _beamMesh.SurfaceAddVertex(w1 + side);

                    _beamMesh.SurfaceSetColor(c0);
                    _beamMesh.SurfaceAddVertex(w0 - side);
                    _beamMesh.SurfaceSetColor(c1);
                    _beamMesh.SurfaceAddVertex(w1 + side);
                    _beamMesh.SurfaceSetColor(c1);
                    _beamMesh.SurfaceAddVertex(w1 - side);
                }
            }

            _beamMesh.SurfaceEnd();

            // Draw source beams (thin lines from projector origin to each point on the wall)
            _sourceMesh.ClearSurfaces();
            if (ShowSourceBeams && _sourceMeshInstance.Visible)
            {
                Vector3 origin = GlobalPosition; // projector position
                _sourceMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

                foreach (var seg in segments)
                {
                    // Draw a source beam to the first and last point of each segment
                    // (drawing to every point would be too dense)
                    LaserPoint pFirst = points[seg.start];
                    LaserPoint pLast = points[seg.end];

                    Vector3 wFirst = LaserPointToWorld(pFirst);
                    Vector3 wLast = LaserPointToWorld(pLast);

                    Color cFirst = new Color(pFirst.r, pFirst.g, pFirst.b, SourceBeamAlpha);
                    Color cLast = new Color(pLast.r, pLast.g, pLast.b, SourceBeamAlpha);
                    Color cOrigin = new Color(cFirst.R * 0.3f, cFirst.G * 0.3f, cFirst.B * 0.3f, SourceBeamAlpha * 0.3f);

                    AddSourceBeamLine(origin, wFirst, cOrigin, cFirst, SourceBeamWidth);
                    if (seg.end != seg.start)
                        AddSourceBeamLine(origin, wLast, cOrigin, cLast, SourceBeamWidth);
                }

                _sourceMesh.SurfaceEnd();
            }
        }

        public void Clear()
        {
            _beamMesh?.ClearSurfaces();
            _sourceMesh?.ClearSurfaces();
        }

        /// <summary>Toggle source beam visibility.</summary>
        public void SetShowSourceBeams(bool show)
        {
            ShowSourceBeams = show;
            _sourceMeshInstance.Visible = show;
            if (!show) _sourceMesh?.ClearSurfaces();
        }

        /// <summary>Set the projection distance (how far the wall is).</summary>
        public void SetProjectionDistance(float distance)
        {
            MaxBeamLength = Mathf.Clamp(distance, 5f, 100f);
            _zoneDirty = true;
        }

        private void AddSourceBeamLine(Vector3 a, Vector3 b, Color colorA, Color colorB, float width)
        {
            Vector3 dir = (b - a);
            if (dir.LengthSquared() < 0.0001f) return;
            dir = dir.Normalized();

            Vector3 forward = new Vector3(0, 0, 1);
            Vector3 side = dir.Cross(forward).Normalized() * width;
            if (side.LengthSquared() < 0.0001f)
                side = dir.Cross(Vector3.Up).Normalized() * width;

            _sourceMesh.SurfaceSetColor(colorA);
            _sourceMesh.SurfaceAddVertex(a - side);
            _sourceMesh.SurfaceSetColor(colorA);
            _sourceMesh.SurfaceAddVertex(a + side);
            _sourceMesh.SurfaceSetColor(colorB);
            _sourceMesh.SurfaceAddVertex(b + side);

            _sourceMesh.SurfaceSetColor(colorA);
            _sourceMesh.SurfaceAddVertex(a - side);
            _sourceMesh.SurfaceSetColor(colorB);
            _sourceMesh.SurfaceAddVertex(b + side);
            _sourceMesh.SurfaceSetColor(colorB);
            _sourceMesh.SurfaceAddVertex(b - side);
        }

        private Color PointColor(LaserPoint pt)
        {
            return new Color(
                pt.r * HazeIntensity,
                pt.g * HazeIntensity,
                pt.b * HazeIntensity,
                1f
            );
        }

        private Vector3 LaserPointToWorld(LaserPoint pt)
        {
            Vector3 localTarget = new Vector3(pt.x * MaxBeamLength * 0.5f, pt.y * MaxBeamLength * 0.5f, -MaxBeamLength);
            return GlobalTransform * localTarget;
        }

        private Vector3 ZoneCornerToWorld(float x, float y)
        {
            Vector3 localTarget = new Vector3(x * MaxBeamLength * 0.5f, y * MaxBeamLength * 0.5f, -MaxBeamLength);
            return GlobalTransform * localTarget;
        }

        /// <summary>
        /// Draws the zone boundary for this specific projector.
        /// Shows the -1..1 scan area boundary, grid, and corner brackets.
        /// Uses this projector's ZoneColor.
        /// </summary>
        private void RebuildZoneBoundary()
        {
            _zoneMesh.ClearSurfaces();

            Color borderColor = ZoneColor;
            Color gridColor = new Color(ZoneColor.R * 0.3f, ZoneColor.G * 0.3f, ZoneColor.B * 0.3f, 0.2f);
            float borderWidth = 0.04f;
            float gridWidth = 0.02f;

            _zoneMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

            // Grid lines at 0.5 intervals
            for (int i = -2; i <= 2; i++)
            {
                float norm = i * 0.5f;
                AddZoneLine(ZoneCornerToWorld(-1f, norm), ZoneCornerToWorld(1f, norm), gridColor, gridWidth);
                AddZoneLine(ZoneCornerToWorld(norm, -1f), ZoneCornerToWorld(norm, 1f), gridColor, gridWidth);
            }

            // Outer boundary
            Vector3 tl = ZoneCornerToWorld(-1f,  1f);
            Vector3 tr = ZoneCornerToWorld( 1f,  1f);
            Vector3 br = ZoneCornerToWorld( 1f, -1f);
            Vector3 bl = ZoneCornerToWorld(-1f, -1f);

            AddZoneLine(tl, tr, borderColor, borderWidth);
            AddZoneLine(tr, br, borderColor, borderWidth);
            AddZoneLine(br, bl, borderColor, borderWidth);
            AddZoneLine(bl, tl, borderColor, borderWidth);

            // Corner brackets
            float bracketLen = 1.2f;
            float bw = borderWidth * 1.5f;
            AddZoneLine(tl, tl + new Vector3(bracketLen, 0, 0), borderColor, bw);
            AddZoneLine(tl, tl + new Vector3(0, -bracketLen, 0), borderColor, bw);
            AddZoneLine(tr, tr + new Vector3(-bracketLen, 0, 0), borderColor, bw);
            AddZoneLine(tr, tr + new Vector3(0, -bracketLen, 0), borderColor, bw);
            AddZoneLine(br, br + new Vector3(-bracketLen, 0, 0), borderColor, bw);
            AddZoneLine(br, br + new Vector3(0, bracketLen, 0), borderColor, bw);
            AddZoneLine(bl, bl + new Vector3(bracketLen, 0, 0), borderColor, bw);
            AddZoneLine(bl, bl + new Vector3(0, bracketLen, 0), borderColor, bw);

            // Center crosshair
            float crossLen = 0.5f;
            Vector3 c = ZoneCornerToWorld(0, 0);
            AddZoneLine(c - new Vector3(crossLen, 0, 0), c + new Vector3(crossLen, 0, 0), borderColor, gridWidth * 1.5f);
            AddZoneLine(c - new Vector3(0, crossLen, 0), c + new Vector3(0, crossLen, 0), borderColor, gridWidth * 1.5f);

            _zoneMesh.SurfaceEnd();
        }

        private void AddZoneLine(Vector3 a, Vector3 b, Color color, float width)
        {
            Vector3 dir = (b - a);
            if (dir.LengthSquared() < 0.0001f) return;
            dir = dir.Normalized();

            Vector3 forward = new Vector3(0, 0, 1);
            Vector3 side = dir.Cross(forward).Normalized() * width;
            if (side.LengthSquared() < 0.0001f)
                side = dir.Cross(Vector3.Up).Normalized() * width;

            _zoneMesh.SurfaceSetColor(color);
            _zoneMesh.SurfaceAddVertex(a - side);
            _zoneMesh.SurfaceSetColor(color);
            _zoneMesh.SurfaceAddVertex(a + side);
            _zoneMesh.SurfaceSetColor(color);
            _zoneMesh.SurfaceAddVertex(b + side);

            _zoneMesh.SurfaceSetColor(color);
            _zoneMesh.SurfaceAddVertex(a - side);
            _zoneMesh.SurfaceSetColor(color);
            _zoneMesh.SurfaceAddVertex(b + side);
            _zoneMesh.SurfaceSetColor(color);
            _zoneMesh.SurfaceAddVertex(b - side);
        }
    }
}
