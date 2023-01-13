// https://nightshade256.github.io/2021/03/27/gb-sound-emulation.html

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
using ASFW.Extension.Audio.Players;
using ASFW.Extension.Audio.Sources;

namespace ADMG;

public sealed class APU : IAudioSource, IDisposable
{
	public string Name => "ADMG";

	public ushort Channels => 1;

	public uint SampleRate => 44152;

	public uint BytesPerSecond => (uint)(BitsPerSample / 8 * Channels * SampleRate);

	public ushort BlockAlign => (ushort)(BitsPerSample / 8 * Channels);

	public ushort BitsPerSample => 16;

	public float Volume { get; set; }

	private readonly AudioPlayer? player;

	private readonly ConcurrentQueue<short> playbackQueue = new();

	private bool apuEnabled = false;

	private bool channel1DacEnabled = false;
	private bool channel1Enabled = false;
	private int channel1EnvelopeInitialVolume = 0;
	private bool channel1EnvelopeDirection = false;
	private int channel1EnvelopeSweepPace = 0;
	private int channel1WaveDuty = 0;
	private int channel1LengthTimer = 0;
	private int channel1WaveLengthInternal = 0;
	private int channel1WaveLength
	{
		get => channel1WaveLengthInternal & 0b111_11111111;
		set => channel1WaveLengthInternal = value;
	}
	private bool channel1SoundLengthEnable = false;
	private int channel1SweepPace = 0;
	private bool channel1SweepDecreasing = false;
	private int channel1SweepSlopeControl = 0;
	private bool channel1SweepEnabled = false;
	private int channel1ShadowFrequency = 0;
	private int channel1SweepTimer = 0;
	private bool channel1DisableOnNegToPos = false;
	private int channel1EnvelopePeriodTimer = 0;
	private int channel1EnvelopeCurrentVolume = 0;

	private bool channel2DacEnabled = false;
	private bool channel2Enabled = false;
	private int channel2EnvelopeInitialVolume = 0;
	private bool channel2EnvelopeDirection = false;
	private int channel2EnvelopeSweepPace = 0;
	private int channel2WaveDuty = 0;
	private int channel2LengthTimer = 0;
	private int channel2WaveLength = 0;
	private bool channel2SoundLengthEnable = false;
	private int channel2EnvelopePeriodTimer = 0;
	private int channel2EnvelopeCurrentVolume = 0;

	public bool Channel3DacEnabled { get; private set; } = false;

	public bool Channel3Enabled { get; private set; } = false;

	private int channel3LengthTimer = 0;
	private int channel3OutputLevel = 0;
	private int channel3WaveLength = 0;
	private bool channel3SoundLengthEnable = false;

	private bool channel4DacEnabled = false;
	private bool channel4Enabled = false;
	private int channel4LengthTimer = 0;
	private int channel4EnvelopeInitialVolume = 0;
	private bool channel4EnvelopeDirection = false;
	private int channel4EnvelopeSweepPace = 0;
	private bool channel4SoundLengthEnable = false;
	private int channel4ClockShift = 0;
	private bool channel4LfsrWidth = false;
	private int channel4ClockDivider = 0;
	private int channel4EnvelopePeriodTimer = 0;
	private int channel4EnvelopeCurrentVolume = 0;

	private bool mixVinLeft = false;
	private int mixLeftVolume = 0;
	private bool mixVinRight = false;
	private int mixRightVolume = 0;

	private bool mixChannel4Left = false;
	private bool mixChannel3Left = false;
	private bool mixChannel2Left = false;
	private bool mixChannel1Left = false;
	private bool mixChannel4Right = false;
	private bool mixChannel3Right = false;
	private bool mixChannel2Right = false;
	private bool mixChannel1Right = false;

