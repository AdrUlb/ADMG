using System.Diagnostics;
using System.Drawing;
using ASFW;
using ASFW.Platform.Desktop;

namespace ADMG;

internal sealed class DMG : IDisposable
{
	public static readonly Color[] Colors =
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
	public readonly Joypad Joypad;
	public readonly Bus Bus;
	public readonly PPU Ppu;
	public readonly Timer Timer;
	private readonly CPU cpu;
	
	public DMG()
	{
		Display = new("ADMG", 160, 144, 2);
		VramWindow = new("ADMG Tile Viewer", 16 * 8, 24 * 8, 2);
		Cartridge = new(File.ReadAllBytes("/home/adrian/Roms/GB/sml2.gb"));
		//Cartridge = new(File.ReadAllBytes("Roms/blargg/cpu_instrs/cpu_instrs.gb"));
		InterruptController = new();
		Joypad = new(InterruptController);
		Bus = new(this);
		Ppu = new(this);
		Timer = new Timer(this);
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

			Joypad.StartPressed = Display.KeysDown.Contains(KeyboardKey.Enter);
			Joypad.SelectPressed = Display.KeysDown.Contains(KeyboardKey.Space);
			Joypad.APressed = Display.KeysDown.Contains(KeyboardKey.S);
			Joypad.BPressed = Display.KeysDown.Contains(KeyboardKey.A);
			Joypad.UpPressed = Display.KeysDown.Contains(KeyboardKey.Up);
			Joypad.DownPressed = Display.KeysDown.Contains(KeyboardKey.Down);
			Joypad.LeftPressed = Display.KeysDown.Contains(KeyboardKey.Left);
			Joypad.RightPressed = Display.KeysDown.Contains(KeyboardKey.Right);
			//Joypad.StartPressed = true;

			var cycles = 0;

			while (cycles < cyclesPerFrame)
			{
				if (cycles % cpuClockDivider == 0)
					cpu.Cycle();

				Ppu.Cycle();
				Timer.Cycle();
				
				cycles++;
			}

			long thisTime;

			//Console.WriteLine(Stopwatch.GetElapsedTime(lastTime).TotalMilliseconds);
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
