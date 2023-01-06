using System.Diagnostics;
using ASFW;

namespace ADMG;

internal sealed class DMG : IDisposable
{
	private readonly Cartridge cartridge;
	private readonly Bus bus;
	private readonly CPU cpu;
	private readonly DisplayWindow display;
	
	public DMG()
	{
		cartridge = new(File.ReadAllBytes("Roms/blargg/cpu_instrs/individual/09-op r,r.gb"));
		bus = new(cartridge);
		cpu = new(bus);
		display = new();
	}

	public void Start()
	{
		display.TrySetDarkMode(true);
		display.Visible = true;
		display.Start();

		const int framesPerSecond = 60;
		var ticksPerFrame = Stopwatch.Frequency / framesPerSecond;
		const int cyclesPerSecond = 4194304; // ~4MHz
		const long cyclesPerFrame = cyclesPerSecond / framesPerSecond;

		const int cpuClockDivider = 4;
		
		var lastTime = Stopwatch.GetTimestamp();

		while (display.IsRunning)
		{
			Asfw.DoEvents();

			var cycles = 0;

			while (cycles < cyclesPerFrame)
			{
				if (cycles % cpuClockDivider == 0)
					cpu.Cycle();

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
		display.Dispose();
	}
}
