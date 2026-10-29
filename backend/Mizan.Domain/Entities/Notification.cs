namespace Mizan.Domain.Entities;

/// <summary>
/// One notice in a person's in-app list. <see cref="Title"/>, <see cref="Body"/> and <see cref="LinkUrl"/>
/// are what every row has always had; the image, the actions and the facts are the structured parts
/// a newer message carries (docs/ARCHITECTURE.md#notifications) and are empty on older rows.
/// </summary>
public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;

    /// <summary>The group the person sets channels for (<c>NotificationKinds</c>). Null on rows written before kinds existed.</summary>
    public string? Kind { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }

    /// <summary>An absolute image URL that is the content of the notice, such as an achievement's badge.</summary>
    public string? ImageUrl { get; set; }

    public List<NotificationActionItem> Actions { get; set; } = [];
    public List<NotificationFactItem> Facts { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public virtual User User { get; set; } = null!;
}

/// <summary>A button under a notice: what it says and the app path it opens.</summary>
public class NotificationActionItem
{
    public string Label { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

/// <summary>A short label and value shown with a notice, such as "Streak" and "12 days".</summary>
public class NotificationFactItem
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
