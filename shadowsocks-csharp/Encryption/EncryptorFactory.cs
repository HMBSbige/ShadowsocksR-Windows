using Shadowsocks.Encryption.Stream;
using System;
using System.Collections.Generic;

namespace Shadowsocks.Encryption
{
    public static class EncryptorFactory
    {
        private static readonly Dictionary<string, Type> _registeredEncryptors = new Dictionary<string, Type>();

        private static Type[] _constructorTypes = { typeof(string), typeof(string) };

        static EncryptorFactory()
        {
            foreach (var method in NoneEncryptor.SupportedCiphers())
            {
                if (!_registeredEncryptors.ContainsKey(method))
                {
                    _registeredEncryptors.Add(method, typeof(NoneEncryptor));
                }
            }

            foreach (var method in StreamOpenSSLEncryptor.SupportedCiphers())
            {
                if (!_registeredEncryptors.ContainsKey(method))
                {
                    _registeredEncryptors.Add(method, typeof(StreamOpenSSLEncryptor));
                }
            }


            foreach (var method in StreamSodiumEncryptor.SupportedCiphers())
            {
                if (!_registeredEncryptors.ContainsKey(method))
                {
                    _registeredEncryptors.Add(method, typeof(StreamSodiumEncryptor));
                }
            }

            foreach (var method in StreamMbedTLSEncryptor.SupportedCiphers())
            {
                if (!_registeredEncryptors.ContainsKey(method))
                {
                    _registeredEncryptors.Add(method, typeof(StreamMbedTLSEncryptor));
                }
            }
        }

        public static Dictionary<string, Type> GetEncryptor()
        {
            return _registeredEncryptors;
        }

        public static IEncryptor GetEncryptor(string method, string password)
        {
            if (string.IsNullOrEmpty(method))
            {
                method = "aes-256-cfb";
            }
            method = method.ToLowerInvariant();
            var t = _registeredEncryptors[method];
            var c = t.GetConstructor(_constructorTypes);
            if (c == null)
            {
                throw new Exception("Invalid ctor");
            }
            var result = (IEncryptor)c.Invoke(new object[] { method, password });
            return result;
        }

        public static EncryptorInfo GetEncryptorInfo(string method)
        {
            if (string.IsNullOrEmpty(method))
            {
                method = "aes-256-cfb";
            }
            method = method.ToLowerInvariant();
            var t = _registeredEncryptors[method];
            var c = t.GetConstructor(_constructorTypes);
            if (c == null)
            {
                throw new Exception("Invalid ctor");
            }
            var result = (IEncryptor)c.Invoke(new object[] { method, "0"});
            var info = result.getInfo();
            result.Dispose();
            return info;
        }
    }
}
