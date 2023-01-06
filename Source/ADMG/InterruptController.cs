namespace ADMG;

internal sealed class InterruptController
{
	private const int bitVblank = 0;
	private const int bitLcdStat = 1;
	private const int bitTimer = 2;
	private const int bitSerial = 3;
	private const int bitJoypad = 4;
	
	public bool EnableVBlank = false;
	public bool EnableLcdStat = false;
	public bool EnableTimer = false;
	public bool EnableSerial = false;
	public bool EnableJoypad = false;
	
	public bool RequestVBlank = false;
	public bool RequestLcdStat = false;
	public bool RequestTimer = false;
	public bool RequestSerial = false;
	public bool RequestJoypad = false;
	
	public byte Enabled
	{
		get
		{
			byte value = 0;

			if (EnableVBlank)
				value |= 1 << bitVblank;
			if (EnableLcdStat)
				value |= 1 << bitLcdStat;
			if (EnableTimer)
				value |= 1 << bitTimer;
			if (EnableSerial)
				value |= 1 << bitSerial;
			if (EnableJoypad)
				value |= 1 << bitJoypad;
			
			return value;
		}

		set
		{
			EnableVBlank = (value & (1 << bitVblank)) != 0;
			EnableLcdStat = (value & (1 << bitLcdStat)) != 0;
			EnableTimer = (value & (1 << bitTimer)) != 0;
			EnableSerial = (value & (1 << bitSerial)) != 0;
			EnableJoypad = (value & (1 << bitJoypad)) != 0;
		}
	}
	
	public byte Requested
	{
		get
		{
			byte value = 0;

			if (RequestVBlank)
				value |= 1 << bitVblank;
			if (RequestLcdStat)
				value |= 1 << bitLcdStat;
			if (RequestTimer)
				value |= 1 << bitTimer;
			if (RequestSerial)
				value |= 1 << bitSerial;
			if (RequestJoypad)
				value |= 1 << bitJoypad;
			
			return value;
		}

		set
		{
			RequestVBlank = (value & (1 << bitVblank)) != 0;
			RequestLcdStat = (value & (1 << bitLcdStat)) != 0;
			RequestTimer = (value & (1 << bitTimer)) != 0;
			RequestSerial = (value & (1 << bitSerial)) != 0;
			RequestJoypad = (value & (1 << bitJoypad)) != 0;
		}
	}
}
