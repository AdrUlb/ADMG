namespace ADMG;

internal sealed class Bus
{
	private readonly Cartridge cartridge;
	private readonly PPU ppu;
	private readonly byte[] temp;
	private byte tempSerial = 0;

	public Bus(Cartridge cartridge, PPU ppu)
	{
		this.cartridge = cartridge;
		this.ppu = ppu;
		temp = new byte[0x10000];
		for (var i = 0; i < temp.Length; i++)
			temp[i] = 0xFF;
	}

	public byte this[ushort address]
	{
		get => address switch
		{
			<= 0x7FFF => cartridge[address],
			0xFF00 => 0xFF, // Hack: report no b
			0xFF44 => ppu.Ly,
			_ => temp[address]
		};

		set
		{
			switch (address)
			{
				case <= 0x7FFF:
					cartridge[address] = value;
					break;
				case 0xFF01:
					tempSerial = value;
					break;
				case 0xFF02:
					if (value == 0x81)
						Console.Write((char)tempSerial);
					break;
				default:
					temp[address] = value;
					break;
			}
		}
	}
}
