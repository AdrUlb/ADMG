using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ASFW.Extension.Audio.Players;
using ASFW.Extension.Audio.Sources;

namespace ADMG;

public sealed class APU : IAudioSource, IDisposable
{

	public string Name => "GameBoy APU";

	public ushort Channels => 1;

	public uint SampleRate => 44151;

	public uint BytesPerSecond => (uint)(BitsPerSample / 8 * Channels * SampleRate);

	public ushort BlockAlign => (ushort)(BitsPerSample / 8 * Channels);

	public ushort BitsPerSample => 16;

	public float Volume { get; set; }

	private const int bitEnableChannel1 = 0;
	private const int bitEnableChannel2 = 1;
	private const int bitEnableChannel3 = 2;
	private const int bitEnableChannel4 = 3;
	private const int bitEnabled = 7;

	public bool Enabled = true;

	public bool EnableChannel1 = false;
	public bool EnableChannel1Length = false;
	public ushort WaveLengthChannel1 = 0;
	public int Channel1Duty = 0;
	public int Channel1LengthTimer = 0;

	public bool EnableChannel2 = false;
	public bool EnableChannel2Length = false;
	public ushort WaveLengthChannel2 = 0;
	public int Channel2Duty = 0;
	public int Channel2LengthTimer = 0;

	public bool EnableChannel3 = false;
	public ushort WaveLengthChannel3 = 0;

	public bool EnableChannel4 = false;

	public byte NR11
	{
		get => (byte)((Channel1Duty & 0b11) << 6);

		set
		{
			Channel1Duty = value >> 6;
			Channel1LengthTimer = 64 - (value & 0b111111);
		}
	}

	public byte NR13
	{
		set => WaveLengthChannel1 = (ushort)((WaveLengthChannel1 & 0b0000011100000000) | value);
	}

	public byte NR14
	{
		get
		{
			byte value = 0;

			if (EnableChannel1Length)
				value |= (1 << 6);

			return value;
		}

		set
		{
			WaveLengthChannel1 = (ushort)((WaveLengthChannel1 & 0xFF) | ((value & 0b111) << 8));

			if ((value & (1 << 7)) != 0)
				EnableChannel1 = true;

			EnableChannel1Length = (value & (1 << 6)) != 0;
		}
	}

	public byte NR21
	{
		get => (byte)((Channel2Duty & 0b11) << 6);

		set
		{
			Channel2Duty = value >> 6;
			Channel2LengthTimer = 64 - (value & 0b111111);
		}
	}

	public byte NR23
	{
		set => WaveLengthChannel2 = (ushort)((WaveLengthChannel2 & 0b0000011100000000) | value);
	}

	public byte NR24
	{
		get
		{
			byte value = 0;

			if (EnableChannel1Length)
				value |= (1 << 6);

			return value;
		}

		set
		{
			WaveLengthChannel2 = (ushort)((WaveLengthChannel2 & 0xFF) | ((value & 0b111) << 8));

			if ((value & (1 << 7)) != 0)
				EnableChannel2 = true;

			EnableChannel2Length = (value & (1 << 6)) != 0;
		}
	}

	public byte NR33
	{
		set => WaveLengthChannel3 = (ushort)((WaveLengthChannel3 & 0b0000011100000000) | value);
	}

	public byte NR34
	{
		get => 0xFF; // TODO

		set
		{
			WaveLengthChannel3 = (ushort)((WaveLengthChannel3 & 0xFF) | ((value & 0b111) << 8));

			if ((value & (1 << 7)) != 0)
				EnableChannel3 = true;
		}
	}

	public byte NR44
	{
		get => 0xFF; // TODO

		set
		{
			if ((value & (1 << 7)) != 0)
				EnableChannel4 = true;
		}
	}

