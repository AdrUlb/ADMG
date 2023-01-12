namespace ADMG;

internal sealed class Mbc1 : Mbc
{
	private readonly byte[] data;
	private readonly byte[] ram;
	
	private int romBankLower = 0;
	private int romBankUpperOrRam = 0;

	private bool ramEnabled = false;

	public Mbc1(byte[] data)
	{
		this.data = data;

		var ramSize = data[0x149] switch
		{
			0x00 => 0,
			0x01 => 0, // Not used in official cartridges
			0x02 => 8 * 1024,
			0x03 => 32 * 1024,
			0x04 => 128 * 1024,
			0x05 => 64 * 1024,
			_ => throw new ArgumentOutOfRangeException()
		};

		ram = new byte[ramSize];
	}

	public override byte ReadRam(ushort address)
	{
		if (!ramEnabled || data.Length < 32 * 1024)
			return 0xFF;
		
		address -= 0xA000;
		address += (ushort)(0x2000 * romBankUpperOrRam);
		
		return ram[address];
	}

	public override void WriteRam(ushort address, byte value)
	{
		if (!ramEnabled || data.Length < 32 * 1024)
			return;

		address -= 0xA000;
		address += (ushort)(0x2000 * romBankUpperOrRam);
		
		ram[address] = value;
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
					var addr = romBank * 0x4000 + (address - 0x4000);
					addr %= data.Length;
					return data[addr];
				}
			}
		}

		set
		{
			switch (address)
			{
				case < 0x2000:
					ramEnabled = value == 0x0A;
					break;
				case < 0x4000:
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
