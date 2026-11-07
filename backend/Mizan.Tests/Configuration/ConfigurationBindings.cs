using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Mizan.Tests.Configuration;

/// <summary>The configuration sections a service collection binds options to with <c>Configure&lt;T&gt;(section)</c>.</summary>
public static class ConfigurationBindings
{
    /// <summary>
    /// Each <c>Configure&lt;T&gt;(IConfiguration)</c> leaves a <see cref="ConfigurationChangeTokenSource{TOptions}"/>
    /// holding the section it binds; its path is the section's name, such as <c>App</c>.
    /// </summary>
    public static IReadOnlyList<(Type Options, string Path)> Sections(IEnumerable<ServiceDescriptor> services) =>
        services
            .Where(d => d.ServiceType.IsGenericType
                && d.ServiceType.GetGenericTypeDefinition() == typeof(IOptionsChangeTokenSource<>)
                && d.ImplementationInstance is not null
                && d.ImplementationInstance.GetType().IsGenericType
                && d.ImplementationInstance.GetType().GetGenericTypeDefinition() == typeof(ConfigurationChangeTokenSource<>))
            .Select(d => (d.ServiceType.GetGenericArguments()[0], PathOf(d.ImplementationInstance!)))
            .ToList();

    private static string PathOf(object source)
    {
        var field = source.GetType().GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ConfigurationChangeTokenSource no longer keeps its section in _config; update this helper.");
        return field.GetValue(source) switch
        {
            IConfigurationSection section => section.Path,
            IConfigurationRoot => "(root)",
            _ => "(unknown)",
        };
    }
}
