﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using URCL.NET.Compiler;
using URCL.NET.VM;

namespace URCL.NET
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var configuration = new Configuration();

                //Load the configuration inputs, either from the command line arguments or console input.
                var arg = 0;
                string configLine = null;
                Type configType = configuration.GetType();
                while (configLine != string.Empty)
                {
                    Console.Write("urcl/config> ");
                    configLine = args.Length > 0 ? (arg >= (args.Length - 1) ? string.Empty : args[arg]) : Console.ReadLine().Trim();
                    arg++;

                    if (args.Length > 0) Console.WriteLine(configLine);

                    if (configLine == string.Empty) break;

                    var configArgs = configLine.Split(' ');

                    if (configArgs.Length == 0) break;

                    bool found = false;
                    foreach (var prop in configType.GetProperties())
                    {
                        if (prop.CanWrite && prop.Name.ToLower() == configArgs[0].ToLower())
                        {
                            found = true;

                            if (prop.PropertyType == typeof(bool))
                            {
                                prop.SetValue(configuration, true);
                            }
                            else if (configArgs.Length < 2)
                            {
                                Console.Error.WriteLine($"Configuration \"{prop.Name}\" requires a value.");
                            }
                            else if (prop.PropertyType == typeof(int))
                            {
                                if (int.TryParse(configArgs[1], out int value))
                                {
                                    prop.SetValue(configuration, value);
                                }
                                else
                                {
                                    Console.Error.WriteLine($"Value \"{configArgs[1]}\" is not valid for configuration \"{prop.Name}\".");
                                }
                            }
                            else if (prop.PropertyType == typeof(long))
                            {
                                if (long.TryParse(configArgs[1], out long value))
                                {
                                    prop.SetValue(configuration, value);
                                }
                                else
                                {
                                    Console.Error.WriteLine($"Value \"{configArgs[1]}\" is not valid for configuration \"{prop.Name}\".");
                                }
                            }
                            else if (prop.PropertyType == typeof(ulong))
                            {
                                if (ulong.TryParse(configArgs[1], out ulong value))
                                {
                                    prop.SetValue(configuration, value);
                                }
                                else
                                {
                                    Console.Error.WriteLine($"Value \"{configArgs[1]}\" is not valid for configuration \"{prop.Name}\".");
                                }
                            }
                            else if (prop.PropertyType == typeof(string))
                            {
                                prop.SetValue(configuration, string.Join(" ", configArgs.Skip(1)));
                            }
                            else
                            {
                                Console.Error.WriteLine($"Configuration \"{prop.Name}\" is not supported.");
                            }
                        }
                    }

                    if (!found) Console.Error.WriteLine($"Configuration \"{configArgs[0]}\" is not valid.");
                }

                //Load the input file, either from the command line arguments or console input.
                Console.Write("urcl/input> ");
                string file = args.Length > 0 ? args[^1] : Console.ReadLine();
                if (args.Length > 0) Console.WriteLine(file);

                if (string.IsNullOrEmpty(configuration.Output)) configuration.Output = "output.txt";

                //Load compiler modules.
                var moduleLoader = new ModuleLoader();
                moduleLoader.AddFileType("urcl", "URCL");
                moduleLoader.Load(configuration);

                //Process the file based on its type.
                if (File.Exists(file))
                {
                    var inExt = Path.GetExtension(file).ToLower().TrimStart('.');
                    var outExt = Path.GetExtension(configuration.Output).ToLower().TrimStart('.');

                    Console.WriteLine($"Processing {moduleLoader.GetFileType(inExt)} source \"{file}\" to {moduleLoader.GetFileType(outExt)} \"{configuration.Output}\".");

                    if (inExt == "urcl")
                    {
                        var instructions = Parser.Parse(File.ReadAllLines(file));

                        if (configuration.Emulate)
                        {
                            Emulator(configuration, instructions);
                        }
                        else
                        {
                            using var stream = new FileStream(configuration.Output, FileMode.Create, FileAccess.ReadWrite);

                            if (!moduleLoader.ExecuteEmitter(outExt, stream.WriteByte, instructions))
                            {
                                Console.WriteLine($"File \"{file}\" is not supported.");
                                Environment.Exit(2);
                                return;
                            }
                        }
                    }
                    else
                    {
                        using var input = new FileStream(file, FileMode.Open, FileAccess.Read);

                        if (configuration.Emulate)
                        {
                            var lines = new List<string>();

                            if (!moduleLoader.ExecuteFileHandler(inExt, input, lines.Add))
                            {
                                Console.WriteLine($"File \"{file}\" is not supported.");
                                Environment.Exit(2);
                                return;
                            }

                            Emulator(configuration, Parser.Parse(lines));
                        }
                        else
                        {
                            using var writer = new StreamWriter(new FileStream(configuration.Output, FileMode.Create, FileAccess.ReadWrite));

                            if (!moduleLoader.ExecuteFileHandler(inExt, input, writer.WriteLine))
                            {
                                Console.WriteLine($"File \"{file}\" is not supported.");
                                Environment.Exit(2);
                                return;
                            }
                        }
                    }

                    return;
                }
                else
                {
                    Console.WriteLine($"File \"{file}\" was not found.");
                    Environment.Exit(1);
                }

                Console.WriteLine("Finished.");
            }
            catch (ParserError ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal Error: {ex}");
            }
        }

        private static void Emulator(Configuration configuration, IEnumerable<UrclInstruction> instructions)
        {
            instructions = instructions.Append(new UrclInstruction(Operation.HLT));

            var machine = new UrclMachine(1, configuration.Registers, configuration.AvailableMemory, configuration.AvailableROM, configuration.ExecuteOnROM, configuration.WordBitMask, new ConsoleIO());

            if (configuration.ExecuteOnROM)
            {
                machine.LoadROM(0, instructions);
            }
            else
            {
                machine.LoadRAM(0, instructions);
            }

            Console.WriteLine();

            var start = Environment.TickCount64;
            var timeLimit = (long)configuration.MaxTime + start;

            while (!machine.Halted && (configuration.StepThrough || timeLimit - Environment.TickCount64 > 0))
            {
                try
                {
                    var brk = machine.Clock();

                    if ((brk && !configuration.DisableBreak) || configuration.StepThrough)
                    {
                        if (!configuration.StepThrough)
                        {
                            Console.WriteLine("Breakpoint hit! System suspended.");
                            Console.WriteLine("Dumping machine state...");
                        }

                        RenderMachineState(machine, configuration.StepThrough);

                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey(true);
                    }
                }
                catch (UrclMachine.InvalidOperationException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Fault! {ex.Message}");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                }
            }

            if (!machine.Halted) Console.WriteLine("Maximum time for execution was exceeded!");

            Console.WriteLine($"System halted. Execution finished in {Environment.TickCount64 - start}ms ({machine.Ticks} ticks).");
            Console.WriteLine("Dumping final machine state...");

            RenderMachineState(machine, true);
        }

        private static void RenderMachineState(UrclMachine machine, bool allCores)
        {
            Console.WriteLine($"Cores: {machine.Cores.LongLength}, Word Mask: 0x{machine.BitMask:X}, RAM: {machine.RAM.LongLength} words, ROM: {machine.ROM.LongLength} words");
            Console.WriteLine($"Current Core: {machine.CurrentCore}");

            if (allCores)
            {
                ulong i = 0;
                foreach (var core in machine.Cores)
                {
                    RenderCoreState(i, core);
                    i++;
                }
            }
            else
            {
                RenderCoreState(machine.CurrentCore, machine.Cores[machine.CurrentCore]);
            }
        }

        private static void RenderCoreState(ulong index, UrclMachine.Core core)
        {
            Console.WriteLine($"Core {index}:");
            Console.WriteLine($"\tInstruction Pointer: 0x{core.InstructionPointer.ToString("X").PadLeft(8, '0')}, Last Value: {core.Flags:X}, Halted: {(core.Halted ? "Yes" : "No")}");

            Console.WriteLine("\tRegisters:");
            for (ulong i = 0; i < (ulong)core.Registers.LongLength; i++)
            {
                Console.WriteLine($"\t\tR{i + 1}: 0x{core.Registers[i].ToString("X").PadLeft(8, '0')}");
            }

            var stack = core.CallStack.ToArray();

            Console.WriteLine("\tCall Stack:");
            for (ulong i = 0; i < (ulong)stack.LongLength; i++)
            {
                Console.WriteLine($"\t\t[{i}]: 0x{stack[i].ToString("X").PadLeft(8, '0')}");
            }

            stack = core.ValueStack.ToArray();

            Console.WriteLine("\tValue Stack:");
            for (ulong i = 0; i < (ulong)stack.LongLength; i++)
            {
                Console.WriteLine($"\t\t[{i}]: 0x{stack[i].ToString("X").PadLeft(8, '0')}");
            }
        }
    }
}
