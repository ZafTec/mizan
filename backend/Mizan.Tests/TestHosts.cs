using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Mizan.Tests;

/// <summary>A started in-memory host for a small pipeline under test, built the generic-host way.</summary>
internal static class TestHosts
{
    public static IHost Start(Action<IServiceCollection> services, Action<IApplicationBuilder> app)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services).Configure(app))
            .Build();
        host.Start();
        return host;
    }
}
