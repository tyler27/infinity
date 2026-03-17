using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Timeline
{
    /// <summary>
    /// Represents a single track in the laser show timeline.
    /// Each track is associated with a projection zone and contains
    /// a sorted list of cue blocks.
    /// </summary>
    [GlobalClass]
    public partial class TimelineTrack : Resource
    {
        [ExportGroup("Track Info")]
        [Export] public string trackName;
        [Export] public int zoneIndex;

        [ExportGroup("Cue Blocks")]
        [Export] public Godot.Collections.Array<LaserCueBlock> blocks = new();

        [ExportGroup("Mixing")]
        [Export] public bool muted;
        [Export] public bool solo;

        [Export(PropertyHint.Range, "0,1")]
        public float volume = 1f;

        // Internal typed list for convenience
        private List<LaserCueBlock> _blocksList;

        private List<LaserCueBlock> BlocksList
        {
            get
            {
                if (_blocksList == null)
                {
                    _blocksList = new List<LaserCueBlock>();
                    if (blocks != null)
                    {
                        foreach (var b in blocks)
                            _blocksList.Add(b);
                    }
                }
                return _blocksList;
            }
        }

        /// <summary>
        /// Returns all cue blocks that are active at the given time.
        /// A block is active when startTime <= time < startTime + duration.
        /// </summary>
        public List<LaserCueBlock> GetActiveBlocks(float time)
        {
            var active = new List<LaserCueBlock>();

            foreach (var block in BlocksList)
            {
                if (time >= block.StartTime && time < block.StartTime + block.Duration)
                {
                    active.Add(block);
                }
            }

            return active;
        }

        /// <summary>
        /// Adds a cue block to the track and re-sorts by start time.
        /// </summary>
        public void AddBlock(LaserCueBlock block)
        {
            if (block == null)
            {
                GD.Print("[TimelineTrack] Cannot add null block.");
                return;
            }

            blocks.Add(block);
            _blocksList = null; // Invalidate cache
            SortBlocks();
        }

        /// <summary>
        /// Removes a cue block from the track.
        /// </summary>
        public void RemoveBlock(LaserCueBlock block)
        {
            blocks.Remove(block);
            _blocksList = null; // Invalidate cache
        }

        /// <summary>
        /// Sorts all blocks by their start time in ascending order.
        /// </summary>
        public void SortBlocks()
        {
            var sorted = new List<LaserCueBlock>();
            foreach (var b in blocks)
                sorted.Add(b);
            sorted.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            blocks.Clear();
            foreach (var b in sorted)
                blocks.Add(b);
            _blocksList = null; // Invalidate cache
        }
    }
}
