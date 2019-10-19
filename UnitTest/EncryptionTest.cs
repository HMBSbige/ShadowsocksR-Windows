using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shadowsocks.Encryption;
using Shadowsocks.Encryption.Stream;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnitTest
{
    [TestClass]
    public class EncryptionTest
    {
        private void RunEncryptionRound(IEncryptor encryptor, IEncryptor decryptor)
        {
            var plain = new byte[16384];
            var cipher = new byte[plain.Length + 16];
            var plain2 = new byte[plain.Length + 16];
            Rng.RandBytes(plain);
            encryptor.Encrypt(plain, plain.Length, cipher, out var outLen);
            decryptor.Decrypt(cipher, outLen, plain2, out var outLen2);
            Assert.AreEqual(plain.Length, outLen2);
            for (var j = 0; j < plain.Length; j++)
            {
                Assert.AreEqual(plain[j], plain2[j]);
            }
            encryptor.Encrypt(plain, 1000, cipher, out outLen);
            decryptor.Decrypt(cipher, outLen, plain2, out outLen2);
            Assert.AreEqual(1000, outLen2);
            for (var j = 0; j < outLen2; j++)
            {
                Assert.AreEqual(plain[j], plain2[j]);
            }
            encryptor.Encrypt(plain, 12333, cipher, out outLen);
            decryptor.Decrypt(cipher, outLen, plain2, out outLen2);
            Assert.AreEqual(12333, outLen2);
            for (var j = 0; j < outLen2; j++)
            {
                Assert.AreEqual(plain[j], plain2[j]);
            }
        }

        [TestMethod]
        public void TestStreamOpenSSLEncryption()
        {
            var failed = false;
            // run it once before the multi-threading test to initialize global tables
            RunSingleStreamOpenSSLEncryptionThread();

            var tasks = new List<Task>();
            foreach (var cipher in StreamOpenSSLEncryptor.SupportedCiphers())
            {
                if (cipher.EndsWith(@"-cbc"))
                {
                    continue;
                }
                var t = new Task(() =>
                {
                    try
                    {
                        RunSingleStreamOpenSSLEncryptionThread(cipher);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($@"{cipher}:{e.Message}");
                        failed = true;
                        throw;
                    }
                });
                tasks.Add(t);
                t.Start();
            }

            Task.WaitAll(tasks.ToArray());
            Assert.IsFalse(failed);
        }

        private void RunSingleStreamOpenSSLEncryptionThread(string methodName = @"aes-256-cfb8", string password = @"barfoo!")
        {
            for (var i = 0; i < 100; i++)
            {
                IEncryptor encryptor = new StreamOpenSSLEncryptor(methodName, password);
                IEncryptor decryptor = new StreamOpenSSLEncryptor(methodName, password);
                RunEncryptionRound(encryptor, decryptor);
            }
        }

        [TestMethod]
        public void TestStreamMbedTLSEncryption()
        {
            var failed = false;
            // run it once before the multi-threading test to initialize global tables
            RunSingleStreamMbedTLSEncryptionThread();
            var tasks = new List<Task>();
            foreach (var cipher in StreamMbedTLSEncryptor.SupportedCiphers())
            {
                if (cipher.EndsWith(@"-cbc"))
                {
                    continue;
                }
                var t = new Task(() =>
                {
                    try
                    {
                        RunSingleStreamMbedTLSEncryptionThread(cipher);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($@"{cipher}:{e.Message}");
                        failed = true;
                        throw;
                    }
                });
                tasks.Add(t);
                t.Start();
            }

            Task.WaitAll(tasks.ToArray());
            Assert.IsFalse(failed);
        }

        private void RunSingleStreamMbedTLSEncryptionThread(string methodName = @"rc4-md5-6", string password = @"barfoo!")
        {
            for (var i = 0; i < 100; i++)
            {
                IEncryptor encryptor = new StreamMbedTLSEncryptor(methodName, password);
                IEncryptor decryptor = new StreamMbedTLSEncryptor(methodName, password);
                RunEncryptionRound(encryptor, decryptor);
            }
        }

        [TestMethod]
        public void TestStreamSodiumEncryption()
        {
            var failed = false;
            // run it once before the multi-threading test to initialize global tables
            RunSingleStreamSodiumEncryptionThread();
            var tasks = new List<Task>();
            foreach (var cipher in StreamSodiumEncryptor.SupportedCiphers())
            {
                if (cipher.StartsWith(@"x"))
                {
                    continue;
                }
                var t = new Task(() =>
                {
                    try
                    {
                        RunSingleStreamSodiumEncryptionThread(cipher);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($@"{cipher}:{e.Message}");
                        failed = true;
                        throw;
                    }
                });
                tasks.Add(t);
                t.Start();
            }

            Task.WaitAll(tasks.ToArray());
            Assert.IsFalse(failed);
        }

        private void RunSingleStreamSodiumEncryptionThread(string methodName = @"salsa20", string password = @"barfoo!")
        {
            for (var i = 0; i < 100; i++)
            {
                IEncryptor encryptor = new StreamSodiumEncryptor(methodName, password);
                IEncryptor decryptor = new StreamSodiumEncryptor(methodName, password);
                RunEncryptionRound(encryptor, decryptor);
            }
        }
    }
}