	public byte NR10
	{
		get
		{
			byte value = 0;

			value |= 1 << 7;
			value |= (byte)(channel1SweepPace << 4);
			if (channel1SweepDecreasing)
				value |= 1 << 3;
			value |= (byte)channel1SweepSlopeControl;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			channel1SweepPace = (value >> 4) & 0b111;
			var wasDecreasing = channel1SweepDecreasing;
			channel1SweepDecreasing = (value & (1 << 3)) != 0;
			if (channel1DisableOnNegToPos && wasDecreasing && !channel1SweepDecreasing)
				channel1Enabled = false;
			channel1SweepSlopeControl = value & 0b111;
		}
	}

	public byte NR11
	{
		get
		{
			byte value = 0;

			value |= (byte)(channel1WaveDuty << 6);
			value |= 0b00111111;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			channel1WaveDuty = (value >> 6) & 0b11;
			channel1LengthTimer = 64 - (value & 0b111111);
		}
	}

	public byte NR12
	{
		get
		{
			byte value = 0;

			value |= (byte)(channel1EnvelopeInitialVolume << 4);
			if (channel1EnvelopeDirection)
				value |= 1 << 3;
			value |= (byte)channel1EnvelopeSweepPace;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			channel1EnvelopeInitialVolume = value >> 4;
			channel1EnvelopeDirection = (value & (1 << 3)) != 0;
			channel1EnvelopeSweepPace = value & 0b111;

			channel1DacEnabled = (value & 0b11111000) != 0;

			if (!channel1DacEnabled)
				channel1Enabled = false;
		}
	}

	public byte NR13
	{
		set
		{
			if (!apuEnabled && value != 0)
				return;

			channel1WaveLength = (channel1WaveLength & 0b11100000000) | value;
		}
	}

	public byte NR14
	{
		get
		{
			byte value = 0;

			if (channel1SoundLengthEnable)
				value |= 1 << 6;

			value |= 0b10111111;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			var lengthWasEnabled = channel1SoundLengthEnable;
			channel1SoundLengthEnable = (value & (1 << 6)) != 0;

			if (!lengthWasEnabled && channel1SoundLengthEnable && frameSequencerStep % 2 == 1 && channel1LengthTimer > 0)
			{
				channel1LengthTimer--;
				if (channel1LengthTimer == 0)
					channel1Enabled = false;
			}

			channel1WaveLength = (channel1WaveLength & 0xFF) | ((value & 0b111) << 8);

			if ((value & (1 << 7)) != 0)
			{
				if (channel1LengthTimer == 0)
				{
					channel1LengthTimer = 64;

					if (channel1SoundLengthEnable && frameSequencerStep % 2 == 1)
						channel1LengthTimer--;
				}

				channel1ShadowFrequency = channel1WaveLength;
				channel1SweepTimer = channel1SweepPace != 0 ? channel1SweepPace : 8;

				channel1SweepEnabled = channel1SweepPace != 0 || channel1SweepSlopeControl != 0;

				channel1EnvelopePeriodTimer = channel1EnvelopeSweepPace;
				channel1EnvelopeCurrentVolume = channel1EnvelopeInitialVolume;

				if (channel1DacEnabled)
				{
					channel1Enabled = true;
					channel1DisableOnNegToPos = false;
				}

				if (channel1SweepSlopeControl != 0)
				{
					if (channel1WaveLength > 2047)
						channel1Enabled = false;
					Channel1CalculateNewFrequency();
				}
			}
		}
	}

	public byte NR21
	{
		get
		{
			byte value = 0;

			value |= (byte)(channel2WaveDuty << 6);
			value |= 0b00111111;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			channel2WaveDuty = (value >> 6) & 0b11;
			channel2LengthTimer = 64 - (value & 0b111111);
		}
	}

	public byte NR22
	{
		get
		{
			byte value = 0;

			value |= (byte)(channel2EnvelopeInitialVolume << 4);
			if (channel2EnvelopeDirection)
				value |= 1 << 3;
			value |= (byte)channel2EnvelopeSweepPace;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			channel2EnvelopeInitialVolume = value >> 4;
			channel2EnvelopeDirection = (value & (1 << 3)) != 0;
			channel2EnvelopeSweepPace = value & 0b111;

			channel2DacEnabled = (value & 0b11111000) != 0;

			if (!channel2DacEnabled)
				channel2Enabled = false;
		}
	}

