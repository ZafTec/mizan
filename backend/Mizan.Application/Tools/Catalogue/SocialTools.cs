using System.Text.Json.Nodes;
using Mizan.Application.Commands;
using Mizan.Application.Queries;
using Mizan.Contracts.Mcp;

namespace Mizan.Application.Tools.Catalogue;

/// <summary>Social: the person's profile and share link, follows, and the feed of workouts with reactions and comments.</summary>
public static class SocialTools
{
    private static readonly string[] Emojis = ["👍", "❤️", "💪", "🔥", "👏", "🎉", "🏆"];
    private static readonly string[] ReportTargets = ["FeedItem", "FeedComment", "SocialProfile", "Exercise"];

    public static IReadOnlyList<TaskTool> All { get; } =
    [
        new TaskTool
        {
            Name = "get_social",
            Title = "Social feed, profile and follows",
            Description = "Read the person's social side: the feed of their own and followed people's posts with reactions and comments, their social profile and share link, or their follows.",
            Kind = ToolKind.Read,
            ModeArgument = "view",
            DefaultMode = "feed",
            Properties = new JsonObject
            {
                ["direction"] = ToolSchema.Enum("follows: \"in\" for people following them (the default), \"out\" for people they follow.", ["in", "out"]),
                ["status"] = ToolSchema.Enum("follows: only this status.", ["Pending", "Accepted"]),
                ["page"] = ToolSchema.Integer("feed: page, from 1."),
                ["pageSize"] = ToolSchema.Integer("feed: posts per page, 20 by default."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "feed",
                    Summary = "The feed, newest first. The default.",
                    Area = McpScopes.SocialRead,
                    Requests = [typeof(GetSocialFeedQuery)],
                    Replaces = ["get_social_feed"],
                    Build = (args, _) => new GetSocialFeedQuery(
                        Math.Max(1, args.OptionalInt("page") ?? 1),
                        Math.Clamp(args.OptionalInt("pageSize") ?? 20, 1, 100)),
                },
                new ToolMode
                {
                    Name = "profile",
                    Summary = "Their social profile with its share token, or none yet.",
                    Area = McpScopes.SocialRead,
                    Requests = [typeof(GetMySocialProfileQuery)],
                    Replaces = ["get_social_profile"],
                    Build = (_, _) => new GetMySocialProfileQuery(),
                    Shape = result => result ?? new { profile = (object?)null, message = "There is no social profile yet; social_action profile creates one." },
                },
                new ToolMode
                {
                    Name = "follows",
                    Summary = "Follows in a direction, with their ids and status.",
                    Area = McpScopes.SocialRead,
                    Requests = [typeof(GetFollowsQuery)],
                    Replaces = ["list_social_follows"],
                    Build = (args, _) => new GetFollowsQuery(args.OptionalString("direction") ?? "in", args.OptionalString("status")),
                },
            ],
        },
        new TaskTool
        {
            Name = "social_action",
            Title = "Act on the social feed",
            Description = "Save the social profile, ask to follow someone by their share token, accept or decline a follow request, publish a workout to the feed, "
                + "react to or comment on a post, or report content. Take each action only when the person asked for it.",
            Kind = ToolKind.Write,
            ModeArgument = "action",
            Properties = new JsonObject
            {
                ["displayName"] = ToolSchema.String("profile: the name others see."),
                ["bio"] = ToolSchema.String("profile: a short bio."),
                ["avatarUrl"] = ToolSchema.String("profile: an avatar address."),
                ["defaultPublishWorkouts"] = ToolSchema.Boolean("profile: publish new workouts to the feed by default."),
                ["shareToken"] = ToolSchema.String("follow: the share token from the other person's link."),
                ["followId"] = ToolSchema.Id("answer_follow: the follow request, from get_social view follows."),
                ["accept"] = ToolSchema.Boolean("answer_follow: true accepts, false declines."),
                ["workoutId"] = ToolSchema.Id("publish: the logged workout."),
                ["caption"] = ToolSchema.String("publish: a caption."),
                ["feedItemId"] = ToolSchema.Id("react, comment: the post."),
                ["emoji"] = ToolSchema.Enum("react: the reaction.", Emojis),
                ["body"] = ToolSchema.String("comment: the comment."),
                ["targetType"] = ToolSchema.Enum("report: what is reported.", ReportTargets),
                ["targetId"] = ToolSchema.Id("report: its id."),
                ["reason"] = ToolSchema.String("report: why, in the person's words."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "profile",
                    Summary = "Create or change the social profile.",
                    Area = McpScopes.SocialWrite,
                    Requests = [typeof(SaveSocialProfileCommand)],
                    Replaces = ["save_social_profile"],
                    Build = (args, _) => new SaveSocialProfileCommand(
                        args.RequiredString("displayName"),
                        args.OptionalString("bio"),
                        args.OptionalString("avatarUrl"),
                        args.OptionalBool("defaultPublishWorkouts") ?? false),
                    Shape = _ => new { saved = true },
                },
                new ToolMode
                {
                    Name = "follow",
                    Summary = "Ask to follow the person whose share token this is.",
                    Area = McpScopes.SocialWrite,
                    Requests = [typeof(RequestFollowCommand)],
                    Replaces = ["request_social_follow"],
                    Build = (args, _) => new RequestFollowCommand(args.RequiredString("shareToken")),
                    Shape = result => new { id = result },
                },
                new ToolMode
                {
                    Name = "answer_follow",
                    Summary = "Accept or decline the follow request followId.",
                    Area = McpScopes.SocialWrite,
                    Requests = [typeof(RespondToFollowCommand)],
                    Replaces = ["respond_social_follow"],
                    Build = (args, _) => new RespondToFollowCommand(
                        args.RequiredGuid("followId"),
                        args.OptionalBool("accept") ?? throw new Exceptions.DomainValidationException("'accept' is required: true or false.")),
                    Shape = _ => new { answered = true },
                },
                new ToolMode
                {
                    Name = "publish",
                    Summary = "Publish the logged workout workoutId to the feed.",
                    Area = McpScopes.SocialWrite,
                    Requests = [typeof(PublishFeedItemCommand)],
                    Replaces = ["publish_workout_to_feed"],
                    Build = (args, _) => new PublishFeedItemCommand("WorkoutCompleted", args.RequiredGuid("workoutId"), null, null, args.OptionalString("caption")),
                    Shape = result => new { id = result },
                },
                new ToolMode
                {
                    Name = "react",
                    Summary = "React to the post feedItemId with an emoji.",
                    Area = McpScopes.SocialWrite,
                    Requests = [typeof(AddFeedReactionCommand)],
                    Replaces = ["react_to_feed_item"],
                    Build = (args, _) => new AddFeedReactionCommand(args.RequiredGuid("feedItemId"), args.RequiredString("emoji")),
                    Shape = result => new { id = result },
                },
                new ToolMode
                {
                    Name = "comment",
                    Summary = "Comment on the post feedItemId.",
                    Area = McpScopes.SocialWrite,
                    Requests = [typeof(AddFeedCommentCommand)],
                    Replaces = ["comment_on_feed_item"],
                    Build = (args, _) => new AddFeedCommentCommand(args.RequiredGuid("feedItemId"), args.RequiredString("body")),
                    Shape = result => new { id = result },
                },
                new ToolMode
                {
                    Name = "report",
                    Summary = "Report a post, comment, profile or exercise to the moderators.",
                    Area = McpScopes.SocialWrite,
                    Requests = [typeof(ReportContentCommand)],
                    Replaces = ["report_social_content"],
                    Build = (args, _) => new ReportContentCommand(
                        args.RequiredString("targetType"),
                        args.RequiredGuid("targetId"),
                        args.RequiredString("reason")),
                    Shape = result => new { id = result },
                },
            ],
        },
    ];

