using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Mizan.Tests.Infrastructure.Email;

internal enum SmtpQuitBehavior
{
    Reply,
    Reject,
    Close,
}

internal sealed record SmtpTestServerOptions
{
    public bool RejectGreetings { get; init; }
    public bool AdvertiseAuthentication { get; init; }
    public bool RejectData { get; init; }
    public SmtpQuitBehavior QuitBehavior { get; init; }
    public Action? OnQuit { get; init; }
}

/// <summary>Loopback-only SMTP fixture. It never relays or persists message contents.</summary>
internal sealed class SmtpTestServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(10));
    private readonly SmtpTestServerOptions _options;

    public SmtpTestServer(SmtpTestServerOptions? options = null)
    {
        _options = options ?? new SmtpTestServerOptions();
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Completion = RunAsync();
    }

    public int Port { get; }
    public CancellationToken CancellationToken => _timeout.Token;
    public Task Completion { get; }
    public List<string> Greetings { get; } = [];
    public List<string> Commands { get; } = [];
    public int AcceptedMessages { get; private set; }

    private async Task RunAsync()
    {
        using var connection = await _listener.AcceptTcpClientAsync(CancellationToken);
        await using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true,
        };

        Task ReplyAsync(string response) => writer.WriteLineAsync(response.AsMemory(), CancellationToken);

        await ReplyAsync("220 smtp.example.test synthetic SMTP fixture");
        while (await reader.ReadLineAsync(CancellationToken) is { } command)
        {
            var verb = command.Split(' ', 2)[0];
            Commands.Add(verb);
            switch (verb)
            {
                case "EHLO":
                    Greetings.Add(command);
                    if (_options.RejectGreetings)
                    {
                        await ReplyAsync("500 EHLO unsupported by this fixture");
                        break;
                    }

                    await ReplyAsync("250-smtp.example.test");
                    if (_options.AdvertiseAuthentication)
                    {
                        await ReplyAsync("250-AUTH PLAIN");
                    }
                    await ReplyAsync("250 HELP");
                    break;
                case "HELO":
                    Greetings.Add(command);
                    if (_options.RejectGreetings)
                    {
                        await ReplyAsync("550 Greeting rejected; no mail accepted");
                        return;
                    }
                    await ReplyAsync("250 smtp.example.test");
                    break;
                case "AUTH":
                    await ReplyAsync("235 Authentication accepted");
                    break;
                case "MAIL":
                case "RCPT":
                case "RSET":
                    await ReplyAsync("250 OK");
                    break;
                case "DATA":
                    await ReplyAsync("354 End message with a single dot");
                    string? line;
                    do
                    {
                        // Consume the synthetic message without retaining it.
                        line = await reader.ReadLineAsync(CancellationToken);
                        if (line is null)
                        {
                            return;
                        }
                    }
                    while (line != ".");
                    if (_options.RejectData)
                    {
                        await ReplyAsync("550 Synthetic message rejected");
                    }
                    else
                    {
                        AcceptedMessages++;
                        await ReplyAsync("250 Message accepted");
                    }
                    break;
                case "QUIT":
                    _options.OnQuit?.Invoke();
                    if (_options.QuitBehavior == SmtpQuitBehavior.Close)
                    {
                        return;
                    }
                    await ReplyAsync(_options.QuitBehavior == SmtpQuitBehavior.Reject
                        ? "500 QUIT rejected after message acceptance"
                        : "221 Closing connection");
                    return;
                default:
                    await ReplyAsync("500 Unexpected command");
                    break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _timeout.CancelAsync();
        _listener.Stop();
        try
        {
            await Completion;
        }
        catch (OperationCanceledException) when (_timeout.IsCancellationRequested)
        {
        }
        finally
        {
            _timeout.Dispose();
        }
    }
}