	public byte NR23
	{
		set
		{
			if (!apuEnabled && value != 0)
				return;

			channel2WaveLength = (channel2WaveLength & 0b11100000000) | value;
		}
	}

	public byte NR24
	{
		get
		{
			byte value = 0;

			if (channel2SoundLengthEnable)
				value |= 1 << 6;

			value |= 0b10111111;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			var lengthWasEnabled = channel2SoundLengthEnable;
			channel2SoundLengthEnable = (value & (1 << 6)) != 0;

			if (!lengthWasEnabled && channel2SoundLengthEnable && frameSequencerStep % 2 == 1 && channel2LengthTimer > 0)
			{
				channel2LengthTimer--;
				if (channel2LengthTimer == 0)
					channel2Enabled = false;
			}

			if ((value & (1 << 7)) != 0)
			{
				if (channel2LengthTimer == 0)
				{
					channel2LengthTimer = 64;

					if (channel2SoundLengthEnable && frameSequencerStep % 2 == 1)
						channel2LengthTimer--;
				}

				channel2EnvelopePeriodTimer = channel2EnvelopeSweepPace;
				channel2EnvelopeCurrentVolume = channel2EnvelopeInitialVolume;

				if (channel2DacEnabled)
					channel2Enabled = true;
			}

			channel2WaveLength = (channel2WaveLength & 0xFF) | ((value & 0b111) << 8);
		}
	}

	public byte NR30
	{
		get
		{
			byte value = 0;

			if (Channel3DacEnabled)
				value |= 1 << 7;

			value |= 0b01111111;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			Channel3DacEnabled = (value & (1 << 7)) != 0;
			if (!Channel3DacEnabled)
				Channel3Enabled = false;
		}
	}

	public byte NR31
	{
		set
		{
			if (!apuEnabled && value != 0)
				return;

			channel3LengthTimer = 256 - value;
		}
	}

	public byte NR32
	{
		get
		{
			byte value = 0;

			value |= (byte)(channel3OutputLevel << 5);
			value |= 0b10011111;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			channel3OutputLevel = (value >> 5) & 0b11;
		}
	}

	public byte NR33
	{
		set
		{
			if (!apuEnabled && value != 0)
				return;

			channel3WaveLength = (channel3WaveLength & 0b11100000000) | value;
		}
	}

	public byte NR34
	{
		get
		{
			byte value = 0;

			if (channel3SoundLengthEnable)
				value |= 1 << 6;
			value |= 0b10111111;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			var lengthWasEnabled = channel3SoundLengthEnable;
			channel3SoundLengthEnable = (value & (1 << 6)) != 0;

			if (!lengthWasEnabled && channel3SoundLengthEnable && frameSequencerStep % 2 == 1 && channel3LengthTimer > 0)
			{
				channel3LengthTimer--;
				if (channel3LengthTimer == 0)
					Channel3Enabled = false;
			}

			if ((value & (1 << 7)) != 0)
			{
				if (channel3LengthTimer == 0)
				{
					channel3LengthTimer = 256;

					if (channel3SoundLengthEnable && frameSequencerStep % 2 == 1)
						channel3LengthTimer--;
				}

				if (Channel3DacEnabled)
					Channel3Enabled = true;
			}

			channel3WaveLength = (channel3WaveLength & 0xFF) | ((value & 0b111) << 8);
		}
	}

	public byte NR41
	{
		set
		{
			if (!apuEnabled && value != 0)
				return;

			channel4LengthTimer = 64 - (value & 0b111111);
		}
	}

