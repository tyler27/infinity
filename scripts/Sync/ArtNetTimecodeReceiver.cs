using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Godot;

namespace LazerSystem.Sync
{
    /// <summary>
    /// Listens for Art-Net ArtTimeCode packets (opcode 0x9700) on UDP port 6454.
    /// Parses SMPTE timecode fields and exposes them as a float time in seconds.
    /// The UDP listener runs on a background thread; parsed values are dispatched
    /// to the main thread via a concurrent queue.
    /// </summary>
    public partial class ArtNetTimecodeReceiver : Node
    {
        private const int ARTNET_PORT = 6454;
        private const ushort OPCODE_TIMECODE = 0x9700;
        private const int MIN_TIMECODE_PACKET_LENGTH = 19;

        [ExportGroup("Settings")]
        [Export] private int _listenPort = ARTNET_PORT;
        [Export] private bool _autoStart = true;

        [ExportGroup("Status (Read Only)")]
        [Export] private int _hours;
        [Export] private int _minutes;
        [Export] private int _seconds;
        [Export] private int _frames;
        [Export] private int _timecodeType;
        [Export] private bool _receiving;

        private UdpClient _udpClient;
        private Thread _listenerThread;
        private volatile bool _running;
        private double _lastReceiveTime;
        private const float TIMEOUT = 2f;

        // Thread-safe queue for passing parsed timecode from background thread.
        private readonly object _queueLock = new object();
        private readonly Queue<TimecodeData> _incomingQueue = new Queue<TimecodeData>();

        private struct TimecodeData
        {
            public int Hours;
            public int Minutes;
            public int Seconds;
            public int Frames;
            public int Type;
        }

        /// <summary>Current decoded time in seconds.</summary>
        public float CurrentTime => SmpteToSeconds(_hours, _minutes, _seconds, _frames, _timecodeType);

        /// <summary>Whether timecode packets are currently being received.</summary>
        public bool IsReceiving => _receiving;

        public override void _Ready()
        {
            if (_autoStart)
            {
                StartListening();
            }
        }

        public override void _ExitTree()
        {
            StopListening();
        }

        public override void _Process(double delta)
        {
            // Drain the incoming queue on the main thread.
            double now = Time.GetTicksMsec() / 1000.0;

            lock (_queueLock)
            {
                while (_incomingQueue.Count > 0)
                {
                    TimecodeData tc = _incomingQueue.Dequeue();
                    _hours = tc.Hours;
                    _minutes = tc.Minutes;
                    _seconds = tc.Seconds;
                    _frames = tc.Frames;
                    _timecodeType = tc.Type;
                    _receiving = true;
                    _lastReceiveTime = now;
                }
            }

            // Timeout detection.
            if (_receiving && now - _lastReceiveTime > TIMEOUT)
            {
                _receiving = false;
            }
        }

        /// <summary>Starts the UDP listener on a background thread.</summary>
        public void StartListening()
        {
            if (_running) return;

            try
            {
                _udpClient = new UdpClient(_listenPort);
                _udpClient.Client.ReceiveTimeout = 1000;
            }
            catch (SocketException ex)
            {
                GD.PushError($"[ArtNetTimecodeReceiver] Failed to bind UDP port {_listenPort}: {ex.Message}");
                return;
            }

            _running = true;
            _listenerThread = new Thread(ListenerLoop)
            {
                Name = "ArtNetTimecodeListener",
                IsBackground = true
            };
            _listenerThread.Start();

            GD.Print($"[ArtNetTimecodeReceiver] Listening on UDP port {_listenPort}.");
        }

        /// <summary>Stops the UDP listener and cleans up resources.</summary>
        public void StopListening()
        {
            _running = false;

            if (_udpClient != null)
            {
                _udpClient.Close();
                _udpClient = null;
            }

            if (_listenerThread != null && _listenerThread.IsAlive)
            {
                _listenerThread.Join(2000);
                _listenerThread = null;
            }

            GD.Print("[ArtNetTimecodeReceiver] Stopped listening.");
        }

        private void ListenerLoop()
        {
            IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);

            while (_running)
            {
                try
                {
                    byte[] data = _udpClient.Receive(ref remoteEp);
                    if (data != null && data.Length >= MIN_TIMECODE_PACKET_LENGTH)
                    {
                        ParsePacket(data);
                    }
                }
                catch (SocketException)
                {
                    // Timeout or socket closed -- continue or exit.
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private void ParsePacket(byte[] data)
        {
            // Verify Art-Net header.
            if (data.Length < MIN_TIMECODE_PACKET_LENGTH) return;

            string header = Encoding.ASCII.GetString(data, 0, 7);
            if (header != "Art-Net") return;
            if (data[7] != 0x00) return; // Null terminator.

            // Opcode: little-endian at bytes 8-9.
            ushort opcode = (ushort)(data[8] | (data[9] << 8));
            if (opcode != OPCODE_TIMECODE) return;

            // Parse timecode fields.
            // [14] Frames, [15] Seconds, [16] Minutes, [17] Hours, [18] Type
            TimecodeData tc;
            tc.Frames = data[14];
            tc.Seconds = data[15];
            tc.Minutes = data[16];
            tc.Hours = data[17];
            tc.Type = data[18];

            lock (_queueLock)
            {
                _incomingQueue.Enqueue(tc);
            }
        }

        /// <summary>
        /// Converts SMPTE timecode to seconds.
        /// Type: 0=24fps, 1=25fps, 2=29.97fps, 3=30fps.
        /// </summary>
        private static float SmpteToSeconds(int h, int m, int s, int f, int type)
        {
            float fps;
            switch (type)
            {
                case 0:  fps = 24f;    break;
                case 1:  fps = 25f;    break;
                case 2:  fps = 29.97f; break;
                case 3:  fps = 30f;    break;
                default: fps = 30f;    break;
            }
            return h * 3600f + m * 60f + s + f / fps;
        }
    }
}
