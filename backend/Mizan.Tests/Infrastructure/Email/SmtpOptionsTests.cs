using FluentAssertions;
using MailKit.Security;
using Mizan.Infrastructure.Email;
using Xunit;

namespace Mizan.Tests.Infrastructure.Email;

public class SmtpOptionsTests
{
    [Theory]
    [InlineData(SmtpSecurityMode.Auto, true, 587, SecureSocketOptions.StartTls)]
    [InlineData(SmtpSecurityMode.Auto, true, 465, SecureSocketOptions.SslOnConnect)]
    [InlineData(SmtpSecurityMode.Auto, true, 2525, SecureSocketOptions.StartTls)]
    [InlineData(SmtpSecurityMode.Auto, false, 587, SecureSocketOptions.Auto)]
    [InlineData(SmtpSecurityMode.Auto, false, 465, SecureSocketOptions.Auto)]
    [InlineData(SmtpSecurityMode.StartTls, false, 465, SecureSocketOptions.StartTls)]
    [InlineData(SmtpSecurityMode.SslOnConnect, true, 2525, SecureSocketOptions.SslOnConnect)]
    [InlineData(SmtpSecurityMode.None, true, 587, SecureSocketOptions.None)]
    public void SelectsSecureDefaultsAndPreservesExplicitAndLegacyModes(
        SmtpSecurityMode security, bool useStartTls, int port, SecureSocketOptions expected)
    {
        var options = new SmtpOptions { Security = security, UseStartTls = useStartTls, Port = port };

        options.GetSocketOptions().Should().Be(expected);
    }

    [Fact]
    public void UnsupportedSecurityModeFailsConfiguration()
    {
        var options = new SmtpOptions { Security = (SmtpSecurityMode)int.MaxValue };

        var resolve = () => options.GetSocketOptions();

        resolve.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(SmtpOptions.Security));
    }
}
