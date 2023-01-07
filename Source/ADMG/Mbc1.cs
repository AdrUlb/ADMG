namespace ADMG;

internal sealed class Mbc1 : Mbc
{
	private readonly byte[] data;
	
	private int romBankLower = 0;
	private int romBankUpperOrRam = 0;

	public Mbc1(byte[] data)
	{
		this.data = data;
	}

	public override byte this[ushort address]
	{
		get
		{
			switch (address)
			{
				case < 0x4000:
					return data[address];
				default:
				{
					var romBank = romBankLower & 0b11111;
					if (romBank == 0)
						romBank++;

					romBank |= (romBankUpperOrRam & 0b11) << 5;
					return data[romBank * 0x4000 + (address - 0x4000)];
				}
			}
		}

		set
		{
			switch (address)
			{
				case >= 0x2000 and < 0x4000:
				{
					romBankLower = value;
					break;
				}
				case >= 0x4000 and < 0x6000:
					romBankUpperOrRam = value;
					break;
			}
		}
	}
}
