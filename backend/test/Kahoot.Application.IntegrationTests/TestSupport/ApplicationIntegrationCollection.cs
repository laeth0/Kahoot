namespace Kahoot.Application.IntegrationTests.TestSupport;

using Xunit;

[CollectionDefinition("ApplicationIntegrationCollection", DisableParallelization = true)]
public sealed class ApplicationIntegrationCollection : ICollectionFixture<ApplicationDependencyFixture>
{
}
