using System.Runtime.CompilerServices;

namespace ADMG;

internal sealed class PPU
{
	private enum Mode
	{
		HBlank,
		VBlank,
		OamScan,
		Draw
	}

	private Mode mode = Mode.OamScan;
	
	private int dot = 0;

	public byte Ly { get; private set; } = 0;
	
	public void Cycle()
	{
		dot++;
		
		switch (mode)
		{
			case Mode.OamScan:
				ModeOamScan();
				break;
			case Mode.Draw:
				ModeDraw();
				break;
			case Mode.HBlank:
				ModeHBlank();
				break;
			case Mode.VBlank:
				ModeVBlank();
				break;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ModeOamScan()
	{
		if (dot % 456 == 80)
			mode = Mode.Draw;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ModeDraw()
	{
		mode = Mode.HBlank;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ModeHBlank()
	{
		if (dot % 456 == 0)
		{
			Ly++;
			
			mode = Ly == 144 ? Mode.VBlank : Mode.OamScan;
		}
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ModeVBlank()
	{
		if (dot % 456 == 0)
			Ly++;
		
		if (dot == 456 * 144 + 4560)
		{
			dot = 0;
			Ly = 0;
			mode = Mode.OamScan;
		}
	}
}
