using System;
using System.Security.Cryptography;

namespace Shadowsocks.Util;

public static class Rng
{
    public static void RandBytes(byte[] buf, int length = -1)
    {
        RandomNumberGenerator.Fill(length < 0 ? buf : buf.AsSpan(0, length));
    }
}
