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

	private bool enabled = true;

	private bool enableChannel1 = false;
	private bool enableChannel1Length = false;
	private ushort waveLengthChannel1 = 0;
	private int channel1Duty = 0;
	private int channel1LengthTimer = 0;
	private int channel1InitialVolume = 0;
	private bool channel1EnvelopeDirection = false;
	private int channel1SweepPace = 0;
	private int channel1SweepTimer;
	private int channel1CurrentVolume;
	
	
	private bool enableChannel2 = false;
	private bool enableChannel2Length = false;
	private ushort waveLengthChannel2 = 0;
	private int channel2Duty = 0;
	private int channel2LengthTimer = 0;
	private int channel2InitialVolume = 0;
	private bool channel2EnvelopeDirection = false;
	private int channel2SweepPace = 0;
	private int channel2SweepTimer;
	private int channel2CurrentVolume;

	private bool enableChannel3 = false;
	private ushort waveLengthChannel3 = 0;

	private bool enableChannel4 = false;

	public byte NR11
	{
		get => (byte)((channel1Duty & 0b11) << 6);

		set
		{
			channel1Duty = value >> 6;
			channel1LengthTimer = 64 - (value & 0b111111);
		}
	}

	public byte NR12
	{
		get
		{
			byte value = 0;

			value |= (byte)(channel1InitialVolume << 4);

			if (channel1EnvelopeDirection)
				value |= 1 << 3;

			value |= (byte)channel1SweepPace;

			return value;
		}

		set
		{
			channel1InitialVolume = value >> 4;
			channel1CurrentVolume = channel1InitialVolume;
			channel1EnvelopeDirection = (value & (1 << 3)) != 0;
			channel1SweepPace = value & 0b111;
		}
	}
	
	public byte NR13
	{
		set => waveLengthChannel1 = (ushort)((waveLengthChannel1 & 0b0000011100000000) | value);
	}

	public byte NR14
	{
		get
		{
			byte value = 0;

			if (enableChannel1Length)
				value |= (1 << 6);

			return value;
		}

		set
		{
			waveLengthChannel1 = (ushort)((waveLengthChannel1 & 0xFF) | ((value & 0b111) << 8));

			// Trigger event
			if ((value & (1 << 7)) != 0)
			{
				channel1SweepTimer = channel1SweepPace;
				channel1CurrentVolume = channel1InitialVolume;
				enableChannel1 = true;
			}

			enableChannel1Length = (value & (1 << 6)) != 0;
		}
	}

	public byte NR21
	{
		get => (byte)((channel2Duty & 0b11) << 6);

		set
		{
			channel2Duty = value >> 6;
			channel2LengthTimer = 64 - (value & 0b111111);
		}
	}

	public byte NR22
	{
		get
		{
			byte value = 0;

			value |= (byte)(channel2InitialVolume << 4);

			if (channel2EnvelopeDirection)
				value |= 1 << 3;

			value |= (byte)channel2SweepPace;

			return value;
		}

		set
		{
			channel2InitialVolume = value >> 4;
			channel2CurrentVolume = channel2InitialVolume;
			channel2EnvelopeDirection = (value & (1 << 3)) != 0;
			channel2SweepPace = value & 0b111;
		}
	}

	public byte NR23
	{
		set => waveLengthChannel2 = (ushort)((waveLengthChannel2 & 0b0000011100000000) | value);
	}

	public byte NR24
	{
		get
		{
			byte value = 0;

			if (enableChannel1Length)
				value |= (1 << 6);

			return value;
		}

		set
		{
			waveLengthChannel2 = (ushort)((waveLengthChannel2 & 0xFF) | ((value & 0b111) << 8));

			// Trigger event
			if ((value & (1 << 7)) != 0)
			{
				channel2SweepTimer = channel2SweepPace;
				channel2CurrentVolume = channel2InitialVolume;
				enableChannel2 = true;
			}

			enableChannel2Length = (value & (1 << 6)) != 0;
		}
	}

	public byte NR33
	{
		set => waveLengthChannel3 = (ushort)((waveLengthChannel3 & 0b0000011100000000) | value);
	}

	public byte NR34
	{
		get => 0xFF; // TODO

		set
		{
			waveLengthChannel3 = (ushort)((waveLengthChannel3 & 0xFF) | ((value & 0b111) << 8));

			if ((value & (1 << 7)) != 0)
				enableChannel3 = true;
		}
	}

	public byte NR44
	{
		get => 0xFF; // TODO

		set
		{
			if ((value & (1 << 7)) != 0)
				enableChannel4 = true;
		}
	}

	public byte NR52
	{
		get
		{
			byte value = 0;

			if (enableChannel1)
				value |= 1 << bitEnableChannel1;

			if (enableChannel2)
				value |= 1 << bitEnableChannel2;

			if (enableChannel3)
				value |= 1 << bitEnableChannel3;

			if (enableChannel4)
				value |= 1 << bitEnableChannel4;

			if (enabled)
				value |= 1 << bitEnabled;

			return value;
		}

		set => enabled = (value & (1 << bitEnabled)) != 0;
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
				if (enableChannel1Length && channel1LengthTimer > 0)
				{
					channel1LengthTimer--;

					if (channel1LengthTimer <= 0)
					{
						enableChannel1 = false;
					}
				}

				if (enableChannel2Length && channel2LengthTimer > 0)
				{
					channel2LengthTimer--;
					if (channel2LengthTimer <= 0)
						enableChannel2 = false;
				}
			}

			if (fsClock == 7)
			{
				if (channel1SweepPace != 0 && channel1SweepTimer != 0)
				{
					channel1SweepTimer--;

					if (channel1SweepTimer == 0)
					{
						channel1SweepTimer = channel1SweepPace;

						if (channel1EnvelopeDirection) // Sweep increase
						{
							if (channel1CurrentVolume < 0xF)
								channel1CurrentVolume++;
						}
						else // Sweep decrease
						{
							if (channel1CurrentVolume > 0)
								channel1CurrentVolume--;
						}
					}
				}
				
				if (channel2SweepPace != 0 && channel2SweepTimer != 0)
				{
					channel2SweepTimer--;

					if (channel2SweepTimer == 0)
					{
						channel2SweepTimer = channel2SweepPace;

						if (channel1EnvelopeDirection) // Sweep increase
						{
							if (channel2CurrentVolume < 0xF)
								channel2CurrentVolume++;
						}
						else // Sweep decrease
						{
							if (channel2CurrentVolume > 0)
								channel2CurrentVolume--;
						}
					}
				}
			}

			fsClock++;
			if (fsClock == 8)
				fsClock = 0;
		}

		if (channel1FreqTimer == 0)
		{
			channel1FreqTimer = (2048 - waveLengthChannel1) * 4;
			if (channel1DutyCycle++ > 7)
				channel1DutyCycle = 0;
		}

		if (channel2FreqTimer == 0)
		{
			channel2FreqTimer = (2048 - waveLengthChannel2) * 4;
			if (channel2DutyCycle++ > 7)
				channel2DutyCycle = 0;
		}

		channel1FreqTimer--;
		channel2FreqTimer--;

		{
			var amplitude = 0;

			if (enableChannel1)
				amplitude = (waveDutyTable[channel1Duty] >> (7 - channel1DutyCycle)) & 1;

			amplitude *= channel1CurrentVolume;
			amplitude *= 500;
			channel1Amplitudes[ampI] = (short)amplitude;
		}

		{
			var amplitude = 0;

			if (enableChannel2)
				amplitude = (waveDutyTable[channel2Duty] >> (7 - channel2DutyCycle)) & 1;
			
			amplitude *= channel2CurrentVolume;
			amplitude *= 500;
			channel2Amplitudes[ampI] = (short)amplitude;
		}

		ampI++;

		if (ampI == ampsPerSample)
		{
			var last = ampI - 1;
			ampI = 0;

			short sample = 0;

			{
				if (enableChannel1)
					sample += channel1Amplitudes[last];

				if (enableChannel2)
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
				sample = nextSample;
			else
				sample = 0;

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
