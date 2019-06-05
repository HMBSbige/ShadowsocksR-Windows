using DnsClient;
using DnsClient.Protocol;
using Microsoft.Win32;
using Shadowsocks.Controller;
using Shadowsocks.Encryption;
using Shadowsocks.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Point = System.Drawing.Point;

namespace Shadowsocks.Util
{
    public static class Utils
    {
        public static LRUCache<string, IPAddress> DnsBuffer { get; } = new LRUCache<string, IPAddress>();

        public static LRUCache<string, IPAddress> LocalDnsBuffer => DnsBuffer;

        private static Process current_process => Process.GetCurrentProcess();

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
                    SetProcessWorkingSetSize(current_process.Handle, (UIntPtr)0xFFFFFFFF, (UIntPtr)0xFFFFFFFF);
                }
                else
                {
                    SetProcessWorkingSetSize(current_process.Handle, (UIntPtr)0xFFFFFFFFFFFFFFFF, (UIntPtr)0xFFFFFFFFFFFFFFFF);
                }
            }
        }

        public static string UnGzip(byte[] buf)
        {
            var buffer = new byte[1024];
            using var sb = new MemoryStream();
            using (var input = new GZipStream(new MemoryStream(buf), CompressionMode.Decompress, false))
            {
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    sb.Write(buffer, 0, n);
                }
            }
            return Encoding.UTF8.GetString(sb.ToArray());
        }

        public static void RandBytes(byte[] buf, int length)
        {
            var temp = new byte[length];
            var rngServiceProvider = new RNGCryptoServiceProvider();
            rngServiceProvider.GetBytes(temp);
            temp.CopyTo(buf, 0);
        }

        public static uint RandUInt32()
        {
            var temp = new byte[4];
            var rngServiceProvider = new RNGCryptoServiceProvider();
            rngServiceProvider.GetBytes(temp);
            return BitConverter.ToUInt32(temp, 0);
        }

        public static void Shuffle<T>(IList<T> list, Random rng)
        {
            var n = list.Count;
            while (n > 1)
            {
                var k = rng.Next(n);
                n--;
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

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

        public static bool isMatchSubNet(IPAddress ip, IPAddress net, int netmask)
        {
            var addr = ip.GetAddressBytes();
            var net_addr = net.GetAddressBytes();
            int i = 8, index = 0;
            for (; i < netmask; i += 8, index += 1)
            {
                if (addr[index] != net_addr[index])
                    return false;
            }
            if ((addr[index] >> (i - netmask)) != (net_addr[index] >> (i - netmask)))
                return false;
            return true;
        }

        public static bool isMatchSubNet(IPAddress ip, string netmask)
        {
            var mask = netmask.Split('/');
            var netmask_ip = IPAddress.Parse(mask[0]);
            if (ip.AddressFamily == netmask_ip.AddressFamily)
            {
                try
                {
                    return isMatchSubNet(ip, netmask_ip, Convert.ToInt16(mask[1]));
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public static bool isLocal(IPAddress ip)
        {
            var addr = ip.GetAddressBytes();
            if (addr.Length == 4)
            {
                var netmasks = new[]
                {
                    "127.0.0.0/8",
                    "169.254.0.0/16",
                };
                foreach (var netmask in netmasks)
                {
                    if (isMatchSubNet(ip, netmask))
                        return true;
                }
                return false;
            }
            else if (addr.Length == 16)
            {
                var netmasks = new[]
                {
                    "::1/128",
                };
                foreach (var netmask in netmasks)
                {
                    if (isMatchSubNet(ip, netmask))
                        return true;
                }
                return false;
            }
            return true;
        }

        public static bool isLocal(Socket socket)
        {
            return isLocal(((IPEndPoint)socket.RemoteEndPoint).Address);
        }

        public static bool isLAN(IPAddress ip)
        {
            var addr = ip.GetAddressBytes();
            if (addr.Length == 4)
            {
                if (ip.Equals(new IPAddress(0)))
                    return false;
                var netmasks = new[]
                {
                    "0.0.0.0/8",
                    "10.0.0.0/8",
                    //"100.64.0.0/10", //部分地区运营商貌似在使用这个，这个可能不安全
                    "127.0.0.0/8",
                    "169.254.0.0/16",
                    "172.16.0.0/12",
                    //"192.0.0.0/24",
                    //"192.0.2.0/24",
                    "192.168.0.0/16",
                    //"198.18.0.0/15",
                    //"198.51.100.0/24",
                    //"203.0.113.0/24",
                };
                foreach (var netmask in netmasks)
                {
                    if (isMatchSubNet(ip, netmask))
                        return true;
                }
                return false;
            }
            else if (addr.Length == 16)
            {
                var netmasks = new[]
                {
                    "::1/128",
                    "fc00::/7",
                    "fe80::/10",
                };
                foreach (var netmask in netmasks)
                {
                    if (isMatchSubNet(ip, netmask))
                        return true;
                }
                return false;
            }
            return true;
        }

        public static bool isLAN(Socket socket)
        {
            return isLAN(((IPEndPoint)socket.RemoteEndPoint).Address);
        }

        public static string GetTimestamp(DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssffff");
        }

        public static string urlDecode(string str)
        {
            var ret = "";
            for (var i = 0; i < str.Length; ++i)
            {
                if (str[i] == '%' && i < str.Length - 2)
                {
                    var s = str.Substring(i + 1, 2).ToLower();
                    var val = 0;
                    var c1 = s[0];
                    var c2 = s[1];
                    val += (c1 < 'a') ? c1 - '0' : 10 + (c1 - 'a');
                    val *= 16;
                    val += (c2 < 'a') ? c2 - '0' : 10 + (c2 - 'a');

                    ret += (char)val;
                    i += 2;
                }
                else if (str[i] == '+')
                {
                    ret += ' ';
                }
                else
                {
                    ret += str[i];
                }
            }
            return ret;
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

        public static IPAddress QueryDns(string host, string dns_servers, bool IPv6_first = false)
        {
            var ret_ipAddress = Query(host, dns_servers, IPv6_first);
            if (ret_ipAddress == null)
            {
                Logging.Info($@"DNS query {host} failed.");
            }
            else
            {
                Logging.Info($@"DNS query {host} answer {ret_ipAddress}");
            }
            return ret_ipAddress;
        }

        private static IPAddress Query(string host, string dnsServers, bool IPv6_first = false)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(dnsServers))
                {
                    var client = new LookupClient(ToIpEndPoints(dnsServers))
                    {
                        UseCache = false
                    };
                    if (IPv6_first)
                    {
                        var r = client.Query(host, QueryType.AAAA).Answers.OfType<AaaaRecord>().FirstOrDefault()
                                ?.Address;
                        if (r != null)
                        {
                            return r;
                        }

                        r = client.Query(host, QueryType.A).Answers.OfType<ARecord>().FirstOrDefault()?.Address;
                        if (r != null)
                        {
                            return r;
                        }
                    }
                    else
                    {
                        var r = client.Query(host, QueryType.A).Answers.OfType<ARecord>().FirstOrDefault()?.Address;
                        if (r != null)
                        {
                            return r;
                        }

                        r = client.Query(host, QueryType.AAAA).Answers.OfType<AaaaRecord>().FirstOrDefault()?.Address;
                        if (r != null)
                        {
                            return r;
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }

            try
            {

                var ips = Dns.GetHostAddresses(host);
                var type = IPv6_first ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;

                foreach (var ad in ips)
                {
                    if (ad.AddressFamily == type)
                    {
                        return ad;
                    }
                }

                foreach (var ad in ips)
                {
                    return ad;
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }

        private static IPEndPoint[] ToIpEndPoints(string dnsServers, ushort defaultPort = 53)
        {
            var dnsServerStr = dnsServers.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
            var dnsServer = new List<IPEndPoint>();
            foreach (var serverStr in dnsServerStr)
            {
                var server = serverStr.Trim();
                var index = server.IndexOf(':');
                string ip = null;
                string port = null;
                if (index >= 0)
                {
                    if (server.StartsWith("["))
                    {
                        var ipv6_end = server.IndexOf(']', 1);
                        if (ipv6_end >= 0)
                        {
                            ip = server.Substring(1, ipv6_end - 1);

                            index = server.IndexOf(':', ipv6_end);
                            if (index == ipv6_end + 1)
                            {
                                port = server.Substring(index + 1);
                            }
                        }
                    }
                    else
                    {
                        ip = server.Substring(0, index);
                        port = server.Substring(index + 1);
                    }
                }
                else
                {
                    index = server.IndexOf(' ');
                    if (index >= 0)
                    {
                        ip = server.Substring(0, index);
                        port = server.Substring(index + 1);
                    }
                    else
                    {
                        ip = server;
                    }
                }

                if (ip != null && IPAddress.TryParse(ip, out var ipAddress))
                {
                    var iPort = defaultPort;
                    if (port != null)
                    {
                        ushort.TryParse(port, out iPort);
                    }

                    dnsServer.Add(new IPEndPoint(ipAddress, iPort));
                }
            }

            return dnsServer.ToArray();
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
            return Path.Combine(Path.GetDirectoryName(dllPath), $@"{Path.GetFileNameWithoutExtension(dllPath)}.exe");
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

        public static int RunAsAdmin(string Arguments)
        {
            Process process;
            var processInfo = new ProcessStartInfo
            {
                Verb = "runas",
                FileName = GetExecutablePath(),
                Arguments = Arguments
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

        public static int GetDpiMul()
        {
            int dpi;
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                dpi = (int)graphics.DpiX;
            }
            return (dpi * 4 + 48) / 96;
        }

        private static string _tempPath;
        // return path to store temporary files
        public static string GetTempPath()
        {
            if (_tempPath == null)
            {
                try
                {
                    _tempPath = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), @"temp")).FullName;
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
                var original = FileManager.NonExclusiveReadAllText(PACServer.PAC_FILE, Encoding.UTF8);
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

        public static void RegistrySetValue(RegistryKey registry, string name, object value)
        {
            try
            {
                registry.SetValue(name, value);
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
            }
        }

        public static RegistryKey OpenUserRegKey(string name, bool writable)
        {
            var userKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentUser, string.Empty,
                    Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32
            ).OpenSubKey(name, writable);
            return userKey;
        }

        public static void BringToFront(this FrameworkElement element)
        {
            if (element?.Parent is Panel parent)
            {
                var maxZ = parent.Children.OfType<UIElement>()
                        .Where(x => x != element)
                        .Select(Panel.GetZIndex)
                        .Max();
                Panel.SetZIndex(element, maxZ + 1);
            }
        }

        public enum DeviceCap
        {
            DESKTOPVERTRES = 117,
            DESKTOPHORZRES = 118,
        }

        public static Point GetScreenPhysicalSize()
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            var desktop = g.GetHdc();
            var PhysicalScreenWidth = GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPHORZRES);
            var PhysicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPVERTRES);

            return new Point(PhysicalScreenWidth, PhysicalScreenHeight);
        }

        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);
    }
}