	public byte NR42
	{
		get
		{
			byte value = 0;

			value |= (byte)(channel4EnvelopeInitialVolume << 4);
			if (channel4EnvelopeDirection)
				value |= 1 << 3;
			value |= (byte)channel4EnvelopeSweepPace;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			channel4EnvelopeInitialVolume = value >> 4;
			channel4EnvelopeDirection = (value & (1 << 3)) != 0;
			channel4EnvelopeSweepPace = value & 0b111;

			channel4DacEnabled = (value & 0b11111000) != 0;

			if (!channel4DacEnabled)
				channel4Enabled = false;
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
			value |= (byte)(channel4ClockDivider);

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

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

			if (channel4SoundLengthEnable)
				value |= 1 << 6;
			value |= 0b10111111;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			var lengthWasEnabled = channel4SoundLengthEnable;
			channel4SoundLengthEnable = (value & (1 << 6)) != 0;

			if (!lengthWasEnabled && channel4SoundLengthEnable && frameSequencerStep % 2 == 1 && channel4LengthTimer > 0)
			{
				channel4LengthTimer--;
				if (channel4LengthTimer == 0)
					channel4Enabled = false;
			}

			if ((value & (1 << 7)) != 0)
			{
				if (channel4LengthTimer == 0)
				{
					channel4LengthTimer = 64;

					if (channel4SoundLengthEnable && frameSequencerStep % 2 == 1)
						channel4LengthTimer--;
				}

				if (channel4DacEnabled)
					channel4Enabled = true;

				channel4EnvelopePeriodTimer = channel4EnvelopeSweepPace;
				channel4EnvelopeCurrentVolume = channel4EnvelopeInitialVolume;
				
				channel4Lfsr = 0b111_1111_1111_1111;
			}

			channel4SoundLengthEnable = (value & (1 << 6)) != 0;
		}
	}

	public byte NR50
	{
		get
		{
			byte value = 0;

			if (mixVinLeft)
				value |= 1 << 7;

			value |= (byte)(mixLeftVolume << 4);

			if (mixVinRight)
				value |= 1 << 3;

			value |= (byte)mixRightVolume;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			mixVinLeft = (value & (1 << 7)) != 0;
			mixLeftVolume = (value >> 4) & 0b111;
			mixVinRight = (value & (1 << 3)) != 0;
			mixRightVolume = value & 0b111;
		}
	}

	public byte NR51
	{
		get
		{
			byte value = 0;

			if (mixChannel4Left)
				value |= 1 << 7;

			if (mixChannel3Left)
				value |= 1 << 6;

			if (mixChannel2Left)
				value |= 1 << 5;

			if (mixChannel1Left)
				value |= 1 << 4;

			if (mixChannel4Right)
				value |= 1 << 3;

			if (mixChannel3Right)
				value |= 1 << 2;

			if (mixChannel2Right)
				value |= 1 << 1;

			if (mixChannel1Right)
				value |= 1 << 0;

			return value;
		}

		set
		{
			if (!apuEnabled && value != 0)
				return;

			mixChannel4Left = (value & (1 << 7)) != 0;
			mixChannel3Left = (value & (1 << 6)) != 0;
			mixChannel2Left = (value & (1 << 5)) != 0;
			mixChannel1Left = (value & (1 << 4)) != 0;
			mixChannel4Right = (value & (1 << 3)) != 0;
			mixChannel3Right = (value & (1 << 2)) != 0;
			mixChannel2Right = (value & (1 << 1)) != 0;
			mixChannel1Right = (value & (1 << 0)) != 0;
		}
	}

