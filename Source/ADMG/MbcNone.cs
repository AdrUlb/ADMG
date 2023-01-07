namespace ADMG;

internal sealed class MbcNone : Mbc
{
	private readonly byte[] data;
	
	public MbcNone(byte[] data)
	{
		this.data = data;
	}

	public override byte this[ushort address]
	{
		get => data[address];
		set {}
	}
}
