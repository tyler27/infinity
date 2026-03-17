using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Zones
{
    /// <summary>
    /// Singleton manager for projection zones. Handles coordinate transforms,
    /// keystone correction, and safety zone enforcement.
    /// </summary>
    public partial class ZoneManager : Node
    {
        private static ZoneManager _instance;

        public static ZoneManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GD.PushError("[ZoneManager] No instance found in scene.");
                }
                return _instance;
            }
        }

        [Export] private Godot.Collections.Array<ProjectionZone> _zones = new();

        public Godot.Collections.Array<ProjectionZone> Zones => _zones;

        // Cached per-projector zone index lists to avoid per-frame allocation
        private List<int>[] _projectorZoneCache;
        private bool _projectorZoneCacheDirty = true;

        public override void _Ready()
        {
            if (_instance != null && _instance != this)
            {
                GD.PushWarning("[ZoneManager] Duplicate instance destroyed.");
                QueueFree();
                return;
            }
            _instance = this;

            // Sync zones from LaserSystemManager so both share the same data
            if (LazerSystem.Core.LaserSystemManager.Instance != null)
            {
                _zones = LazerSystem.Core.LaserSystemManager.Instance.Zones;
            }

            RebuildProjectorZoneCache();
        }

        public override void _ExitTree()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Call when zones are added/removed/reassigned to rebuild the cache.
        /// </summary>
        public void InvalidateCache()
        {
            _projectorZoneCacheDirty = true;
        }

        private void RebuildProjectorZoneCache()
        {
            _projectorZoneCache = new List<int>[4];
            for (int p = 0; p < 4; p++)
            {
                _projectorZoneCache[p] = new List<int>();
                for (int i = 0; i < _zones.Count; i++)
                {
                    if (_zones[i] != null && _zones[i].ProjectorIndex == p)
                        _projectorZoneCache[p].Add(i);
                }
            }
            _projectorZoneCacheDirty = false;
        }

        /// <summary>
        /// Applies a zone's position offset, scale, rotation, and keystone correction to a point.
        /// </summary>
        public LaserPoint TransformPoint(int zoneIndex, LaserPoint point)
        {
            if (!ValidateZoneIndex(zoneIndex)) return point;

            ProjectionZone zone = _zones[zoneIndex];
            if (zone == null || !zone.Enabled) return point;

            float px = point.x;
            float py = point.y;

            // Apply scale.
            px *= zone.Scale.X;
            py *= zone.Scale.Y;

            // Apply rotation (degrees, around origin).
            if (Mathf.Abs(zone.Rotation) > Mathf.Epsilon)
            {
                float rad = Mathf.DegToRad(zone.Rotation);
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                float rx = px * cos - py * sin;
                float ry = px * sin + py * cos;
                px = rx;
                py = ry;
            }

            // Apply position offset.
            px += zone.PositionOffset.X;
            py += zone.PositionOffset.Y;

            // Apply keystone correction.
            if (zone.KeystoneCorners != null && zone.KeystoneCorners.Length == 4)
            {
                var pos = new Vector2(px, py);
                pos = KeystoneCorrection.ApplyKeystone(pos, zone.KeystoneCorners);
                px = pos.X;
                py = pos.Y;
            }

            return new LaserPoint(px, py, point.r, point.g, point.b, point.blanking);
        }

        /// <summary>
        /// Transforms points in-place within the target list, starting at the given offset.
        /// This avoids allocating a new list.
        /// </summary>
        public void TransformPointsInPlace(int zoneIndex, List<LaserPoint> points, int startIndex)
        {
            if (points == null) return;
            if (!ValidateZoneIndex(zoneIndex)) return;

            ProjectionZone zone = _zones[zoneIndex];
            if (zone == null || !zone.Enabled) return;

            for (int i = startIndex; i < points.Count; i++)
            {
                points[i] = TransformPoint(zoneIndex, points[i]);
            }
        }

        /// <summary>
        /// Applies zone transforms to a list of points.
        /// </summary>
        public List<LaserPoint> TransformPoints(int zoneIndex, List<LaserPoint> points)
        {
            if (points == null) return points;

            var result = new List<LaserPoint>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                result.Add(TransformPoint(zoneIndex, points[i]));
            }
            return result;
        }

        /// <summary>
        /// Checks whether a point falls within the zone's safety bounds.
        /// Safety zone is defined as a Rect2 in normalized space (0..1), mapped to (-1..1).
        /// </summary>
        public bool IsInSafetyZone(int zoneIndex, LaserPoint point)
        {
            if (!ValidateZoneIndex(zoneIndex)) return false;

            ProjectionZone zone = _zones[zoneIndex];
            if (zone == null) return false;

            Rect2 sz = zone.SafetyZone;
            float minX = sz.Position.X * 2f - 1f;
            float maxX = sz.End.X * 2f - 1f;
            float minY = sz.Position.Y * 2f - 1f;
            float maxY = sz.End.Y * 2f - 1f;

            return point.x >= minX && point.x <= maxX &&
                   point.y >= minY && point.y <= maxY;
        }

        /// <summary>
        /// Clamps a point to the zone's safety bounds.
        /// </summary>
        public LaserPoint ClampToSafety(int zoneIndex, LaserPoint point)
        {
            if (!ValidateZoneIndex(zoneIndex)) return point;

            ProjectionZone zone = _zones[zoneIndex];
            if (zone == null) return point;

            Rect2 sz = zone.SafetyZone;
            float minX = sz.Position.X * 2f - 1f;
            float maxX = sz.End.X * 2f - 1f;
            float minY = sz.Position.Y * 2f - 1f;
            float maxY = sz.End.Y * 2f - 1f;

            float cx = Mathf.Clamp(point.x, minX, maxX);
            float cy = Mathf.Clamp(point.y, minY, maxY);

            return new LaserPoint(cx, cy, point.r, point.g, point.b, point.blanking);
        }

        /// <summary>
        /// Returns all zone indices assigned to a given projector.
        /// Uses cached results; call InvalidateCache() when zones change.
        /// </summary>
        public List<int> GetZonesForProjector(int projectorIndex)
        {
            if (_projectorZoneCacheDirty)
                RebuildProjectorZoneCache();

            if (projectorIndex >= 0 && projectorIndex < _projectorZoneCache.Length)
                return _projectorZoneCache[projectorIndex];

            return new List<int>();
        }

        private bool ValidateZoneIndex(int index)
        {
            if (index < 0 || index >= _zones.Count)
            {
                GD.PushWarning($"[ZoneManager] Zone index {index} out of range (0-{_zones.Count - 1}).");
                return false;
            }
            return true;
        }
    }
}
