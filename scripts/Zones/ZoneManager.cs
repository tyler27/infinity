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
        }

        public override void _ExitTree()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Applies a zone's position offset, scale, rotation, and keystone correction to a point.
        /// </summary>
        public LaserPoint TransformPoint(int zoneIndex, LaserPoint point)
        {
            if (!ValidateZoneIndex(zoneIndex)) return point;

            ProjectionZone zone = _zones[zoneIndex];
            if (zone == null || !zone.Enabled) return point;

            Vector2 pos = new Vector2(point.x, point.y);

            // Apply scale.
            pos.X *= zone.Scale.X;
            pos.Y *= zone.Scale.Y;

            // Apply rotation (degrees, around origin).
            if (Mathf.Abs(zone.Rotation) > Mathf.Epsilon)
            {
                float rad = Mathf.DegToRad(zone.Rotation);
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                float rx = pos.X * cos - pos.Y * sin;
                float ry = pos.X * sin + pos.Y * cos;
                pos.X = rx;
                pos.Y = ry;
            }

            // Apply position offset.
            pos += zone.PositionOffset;

            // Apply keystone correction.
            if (zone.KeystoneCorners != null && zone.KeystoneCorners.Length == 4)
            {
                pos = KeystoneCorrection.ApplyKeystone(pos, zone.KeystoneCorners);
            }

            return new LaserPoint(pos.X, pos.Y, point.r, point.g, point.b, point.blanking);
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
        /// </summary>
        public List<int> GetZonesForProjector(int projectorIndex)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < _zones.Count; i++)
            {
                if (_zones[i] != null && _zones[i].ProjectorIndex == projectorIndex)
                {
                    result.Add(i);
                }
            }
            return result;
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
