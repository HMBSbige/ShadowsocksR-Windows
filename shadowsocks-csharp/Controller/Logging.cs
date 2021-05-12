using Shadowsocks.Enums;
using Shadowsocks.Obfs;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Shadowsocks.Controller
{
    public static class Logging
    {
        public static string LogFile;
        public static string LogFileName;
        private static string _date;

        private static FileStream _logFileStream;
        private static StreamWriterWithTimestamp _logStreamWriter;
        private static readonly object Lock = new();
        public static bool SaveToFile = true;
        public static TextWriter DefaultOut;
        public static TextWriter DefaultError;

        public static bool OpenLogFile()
        {
            try
            {
                CloseLogFile();

                if (SaveToFile)
                {
                    var newDate = DateTime.Now.ToString("yyyy-MM");
                    LogFileName = $@"shadowsocks_{newDate}.log";
                    LogFile = Utils.GetTempPath(LogFileName);
                    _logFileStream = new FileStream(LogFile, FileMode.Append);
                    _logStreamWriter = new StreamWriterWithTimestamp(_logFileStream)
                    {
                        AutoFlush = true
                    };
                    Console.SetOut(_logStreamWriter);
                    Console.SetError(_logStreamWriter);
                    _date = newDate;
                    CompressOldLogFile();
                }
                else
                {
                    Console.SetOut(DefaultOut);
                    Console.SetError(DefaultError);
                }

                return true;
            }
            catch (IOException e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        private static void CloseLogFile()
        {
            _logStreamWriter?.Close();
            _logStreamWriter?.Dispose();
            _logFileStream?.Close();
            _logFileStream?.Dispose();

            _logStreamWriter = null;
            _logFileStream = null;
        }

        public static void Clear()
        {
            CloseLogFile();
            if (LogFile != null)
            {
                File.Delete(LogFile);
            }
            OpenLogFile();
        }

        public static void Error(object o)
        {
            Log(LogLevel.Error, o);
            System.Diagnostics.Debug.WriteLine($@"[{DateTime.Now}] ERROR {o}");
        }

        public static void Info(object o)
        {
            Log(LogLevel.Info, o);
            System.Diagnostics.Debug.WriteLine($@"[{DateTime.Now}] INFO {o}");
        }

        [Conditional("DEBUG")]
        public static void Debug(object o)
        {
            Log(LogLevel.Debug, o);
            System.Diagnostics.Debug.WriteLine($@"[{DateTime.Now}] DEBUG {o}");
        }

        private static string ToString(IEnumerable<StackFrame> stacks)
        {
            return stacks.Aggregate(string.Empty, (current, stack) => current + $@"{stack.GetMethod()}{Environment.NewLine}");
        }

        private static void CompressOldLogFile()
        {
            var list = Directory.GetFiles(Utils.TempPath, @"shadowsocks_*.log", SearchOption.TopDirectoryOnly);
            foreach (var file in list)
            {
                if (file != LogFile)
                {
                    FileManager.ZipCompressToFile(file).ContinueWith(task =>
                    {
                        if (task.Result)
                        {
                            File.Delete(file);
                        }
                    });
                }
            }
        }

        private static void UpdateLogFile()
        {
            if (DateTime.Now.ToString("yyyy-MM") != _date)
            {
                lock (Lock)
                {
                    if (DateTime.Now.ToString("yyyy-MM") != _date)
                    {
                        OpenLogFile();
                    }
                }
            }
        }

        public static void LogUsefulException(Exception e)
        {
            UpdateLogFile();
            // just log useful exceptions, not all of them
            if (e is SocketException se)
            {
                switch (se.SocketErrorCode)
                {
                    case SocketError.ConnectionAborted:
                        // closed by browser when sending
                        // normally happens when download is canceled or a tab is closed before page is loaded
                        break;
                    case SocketError.ConnectionReset:
                        // received rst
                        break;
                    case SocketError.NotConnected:
                        // close when not connected
                        break;
                    case SocketError.Shutdown:
                        // ignore
                        break;
                    case SocketError.Interrupted:
                        // ignore
                        break;
                    default:
                    {
                        if ((uint)se.SocketErrorCode == 0x80004005)
                        {
                            // already closed
                        }
                        else
                        {
                            Error(e);

                            Debug(ToString(new StackTrace().GetFrames()));
                        }
                        break;
                    }
                }
            }
            else
            {
                Error(e);

                Debug(ToString(new StackTrace().GetFrames()));
            }
        }

        public static bool LogSocketException(string remarks, string server, Exception e)
        {
            UpdateLogFile();
            switch (e)
            {
                // just log useful exceptions, not all of them
                case ObfsException oe:
                    Error($@"Proxy server [{remarks}({server})] {oe.Message}");
                    return true;
                case NullReferenceException _:
                case ObjectDisposedException _:
                    return true;
                case SocketException se when se.ErrorCode == 11004:
                    Log(LogLevel.Warn, $@"Proxy server [{remarks}({server})] DNS lookup failed");
                    return true;
                case SocketException se when (uint)se.SocketErrorCode == 0x80004005:
                    // already closed
                    return true;
                case SocketException se:
                    switch (se.SocketErrorCode)
                    {
                        case SocketError.HostNotFound:
                            Log(LogLevel.Warn, $@"Proxy server [{remarks}({server})] Host not found");
                            return true;
                        case SocketError.ConnectionRefused:
                            Log(LogLevel.Warn, $@"Proxy server [{remarks}({server})] connection refused");
                            return true;
                        case SocketError.NetworkUnreachable:
                            Log(LogLevel.Warn, $@"Proxy server [{remarks}({server})] network unreachable");
                            return true;
                        case SocketError.TimedOut:
                        case SocketError.Shutdown:
                            return true;
                    }

                    Log(LogLevel.Info, $@"Proxy server [{remarks}({server})] {Convert.ToString(se.SocketErrorCode)}:{se.Message}");

                    Debug(ToString(new StackTrace().GetFrames()));
                    return true;
                default:
                    return false;
            }
        }

        public static void Log(LogLevel level, object s)
        {
            UpdateLogFile();
            Console.WriteLine($@"[{level}] {s}");
        }

        [Conditional("DEBUG")]
        public static void LogBin(LogLevel level, string info, byte[] data, int length)
        {
            var s = new StringBuilder();
            for (var i = 0; i < length; ++i)
            {
                var fs = $@"0{Convert.ToString(data[i], 16)}";
                s.Append($@" {fs.Substring(fs.Length - 2, 2)}");
            }
            Log(level, $@"{info}{s}");
        }

    }

    // Simply extended System.IO.StreamWriter for adding timestamp workaround
    public class StreamWriterWithTimestamp : StreamWriter
    {
        public StreamWriterWithTimestamp(Stream stream) : base(stream)
        {
        }

        private static string GetTimestamp()
        {
            return $@"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ";
        }

        public override void WriteLine(string value)
        {
            try
            {
                base.WriteLine(GetTimestamp() + value);
            }
            catch (ObjectDisposedException)
            {

            }
        }

        public override void Write(string value)
        {
            try
            {
                base.Write(GetTimestamp() + value);
            }
            catch (ObjectDisposedException)
            {

            }
        }
    }

}
