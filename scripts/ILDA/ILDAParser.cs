using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LazerSystem.Core;
using Godot;

namespace LazerSystem.ILDA
{
    /// <summary>
    /// Parses ILDA standard laser show files (.ild).
    /// Supports format codes 0 (3D indexed), 1 (2D indexed), 4 (3D true color), and 5 (2D true color).
    /// </summary>
    public static class ILDAParser
    {
        private const int HeaderSize = 32;
        private static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes("ILDA");

        /// <summary>
        /// Parses an ILDA file from disk.
        /// </summary>
        public static ILDAFile Parse(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("ILDA file not found.", filePath);

            byte[] data = File.ReadAllBytes(filePath);
            return Parse(data);
        }

        /// <summary>
        /// Parses an ILDA file from a byte array.
        /// </summary>
        public static ILDAFile Parse(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var file = new ILDAFile();
            int offset = 0;

            while (offset + HeaderSize <= data.Length)
            {
                // Validate magic bytes "ILDA"
                if (data[offset] != MagicBytes[0] || data[offset + 1] != MagicBytes[1] ||
                    data[offset + 2] != MagicBytes[2] || data[offset + 3] != MagicBytes[3])
                {
                    throw new FormatException($"Invalid ILDA magic at offset {offset}. Expected 'ILDA'.");
                }

                // Byte 7: format code
                int formatCode = data[offset + 7];

                // Bytes 8-15: name (8 bytes, null-padded ASCII)
                string sectionName = ReadString(data, offset + 8, 8);

                // Bytes 16-23: company name (8 bytes, null-padded ASCII)
                string companyName = ReadString(data, offset + 16, 8);

                // Bytes 24-25: total number of points/records (big-endian uint16)
                int totalPoints = ReadUInt16BE(data, offset + 24);

                // Bytes 26-27: frame number (big-endian uint16)
                int frameNumber = ReadUInt16BE(data, offset + 26);

                // Bytes 28-29: total frames (big-endian uint16) - informational
                // Bytes 30: projector number
                // Byte 31: reserved

                offset += HeaderSize;

                // A section with 0 points signals end-of-file
                if (totalPoints == 0)
                    break;

                // Skip palette sections (format 2 = color palette)
                if (formatCode == 2 || formatCode == 3)
                {
                    // Format 2: 3 bytes per entry (R, G, B)
                    // Format 3: reserved - skip same as format 2
                    offset += totalPoints * 3;
                    continue;
                }

                int bytesPerPoint = GetBytesPerPoint(formatCode);
                if (bytesPerPoint < 0)
                {
                    GD.PushWarning($"ILDA: Unknown format code {formatCode} at frame {frameNumber}. Skipping.");
                    break;
                }

                int requiredBytes = totalPoints * bytesPerPoint;
                if (offset + requiredBytes > data.Length)
                {
                    throw new FormatException(
                        $"ILDA: Unexpected end of data. Frame {frameNumber} expects {requiredBytes} bytes " +
                        $"for {totalPoints} points at offset {offset}, but only {data.Length - offset} bytes remain.");
                }

                var frame = new ILDAFrame
                {
                    frameName = sectionName,
                    frameNumber = frameNumber
                };

                for (int i = 0; i < totalPoints; i++)
                {
                    ILDAPoint point = ParsePoint(data, offset, formatCode);
                    frame.points.Add(point);
                    offset += bytesPerPoint;
                }

                file.frames.Add(frame);

                // Capture name and company from the first section
                if (file.frames.Count == 1)
                {
                    file.name = sectionName;
                    file.companyName = companyName;
                }
            }

            return file;
        }

