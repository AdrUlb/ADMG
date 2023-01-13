using System.Diagnostics;
using System.Drawing;
using ASFW.Graphics;
using ASFW.Platform.Desktop;
using ASFW.Platform.Desktop.GLFW;

namespace ADMG;

internal sealed class DisplayWindow : Window
{
	private readonly HashSet<KeyboardKey> keysDown = new();

	private bool committed = false;

	public IReadOnlySet<KeyboardKey> KeysDown => keysDown;

	private readonly TimeSpan targetFrameTime = TimeSpan.FromSeconds(1 / 60.0);
	private readonly Stopwatch frameTimer = Stopwatch.StartNew();
	private readonly Color[] pixels;
	private readonly Texture texture;
	
	public DisplayWindow(string title, int width, int height, int scale) : base(new()
	{
		Title = title,
		Size = new(width * scale, height * scale),
		Resizable = false
	})
	{
		pixels = new Color[width * height];
		texture = new(width, height);
	}

	protected override void OnRender()
	{
		if (!committed)
			return;
		
		committed = false;

		texture.Lock();
		for (var i = 0; i < pixels.Length; i++)
			texture[i % texture.Width, i / texture.Width] = pixels[i];
		texture.Unlock();

		Renderer.DrawTexture(new(0, 0), new(Size.Width, Size.Height), texture, Color.White);
	}

	protected override void OnKeyDown(KeyboardKey key, ModifierKeys modifiers)
	{
		keysDown.Add(key);
	}

	protected override void OnKeyUp(KeyboardKey key, ModifierKeys modifiers)
	{
		keysDown.Remove(key);
	}

	public void Commit()
	{
		committed = true;
	}

	public Color this[int x, int y]
	{
		set
		{
			if (x < 0 || y < 0 || x >= texture.Width || y >= texture.Height)
				throw new IndexOutOfRangeException();

			pixels[x + y * texture.Width] = value;
		}
	}

	public override void Dispose()
	{
		base.Dispose();
		texture.Dispose();
	}
}
