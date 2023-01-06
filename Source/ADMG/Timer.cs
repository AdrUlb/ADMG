using System.Diagnostics;

namespace ADMG;

internal sealed class Timer
{
	private readonly DMG dmg;
	
	public byte Divider;
	public byte Counter;
	public byte Modulo;
	
	public bool Enabled;
	public int Clock = 1024;

	private int cycles = 0;

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
				0 => 1024,
				1 => 16,
				2 => 64,
				3 => 256,
				_ => throw new UnreachableException()
			};
		}
	}
	
	public void Cycle()
	{
		cycles++;

		if (cycles % 256 == 0)
			Divider++;

		if (cycles % Clock == 0 && Enabled)
		{
			Counter++;

			if (Counter == 0)
			{
				Counter = Modulo;
				dmg.InterruptController.RequestTimer = true;
			}
		}
	}
}
