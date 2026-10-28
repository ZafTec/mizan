using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Mizan.Application.Common;
using Mizan.Domain.Settings;
using Mizan.Infrastructure.Ai;
using Mizan.Infrastructure.Email;
using Mizan.Infrastructure.Identity;
using Mizan.Infrastructure.Outbox;
using Mizan.Infrastructure.Push;
using Mizan.Infrastructure.Settings;
using Mizan.Infrastructure.Storage;
using Xunit;

namespace Mizan.Tests.Settings;

/// <summary>
/// With nothing stored, a feature runs on the catalog's defaults. The options classes carry the same values, so
/// code that builds one directly (a test, a fallback after a bad row) behaves like an empty settings table.
/// </summary>
public class RuntimeSettingDefaultsTests
{
    public static TheoryData<string, Type> Sections => new()
    {
        { "Smtp", typeof(SmtpOptions) },
        { "Ai", typeof(AiOptions) },
        { "Storage", typeof(StorageOptions) },
        { "Telegram", typeof(TelegramOptions) },
        { "Push", typeof(PushOptions) },
        { "Paddle", typeof(PaddleOptions) },
        { "OAuth", typeof(AuthorizationServerOptions) },
        { "Outbox", typeof(OutboxOptions) },
        { "Prices", typeof(PriceCrawlOptions) },
    };

    [Theory]
    [MemberData(nameof(Sections))]
    public void TheCatalogsDefaults_MatchTheOptionsClass(string section, Type options)
    {
        var defaults = RuntimeSettingDefinitions.All
            .Where(d => d.Section == section && d.Default is not null)
            .ToDictionary(d => d.ConfigurationPath, d => d.Default);

        var fromCatalog = Activator.CreateInstance(options)!;
        new ConfigurationBuilder().AddInMemoryCollection(defaults).Build().GetSection(section).Bind(fromCatalog);

        JsonSerializer.Serialize(fromCatalog, options).Should().Be(JsonSerializer.Serialize(Activator.CreateInstance(options), options));
    }
}