	public byte NR52
	{
		get
		{
			byte value = 0;

			if (EnableChannel1)
				value |= 1 << bitEnableChannel1;

			if (EnableChannel2)
				value |= 1 << bitEnableChannel2;

			if (EnableChannel3)
				value |= 1 << bitEnableChannel3;

			if (EnableChannel4)
				value |= 1 << bitEnableChannel4;

			if (Enabled)
				value |= 1 << bitEnabled;

			return value;
		}

		set => Enabled = (value & (1 << bitEnabled)) != 0;
	}

	//private short amplitude = 0;

	private static readonly byte[] waveDutyTable =
	{
		0b00000001,
		0b00000011,
		0b00001111,
		0b11111100
	};

	private const int ampsPerSample = 4194304 / 44100;

	private int channel1DutyCycle = 0;
	private readonly short[] channel1Amplitudes = new short[ampsPerSample];
	private int channel1FreqTimer = 0;

	private int channel2DutyCycle = 0;
	private readonly short[] channel2Amplitudes = new short[ampsPerSample];
	private int channel2FreqTimer = 0;

	private int ampI = 0;

	private readonly AudioPlayer? player;

	private readonly ConcurrentQueue<short> playbackQueue = new();

	private int fsWait = 0;
	private int fsClock = 0;

	public APU()
	{
		AudioPlayer.TryCreate(this, out player);

		player?.Play();
	}

	public void Tick()
	{
		fsWait++;

		if (fsWait == 8192)
		{
			fsWait = 0;

			if (fsClock % 2 == 0)
			{
				if (EnableChannel1Length && Channel1LengthTimer > 0)
				{
					Channel1LengthTimer--;

					if (Channel1LengthTimer <= 0)
					{
						EnableChannel1 = false;
					}
				}

				if (EnableChannel2Length && Channel2LengthTimer > 0)
				{
					Channel2LengthTimer--;
					if (Channel2LengthTimer <= 0)
						EnableChannel2 = false;
				}
			}

			fsClock++;
			if (fsClock == 8)
				fsClock = 0;
		}

		if (channel1FreqTimer == 0)
		{
			channel1FreqTimer = (2048 - WaveLengthChannel1) * 4;
			if (channel1DutyCycle++ > 7)
				channel1DutyCycle = 0;
		}

		if (channel2FreqTimer == 0)
		{
			channel2FreqTimer = (2048 - WaveLengthChannel2) * 4;
			if (channel2DutyCycle++ > 7)
				channel2DutyCycle = 0;
		}

		channel1FreqTimer--;
		channel2FreqTimer--;

		{
			short amplitude = 0;
			if (EnableChannel1)
				amplitude = (short)(2000 * ((waveDutyTable[Channel1Duty] >> (7 - channel1DutyCycle)) & 1));
			channel1Amplitudes[ampI] = amplitude;
		}

		{
			short amplitude = 0;

			if (EnableChannel2)
				amplitude = (short)(2000 * ((waveDutyTable[Channel2Duty] >> (7 - channel2DutyCycle)) & 1));
			channel2Amplitudes[ampI] = amplitude;
		}

		ampI++;

		if (ampI == ampsPerSample)
		{
			var last = ampI - 1;
			ampI = 0;

			short sample = 0;

			{
				if (EnableChannel1)
					sample += channel1Amplitudes[last];

				if (EnableChannel2)
					sample += channel2Amplitudes[last];
			}
			
			playbackQueue.Enqueue(sample);
		}
	}

	short sample = 0;

	public int GetNextBlock(Span<byte> buffer, bool rewind = false)
	{
		var i = 0;

		while (i < buffer.Length / 2)
		{
			sample *= 2;
			if (playbackQueue.TryDequeue(out var nextSample))
				sample = (short)(nextSample * 2);
			//else
			//	sample = 0;

			var lo = (byte)sample;
			var hi = (byte)(sample >> 8);

			buffer[i * 2] = lo;
			buffer[i * 2 + 1] = hi;
			i++;
		}

		return buffer.Length;
	}

	~APU() => Dispose();

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		player?.Stop();
		player?.Dispose();
	}

}
