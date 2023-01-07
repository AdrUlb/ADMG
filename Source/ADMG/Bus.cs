namespace ADMG;

internal sealed class Bus
{
	private readonly DMG dmg;
	private readonly byte[] bootrom;
	private byte disbaleBootrom = 0;
	
	private readonly byte[] temp;

	public Bus(DMG dmg)
	{
		this.dmg = dmg;
		temp = new byte[0x10000];
		for (var i = 0; i < temp.Length; i++)
			temp[i] = 0xFF;
		bootrom = File.ReadAllBytes("/home/adrian/Roms/GB/bootrom.bin");
	}

	public byte this[ushort address]
	{
		get => address switch
		{
			< 0x0100 when disbaleBootrom == 0 => bootrom[address],
			< 0x8000 => dmg.Cartridge[address],
			0xFF00 => dmg.Joypad.Control,
			0xFF04 => dmg.Timer.Divider,
			0xFF05 => dmg.Timer.Counter,
			0xFF06 => dmg.Timer.Modulo,
			0xFF07 => dmg.Timer.Control,
			0xFF0F => dmg.InterruptController.Requested,
			0xFF40 => dmg.Ppu.Control,
			0xFF41 => dmg.Ppu.Status,
			0xFF42 => dmg.Ppu.ScrollY,
			0xFF43 => dmg.Ppu.ScrollX,
			0xFF44 => dmg.Ppu.LcdY,
			0xFF45 => dmg.Ppu.LcdYCompare,
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
				case 0xFF00:
					dmg.Joypad.Control = value;
					break;
				case 0xFF04:
					dmg.Timer.Divider = 0;
					break;
				case 0xFF05:
					dmg.Timer.Counter = value;
					break;
				case 0xFF06:
					dmg.Timer.Modulo = value;
					break;
				case 0xFF07:
					dmg.Timer.Control = value;
					break;
				case 0xFF0F:
					dmg.InterruptController.Requested = value;
					break;
				case 0xFF40:
					dmg.Ppu.Control = value;
					break;
				case 0xFF41:
					dmg.Ppu.Status = value;
					break;
				case 0xFF42:
					dmg.Ppu.ScrollY = value;
					break;
				case 0xFF43:
					dmg.Ppu.ScrollX = value;
					break;
				case 0xFF45:
					dmg.Ppu.LcdYCompare = value;
					break;
				case 0xFF46:
					for (var i = 0; i < 0x100; i++) // Hack: Instant OAM DMA transfer
						this[(ushort)(0xFE00 + i)] = this[(ushort)((value << 8) + i)];
					break;
				case 0xFF50:
					disbaleBootrom = value;
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
