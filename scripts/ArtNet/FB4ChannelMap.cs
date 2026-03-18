using System;
using Godot;

namespace LazerSystem.ArtNet
{
	/// <summary>
	/// Static class mapping Pangolin FB4 DMX channels in standard DMX mode.
	/// Provides constants for channel offsets and helper methods to build DMX frames
	/// for controlling FB4 laser projectors.
	/// </summary>
	public static class FB4ChannelMap
	{
		// FB4 Standard Mode DMX Channel Assignments (1-indexed as per DMX convention)
		public const int CH_CONTROL     = 1;   // Control/Mode: 0=blackout, 255=enable
		public const int CH_PATTERN     = 2;   // Pattern select: 0-255
		public const int CH_X_POS       = 3;   // X position: 0-255, 128=center
		public const int CH_Y_POS       = 4;   // Y position: 0-255, 128=center
		public const int CH_X_SIZE      = 5;   // X size: 0-255
		public const int CH_Y_SIZE      = 6;   // Y size: 0-255
		public const int CH_ROTATION    = 7;   // Rotation: 0-255, 128=no rotation
		public const int CH_RED         = 8;   // Red intensity: 0-255
		public const int CH_GREEN       = 9;   // Green intensity: 0-255
		public const int CH_BLUE        = 10;  // Blue intensity: 0-255
		public const int CH_SCAN_SPEED  = 11;  // Scan speed: 0-255
		public const int CH_DRAW_MODE   = 12;  // Drawing mode / blanking
		public const int CH_EFFECT      = 13;  // Effect select
		public const int CH_EFFECT_SPEED = 14; // Effect speed
		public const int CH_EFFECT_SIZE = 15;  // Effect size
		public const int CH_ZOOM        = 16;  // Zoom: 0-255

		/// <summary>Total number of DMX channels used by FB4 in standard mode.</summary>
		public const int CHANNEL_COUNT = 16;

		/// <summary>
		/// Builds a complete DMX frame for an FB4 projector in standard mode.
		/// Float parameters in range [0..1] are mapped to DMX byte values [0..255].
		/// Position parameters map [-1..1] to [0..255] where 0 = center (128).
		/// </summary>
		/// <param name="enabled">True to enable output, false for blackout.</param>
		/// <param name="pattern">Pattern index (0-255).</param>
		/// <param name="x">X position, -1.0 to 1.0 (0.0 = center).</param>
		/// <param name="y">Y position, -1.0 to 1.0 (0.0 = center).</param>
		/// <param name="sizeX">X size, 0.0 to 1.0.</param>
		/// <param name="sizeY">Y size, 0.0 to 1.0.</param>
		/// <param name="rotation">Rotation, -1.0 to 1.0 (0.0 = no rotation).</param>
		/// <param name="color">RGB color.</param>
		/// <param name="scanSpeed">Scan speed, 0.0 to 1.0.</param>
		/// <param name="effect">Effect index (0-255).</param>
		/// <param name="effectSpeed">Effect speed, 0.0 to 1.0.</param>
		/// <param name="effectSize">Effect size, 0.0 to 1.0.</param>
		/// <param name="zoom">Zoom level, 0.0 to 1.0.</param>
		/// <returns>A 512-byte DMX frame with FB4 channels populated.</returns>
		public static byte[] BuildDmxFrame(
			bool enabled,
			int pattern,
			float x,
			float y,
			float sizeX,
			float sizeY,
			float rotation,
			Color color,
			float scanSpeed,
			int effect,
			float effectSpeed,
			float effectSize,
			float zoom)
		{
			byte[] frame = new byte[512];
			FillDmxFrame(frame, enabled, pattern, x, y, sizeX, sizeY, rotation,
				color, scanSpeed, effect, effectSpeed, effectSize, zoom);
			return frame;
		}

