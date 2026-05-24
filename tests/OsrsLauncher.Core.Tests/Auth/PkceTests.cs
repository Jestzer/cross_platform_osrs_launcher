// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Auth;
using Xunit;

namespace OsrsLauncher.Core.Tests.Auth;

public class PkceTests
{
    [Fact]
    public void CreateChallenge_MatchesRfc7636Vector()
    {
        // RFC 7636 Appendix B test vector.
        var verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        var challenge = Pkce.CreateChallenge(verifier);
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", challenge);
    }

    [Fact]
    public void GenerateVerifier_IsUrlSafeAndCorrectLength()
    {
        var v = Pkce.GenerateVerifier();
        Assert.InRange(v.Length, 43, 128);
        Assert.DoesNotContain('+', v);
        Assert.DoesNotContain('/', v);
        Assert.DoesNotContain('=', v);
    }
}
