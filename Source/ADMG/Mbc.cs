namespace ADMG;

internal abstract class Mbc
{
	public abstract byte this[ushort address] { get; set; }

	public virtual byte ReadRam(ushort address) => 0xFF;

	public virtual void WriteRam(ushort address, byte value) { }

	public virtual void LoadRam(string ramFilePath) { }
	public virtual void SaveRam(string ramFilePath) { }
}
