using Andreja.Adapters.PostgreSql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;

[assembly: Parallelize(Workers = 2, Scope = ExecutionScope.ClassLevel)]

namespace MSTestMtpSpike;

[TestClass]
public sealed class BasicAndLifecycleTests(TestContext testContext)
{
    private bool initialized;

    [TestInitialize]
    public void Initialize() => initialized = true;

    [TestCleanup]
    public async Task Cleanup()
    {
        var path = Environment.GetEnvironmentVariable("ANDREJA_SPIKE_LIFECYCLE_FILE");
        if (path is not null)
        {
            await File.WriteAllTextAsync(
                path,
                "mstest-mtp-cleanup",
                testContext.CancellationToken);
        }
    }

    [TestMethod]
    [TestCategory("Smoke")]
    public void TestMethodAndLifecyclePass()
    {
        testContext.WriteLine("mstest-mtp-output");
        Assert.IsTrue(initialized);
    }

    [TestMethod]
    [DataRow(1, 2, 3)]
    [DataRow(-1, 1, 0)]
    [TestCategory("Smoke")]
    public void DataRowsAdd(int left, int right, int expected) =>
        Assert.AreEqual(expected, left + right);

    public static IEnumerable<(Exception exception, int ordinal)> RuntimeRows =>
    [
        (new InvalidOperationException("first"), 1),
        (new ArgumentException("second"), 2),
    ];

    [TestMethod]
    [DynamicData(nameof(RuntimeRows))]
    public void RuntimeExpandedDynamicDataIsAccountedFor(Exception exception, int ordinal)
    {
        Assert.IsNotEmpty(exception.Message);
        Assert.IsGreaterThan(0, ordinal);
    }

    [TestMethod]
    [Ignore("intentional evidence skip")]
    [TestCategory("Smoke")]
    public void IntentionalSkipIsReported()
    {
    }

    [TestMethod]
    [Timeout(2_000, CooperativeCancellation = true)]
    [TestCategory("Smoke")]
    public async Task TimeoutAndCancellationAreBounded() =>
        await Task.Delay(10, testContext.CancellationToken);
}

public sealed class AndrejaFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Development");
}

[TestClass]
public sealed class WebApplicationFactoryTests(TestContext testContext)
{
    private static AndrejaFactory factory = null!;

    [ClassInitialize]
    public static void Initialize(TestContext context) => factory = new AndrejaFactory();

    [ClassCleanup]
    public static void Cleanup() => factory.Dispose();

    [TestMethod]
    [TestCategory("Smoke")]
    public async Task AnonymousApiAndFakeAuthenticationHeaderStayUnauthorized()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using var anonymous = await client.GetAsync(
            "/api/v1/open-loops/tasks",
            testContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Add("X-Andreja-Test-Authenticate", "true");
        using var fakeHeader = await client.GetAsync(
            "/api/v1/open-loops/tasks",
            testContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.Unauthorized, fakeHeader.StatusCode);
    }
}

[TestClass]
public sealed class EfMigrationMetadataTests
{
    [TestMethod]
    public void ProductionEfMigrationsAreDiscoverableWithoutConnecting()
    {
        var migrations = typeof(AndrejaIdentityDbContext).Assembly
            .GetTypes()
            .Count(type => type.GetCustomAttribute<MigrationAttribute>() is not null);

        Assert.AreEqual(6, migrations);
    }
}

public sealed class SharedFixture : IDisposable
{
    public SharedFixture()
    {
        var marker = Environment.GetEnvironmentVariable("ANDREJA_SPIKE_FIXTURE_FILE");
        if (marker is not null)
        {
            File.WriteAllText(marker, "started");
        }
    }

    public Guid InstanceId { get; } = Guid.NewGuid();

    public void Dispose()
    {
        var marker = Environment.GetEnvironmentVariable("ANDREJA_SPIKE_FIXTURE_FILE");
        if (marker is not null)
        {
            File.AppendAllText(marker, ";disposed");
        }
    }
}

public static class SharedFixtureOwner
{
    private static readonly Lazy<SharedFixture> Fixture = new();
    private static readonly ConcurrentBag<Guid> InstanceIds = [];

    public static SharedFixture Get()
    {
        var fixture = Fixture.Value;
        InstanceIds.Add(fixture.InstanceId);
        return fixture;
    }

    public static int DistinctInstances => InstanceIds.Distinct().Count();

    public static void DisposeIfCreated()
    {
        if (Fixture.IsValueCreated)
        {
            Fixture.Value.Dispose();
        }
    }
}

[TestClass]
public sealed class SharedFixtureFirstTests
{
    private static SharedFixture fixture = null!;

    [ClassInitialize]
    public static void Initialize(TestContext context) => fixture = SharedFixtureOwner.Get();

    [TestMethod]
    public void FirstClassReceivesSharedFixture()
    {
        Assert.AreNotEqual(Guid.Empty, fixture.InstanceId);
        Assert.AreEqual(1, SharedFixtureOwner.DistinctInstances);
    }
}

[TestClass]
public sealed class SharedFixtureSecondTests
{
    private static SharedFixture fixture = null!;

    [ClassInitialize]
    public static void Initialize(TestContext context) => fixture = SharedFixtureOwner.Get();

    [TestMethod]
    public void SecondClassReceivesSameFixture()
    {
        Assert.AreNotEqual(Guid.Empty, fixture.InstanceId);
        Assert.AreEqual(1, SharedFixtureOwner.DistinctInstances);
    }
}

[TestClass]
public static class SharedFixtureCleanup
{
    [AssemblyCleanup]
    public static void Cleanup() => SharedFixtureOwner.DisposeIfCreated();
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

[TestClass]
public sealed class ConcurrencyFirstTests
{
    [TestMethod]
    public Task SeparateClassesRunConcurrently() => ConcurrencyProbe.MeetAsync(first: true);
}

[TestClass]
public sealed class ConcurrencySecondTests
{
    [TestMethod]
    public Task SeparateClassesRunConcurrently() => ConcurrencyProbe.MeetAsync(first: false);
}
