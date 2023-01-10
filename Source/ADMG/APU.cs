// https://nightshade256.github.io/2021/03/27/gb-sound-emulation.html

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
	private ushort channel1WaveLength = 0;
	private int channel1Duty = 0;
	private int channel1LengthTimer = 0;
	private int channel1InitialVolume = 0;
	private bool channel1EnvelopeDirection = false;
	private int channel1EnvelopeSweepPace = 0;
	private int channel1EnvelopeSweepTimer;
	private int channel1CurrentVolume;
	private int channel1SweepPace;
	private bool channel1SweepDirection;
	private int channel1SweepSlope;

	private bool enableChannel2 = false;
	private bool enableChannel2Length = false;
	private ushort channel2WaveLength = 0;
	private int channel2Duty = 0;
	private int channel2LengthTimer = 0;
	private int channel2InitialVolume = 0;
	private bool channel2EnvelopeDirection = false;
	private int channel2EnvelopeSweepPace = 0;
	private int channel2EnvelopeSweepTimer;
	private int channel2CurrentVolume;

	private bool enableChannel3 = false;
	private bool enableChannel3Length = false;
	private ushort channel3WaveLength = 0;
	private int channel3LengthTimer = 0;
	private bool channel3DacEnable;
	private int channel3OutputLevel;

	private bool enableChannel4 = false;
	private int channel4LengthTimer = 0;
	private int channel4InitialVolume = 0;
	private bool channel4EnvelopeDirection = false;
	private int channel4EnvelopeSweepPace = 0;
	private int channel4EnvelopeSweepTimer;
	private int channel4CurrentVolume;
	private int channel4ClockShift;
	private bool channel4LfsrWidth;
	public int channel4ClockDivider;
	private bool enableChannel4Length;

	public byte NR10
	{
		get
		{
			byte value = 0;

			value |= (byte)((channel1SweepPace & 0b111) << 4);

			if (channel1SweepDirection)
				value |= (1 << 3);

			value |= (byte)(channel1SweepSlope & 0b111);

			return value;
		}

		set
		{
			channel1SweepPace = (value >> 4) & 0b111;
			channel1SweepDirection = (value & (1 << 3)) != 0;
			channel1SweepSlope = value & 0b111;
		}
	}

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

			value |= (byte)channel1EnvelopeSweepPace;

			return value;
		}

		set
		{
			channel1InitialVolume = value >> 4;
			channel1EnvelopeDirection = (value & (1 << 3)) != 0;
			channel1EnvelopeSweepPace = value & 0b111;
		}
	}

	public byte NR13
	{
		set => channel1WaveLength = (ushort)((channel1WaveLength & 0b0000011100000000) | value);
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
			channel1WaveLength = (ushort)((channel1WaveLength & 0xFF) | ((value & 0b111) << 8));

			// Trigger event
			if ((value & (1 << 7)) != 0)
			{
				enableChannel1 = true;
				
				channel1SweepTimer = channel1SweepPace != 0 ? channel1SweepPace : 8;
				channel1ShadowFrequency = channel1WaveLength;
				channel1EnvelopeSweepTimer = channel1EnvelopeSweepPace;
				
				channel1SweepEnabled = channel1SweepPace != 0 || channel1SweepSlope != 0;
				if (channel1SweepSlope != 0 && channel1WaveLength > 2047)
					enableChannel1 = false;
				
				channel1CurrentVolume = channel1InitialVolume;
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

			value |= (byte)channel2EnvelopeSweepPace;

			return value;
		}

		set
		{
			channel2InitialVolume = value >> 4;
			channel2EnvelopeDirection = (value & (1 << 3)) != 0;
			channel2EnvelopeSweepPace = value & 0b111;
		}
	}

	public byte NR23
	{
		set => channel2WaveLength = (ushort)((channel2WaveLength & 0b0000011100000000) | value);
	}

	public byte NR24
	{
		get
		{
			byte value = 0;

			if (enableChannel2Length)
				value |= (1 << 6);

			return value;
		}

		set
		{
			channel2WaveLength = (ushort)((channel2WaveLength & 0xFF) | ((value & 0b111) << 8));

			// Trigger event
			if ((value & (1 << 7)) != 0)
			{
				channel2EnvelopeSweepTimer = channel2EnvelopeSweepPace;
				channel2CurrentVolume = channel2InitialVolume;
				enableChannel2 = true;
			}

			enableChannel2Length = (value & (1 << 6)) != 0;
		}
	}

	public byte NR30
	{
		get
		{
			byte value = 0;

			if (channel3DacEnable)
				value |= 1 << 7;

			return value;
		}

		set => channel3DacEnable = (value & (1 << 7)) != 0;
	}

	public byte NR31
	{
		set => channel3LengthTimer = 256 - value;
	}

	public byte NR32
	{
		get => (byte)(channel3OutputLevel << 5);
		set => channel3OutputLevel = (value >> 5) & 0b11;
	}

	public byte NR33
	{
		set => channel3WaveLength = (ushort)((channel3WaveLength & 0b0000011100000000) | value);
	}

	public byte NR34
	{
		get
		{
			byte value = 0;

			if (enableChannel3Length)
				value |= (1 << 6);

			return value;
		}

		set
		{
			channel3WaveLength = (ushort)((channel3WaveLength & 0xFF) | ((value & 0b111) << 8));

			if ((value & (1 << 7)) != 0)
			{
				enableChannel3 = true;
			}

			enableChannel3Length = (value & (1 << 6)) != 0;
		}
	}

	public byte NR41
	{
		set => channel4LengthTimer = 64 - (value & 0b111111);
	}

	public byte NR42
	{
		get
		{
			byte value = 0;

			value |= (byte)(channel4InitialVolume << 4);

			if (channel4EnvelopeDirection)
				value |= 1 << 3;

			value |= (byte)channel4EnvelopeSweepPace;

			return value;
		}

		set
		{
			channel4InitialVolume = value >> 4;
			channel4CurrentVolume = channel4InitialVolume;
			channel4EnvelopeDirection = (value & (1 << 3)) != 0;
			channel4EnvelopeSweepPace = value & 0b111;
		}
	}

	public byte NR43
	{
		get
		{
			byte value = 0;

			value |= (byte)(channel4ClockShift << 4);

			if (channel4LfsrWidth)
				value |= 1 << 3;

			value |= (byte)channel4ClockDivider;

			return value;
		}

		set
		{
			channel4ClockShift = value >> 4;
			channel4LfsrWidth = (value & (1 << 3)) != 0;
			channel4ClockDivider = value & 0b111;
		}
	}

	public byte NR44
	{
		get
		{
			byte value = 0;

			if (enableChannel4Length)
				value |= (1 << 6);

			return value;
		}

		set
		{
			// Trigger event
			if ((value & (1 << 7)) != 0)
			{
				channel4EnvelopeSweepTimer = channel4EnvelopeSweepPace;
				channel4CurrentVolume = channel4InitialVolume;
				enableChannel4 = true;
				channel4Lfsr = 0b1111_1111_1111_111;
			}
			
			enableChannel4Length = (value & (1 << 6)) != 0;
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

	public readonly byte[] WaveRam = new byte[16];

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
	private bool channel1SweepEnabled;
	private int channel1ShadowFrequency;
	private int channel1SweepTimer;

	private int channel2DutyCycle = 0;
	private readonly short[] channel2Amplitudes = new short[ampsPerSample];
	private int channel2FreqTimer = 0;

	private int channel3WaveIndex = 0;
	private readonly short[] channel3Amplitudes = new short[ampsPerSample];
	private int channel3FreqTimer = 0;

	private readonly short[] channel4Amplitudes = new short[ampsPerSample];
	private int channel4FreqTimer = 0;
	private int channel4Lfsr;
	
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
						enableChannel1 = false;
				}

				if (enableChannel2Length && channel2LengthTimer > 0)
				{
					channel2LengthTimer--;
					if (channel2LengthTimer <= 0)
						enableChannel2 = false;
				}

				if (enableChannel3Length && channel3LengthTimer > 0)
				{
					channel3LengthTimer--;
					if (channel3LengthTimer <= 0)
						enableChannel3 = false;
				}
				
				if (enableChannel4Length && channel4LengthTimer > 0)
				{
					channel4LengthTimer--;
					if (channel4LengthTimer <= 0)
						enableChannel4 = false;
				}
			}

			if (fsClock is 2 or 6)
			{
				if (channel1SweepTimer > 0)
					channel1SweepTimer--;

				if (channel1SweepTimer == 0)
				{
					channel1SweepTimer = channel1SweepPace > 0 ? channel1SweepPace : 8;


					if (channel1SweepEnabled && channel1SweepPace > 0)
					{
						var newFreq = CalculateFrequency();

						if (newFreq < 2048 && channel1SweepSlope > 0)
						{
							channel1WaveLength = (ushort)newFreq;
							channel1ShadowFrequency = newFreq;

							// For overflow check
							CalculateFrequency();
						}

						int CalculateFrequency()
						{
							var newFreq = channel1ShadowFrequency >> channel1SweepSlope;

							if (channel1SweepDirection) // Sweep decrease
								newFreq = channel1ShadowFrequency - newFreq;
							else // Sweep increase
								newFreq = channel1ShadowFrequency + newFreq;

							if (newFreq > 2047)
								enableChannel1 = false;

							return newFreq;
						}
					}
				}
			}

			if (fsClock == 7)
			{
				if (channel1EnvelopeSweepPace != 0)
				{
					channel1EnvelopeSweepTimer--;

					if (channel1EnvelopeSweepTimer == 0)
					{
						channel1EnvelopeSweepTimer = channel1EnvelopeSweepPace;

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

				if (channel2EnvelopeSweepPace != 0)
				{
					channel2EnvelopeSweepTimer--;

					if (channel2EnvelopeSweepTimer == 0)
					{
						channel2EnvelopeSweepTimer = channel2EnvelopeSweepPace;

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
				
				if (channel4EnvelopeSweepPace != 0)
				{
					channel4EnvelopeSweepTimer--;

					if (channel4EnvelopeSweepTimer == 0)
					{
						channel4EnvelopeSweepTimer = channel4EnvelopeSweepPace;

						if (channel4EnvelopeDirection) // Sweep increase
						{
							if (channel4CurrentVolume < 0xF)
								channel4CurrentVolume++;
						}
						else // Sweep decrease
						{
							if (channel4CurrentVolume > 0)
								channel4CurrentVolume--;
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
			channel1FreqTimer = (2048 - channel1WaveLength) * 4;
			if (++channel1DutyCycle > 7)
				channel1DutyCycle = 0;
		}

		if (channel2FreqTimer == 0)
		{
			channel2FreqTimer = (2048 - channel2WaveLength) * 4;
			if (++channel2DutyCycle > 7)
				channel2DutyCycle = 0;
		}

		if (channel3FreqTimer == 0)
		{
			channel3FreqTimer = (2048 - channel3WaveLength) * 2;
			if (++channel3WaveIndex >= 32)
				channel3WaveIndex = 0;
		}

		if (channel4FreqTimer == 0)
		{
			channel4FreqTimer = (channel4ClockDivider > 0 ? channel4ClockDivider << 4 : 8) << channel4ClockShift;
			var xorResult = (channel4Lfsr & 0b01) ^ ((channel4Lfsr & 0b10) >> 1);
			channel4Lfsr = (channel4Lfsr >> 1) | (xorResult << 14);

			if (channel4LfsrWidth)
			{
				channel4Lfsr &= ~(1 << 6);
				channel4Lfsr |= xorResult << 6;
			}
		}

		channel1FreqTimer--;
		channel2FreqTimer--;
		channel3FreqTimer--;
		channel4FreqTimer--;

		{
			var amplitude = 0;

			if (enableChannel1)
				amplitude = (waveDutyTable[channel1Duty] >> (7 - channel1DutyCycle)) & 1;

			amplitude *= channel1CurrentVolume;
			channel1Amplitudes[ampI] = (short)amplitude;
		}

		{
			var amplitude = 0;

			if (enableChannel2)
				amplitude = (waveDutyTable[channel2Duty] >> (7 - channel2DutyCycle)) & 1;

			amplitude *= channel2CurrentVolume;
			channel2Amplitudes[ampI] = (short)amplitude;
		}

		{
			short amplitude = 0;

			if (enableChannel3)
			{
				var waveRamIndex = channel3WaveIndex / 2;

				var ram = WaveRam[waveRamIndex];

				byte sample = 0;

				if (channel3WaveIndex % 2 == 0) // Play upper 4 bits
					sample = (byte)(ram >> 4);
				else
					sample = (byte)(ram & 0xF);

				switch (channel3OutputLevel)
				{
					case 0:
						sample >>= 4;
						break;
					case 2:
						sample >>= 1;
						break;
					case 3:
						sample >>= 2;
						break;
				}

				amplitude = (short)(sample - 8);
			}

			channel3Amplitudes[ampI] = amplitude;
		}

		{
			var amplitude = 0;

			if (enableChannel4)
			{
				amplitude = ~channel4Lfsr & 1;
			}

			amplitude *= channel4CurrentVolume;
			channel4Amplitudes[ampI] = (short)amplitude;
		}

		ampI++;

		if (ampI == ampsPerSample)
		{
			var last = ampI - 1;
			ampI = 0;

			short sample = 0;

			{
				if (enableChannel1 && (NR12 & 0xF8) != 0)
					sample += channel1Amplitudes[last];
				if (enableChannel2 && (NR22 & 0xF8) != 0)
					sample += channel2Amplitudes[last];
				if (enableChannel3 && channel3DacEnable)
					sample += channel3Amplitudes[last];
				if (enableChannel4 && (NR42 & 0xF8) != 0)
					sample += channel4Amplitudes[last];

				sample *= 500;
			}

			playbackQueue.Enqueue(sample);
		}
	}

	private short sample = 0;

	public int GetNextBlock(Span<byte> buffer, bool rewind = false)
	{
		var i = 0;

		while (i < buffer.Length / 2)
		{
			sample *= 2;
			sample = playbackQueue.TryDequeue(out var nextSample) ? nextSample : (short)0;

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