	public byte NR52
	{
		get
		{
			byte value = 0;

			if (apuEnabled)
				value |= 1 << 7;

			if (channel4Enabled)
				value |= 1 << 3;

			if (Channel3Enabled)
				value |= 1 << 2;

			if (channel2Enabled)
				value |= 1 << 1;

			if (channel1Enabled)
				value |= 1 << 0;

			value |= 0b01110000;

			return value;
		}

		set
		{
			var apuWasEnabled = apuEnabled;
			apuEnabled = (value & (1 << 7)) != 0;

			if (apuEnabled && !apuWasEnabled)
				frameSequencerStep = 0;

			if (!apuEnabled)
			{
				NR10 = 0;
				NR11 = 0;
				NR12 = 0;
				NR13 = 0;
				NR14 = 0;
				NR21 = 0;
				NR22 = 0;
				NR23 = 0;
				NR24 = 0;
				NR30 = 0;
				NR31 = 0;
				NR32 = 0;
				NR33 = 0;
				NR34 = 0;
				NR41 = 0;
				NR42 = 0;
				NR43 = 0;
				NR44 = 0;
				NR50 = 0;
				NR51 = 0;
			}
		}
	}

	private static readonly byte[] waveDutyTable =
	{
		0b00000001,
		0b00000011,
		0b00001111,
		0b11111100
	};

	private const int ampsPerSample = 4194304 / 44150;

	public byte[] WaveRam = new byte[16];

	private int frameSequencerCounter = 0;
	private int frameSequencerStep = 0;

	private readonly short[] channel1Amplitudes = new short[ampsPerSample];
	private int channel1FreqTimer = 0;
	private int channel1DutyCycle = 0;

	private readonly short[] channel2Amplitudes = new short[ampsPerSample];
	private int channel2FreqTimer = 0;
	private int channel2DutyCycle = 0;

	private readonly short[] channel3Amplitudes = new short[ampsPerSample];
	private int channel3FreqTimer = 0;
	private int channel3WaveIndex = 0;

	private readonly short[] channel4Amplitudes = new short[ampsPerSample];
	private int channel4FreqTimer = 0;
	private int channel4Lfsr = 0;
	
	private int ampI = 0;

	public APU()
	{
		AudioPlayer.TryCreate(this, out player);

		player?.Play();
	}

	private int Channel1CalculateNewFrequency()
	{
		if (channel1SweepDecreasing)
			channel1DisableOnNegToPos = true;

		var freq = channel1ShadowFrequency >> channel1SweepSlopeControl;

		if (channel1SweepDecreasing)
			freq = channel1ShadowFrequency - freq;
		else
			freq = channel1ShadowFrequency + freq;

		if (freq > 2047)
			channel1Enabled = false;

		return freq;
	}

