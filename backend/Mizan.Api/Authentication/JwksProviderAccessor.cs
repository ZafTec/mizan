namespace Mizan.Api.Authentication;

public static class JwksProviderAccessor
{
    public static IJwksProvider? Current { get; private set; }

    public static void Set(IJwksProvider provider)
    {
        Current = provider;
    }
}
