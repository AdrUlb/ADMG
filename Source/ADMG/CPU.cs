// https://gb-archive.github.io/salvage/decoding_gbz80_opcodes/Decoding%20Gamboy%20Z80%20Opcodes.html

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ADMG;

internal sealed class CPU
{
	private readonly Bus bus;

	public ushort RegBC;
	public ushort RegDE;
	public ushort RegHL;
	public ushort RegAF;

	public ushort RegSP;
	public ushort RegPC;


	private int op;
	private int opCycle = 0;
	private byte readLo;
	private byte readHi;

	private ushort read16 => (ushort)((readHi << 8) | readLo);

	public CPU(Bus bus)
	{
		this.bus = bus;
		RegBC = 0x0013;
		RegDE = 0x00D8;
		RegHL = 0x014D;
		RegAF = 0x01B0;
		RegSP = 0xFFFE;
		RegPC = 0x0100;
	}

	private enum Reg16Id
	{
		BC = 0,
		DE = 1,
		HL = 2,
		SP = 3,
		AF = 3
	}

	private enum Reg8Id
	{
		B = 0,
		C = 1,
		D = 2,
		E = 3,
		H = 4,
		L = 5,
		MHL = 6,
		A = 7,

		F,
		SPHi,
		SPLo
	}

	private enum ConditionId
	{
		NotZero = 0,
		Zero = 1,
		NotCarry = 2,
		Carry = 3
	}

