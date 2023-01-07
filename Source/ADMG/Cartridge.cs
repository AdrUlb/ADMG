namespace ADMG;

internal sealed class Cartridge
{
	private enum Controller
	{
		None,
		Mbc1
	}

	private readonly Controller controller;

	private readonly bool hasRam = false;
	private readonly bool hasBattery = false;
	private readonly byte[] data;

	private int romBankLower = 0;
	private int romBankUpperOrRam = 0;

	private int romBank
	{
		get
		{
			var value = romBankLower & 0b11111;
			if (value == 0)
				value++;

			value |= (romBankUpperOrRam & 0b11) << 5;
			return value;
		}
	}

	public Cartridge(byte[] data)
	{
		this.data = data;

		switch (data[0x147])
		{
			case 0x00:
				controller = Controller.None;
				break;
			case 0x01:
				controller = Controller.Mbc1;
				break;
			case 0x02:
				controller = Controller.Mbc1;
				hasRam = true;
				break;
			case 0x03:
				controller = Controller.Mbc1;
				hasRam = true;
				hasBattery = true;
				break;
		}

		Console.WriteLine($"Controller: {controller}");
		Console.WriteLine($"Has RAM: {(hasRam ? "yes" : "no")}");
		Console.WriteLine($"Has Battery: {(hasBattery ? "yes" : "no")}");
	}

	public byte this[ushort address]
	{
		get => controller switch
		{
			Controller.Mbc1 => address switch
			{
				< 0x4000 => data[address],
				_ => data[romBank * 0x4000 + (address - 0x4000)]
			},
			_ => data[address]
		};

		set
		{
			switch (controller)
			{
				case Controller.Mbc1:
					switch (address)
					{
						case >= 0x2000 and < 0x4000:
						{
							romBankLower = value & 0b11111;
							break;
						}
						case >= 0x4000 and < 0x6000:
							romBankUpperOrRam = value & 0b11;
							break;
					}
					break;
			}
		}
	}
}
