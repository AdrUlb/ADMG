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
			>= 0xA000 and < 0xC000 => dmg.Cartridge.ReadRam(address),
			0xFF00 => dmg.Joypad.Control,
			0xFF04 => dmg.Timer.Divider,
			0xFF05 => dmg.Timer.Counter,
			0xFF06 => dmg.Timer.Modulo,
			0xFF07 => dmg.Timer.Control,
			0xFF0F => dmg.InterruptController.Requested,
			0xFF10 => dmg.Apu.NR10,
			0xFF11 => dmg.Apu.NR11,
			0xFF12 => dmg.Apu.NR12,
			0xFF13 => 0xFF,
			0xFF14 => dmg.Apu.NR14,
			0xFF15 => 0xFF,
			0xFF16 => dmg.Apu.NR21,
			0xFF17 => dmg.Apu.NR22,
			0xFF18 => 0xFF,
			0xFF19 => dmg.Apu.NR24,
			0xFF1A => dmg.Apu.NR30,
			0xFF1B => 0xFF,
			0xFF1C => dmg.Apu.NR32,
			0xFF1D => 0xFF,
			0xFF1E => dmg.Apu.NR34,
			0xFF1F => 0xFF,
			0xFF20 => 0xFF,
			0xFF21 => dmg.Apu.NR42,
			0xFF22 => dmg.Apu.NR43,
			0xFF23 => dmg.Apu.NR44,
			0xFF24 => dmg.Apu.NR50,
			0xFF25 => dmg.Apu.NR51,
			0xFF26 => dmg.Apu.NR52,
			0xFF27 => 0xFF,
			0xFF28 => 0xFF,
			0xFF29 => 0xFF,
			0xFF2A => 0xFF,
			0xFF2B => 0xFF,
			0xFF2C => 0xFF,
			0xFF2D => 0xFF,
			0xFF2E => 0xFF,
			0xFF2F => 0xFF,
			>= 0xFF30 and < 0xFF40 => dmg.Apu.WaveRam[address - 0xFF30],
			0xFF40 => dmg.Ppu.Control,
			0xFF41 => dmg.Ppu.Status,
			0xFF42 => dmg.Ppu.ScrollY,
			0xFF43 => dmg.Ppu.ScrollX,
			0xFF44 => dmg.Ppu.LcdY,
			0xFF45 => dmg.Ppu.LcdYCompare,
			0xFF47 => dmg.Ppu.BackgroundPaletteByte,
			0xFF48 => dmg.Ppu.ObjectPalette0Byte,
			0xFF49 => dmg.Ppu.ObjectPalette1Byte,
			0xFF4A => dmg.Ppu.WindowY,
			0xFF4B => dmg.Ppu.WindowX,
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
				case >= 0xA000 and < 0xC000:
					dmg.Cartridge.WriteRam(address, value);
					break;
				case 0xFF00:
					dmg.Joypad.Control = value;
					break;
				case 0xFF04:
					dmg.Timer.ClkTimer = 0;
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
				case 0xFF10:
					dmg.Apu.NR10 = value;
					break;
				case 0xFF11:
					dmg.Apu.NR11 = value;
					break;
				case 0xFF12:
					dmg.Apu.NR12 = value;
					break;
				case 0xFF13:
					dmg.Apu.NR13 = value;
					break;
				case 0xFF14:
					dmg.Apu.NR14 = value;
					break;
				case 0xFF16:
					dmg.Apu.NR21 = value;
					break;
				case 0xFF17:
					dmg.Apu.NR22 = value;
					break;
				case 0xFF18:
					dmg.Apu.NR23 = value;
					break;
				case 0xFF19:
					dmg.Apu.NR24 = value;
					break;
				case 0xFF1A:
					dmg.Apu.NR30 = value;
					break;
				case 0xFF1B:
					dmg.Apu.NR31 = value;
					break;
				case 0xFF1C:
					dmg.Apu.NR32 = value;
					break;
				case 0xFF1D:
					dmg.Apu.NR33 = value;
					break;
				case 0xFF1E:
					dmg.Apu.NR34 = value;
					break;
				case 0xFF20:
					dmg.Apu.NR41 = value;
					break;
				case 0xFF21:
					dmg.Apu.NR42 = value;
					break;
				case 0xFF22:
					dmg.Apu.NR43 = value;
					break;
				case 0xFF23:
					dmg.Apu.NR44 = value;
					break;
				case 0xFF24:
					dmg.Apu.NR50 = value;
					break;
				case 0xFF25:
					dmg.Apu.NR51 = value;
					break;
				case 0xFF26:
					dmg.Apu.NR52 = value;
					break;
				case >= 0xFF30 and < 0xFF40:
					dmg.Apu.WaveRam[address - 0xFF30] = value;
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
				case 0xFF47:
					dmg.Ppu.BackgroundPaletteByte = value;
					break;
				case 0xFF48:
					dmg.Ppu.ObjectPalette0Byte = value;
					break;
				case 0xFF49:
					dmg.Ppu.ObjectPalette1Byte = value;
					break;
				case 0xFF4A:
					dmg.Ppu.WindowY = value;
					break;
				case 0xFF4B:
					dmg.Ppu.WindowX = value;
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
