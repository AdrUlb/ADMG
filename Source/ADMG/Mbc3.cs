namespace ADMG;

internal sealed class Mbc3 : Mbc
{
	private readonly byte[] data;
	private readonly byte[] ram;

	private bool ramAndTimerEnabled = false;
	private int romBank = 0;
	private byte ramOrTimerSelect = 0;

	private bool dayCounterCarry = false;
	private DateTime rtcStartTime;
	private DateTime haltedTime;

	private bool halted = false;

	public Mbc3(byte[] data)
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

		for (var i = 0; i < ram.Length; i++)
			ram[i] = 0xFF;
		
		rtcStartTime = DateTime.Now;
	}

	private TimeSpan GetCurrentRtcTime()
	{
		var value = DateTime.UtcNow - rtcStartTime;
		if (halted)
			value -= DateTime.UtcNow - haltedTime;
		return value;
	}

	public override byte ReadRam(ushort address)
	{
		if (!ramAndTimerEnabled || ramOrTimerSelect > 0x0C)
			return 0xFF;

		switch (ramOrTimerSelect)
		{
			case 0x08:
				return (byte)GetCurrentRtcTime().Seconds;
			case 0x09:
				return (byte)GetCurrentRtcTime().Minutes;
			case 0x0A:
				return (byte)GetCurrentRtcTime().Hours;
			case 0x0B:
				return (byte)GetCurrentRtcTime().Days;
			case 0x0C:
			{
				var days = GetCurrentRtcTime().Days;

				if (days > 0b111111111)
					dayCounterCarry = true;

				byte value = 0;

				if ((days & (1 << 8)) != 0)
					value |= 1;

				if (halted)
					value |= 1 << 6;

				if (dayCounterCarry)
					value |= 1 << 7;

				return value;
			}
		}

		address -= 0xA000;
		//address += (ushort)(0x2000 * ramOrTimerSelect);

		return ram[address];
	}

	public override void WriteRam(ushort address, byte value)
	{
		if (!ramAndTimerEnabled || ramOrTimerSelect > 0x0C)
			return;

		switch (ramOrTimerSelect)
		{
			case 0x08:
			{
				var add = value - GetCurrentRtcTime().Seconds;
				rtcStartTime += TimeSpan.FromSeconds(add);
				break;
			}
			case 0x09:
			{
				var add = value - GetCurrentRtcTime().Minutes;
				rtcStartTime += TimeSpan.FromMinutes(add);
				break;
			}
			case 0x0A:
			{
				var add = value - GetCurrentRtcTime().Hours;
				rtcStartTime += TimeSpan.FromHours(add);
				break;
			}
			case 0x0B:
			{
				var add = value - GetCurrentRtcTime().Days;
				rtcStartTime += TimeSpan.FromDays(add);
				break;
			}
			case 0x0C:
			{
				var days = GetCurrentRtcTime().Days;

				if (days > 0b111111111)
					dayCounterCarry = true;

				var add = ((value & 1) << 8) - (days & (1 << 8));
				rtcStartTime += TimeSpan.FromDays(add);

				var wasHalted = halted;
				halted = (value & (1 << 6)) != 0;

				switch (halted)
				{
					case true when !wasHalted:
						haltedTime = DateTime.UtcNow;
						break;
					case false when wasHalted:
						rtcStartTime += DateTime.UtcNow - haltedTime;
						break;
				}
				dayCounterCarry = (value & (1 << 7)) != 0;
				
				break;
			}
		}

		address -= 0xA000;
		//address += (ushort)(0x2000 * ramOrTimerSelect);

		ram[address] = value;
	}

	public override void LoadRam(string ramFilePath)
	{
		using var fs = File.OpenRead(ramFilePath);
		fs.ReadExactly(ram);
	}
	
	public override void SaveRam(string ramFilePath)
	{
		using var fs = File.OpenWrite(ramFilePath);
		fs.Position = 0;
		fs.Write(ram);
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
					ramAndTimerEnabled = (value & 0x0F) == 0x0A;
					break;
				case < 0x4000:
					romBank = value & 0b1111111;
					break;
				case < 0x6000:
					ramOrTimerSelect = value;
					break;
			}
		}
	}
}
