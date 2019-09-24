using Shadowsocks.Obfs;
using Shadowsocks.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;

namespace Shadowsocks.Controller
{
    public enum LogLevel
    {
        Debug = 0,
        Info,
        Warn,
        Error,
        Assert
    }

    public class Logging
    {
        public static string LogFile;
        public static string LogFileName;
        protected static string date;

        private static FileStream _logFileStream;
        private static StreamWriterWithTimestamp _logStreamWriter;
        private static readonly object _lock = new object();
        public static bool save_to_file = true;
        public static TextWriter DefaultOut;
        public static TextWriter DefaultError;

        public static bool OpenLogFile()
        {
            try
            {
                CloseLogFile();

                if (save_to_file)
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
                    date = newDate;
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
            System.Diagnostics.Debug.WriteLine($@"[{DateTime.Now}] INFO  {o}");
        }

        [Conditional("DEBUG")]
        public static void Debug(object o)
        {
            Log(LogLevel.Debug, o);
            System.Diagnostics.Debug.WriteLine($@"[{DateTime.Now}] DEBUG {o}");
        }

        private static string ToString(StackFrame[] stacks)
        {
            return stacks.Aggregate(string.Empty, (current, stack) => current + $@"{stack.GetMethod()}{Environment.NewLine}");
        }

        private static void CompressOldLogFile()
        {
            var list = Directory.GetFiles(Utils.GetTempPath(), @"shadowsocks_*.log", SearchOption.TopDirectoryOnly);
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

        protected static void UpdateLogFile()
        {
            if (DateTime.Now.ToString("yyyy-MM") != date)
            {
                lock (_lock)
                {
                    if (DateTime.Now.ToString("yyyy-MM") != date)
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
                if (se.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    // closed by browser when sending
                    // normally happens when download is canceled or a tab is closed before page is loaded
                }
                else if (se.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // received rst
                }
                else if (se.SocketErrorCode == SocketError.NotConnected)
                {
                    // close when not connected
                }
                else if ((uint)se.SocketErrorCode == 0x80004005)
                {
                    // already closed
                }
                else if (se.SocketErrorCode == SocketError.Shutdown)
                {
                    // ignore
                }
                else if (se.SocketErrorCode == SocketError.Interrupted)
                {
                    // ignore
                }
                else
                {
                    Error(e);

                    Debug(ToString(new StackTrace().GetFrames()));
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
            // just log useful exceptions, not all of them
            if (e is ObfsException oe)
            {
                Error("Proxy server [" + remarks + "(" + server + ")] "
                    + oe.Message);
                return true;
            }

            if (e is NullReferenceException)
            {
                return true;
            }

            if (e is ObjectDisposedException)
            {
                // ignore
                return true;
            }

            if (e is SocketException se)
            {
                if ((uint)se.SocketErrorCode == 0x80004005)
                {
                    // already closed
                    return true;
                }

                if (se.ErrorCode == 11004)
                {
                    Log(LogLevel.Warn, "Proxy server [" + remarks + "(" + server + ")] "
                                       + "DNS lookup failed");
                    return true;
                }

                if (se.SocketErrorCode == SocketError.HostNotFound)
                {
                    Log(LogLevel.Warn, "Proxy server [" + remarks + "(" + server + ")] "
                                       + "Host not found");
                    return true;
                }

                if (se.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    Log(LogLevel.Warn, "Proxy server [" + remarks + "(" + server + ")] "
                                       + "connection refused");
                    return true;
                }

                if (se.SocketErrorCode == SocketError.NetworkUnreachable)
                {
                    Log(LogLevel.Warn, "Proxy server [" + remarks + "(" + server + ")] "
                                       + "network unreachable");
                    return true;
                }

                if (se.SocketErrorCode == SocketError.TimedOut)
                {
                    //Logging.Log(LogLevel.Warn, "Proxy server [" + remarks + "(" + server + ")] "
                    //    + "connection timeout");
                    return true;
                }

                if (se.SocketErrorCode == SocketError.Shutdown)
                {
                    return true;
                }

                Log(LogLevel.Info, "Proxy server [" + remarks + "(" + server + ")] "
                                   + Convert.ToString(se.SocketErrorCode) + ":" + se.Message);

                Debug(ToString(new StackTrace().GetFrames()));

                return true;
            }
            return false;
        }

        public static void Log(LogLevel level, object s)
        {
            UpdateLogFile();
            var strMap = new[]{
                "Debug",
                "Info",
                "Warn",
                "Error",
                "Assert"
            };
            Console.WriteLine($@"[{strMap[(int)level]}] {s}");
        }

        [Conditional("DEBUG")]
        public static void LogBin(LogLevel level, string info, byte[] data, int length)
        {
            //string s = "";
            //for (int i = 0; i < length; ++i)
            //{
            //    string fs = "0" + Convert.ToString(data[i], 16);
            //    s += " " + fs.Substring(fs.Length - 2, 2);
            //}
            //Log(level, info + s);
        }

    }

    // Simply extended System.IO.StreamWriter for adding timestamp workaround
    public class StreamWriterWithTimestamp : StreamWriter
    {
        public StreamWriterWithTimestamp(Stream stream) : base(stream)
        {
        }

        private string GetTimestamp()
        {
            return "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] ";
        }

        public override void WriteLine(string value)
        {
            base.WriteLine(GetTimestamp() + value);
        }

        public override void Write(string value)
        {
            base.Write(GetTimestamp() + value);
        }
    }

}
