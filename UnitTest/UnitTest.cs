using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shadowsocks.Controller;
using Shadowsocks.Encryption;
using Shadowsocks.Util;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace UnitTest
{
    [TestClass]
    public class UnitTest
    {
        [TestMethod]
        public void TestCompareVersion()
        {
            Assert.IsTrue(UpdateChecker.CompareVersion("2.3.1.0", "2.3.1") == 0);
            Assert.IsTrue(UpdateChecker.CompareVersion("1.2", "1.3") < 0);
            Assert.IsTrue(UpdateChecker.CompareVersion("1.3", "1.2") > 0);
            Assert.IsTrue(UpdateChecker.CompareVersion("1.3", "1.3") == 0);
            Assert.IsTrue(UpdateChecker.CompareVersion("1.2.1", "1.2") > 0);
            Assert.IsTrue(UpdateChecker.CompareVersion("2.3.1", "2.4") < 0);
            Assert.IsTrue(UpdateChecker.CompareVersion("1.3.2", "1.3.1") > 0);
        }

        [TestMethod]
        public void TestMd5()
        {
            var buff = Encoding.UTF8.GetBytes(@"密码");
            var md5sum = MbedTLS.MD5(buff);
            var md5Hash = MD5.Create().ComputeHash(buff);
            Assert.IsTrue(md5Hash.SequenceEqual(md5sum));
        }

        [TestMethod]
        public void EncryptStringTest()
        {
            var largeBytes = new byte[ushort.MaxValue * 100];
            RandomNumberGenerator.Fill(largeBytes);
            var largeStr = Encoding.UTF8.GetString(largeBytes);
            var encryptor = EncryptorFactory.GetEncryptor(@"aes-256-cfb", @"密码");

            var encodeStr = Utils.EncryptLargeBytesToBase64String(encryptor, largeBytes);

            var decodeStr = Encoding.UTF8.GetString(Utils.DecryptLargeBase64StringToBytes(encryptor, encodeStr));

            Assert.AreEqual(largeStr, decodeStr);
        }
    }
}
