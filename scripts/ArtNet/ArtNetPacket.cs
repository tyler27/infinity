using System;
using System.Text;

namespace LazerSystem.ArtNet
{
    /// <summary>
    /// Static utility class for building Art-Net protocol packets.
    /// Art-Net is a protocol for transmitting DMX512 data over UDP/IP networks.
    /// </summary>
    public static class ArtNetPacket
    {
        public const int PORT = 6454;
        public const string HEADER = "Art-Net\0";
        public const int PROTOCOL_VERSION = 14;
        public const int MAX_DMX_LENGTH = 512;

        // Opcodes
        public const ushort OPCODE_POLL = 0x2000;
        public const ushort OPCODE_DMX = 0x5000;
        public const ushort OPCODE_TIMECODE = 0x9700;

        private static readonly byte[] HeaderBytes = Encoding.ASCII.GetBytes(HEADER);

        private static byte _sequence = 0;

        /// <summary>
        /// Builds an ArtDmx (opcode 0x5000) packet for transmitting DMX512 data.
        /// Packet layout:
        ///   [0..7]   Art-Net header ("Art-Net\0")
        ///   [8..9]   Opcode low, high (little-endian: 0x00, 0x50)
        ///   [10..11] Protocol version high, low (big-endian: 0x00, 0x0E)
        ///   [12]     Sequence number (0 = disabled, 1-255 = sequenced)
        ///   [13]     Physical port
        ///   [14..15] Universe low, high (little-endian)
        ///   [16..17] DMX data length high, low (big-endian)
        ///   [18..]   DMX channel data (up to 512 bytes)
        /// </summary>
        /// <param name="universe">The Art-Net universe (0-32767).</param>
        /// <param name="dmxData">DMX channel data, up to 512 bytes.</param>
        /// <returns>Complete ArtDmx packet as byte array.</returns>
        public static byte[] BuildArtDmxPacket(int universe, byte[] dmxData)
        {
            if (dmxData == null)
                throw new ArgumentNullException(nameof(dmxData));

            int dmxLength = Math.Min(dmxData.Length, MAX_DMX_LENGTH);
            // ArtNet spec requires even-length DMX data
            if (dmxLength % 2 != 0)
                dmxLength++;

            int packetLength = 18 + dmxLength;
            byte[] packet = new byte[packetLength];

            // Header: "Art-Net\0"
            Array.Copy(HeaderBytes, 0, packet, 0, 8);

            // Opcode: 0x5000 little-endian -> 0x00, 0x50
            packet[8] = (byte)(OPCODE_DMX & 0xFF);
            packet[9] = (byte)((OPCODE_DMX >> 8) & 0xFF);

            // Protocol version: 14 big-endian -> 0x00, 0x0E
            packet[10] = (byte)((PROTOCOL_VERSION >> 8) & 0xFF);
            packet[11] = (byte)(PROTOCOL_VERSION & 0xFF);

            // Sequence (auto-increment, wraps 1-255; 0 = disabled)
            _sequence++;
            if (_sequence == 0)
                _sequence = 1;
            packet[12] = _sequence;

            // Physical port
            packet[13] = 0;

            // Universe: little-endian
            packet[14] = (byte)(universe & 0xFF);
            packet[15] = (byte)((universe >> 8) & 0xFF);

            // DMX data length: big-endian
            packet[16] = (byte)((dmxLength >> 8) & 0xFF);
            packet[17] = (byte)(dmxLength & 0xFF);

            // DMX data
            Array.Copy(dmxData, 0, packet, 18, Math.Min(dmxData.Length, dmxLength));

            return packet;
        }

        /// <summary>
        /// Builds an ArtPoll (opcode 0x2000) packet for device discovery.
        /// Packet layout:
        ///   [0..7]   Art-Net header
        ///   [8..9]   Opcode little-endian
        ///   [10..11] Protocol version big-endian
        ///   [12]     TalkToMe flags
        ///   [13]     Priority
        /// </summary>
        /// <returns>Complete ArtPoll packet as byte array.</returns>
        public static byte[] BuildArtPollPacket()
        {
            byte[] packet = new byte[14];

            // Header
            Array.Copy(HeaderBytes, 0, packet, 0, 8);

            // Opcode: 0x2000 little-endian
            packet[8] = (byte)(OPCODE_POLL & 0xFF);
            packet[9] = (byte)((OPCODE_POLL >> 8) & 0xFF);

            // Protocol version: big-endian
            packet[10] = (byte)((PROTOCOL_VERSION >> 8) & 0xFF);
            packet[11] = (byte)(PROTOCOL_VERSION & 0xFF);

            // TalkToMe: request diagnostics, send ArtPollReply on change
            packet[12] = 0x06;

            // Priority: low
            packet[13] = 0x00;

            return packet;
        }

        /// <summary>
        /// Builds an ArtTimeCode (opcode 0x9700) packet.
        /// Packet layout:
        ///   [0..7]   Art-Net header
        ///   [8..9]   Opcode little-endian
        ///   [10..11] Protocol version big-endian
        ///   [12..13] Filler (0x00, 0x00)
        ///   [14]     Frames
        ///   [15]     Seconds
        ///   [16]     Minutes
        ///   [17]     Hours
        ///   [18]     Type (0=Film 24fps, 1=EBU 25fps, 2=DF 29.97fps, 3=SMPTE 30fps)
        /// </summary>
        public static byte[] BuildArtTimeCodePacket(int hours, int minutes, int seconds, int frames, int type)
        {
            byte[] packet = new byte[19];

            // Header
            Array.Copy(HeaderBytes, 0, packet, 0, 8);

            // Opcode: 0x9700 little-endian
            packet[8] = (byte)(OPCODE_TIMECODE & 0xFF);
            packet[9] = (byte)((OPCODE_TIMECODE >> 8) & 0xFF);

            // Protocol version: big-endian
            packet[10] = (byte)((PROTOCOL_VERSION >> 8) & 0xFF);
            packet[11] = (byte)(PROTOCOL_VERSION & 0xFF);

            // Filler
            packet[12] = 0x00;
            packet[13] = 0x00;

            // Timecode fields
            packet[14] = (byte)(frames & 0xFF);
            packet[15] = (byte)(seconds & 0xFF);
            packet[16] = (byte)(minutes & 0xFF);
            packet[17] = (byte)(hours & 0xFF);
            packet[18] = (byte)(type & 0xFF);

            return packet;
        }
    }
}
