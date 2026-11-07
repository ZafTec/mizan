namespace Mizan.Infrastructure.Push;

public class PushOptions
{
    public const string SectionName = "Push";

    /// <summary>Firebase project id. Push is off until this and the service account are set.</summary>
    public string FcmProjectId { get; set; } = string.Empty;

    /// <summary>The Firebase service account key, as the JSON file's content.</summary>
    public string FcmServiceAccountJson { get; set; } = string.Empty;
}
