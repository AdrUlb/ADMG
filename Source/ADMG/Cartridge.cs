namespace ADMG;

internal sealed class Cartridge
{
	private readonly byte[] data;
	
	public Cartridge(byte[] data)
	{
		this.data = data;
	}

	public byte this[ushort address]
	{
		get => data[address];

		set
		{
			
		}
	}
}
