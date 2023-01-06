using System.Diagnostics;
using System.Drawing;
using ASFW.Graphics;
using ASFW.Platform.Desktop;

namespace ADMG;

internal sealed class DisplayWindow : Window
{
	private readonly TimeSpan targetFrameTime = TimeSpan.FromSeconds(1 / 60.0);
	private readonly Stopwatch frameTimer = Stopwatch.StartNew();
	private readonly Texture texture;
	private readonly object textureLock = new();

	private bool committed = false;

	public DisplayWindow(string title, int width, int height, int scale) : base(new()
	{
		Title = title,
		Size = new(width * scale, height * scale),
		Resizable = false
	})
	{
		texture = new(width, height);
	}

	protected override void OnRender()
	{
		if (committed)
		{
			lock (textureLock)
			{
				texture.Unlock();
				Renderer.CommitBatch();
				Renderer.DrawTexture(new(0, 0), new(Size.Width, Size.Height), texture, Color.White);
			}
			committed = false;
		}

		var sleepTime = targetFrameTime - frameTimer.Elapsed;
		if (sleepTime.Ticks > 0)
			Thread.Sleep(sleepTime);

		frameTimer.Restart();
	}

	public void Commit() => committed = true;

	public Color this[int x, int y]
	{
		set
		{
			lock (textureLock)
			{
				texture.Lock();
				texture[x, y] = value;
			}
		}
	}

	public override void Dispose()
	{
		base.Dispose();
		texture.Dispose();
	}
}
