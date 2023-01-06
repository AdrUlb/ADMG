namespace ADMG;

internal sealed class Bus
{
	private readonly Cartridge cartridge;
	private readonly byte[] temp;
	private byte tempSerial = 0;
	
	public Bus(Cartridge cartridge)
	{
		this.cartridge = cartridge;
		temp = new byte[0x10000];
	}
	
	public byte this[ushort address]
	{
		get => address switch
		{
			<= 0x7FFF => cartridge[address],
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
