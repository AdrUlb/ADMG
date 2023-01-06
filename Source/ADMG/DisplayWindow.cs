using System.Diagnostics;
using System.Drawing;
using ASFW.Platform.Desktop;

namespace ADMG;

internal sealed class DisplayWindow : Window
{
	private readonly TimeSpan targetFrameTime = TimeSpan.FromSeconds(1 / 60.0);
	private readonly Stopwatch frameTimer = Stopwatch.StartNew();

	public DisplayWindow() : base(new()
	{
		Title = "ADMG",
		Size = new(160 * 2, 144 * 2),
		Resizable = false
	})
	{
		
	}

	protected override void OnRender()
	{
		Renderer.Clear(Color.Black);

		var sleepTime = targetFrameTime - frameTimer.Elapsed;
		if (sleepTime.Ticks > 0)
			Thread.Sleep(sleepTime);
		frameTimer.Restart();
	}

}
