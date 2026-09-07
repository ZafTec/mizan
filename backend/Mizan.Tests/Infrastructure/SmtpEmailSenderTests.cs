using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Email;
using Mizan.Tests.Infrastructure.Email;
using Moq;
using Xunit;

namespace Mizan.Tests.Infrastructure;

public class SmtpEmailSenderTests
{
    private static readonly EmailMessage Message = new(
        "recipient@example.test", "Synthetic SMTP message", "<p>Synthetic message</p>", "Synthetic message");

    [Theory]
    [InlineData("smtp.example.test")]
    [InlineData("  smtp.example.test  ")]
    public async Task ConfiguredLocalDomainIsUsedForEhloAndHelo(string localDomain)
    {
        var greetings = await CaptureGreetingsAsync(localDomain);

        greetings.Should().Equal("EHLO smtp.example.test", "HELO smtp.example.test");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task MissingLocalDomainUsesTheSenderDomainForEhloAndHelo(string? localDomain)
    {
        var greetings = await CaptureGreetingsAsync(localDomain);

        greetings.Should().Equal("EHLO example.test", "HELO example.test");
    }

    [Theory]
    [InlineData("Production", "")]
    [InlineData("Production", " ")]
    [InlineData("Staging", "")]
    public async Task MissingHostOutsideDevelopmentFailsDelivery(string environment, string host)
    {
        var sender = CreateSender(new SmtpOptions { Host = host }, environment);

        var send = () => sender.SendAsync(Message);

        await send.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Smtp:Host*");
    }

    [Fact]
    public async Task DefaultSecurityRefusesAPlaintextServerBeforeSendingMail()
    {
        await using var server = new SmtpTestServer();
        var sender = CreateSender(ServerOptions(server));

        var send = () => sender.SendAsync(Message, server.CancellationToken);

        await send.Should().ThrowAsync<NotSupportedException>();
        await server.Completion;
        server.Commands.Should().NotContain("MAIL");
        server.AcceptedMessages.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitPlaintextRelayAuthenticatesOnlyWhenCredentialsAreConfigured(bool authenticate)
    {
        await using var server = new SmtpTestServer(new SmtpTestServerOptions { AdvertiseAuthentication = true });
        var options = ServerOptions(server);
        options.Security = SmtpSecurityMode.None;
        if (authenticate)
        {
            options.Username = "sender@example.test";
            options.Password = "synthetic-password";
        }

        await CreateSender(options).SendAsync(Message, server.CancellationToken);
        await server.Completion;

        server.Commands.Count(command => command == "AUTH").Should().Be(authenticate ? 1 : 0);
        server.AcceptedMessages.Should().Be(1);
        server.Commands.Count(command => command == "DATA").Should().Be(1);
    }

    [Fact]
    public async Task LegacyStartTlsDisabledStillAllowsAPlaintextRelay()
    {
        await using var server = new SmtpTestServer();
        var options = ServerOptions(server);
        options.UseStartTls = false;

        await CreateSender(options).SendAsync(Message, server.CancellationToken);
        await server.Completion;

        server.AcceptedMessages.Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DisconnectFailureAfterAcceptanceDoesNotFailDelivery(bool rejectQuit)
    {
        await using var server = new SmtpTestServer(new SmtpTestServerOptions
        {
            QuitBehavior = rejectQuit ? SmtpQuitBehavior.Reject : SmtpQuitBehavior.Close,
        });
        var options = ServerOptions(server);
        options.Security = SmtpSecurityMode.None;

        await CreateSender(options).SendAsync(Message, server.CancellationToken);
        await server.Completion;

        server.Commands.Should().Contain("QUIT");
        server.AcceptedMessages.Should().Be(1);
        server.Commands.Count(command => command == "DATA").Should().Be(1);
    }

    [Fact]
    public async Task CancellationDuringDisconnectDoesNotFailAcceptedDelivery()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var server = new SmtpTestServer(new SmtpTestServerOptions
        {
            QuitBehavior = SmtpQuitBehavior.Close,
            OnQuit = cancellation.Cancel,
        });
        var options = ServerOptions(server);
        options.Security = SmtpSecurityMode.None;

        await CreateSender(options).SendAsync(Message, cancellation.Token);
        await server.Completion;

        cancellation.IsCancellationRequested.Should().BeTrue();
        server.AcceptedMessages.Should().Be(1);
    }

    [Fact]
    public async Task MessageRejectionPropagatesWithoutAnotherSendAttempt()
    {
        await using var server = new SmtpTestServer(new SmtpTestServerOptions { RejectData = true });
        var options = ServerOptions(server);
        options.Security = SmtpSecurityMode.None;

        var send = () => CreateSender(options).SendAsync(Message, server.CancellationToken);

        await send.Should().ThrowAsync<SmtpCommandException>();
        await server.Completion;
        server.AcceptedMessages.Should().Be(0);
        server.Commands.Count(command => command == "DATA").Should().Be(1);
    }

    [Fact]
    public async Task ExplicitImplicitTlsStartsTlsBeforeAnySmtpGreeting()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var capture = CaptureTlsHeaderAsync(listener, timeout.Token);
        var options = new SmtpOptions
        {
            Host = IPAddress.Loopback.ToString(),
            Port = ((IPEndPoint)listener.LocalEndpoint).Port,
            FromAddress = "sender@example.test",
            Security = SmtpSecurityMode.SslOnConnect,
        };

        var send = () => CreateSender(options).SendAsync(Message, timeout.Token);

        // The server closes after the ClientHello header; no certificate policy is overridden.
        var handshakeFailure = (await send.Should().ThrowAsync<SslHandshakeException>()).Which;
        var readHeader = () => capture;
        var header = (await readHeader.Should().NotThrowAsync(
            "implicit TLS must send a ClientHello before the SMTP greeting; TLS failure: {0}",
            handshakeFailure.GetBaseException().Message)).Which;
        header[0].Should().Be(0x16, "the first record must be a TLS handshake");
        header[1].Should().Be(0x03, "the record must use the TLS protocol family");
    }

    private static async Task<byte[]> CaptureTlsHeaderAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
        var header = new byte[3];
        await connection.GetStream().ReadExactlyAsync(header, cancellationToken);
        return header;
    }

    private static async Task<string[]> CaptureGreetingsAsync(string? localDomain)
    {
        await using var server = new SmtpTestServer(new SmtpTestServerOptions { RejectGreetings = true });
        var options = ServerOptions(server);
        options.Security = SmtpSecurityMode.None;
        options.LocalDomain = localDomain;
        var send = () => CreateSender(options).SendAsync(Message, server.CancellationToken);

        await send.Should().ThrowAsync<SmtpCommandException>();
        await server.Completion;
        return server.Greetings.ToArray();
    }

    private static SmtpOptions ServerOptions(SmtpTestServer server) => new()
    {
        Host = IPAddress.Loopback.ToString(),
        Port = server.Port,
        FromAddress = "sender@example.test",
    };

    private static SmtpEmailSender CreateSender(SmtpOptions options, string environment = "Production") => new(
        Options.Create(options),
        Mock.Of<IHostEnvironment>(value => value.EnvironmentName == environment),
        NullLogger<SmtpEmailSender>.Instance);
}
