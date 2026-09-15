using System;
using System.Text;
using NUnit.Framework;
using UIU.Simulator.Authentication;

namespace UIU.Simulator.Authentication.Tests
{
    [TestFixture]
    public sealed class JwtClaimsParserTests
    {
        [Test]
        public void TryReadTiming_ReadsUnixIatAndExp()
        {
            long iat = 1_700_000_000;
            long exp = iat + 3600;
            string jwt = MakeUnsignedJwt(iat, exp);

            Assert.That(JwtClaimsParser.TryReadTiming(jwt, out long parsedIat, out long parsedExp), Is.True);
            Assert.That(parsedIat, Is.EqualTo(iat));
            Assert.That(parsedExp, Is.EqualTo(exp));
            Assert.That(parsedExp - parsedIat, Is.EqualTo(3600));
        }

        [Test]
        public void IsExpiredUtc_UsesUnixUtcAndSmallSkewOnly()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Assert.That(JwtClaimsParser.IsExpiredUtc(now + 3600, 15), Is.False,
                "A one-hour remaining token must not be treated as expired.");
            Assert.That(JwtClaimsParser.IsExpiredUtc(now - 5, 15), Is.False,
                "Small clock skew may keep a just-expired token usable.");
            Assert.That(JwtClaimsParser.IsExpiredUtc(now - 30, 15), Is.True,
                "Tokens older than the small skew window must be expired.");
            Assert.That(JwtClaimsParser.ClockSkewSeconds, Is.EqualTo(15));
        }

        private static string MakeUnsignedJwt(long iat, long exp)
        {
            string header = "{\"alg\":\"none\",\"typ\":\"JWT\"}";
            string payload = $"{{\"sub\":\"user_test\",\"iat\":{iat},\"exp\":{exp}}}";
            return Base64Url(header) + "." + Base64Url(payload) + ".sig";
        }

        private static string Base64Url(string json)
        {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
