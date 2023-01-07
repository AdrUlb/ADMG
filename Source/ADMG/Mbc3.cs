namespace ADMG;

internal sealed class Mbc3 : Mbc
{
	private readonly byte[] data;
	
	private int romBank = 0;
	
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
				case >= 0x2000 and < 0x4000:
					romBank = value;
					break;
				default:
					romBank = romBank;
					break;
			}
		}
	}
}
