using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Shadowsocks.Util;

namespace Shadowsocks.Controller
{
    internal static class AutoStartup
    {
        private static readonly string Key = $@"ShadowsocksR_{Directory.GetCurrentDirectory().GetDeterministicHashCode()}";
        private const string RegistryRunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool Set(bool enabled)
        {
            RegistryKey runKey = null;
            try
            {
                var path = Utils.GetExecutablePath();
                runKey = Utils.OpenRegKey(RegistryRunPath, true);
                if (enabled)
                {
                    runKey.SetValue(Key, path);
                }
                else
                {
                    runKey.DeleteValue(Key);
                }
                runKey.Close();
                return true;
            }
            catch //(Exception e)
            {
                //Logging.LogUsefulException(e);
                return Utils.RunAsAdmin(Constants.ParameterSetautorun) == 0;
            }
            finally
            {
                if (runKey != null)
                {
                    try
                    {
                        runKey.Close();
                    }
                    catch (Exception e)
                    {
                        Logging.LogUsefulException(e);
                    }
                }
            }
        }

        public static bool Switch()
        {
            var enabled = !Check();
            RegistryKey runKey = null;
            try
            {
                var path = Utils.GetExecutablePath();
                runKey = Utils.OpenRegKey(RegistryRunPath, true);
                if (enabled)
                {
                    runKey.SetValue(Key, path);
                }
                else
                {
                    runKey.DeleteValue(Key);
                }
                runKey.Close();
                return true;
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                return false;
            }
            finally
            {
                if (runKey != null)
                {
                    try
                    {
                        runKey.Close();
                    }
                    catch (Exception e)
                    {
                        Logging.LogUsefulException(e);
                    }
                }
            }
        }

        public static bool Check()
        {
            RegistryKey runKey = null;
            try
            {
                runKey = Utils.OpenRegKey(RegistryRunPath, false);
                var runList = runKey.GetValueNames();
                runKey.Close();
                return runList.Any(item => item.Equals(Key));
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                return false;
            }
            finally
            {
                if (runKey != null)
                {
                    try
                    {
                        runKey.Close();
                    }
                    catch (Exception e)
                    {
                        Logging.LogUsefulException(e);
                    }
                }
            }
        }
    }
}
