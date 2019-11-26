using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AppHostPatcher
{
    internal static class Program
    {
        private static void Usage()
        {
            Console.WriteLine(@"AppHostPatcher <appHostExe> <origDllPath> <newDllPath>");
            Console.WriteLine(@"AppHostPatcher <appHostExe> <newDllPath>");
            Console.WriteLine(@"AppHostPatcher <appHostExe> -d <newSubDir>");
            Console.WriteLine(@"example: AppHostPatcher my.exe -d bin");
        }

        private const int MaxPathBytes = 1024;

        private static string ChangeExecutableExtension(string appHostExe) =>
            // Windows appHosts have an .exe extension. Don't call Path.ChangeExtension() unless it's guaranteed
            // to have an .exe extension, eg. 'some.file' => 'some.file.dll', not 'some.dll'
            appHostExe.EndsWith(@".exe", StringComparison.OrdinalIgnoreCase) ? Path.ChangeExtension(appHostExe, @".dll") : $@"{appHostExe}.dll";

        private static string GetPathSeparator(string appHostExe) =>
            appHostExe.EndsWith(@".exe", StringComparison.OrdinalIgnoreCase) ? @"\" : @"/";

        private static int Main(string[] args)
        {
            try
            {
                string appHostExe, origPath, newPath;
                if (args.Length == 3)
                {
                    if (args[1] == @"-d")
                    {
                        appHostExe = args[0];
                        origPath = Path.GetFileName(ChangeExecutableExtension(appHostExe));
                        newPath = args[2] + GetPathSeparator(appHostExe) + origPath;
                    }
                    else
                    {
                        appHostExe = args[0];
                        origPath = args[1];
                        newPath = args[2];
                    }
                }
                else if (args.Length == 2)
                {
                    appHostExe = args[0];
                    origPath = Path.GetFileName(ChangeExecutableExtension(appHostExe));
                    newPath = args[1];
                }
                else
                {
                    Usage();
                    return 1;
                }
                if (!File.Exists(appHostExe))
                {
                    Console.WriteLine($@"AppHost '{appHostExe}' does not exist");
                    return 1;
                }
                if (origPath == string.Empty)
                {
                    Console.WriteLine(@"Original path is empty");
                    return 1;
                }
                var origPathBytes = Encoding.UTF8.GetBytes($"{origPath}\0");
                Debug.Assert(origPathBytes.Length > 0);
                var newPathBytes = Encoding.UTF8.GetBytes($"{newPath}\0");
                if (origPathBytes.Length > MaxPathBytes)
                {
                    Console.WriteLine(@"Original path is too long");
                    return 1;
                }
                if (newPathBytes.Length > MaxPathBytes)
                {
                    Console.WriteLine(@"New path is too long");
                    return 1;
                }

                var appHostExeBytes = File.ReadAllBytes(appHostExe);
                var offset = GetOffset(appHostExeBytes, origPathBytes);
                if (offset < 0)
                {
                    Console.WriteLine($@"Could not find original path '{origPath}'");
                    return 1;
                }
                if (offset + newPathBytes.Length > appHostExeBytes.Length)
                {
                    Console.WriteLine($@"New path is too long: {newPath}");
                    return 1;
                }
                for (var i = 0; i < newPathBytes.Length; i++)
                    appHostExeBytes[offset + i] = newPathBytes[i];
                File.WriteAllBytes(appHostExe, appHostExeBytes);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static int GetOffset(byte[] bytes, byte[] pattern)
        {
            var si = 0;
            var b = pattern[0];
            while (si < bytes.Length)
            {
                si = Array.IndexOf(bytes, b, si);
                if (si < 0)
                    break;
                if (Match(bytes, si, pattern))
                    return si;
                si++;
            }
            return -1;
        }

        private static bool Match(IReadOnlyList<byte> bytes, int index, byte[] pattern)
        {
            if (index + pattern.Length > bytes.Count)
                return false;
            return !pattern.Where((t, i) => bytes[index + i] != t).Any();
        }
    }
}