	public void Tick()
	{
		if (!apuEnabled)
			return;

		frameSequencerCounter++;

		if (frameSequencerCounter == 8192)
		{
			frameSequencerCounter = 0;

			// Length Ctr
			if (frameSequencerStep % 2 == 0)
			{
				if (channel1SoundLengthEnable && channel1LengthTimer > 0)
				{
					channel1LengthTimer--;

					if (channel1LengthTimer == 0)
						channel1Enabled = false;
				}

				if (channel2SoundLengthEnable && channel2LengthTimer > 0)
				{
					channel2LengthTimer--;

					if (channel2LengthTimer == 0)
						channel2Enabled = false;
				}

				if (channel3SoundLengthEnable && channel3LengthTimer > 0)
				{
					channel3LengthTimer--;

					if (channel3LengthTimer == 0)
						Channel3Enabled = false;
				}

				if (channel4SoundLengthEnable && channel4LengthTimer > 0)
				{
					channel4LengthTimer--;

					if (channel4LengthTimer == 0)
						channel4Enabled = false;
				}
			}

			// Sweep
			if (frameSequencerStep is 2 or 6)
			{
				if (channel1SweepTimer > 0)
					channel1SweepTimer--;
				
				if (channel1SweepTimer == 0)
				{
					channel1SweepTimer = channel1SweepPace > 0 ? channel1SweepPace : 8;

					if (channel1SweepEnabled && channel1SweepPace > 0)
					{
						// Calculate new freq and perform overflow check
						var newFrequency = Channel1CalculateNewFrequency();

						if (channel1WaveLength < 2048 && channel1SweepSlopeControl > 0)
						{
							channel1WaveLength = newFrequency;
							channel1ShadowFrequency = newFrequency;
							// Repeat the frequency calculation to repeat the overflow check
							Channel1CalculateNewFrequency();
						}
					}
				}
			}

			// Envelope
			if (frameSequencerStep == 7)
			{
				if (channel1EnvelopeSweepPace != 0)
				{
					if (channel1EnvelopePeriodTimer != 0)
						channel1EnvelopePeriodTimer--;

					if (channel1EnvelopePeriodTimer == 0)
					{
						channel1EnvelopePeriodTimer = channel1EnvelopeSweepPace;

						if (channel1EnvelopeCurrentVolume < 0xF && channel1EnvelopeDirection) // Increase
							channel1EnvelopeCurrentVolume++;
						else if (channel1EnvelopeCurrentVolume > 0 && !channel1EnvelopeDirection) // Decrease
							channel1EnvelopeCurrentVolume--;
					}
				}

				if (channel2EnvelopeSweepPace != 0)
				{
					if (channel2EnvelopePeriodTimer != 0)
						channel2EnvelopePeriodTimer--;

					if (channel2EnvelopePeriodTimer == 0)
					{
						channel2EnvelopePeriodTimer = channel2EnvelopeSweepPace;

						if (channel2EnvelopeCurrentVolume < 0xF && channel2EnvelopeDirection) // Increase
							channel2EnvelopeCurrentVolume++;
						else if (channel2EnvelopeCurrentVolume > 0 && !channel2EnvelopeDirection) // Decrease
							channel2EnvelopeCurrentVolume--;
					}
				}
				
				if (channel4EnvelopeSweepPace != 0)
				{
					if (channel4EnvelopePeriodTimer != 0)
						channel4EnvelopePeriodTimer--;

					if (channel4EnvelopePeriodTimer == 0)
					{
						channel4EnvelopePeriodTimer = channel4EnvelopeSweepPace;

						if (channel4EnvelopeCurrentVolume < 0xF && channel4EnvelopeDirection) // Increase
							channel4EnvelopeCurrentVolume++;
						else if (channel4EnvelopeCurrentVolume > 0 && !channel4EnvelopeDirection) // Decrease
							channel4EnvelopeCurrentVolume--;
					}
				}
			}

			frameSequencerStep++;
			frameSequencerStep %= 8;
		}

		if (channel1FreqTimer == 0)
		{
			channel1FreqTimer = (2048 - channel1WaveLength) * 4;

			if (++channel1DutyCycle > 7)
			{
				channel1DutyCycle = 0;
			}
		}

		if (channel2FreqTimer == 0)
		{
			channel2FreqTimer = (2048 - channel2WaveLength) * 4;

			if (++channel2DutyCycle > 7)
			{
				channel2DutyCycle = 0;
			}
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

			if (channel1Enabled)
			{
				amplitude = (waveDutyTable[channel1WaveDuty] >> (7 - channel1DutyCycle)) & 1;
				amplitude *= channel1EnvelopeCurrentVolume;
			}

			channel1Amplitudes[ampI] = (short)amplitude;
		}

		{
			var amplitude = 0;

			if (channel2Enabled)
			{
				amplitude = (waveDutyTable[channel2WaveDuty] >> (7 - channel2DutyCycle)) & 1;
				amplitude *= channel2EnvelopeCurrentVolume;
			}

			channel2Amplitudes[ampI] = (short)amplitude;
		}

		{
			short amplitude = 0;

			if (Channel3Enabled)
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

			if (channel4Enabled)
			{
				amplitude = ~channel4Lfsr & 1;
				amplitude *= channel4EnvelopeCurrentVolume;
			}

			channel4Amplitudes[ampI] = (short)amplitude;
		}

		ampI++;

		if (ampI == ampsPerSample)
		{
			var last = ampI - 1;
			ampI = 0;

			short sample = 0;

			{
				sample += channel1Amplitudes[last];
				sample += channel2Amplitudes[last];
				sample += channel3Amplitudes[last];
				sample += channel4Amplitudes[last];

				sample *= 200;
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