		/// <summary>
		/// Fills an existing DMX frame buffer (avoids allocation).
		/// </summary>
		public static void FillDmxFrame(
			byte[] frame,
			bool enabled,
			int pattern,
			float x,
			float y,
			float sizeX,
			float sizeY,
			float rotation,
			Color color,
			float scanSpeed,
			int effect,
			float effectSpeed,
			float effectSize,
			float zoom)
		{
			// Ch1: Control - 0 for blackout, 255 for enabled
			frame[CH_CONTROL - 1] = enabled ? (byte)255 : (byte)0;

			// Ch2: Pattern select
			frame[CH_PATTERN - 1] = (byte)Math.Clamp(pattern, 0, 255);

			// Ch3: X position - map [-1..1] to [0..255], 0.0 -> 128
			frame[CH_X_POS - 1] = FloatToBipolar(x);

			// Ch4: Y position - map [-1..1] to [0..255], 0.0 -> 128
			frame[CH_Y_POS - 1] = FloatToBipolar(y);

			// Ch5: X size - map [0..1] to [0..255]
			frame[CH_X_SIZE - 1] = FloatToUnipolar(sizeX);

			// Ch6: Y size - map [0..1] to [0..255]
			frame[CH_Y_SIZE - 1] = FloatToUnipolar(sizeY);

			// Ch7: Rotation - map [-1..1] to [0..255], 0.0 -> 128
			frame[CH_ROTATION - 1] = FloatToBipolar(rotation);

			// Ch8-10: RGB color
			frame[CH_RED - 1] = (byte)Math.Round(Math.Clamp(color.R, 0f, 1f) * 255f);
			frame[CH_GREEN - 1] = (byte)Math.Round(Math.Clamp(color.G, 0f, 1f) * 255f);
			frame[CH_BLUE - 1] = (byte)Math.Round(Math.Clamp(color.B, 0f, 1f) * 255f);

			// Ch11: Scan speed
			frame[CH_SCAN_SPEED - 1] = FloatToUnipolar(scanSpeed);

			// Ch12: Drawing mode / blanking (default 0)
			frame[CH_DRAW_MODE - 1] = 0;

			// Ch13: Effect select
			frame[CH_EFFECT - 1] = (byte)Math.Clamp(effect, 0, 255);

			// Ch14: Effect speed
			frame[CH_EFFECT_SPEED - 1] = FloatToUnipolar(effectSpeed);

			// Ch15: Effect size
			frame[CH_EFFECT_SIZE - 1] = FloatToUnipolar(effectSize);

			// Ch16: Zoom
			frame[CH_ZOOM - 1] = FloatToUnipolar(zoom);
		}

		/// <summary>
		/// Sets a single channel value in a DMX frame.
		/// Channel is 1-indexed per DMX convention.
		/// </summary>
		/// <param name="frame">The 512-byte DMX frame to modify.</param>
		/// <param name="channel">DMX channel number (1-512).</param>
		/// <param name="value">Byte value to set (0-255).</param>
		public static void SetChannelValue(byte[] frame, int channel, byte value)
		{
			if (frame == null || frame.Length < 512)
			{
				GD.PushError("[FB4ChannelMap] Invalid DMX frame.");
				return;
			}

			if (channel < 1 || channel > 512)
			{
				GD.PushError($"[FB4ChannelMap] Channel {channel} out of range (1-512).");
				return;
			}

			frame[channel - 1] = value;
		}

		/// <summary>
		/// Maps a unipolar float [0..1] to a DMX byte [0..255].
		/// </summary>
		private static byte FloatToUnipolar(float value)
		{
			return (byte)Math.Round(Math.Clamp(value, 0f, 1f) * 255f);
		}

		/// <summary>
		/// Maps a bipolar float [-1..1] to a DMX byte [0..255] where 0.0 maps to 128.
		/// </summary>
		private static byte FloatToBipolar(float value)
		{
			float clamped = Math.Clamp(value, -1f, 1f);
			float mapped = (clamped + 1f) * 0.5f * 255f;
			return (byte)Math.Round(mapped);
		}
	}
}
