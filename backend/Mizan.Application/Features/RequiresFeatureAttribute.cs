namespace Mizan.Application.Features;

/// <summary>
/// Marks a MediatR request or an API action as belonging to a catalog feature.
/// <see cref="FeatureGateBehavior{TRequest,TResponse}"/> checks requests and the
/// API's feature filter checks actions; both ask <see cref="FeatureGate"/>, so
/// the rule lives in one place and no adapter repeats it.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiresFeatureAttribute : Attribute
{
    public RequiresFeatureAttribute(string key) => Key = key;

    public string Key { get; }
}
