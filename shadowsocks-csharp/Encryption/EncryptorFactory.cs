using Shadowsocks.Encryption.Stream;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Shadowsocks.Encryption
{
    public static class EncryptorFactory
    {
        public static readonly Dictionary<string, Type> RegisteredEncryptors = new Dictionary<string, Type>();

        private static readonly Type[] ConstructorTypes = { typeof(string), typeof(string) };

        static EncryptorFactory()
        {
            foreach (var method in NoneEncryptor.SupportedCiphers().Where(method => !RegisteredEncryptors.ContainsKey(method)))
            {
                RegisteredEncryptors.Add(method, typeof(NoneEncryptor));
            }

            foreach (var method in StreamOpenSSLEncryptor.SupportedCiphers().Where(method => !RegisteredEncryptors.ContainsKey(method)))
            {
                RegisteredEncryptors.Add(method, typeof(StreamOpenSSLEncryptor));
            }

            foreach (var method in StreamSodiumEncryptor.SupportedCiphers().Where(method => !RegisteredEncryptors.ContainsKey(method)))
            {
                RegisteredEncryptors.Add(method, typeof(StreamSodiumEncryptor));
            }

            foreach (var method in StreamMbedTLSEncryptor.SupportedCiphers().Where(method => !RegisteredEncryptors.ContainsKey(method)))
            {
                RegisteredEncryptors.Add(method, typeof(StreamMbedTLSEncryptor));
            }

            var allEncryptor = new StringBuilder(Environment.NewLine);
            allEncryptor.AppendLine(@"============================");
            allEncryptor.AppendLine(@"Registered Encryptor Info");
            foreach (var encryptor in RegisteredEncryptors)
            {
                allEncryptor.AppendLine($@"{encryptor.Key}=>{encryptor.Value.Name}");
            }
            allEncryptor.AppendLine(@"============================");
            Console.WriteLine(allEncryptor);
        }

        public static IEncryptor GetEncryptor(string method, string password)
        {
            if (string.IsNullOrEmpty(method))
            {
                method = @"aes-256-cfb";
            }
            method = method.ToLowerInvariant();
            var t = RegisteredEncryptors[method];
            var c = t.GetConstructor(ConstructorTypes);
            if (c == null)
            {
                throw new System.Exception("Invalid ctor");
            }
            var result = (IEncryptor)c.Invoke(new object[] { method, password });
            return result;
        }

        public static EncryptorInfo GetEncryptorInfo(string method)
        {
            if (string.IsNullOrEmpty(method))
            {
                method = @"aes-256-cfb";
            }
            method = method.ToLowerInvariant();
            var t = RegisteredEncryptors[method];
            var c = t.GetConstructor(ConstructorTypes);
            if (c == null)
            {
                throw new System.Exception("Invalid ctor");
            }
            var result = (IEncryptor)c.Invoke(new object[] { method, "0" });
            var info = result.getInfo();
            result.Dispose();
            return info;
        }
    }
}
