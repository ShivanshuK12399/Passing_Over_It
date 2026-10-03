using System;
using UnityEngine;

namespace PassingOverIt.Networking
{
    /// <summary>
    /// Helper class to parse command-line flags when launching standalone/headless Dedicated Server executable.
    /// Example usage: PassingOverItServer.exe -batchmode -nographics -port 7777 -maxplayers 20
    /// </summary>
    public static class ServerCommandLineArgs
    {
        public const ushort DefaultPort = 7777;
        public const string DefaultBindIP = "0.0.0.0";
        public const string DefaultConnectIP = "127.0.0.1";
        public const int DefaultMaxPlayers = 20;

        /// <summary>
        /// Checks if the process was launched with server or batchmode flags.
        /// </summary>
        public static bool IsDedicatedServerRequested()
        {
            if (Application.isBatchMode) return true;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();
                if (arg == "-server" || arg == "-dedicated" || arg == "-dedicatedserver" || arg == "-batchmode")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Retrieves the port specified via command line (-port 7777). Defaults to 7777 if unassigned.
        /// </summary>
        public static ushort GetPort()
        {
            string value = GetArgValue("-port");
            if (!string.IsNullOrEmpty(value) && ushort.TryParse(value, out ushort port))
            {
                return port;
            }
            return DefaultPort;
        }

        /// <summary>
        /// Retrieves the IP address specified via command line (-ip 127.0.0.1). Defaults to 0.0.0.0 for server bind.
        /// </summary>
        public static string GetIP()
        {
            string value = GetArgValue("-ip");
            if (!string.IsNullOrEmpty(value))
            {
                return value.Trim();
            }
            return DefaultBindIP;
        }

        /// <summary>
        /// Retrieves max players specified via command line (-maxplayers 20). Defaults to 20.
        /// </summary>
        public static int GetMaxPlayers()
        {
            string value = GetArgValue("-maxplayers");
            if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int maxPlayers))
            {
                return maxPlayers;
            }
            return DefaultMaxPlayers;
        }

        private static string GetArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}
