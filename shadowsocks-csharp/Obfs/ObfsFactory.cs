﻿using System;
using System.Collections.Generic;
using System.Reflection;

namespace Shadowsocks.Obfs
{
    public static class ObfsFactory
    {
        private static Dictionary<string, Type> _registeredObfs;

        private static Type[] _constructorTypes = { typeof(string) };

        static ObfsFactory()
        {
            _registeredObfs = new Dictionary<string, Type>();
            foreach (string method in Plain.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(Plain));
            }
            foreach (string method in HttpSimpleObfs.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(HttpSimpleObfs));
            }
            foreach (string method in TlsTicketAuthObfs.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(TlsTicketAuthObfs));
            }
            foreach (string method in VerifyDeflateObfs.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(VerifyDeflateObfs));
            }
#if DEBUG
            foreach (string method in AuthSHA1.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(AuthSHA1));
            }
            foreach (string method in AuthSHA1V2.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(AuthSHA1V2));
            }
#endif
            foreach (string method in AuthSHA1V4.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(AuthSHA1V4));
            }
            foreach (string method in AuthAES128SHA1.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(AuthAES128SHA1));
            }
            foreach (string method in AuthChain_a.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(AuthChain_a));
            }
            foreach (string method in AuthChain_b.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(AuthChain_b));
            }
            foreach (string method in AuthChain_c.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(AuthChain_c));
            }
            foreach (string method in AuthChain_d.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(AuthChain_d));
            }
            foreach (string method in AuthChain_e.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(AuthChain_e));
            }
            foreach (string method in AuthChain_f.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(AuthChain_f));
            }
            foreach (string method in AuthAkarin.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(AuthAkarin));
            }
            foreach (string method in AuthAkarin_spec_a.SupportedObfs())
            {
                _registeredObfs.Add(method, typeof(AuthAkarin_spec_a));
            }
        }

        public static IObfs GetObfs(string method)
        {
            if (string.IsNullOrEmpty(method))
            {
                method = "plain";
            }
            method = method.ToLowerInvariant();
            Type t = _registeredObfs[method];
            ConstructorInfo c = t.GetConstructor(_constructorTypes);
            IObfs result = (IObfs)c.Invoke(new object[] { method });
            return result;
        }
    }
}