	private enum FlagId
	{
		Zero,
		Negative,
		HalfCarry,
		Carry
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private byte GetReg8(Reg8Id id) => id switch
	{
		Reg8Id.B => (byte)(RegBC >> 8),
		Reg8Id.C => (byte)(RegBC),
		Reg8Id.D => (byte)(RegDE >> 8),
		Reg8Id.E => (byte)(RegDE),
		Reg8Id.H => (byte)(RegHL >> 8),
		Reg8Id.L => (byte)(RegHL),
		Reg8Id.MHL => bus[RegHL],
		Reg8Id.A => (byte)(RegAF >> 8),

		Reg8Id.F => (byte)(RegAF),
		Reg8Id.SPHi => (byte)(RegSP >> 8),
		Reg8Id.SPLo => (byte)RegSP,
		_ => throw new UnreachableException()
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void SetReg8(Reg8Id id, byte value)
	{
		switch (id)
		{
			case Reg8Id.B:
				RegBC = (ushort)((RegBC & 0x00FF) | (value << 8));
				break;
			case Reg8Id.C:
				RegBC = (ushort)((RegBC & 0xFF00) | value);
				break;
			case Reg8Id.D:
				RegDE = (ushort)((RegDE & 0x00FF) | (value << 8));
				break;
			case Reg8Id.E:
				RegDE = (ushort)((RegDE & 0xFF00) | value);
				break;
			case Reg8Id.H:
				RegHL = (ushort)((RegHL & 0x00FF) | (value << 8));
				break;
			case Reg8Id.L:
				RegHL = (ushort)((RegHL & 0xFF00) | value);
				break;
			case Reg8Id.MHL:
				bus[RegHL] = value;
				break;
			case Reg8Id.A:
				RegAF = (ushort)((RegAF & 0x00FF) | (value << 8));
				break;

			case Reg8Id.F:
				RegAF = (ushort)((RegAF & 0xFF00) | value);
				break;
			case Reg8Id.SPHi:
				RegSP = (ushort)((RegSP & 0x00FF) | (value << 8));
				break;
			case Reg8Id.SPLo:
				RegSP = (ushort)((RegSP & 0xFF00) | value);
				break;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Reg8Id GetR16HiId(Reg16Id id) => id switch
	{
		Reg16Id.BC => Reg8Id.B,
		Reg16Id.DE => Reg8Id.D,
		Reg16Id.HL => Reg8Id.H,
		Reg16Id.SP => Reg8Id.SPHi,
		_ => throw new UnreachableException()
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Reg8Id GetR16LoId(Reg16Id id) => id switch
	{
		Reg16Id.BC => Reg8Id.C,
		Reg16Id.DE => Reg8Id.E,
		Reg16Id.HL => Reg8Id.L,
		Reg16Id.SP => Reg8Id.SPLo,
		_ => throw new UnreachableException()
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Reg8Id GetR16AFHiId(Reg16Id id) => id switch
	{
		Reg16Id.BC => Reg8Id.B,
		Reg16Id.DE => Reg8Id.D,
		Reg16Id.HL => Reg8Id.H,
		Reg16Id.AF => Reg8Id.A,
		_ => throw new UnreachableException()
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Reg8Id GetR16AFLoId(Reg16Id id) => id switch
	{
		Reg16Id.BC => Reg8Id.C,
		Reg16Id.DE => Reg8Id.E,
		Reg16Id.HL => Reg8Id.L,
		Reg16Id.AF => Reg8Id.F,
		_ => throw new UnreachableException()
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool GetCondition(ConditionId id) => id switch
	{
		ConditionId.NotZero => (RegAF & (1 << 7)) == 0,
		ConditionId.Zero => (RegAF & (1 << 7)) != 0,
		ConditionId.NotCarry => (RegAF & (1 << 4)) == 0,
		ConditionId.Carry => (RegAF & (1 << 4)) != 0,
		_ => throw new UnreachableException()
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool GetFlag(FlagId id) => id switch
	{
		FlagId.Zero => (RegAF & (1 << 7)) != 0,
		FlagId.Negative => (RegAF & (1 << 6)) != 0,
		FlagId.HalfCarry => (RegAF & (1 << 5)) != 0,
		FlagId.Carry => (RegAF & (1 << 4)) != 0,
		_ => throw new UnreachableException()
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void SetFlag(FlagId id, bool value)
	{
		if (value)
		{
			switch (id)
			{
				case FlagId.Zero:
					RegAF |= 1 << 7;
					break;
				case FlagId.Negative:
					RegAF |= 1 << 6;
					break;
				case FlagId.HalfCarry:
					RegAF |= 1 << 5;
					break;
				case FlagId.Carry:
					RegAF |= 1 << 4;
					break;
			}
		}
		else
		{
			switch (id)
			{
				case FlagId.Zero:
					RegAF &= unchecked((ushort)~(1 << 7));
					break;
				case FlagId.Negative:
					RegAF &= unchecked((ushort)~(1 << 6));
					break;
				case FlagId.HalfCarry:
					RegAF &= unchecked((ushort)~(1 << 5));
					break;
				case FlagId.Carry:
					RegAF &= unchecked((ushort)~(1 << 4));
					break;
			}
		}
	}

	private bool trace = false;

	public void Cycle()
	{
		if (opCycle++ == 0)
			op = bus[RegPC++];

		var opX = op >> 6;

		var opY = (op >> 3) & 0b111;
		var opZ = op & 0b111;
		var opP = opY >> 1;
		var opQ = opY & 1;

		if (opCycle == 1)
		{
			//	if (RegPC - 1 == 0x020B)
			trace = true;

			if (trace)
			{
				Console.WriteLine($"0x{RegPC - 1:X4} 0x{op:X2} - AF:{RegAF:X4} BC:{RegBC:X4} DE:{RegDE:X4} HL:{RegHL:X4} SP:{RegSP:X4}");
				//		Console.ReadKey(true);
			}
		}

		switch (opX)
		{
			case 0:
				if (!CycleX0())
					goto default;
				break;
			case 1:
				if (!CycleX1())
					goto default;
				break;
			case 2:
				if (!CycleX2())
					goto default;
				break;
			case 3:
				if (!CycleX3())
					goto default;
				break;
			default:
				throw new NotImplementedException($"Instruction not implemented: 0x{op:X2} (x:{opX},y:{opY},z:{opZ},p:{opP},q:{opQ}).");
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool CycleX0()
	{
		var opY = (op >> 3) & 0b111;
		var opZ = op & 0b111;
		var opP = opY >> 1;
		var opQ = opY & 1;

		switch (opZ)
		{
			case 0:
				switch (opY)
				{
					case 0: // NOP
						opCycle = 0;
						break;
					case >= 3 and <= 7: // JR flag, i8
						switch (opCycle)
						{
							case 2:
								readLo = bus[RegPC++];
								// Not an unconditional jump and condition not true
								if (opY != 3 && !GetCondition((ConditionId)(opY - 4)))
									opCycle = 0;
								break;
							case 3:
								RegPC = (ushort)(RegPC + (sbyte)readLo);
								opCycle = 0;
								break;
						}
						break;
					default:
						return false;
				}
				break;
			case 1:
				switch (opQ)
				{
					case 0: // LD r16, u16
						switch (opCycle)
						{
							case 2:
								SetReg8(GetR16LoId((Reg16Id)opP), bus[RegPC++]);
								break;
							case 3:
								SetReg8(GetR16HiId((Reg16Id)opP), bus[RegPC++]);
								opCycle = 0;
								break;
						}
						break;
					default:
						return false;
				}
				break;
			case 2:
				switch (opQ)
				{
					case 0:
						switch (opP)
						{
							case 0: // LD (BC), A
								if (opCycle == 2)
								{
									bus[RegBC] = GetReg8(Reg8Id.A);
									opCycle = 0;
								}
								break;
							case 1: // LD (DE), A
								if (opCycle == 2)
								{
									bus[RegDE] = GetReg8(Reg8Id.A);
									opCycle = 0;
								}
								break;
							case 2: // LD (HL+), A
								if (opCycle == 2)
								{
									bus[RegHL++] = GetReg8(Reg8Id.A);
									opCycle = 0;
								}
								break;
							case 3: // LD (HL-), A
								if (opCycle == 2)
								{
									bus[RegHL--] = GetReg8(Reg8Id.A);
									opCycle = 0;
								}
								break;
							default:
								return false;
						}
						break;
					case 1:
						switch (opP)
						{
							case 0: // LD A, (BC)
								if (opCycle == 2)
								{
									SetReg8(Reg8Id.A, bus[RegBC]);
									opCycle = 0;
								}
								break;
							case 1: // LD A, (DE)
								if (opCycle == 2)
								{
									SetReg8(Reg8Id.A, bus[RegDE]);
									opCycle = 0;
								}
								break;
							case 2: // LD A, (HL+)
								if (opCycle == 2)
								{
									SetReg8(Reg8Id.A, bus[RegHL++]);
									opCycle = 0;
								}
								break;
							case 3: // LD A, (HL-)
								if (opCycle == 2)
								{
									SetReg8(Reg8Id.A, bus[RegHL--]);
									opCycle = 0;
								}
								break;
							default:
								return false;
						}
						break;
					default:
						return false;
				}
				break;
			case 4: // INC r8
				switch (opCycle)
				{
					case 1:
						if ((Reg8Id)opY != Reg8Id.MHL)
							goto case 2;
						break;
					case 2:
						readLo = GetReg8((Reg8Id)opY);
						readLo++;
						if ((Reg8Id)opY != Reg8Id.MHL)
							goto case 3;
						break;
					case 3:
						SetReg8((Reg8Id)opY, readLo);
						SetFlag(FlagId.Zero, readLo == 0);
						SetFlag(FlagId.Negative, false);
						SetFlag(FlagId.HalfCarry, (readLo & 0x0F) == 0);
						opCycle = 0;
						break;
				}
				break;
			case 5: // DEC r8
				switch (opCycle)
				{
					case 1:
						if ((Reg8Id)opY != Reg8Id.MHL)
							goto case 2;
						break;
					case 2:
						readLo = GetReg8((Reg8Id)opY);
						readLo--;
						if ((Reg8Id)opY != Reg8Id.MHL)
							goto case 3;
						break;
					case 3:
						SetReg8((Reg8Id)opY, readLo);
						SetFlag(FlagId.Zero, readLo == 0);
						SetFlag(FlagId.Negative, true);
						SetFlag(FlagId.HalfCarry, (readLo & 0x0F) == 0x0F);
						opCycle = 0;
						break;
				}
				break;
			case 6: // LD r8, u8
				switch (opCycle)
				{
					case 2:
						if ((Reg8Id)opY != Reg8Id.MHL)
							goto case 3;
						break;
					case 3:
						SetReg8((Reg8Id)opY, bus[RegPC++]);
						opCycle = 0;
						break;
				}
				break;
			default:
				return false;
		}

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool CycleX1()
	{
		var opY = (op >> 3) & 0b111;
		var opZ = op & 0b111;
		var opP = opY >> 1;
		var opQ = opY & 1;

		if ((Reg8Id)opZ == Reg8Id.MHL && (Reg8Id)opY == Reg8Id.MHL) // HALT
		{
			// TODO: HALT
			opCycle = 0;
			return true;
		}

		switch (opCycle) // LD r8, r8
		{
			case 1:
				if ((Reg8Id)opY != Reg8Id.MHL && (Reg8Id)opZ != Reg8Id.MHL)
					goto case 2;
				break;
			case 2:
				SetReg8((Reg8Id)opY, GetReg8((Reg8Id)opZ));
				opCycle = 0;
				break;
		}

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool CycleX2()
	{
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool CycleX3()
	{
		var opY = (op >> 3) & 0b111;
		var opZ = op & 0b111;
		var opP = opY >> 1;
		var opQ = opY & 1;

		switch (opZ)
		{
			case 0:
				switch (opY)
				{
					case 4: // LD (FF00+u8), A
						switch (opCycle)
						{
							case 2:
								readLo = bus[RegPC++];
								break;
							case 3:
								bus[(ushort)(0xFF00 + readLo)] = GetReg8(Reg8Id.A);
								opCycle = 0;
								break;
						}
						break;
					default:
						return false;
				}
				break;
			case 1:
				switch (opQ)
				{
					case 1:
						switch (opP)
						{
							case 0: // RET
								switch (opCycle)
								{
									case 2:
										readLo = bus[RegSP++];
										break;
									case 3:
										readHi = bus[RegSP++];
										break;
									case 4:
										RegPC = read16;
										opCycle = 0;
										break;
								}
								break;
							default:
								return false;
						}
						break;
					default:
						return false;
				}
				break;
			case 2:
				switch (opY)
				{
					case 5: // LD (u16), A
						switch (opCycle)
						{
							case 2:
								readLo = bus[RegPC++];
								break;
							case 3:
								readHi = bus[RegPC++];
								break;
							case 4:
								bus[read16] = GetReg8(Reg8Id.A);
								opCycle = 0;
								break;
						}
						break;
					default:
						return false;
				}
				break;
			case 3:
				switch (opY)
				{
					case 0: // JP u16
						switch (opCycle)
						{
							case 2:
								readLo = bus[RegPC++];
								break;
							case 3:
								readHi = bus[RegPC++];
								break;
							case 4:
								RegPC = read16;
								opCycle = 0;
								break;
						}
						break;
					case 6: // DI
						// TODO: DI
						opCycle = 0;
						break;
					case 7: // EI
						// TODO: EI
						opCycle = 0;
						break;
					default:
						return false;
				}
				break;
			case 5:
				switch (opQ)
				{
					case 1:
						if (opP != 0) // Invalid instruction, hang
							return true;
						
						// CALL u16
						switch (opCycle)
						{
							case 2:
								readLo = bus[RegPC++];
								break;
							case 3:
								readHi = bus[RegPC++];
								break;
							case 5:
								bus[--RegSP] = (byte)(RegPC >> 8);
								break;
							case 6:
								bus[--RegSP] = (byte)RegPC;
								RegPC = read16;
								opCycle = 0;
								break;
						}
						break;
					default:
						return false;
				}
				break;
			default:
				return false;
		}

		return true;
	}
}
