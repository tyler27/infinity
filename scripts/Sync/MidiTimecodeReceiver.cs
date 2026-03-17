using Godot;

namespace LazerSystem.Sync
{
    /// <summary>
    /// Receives and decodes MIDI Time Code (MTC) quarter-frame messages
    /// to reconstruct SMPTE timecode. Requires an external MIDI library
    /// (e.g., RtMidi or similar) to provide raw MIDI input.
    /// </summary>
    public partial class MidiTimecodeReceiver : Node
    {
        public enum SmpteFrameRate
        {
            Fps24,
            Fps25,
            Fps30,
            Fps30Drop
        }

        [ExportGroup("Settings")]
        [Export] private SmpteFrameRate _frameRate = SmpteFrameRate.Fps30;

        [ExportGroup("Status (Read Only)")]
        [Export] private int _hours;
        [Export] private int _minutes;
        [Export] private int _seconds;
        [Export] private int _frames;
        [Export] private bool _receiving;

        // MTC quarter-frame reconstruction state.
        private int[] _quarterFrameData = new int[8];
        private int _quarterFrameCount;
        private double _lastMessageTime;
        private const float TIMEOUT = 2f;

        /// <summary>Current decoded time in seconds.</summary>
        public float CurrentTime => SmpteToSeconds(_hours, _minutes, _seconds, _frames);

        /// <summary>Whether MTC messages are currently being received.</summary>
        public bool IsReceiving => _receiving;

        public SmpteFrameRate FrameRate
        {
            get => _frameRate;
            set => _frameRate = value;
        }

        public override void _Process(double delta)
        {
            // Mark as not receiving if we haven't gotten a message recently.
            double now = Time.GetTicksMsec() / 1000.0;
            if (_receiving && now - _lastMessageTime > TIMEOUT)
            {
                _receiving = false;
            }
        }

        /// <summary>
        /// Call this method from your MIDI input callback when an MTC quarter-frame
        /// message is received (status byte 0xF1).
        /// </summary>
        /// <param name="dataByte">The data byte of the quarter-frame message.</param>
        public void ProcessQuarterFrameMessage(byte dataByte)
        {
            int piece = (dataByte >> 4) & 0x07;
            int nibble = dataByte & 0x0F;

            _quarterFrameData[piece] = nibble;
            _quarterFrameCount++;
            _lastMessageTime = Time.GetTicksMsec() / 1000.0;
            _receiving = true;

            // A complete timecode is reconstructed after 8 quarter-frame messages.
            if (_quarterFrameCount >= 8)
            {
                _quarterFrameCount = 0;
                ReconstructTimecode();
            }
        }

        /// <summary>
        /// Call this method to directly set the timecode (e.g., from a full-frame MTC message).
        /// </summary>
        public void SetTimecode(int h, int m, int s, int f)
        {
            _hours = h;
            _minutes = m;
            _seconds = s;
            _frames = f;
            _receiving = true;
            _lastMessageTime = Time.GetTicksMsec() / 1000.0;
        }

        private void ReconstructTimecode()
        {
            // MTC quarter-frame encoding:
            // Piece 0: frames low nibble
            // Piece 1: frames high nibble
            // Piece 2: seconds low nibble
            // Piece 3: seconds high nibble
            // Piece 4: minutes low nibble
            // Piece 5: minutes high nibble
            // Piece 6: hours low nibble
            // Piece 7: hours high nibble + SMPTE type (bits 5-6)

            _frames = _quarterFrameData[0] | (_quarterFrameData[1] << 4);
            _seconds = _quarterFrameData[2] | (_quarterFrameData[3] << 4);
            _minutes = _quarterFrameData[4] | (_quarterFrameData[5] << 4);

            int hoursByte = _quarterFrameData[6] | (_quarterFrameData[7] << 4);
            _hours = hoursByte & 0x1F; // Lower 5 bits are hours.

            // Bits 5-6 of piece 7 encode the SMPTE type.
            int smpteType = (_quarterFrameData[7] >> 1) & 0x03;
            switch (smpteType)
            {
                case 0: _frameRate = SmpteFrameRate.Fps24; break;
                case 1: _frameRate = SmpteFrameRate.Fps25; break;
                case 2: _frameRate = SmpteFrameRate.Fps30Drop; break;
                case 3: _frameRate = SmpteFrameRate.Fps30; break;
            }
        }

        /// <summary>
        /// Converts SMPTE timecode to seconds using the current frame rate.
        /// </summary>
        private float SmpteToSeconds(int h, int m, int s, int f)
        {
            float fps = GetFrameRateValue();
            return h * 3600f + m * 60f + s + f / fps;
        }

        private float GetFrameRateValue()
        {
            switch (_frameRate)
            {
                case SmpteFrameRate.Fps24:     return 24f;
                case SmpteFrameRate.Fps25:     return 25f;
                case SmpteFrameRate.Fps30:     return 30f;
                case SmpteFrameRate.Fps30Drop: return 29.97f;
                default:                       return 30f;
            }
        }
    }
}
