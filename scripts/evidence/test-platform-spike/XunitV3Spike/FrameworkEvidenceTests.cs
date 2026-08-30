using Andreja.Adapters.PostgreSql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using Xunit;

namespace XunitV3Spike;

public sealed class BasicAndLifecycleTests : IAsyncLifetime
{
    private readonly ITestOutputHelper output;
    private bool initialized;

    public BasicAndLifecycleTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    public ValueTask InitializeAsync()
    {
        initialized = true;
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        var path = Environment.GetEnvironmentVariable("ANDREJA_SPIKE_LIFECYCLE_FILE");
        if (path is not null)
        {
            await File.WriteAllTextAsync(path, "xunit-v3-cleanup");
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void FactAndAsyncLifecyclePass()
    {
        output.WriteLine("xunit-v3-output");
        Assert.True(initialized);
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(-1, 1, 0)]
    [Trait("Category", "Smoke")]
    public void InlineDataAdds(int left, int right, int expected) =>
        Assert.Equal(expected, left + right);

    public static TheoryData<Exception> RuntimeRows =>
    [
        new InvalidOperationException("first"),
        new ArgumentException("second"),
    ];

    [Theory]
    [MemberData(nameof(RuntimeRows), DisableDiscoveryEnumeration = true)]
    public void RuntimeExpandedMemberDataIsAccountedFor(Exception exception) =>
        Assert.NotEmpty(exception.Message);

    [Fact(Skip = "intentional evidence skip")]
    [Trait("Category", "Smoke")]
    public void IntentionalSkipIsReported()
    {
    }

    [Fact(Timeout = 2_000)]
    [Trait("Category", "Smoke")]
    public async Task TimeoutAndCancellationAreBounded()
    {
        await Task.Delay(10, TestContext.Current.CancellationToken);
    }
}

public sealed class AndrejaFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Development");
}

public sealed class WebApplicationFactoryTests(AndrejaFactory factory)
    : IClassFixture<AndrejaFactory>
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnonymousApiAndFakeAuthenticationHeaderStayUnauthorized()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using var anonymous = await client.GetAsync(
            "/api/v1/open-loops/tasks",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Add("X-Andreja-Test-Authenticate", "true");
        using var fakeHeader = await client.GetAsync(
            "/api/v1/open-loops/tasks",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, fakeHeader.StatusCode);
    }
}

public sealed class EfMigrationMetadataTests
{
    [Fact]
    public void ProductionEfMigrationsAreDiscoverableWithoutConnecting()
    {
        var migrations = typeof(AndrejaIdentityDbContext).Assembly
            .GetTypes()
            .Count(type => type.GetCustomAttribute<MigrationAttribute>() is not null);

        Assert.Equal(6, migrations);
    }
}

public sealed class SharedFixture : IAsyncLifetime
{
    public Guid InstanceId { get; } = Guid.NewGuid();

    public ValueTask InitializeAsync()
    {
        SharedFixtureState.Record(InstanceId);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public static class SharedFixtureState
{
    private static readonly ConcurrentBag<Guid> InstanceIds = [];

    public static void Record(Guid instanceId) => InstanceIds.Add(instanceId);

    public static int DistinctInstances => InstanceIds.Distinct().Count();
}

[CollectionDefinition("shared-resource")]
public sealed class SharedResourceGroup : ICollectionFixture<SharedFixture>;

[Collection("shared-resource")]
public sealed class SharedFixtureFirstTests(SharedFixture fixture)
{
    [Fact]
    public void FirstClassReceivesCollectionFixture()
    {
        Assert.NotEqual(Guid.Empty, fixture.InstanceId);
        Assert.Equal(1, SharedFixtureState.DistinctInstances);
    }
}

[Collection("shared-resource")]
public sealed class SharedFixtureSecondTests(SharedFixture fixture)
{
    [Fact]
    public void SecondClassReceivesSameCollectionFixture()
    {
        Assert.NotEqual(Guid.Empty, fixture.InstanceId);
        Assert.Equal(1, SharedFixtureState.DistinctInstances);
    }
}

public static class ConcurrencyProbe
{
    private static readonly TaskCompletionSource<bool> First =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly TaskCompletionSource<bool> Second =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static async Task MeetAsync(bool first)
    {
        (first ? First : Second).TrySetResult(true);
        await (first ? Second : First).Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}

public sealed class ConcurrencyFirstTests
{
    [Fact]
    public Task SeparateClassesRunConcurrently() => ConcurrencyProbe.MeetAsync(first: true);
}

public sealed class ConcurrencySecondTests
{
    [Fact]
    public Task SeparateClassesRunConcurrently() => ConcurrencyProbe.MeetAsync(first: false);
}
