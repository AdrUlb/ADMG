using System.Runtime.CompilerServices;

namespace ADMG;

internal sealed class PPU
{
	private readonly DMG dmg;

	private int dot = 0;

	private byte lcdY_internal = 0;

	public byte LcdY
	{
		get => lcdY_internal;

		set
		{
			if (value == lcdY_internal)
				return;

			lcdY_internal = value;

			StatusLcdYCompare = LcdY == LcdYCompare;

			if (StatusLcdYCompare && StatusLcdYCompareInterrupt)
				dmg.InterruptController.RequestLcdStat = true;
		}
	}

	public byte LcdYCompare = 0;

	public byte ScrollX;
	public byte ScrollY;

	private const int bitControlBgTilemap = 3;
	private const int bitControlTiledata = 4;
	private const int bitControlEnable = 7;

	private const int bitsStatusMode = 0;
	private const int bitStatusLcdYCompare = 2;
	private const int bitStatusMode0Interrupt = 3;
	private const int bitStatusMode1Interrupt = 4;
	private const int bitStatusMode2Interrupt = 5;
	private const int bitStatusLcdYCompareInterrupt = 6;

	public bool ControlBgTilemap = false;
	public bool ControlTiledata = false;
	public bool ControlEnable = true;

	public bool StatusLcdYCompareInterrupt = false;
	public bool StatusMode2Interrupt = false;
	public bool StatusMode1Interrupt = false;
	public bool StatusMode0Interrupt = false;
	public bool StatusLcdYCompare = false;
	public PpuMode StatusMode = PpuMode.OamScan;

	public byte Control
	{
		get
		{
			byte value = 0;

			if (ControlBgTilemap)
				value |= 1 << bitControlBgTilemap;

			if (ControlTiledata)
				value |= 1 << bitControlTiledata;

			if (ControlEnable)
				value |= 1 << bitControlEnable;

			return value;
		}

		set
		{
			ControlBgTilemap = (value & (1 << bitControlBgTilemap)) != 0;
			ControlTiledata = (value & (1 << bitControlTiledata)) != 0;
			ControlEnable = (value & (1 << bitControlEnable)) != 0;
		}
	}

	public byte Status
	{
		get
		{
			byte value = 0;

			if (StatusLcdYCompareInterrupt)
				value |= 1 << bitStatusLcdYCompareInterrupt;

			if (StatusMode2Interrupt)
				value |= 1 << bitStatusMode2Interrupt;

			if (StatusMode1Interrupt)
				value |= 1 << bitStatusMode1Interrupt;

			if (StatusMode0Interrupt)
				value |= 1 << bitStatusMode0Interrupt;

			if (StatusLcdYCompare)
				value |= 1 << bitStatusLcdYCompare;

			value |= (byte)((int)StatusMode << bitsStatusMode);

			return value;
		}

		set
		{
			StatusLcdYCompareInterrupt = (value & (1 << bitStatusLcdYCompareInterrupt)) != 0;
			StatusMode2Interrupt = (value & (1 << bitStatusMode2Interrupt)) != 0;
			StatusMode1Interrupt = (value & (1 << bitStatusMode1Interrupt)) != 0;
			StatusMode0Interrupt = (value & (1 << bitStatusMode0Interrupt)) != 0;
		}
	}

	public PPU(DMG dmg)
	{
		this.dmg = dmg;
	}

