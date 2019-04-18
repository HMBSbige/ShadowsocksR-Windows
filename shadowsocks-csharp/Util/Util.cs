﻿using Microsoft.Win32;
using OpenDNS;
using Shadowsocks.Controller;
using Shadowsocks.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace Shadowsocks.Util
{
    public class Utils
    {
        private delegate IPHostEntry GetHostEntryHandler(string ip);

        private static LRUCache<string, IPAddress> dnsBuffer = new LRUCache<string, IPAddress>();

        public static LRUCache<string, IPAddress> DnsBuffer => dnsBuffer;

        public static LRUCache<string, IPAddress> LocalDnsBuffer
        {
            get
            {
                return dnsBuffer;
            }
        }

        private static Process current_process => Process.GetCurrentProcess();

        public static void ReleaseMemory()
        {
#if !_CONSOLE
            // release any unused pages
            // making the numbers look good in task manager
            // this is totally nonsense in programming
            // but good for those users who care
            // making them happier with their everyday life
            // which is part of user experience
            GC.Collect(GC.MaxGeneration);
            GC.WaitForPendingFinalizers();

            if (UIntPtr.Size == 4)
            {
                SetProcessWorkingSetSize(current_process.Handle,
                                         (UIntPtr)0xFFFFFFFF,
                                         (UIntPtr)0xFFFFFFFF);
            }
            else if (UIntPtr.Size == 8)
            {
                SetProcessWorkingSetSize(current_process.Handle,
                                         (UIntPtr)0xFFFFFFFFFFFFFFFF,
                                         (UIntPtr)0xFFFFFFFFFFFFFFFF);
            }
#endif
        }

        public static string UnGzip(byte[] buf)
        {
            byte[] buffer = new byte[1024];
            int n;
            using (MemoryStream sb = new MemoryStream())
            {
                using (GZipStream input = new GZipStream(new MemoryStream(buf),
                    CompressionMode.Decompress, false))
                {
                    while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        sb.Write(buffer, 0, n);
                    }
                }
                return System.Text.Encoding.UTF8.GetString(sb.ToArray());
            }
        }

        public static void RandBytes(byte[] buf, int length)
        {
            byte[] temp = new byte[length];
            RNGCryptoServiceProvider rngServiceProvider = new RNGCryptoServiceProvider();
            rngServiceProvider.GetBytes(temp);
            temp.CopyTo(buf, 0);
        }

        public static uint RandUInt32()
        {
            byte[] temp = new byte[4];
            RNGCryptoServiceProvider rngServiceProvider = new RNGCryptoServiceProvider();
            rngServiceProvider.GetBytes(temp);
            return BitConverter.ToUInt32(temp, 0);
        }

        public static void Shuffle<T>(IList<T> list, Random rng)
        {
            int n = list.Count;
            while (n > 1)
            {
                int k = rng.Next(n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static bool BitCompare(byte[] target, int target_offset, byte[] m, int m_offset, int targetLength)
        {
            for (int i = 0; i < targetLength; ++i)
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
                for (int i = 0; i <= targetLength - m.Length; ++i)
                {
                    if (target[i] == m[0])
                    {
                        int j = 1;
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
            byte[] addr = ip.GetAddressBytes();
            byte[] net_addr = net.GetAddressBytes();
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
            string[] mask = netmask.Split('/');
            IPAddress netmask_ip = IPAddress.Parse(mask[0]);
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
            byte[] addr = ip.GetAddressBytes();
            if (addr.Length == 4)
            {
                string[] netmasks = new string[]
                {
                    "127.0.0.0/8",
                    "169.254.0.0/16",
                };
                foreach (string netmask in netmasks)
                {
                    if (isMatchSubNet(ip, netmask))
                        return true;
                }
                return false;
            }
            else if (addr.Length == 16)
            {
                string[] netmasks = new string[]
                {
                    "::1/128",
                };
                foreach (string netmask in netmasks)
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
            byte[] addr = ip.GetAddressBytes();
            if (addr.Length == 4)
            {
                if (ip.Equals(new IPAddress(0)))
                    return false;
                string[] netmasks = new string[]
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
                foreach (string netmask in netmasks)
                {
                    if (isMatchSubNet(ip, netmask))
                        return true;
                }
                return false;
            }
            else if (addr.Length == 16)
            {
                string[] netmasks = new string[]
                {
                    "::1/128",
                    "fc00::/7",
                    "fe80::/10",
                };
                foreach (string netmask in netmasks)
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
            string ret = "";
            for (int i = 0; i < str.Length; ++i)
            {
                if (str[i] == '%' && i < str.Length - 2)
                {
                    string s = str.Substring(i + 1, 2).ToLower();
                    int val = 0;
                    char c1 = s[0];
                    char c2 = s[1];
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
            IPAddress ret_ipAddress = null;
            ret_ipAddress = _QueryDns(host, dns_servers, IPv6_first);
            Logging.Info($"DNS query {host} answer {ret_ipAddress.ToString()}");
            return ret_ipAddress;
        }

        public static IPAddress _QueryDns(string host, string dns_servers, bool IPv6_first = false)
        {
            IPAddress ret_ipAddress = null;
            {
                if (!string.IsNullOrEmpty(dns_servers))
                {
                    OpenDNS.Types[] types;
                    if (IPv6_first)
                        types = new Types[] { Types.AAAA, Types.A };
                    else
                        types = new Types[] { Types.A, Types.AAAA };
                    string[] _dns_server = dns_servers.Split(',');
                    List<IPEndPoint> dns_server = new List<IPEndPoint>();
                    List<IPEndPoint> local_dns_server = new List<IPEndPoint>();
                    foreach (string server_str in _dns_server)
                    {
                        IPAddress ipAddress = null;
                        string server = server_str.Trim(' ');
                        int index = server.IndexOf(':');
                        string ip = null;
                        string port = null;
                        if (index >= 0)
                        {
                            if (server.StartsWith("["))
                            {
                                int ipv6_end = server.IndexOf(']', 1);
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
                        if (ip != null && IPAddress.TryParse(ip, out ipAddress))
                        {
                            int i_port = 53;
                            if (port != null)
                                int.TryParse(port, out i_port);
                            dns_server.Add(new IPEndPoint(ipAddress, i_port));
                            //dns_server.Add(port == null ? ip : ip + " " + port);
                        }
                    }
                    for (int query_i = 0; query_i < types.Length; ++query_i)
                    {
                        DnsQuery dns = new DnsQuery(host, types[query_i]);
                        dns.RecursionDesired = true;
                        foreach (IPEndPoint server in dns_server)
                        {
                            dns.Servers.Add(server);
                        }
                        if (dns.Send())
                        {
                            int count = dns.Response.Answers.Count;
                            if (count > 0)
                            {
                                for (int i = 0; i < count; ++i)
                                {
                                    if (((ResourceRecord)dns.Response.Answers[i]).Type != types[query_i])
                                        continue;
                                    return ((OpenDNS.Address)dns.Response.Answers[i]).IP;
                                }
                            }
                        }
                    }
                }
                {
                    try
                    {
                        GetHostEntryHandler callback = new GetHostEntryHandler(Dns.GetHostEntry);
                        IAsyncResult result = callback.BeginInvoke(host, null, null);
                        if (result.AsyncWaitHandle.WaitOne(10000, true))
                        {
                            IPHostEntry ipHostEntry = callback.EndInvoke(result);
                            foreach (IPAddress ad in ipHostEntry.AddressList)
                            {
                                if (ad.AddressFamily == AddressFamily.InterNetwork)
                                    return ad;
                            }
                            foreach (IPAddress ad in ipHostEntry.AddressList)
                            {
                                return ad;
                            }
                        }
                    }
                    catch
                    {

                    }
                }
            }
            return ret_ipAddress;
        }

        public static string GetExecutablePath()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().Location;
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
            Process process = null;
            ProcessStartInfo processInfo = new ProcessStartInfo();
            processInfo.Verb = "runas";
            processInfo.FileName = Application.ExecutablePath;
            processInfo.Arguments = Arguments;
            try
            {
                process = Process.Start(processInfo);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return -1;
            }
            if (process != null)
            {
                process.WaitForExit();
            }
            int ret = process.ExitCode;
            process.Close();
            return ret;
        }

        public static int GetDpiMul()
        {
            int dpi;
            using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                dpi = (int)graphics.DpiX;
            }
            return (dpi * 4 + 48) / 96;
        }

        private static string _tempPath = null;
        // return path to store temporary files
        public static string GetTempPath()
        {
            if (_tempPath == null)
            {
                try
                {
                    _tempPath = Directory.CreateDirectory(Path.Combine(Application.StartupPath, @"temp")).FullName;
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

#if !_CONSOLE
        public enum DeviceCap
        {
            DESKTOPVERTRES = 117,
            DESKTOPHORZRES = 118,
        }

        public static Point GetScreenPhysicalSize()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                IntPtr desktop = g.GetHdc();
                int PhysicalScreenWidth = GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPHORZRES);
                int PhysicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPVERTRES);

                return new Point(PhysicalScreenWidth, PhysicalScreenHeight);
            }
        }

        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);
#endif
    }
}
