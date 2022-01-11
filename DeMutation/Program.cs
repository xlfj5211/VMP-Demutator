using System;
using System.Collections.Generic;
using System.Linq;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace DeMutation
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			string path = args[0];
			ModuleDefMD module = ModuleDefMD.Load(path);
			BlocksCflowDeobfuscator deobfuscator = new BlocksCflowDeobfuscator();
			deobfuscator.Add(new ControlFlowDeobfuscator());
			foreach (TypeDef type in module.GetTypes())
			{
				foreach (MethodDef method in type.Methods)
				{
					if (method.HasBody)
					{
						if (method.Body.HasInstructions)
						{
							LocalList locals = method.Body.Variables;
							if (locals.Any((Local x) => x.Type.ElementType == ElementType.U4))
							{
								Blocks blocks = new Blocks(method);
								deobfuscator.Initialize(blocks);
								deobfuscator.Deobfuscate();
								IList<Instruction> allInstructions;
								IList<ExceptionHandler> allExceptionHandlers;
								blocks.GetCode(out allInstructions, out allExceptionHandlers);
								DotNetUtils.RestoreBody(method, allInstructions, allExceptionHandlers);
							}
						}
					}
				}
			}
			NativeModuleWriterOptions writerOptions = new NativeModuleWriterOptions(module, false);
			writerOptions.Logger = DummyLogger.NoThrowInstance;
			writerOptions.MetadataOptions.Flags = MetadataFlags.PreserveAll;
			module.NativeWrite(path.Replace(".exe", "-cleaned.exe"), writerOptions);
		}
	}
}
