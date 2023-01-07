namespace ADMG;

internal sealed class Mbc3 : Mbc
{
	private readonly byte[] data;

	private bool ramAndTimerEnabled = false;
	private int romBank = 0;
	private byte ramOrTimerSelect;
	
	public Mbc3(byte[] data)
	{
		this.data = data;
	}

	public override byte this[ushort address]
	{
		get => address switch
		{
			< 0x4000 => data[address],
			_ => data[romBank * 0x4000 + (address - 0x4000)]
		};

		set
		{
			switch (address)
			{
				case < 0x2000:
					ramAndTimerEnabled = (value & 0x0F) == 0x0A;
					break;
				case < 0x4000:
					romBank = value;
					break;
				case < 0x6000:
					if (!ramAndTimerEnabled)
						return;

					ramOrTimerSelect = value;
					break;
			}
		}
	}
}
