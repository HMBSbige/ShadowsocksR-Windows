using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shadowsocks.Encryption;
using Shadowsocks.Util;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace UnitTest
{
    [TestClass]
    public class UnitTest
    {
        [TestMethod]
        public void TestMd5()
        {
            var buff = Encoding.UTF8.GetBytes(@"密码");
            var md5Sum = MbedTLS.MD5(buff);
            var md5Hash = MD5.Create().ComputeHash(buff);
            Assert.IsTrue(md5Hash.SequenceEqual(md5Sum));
        }

        [TestMethod]
        public void EncryptStringTest()
        {
            var largeBytes = new byte[ushort.MaxValue * 100];
            Rng.RandBytes(largeBytes);
            var largeStr = Encoding.UTF8.GetString(largeBytes);
            using var encryptor = EncryptorFactory.GetEncryptor(@"aes-256-cfb", @"密码");

            var encodeStr = Utils.EncryptLargeBytesToBase64String(encryptor, largeBytes);

            var decodeStr = Encoding.UTF8.GetString(Utils.DecryptLargeBase64StringToBytes(encryptor, encodeStr));

            Assert.AreEqual(largeStr, decodeStr);
        }

    }
}
