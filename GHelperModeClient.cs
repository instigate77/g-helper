using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GHelperModeClient
{
    /// <summary>
    /// Simple client utility to send mode commands to a running G-Helper instance
    /// Usage: GHelperModeClient.exe turbo|performance|silent
    /// </summary>
    class Program
    {
        private const int IPC_PORT = 12345;

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: GHelperModeClient.exe <mode>");
                Console.WriteLine("Modes: turbo, performance, silent");
                return;
            }

            string mode = args[0].ToLower();
            if (mode != "turbo" && mode != "performance" && mode != "silent")
            {
                Console.WriteLine("Invalid mode. Use: turbo, performance, or silent");
                return;
            }

            if (SendModeCommand(mode))
            {
                Console.WriteLine($"Successfully sent mode command '{mode}' to G-Helper");
            }
            else
            {
                Console.WriteLine("Failed to send command. Make sure G-Helper is running.");
            }
        }

        private static bool SendModeCommand(string mode)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var connectTask = client.ConnectAsync(IPAddress.Loopback, IPC_PORT);
                    if (!connectTask.Wait(3000))
                    {
                        Console.WriteLine("Connection timeout - G-Helper may not be running");
                        return false;
                    }

                    using (var stream = client.GetStream())
                    using (var writer = new StreamWriter(stream, Encoding.UTF8))
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        writer.WriteLine($"mode:{mode}");
                        writer.Flush();

                        var response = reader.ReadLine();
                        if (response == "OK")
                        {
                            return true;
                        }
                        else
                        {
                            Console.WriteLine($"G-Helper responded with: {response}");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }
    }
}
