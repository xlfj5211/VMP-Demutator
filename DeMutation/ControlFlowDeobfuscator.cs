using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace DeMutation
{
	public class ControlFlowDeobfuscator : IBlocksDeobfuscator
	{
		public void DeobfuscateBegin(Blocks blocks)
		{
			MethodDef method = blocks.Method;
			this.Method = method;
			ControlFlowDeobfuscator.Emulator.Initialize(method);
			this.Context = this.FindContext(method);
			this.VisitedBlocks = new List<Block>();
		}

		public bool ExecuteIfNotModified
		{
			get
			{
				return true;
			}
		}

		public bool Deobfuscate(List<Block> allBlocks)
		{
			bool result;
			if (this.Context == null)
			{
				result = false;
			}
			else
			{
				foreach (Block block in allBlocks)
				{
					this.ProcessBlock(block, null);
				}
				result = false;
			}
			return result;
		}

		private void ProcessBlock(Block block, Value value = null)
		{
			if (!this.VisitedBlocks.Contains(block))
			{
				this.VisitedBlocks.Add(block);
				if (value != null)
				{
					ControlFlowDeobfuscator.Emulator.SetLocal(this.Context, value);
				}
				foreach (Instr instr in block.Instructions)
				{
					this.ProcessInstruction(instr.Instruction);
				}
				Value currentValue = ControlFlowDeobfuscator.Emulator.GetLocal(this.Context);
				foreach (Block target in block.GetTargets())
				{
					this.ProcessBlock(target, currentValue);
				}
			}
		}

		private void ProcessInstruction(Instruction instruction)
		{
			if (instruction.IsStloc())
			{
				Local local = instruction.GetLocal(this.Method.Body.Variables);
				if (local == this.Context)
				{
					ControlFlowDeobfuscator.Emulator.Emulate(instruction);
				}
				else
				{
					ControlFlowDeobfuscator.Emulator.Pop();
					ControlFlowDeobfuscator.Emulator.MakeLocalUnknown(local);
				}
			}
			else
			{
				if (instruction.IsLdloc())
				{
					ControlFlowDeobfuscator.Emulator.Emulate(instruction);
					Local local2 = instruction.GetLocal(this.Method.Body.Variables);
					if (local2 == this.Context)
					{
						instruction.OpCode = OpCodes.Ldc_I4;
						instruction.Operand = (ControlFlowDeobfuscator.Emulator.Peek() as Int32Value).Value;
					}
				}
				else
				{
					ControlFlowDeobfuscator.Emulator.Emulate(instruction);
				}
			}
		}

		private Local FindContext(MethodDef method)
		{
			Dictionary<Local, int> frequencies = new Dictionary<Local, int>();
			LocalList locals = method.Body.Variables;
			foreach (Local local in locals)
			{
				if (local.Type.ElementType == ElementType.U4)
				{
					frequencies.Add(local, 0);
				}
			}
			IList<Instruction> instructions = method.Body.Instructions;
			int i = 0;
			while (instructions.Count > i)
			{
				Instruction instr = instructions[i];
				if (instr.OpCode == OpCodes.Ldloca || instr.OpCode == OpCodes.Ldloca_S)
				{
					Local local2 = instr.GetLocal(locals);
					if (frequencies.ContainsKey(local2))
					{
						frequencies.Remove(local2);
					}
				}
				else
				{
					if (instr.IsStloc())
					{
						Local local3 = instr.GetLocal(locals);
						if (frequencies.ContainsKey(local3))
						{
							Instruction before = instructions[i - 1];
							if (!before.IsLdcI4() && !this.IsArithmethic(before))
							{
								frequencies.Remove(local3);
							}
							else
							{
								int current = frequencies[local3];
								frequencies[local3] = current + 1;
							}
						}
					}
				}
				i++;
			}
			Local result;
			if (frequencies.Count == 1)
			{
				result = frequencies.Keys.ToArray<Local>()[0];
			}
			else
			{
				if (frequencies.Count == 0)
				{
					result = null;
				}
				else
				{
					int highestCount = 0;
					Local highestLocal = null;
					foreach (KeyValuePair<Local, int> entry in frequencies)
					{
						if (entry.Value > highestCount)
						{
							highestLocal = entry.Key;
							highestCount = entry.Value;
						}
					}
					result = highestLocal;
				}
			}
			return result;
		}

		private bool IsArithmethic(Instruction instruction)
		{
			return ControlFlowDeobfuscator.ArithmethicCodes.Contains(instruction.OpCode.Code);
		}

		static ControlFlowDeobfuscator()
		{
			Code[] array = new Code[21];
            ControlFlowDeobfuscator.ArithmethicCodes = array;
		}

		private static InstructionEmulator Emulator = new InstructionEmulator();

		private Local Context;

		private MethodDef Method;

		private List<Block> VisitedBlocks;

		private static readonly Code[] ArithmethicCodes;
	}
}