	public void Cycle()
	{
		if (!ControlEnable)
			return;

		dot++;

		switch (StatusMode)
		{
			case PpuMode.OamScan:
				ModeOamScan();
				break;
			case PpuMode.Draw:
				ModeDraw();
				break;
			case PpuMode.HBlank:
				ModeHBlank();
				break;
			case PpuMode.VBlank:
				ModeVBlank();
				break;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ModeOamScan()
	{
		if (dot % 456 == 80)
			StatusMode = PpuMode.Draw;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ModeDraw()
	{
		StatusMode = PpuMode.HBlank;
		if (StatusMode0Interrupt)
			dmg.InterruptController.RequestLcdStat = true;
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
				StatusMode = PpuMode.VBlank;
				if (StatusMode1Interrupt)
					dmg.InterruptController.RequestLcdStat = true;
			}
			else
			{
				DrawLine();
				StatusMode = PpuMode.OamScan;
				if (StatusMode2Interrupt)
					dmg.InterruptController.RequestLcdStat = true;
			}
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
			StatusMode = PpuMode.OamScan;
			if (StatusMode2Interrupt)
				dmg.InterruptController.RequestLcdStat = true;
		}
	}

	private void DrawLine()
	{
		var tilemap = ControlBgTilemap ? 0x9C00 : 0x9800;
		var tiledata = ControlTiledata ? 0x8000 : 0x9000;

		var yy = LcdY + ScrollY;

		Span<int> selectedSprites = stackalloc int[10];

		var selectedSpriteCount = 0;
		
		for (var i = 0; i < 40; i++)
		{
			var y = dmg.Bus[(ushort)(0xFE00 + i * 4)] - 16;

			if (y > LcdY || y + 8 <= LcdY)
				continue;

			selectedSprites[selectedSpriteCount] = i;
			selectedSpriteCount++;
			if (selectedSpriteCount == 10)
				break;
		}

		for (var x = 0; x < 160; x++)
		{
			var sprite = false;

			var pixel = 0;

			var pixelSprite = -1;
			var bgOverSprite = false;
			foreach (var i in selectedSprites[..selectedSpriteCount])
			{
				var spriteX = dmg.Bus[(ushort)(0xFE00 + i * 4 + 1)] - 8;

				if (spriteX > x || spriteX + 8 <= x)
					continue;

				if (pixelSprite != -1)
				{
					var prevSpriteX = dmg.Bus[(ushort)(0xFE00 + pixelSprite * 4 + 1)] - 8;
					if (prevSpriteX <= spriteX)
						continue;
				}
				
				var spriteY = dmg.Bus[(ushort)(0xFE00 + i * 4)] - 16;
				var tileIndex = dmg.Bus[(ushort)(0xFE00 + i * 4 + 2)];
				var attribs = dmg.Bus[(ushort)(0xFE00 + i * 4 + 3)];

				var xFlip = (attribs & (1 << 5)) != 0;
				var yFlip = (attribs & (1 << 6)) != 0;
				bgOverSprite = (attribs & (1 << 7)) != 0;
				
				var tileRow = LcdY - spriteY;
				var tileCol = x - spriteX;

				if (xFlip)
					tileCol = 7 - tileCol;

				if (yFlip)
					tileRow = 7 - tileRow;
				
				var tileOffset = tileIndex * 16;
				var rowOffset = tileRow * 2;

				var rowByte1 = dmg.Bus[(ushort)(0x8000 + tileOffset + rowOffset)];
				var rowByte2 = dmg.Bus[(ushort)(0x8000 + tileOffset + rowOffset + 1)];

				var bit1 = (rowByte1 >> (7 - tileCol)) & 1;
				var bit2 = (rowByte2 >> (7 - tileCol)) & 1;
				pixel = (bit2 << 1) | bit1;

				if (pixel == 0)
					break;
				
				sprite = true;
				pixelSprite = i;
			}
			
			if (!sprite || bgOverSprite)
			{
				var xx = x + ScrollX;
				var tilemapIndex = yy / 8 % 32 * 32 + xx / 8 % 32;
				var tileIndex = dmg.Bus[(ushort)(tilemap + tilemapIndex)];
				var tileOffset = ControlTiledata ? tileIndex * 16 : (sbyte)tileIndex * 16;
				var tileRow = yy % 8;
				var tileCol = xx % 8;

				var rowOffset = tileRow * 2;

				var rowByte1 = dmg.Bus[(ushort)(tiledata + tileOffset + rowOffset)];
				var rowByte2 = dmg.Bus[(ushort)(tiledata + tileOffset + rowOffset + 1)];

				var bit1 = (rowByte1 >> (7 - tileCol)) & 1;
				var bit2 = (rowByte2 >> (7 - tileCol)) & 1;
				
				var bgPixel = (bit2 << 1) | bit1;
				
				if (!sprite || (bgOverSprite && bgPixel != 0))
					pixel = bgPixel;
			}
			
			var color = DMG.Palette[pixel];
			dmg.Display[x, LcdY] = color;
		}
	}


	private void DisplayFrame()
	{
		dmg.Display.Commit();

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
	}

}
