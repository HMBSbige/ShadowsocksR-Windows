using Shadowsocks.Controller;
using Shadowsocks.Properties;
using Shadowsocks.Util;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Shadowsocks.Encryption
{
    public static class OpenSSL
    {
        private const string DLLNAME = @"libsscrypto.dll";

        public const int OPENSSL_ENCRYPT = 1;
        public const int OPENSSL_DECRYPT = 0;

        static OpenSSL()
        {
            var dllPath = Utils.GetTempPath(DLLNAME);
            try
            {
                FileManager.DecompressFile(dllPath, Environment.Is64BitProcess ? Resources.libsscrypto64_dll : Resources.libsscrypto_dll);
                LoadLibrary(dllPath);
            }
            catch (IOException)
            {
            }
            catch (System.Exception e)
            {
                Logging.LogUsefulException(e);
            }
        }

        public static IntPtr GetCipherInfo(string cipherName)
        {
            var name = Encoding.ASCII.GetBytes(cipherName);
            Array.Resize(ref name, name.Length + 1);
            return EVP_get_cipherbyname(name);
        }

        [DllImport(@"Kernel32.dll")]
        private static extern IntPtr LoadLibrary(string path);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(DLLNAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr EVP_get_cipherbyname(byte[] name);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(DLLNAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr EVP_CIPHER_CTX_new();

        [SuppressUnmanagedCodeSecurity]
        [DllImport(DLLNAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void EVP_CIPHER_CTX_free(IntPtr ctx);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(DLLNAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int EVP_CipherInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, byte[] key, byte[] iv, int enc);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(DLLNAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int EVP_CipherUpdate(IntPtr ctx, byte[] outb, out int outl, byte[] inb, int inl);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(DLLNAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int EVP_CIPHER_CTX_set_padding(IntPtr x, int padding);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(DLLNAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int EVP_CIPHER_CTX_set_key_length(IntPtr x, int keylen);
    }
}
