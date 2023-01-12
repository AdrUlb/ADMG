using System.Diagnostics;

namespace ADMG;

internal sealed class Timer
{
	private readonly DMG dmg;

	public ushort ClkTimer;

	public byte Divider => (byte)(ClkTimer >> 8);
	public byte Counter;
	public byte Modulo;

	public bool Enabled;
	private int clockIndex = 0b11;

	private int tacTicks = 0;

	public Timer(DMG dmg)
	{
		this.dmg = dmg;
	}

	public byte Control
	{
		get
		{
			byte value = 0;

			if (Enabled)
				value |= 1 << 2;

			value |= (byte)clockIndex;

			return value;
		}

		set
		{
			Enabled = (value & (1 << 2)) != 0;
			clockIndex = value & 0b11;
		}
	}

	public void Cycle()
	{
		var clock = clockIndex switch
		{
			0b00 => 1024,
			0b01 => 16,
			0b10 => 64,
			0b11 => 256,
			_ => throw new UnreachableException()
		};

		ClkTimer++;
		tacTicks++;

		if (Enabled)
		{
			if (tacTicks >= clock)
			{
				tacTicks = 0;
				Counter++;

				if (Counter == 0)
				{
					Counter = Modulo;
					dmg.InterruptController.RequestTimer = true;
				}
			}
		}
	}
}
