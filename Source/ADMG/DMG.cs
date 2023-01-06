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
	
	public readonly DisplayWindow Display;
	public readonly DisplayWindow VramWindow;
	
	public readonly Cartridge Cartridge;
	public readonly InterruptController InterruptController;
	public readonly Bus Bus;
	public readonly PPU Ppu;
	private readonly CPU cpu;
	
	public DMG()
	{
		Display = new("ADMG", 160, 144, 2);
		VramWindow = new("ADMG Tile Viewer", 16 * 8, 24 * 8, 2);
		
		Cartridge = new(File.ReadAllBytes("/home/adrian/Roms/GB/tetris.gb"));
		InterruptController = new();
		Bus = new(this);
		Ppu = new(this);
		cpu = new(Bus, InterruptController);
	}

	public void Start()
	{
		VramWindow.TrySetDarkMode(true);
		VramWindow.Visible = true;
		VramWindow.Start();
		
		Display.TrySetDarkMode(true);
		Display.Visible = true;
		Display.Start();

		const int framesPerSecond = 60;
		var ticksPerFrame = Stopwatch.Frequency / framesPerSecond;
		const int cyclesPerSecond = 4194304; // ~4MHz
		const long cyclesPerFrame = cyclesPerSecond / framesPerSecond;

		const int cpuClockDivider = 4; // CPU runs at 1/4 of the base clock
		
		var lastTime = Stopwatch.GetTimestamp();

		while (Display.IsRunning)
		{
			Asfw.DoEvents();

			var cycles = 0;

			while (cycles < cyclesPerFrame)
			{
				if (cycles % cpuClockDivider == 0)
					cpu.Cycle();

				Ppu.Cycle();
				
				cycles++;
			}

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
		VramWindow.Dispose();
		Display.Dispose();
	}
}
