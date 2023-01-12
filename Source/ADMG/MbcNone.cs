namespace ADMG;

internal sealed class MbcNone : Mbc
{
	private readonly byte[] data;
	private readonly byte[] ram;
	
	public MbcNone(byte[] data)
	{
		this.data = data;

		var ramSize = data[0x149] switch
		{
			0x00 => 0,
			0x01 => 0, // Not used in official cartridges
			0x02 => 8 * 1024,
			0x03 => 32 * 1024,
			0x04 => 128 * 1024,
			0x05 => 64 * 1024,
			_ => throw new ArgumentOutOfRangeException()
		};

		ram = new byte[ramSize];
	}

	public override byte ReadRam(ushort address) => ram[address - 0xA000];
	public override void WriteRam(ushort address, byte value) => ram[address - 0xA000] = value;

	public override byte this[ushort address]
	{
		get => data[address];
		set {}
	}
}
