using Xunit;

namespace Mizan.Tests.Eval;

/// <summary>
/// A test that calls a live model and spends its tokens. It runs only when <c>MIZAN_TOOL_EVAL=1</c>
/// and the model settings are in the environment, so a normal test run and CI skip it.
/// </summary>
public sealed class ToolEvalFactAttribute : FactAttribute
{
    public const string Switch = "MIZAN_TOOL_EVAL";

    public ToolEvalFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Switch) != "1")
        {
            Skip = $"A live-model evaluation; set {Switch}=1 and EVAL_BASE_URL, EVAL_KEY and EVAL_MODEL to run it.";
        }
    }
}
