using FluentAssertions;
using Mizan.Application.Telegram;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>A notification's web link, and the Mini App page its Open button opens in Telegram.</summary>
public class MiniAppPathsTests
{
    [Theory]
    [InlineData("/meal-plan/5d1f6a2e-0c4b-4f7e-9a51-2b8c3d4e5f60", "plan?planId=5d1f6a2e-0c4b-4f7e-9a51-2b8c3d4e5f60")]
    [InlineData("/meal-plan/5D1F6A2E-0C4B-4F7E-9A51-2B8C3D4E5F60/", "plan?planId=5d1f6a2e-0c4b-4f7e-9a51-2b8c3d4e5f60")]
    [InlineData("/meal-plan/5d1f6a2e-0c4b-4f7e-9a51-2b8c3d4e5f60?day=2026-10-23", "plan?planId=5d1f6a2e-0c4b-4f7e-9a51-2b8c3d4e5f60&day=2026-10-23")]
    [InlineData("/meal-plan/5d1f6a2e-0c4b-4f7e-9a51-2b8c3d4e5f60?tab=rules&day=2026-10-23", "plan?planId=5d1f6a2e-0c4b-4f7e-9a51-2b8c3d4e5f60&day=2026-10-23")]
    [InlineData("/meal-plan/5d1f6a2e-0c4b-4f7e-9a51-2b8c3d4e5f60?day=tomorrow", "plan?planId=5d1f6a2e-0c4b-4f7e-9a51-2b8c3d4e5f60")]
    [InlineData("/shopping-lists/7a8b9c0d-1e2f-4a3b-8c4d-5e6f7a8b9c0d", "shopping-list?listId=7a8b9c0d-1e2f-4a3b-8c4d-5e6f7a8b9c0d")]
    [InlineData("/meal-plan/not-a-guid", null)]
    [InlineData("/meal-plan", null)]
    [InlineData("/meal-plan/5d1f6a2e-0c4b-4f7e-9a51-2b8c3d4e5f60/edit", null)]
    [InlineData("/meal-plan/shopping-list", null)]
    [InlineData("/trainer/clients/5d1f6a2e-0c4b-4f7e-9a51-2b8c3d4e5f60/plan", null)]
    [InlineData("/ingredients/5d1f6a2e-0c4b-4f7e-9a51-2b8c3d4e5f60", null)]
    [InlineData("/social", null)]
    [InlineData("https://elsewhere.test/meal-plan/5d1f6a2e-0c4b-4f7e-9a51-2b8c3d4e5f60", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ALinkOpensTheMiniAppPageThatShowsTheSameThing_OrNothing(string? linkUrl, string? expected) =>
        MiniAppPaths.ForLink(linkUrl).Should().Be(expected);
}
