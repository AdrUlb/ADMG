using System.Drawing;
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

			StatusLcdYCompare = LcdY == LcdYCompare && ControlEnable;

			if (StatusLcdYCompare && StatusLcdYCompareInterrupt)
				dmg.InterruptController.RequestLcdStat = true;
		}
	}

	public byte LcdYCompare = 0;

	public byte ScrollX;
	public byte ScrollY;
	public byte WindowX;
	public byte WindowY;
	public byte WindowOffX;
	public byte WindowOffY;

	public readonly int[] BackgroundPalette = { 0, 1, 2, 3 };
	public readonly int[] ObjectPalette0 = { 0, 1, 2, 3 };
	public readonly int[] ObjectPalette1 = { 0, 1, 2, 3 };

	private const int bitControlObjSize = 2;
	private const int bitControlBgTilemap = 3;
	private const int bitControlTiledata = 4;
	private const int bitControlWindowEnable = 5;
	private const int bitControlWindowTilemap = 6;
	private const int bitControlEnable = 7;

	private const int bitsStatusMode = 0;
	private const int bitStatusLcdYCompare = 2;
	private const int bitStatusMode0Interrupt = 3;
	private const int bitStatusMode1Interrupt = 4;
	private const int bitStatusMode2Interrupt = 5;
	private const int bitStatusLcdYCompareInterrupt = 6;

	public bool ControlObjSize = false;
	public bool ControlBgTilemap = false;
	public bool ControlTiledata = false;
	public bool ControlWindowEnable = false;
	public bool ControlWindowTilemap = false;
	public bool ControlEnable = true;

	public bool StatusLcdYCompareInterrupt = false;
	public bool StatusMode2Interrupt = false;
	public bool StatusMode1Interrupt = false;
	public bool StatusMode0Interrupt = false;
	public bool StatusLcdYCompare = false;
	public PpuMode StatusMode = PpuMode.OamScan;

	private readonly int[] selectedObjs = new int[10];
	private int selectedObjsCount = 0;
	private bool windowConditionY = false;
	private bool windowActive = false;

	public byte BackgroundPaletteByte
	{
		get => (byte)((BackgroundPalette[0] & 0b11) | ((BackgroundPalette[1] & 0b11) << 2) | ((BackgroundPalette[2] & 0b11) << 4) | ((BackgroundPalette[3] & 0b11) << 6));

		set
		{
			BackgroundPalette[0] = value & 0b11;
			BackgroundPalette[1] = (value >> 2) & 0b11;
			BackgroundPalette[2] = (value >> 4) & 0b11;
			BackgroundPalette[3] = (value >> 6) & 0b11;
		}
	}

	public byte ObjectPalette0Byte
	{
		get => (byte)((ObjectPalette0[0] & 0b11) | ((ObjectPalette0[1] & 0b11) << 2) | ((ObjectPalette0[2] & 0b11) << 4) | ((ObjectPalette0[3] & 0b11) << 6));

		set
		{
			ObjectPalette0[0] = value & 0b11;
			ObjectPalette0[1] = (value >> 2) & 0b11;
			ObjectPalette0[2] = (value >> 4) & 0b11;
			ObjectPalette0[3] = (value >> 6) & 0b11;
		}
	}

	public byte ObjectPalette1Byte
	{
		get => (byte)((ObjectPalette1[0] & 0b11) | ((ObjectPalette1[1] & 0b11) << 2) | ((ObjectPalette1[2] & 0b11) << 4) | ((ObjectPalette1[3] & 0b11) << 6));

		set
		{
			ObjectPalette1[0] = value & 0b11;
			ObjectPalette1[1] = (value >> 2) & 0b11;
			ObjectPalette1[2] = (value >> 4) & 0b11;
			ObjectPalette1[3] = (value >> 6) & 0b11;
		}
	}

	public byte Control
	{
		get
		{
			byte value = 0;

			if (ControlObjSize)
				value |= 1 << bitControlObjSize;

			if (ControlBgTilemap)
				value |= 1 << bitControlBgTilemap;

			if (ControlTiledata)
				value |= 1 << bitControlTiledata;

			if (ControlWindowEnable)
				value |= 1 << bitControlWindowEnable;

			if (ControlWindowTilemap)
				value |= 1 << bitControlWindowTilemap;

			if (ControlEnable)
				value |= 1 << bitControlEnable;

			return value;
		}

		set
		{
			ControlObjSize = (value & (1 << bitControlObjSize)) != 0;
			ControlBgTilemap = (value & (1 << bitControlBgTilemap)) != 0;
			ControlTiledata = (value & (1 << bitControlTiledata)) != 0;
			ControlWindowEnable = (value & (1 << bitControlWindowEnable)) != 0;
			ControlWindowTilemap = (value & (1 << bitControlWindowTilemap)) != 0;
			ControlEnable = (value & (1 << bitControlEnable)) != 0;

			if (!ControlEnable)
			{
				LcdY = 0;
			}
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
			dmg.InterruptController.RequestLcdStat = true;
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
		if (dot % 456 == 1)
			windowConditionY = LcdY == WindowY;

		var objHeight = ControlObjSize ? 16 : 8;

		if (dot % 456 == 80)
		{
			selectedObjsCount = 0;

			for (var i = 0; i < 40; i++)
			{
				var y = dmg.Bus[(ushort)(0xFE00 + i * 4)] - 16;

				if (y > LcdY || y + objHeight <= LcdY)
					continue;

				selectedObjs[selectedObjsCount] = i;
				selectedObjsCount++;
				if (selectedObjsCount == 10)
					break;
			}

			StatusMode = PpuMode.Draw;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ModeDraw()
	{
		if (dot % 456 == 80 + 172)
		{
			DrawLine();
			StatusMode = PpuMode.HBlank;
			if (StatusMode0Interrupt)
				dmg.InterruptController.RequestLcdStat = true;
		}
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
				StatusMode = PpuMode.VBlank;
				dmg.InterruptController.RequestVBlank = true;
				if (StatusMode1Interrupt)
					dmg.InterruptController.RequestLcdStat = true;
			}
			else
			{
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
			WindowOffY = 0;
			windowActive = false;
			StatusMode = PpuMode.OamScan;
			if (StatusMode2Interrupt)
				dmg.InterruptController.RequestLcdStat = true;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DrawLine()
	{
		var bgTilemap = ControlBgTilemap ? 0x9C00 : 0x9800;
		var winTilemap = ControlWindowTilemap ? 0x9C00 : 0x9800;
		var tilemap = windowActive ? winTilemap : bgTilemap;
		var tiledata = ControlTiledata ? 0x8000 : 0x9000;

		WindowOffX = 0;

		var objHeight = ControlObjSize ? 16 : 8;

		for (var x = 0; x < 160; x++)
		{
			if (!windowActive && windowConditionY && x == WindowX - 7 && ControlWindowEnable)
			{
				windowActive = true;
				tilemap = winTilemap;
			}

			var hasObj = false;

			var pixel = 0;
			var palette = BackgroundPalette;

			var prevObj = -1;
			var bgOverObj = false;

			for (var oamI = 0; oamI < selectedObjsCount; oamI++)
			{
				var obj = selectedObjs[oamI];
				var spriteX = dmg.Bus[(ushort)(0xFE00 + obj * 4 + 1)] - 8;

				if (spriteX > x || spriteX + 8 <= x)
					continue;

				if (prevObj != -1)
				{
					var prevSpriteX = dmg.Bus[(ushort)(0xFE00 + prevObj * 4 + 1)] - 8;
					if (prevSpriteX <= spriteX)
						continue;
				}

				var spriteY = dmg.Bus[(ushort)(0xFE00 + obj * 4)] - 16;
				var tileIndex = dmg.Bus[(ushort)(0xFE00 + obj * 4 + 2)];
				var attribs = dmg.Bus[(ushort)(0xFE00 + obj * 4 + 3)];

				var xFlip = (attribs & (1 << 5)) != 0;
				var yFlip = (attribs & (1 << 6)) != 0;
				bgOverObj = (attribs & (1 << 7)) != 0;
				var objPalette = (attribs & (1 << 4)) != 0;

				var tileRow = LcdY - spriteY;
				var tileCol = x - spriteX;

				if (xFlip)
					tileCol = 7 - tileCol;

				if (yFlip)
					tileRow = objHeight - 1 - tileRow;

				var tileOffset = tileIndex * 16;
				var rowOffset = tileRow * 2;

				var rowByte1 = dmg.Bus[(ushort)(0x8000 + tileOffset + rowOffset)];
				var rowByte2 = dmg.Bus[(ushort)(0x8000 + tileOffset + rowOffset + 1)];

				var bit1 = (rowByte1 >> (7 - tileCol)) & 1;
				var bit2 = (rowByte2 >> (7 - tileCol)) & 1;
				pixel = (bit2 << 1) | bit1;
				palette = objPalette ? ObjectPalette1 : ObjectPalette0;

				if (pixel == 0)
					continue;
				
				hasObj = true;
				prevObj = obj;
			}

			if (!hasObj || bgOverObj)
			{
				var xx = windowActive ? WindowOffX : x + ScrollX;
				var yy = windowActive ? WindowOffY : LcdY + ScrollY;

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

				if (!hasObj || (bgOverObj && bgPixel != 0))
				{
					pixel = bgPixel;
					palette = BackgroundPalette;
				}
			}

			var color = DMG.Colors[palette[pixel]];
			dmg.Display[x, LcdY] = color;

			if (windowActive)
				WindowOffX++;
		}

		if (windowActive)
			WindowOffY++;
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
					var color = DMG.Colors[pix];
					var x = i % 16 * 8 + tileCol;
					var y = i / 16 * 8 + tileRow;
					dmg.VramWindow[x, y] = color;
				}
			}
		}

		dmg.VramWindow.Commit();
	}
}
