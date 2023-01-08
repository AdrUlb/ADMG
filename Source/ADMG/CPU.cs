// https://gb-archive.github.io/salvage/decoding_gbz80_opcodes/Decoding%20Gamboy%20Z80%20Opcodes.html

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ADMG;

internal sealed class CPU
{
	private readonly InterruptController interruptController;
	private readonly Bus bus;

	public ushort RegBC;
	public ushort RegDE;
	public ushort RegHL;
	public ushort RegAF;

	public ushort RegSP;
	public ushort RegPC;

	private bool processedInts = false;
	private bool intsEnabled = false;
	private bool halted = false;

	private int op;
	private int opCycle = 0;

	private byte readLo;
	private byte readHi;


	private ushort read16 => (ushort)((readHi << 8) | readLo);

	private readonly StreamWriter log;

	public CPU(Bus bus, InterruptController interruptController)
	{
		log = new StreamWriter("log.txt");

		this.bus = bus;
		this.interruptController = interruptController;
		/*RegBC = 0x0013;
		RegDE = 0x00D8;
		RegHL = 0x014D;
		RegAF = 0x01B0;
		RegSP = 0xFFFE;
		RegPC = 0x0100;*/
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

		Reg8Id.F => (byte)(RegAF & 0xFF),
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
	private ushort GetReg16(Reg16Id id) => id switch
	{
		Reg16Id.BC => RegBC,
		Reg16Id.DE => RegDE,
		Reg16Id.HL => RegHL,
		Reg16Id.SP => RegSP,
		_ => throw new UnreachableException()
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Reg8Id GetReg16HiId(Reg16Id id) => id switch
	{
		Reg16Id.BC => Reg8Id.B,
		Reg16Id.DE => Reg8Id.D,
		Reg16Id.HL => Reg8Id.H,
		Reg16Id.SP => Reg8Id.SPHi,
		_ => throw new UnreachableException()
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Reg8Id GetReg16LoId(Reg16Id id) => id switch
	{
		Reg16Id.BC => Reg8Id.C,
		Reg16Id.DE => Reg8Id.E,
		Reg16Id.HL => Reg8Id.L,
		Reg16Id.SP => Reg8Id.SPLo,
		_ => throw new UnreachableException()
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Reg8Id GetReg16AFHiId(Reg16Id id) => id switch
	{
		Reg16Id.BC => Reg8Id.B,
		Reg16Id.DE => Reg8Id.D,
		Reg16Id.HL => Reg8Id.H,
		Reg16Id.AF => Reg8Id.A,
		_ => throw new UnreachableException()
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Reg8Id GetReg16AFLoId(Reg16Id id) => id switch
	{
		Reg16Id.BC => Reg8Id.C,
		Reg16Id.DE => Reg8Id.E,
		Reg16Id.HL => Reg8Id.L,
		Reg16Id.AF => Reg8Id.F,
		_ => throw new UnreachableException()
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool CheckCondition(ConditionId id) => id switch
	{
		ConditionId.NotZero => !GetFlag(FlagId.Zero),
		ConditionId.Zero => GetFlag(FlagId.Zero),
		ConditionId.NotCarry => !GetFlag(FlagId.Carry),
		ConditionId.Carry => GetFlag(FlagId.Carry),
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
		//if (opCycle == 0)
		//	log.WriteLine($"A:{GetReg8(Reg8Id.A):X2} F:{GetReg8(Reg8Id.F):X2} B:{GetReg8(Reg8Id.B):X2} C:{GetReg8(Reg8Id.C):X2} D:{GetReg8(Reg8Id.D):X2} E:{GetReg8(Reg8Id.E):X2} H:{GetReg8(Reg8Id.H):X2} L:{GetReg8(Reg8Id.L):X2} SP:{GetReg8(Reg8Id.SPHi):X2}{GetReg8(Reg8Id.SPLo):X2} PC:{RegPC:X4} PCMEM:{bus[RegPC]:X2},{bus[(ushort)(RegPC + 1)]:X2},{bus[(ushort)(RegPC + 2)]:X2},{bus[(ushort)(RegPC + 3)]:X2}");

		if (opCycle == 0 && !processedInts && (interruptController.Enabled & interruptController.Requested) != 0)
		{
			halted = false;

			processedInts = true;

			if (intsEnabled)
			{
				intsEnabled = false;

				bus[--RegSP] = (byte)(RegPC >> 8);
				bus[--RegSP] = (byte)(RegPC & 0xFF);

				if (interruptController is { EnableVBlank: true, RequestVBlank: true })
				{
					interruptController.RequestVBlank = false;
					RegPC = 0x40;
					return;
				}

				if (interruptController is { EnableLcdStat: true, RequestLcdStat: true })
				{
					interruptController.RequestLcdStat = false;
					RegPC = 0x48;
					return;
				}

				if (interruptController is { EnableTimer: true, RequestTimer: true })
				{
					interruptController.RequestTimer = false;
					RegPC = 0x50;
					return;
				}

				if (interruptController is { EnableSerial: true, RequestSerial: true })
				{
					interruptController.RequestSerial = false;
					RegPC = 0x58;
					return;
				}

				if (interruptController is { EnableJoypad: true, RequestJoypad: true })
				{
					interruptController.RequestJoypad = false;
					RegPC = 0x60;
					return;
				}

				throw new UnreachableException();
			}
		}

		if (halted)
			return;

		if (++opCycle == 1)
		{
			op = bus[RegPC++];
		}

		var opX = op >> 6;

		var opY = (op >> 3) & 0b111;
		var opZ = op & 0b111;
		var opP = opY >> 1;
		var opQ = opY & 1;

		if (opCycle == 1)
		{
			//if (RegPC - 1 == 0x2803)
			trace = true;

			if (trace)
			{
				//Console.WriteLine($"0x{RegPC - 1:X4} 0x{op:X2} - AF:{RegAF:X4} BC:{RegBC:X4} DE:{RegDE:X4} HL:{RegHL:X4} SP:{RegSP:X4}");
				//Console.ReadKey(true);
			}
		}

		switch (opX)
		{
			case 0:
				if (!CycleX0())
					goto default;
				break;
			case 1:
				CycleX1();
				break;
			case 2:
				CycleX2();
				break;
			case 3:
				if (!CycleX3())
					goto default;
				break;
			default:
				throw new NotImplementedException(
					$"Instruction not implemented: 0x{op:X2} (x:{opX},y:{opY},z:{opZ},p:{opP},q:{opQ}) - PC:{RegPC - 1:X4} AF:{RegAF:X4} BC:{RegBC:X4} DE:{RegDE:X4} HL:{RegHL:X4} SP:{RegSP:X4}.");
		}

		RegAF &= 0xFFF0;

		if (opCycle == 0)
			processedInts = false;
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
					case 1: // LD (u16), SP
						switch (opCycle)
						{
							case 2:
								readLo = bus[RegPC++];
								break;
							case 3:
								readHi = bus[RegPC++];
								break;
							case 4:
								bus[read16] = GetReg8(Reg8Id.SPLo);
								break;
							case 5:
								bus[(ushort)(read16 + 1)] = GetReg8(Reg8Id.SPHi);
								opCycle = 0;
								break;
						}
						break;
					case >= 3 and <= 7: // JR flag, i8
						switch (opCycle)
						{
							case 2:
								readLo = bus[RegPC++];
								// Not an unconditional jump and condition not true
								if (opY != 3 && !CheckCondition((ConditionId)(opY - 4)))
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
								SetReg8(GetReg16LoId((Reg16Id)opP), bus[RegPC++]);
								break;
							case 3:
								SetReg8(GetReg16HiId((Reg16Id)opP), bus[RegPC++]);
								opCycle = 0;
								break;
						}
						break;
					case 1: // ADD HL, r16
						switch (opCycle)
						{
							case 1:
							{
								var r16 = GetReg16((Reg16Id)opP);

								SetFlag(FlagId.HalfCarry, (((RegHL & 0b111111111111) + (r16 & 0b111111111111)) & (1 << 12)) == (1 << 12));
								SetFlag(FlagId.Carry, RegHL + r16 > 0xFFFF);
								RegHL += r16;
								SetFlag(FlagId.Negative, false);
								break;
							}
							case 2:
							{
								opCycle = 0;
								break;
							}
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
			case 3:
				switch (opQ)
				{
					case 0: // INC r16
						switch (opCycle)
						{
							case 1:
							{
								var id = GetReg16LoId((Reg16Id)opP);
								readHi = GetReg8(id);
								// Skip over the operation that would increase the high byte if there was no overflow
								if (readHi + 1 <= 0xFF)
									opCycle++;
								readHi++;
								SetReg8(id, readHi);
								break;
							}
							case 2:
							{
								var id = GetReg16HiId((Reg16Id)opP);
								readHi = GetReg8(id);
								readHi++;
								SetReg8(id, readHi);
								opCycle = 0;
								break;
							}
							case 3:
								opCycle = 0;
								break;
						}
						break;
					case 1: // DEC r16
						switch (opCycle)
						{
							case 1:
							{
								var id = GetReg16LoId((Reg16Id)opP);
								readHi = GetReg8(id);
								// Skip over the operation that would increase the high byte if there was no overflow
								if (readHi - 1 >= 0)
									opCycle++;
								readHi--;
								SetReg8(id, readHi);
								break;
							}
							case 2:
							{
								var id = GetReg16HiId((Reg16Id)opP);
								readHi = GetReg8(id);
								readHi--;
								SetReg8(id, readHi);
								opCycle = 0;
								break;
							}
							case 3:
								opCycle = 0;
								break;
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
			case 7:
				switch (opY)
				{
					case 0: // RLCA
					{
						var value = GetReg8(Reg8Id.A);
						var msb = value >> 7;

						value <<= 1;
						value |= (byte)msb;

						SetFlag(FlagId.Zero, false);
						SetFlag(FlagId.Negative, false);
						SetFlag(FlagId.HalfCarry, false);
						SetFlag(FlagId.Carry, msb == 1);

						SetReg8(Reg8Id.A, value);

						opCycle = 0;
						break;
					}
					case 1: // RRCA
					{
						var value = GetReg8(Reg8Id.A);
						var lsb = value & 1;

						value >>= 1;
						value |= (byte)(lsb << 7);

						SetFlag(FlagId.Zero, false);
						SetFlag(FlagId.Negative, false);
						SetFlag(FlagId.HalfCarry, false);
						SetFlag(FlagId.Carry, lsb == 1);

						SetReg8(Reg8Id.A, value);

						opCycle = 0;
						break;
					}
					case 2: // RLA
					{
						var value = GetReg8(Reg8Id.A);

						var msb = value >> 7;

						value <<= 1;
						if (GetFlag(FlagId.Carry))
							value |= 1;

						SetFlag(FlagId.Zero, false);
						SetFlag(FlagId.Negative, false);
						SetFlag(FlagId.HalfCarry, false);
						SetFlag(FlagId.Carry, msb == 1);

						SetReg8(Reg8Id.A, value);
						opCycle = 0;
						break;
					}
					case 3: // RRA
					{
						var value = GetReg8(Reg8Id.A);

						var lsb = value & 1;

						value >>= 1;
						if (GetFlag(FlagId.Carry))
							value |= 1 << 7;

						SetFlag(FlagId.Zero, false);
						SetFlag(FlagId.Negative, false);
						SetFlag(FlagId.HalfCarry, false);
						SetFlag(FlagId.Carry, lsb == 1);

						SetReg8(Reg8Id.A, value);
						opCycle = 0;
						break;
					}
					case 4: // DAA
					{
						int temp = GetReg8(Reg8Id.A);

						if (!GetFlag(FlagId.Negative))
						{
							if (GetFlag(FlagId.HalfCarry) || (temp & 0x0F) > 9)
								temp += 6;
							if (GetFlag(FlagId.Carry) || temp > 0x9F)
								temp += 0x60;
						}
						else
						{
							if (GetFlag(FlagId.HalfCarry))
							{
								temp -= 6;
								if (!GetFlag(FlagId.Carry))
									temp &= 0xFF;
							}
							if (GetFlag(FlagId.Carry))
								temp -= 0x60;
						}

						SetFlag(FlagId.HalfCarry, false);

						if ((temp & 0x100) != 0)
							SetFlag(FlagId.Carry, true);

						SetReg8(Reg8Id.A, (byte)(temp & 0xFF));

						SetFlag(FlagId.Zero, (byte)(temp & 0xFF) == 0);

						opCycle = 0;
						break;
					}
					case 5: // CPL
						SetReg8(Reg8Id.A, (byte)~GetReg8(Reg8Id.A));
						SetFlag(FlagId.Negative, true);
						SetFlag(FlagId.HalfCarry, true);
						opCycle = 0;
						break;
					case 6: // SCF
						SetFlag(FlagId.Negative, false);
						SetFlag(FlagId.HalfCarry, false);
						SetFlag(FlagId.Carry, true);
						opCycle = 0;
						break;
					case 7: // CCF
						SetFlag(FlagId.Negative, false);
						SetFlag(FlagId.HalfCarry, false);
						SetFlag(FlagId.Carry, !GetFlag(FlagId.Carry));
						opCycle = 0;
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void CycleX1()
	{
		var opY = (op >> 3) & 0b111;
		var opZ = op & 0b111;

		if ((Reg8Id)opZ == Reg8Id.MHL && (Reg8Id)opY == Reg8Id.MHL) // HALT
		{
			halted = true;
			opCycle = 0;
			return;
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
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void CycleX2()
	{
		var opY = (op >> 3) & 0b111;
		var opZ = op & 0b111;

		if (opCycle == 1)
		{
			readLo = GetReg8((Reg8Id)opZ);
			if ((Reg8Id)opZ == Reg8Id.MHL)
				return;
		}

		AluOperation(opY, readLo);

		opCycle = 0;
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
					case >= 0 and <= 3:
						switch (opCycle)
						{
							case 2:
								if (!CheckCondition((ConditionId)opY))
									opCycle = 0;
								break;
							case 3:
								readLo = bus[RegSP++];
								break;
							case 4:
								readHi = bus[RegSP++];
								break;
							case 5:
								RegPC = read16;
								opCycle = 0;
								break;
						}
						break;
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
					case 5: // ADD SP, i8
						switch (opCycle)
						{
							case 2:
								readLo = bus[RegPC++];
								break;
							case 3:
								SetFlag(FlagId.HalfCarry, (((RegSP & 0xF) + (readLo & 0xF)) & 0x10) == 0x10);
								SetFlag(FlagId.Carry, (byte)RegSP + readLo > 0xFF);
								RegSP = (ushort)(RegSP + (sbyte)readLo);
								SetFlag(FlagId.Zero, false);
								SetFlag(FlagId.Negative, false);
								break;
							case 4:
								opCycle = 0;
								break;
						}
						break;
					case 6: // LD A,(FF00+u8)
						switch (opCycle)
						{
							case 2:
								readLo = bus[RegPC++];
								break;
							case 3:
								SetReg8(Reg8Id.A, bus[(ushort)(0xFF00 + readLo)]);
								opCycle = 0;
								break;
						}
						break;
					case 7: // LD HL, SP + i8
					{
						switch (opCycle)
						{
							case 2:
								readLo = bus[RegPC++];
								RegHL = (ushort)(RegSP + (sbyte)readLo);
								SetFlag(FlagId.Zero, false);
								SetFlag(FlagId.Negative, false);
								SetFlag(FlagId.HalfCarry, (((RegSP & 0xF) + (readLo & 0xF)) & 0x10) == 0x10);
								SetFlag(FlagId.Carry, (byte)RegSP + readLo > 0xFF);
								break;
							case 3:
								opCycle = 0;
								break;
						}
						break;
					}
					default:
						return false;
				}
				break;
			case 1:
				switch (opQ)
				{
					case 0: // POP r16
						switch (opCycle)
						{
							case 2:
								SetReg8(GetReg16AFLoId((Reg16Id)opP), bus[RegSP++]);
								break;
							case 3:
								SetReg8(GetReg16AFHiId((Reg16Id)opP), bus[RegSP++]);
								opCycle = 0;
								break;
						}
						break;
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
							case 1: // RETI
								intsEnabled = true;
								goto case 0;
							case 2: // JP HL
								RegPC = RegHL;
								opCycle = 0;
								break;
							case 3: // LD SP, HL
								if (opCycle == 2)
								{
									RegSP = RegHL;
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
			case 2:
				switch (opY)
				{
					case >= 0 and <= 3: // JP cond, u16
						switch (opCycle)
						{
							case 2:
								readLo = bus[RegPC++];
								break;
							case 3:
								readHi = bus[RegPC++];
								if (!CheckCondition((ConditionId)opY))
									opCycle = 0;
								break;
							case 4:
								RegPC = read16;
								opCycle = 0;
								break;
						}
						break;
					case 4: // LD (0xFF00+C), A
						if (opCycle == 2)
						{
							bus[(ushort)(0xFF00 + GetReg8(Reg8Id.C))] = GetReg8(Reg8Id.A);
							opCycle = 0;
						}
						break;
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
					case 6: // LD A, (0xFF00+C)
						if (opCycle == 2)
						{
							SetReg8(Reg8Id.A, bus[(ushort)(0xFF00 + GetReg8(Reg8Id.C))]);
							opCycle = 0;
						}
						break;
					case 7: // LD A, (u16)
						switch (opCycle)
						{
							case 2:
								readLo = bus[RegPC++];
								break;
							case 3:
								readHi = bus[RegPC++];
								break;
							case 4:
								SetReg8(Reg8Id.A, bus[read16]);
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
					case 1: // CB prefix
					{
						switch (opCycle)
						{
							case 2:
								readLo = bus[RegPC++];
								if ((Reg8Id)(readLo & 0b111) != Reg8Id.MHL)
									goto case 3;
								break;
							case 3:
								readHi = GetReg8((Reg8Id)(readLo & 0b111));

								var bit = (readLo >> 3) & 0b111;

								switch (readLo >> 6)
								{
									case 0:
										switch (bit)
										{
											case 0: // RLC r8
											{
												var msb = readHi >> 7;

												readHi <<= 1;
												readHi |= (byte)msb;

												SetFlag(FlagId.Zero, readHi == 0);
												SetFlag(FlagId.Negative, false);
												SetFlag(FlagId.HalfCarry, false);
												SetFlag(FlagId.Carry, msb == 1);
												break;
											}
											case 1: // RRC r8
											{
												var lsb = readHi & 1;

												readHi >>= 1;
												readHi |= (byte)(lsb << 7);

												SetFlag(FlagId.Zero, readHi == 0);
												SetFlag(FlagId.Negative, false);
												SetFlag(FlagId.HalfCarry, false);
												SetFlag(FlagId.Carry, lsb == 1);
												break;
											}
											case 2: // RL r8
											{
												var msb = readHi >> 7;

												readHi <<= 1;
												if (GetFlag(FlagId.Carry))
													readHi |= 1;

												SetFlag(FlagId.Zero, readHi == 0);
												SetFlag(FlagId.Negative, false);
												SetFlag(FlagId.HalfCarry, false);
												SetFlag(FlagId.Carry, msb == 1);
												break;
											}
											case 3: // RR r8
											{
												var lsb = readHi & 1;

												readHi >>= 1;
												if (GetFlag(FlagId.Carry))
													readHi |= 1 << 7;

												SetFlag(FlagId.Zero, readHi == 0);
												SetFlag(FlagId.Negative, false);
												SetFlag(FlagId.HalfCarry, false);
												SetFlag(FlagId.Carry, lsb == 1);
												break;
											}
											case 4: // SLA r8
											{
												var msb = readHi >> 7;

												readHi <<= 1;

												SetFlag(FlagId.Zero, readHi == 0);
												SetFlag(FlagId.Negative, false);
												SetFlag(FlagId.HalfCarry, false);
												SetFlag(FlagId.Carry, msb == 1);
												break;
											}
											case 5: // SRA r8
											{
												var lsb = readHi & 1;

												var msb = readHi >> 7;
												readHi >>= 1;
												readHi |= (byte)(msb << 7);

												SetFlag(FlagId.Zero, readHi == 0);
												SetFlag(FlagId.Negative, false);
												SetFlag(FlagId.HalfCarry, false);
												SetFlag(FlagId.Carry, lsb == 1);
												break;
											}
											case 6: // SWAP r8
											{
												var hi = readHi >> 4;
												var lo = readHi & 0x0F;

												var result = (lo << 4) | hi;

												readHi = (byte)result;

												SetFlag(FlagId.Zero, readHi == 0);
												SetFlag(FlagId.Negative, false);
												SetFlag(FlagId.HalfCarry, false);
												SetFlag(FlagId.Carry, false);
												break;
											}
											case 7: // SRL r8
											{
												var lsb = readHi & 1;

												readHi >>= 1;

												SetFlag(FlagId.Zero, readHi == 0);
												SetFlag(FlagId.Negative, false);
												SetFlag(FlagId.HalfCarry, false);
												SetFlag(FlagId.Carry, lsb == 1);
												break;
											}
										}
										break;
									case 1: // BIT bit, r8
									{
										var b = (readHi >> bit) & 1;

										SetFlag(FlagId.Zero, b == 0);
										SetFlag(FlagId.Negative, false);
										SetFlag(FlagId.HalfCarry, true);

										opCycle = 0;
										break;
									}
									case 2: // RES bit, r8
										readHi &= (byte)~(1 << bit);
										break;
									case 3: // SET bit, r8
										readHi |= (byte)(1 << bit);
										break;
								}

								if ((Reg8Id)(readLo & 0b111) != Reg8Id.MHL && opCycle != 0)
									goto case 4;
								break;
							case 4:
							{
								SetReg8((Reg8Id)(readLo & 0b111), readHi);
								opCycle = 0;
								break;
							}
						}
						break;
					}
					case 6: // DI
						intsEnabled = false;
						opCycle = 0;
						break;
					case 7: // EI
						intsEnabled = true;
						opCycle = 0;
						break;
					default:
						return false;
				}
				break;
			case 4: // CALL cond, u16
				if (opY >= 4) // Invalid opcode
					break;

				switch (opCycle)
				{
					case 2:
						readLo = bus[RegPC++];
						break;
					case 3:
						readHi = bus[RegPC++];
						if (!CheckCondition((ConditionId)opY))
							opCycle = 0;
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
			case 5:
				switch (opQ)
				{
					case 0: // PUSH r16
						switch (opCycle)
						{
							case 3:
								bus[--RegSP] = GetReg8(GetReg16AFHiId((Reg16Id)opP));
								break;
							case 4:
								bus[--RegSP] = GetReg8(GetReg16AFLoId((Reg16Id)opP));
								opCycle = 0;
								break;
						}
						break;
					case 1:
						if (opP != 0) // Invalid instructions, hang
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
			case 6:
				switch (opCycle)
				{
					case 1:
						readLo = bus[RegPC++];
						break;
					case 2:
						AluOperation(opY, readLo);
						opCycle = 0;
						break;
				}
				break;
			case 7: // RST
				switch (opCycle)
				{
					case 3:
						bus[--RegSP] = (byte)(RegPC >> 8);
						break;
					case 4:
						bus[--RegSP] = (byte)RegPC;
						RegPC = (ushort)(opY * 8);
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
	private void AluOperation(int id, byte value)
	{
		var regA = GetReg8(Reg8Id.A);

		switch (id)
		{
			case 0: // ADD A
				SetFlag(FlagId.HalfCarry, (((regA & 0xF) + (value & 0xF)) & 0x10) == 0x10);
				SetFlag(FlagId.Carry, regA + value > 0xFF);
				regA += value;
				SetFlag(FlagId.Zero, regA == 0);
				SetFlag(FlagId.Negative, false);
				break;
			case 1: // ADC A
			{
				var carry = GetFlag(FlagId.Carry) ? (byte)1 : (byte)0;
				SetFlag(FlagId.HalfCarry, (((regA & 0xF) + (value & 0xF) + carry) & 0x10) == 0x10);
				SetFlag(FlagId.Carry, regA + value + carry > 0xFF);
				regA += value;
				regA += carry;
				SetFlag(FlagId.Zero, regA == 0);
				SetFlag(FlagId.Negative, false);
				break;
			}
			case 2: // SUB A
				SetFlag(FlagId.HalfCarry, (((regA & 0xF) - (value & 0xF)) & 0x10) == 0x10);
				SetFlag(FlagId.Carry, regA - value < 0);
				regA -= value;
				SetFlag(FlagId.Zero, regA == 0);
				SetFlag(FlagId.Negative, true);
				break;
			case 3: // SBC A
			{
				var carry = GetFlag(FlagId.Carry) ? (byte)1 : (byte)0;
				SetFlag(FlagId.HalfCarry, (((regA & 0xF) - (value & 0xF) - carry) & 0x10) == 0x10);
				SetFlag(FlagId.Carry, regA - value - carry < 0);
				regA -= value;
				regA -= carry;
				SetFlag(FlagId.Zero, regA == 0);
				SetFlag(FlagId.Negative, true);
				break;
			}
			case 4: // AND A
				regA &= value;
				SetFlag(FlagId.Zero, regA == 0);
				SetFlag(FlagId.Negative, false);
				SetFlag(FlagId.HalfCarry, true);
				SetFlag(FlagId.Carry, false);
				break;
			case 5: // XOR A
				regA ^= value;
				SetFlag(FlagId.Zero, regA == 0);
				SetFlag(FlagId.Negative, false);
				SetFlag(FlagId.HalfCarry, false);
				SetFlag(FlagId.Carry, false);
				break;
			case 6: // OR A
				regA |= value;
				SetFlag(FlagId.Zero, regA == 0);
				SetFlag(FlagId.Negative, false);
				SetFlag(FlagId.HalfCarry, false);
				SetFlag(FlagId.Carry, false);
				break;
			case 7: // CP A
				SetFlag(FlagId.HalfCarry, (((regA & 0xF) - (value & 0xF)) & 0x10) == 0x10);
				SetFlag(FlagId.Carry, regA - value < 0);
				regA -= value;
				SetFlag(FlagId.Zero, regA == 0);
				SetFlag(FlagId.Negative, true);
				return;
		}

		SetReg8(Reg8Id.A, regA);
	}
}
