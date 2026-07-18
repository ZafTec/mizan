using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class SocialTools
{
    private readonly IBackendApiClient _api; public SocialTools(IBackendApiClient api) => _api = api;
    [McpServerTool(Name = "get_social_profile", ReadOnly = true, Idempotent = true)] public Task<string> Profile(CancellationToken ct = default) => _api.GetAsync("/api/Social/profile", ct);
    [McpServerTool(Name = "save_social_profile", Idempotent = true)] public Task<string> SaveProfile(string displayName, string? bio = null, string? avatarUrl = null, bool defaultPublishWorkouts = false, CancellationToken ct = default) => _api.PostAsync("/api/Social/profile", new { displayName, bio, avatarUrl, defaultPublishWorkouts }, ct);
    [McpServerTool(Name = "delete_social_profile", Destructive = true)] public Task<string> DeleteProfile(CancellationToken ct = default) => _api.DeleteAsync("/api/Social/profile", ct);
    [McpServerTool(Name = "rotate_social_share_link", Destructive = true)] public Task<string> Rotate(CancellationToken ct = default) => _api.PostAsync("/api/Social/profile/rotate-token", null, ct);
    [McpServerTool(Name = "request_social_follow")] public Task<string> RequestFollow(string shareToken, CancellationToken ct = default) => _api.PostAsync("/api/Social/follows", new { shareToken }, ct);
    [McpServerTool(Name = "list_social_follows", ReadOnly = true, Idempotent = true)] public Task<string> Follows(string direction = "in", string? status = null, CancellationToken ct = default) => _api.GetAsync($"/api/Social/follows?direction={direction}&status={status}", ct);
    [McpServerTool(Name = "respond_social_follow")] public Task<string> Respond(string id, bool accept, CancellationToken ct = default) => _api.PostAsync($"/api/Social/follows/{id}/respond", new { accept }, ct);
    [McpServerTool(Name = "remove_social_follow", Destructive = true)] public Task<string> RemoveFollow(string id, CancellationToken ct = default) => _api.DeleteAsync($"/api/Social/follows/{id}", ct);
    [McpServerTool(Name = "get_social_feed", ReadOnly = true, Idempotent = true)] public Task<string> Feed(int page = 1, int pageSize = 20, CancellationToken ct = default) => _api.GetAsync($"/api/Social/feed?page={page}&pageSize={pageSize}", ct);
    [McpServerTool(Name = "publish_workout_to_feed")] public Task<string> PublishWorkout(string workoutId, string? caption = null, CancellationToken ct = default) => _api.PostAsync("/api/Social/feed", new { type = "WorkoutCompleted", workoutId, caption }, ct);
    [McpServerTool(Name = "delete_feed_item", Destructive = true)] public Task<string> DeleteFeedItem(string id, CancellationToken ct = default) => _api.DeleteAsync($"/api/Social/feed/{id}", ct);
    [McpServerTool(Name = "react_to_feed_item")] [Description("Emoji must be one of 👍 ❤️ 💪 🔥 👏 🎉 🏆.")] public Task<string> React(string id, string emoji, CancellationToken ct = default) => _api.PostAsync($"/api/Social/feed/{id}/reactions", new { emoji }, ct);
    [McpServerTool(Name = "remove_feed_reaction", Destructive = true)] public Task<string> RemoveReaction(string id, string emoji, CancellationToken ct = default) => _api.DeleteAsync($"/api/Social/feed/{id}/reactions?emoji={Uri.EscapeDataString(emoji)}", ct);
    [McpServerTool(Name = "comment_on_feed_item")] public Task<string> Comment(string id, string body, CancellationToken ct = default) => _api.PostAsync($"/api/Social/feed/{id}/comments", new { body }, ct);
    [McpServerTool(Name = "delete_feed_comment", Destructive = true)] public Task<string> DeleteComment(string id, CancellationToken ct = default) => _api.DeleteAsync($"/api/Social/comments/{id}", ct);
    [McpServerTool(Name = "report_social_content")] public Task<string> Report(string targetType, string targetId, string reason, CancellationToken ct = default) => _api.PostAsync("/api/Social/reports", new { targetType, targetId, reason }, ct);
}
