using System.Diagnostics;
using System.Drawing;
using ASFW;

namespace ADMG;

internal sealed class DMG : IDisposable
{
	public static readonly Color[] Palette =
	{
		Color.White,
		Color.FromArgb(0xAA, 0xAA, 0xAA),
		Color.FromArgb(0x55, 0x55, 0x55),
		Color.Black,
	};
	
	private readonly Cartridge cartridge;
	private readonly PPU ppu;
	private readonly Bus bus;
	private readonly CPU cpu;
	private readonly DisplayWindow vramWindow;
	private readonly DisplayWindow display;
	
	public DMG()
	{
		cartridge = new(File.ReadAllBytes("/home/adrian/Roms/GB/tetris.gb"));
		ppu = new();
		bus = new(cartridge, ppu);
		cpu = new(bus);
		vramWindow = new("ADMG Tile Viewer", 16 * 8, 24 * 8, 2);
		display = new("ADMG", 160, 144, 2);
	}

	public void Start()
	{
		vramWindow.TrySetDarkMode(true);
		vramWindow.Visible = true;
		vramWindow.Start();
		
		display.TrySetDarkMode(true);
		display.Visible = true;
		display.Start();

		const int framesPerSecond = 60;
		var ticksPerFrame = Stopwatch.Frequency / framesPerSecond;
		const int cyclesPerSecond = 4194304; // ~4MHz
		const long cyclesPerFrame = cyclesPerSecond / framesPerSecond;

		const int cpuClockDivider = 4; // CPU runs at 1/4 of the base clock
		
		var lastTime = Stopwatch.GetTimestamp();

		while (display.IsRunning)
		{
			Asfw.DoEvents();

			var cycles = 0;

			while (cycles < cyclesPerFrame)
			{
				if (cycles % cpuClockDivider == 0)
					cpu.Cycle();

				ppu.Cycle();
				
				cycles++;
			}

			for (var i = 0; i < 384; i++)
			{
				var tileOffset = i * 16;
				
				for (var tileRow = 0; tileRow < 8; tileRow++)
				{
					var rowOffset = tileRow * 2;

					var rowByte1 = bus[(ushort)(0x8000 + tileOffset + rowOffset)];
					var rowByte2 = bus[(ushort)(0x8000 + tileOffset + rowOffset + 1)];

					for (var tileCol = 0; tileCol < 8; tileCol++)
					{
						var bit1 = (rowByte1 >> (7 - tileCol)) & 1;
						var bit2 = (rowByte2 >> (7 - tileCol)) & 1;
						var pix = (bit2 << 1) | bit1;
						var color = Palette[pix];
						var x = i % 16 * 8 + tileCol;
						var y = i / 16 * 8 + tileRow;
						vramWindow[x, y] = color;
					}
				}
			}
			vramWindow.Commit();

			for (var i = 0; i < 32 * 32; i++)
			{
				var tileIndex = bus[(ushort)(0x9800 + i)];
				var tileOffset = tileIndex * 16;
				for (var tileRow = 0; tileRow < 8; tileRow++)
				{
					var y = i / 32 * 8 + tileRow;
					if (y >= 144)
						break;
					
					var rowOffset = tileRow * 2;

					var rowByte1 = bus[(ushort)(0x8000 + tileOffset + rowOffset)];
					var rowByte2 = bus[(ushort)(0x8000 + tileOffset + rowOffset + 1)];

					for (var tileCol = 0; tileCol < 8; tileCol++)
					{
						var x = i % 32 * 8 + tileCol;
						if (x >= 160)
							break;
						
						var bit1 = (rowByte1 >> (7 - tileCol)) & 1;
						var bit2 = (rowByte2 >> (7 - tileCol)) & 1;
						var pix = (bit2 << 1) | bit1;
						var color = Palette[pix];
						display[x, y] = color;
					}
				}
			}
			display.Commit();
			
			long thisTime;

			Console.WriteLine(Stopwatch.GetElapsedTime(lastTime).TotalMilliseconds);
			do
			{
				thisTime = Stopwatch.GetTimestamp();
			}
			while (thisTime - lastTime < ticksPerFrame);
			lastTime = thisTime;
		}
	}

	~DMG() => Dispose();

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		vramWindow.Dispose();
		display.Dispose();
	}
}
