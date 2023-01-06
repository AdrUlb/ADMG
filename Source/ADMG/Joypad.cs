namespace ADMG;

internal sealed class Joypad
{
	public bool ActionSelected = false;
	public bool DirectionSelected = false;

	public bool UpPressed = false;
	public bool DownPressed = false;
	public bool LeftPressed = false;
	public bool RightPressed = false;
	public bool StartPressed = false;
	public bool SelectPressed = false;
	public bool APressed = false;
	public bool BPressed = false;
	
	public byte Control
	{
		get
		{
			byte value = 0;

			/*if (RightPressed || APressed)
				value |= 1 << 0;

			if (LeftPressed || BPressed)
				value |= 1 << 1;

			if (UpPressed || SelectPressed)
				value |= 1 << 2;

			if (DownPressed || StartPressed)
				value |= 1 << 3;*/

			if (DirectionSelected)
				value |= 1 << 4;

			if (ActionSelected)
				value |= 1 << 5;

			if (ActionSelected)
			{
				if (APressed)
					value |= 1 << 0;

				if (BPressed)
					value |= 1 << 1;

				if (SelectPressed)
					value |= 1 << 2;

				if (StartPressed)
					value |= 1 << 3;

			}
			else if (DirectionSelected)
			{
				if (RightPressed)
					value |= 1 << 0;

				if (LeftPressed)
					value |= 1 << 1;

				if (UpPressed)
					value |= 1 << 2;

				if (DownPressed)
					value |= 1 << 3;
			}
			
			return (byte)~value;
		}

		set
		{
			DirectionSelected = (value & (1 << 4)) == 0;
			ActionSelected = (value & (1 << 5)) == 0;
		}
	}
}
