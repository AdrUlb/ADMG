namespace ADMG;

internal sealed class Bus
{
	private readonly DMG dmg;
	private readonly byte[] temp;

	public Bus(DMG dmg)
	{
		this.dmg = dmg;
		temp = new byte[0x10000];
		for (var i = 0; i < temp.Length; i++)
			temp[i] = 0xFF;
	}

	public byte this[ushort address]
	{
		get => address switch
		{
			<= 0x7FFF => dmg.Cartridge[address],
			0xFF00 => 0xFF, // Hack: report no buttons pressed
			0xFF0F => dmg.InterruptController.Requested,
			0xFF40 => dmg.Ppu.LcdControl,
			0xFF44 => dmg.Ppu.LcdY,
			0xFFFF => dmg.InterruptController.Enabled,
			_ => temp[address]
		};

		set
		{
			switch (address)
			{
				case <= 0x7FFF:
					dmg.Cartridge[address] = value;
					break;
				case 0xFF0F:
					dmg.InterruptController.Requested = value;
					break;
				case 0xFF40:
					dmg.Ppu.LcdControl = value;
					break;
				case 0xFF46:
					for (var i = 0; i < 0x100; i++) // Hack: Instant OAM DMA transfer
						this[(ushort)(0xFE00 + i)] = this[(ushort)((value << 8) + i)];
					break;
				case 0xFFFF:
					dmg.InterruptController.Enabled = value;
					break;
				default:
					temp[address] = value;
					break;
			}
		}
	}
}
