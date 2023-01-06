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

	private readonly DMG dmg;

	private Mode mode = Mode.OamScan;
	
	private int dot = 0;

	public byte LcdY { get; private set; } = 0;

	private const int bitControlBgTilemap = 3;
	private const int bitControlTiledata = 4;
	
	public bool ControlBgTilemap = false;
	public bool ControlTiledata = false;
	
	public byte LcdControl
	{
		get
		{
			byte value = 0;

			if (ControlBgTilemap)
				value |= 1 << bitControlBgTilemap;

			if (ControlTiledata)
				value |= 1 << bitControlTiledata;

			return value;
		}

		set
		{
			ControlBgTilemap = (value & (1 << bitControlBgTilemap)) != 0;
			ControlTiledata = (value & (1 << bitControlTiledata)) != 0;
		}
	}
	
	public PPU(DMG dmg)
	{
		this.dmg = dmg;
	}
	
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
			LcdY++;

			if (LcdY == 144)
			{
				DisplayFrame();
				dmg.InterruptController.RequestVBlank = true;
				mode = Mode.VBlank;
			}
			else
				mode = Mode.OamScan;
		}
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ModeVBlank()
	{
		if (dot % 456 == 0)
			LcdY++;
		
		if (dot == 456 * 144 + 4560)
		{
			dot = 0;
			LcdY = 0;
			mode = Mode.OamScan;
		}
	}

	private void DisplayFrame()
	{
		for (var i = 0; i < 384; i++)
		{
			var tileOffset = i * 16;
				
			for (var tileRow = 0; tileRow < 8; tileRow++)
			{
				var rowOffset = tileRow * 2;

				var rowByte1 = dmg.Bus[(ushort)(0x8000 + tileOffset + rowOffset)];
				var rowByte2 = dmg.Bus[(ushort)(0x8000 + tileOffset + rowOffset + 1)];

				for (var tileCol = 0; tileCol < 8; tileCol++)
				{
					var bit1 = (rowByte1 >> (7 - tileCol)) & 1;
					var bit2 = (rowByte2 >> (7 - tileCol)) & 1;
					var pix = (bit2 << 1) | bit1;
					var color = DMG.Palette[pix];
					var x = i % 16 * 8 + tileCol;
					var y = i / 16 * 8 + tileRow;
					dmg.VramWindow[x, y] = color;
				}
			}
		}
		dmg.VramWindow.Commit();
		
		var tilemap = ControlBgTilemap ? 0x9C00 : 0x9800;
		var tiledata = ControlTiledata ? 0x8000 : 0x9000;
		
		for (var i = 0; i < 32 * 32; i++)
		{
			var tileIndex = dmg.Bus[(ushort)(tilemap + i)];
			var tileOffset = ControlTiledata ? tileIndex * 16 : (sbyte)tileIndex * 16;
			for (var tileRow = 0; tileRow < 8; tileRow++)
			{
				var y = i / 32 * 8 + tileRow;
				if (y >= 144)
					break;
					
				var rowOffset = tileRow * 2;

				var rowByte1 = dmg.Bus[(ushort)(tiledata + tileOffset + rowOffset)];
				var rowByte2 = dmg.Bus[(ushort)(tiledata + tileOffset + rowOffset + 1)];

				for (var tileCol = 0; tileCol < 8; tileCol++)
				{
					var x = i % 32 * 8 + tileCol;
					if (x >= 160)
						break;
						
					var bit1 = (rowByte1 >> (7 - tileCol)) & 1;
					var bit2 = (rowByte2 >> (7 - tileCol)) & 1;
					var pix = (bit2 << 1) | bit1;
					var color = DMG.Palette[pix];
					dmg.Display[x, y] = color;
				}
			}
		}

		for (var i = 0; i < 40; i++)
		{
			var y = dmg.Bus[(ushort)(0xFE00 + i * 4)] - 16;
			var x = dmg.Bus[(ushort)(0xFE00 + i * 4 + 1)] - 8;
			var tileIndex = dmg.Bus[(ushort)(0xFE00 + i * 4 + 2)];
			var tileOffset = tileIndex * 16;
			
			for (var tileRow = 0; tileRow < 8; tileRow++)
			{
				var yy = y + tileRow;
				if (yy < 0)
					continue;
				if (yy >= 144)
					break;
					
				var rowOffset = tileRow * 2;

				var rowByte1 = dmg.Bus[(ushort)(0x8000 + tileOffset + rowOffset)];
				var rowByte2 = dmg.Bus[(ushort)(0x8000 + tileOffset + rowOffset + 1)];

				for (var tileCol = 0; tileCol < 8; tileCol++)
				{
					var xx = x + tileCol;
					if (yy < 0)
						continue;
					if (xx >= 160)
						break;
						
					var bit1 = (rowByte1 >> (7 - tileCol)) & 1;
					var bit2 = (rowByte2 >> (7 - tileCol)) & 1;
					var pix = (bit2 << 1) | bit1;

					if (pix == 0)
						continue;
					
					var color = DMG.Palette[pix];
					dmg.Display[xx, yy] = color;
				}
			}
		}
		
		dmg.Display.Commit();
	}
}
