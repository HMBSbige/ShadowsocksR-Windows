using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Util;
using System;
using System.Text.RegularExpressions;

namespace UnitTest
{
    [TestClass]
    public class SubscribeTest
    {
        [TestMethod]
        public void ParseTest()
        {
            var url = @"sub://aHR0cHM6Ly9yYXcuZ2l0aHVidXNlcmNvbnRlbnQuY29tL0hNQlNiaWdlL1RleHRfVHJhbnNsYXRpb24vbWFzdGVyL1NoYWRvd3NvY2tzUi9mcmVlbm9kZXBsYWluLnR4dA";
            var sub = Regex.Match(url, "sub://([A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
            if (!sub.Success)
                throw new FormatException();

            var res = Base64.DecodeUrlSafeBase64(sub.Groups[1].Value);
            Assert.AreEqual(res, UpdateNode.DefaultUpdateUrl);
        }
    }
}