        /// <summary>
        /// Converts an ILDAFrame into a list of LaserPoints suitable for the laser system.
        /// Z coordinate is discarded since LaserPoint is 2D.
        /// </summary>
        public static List<LaserPoint> FrameToPoints(ILDAFrame frame)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));

            var result = new List<LaserPoint>(frame.points.Count);

            for (int i = 0; i < frame.points.Count; i++)
            {
                ILDAPoint p = frame.points[i];
                result.Add(new LaserPoint(p.x, p.y, p.r, p.g, p.b, p.blanking));
            }

            return result;
        }

        private static int GetBytesPerPoint(int formatCode)
        {
            switch (formatCode)
            {
                case 0: return 8;   // 3D indexed:    X(2) Y(2) Z(2) Status(1) Color(1)
                case 1: return 6;   // 2D indexed:    X(2) Y(2) Status(1) Color(1)
                case 4: return 10;  // 3D true color: X(2) Y(2) Z(2) Status(1) B(1) G(1) R(1)
                case 5: return 8;   // 2D true color: X(2) Y(2) Status(1) B(1) G(1) R(1)
                default: return -1;
            }
        }

        private static ILDAPoint ParsePoint(byte[] data, int offset, int formatCode)
        {
            ILDAPoint point = default;

            switch (formatCode)
            {
                case 0: // 3D indexed color
                {
                    point.x = ReadInt16BE(data, offset) / 32767f;
                    point.y = ReadInt16BE(data, offset + 2) / 32767f;
                    point.z = ReadInt16BE(data, offset + 4) / 32767f;
                    byte status = data[offset + 6];
                    byte colorIndex = data[offset + 7];
                    point.blanking = (status & 0x40) != 0;
                    point.isLastPoint = (status & 0x80) != 0;
                    Color c = ILDAColorPalette.GetColor(colorIndex);
                    point.r = c.R;
                    point.g = c.G;
                    point.b = c.B;
                    break;
                }
                case 1: // 2D indexed color
                {
                    point.x = ReadInt16BE(data, offset) / 32767f;
                    point.y = ReadInt16BE(data, offset + 2) / 32767f;
                    point.z = 0f;
                    byte status = data[offset + 4];
                    byte colorIndex = data[offset + 5];
                    point.blanking = (status & 0x40) != 0;
                    point.isLastPoint = (status & 0x80) != 0;
                    Color c = ILDAColorPalette.GetColor(colorIndex);
                    point.r = c.R;
                    point.g = c.G;
                    point.b = c.B;
                    break;
                }
                case 4: // 3D true color
                {
                    point.x = ReadInt16BE(data, offset) / 32767f;
                    point.y = ReadInt16BE(data, offset + 2) / 32767f;
                    point.z = ReadInt16BE(data, offset + 4) / 32767f;
                    byte status = data[offset + 6];
                    point.blanking = (status & 0x40) != 0;
                    point.isLastPoint = (status & 0x80) != 0;
                    point.b = data[offset + 7] / 255f;
                    point.g = data[offset + 8] / 255f;
                    point.r = data[offset + 9] / 255f;
                    break;
                }
                case 5: // 2D true color
                {
                    point.x = ReadInt16BE(data, offset) / 32767f;
                    point.y = ReadInt16BE(data, offset + 2) / 32767f;
                    point.z = 0f;
                    byte status = data[offset + 4];
                    point.blanking = (status & 0x40) != 0;
                    point.isLastPoint = (status & 0x80) != 0;
                    point.b = data[offset + 5] / 255f;
                    point.g = data[offset + 6] / 255f;
                    point.r = data[offset + 7] / 255f;
                    break;
                }
            }

            return point;
        }

        /// <summary>
        /// Reads a big-endian signed 16-bit integer from the data array.
        /// </summary>
        private static short ReadInt16BE(byte[] data, int offset)
        {
            return (short)((data[offset] << 8) | data[offset + 1]);
        }

        /// <summary>
        /// Reads a big-endian unsigned 16-bit integer from the data array.
        /// </summary>
        private static int ReadUInt16BE(byte[] data, int offset)
        {
            return (data[offset] << 8) | data[offset + 1];
        }

        /// <summary>
        /// Reads a null-padded ASCII string from the data array.
        /// </summary>
        private static string ReadString(byte[] data, int offset, int length)
        {
            int end = offset + length;
            if (end > data.Length) end = data.Length;

            int actualLength = 0;
            for (int i = offset; i < end; i++)
            {
                if (data[i] == 0) break;
                actualLength++;
            }

            return Encoding.ASCII.GetString(data, offset, actualLength).TrimEnd();
        }
    }
}
