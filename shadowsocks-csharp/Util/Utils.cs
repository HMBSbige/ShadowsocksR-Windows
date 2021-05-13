using Shadowsocks.Controller;
using Shadowsocks.Encryption;
using Shadowsocks.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

#nullable enable

namespace Shadowsocks.Util
{
    public static class Utils
    {
        public static void SetArrayMinSize<T>(ref T[] array, int size)
        {
            if (size > array.Length)
            {
                Array.Resize(ref array, size);
            }
        }

        public static void SetArrayMinSize2<T>(ref T[] array, int size)
        {
            if (size > array.Length)
            {
                Array.Resize(ref array, size * 2);
            }
        }

        public static string GetExecutablePath()
        {
            var p = Process.GetCurrentProcess();
            var res = p.MainModule?.FileName;
            if (res is not null)
            {
                return res;
            }

            var dllPath = GetDllPath();
            return Path.ChangeExtension(dllPath, @"exe");
        }

        public static string GetDllPath()
        {
            return Assembly.GetExecutingAssembly().Location;
        }

        /// <summary>
        /// return path to store temporary files
        /// </summary>
        public static string TempPath => TempPathLazy.Value;
        private static readonly Lazy<string> TempPathLazy = new(() =>
        {
            try
            {
                return Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), @"temp")).FullName;
            }
            catch (Exception e)
            {
                Logging.Error(e);
                throw;
            }
        });

        public static string GetTempPath(string filename)
        {
            return Path.Combine(TempPath, filename);
        }

        public static bool IsGFWListPAC(string filename)
        {
            if (File.Exists(filename))
            {
                var original = FileManager.NonExclusiveReadAllText(filename, Encoding.UTF8);
                if (original.Contains(@"adblockplus") && !original.Contains(@"cnIpRange"))
                {
                    return true;
                }
            }

            return false;
        }

        public static void OpenURL(string path)
        {
            new Process
            {
                StartInfo = new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                }
            }.Start();
        }

        public static string EncryptLargeBytesToBase64String(IEncryptor encryptor, byte[] cfgData)
        {
            var cfgEncrypt = new byte[cfgData.Length + 128];
            var dataLen = 0;
            const int bufferSize = 32768;
            var input = new byte[bufferSize];
            var output = new byte[bufferSize + 128];
            for (var startPos = 0; startPos < cfgData.Length; startPos += bufferSize)
            {
                var len = Math.Min(cfgData.Length - startPos, bufferSize);
                Buffer.BlockCopy(cfgData, startPos, input, 0, len);
                encryptor.Encrypt(input, len, output, out var outLen);
                Buffer.BlockCopy(output, 0, cfgEncrypt, dataLen, outLen);
                dataLen += outLen;
            }

            return Convert.ToBase64String(cfgEncrypt, 0, dataLen);
        }

        public static byte[] DecryptLargeBase64StringToBytes(IEncryptor encryptor, string encodeStr)
        {
            var cfgEncrypt = Convert.FromBase64String(encodeStr);
            var cfgData = new byte[cfgEncrypt.Length];
            var dataLen = 0;
            const int bufferSize = 32768;
            var input = new byte[bufferSize];
            var output = new byte[bufferSize + 128];
            for (var startPos = 0; startPos < cfgEncrypt.Length; startPos += bufferSize)
            {
                var len = Math.Min(cfgEncrypt.Length - startPos, bufferSize);
                Buffer.BlockCopy(cfgEncrypt, startPos, input, 0, len);
                encryptor.Decrypt(input, len, output, out var outLen);
                Buffer.BlockCopy(output, 0, cfgData, dataLen, outLen);
                dataLen += outLen;
            }

            Array.Resize(ref cfgData, dataLen);
            return cfgData;
        }

        public static string FormatBytes(long bytes)
        {
            const long K = 1024L;
            const long M = K * 1024L;
            const long G = M * 1024L;
            const long T = G * 1024L;
            const long P = T * 1024L;
            const long E = P * 1024L;

            return bytes switch
            {
                >= M * 990 and >= G * 990 => bytes switch
                {
                    >= P * 990 => $@"{bytes / (double)E:F3}EB",
                    >= T * 990 => $@"{bytes / (double)P:F3}PB",
                    _ => $@"{bytes / (double)T:F3}TB"
                },
                >= M * 990 and >= G * 99 => $@"{bytes / (double)G:F2}GB",
                >= M * 990 and >= G * 9 => $@"{bytes / (double)G:F3}GB",
                >= M * 990 => $@"{bytes / (double)G:F4}GB",
                >= K * 990 and >= M * 100 => $@"{bytes / (double)M:F1}MB",
                >= K * 990 when bytes > M * 9.9 => $@"{bytes / (double)M:F2}MB",
                >= K * 990 => $@"{bytes / (double)M:F3}MB",
                > K * 99 => $@"{bytes / (double)K:F0}KB",
                > 900 => $@"{bytes / (double)K:F1}KB",
                _ => bytes == 0 ? $@"{bytes}Byte" : $@"{bytes}Bytes"
            };
        }

        public static IEnumerable<Server> Except(this IEnumerable<Server> x, IList<Server> y)
        {
            return from xi in x let found = y.Any(xi.IsMatchServer) where !found select xi;
        }

        public static IEnumerable<string> GetLines(this string str, bool removeEmptyLines = true)
        {
            using var sr = new StringReader(str);
            string? line;
            while ((line = sr.ReadLine()) is not null)
            {
                if (removeEmptyLines && string.IsNullOrEmpty(line))
                {
                    continue;
                }
                yield return line;
            }
        }

        public static void SetTls()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20170))
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls13;
            }
        }
    }
}
