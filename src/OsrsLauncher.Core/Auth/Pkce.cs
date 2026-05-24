// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text;

namespace OsrsLauncher.Core.Auth;

public static class Pkce
{
    public static string GenerateVerifier(int byteLength = 32)
        => Base64Url(RandomNumberGenerator.GetBytes(byteLength));

    public static string CreateChallenge(string verifier)
        => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