    public static IReadOnlyList<ToolMode> DeleteKinds { get; } =
    [
        new ToolMode
        {
            Name = "social_profile",
            Summary = "The person's social profile, with every follow and their feed posts (no id). Previews first: what goes.",
            Area = McpScopes.SocialWrite,
            Requests = [typeof(DeleteSocialProfileCommand)],
            Replaces = ["delete_social_profile"],
            Build = (_, _) => new DeleteSocialProfileCommand(),
            Confirm = new ToolConfirmStep(
                typeof(GetSocialProfileDeletePreviewQuery),
                (_, _) => new GetSocialProfileDeletePreviewQuery(),
                result => ProfilePreviewFacts(result as SocialProfileDeletePreview)),
        },
        new ToolMode
        {
            Name = "share_link",
            Summary = "The profile's share link (no id): it stops working and a new one is returned.",
            Area = McpScopes.SocialWrite,
            Requests = [typeof(RotateSocialShareTokenCommand)],
            Replaces = ["rotate_social_share_link"],
            Build = (_, _) => new RotateSocialShareTokenCommand(),
            Shape = result => new { shareToken = result },
        },
        new ToolMode
        {
            Name = "follow",
            Summary = "A follow or follow request, either direction, by id.",
            Area = McpScopes.SocialWrite,
            Requests = [typeof(DeleteFollowCommand)],
            Replaces = ["remove_social_follow"],
            Build = (args, _) => new DeleteFollowCommand(args.RequiredGuid("id")),
        },
        new ToolMode
        {
            Name = "feed_item",
            Summary = "One of the person's feed posts, by id.",
            Area = McpScopes.SocialWrite,
            Requests = [typeof(DeleteFeedItemCommand)],
            Replaces = ["delete_feed_item"],
            Build = (args, _) => new DeleteFeedItemCommand(args.RequiredGuid("id")),
        },
        new ToolMode
        {
            Name = "reaction",
            Summary = "The person's reaction (emoji) on the post id.",
            Area = McpScopes.SocialWrite,
            Requests = [typeof(RemoveFeedReactionCommand)],
            Replaces = ["remove_feed_reaction"],
            Build = (args, _) => new RemoveFeedReactionCommand(args.RequiredGuid("id"), args.RequiredString("emoji")),
        },
        new ToolMode
        {
            Name = "comment",
            Summary = "One of the person's own comments, by id.",
            Area = McpScopes.SocialWrite,
            Requests = [typeof(DeleteFeedCommentCommand)],
            Replaces = ["delete_feed_comment"],
            Build = (args, _) => new DeleteFeedCommentCommand(args.RequiredGuid("id")),
        },
    ];

    /// <summary>What deleting the profile removes. Counts only, so the same state gives the same facts.</summary>
    private static JsonNode ProfilePreviewFacts(SocialProfileDeletePreview? preview)
    {
        if (preview is null) return new JsonObject();
        return new JsonObject
        {
            ["profile"] = preview.DisplayName,
            ["followers"] = preview.Followers,
            ["following"] = preview.Following,
            ["feedItems"] = preview.FeedItems,
            ["commentsOnThem"] = preview.CommentsOnFeedItems,
            ["reactionsOnThem"] = preview.ReactionsOnFeedItems,
            ["note"] = "The profile, every follow in either direction and every feed post go, with the comments and reactions on those posts. Comments they left on others' posts stay.",
        };
    }
}
