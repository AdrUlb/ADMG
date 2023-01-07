using System.Diagnostics;

namespace ADMG;

internal sealed class Cartridge
{
	private enum MbcId
	{
		None,
		Mbc1,
		Mbc3
	}

	private readonly MbcId mbcId;

	private readonly Mbc mbc;

	private readonly bool hasRam = false;
	private readonly bool hasBattery = false;
	private readonly byte[] data;

	public Cartridge(byte[] data)
	{
		this.data = data;

		switch (data[0x147])
		{
			case 0x00:
				mbcId = MbcId.None;
				break;
			case 0x01:
				mbcId = MbcId.Mbc1;
				break;
			case 0x02:
				mbcId = MbcId.Mbc1;
				hasRam = true;
				break;
			case 0x03:
				mbcId = MbcId.Mbc1;
				hasRam = true;
				hasBattery = true;
				break;
			case 0x11:
				mbcId = MbcId.Mbc3;
				break;
			case 0x12:
				mbcId = MbcId.Mbc3;
				hasRam = true;
				break;
			case 0x13:
				mbcId = MbcId.Mbc3;
				hasRam = true;
				hasBattery = true;
				break;
			default:
				throw new NotImplementedException("Controller 0x{data[0x147]:X2} not supported!");
		}

		mbc = mbcId switch
		{
			MbcId.Mbc1 => new Mbc1(data),
			MbcId.Mbc3 => new Mbc3(data),
			_ => throw new UnreachableException()
		};

		Console.WriteLine($"Controller: {mbcId}");
		Console.WriteLine($"Has RAM: {(hasRam ? "yes" : "no")}");
		Console.WriteLine($"Has Battery: {(hasBattery ? "yes" : "no")}");
	}

	public byte this[ushort address]
	{
		get => mbc[address];
		set => mbc[address] = value;
	}
}
