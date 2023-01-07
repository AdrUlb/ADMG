namespace ADMG;

internal sealed class Joypad
{
	private readonly InterruptController interruptController;
	
	public bool ActionSelected = false;
	public bool DirectionSelected = false;

	private bool upPressed;
	private bool downPressed;
	private bool leftPressed;
	private bool rightPressed;
	private bool startPressed;
	private bool selectPressed;
	private bool aPressed;
	private bool bPressed;

	public bool UpPressed
	{
		get => upPressed;

		set
		{
			var old = upPressed;
			upPressed = value;
			if (old != value && DirectionSelected)
				interruptController.RequestJoypad = true;
		}
	}

	public bool DownPressed
	{
		get => downPressed;

		set
		{
			var old = downPressed;
			downPressed = value;
			if (old != value && DirectionSelected)
				interruptController.RequestJoypad = true;
		}
	}

	public bool LeftPressed
	{
		get => leftPressed;

		set
		{
			var old = leftPressed;
			leftPressed = value;
			if (old != value && DirectionSelected)
				interruptController.RequestJoypad = true;
		}
	}

	public bool RightPressed
	{
		get => rightPressed;

		set
		{
			var old = rightPressed;
			rightPressed = value;
			if (old != value && DirectionSelected)
				interruptController.RequestJoypad = true;
		}
	}

	public bool StartPressed
	{
		get => startPressed;

		set
		{
			var old = startPressed;
			startPressed = value;
			if (old != value && ActionSelected)
				interruptController.RequestJoypad = true;
		}
	}

	public bool SelectPressed
	{
		get => selectPressed;

		set
		{
			var old = selectPressed;
			selectPressed = value;
			if (old != value && ActionSelected)
				interruptController.RequestJoypad = true;
		}
	}

	public bool APressed
	{
		get => aPressed;

		set
		{
			var old = aPressed;
			aPressed = value;
			if (old != value && ActionSelected)
				interruptController.RequestJoypad = true;
		}
	}

	public bool BPressed
	{
		get => bPressed;

		set
		{
			var old = bPressed;
			bPressed = value;
			if (old != value && ActionSelected)
				interruptController.RequestJoypad = true;
		}
	}

	public byte Control
	{
		get
		{
			byte value = 0;

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

	public Joypad(InterruptController interruptController)
	{
		this.interruptController = interruptController;
	}
}
