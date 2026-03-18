using System.Collections.Generic;
using Godot;
using LazerSystem.Core;
using LazerSystem.Patterns;
using LazerSystem.Zones;
using LazerSystem.ArtNet;
using LazerSystem.Sync;
using LazerSystem.Preview;

namespace LazerSystem.Timeline
{
    /// <summary>
    /// Singleton manager responsible for evaluating the laser show timeline,
    /// generating pattern output, and routing data to preview and ArtNet.
    /// </summary>
    public partial class PlaybackManager : Node
    {
        private static PlaybackManager _instance;

        public static PlaybackManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GD.PrintErr("[PlaybackManager] No instance found in scene.");
                }
                return _instance;
            }
        }

        [ExportGroup("References")]
        [Export] public LaserShow laserShow;
        [Export] public SyncManager syncManager;
        // PatternFactory is a static class; no serialized reference needed.
        [Export] public ZoneManager zoneManager;
        [Export] public ArtNetManager artNetManager;
        [Export] public LaserPreviewManager previewManager;

        [ExportGroup("Playback State")]
        [Export] private bool isPlaying;
        [Export] private bool isPaused;

        [ExportGroup("Tracks")]
        [Export] private Godot.Collections.Array<TimelineTrack> tracks = new();

        [ExportGroup("Live Cues")]
        [Export] private CueGridManager cueGridManager;

        // Internal typed list mirroring the export for convenience
        private List<TimelineTrack> _tracks = new List<TimelineTrack>();

        // Track which zones had output last frame so we can clear idle ones
        private HashSet<int> _activeZonesLastFrame = new HashSet<int>();

        public LaserShow LaserShow
        {
            get => laserShow;
            set => laserShow = value;
        }

        public bool IsPlaying => isPlaying;
        public bool IsPaused => isPaused;
        public List<TimelineTrack> Tracks => _tracks;

        /// <summary>Current playback time in seconds, sourced from the SyncManager.</summary>
        public float CurrentTime => syncManager != null ? syncManager.CurrentTime : 0f;

        /// <summary>Current BPM from the loaded show.</summary>
        public float BPM => laserShow != null ? laserShow.Bpm : 120f;

        /// <summary>Converts a time in seconds to a beat number based on the show BPM.</summary>
        public float TimeToBeat(float time)
        {
            return time * BPM / 60f;
        }

        /// <summary>Converts a beat number to a time in seconds based on the show BPM.</summary>
        public float BeatToTime(float beat)
        {
            return beat * 60f / BPM;
        }

        /// <summary>The current beat number (integer) based on playback time.</summary>
        public int CurrentBeat => Mathf.FloorToInt(TimeToBeat(CurrentTime));

        public override void _EnterTree()
        {
            if (_instance != null && _instance != this)
            {
                GD.Print("[PlaybackManager] Duplicate instance removed.");
                QueueFree();
                return;
            }
            _instance = this;
        }

        public override void _ExitTree()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public override void _Ready()
        {
            // Sync export array to internal list
            _tracks.Clear();
            if (tracks != null)
            {
                foreach (var t in tracks)
                    _tracks.Add(t);
            }

            // Run after LiveEngine (default priority 0) so timeline output
            // renders on top of / after live cue output.
            ProcessPriority = 100;
        }

        public override void _Process(double delta)
        {
            if (!isPlaying)
                return;

            float currentTime = CurrentTime;

            // Evaluate timeline cue blocks
            if (laserShow != null)
            {
                EvaluateTimeline(currentTime);
            }

            // Evaluate live-triggered cues from the cue grid
            if (cueGridManager != null)
            {
                EvaluateLiveCues(currentTime);
            }
        }

        /// <summary>
        /// Evaluates all timeline tracks at the given time, generating pattern output
        /// and routing to preview and ArtNet.
        /// </summary>
        private void EvaluateTimeline(float currentTime)
        {
            // Collect active blocks per zone for blending
            var zonePoints = new Dictionary<int, List<LaserPoint>>();

            // Check if any track is soloed
            bool anySolo = false;
            foreach (var t in _tracks)
            {
                if (t.solo) { anySolo = true; break; }
            }

            foreach (var track in _tracks)
            {
                if (track.muted)
                    continue;
                if (anySolo && !track.solo)
                    continue;

                var activeBlocks = track.GetActiveBlocks(currentTime);

                foreach (var block in activeBlocks)
                {
                    if (block.Cue == null)
                        continue;
                    if (block.Muted)
                        continue;

                    // Generate pattern points
                    var parameters = PatternParameters.FromCue(block.Cue);

                    // Apply per-block automation envelopes
                    if (block.Automation?.HasAnyAutomation == true)
                    {
                        float normalizedTime = (currentTime - block.StartTime) / block.Duration;
                        AutomationEvaluator.Apply(block.Automation, normalizedTime, parameters);
                    }

                    parameters.intensity *= track.volume;

                    // Apply fade in/out multiplier
                    float fadeMult = ComputeFadeMultiplier(block, currentTime);
                    parameters.intensity *= fadeMult;

                    float localTime = currentTime - block.StartTime;
                    List<LaserPoint> points = GeneratePatternPoints(block.Cue.PatternType, localTime, parameters);

                    // Apply zone transforms
                    int zoneIndex = block.ZoneIndex;
                    if (zoneManager != null)
                    {
                        points = zoneManager.TransformPoints(zoneIndex, points);
                    }

                    // Accumulate points for blending
                    if (!zonePoints.ContainsKey(zoneIndex))
                    {
                        zonePoints[zoneIndex] = new List<LaserPoint>();
                    }

                    // Additive color blending for overlapping cues on same zone
                    if (zonePoints[zoneIndex].Count > 0)
                    {
                        points = BlendPointsAdditive(zonePoints[zoneIndex], points);
                        zonePoints[zoneIndex] = points;
                    }
                    else
                    {
                        zonePoints[zoneIndex].AddRange(points);
                    }
                }
            }

            // Clear zones that were active last frame but have no output now
            foreach (int prevZone in _activeZonesLastFrame)
            {
                if (!zonePoints.ContainsKey(prevZone))
                {
                    if (previewManager != null)
                    {
                        var renderer = previewManager.GetRenderer(prevZone);
                        renderer?.Clear();
                    }
                    if (artNetManager != null)
                    {
                        byte[] blackout = ConvertPointsToDmx(prevZone, new List<LaserPoint>());
                        artNetManager.SendDmx(prevZone, blackout);
                    }
                }
            }

            _activeZonesLastFrame.Clear();

            // Send output to preview and ArtNet
            foreach (var kvp in zonePoints)
            {
                int zoneIndex = kvp.Key;
                var points = kvp.Value;

                _activeZonesLastFrame.Add(zoneIndex);

                // Send to preview renderer
                if (previewManager != null)
                {
                    previewManager.UpdatePreview(zoneIndex, points);
                }

                // Convert to DMX and send via ArtNet
                if (artNetManager != null)
                {
                    byte[] dmxFrame = ConvertPointsToDmx(zoneIndex, points);
                    artNetManager.SendDmx(zoneIndex, dmxFrame);
                }
            }
        }

        /// <summary>
        /// Evaluates live-triggered cues from the CueGridManager.
        /// </summary>
        private void EvaluateLiveCues(float currentTime)
        {
            var liveCues = cueGridManager.ActiveLiveCues;
            if (liveCues == null)
                return;

            for (int i = liveCues.Count - 1; i >= 0; i--)
            {
                var liveCue = liveCues[i];
                if (!liveCue.isActive || liveCue.cue == null)
                    continue;

                var parameters = PatternParameters.FromCue(liveCue.cue);
                float localTime = currentTime - liveCue.triggerTime;
                List<LaserPoint> points = GeneratePatternPoints(liveCue.cue.PatternType, localTime, parameters);

                if (zoneManager != null)
                {
                    points = zoneManager.TransformPoints(liveCue.zoneIndex, points);
                }

                if (previewManager != null)
                {
                    previewManager.UpdatePreview(liveCue.zoneIndex, points);
                }

                if (artNetManager != null)
                {
                    byte[] dmxFrame = ConvertPointsToDmx(liveCue.zoneIndex, points);
                    artNetManager.SendDmx(liveCue.zoneIndex, dmxFrame);
                }
            }
        }

        /// <summary>
        /// Generates pattern points using the PatternFactory for the given pattern type.
        /// </summary>
        private List<LaserPoint> GeneratePatternPoints(LaserPatternType patternType, float time, PatternParameters parameters)
        {
            var points = new List<LaserPoint>();
            ILaserPattern pattern = PatternFactory.Create(patternType);
            if (pattern != null)
            {
                pattern.Generate(time, parameters, points);
                return points;
            }

            // Fallback: return single center point
            points.Add(LaserPoint.Colored(parameters.position.X, parameters.position.Y, parameters.EffectiveColor()));
            return points;
        }

        /// <summary>
        /// Performs additive color blending between two lists of laser points.
        /// Points from the second list are merged into the first by adding color values.
        /// </summary>
        private List<LaserPoint> BlendPointsAdditive(List<LaserPoint> existing, List<LaserPoint> incoming)
        {
            var result = new List<LaserPoint>(existing);
            foreach (var point in incoming)
            {
                // Find closest existing point for blending, or just add
                bool blended = false;
                for (int i = 0; i < result.Count; i++)
                {
                    var ep = result[i];
                    float dist = new Vector2(ep.x, ep.y).DistanceTo(new Vector2(point.x, point.y));
                    if (dist < 0.01f)
                    {
                        // Additive color blend
                        result[i] = new LaserPoint(
                            ep.x, ep.y,
                            Mathf.Clamp(ep.r + point.r, 0f, 1f),
                            Mathf.Clamp(ep.g + point.g, 0f, 1f),
                            Mathf.Clamp(ep.b + point.b, 0f, 1f),
                            ep.blanking && point.blanking
                        );
                        blended = true;
                        break;
                    }
                }

                if (!blended)
                {
                    result.Add(point);
                }
            }

            return result;
        }

        /// <summary>
        /// Converts a list of laser points to an FB4 DMX frame for the given zone.
        /// Uses the first point's position and average color to populate the FB4 channel map.
        /// </summary>
        public byte[] ConvertPointsToDmx(int zoneIndex, List<LaserPoint> points)
        {
            if (points == null || points.Count == 0)
            {
                return FB4ChannelMap.BuildDmxFrame(
                    false, 0, 0f, 0f, 0f, 0f, 0f,
                    Colors.Black, 0.5f, 0, 0f, 0f, 0.5f
                );
            }

            // Calculate average position and color from points
            float avgX = 0f, avgY = 0f;
            float avgR = 0f, avgG = 0f, avgB = 0f;
            int visibleCount = 0;

            foreach (var point in points)
            {
                if (!point.blanking)
                {
                    avgX += point.x;
                    avgY += point.y;
                    avgR += point.r;
                    avgG += point.g;
                    avgB += point.b;
                    visibleCount++;
                }
            }

            if (visibleCount > 0)
            {
                avgX /= visibleCount;
                avgY /= visibleCount;
                avgR /= visibleCount;
                avgG /= visibleCount;
                avgB /= visibleCount;
            }

            Color avgColor = new Color(
                Mathf.Clamp(avgR, 0f, 1f),
                Mathf.Clamp(avgG, 0f, 1f),
                Mathf.Clamp(avgB, 0f, 1f)
            );

            return FB4ChannelMap.BuildDmxFrame(
                enabled: visibleCount > 0,
                pattern: 0,
                x: avgX,
                y: avgY,
                sizeX: 0.5f,
                sizeY: 0.5f,
                rotation: 0f,
                color: avgColor,
                scanSpeed: 0.5f,
                effect: 0,
                effectSpeed: 0f,
                effectSize: 0f,
                zoom: 0.5f
            );
        }

        /// <summary>
        /// Computes a 0-1 fade multiplier for a block based on its fade in/out durations.
        /// </summary>
        private float ComputeFadeMultiplier(LaserCueBlock block, float currentTime)
        {
            float localTime = currentTime - block.StartTime;
            float mult = 1f;

            if (block.FadeInDuration > 0f && localTime < block.FadeInDuration)
            {
                mult *= Mathf.Clamp(localTime / block.FadeInDuration, 0f, 1f);
            }

            if (block.FadeOutDuration > 0f)
            {
                float timeUntilEnd = block.Duration - localTime;
                if (timeUntilEnd < block.FadeOutDuration)
                {
                    mult *= Mathf.Clamp(timeUntilEnd / block.FadeOutDuration, 0f, 1f);
                }
            }

            return mult;
        }

        /// <summary>Saves the current show to a .tres resource file.</summary>
        public Error SaveShow(string path)
        {
            if (laserShow == null)
            {
                GD.PrintErr("[PlaybackManager] No show to save.");
                return Error.InvalidData;
            }

            // Sync tracks back to the show's TimelineBlocks before saving
            laserShow.TimelineBlocks.Clear();
            for (int i = 0; i < _tracks.Count; i++)
            {
                var track = _tracks[i];
                if (track.blocks == null) continue;
                foreach (var block in track.blocks)
                {
                    block.TrackIndex = i;
                    laserShow.TimelineBlocks.Add(block);
                }
            }

            GD.Print($"[PlaybackManager] Saving show '{laserShow.ShowName}' with {laserShow.TimelineBlocks.Count} blocks to {path}");

            var err = ResourceSaver.Save(laserShow, path);
            if (err == Error.Ok)
                GD.Print($"[PlaybackManager] Show saved successfully.");
            else
                GD.PrintErr($"[PlaybackManager] Failed to save show: {err}");
            return err;
        }

        /// <summary>Loads a show from a .tres resource file and binds it.</summary>
        public Error LoadShow(string path)
        {
            if (!ResourceLoader.Exists(path))
            {
                GD.PrintErr($"[PlaybackManager] File not found: {path}");
                return Error.FileNotFound;
            }

            var resource = ResourceLoader.Load<LaserShow>(path);
            if (resource == null)
            {
                GD.PrintErr($"[PlaybackManager] Failed to load show from {path}");
                return Error.CantOpen;
            }

            laserShow = resource;
            GD.Print($"[PlaybackManager] Loaded show '{laserShow.ShowName}', TimelineBlocks={laserShow.TimelineBlocks?.Count ?? 0}");

            // Rebuild tracks from loaded blocks
            _tracks.Clear();
            tracks.Clear();

            if (laserShow.TimelineBlocks != null)
            {
                var trackMap = new Dictionary<int, TimelineTrack>();
                foreach (var block in laserShow.TimelineBlocks)
                {
                    if (!trackMap.ContainsKey(block.TrackIndex))
                    {
                        var track = new TimelineTrack
                        {
                            trackName = $"Track {block.TrackIndex + 1}",
                            zoneIndex = block.ZoneIndex
                        };
                        trackMap[block.TrackIndex] = track;
                    }
                    trackMap[block.TrackIndex].AddBlock(block);
                }

                // Add tracks in index order
                var sortedKeys = new List<int>(trackMap.Keys);
                sortedKeys.Sort();
                foreach (var key in sortedKeys)
                {
                    _tracks.Add(trackMap[key]);
                    tracks.Add(trackMap[key]);
                }
            }

            GD.Print($"[PlaybackManager] Show loaded from {path}: {laserShow.ShowName}");
            return Error.Ok;
        }

        /// <summary>Starts or resumes playback of the current show.</summary>
        public void PlayShow()
        {
            if (laserShow == null)
            {
                GD.Print("[PlaybackManager] No show loaded — cannot play.");
                return;
            }

            isPlaying = true;
            isPaused = false;

            if (syncManager != null)
            {
                syncManager.Play();
                GD.Print($"[PlaybackManager] Playback started. tracks={_tracks.Count}, syncMgr={syncManager != null}, previewMgr={previewManager != null}");
            }
            else
            {
                GD.Print("[PlaybackManager] Playback started but NO SyncManager — time won't advance!");
            }
        }

        /// <summary>Stops playback and resets to the beginning.</summary>
        public void StopShow()
        {
            isPlaying = false;
            isPaused = false;

            if (syncManager != null)
            {
                syncManager.Stop();
            }

            // Clear all outputs
            if (previewManager != null)
            {
                previewManager.ClearAll();
            }
            _activeZonesLastFrame.Clear();

            GD.Print("[PlaybackManager] Playback stopped.");
        }

        /// <summary>Pauses playback at the current position.</summary>
        public void PauseShow()
        {
            if (!isPlaying)
                return;

            isPaused = true;

            if (syncManager != null)
            {
                syncManager.Pause();
            }

            GD.Print("[PlaybackManager] Playback paused.");
        }
    }
}
