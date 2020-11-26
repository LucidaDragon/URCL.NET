﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using URCL.NET.Compiler;
using URCL.NET.VM;

namespace URCL.NET.PlatformAPI
{
    public class APIModule
    {
        public static void HandleConfiguration(Configuration configuration)
        {
            if (configuration.ApiPort > 0)
            {
                var modules = new ModuleLoader();
                var tcp = new TcpListener(new IPEndPoint(IPAddress.Loopback, configuration.ApiPort));

                modules.Load(configuration);
                tcp.Start();

                while (true)
                {
                    try
                    {
                        using var connection = tcp.AcceptTcpClient();
                        using var stream = connection.GetStream();
                        using var reader = new BinaryReader(stream);
                        using var writer = new BinaryWriter(stream);

                        var builder = new ConfigurationBuilder();
                        var configCount = ushort.Parse(reader.ReadString());
                        for (ushort i = 0; i < configCount; i++)
                        {
                            builder.Configure(reader.ReadString(), writer.Write);
                        }

                        writer.Write(string.Empty);

                        var lang = reader.ReadString().ToLower();

                        stream.Flush();

                        var lineCount = ulong.Parse(reader.ReadString());

                        if (lineCount == 0) continue;

                        var lines = new List<string>();

                        for (ulong i = 0; i < lineCount; i++)
                        {
                            lines.Add(reader.ReadString());
                        }

                        IEnumerable<UrclInstruction> instructions;

                        try
                        {
                            if (lang != "urcl")
                            {
                                var source = lines;
                                lines = new List<string>();

                                if (!modules.ExecuteFileHandler(lang, source, lines.Add))
                                {
                                    throw new ParserError($"Format \"{lang}\" is not supported.");
                                }
                            }

                            instructions = Parser.Parse(lines);

                            EmulatorHost.Emulator(configuration, instructions, writer.Write, () => { }, false);
                        }
                        catch (ParserError ex)
                        {
                            writer.Write($"<P> {ex.Message}");
                            writer.Write(string.Empty);
                            stream.Flush();
                            continue;
                        }
                        catch (Exception ex)
                        {
                            writer.Write($"<C> {ex.Message}");
                            writer.Write(string.Empty);
                            continue;
                        }

                        writer.Write(string.Empty);
                        stream.Flush();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"API: {ex}");
                    }
                }
            }
        }
    }
}
