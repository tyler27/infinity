using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Preview
{
    /// <summary>
    /// Renders laser beams in 3D. Zone boundary is now generated as LaserPoints
    /// and fed through the same rendering pipeline as pattern output.
    /// </summary>
    public partial class LaserPreviewRenderer : Node3D
    {
        [ExportGroup("Beam Settings")]
        [Export] public float BeamWidth = 0.03f;
        /// <summary>Full scan angle in degrees (galvo FOV). Typical FB4 is 40-60°.</summary>
        [Export] public float ScanAngleDeg = 50f;
        /// <summary>Maximum ray distance if no surface is hit.</summary>
        private const float MaxRayDistance = 100f;

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

        private bool _showZoneBoundary;

        /// <summary>Whether zone boundary is enabled for this projector.</summary>
        public bool ShowZoneBoundary => _showZoneBoundary;

        public override void _Ready()
        {
            // Beam mesh (additive blend for glow)
            _beamMesh = new ImmediateMesh();
            _beamMeshInstance = new MeshInstance3D();
            _beamMeshInstance.Mesh = _beamMesh;
            _beamMeshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _beamMeshInstance.TopLevel = true; // Don't inherit parent transform — we use world coords

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
            _sourceMeshInstance.TopLevel = true; // Don't inherit parent transform — we use world coords
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
        }

        /// <summary>Toggle zone boundary visibility for this projector.</summary>
        public void SetShowZoneBoundary(bool show)
        {
            _showZoneBoundary = show;
        }

        /// <summary>Set the zone color (matches projector color).</summary>
        public void SetZoneColor(Color color)
        {
            ZoneColor = color;
        }

        /// <summary>
        /// Generates zone boundary as LaserPoints in scan space.
        /// The boundary traces the zone's keystone corners, so it represents exactly
        /// what the real projector would output. Points go through the same
        /// rendering/ArtNet pipeline as pattern output.
        /// </summary>
        /// <param name="corners">
        /// Four keystone corners in scan space: [0]=BL, [1]=BR, [2]=TR, [3]=TL.
        /// If null, defaults to the full -1..1 scan area.
        /// </param>
        public List<LaserPoint> GenerateZoneBoundaryPoints(Vector2[] corners = null)
        {
            // Default to full scan area if no keystone corners provided
            Vector2 bl = corners != null && corners.Length == 4 ? corners[0] : new Vector2(-1f, -1f);
            Vector2 br = corners != null && corners.Length == 4 ? corners[1] : new Vector2( 1f, -1f);
            Vector2 tr = corners != null && corners.Length == 4 ? corners[2] : new Vector2( 1f,  1f);
            Vector2 tl = corners != null && corners.Length == 4 ? corners[3] : new Vector2(-1f,  1f);

            var pts = new List<LaserPoint>(200);
            float r = ZoneColor.R;
            float g = ZoneColor.G;
            float b = ZoneColor.B;

            // Dimmer color for grid lines
            float gr = r * 0.4f;
            float gg = g * 0.4f;
            float gb = b * 0.4f;

            int steps = 20; // points per line segment for smooth rendering

            // ── Outer boundary (trace the keystone quad) ──
            AddScanLine(pts, bl.X, bl.Y, br.X, br.Y, r, g, b, steps); // bottom
            AddScanLine(pts, br.X, br.Y, tr.X, tr.Y, r, g, b, steps); // right
            AddScanLine(pts, tr.X, tr.Y, tl.X, tl.Y, r, g, b, steps); // top
            AddScanLine(pts, tl.X, tl.Y, bl.X, bl.Y, r, g, b, steps); // left

            // ── Grid lines interpolated across the keystone quad ──
            // Use bilinear interpolation so grid lines follow the keystone warp
            int gridDivs = 4; // 4 divisions = lines at 0.25, 0.5, 0.75
            for (int i = 1; i < gridDivs; i++)
            {
                float t = (float)i / gridDivs;
                // Horizontal line: lerp left edge and right edge at parameter t
                Vector2 left = bl.Lerp(tl, t);
                Vector2 right = br.Lerp(tr, t);
                AddScanLine(pts, left.X, left.Y, right.X, right.Y, gr, gg, gb, steps);
                // Vertical line: lerp bottom edge and top edge at parameter t
                Vector2 bottom = bl.Lerp(br, t);
                Vector2 top = tl.Lerp(tr, t);
                AddScanLine(pts, bottom.X, bottom.Y, top.X, top.Y, gr, gg, gb, steps);
            }

            // ── Center crosshair ──
            Vector2 center = (bl + br + tr + tl) * 0.25f;
            // Cross arms along the quad's edge directions
            Vector2 hDir = ((br - bl) + (tr - tl)).Normalized() * 0.08f;
            Vector2 vDir = ((tl - bl) + (tr - br)).Normalized() * 0.08f;
            AddScanLine(pts, center.X - hDir.X, center.Y - hDir.Y,
                             center.X + hDir.X, center.Y + hDir.Y, r, g, b, 6);
            AddScanLine(pts, center.X - vDir.X, center.Y - vDir.Y,
                             center.X + vDir.X, center.Y + vDir.Y, r, g, b, 6);

            // ── Corner brackets (follow the edges of the keystone quad) ──
            float bFrac = 0.1f; // bracket = 10% of each edge
            // BL corner
            AddScanLine(pts, bl.X, bl.Y, bl.Lerp(br, bFrac).X, bl.Lerp(br, bFrac).Y, r, g, b, 4);
            AddScanLine(pts, bl.X, bl.Y, bl.Lerp(tl, bFrac).X, bl.Lerp(tl, bFrac).Y, r, g, b, 4);
            // BR corner
            AddScanLine(pts, br.X, br.Y, br.Lerp(bl, bFrac).X, br.Lerp(bl, bFrac).Y, r, g, b, 4);
            AddScanLine(pts, br.X, br.Y, br.Lerp(tr, bFrac).X, br.Lerp(tr, bFrac).Y, r, g, b, 4);
            // TR corner
            AddScanLine(pts, tr.X, tr.Y, tr.Lerp(tl, bFrac).X, tr.Lerp(tl, bFrac).Y, r, g, b, 4);
            AddScanLine(pts, tr.X, tr.Y, tr.Lerp(br, bFrac).X, tr.Lerp(br, bFrac).Y, r, g, b, 4);
            // TL corner
            AddScanLine(pts, tl.X, tl.Y, tl.Lerp(tr, bFrac).X, tl.Lerp(tr, bFrac).Y, r, g, b, 4);
            AddScanLine(pts, tl.X, tl.Y, tl.Lerp(bl, bFrac).X, tl.Lerp(bl, bFrac).Y, r, g, b, 4);

            return pts;
        }

        /// <summary>
        /// Adds a line segment as LaserPoints with a blanked move to the start.
        /// </summary>
        private static void AddScanLine(List<LaserPoint> pts, float x0, float y0, float x1, float y1,
            float r, float g, float b, int steps)
        {
            // Blank move to start
            pts.Add(LaserPoint.Blanked(x0, y0));

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float x = x0 + (x1 - x0) * t;
                float y = y0 + (y1 - y0) * t;
                pts.Add(new LaserPoint(x, y, r, g, b, false));
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
            return ScanPointToWorld(pt.x, pt.y);
        }

        /// <summary>
        /// Maps a normalized scan coordinate (-1..1) to a world-space point.
        /// Uses the scan angle (galvo FOV) to compute a beam direction,
        /// then raycasts against venue surfaces (back wall, floor, ceiling, side walls).
        /// Beam terminates where it hits — like a real laser.
        /// </summary>
        private Vector3 ScanPointToWorld(float scanX, float scanY)
        {
            // Convert scan coordinates to galvo angles
            float halfAngleRad = Mathf.DegToRad(ScanAngleDeg * 0.5f);
            float angleX = scanX * halfAngleRad;
            float angleY = scanY * halfAngleRad;

            // Build local direction from angles (beam shoots along local -Z)
            Vector3 localDir = new Vector3(
                Mathf.Tan(angleX),
                Mathf.Tan(angleY),
                -1f
            ).Normalized();

            // Transform direction to world space
            Vector3 worldOrigin = GlobalPosition;
            Vector3 worldDir = GlobalTransform.Basis * localDir;

            // Raycast against venue surfaces — find nearest hit
            float nearest = MaxRayDistance;

            // Back wall: z = -25
            float hitDist = RayPlaneZ(worldOrigin, worldDir, -25f);
            if (hitDist > 0 && hitDist < nearest) nearest = hitDist;

            // Floor: y = 0
            hitDist = RayPlaneY(worldOrigin, worldDir, 0f);
            if (hitDist > 0 && hitDist < nearest) nearest = hitDist;

            // Ceiling: y = 15
            hitDist = RayPlaneY(worldOrigin, worldDir, 15f);
            if (hitDist > 0 && hitDist < nearest) nearest = hitDist;

            // Left wall: x = -20
            hitDist = RayPlaneX(worldOrigin, worldDir, -20f);
            if (hitDist > 0 && hitDist < nearest) nearest = hitDist;

            // Right wall: x = 20
            hitDist = RayPlaneX(worldOrigin, worldDir, 20f);
            if (hitDist > 0 && hitDist < nearest) nearest = hitDist;

            return worldOrigin + worldDir * nearest;
        }

        /// <summary>Ray-plane intersection for a plane at z = planeZ. Returns distance or -1.</summary>
        private static float RayPlaneZ(Vector3 origin, Vector3 dir, float planeZ)
        {
            if (Mathf.Abs(dir.Z) < 0.0001f) return -1f;
            float t = (planeZ - origin.Z) / dir.Z;
            return t > 0.001f ? t : -1f;
        }

        /// <summary>Ray-plane intersection for a plane at y = planeY. Returns distance or -1.</summary>
        private static float RayPlaneY(Vector3 origin, Vector3 dir, float planeY)
        {
            if (Mathf.Abs(dir.Y) < 0.0001f) return -1f;
            float t = (planeY - origin.Y) / dir.Y;
            return t > 0.001f ? t : -1f;
        }

        /// <summary>Ray-plane intersection for a plane at x = planeX. Returns distance or -1.</summary>
        private static float RayPlaneX(Vector3 origin, Vector3 dir, float planeX)
        {
            if (Mathf.Abs(dir.X) < 0.0001f) return -1f;
            float t = (planeX - origin.X) / dir.X;
            return t > 0.001f ? t : -1f;
        }
    }
}
