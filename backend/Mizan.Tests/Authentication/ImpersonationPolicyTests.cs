using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Mizan.Api.Middleware;
using Xunit;

namespace Mizan.Tests.Authentication;

/// <summary>
/// An administrator viewing the site as a user is held to what ImpersonationPolicy allows. The map
/// must name every controller, spell each prefix as the controller's route does, and refuse a write
/// to anything it does not know.
/// </summary>
public sealed class ImpersonationPolicyTests
{
    private static readonly Type[] ApiControllers = typeof(Program).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
        .ToArray();

    [Fact]
    public void EveryControllerInTheApi_IsClassified()
    {
        ApiControllers.Should().NotBeEmpty();

        var unclassified = ApiControllers.Where(t => !ImpersonationPolicy.Controllers.ContainsKey(t)).Select(t => t.Name).ToList();

        unclassified.Should().BeEmpty("a controller the policy does not know is closed to the view; say whether it is Allowed or Blocked");
        ImpersonationPolicy.Controllers.Keys.Should().OnlyContain(t => ApiControllers.Contains(t), "a classification for a controller that no longer exists is stale");
    }

    [Fact]
    public void EveryClassification_SpellsThePrefixAsTheControllersRouteDoes()
    {
        foreach (var (type, classification) in ImpersonationPolicy.Controllers)
        {
            var template = type.GetCustomAttribute<RouteAttribute>()?.Template;
            template.Should().NotBeNullOrEmpty($"{type.Name} needs a [Route] for its prefix to mean anything");
            var expected = "/" + template!.Replace("[controller]", type.Name[..^"Controller".Length]).Trim('/');
            classification.Prefix.Should().BeEquivalentTo(expected, $"{type.Name} is routed at {expected}");
        }
    }

    [Theory]
    [InlineData("GET", "/api/Something/new", false)]
    [InlineData("HEAD", "/api/Something/new", false)]
    [InlineData("OPTIONS", "/api/Something/new", false)]
    [InlineData("POST", "/api/Something/new", true)]
    [InlineData("PUT", "/api/Something/new", true)]
    [InlineData("DELETE", "/api/Something/new", true)]
    [InlineData("POST", "/api/Foods", false)]
    [InlineData("POST", "/api/foods/123/favorite", false)]
    [InlineData("POST", "/api/Auth/change-password", true)]
    [InlineData("POST", "/api/Auth/me", false)]
    [InlineData("POST", "/api/Auth/logout", false)]
    [InlineData("POST", "/api/Auth/impersonation/stop", false)]
    [InlineData("GET", "/api/Auth/external/google", true)]
    [InlineData("POST", "/api/admin/users/00000000-0000-0000-0000-000000000001/impersonate", true)]
    [InlineData("POST", "/hubs/chat/negotiate", false)]
    public void AWrite_IsRefusedUnlessItsRouteIsAllowed(string method, string path, bool refused)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        ImpersonationPolicy.IsRefused(context.Request).Should().Be(refused);
    }

    [Fact]
    public void TheMostSpecificPrefix_Wins()
    {
        ImpersonationPolicy.Classify("/api/admin/billing/x").Should().Be(ImpersonationAccess.Blocked);
        ImpersonationPolicy.Classify("/api/Nowhere").Should().BeNull();
    }
}
