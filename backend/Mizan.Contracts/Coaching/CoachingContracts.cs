namespace Mizan.Contracts.Coaching;

/// <summary>What a client lets their coach see and do. A field left null keeps its value; End ends the relationship.</summary>
public record UpdateGrantsRequest(
    bool? CanViewNutrition = null,
    bool? CanViewWorkouts = null,
    bool? CanViewMeasurements = null,
    bool? CanMessage = null,
    bool End = false,
    bool? CanPlanMeals = null
);

/// <summary>One message in a coach and client conversation.</summary>
public record SendMessageRequest(Guid ConversationId, string Content);

/// <summary>A coach asking the assistant about one client. ThreadId continues a conversation.</summary>
public record AskClientRequest(Guid? ThreadId, string Message);
