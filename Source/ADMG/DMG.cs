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
	public readonly APU Apu;
	private readonly CPU cpu;

	private readonly string romFilePath;
	
	
	public DMG(string romFilePath)
	{
		this.romFilePath = romFilePath;
		//romFilePath = "/home/adrian/Downloads/gb-test-roms-master/dmg_sound/rom_singles/09-wave read while on.gb";
		//romFilePath = "Roms/blargg/dmg_sound.gb";
		
		Display = new("ADMG", 160, 144, 2);
		VramWindow = new("ADMG Tile Viewer", 16 * 8, 24 * 8, 2);
		Cartridge = new(File.ReadAllBytes(romFilePath));
		InterruptController = new();
		Joypad = new(InterruptController);
		Bus = new(this);
		Ppu = new(this);
		Timer = new(this);
		Apu = new();
		cpu = new(Bus, InterruptController);
	}

	public void Start()
	{
		var ramFilePath = $"{Path.Combine(romFilePath, "..", Path.GetFileNameWithoutExtension(romFilePath))}.sav";
		
		/*VramWindow.TrySetDarkMode(true);
		VramWindow.Visible = true;
		VramWindow.Start();*/
		
		Display.TrySetDarkMode(true);
		Display.Visible = true;
		Display.Start();

		const int framesPerSecond = 60;
		var ticksPerFrame = Stopwatch.Frequency / framesPerSecond;
		const int cyclesPerSecond = 4194304; // ~4MHz
		const long cyclesPerFrame = cyclesPerSecond / framesPerSecond;

		var lastTime = Stopwatch.GetTimestamp();

		if (File.Exists(ramFilePath))
			Cartridge.LoadRam(ramFilePath);

		var cpuCycles = 0;

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
				cpuCycles++;
				cycles++;
				
				Ppu.Cycle();
				Apu.Tick();
				Timer.Cycle();

				if (cpuCycles >= 4)
				{
					cpuCycles = 0;
					cpu.Cycle();
				}
			}

			long thisTime;

			do
			{
				thisTime = Stopwatch.GetTimestamp();
			}
			while (thisTime - lastTime < ticksPerFrame);
			lastTime = thisTime;
		}

		Cartridge.SaveRam(ramFilePath);
	}

	~DMG() => Dispose();

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		//VramWindow.Dispose();
		Display.Dispose();
		Apu.Dispose();
	}
}
