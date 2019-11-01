using Microsoft.Win32;
using Shadowsocks.Controller;
using Shadowsocks.Encryption;
using Shadowsocks.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Shadowsocks.Util
{
    public static class Utils
    {
        #region ReleaseMemory

        private static Process CurrentProcess => Process.GetCurrentProcess();

        [DllImport(@"kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, UIntPtr minimumWorkingSetSize,
                UIntPtr maximumWorkingSetSize);

        public static void ReleaseMemory(bool removePages = true)
        {
            // release any unused pages
            // making the numbers look good in task manager
            // this is totally nonsense in programming
            // but good for those users who care
            // making them happier with their everyday life
            // which is part of user experience
            GC.Collect(GC.MaxGeneration);
            GC.WaitForPendingFinalizers();
            if (removePages)
            {
                // as some users have pointed out
                // removing pages from working set will cause some IO
                // which lowered user experience for another group of users
                //
                // so we do 2 more things here to satisfy them:
                // 1. only remove pages once when configuration is changed
                // 2. add more comments here to tell users that calling
                //    this function will not be more frequent than
                //    IM apps writing chat logs, or web browsers writing cache files
                //    if they're so concerned about their disk, they should
                //    uninstall all IM apps and web browsers
                //
                // please open an issue if you're worried about anything else in your computer
                // no matter it's GPU performance, monitor contrast, audio fidelity
                // or anything else in the task manager
                // we'll do as much as we can to help you
                //
                // just kidding
                if (!Environment.Is64BitProcess)
                {
                    SetProcessWorkingSetSize(CurrentProcess.Handle, (UIntPtr)0xFFFFFFFF, (UIntPtr)0xFFFFFFFF);
                }
                else
                {
                    SetProcessWorkingSetSize(CurrentProcess.Handle, (UIntPtr)0xFFFFFFFFFFFFFFFF,
                            (UIntPtr)0xFFFFFFFFFFFFFFFF);
                }
            }
        }

        #endregion

        public static bool BitCompare(byte[] target, int target_offset, byte[] m, int m_offset, int targetLength)
        {
            for (var i = 0; i < targetLength; ++i)
            {
                if (target[target_offset + i] != m[m_offset + i])
                    return false;
            }

            return true;
        }

        public static int FindStr(byte[] target, int targetLength, byte[] m)
        {
            if (m.Length > 0 && targetLength >= m.Length)
            {
                for (var i = 0; i <= targetLength - m.Length; ++i)
                {
                    if (target[i] == m[0])
                    {
                        var j = 1;
                        for (; j < m.Length; ++j)
                        {
                            if (target[i + j] != m[j])
                                break;
                        }

                        if (j >= m.Length)
                        {
                            return i;
                        }
                    }
                }
            }

            return -1;
        }

        public static string GetTimestamp(DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssffff");
        }

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
            if (p.MainModule != null)
            {
                var res = p.MainModule.FileName;
                return res;
            }

            var dllPath = GetDllPath();
            return Path.Combine(Path.GetDirectoryName(dllPath) ?? throw new InvalidOperationException(), $@"{Path.GetFileNameWithoutExtension(dllPath)}.exe");
        }

        public static string GetDllPath()
        {
            return Assembly.GetExecutingAssembly().Location;
        }

        public static RegistryKey OpenRegKey(string name, bool writable, RegistryHive hive = RegistryHive.CurrentUser)
        {
            var userKey = RegistryKey.OpenBaseKey(hive,
                            Environment.Is64BitProcess ? RegistryView.Registry64 : RegistryView.Registry32)
                    .OpenSubKey(name, writable);
            return userKey;
        }

        public static int RunAsAdmin(string arguments)
        {
            Process process;
            var processInfo = new ProcessStartInfo
            {
                Verb = "runas",
                FileName = GetExecutablePath(),
                Arguments = arguments
            };
            try
            {
                process = Process.Start(processInfo);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return -1;
            }

            process?.WaitForExit();
            if (process != null)
            {
                var ret = process.ExitCode;
                process.Close();
                return ret;
            }

            return -1;
        }

        private static string _tempPath;

        // return path to store temporary files
        public static string GetTempPath()
        {
            if (_tempPath == null)
            {
                try
                {
                    _tempPath = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), @"temp"))
                            .FullName;
                }
                catch (Exception e)
                {
                    Logging.Error(e);
                    throw;
                }
            }

            return _tempPath;
        }

        public static string GetTempPath(string filename)
        {
            return Path.Combine(GetTempPath(), filename);
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

        public static int GetDeterministicHashCode(this string str)
        {
            unchecked
            {
                var hash1 = (5381 << 16) + 5381;
                var hash2 = hash1;

                for (var i = 0; i < str.Length; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1)
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + hash2 * 1566083941;
            }
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

            if (bytes >= M * 990)
            {
                if (bytes >= G * 990)
                {
                    if (bytes >= P * 990)
                        return $@"{bytes / (double)E:F3}EB";
                    if (bytes >= T * 990)
                        return $@"{bytes / (double)P:F3}PB";
                    return $@"{bytes / (double)T:F3}TB";
                }

                if (bytes >= G * 99)
                    return $@"{bytes / (double)G:F2}GB";
                if (bytes >= G * 9)
                    return $@"{bytes / (double)G:F3}GB";
                return $@"{bytes / (double)G:F4}GB";
            }

            if (bytes >= K * 990)
            {
                if (bytes >= M * 100)
                    return $@"{bytes / (double)M:F1}MB";
                if (bytes > M * 9.9)
                    return $@"{bytes / (double)M:F2}MB";
                return $@"{bytes / (double)M:F3}MB";
            }

            if (bytes > K * 99)
                return $@"{bytes / (double)K:F0}KB";
            if (bytes > 900)
                return $@"{bytes / (double)K:F1}KB";
            return bytes == 0 ? $@"{bytes}Byte" : $@"{bytes}Bytes";
        }

        public static void URL_Split(string text, ref List<string> outUrls)
        {
            while (true)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                var ssIndex = text.IndexOf(@"ss://", 1, StringComparison.OrdinalIgnoreCase);
                var ssrIndex = text.IndexOf(@"ssr://", 1, StringComparison.OrdinalIgnoreCase);
                var index = ssIndex;
                if (index == -1 || index > ssrIndex && ssrIndex != -1) index = ssrIndex;
                if (index == -1)
                {
                    outUrls.Insert(0, text);
                }
                else
                {
                    outUrls.Insert(0, text.Substring(0, index));
                    text = text.Substring(index);
                    continue;
                }

                break;
            }
        }

        public static IEnumerable<Server> Except(this IEnumerable<Server> x, IList<Server> y)
        {
            return from xi in x let found = y.Any(xi.IsMatchServer) where !found select xi;
        }

        public static IEnumerable<string> GetLines(this string str, bool removeEmptyLines = true)
        {
            using var sr = new StringReader(str);
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (removeEmptyLines && string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                yield return line;
            }
        }

        public static async void WriteAllTextAsync(string path, string str)
        {
#if IsDotNetCore
            await File.WriteAllTextAsync(path, str);
#else
            using var sw = new StreamWriter(path);
            await sw.WriteAsync(str);
#endif
        }
    }
}
