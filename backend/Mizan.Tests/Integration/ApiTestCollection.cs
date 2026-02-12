using Xunit;

namespace Mizan.Tests.Integration;

[CollectionDefinition("ApiIntegration", DisableParallelization = true)]
public class ApiTestCollection : ICollectionFixture<ApiTestFixture>
{
}
