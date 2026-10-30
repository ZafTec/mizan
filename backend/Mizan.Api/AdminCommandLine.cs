using Mizan.Application.Admin;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Settings;
using Serilog;

namespace Mizan.Api;

/// <summary>
/// One-off operator commands that run against the database and exit, like <c>--migrate</c>
/// (docs/ADMIN.md#first-administrator, docs/ADMIN.md#moving-settings-out-of-the-environment):
/// <code>
/// Mizan.Api --create-admin admin@example.com
/// Mizan.Api --import-env-settings
/// </code>
/// </summary>
public static class AdminCommandLine
{
    public const string CreateAdmin = "--create-admin";
    public const string ImportEnvSettings = "--import-env-settings";

    public static bool Handles(string[] args) => args.Contains(CreateAdmin) || args.Contains(ImportEnvSettings);

    /// <summary>Runs the command and returns the process exit code.</summary>
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        try
        {
            await DatabaseMigrations.RequireCurrentAsync(services);

            using var scope = services.CreateScope();
            var provider = scope.ServiceProvider;

            if (args.Contains(ImportEnvSettings))
            {
                return await EnvironmentSettingsImport.RunAsync(
                    provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<IRuntimeSettings>(),
                    Console.Out,
                    CancellationToken.None);
            }

            var index = Array.IndexOf(args, CreateAdmin);
            var email = index + 1 < args.Length ? args[index + 1] : string.Empty;
            var result = await AdminBootstrap.CreateAsync(
                provider.GetRequiredService<MizanDbContext>(),
                provider.GetRequiredService<IAppUrls>(),
                email,
                CancellationToken.None);

            // The link sets the password, so it goes to the terminal only, never to the log.
            Console.WriteLine(result.Created ? "Created a verified administrator." : "Made the existing account a verified administrator.");
            Console.WriteLine($"Open this link within {AdminBootstrap.LinkLifetime.TotalHours:0} hours to set the password. It works once:");
            Console.WriteLine(result.PasswordLink);
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "The operator command failed");
            return 1;
        }
    }
}
