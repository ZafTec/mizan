using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Email;
using Moq;
using Xunit;

namespace Mizan.Tests.Infrastructure;

public class SmtpEmailSenderTests
{
    [Theory]
    [InlineData("zaftech.co")]
    [InlineData("  zaftech.co  ")]
    public async Task ConfiguredLocalDomainIsUsedForEhloAndHelo(string localDomain)
    {
        var greetings = await CaptureGreetingsAsync(localDomain);

        greetings.Should().Equal("EHLO zaftech.co", "HELO zaftech.co");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task MissingLocalDomainPreservesTheMailKitDefaultGreeting(string? localDomain)
    {
        var expected = await CaptureGreetingsAsync(async (port, cancellationToken) =>
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(IPAddress.Loopback.ToString(), port,
                SecureSocketOptions.Auto, cancellationToken);
        });
        var greetings = await CaptureGreetingsAsync(localDomain);

        greetings.Should().Equal(expected);
    }

    private static Task<string[]> CaptureGreetingsAsync(string? localDomain) =>
        CaptureGreetingsAsync((port, cancellationToken) =>
        {
            var environment = Mock.Of<IHostEnvironment>(value => value.EnvironmentName == Environments.Production);
            var sender = new SmtpEmailSender(Options.Create(new SmtpOptions
            {
                Host = IPAddress.Loopback.ToString(),
                Port = port,
                UseStartTls = false,
                LocalDomain = localDomain,
                FromAddress = "sender@example.test",
            }), environment, NullLogger<SmtpEmailSender>.Instance);
            return sender.SendAsync(new EmailMessage(
                "recipient@example.test", "Synthetic greeting probe", "", ""), cancellationToken);
        });

    private static async Task<string[]> CaptureGreetingsAsync(Func<int, CancellationToken, Task> connect)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var capture = RejectAfterGreetingsAsync(listener, timeout.Token);

            // The loopback fixture rejects both greetings, before any envelope
            // or message body can be sent. It never relays or stores mail.
            var attempt = () => connect(((IPEndPoint)listener.LocalEndpoint).Port, timeout.Token);
            await attempt.Should().ThrowAsync<SmtpCommandException>();
            return await capture;
        }
        finally
        {
            await timeout.CancelAsync();
            listener.Stop();
        }
    }

    private static async Task<string[]> RejectAfterGreetingsAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true,
        };

        await writer.WriteLineAsync("220 localhost synthetic SMTP probe".AsMemory(), cancellationToken);
        var ehlo = await reader.ReadLineAsync(cancellationToken);
        await writer.WriteLineAsync("500 EHLO unsupported by this probe".AsMemory(), cancellationToken);
        var helo = await reader.ReadLineAsync(cancellationToken);
        await writer.WriteLineAsync("550 Greeting rejected; no mail accepted".AsMemory(), cancellationToken);
        return [ehlo ?? "", helo ?? ""];
    }
}
