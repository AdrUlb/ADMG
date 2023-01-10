using System.Diagnostics;

namespace ADMG;

internal sealed class Timer
{
	private readonly DMG dmg;

	public byte Divider;
	public int Counter;
	public byte Modulo;

	public bool Enabled;
	public int Clock = 1024;

	private int tacTicks = 0;
	private int divTicks = 0;
	
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

			switch (Clock)
			{
				case 16:
					value |= 1;
					break;
				case 64:
					value |= 2;
					break;
				case 256:
					value |= 3;
					break;
			}

			return value;
		}

		set
		{
			Enabled = (value & (1 << 2)) != 0;

			Clock = (value & 0b11) switch
			{
				0b00 => 1024,
				0b01 => 16,
				0b10 => 64,
				0b11 => 256,
				_ => throw new UnreachableException()
			};
		}
	}

	public void Cycle()
	{
		divTicks++;

		if (divTicks >= 256)
		{
			divTicks = 0;
			Divider++;
		}

		tacTicks++;

		if (Enabled && tacTicks >= Clock)
		{
			tacTicks = 0;
			Counter++;

			if (Counter > 0xFF)
			{
				Counter = Modulo;
				dmg.InterruptController.RequestTimer = true;
			}
		}
	}
}
