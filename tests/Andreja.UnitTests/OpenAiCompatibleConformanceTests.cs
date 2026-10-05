using Andreja.Adapters.Assistant.OpenAiCompatible;
using Andreja.AppHost.Hosting;
using Andreja.AppHost.OpenLoops;
using Andreja.Modules.Assistant;
using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Andreja.Modules.Proposals;
using Andreja.Platform.Contracts.Assistant;
using Andreja.Platform.Contracts.Sharing;
using Andreja.Platform.Contracts.Skills;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Andreja.UnitTests;

[TestClass]
public sealed class OpenAiCompatibleConformanceTests
{
    private const string CredentialValue = "conformance-secret-not-for-logs";

    [TestMethod]
    public async Task MapsSuccessfulTypedToolAndContentFreeUsage()
    {
        await using var server = await ConformanceServer.StartAsync(async (context, _) =>
        {
            await AssertRequestAsync(context, CredentialValue);
            await WriteCompletionAsync(
                context,
                "open-loops_propose-task",
                """{"title":"Renew library card"}""",
                "Review this proposal.",
                21,
                7);
        });
        var store = new MutableCredentialStore(CredentialValue);
        var provider = Provider(server.Client, store);

        await using var session = await provider.CreateSessionAsync(
            SessionRequest(),
            CancellationToken.None);
        var response = await session.CompleteAsync(Request(), CancellationToken.None);

        Assert.IsTrue(
            response.Status == AssistantResponseStatus.Completed,
            response.Failure?.Code);
        Assert.AreEqual("Review this proposal.", response.Content);
        var call = Assert.ContainsSingle(response.ToolCalls);
        Assert.AreEqual(OpenLoopsSkill.ProposeTaskTool, call.ToolName);
        Assert.AreEqual("Renew library card", call.Arguments["title"].GetString());
        Assert.AreEqual(21, response.Usage.InputUnits);
        Assert.AreEqual(7, response.Usage.OutputUnits);
        Assert.AreEqual(1, response.Usage.ToolCount);
        Assert.AreEqual("completed", response.Usage.ResultClass);
    }

    [TestMethod]
    public async Task ConfiguredByokCreatesOnlyReviewableOpenLoopsProposal()
    {
        await using var server = await ConformanceServer.StartAsync(async (context, _) =>
        {
            await AssertRequestAsync(context, CredentialValue);
            await WriteCompletionAsync(
                context,
                "open-loops_propose-task",
                """{"title":"Prepare quarterly review","details":"Collect notes"}""",
                "I prepared a proposal.",
                18,
                5);
        });
        var taskStore = new InMemoryOpenLoopsTaskStore();
        var proposalStore = new InMemoryProposalStore();
        var application = new OpenLoopsTaskApplication(
            taskStore,
            proposalStore,
            TimeProvider.System);
        var assistant = new OpenLoopsAssistantService(
            Provider(server.Client, new MutableCredentialStore(CredentialValue)),
            OpenLoopsSkill.CreateHost(application));
        var context = new TenantPrincipalContext(
            TenantId.New(),
            AppUserId.New(),
            PrincipalId.New(),
            OpenLoopsPolicy.Purpose);

        var result = await assistant.ProposeTaskAsync(
            context,
            "Prepare my quarterly review");

        Assert.IsNotNull(result.Proposal);
        Assert.IsNull(result.ErrorCode);
        Assert.IsEmpty(await application.ListAsync(context));
        Assert.Contains(
            "Prepare quarterly review",
            result.Proposal.Diff.AfterCanonical,
            StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("unapproved_tool", """{"title":"Not allowed"}""", "provider-unknown-tool")]
    [DataRow("open-loops_propose-task", """{"title":12}""", "provider-malformed-tool")]
    [DataRow("open-loops_propose-task", """{"details":"Missing title"}""", "provider-malformed-tool")]
    [DataRow("open-loops_propose-task", """not-json""", "provider-malformed-tool")]
    public async Task RejectsUnknownOrMalformedToolCalls(
        string toolName,
        string arguments,
        string expectedCode)
    {
        await using var server = await ConformanceServer.StartAsync((context, _) =>
            WriteCompletionAsync(context, toolName, arguments, null, 2, 1));
        var response = await CompleteAsync(
            Provider(server.Client, new MutableCredentialStore(CredentialValue)));

        Assert.AreEqual(AssistantResponseStatus.Failed, response.Status);
        Assert.AreEqual(expectedCode, response.Failure?.Code);
        Assert.IsEmpty(response.ToolCalls);
    }

    [TestMethod]
    public async Task ReportsMalformedJsonWithoutLeakingProviderBody()
    {
        await using var server = await ConformanceServer.StartAsync(async (context, _) =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync($"{{malformed:{CredentialValue}");
        });
        var response = await CompleteAsync(
            Provider(server.Client, new MutableCredentialStore(CredentialValue)));

        Assert.AreEqual("provider-malformed-json", response.Failure?.Code);
        Assert.DoesNotContain(
            CredentialValue,
            JsonSerializer.Serialize(response),
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task TimeoutAndCallerCancellationAreDistinct()
    {
        await using var server = await ConformanceServer.StartAsync(async (context, _) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);
        });
        var timedProvider = Provider(
            server.Client,
            new MutableCredentialStore(CredentialValue),
            Profile() with { Timeout = TimeSpan.FromMilliseconds(50) });

        var timedOut = await CompleteAsync(timedProvider);

        Assert.AreEqual(AssistantResponseStatus.Failed, timedOut.Status);
        Assert.AreEqual("provider-timeout", timedOut.Failure?.Code);
        Assert.IsTrue(timedOut.Failure?.IsTransient);

        var cancellationProvider = Provider(
            server.Client,
            new MutableCredentialStore(CredentialValue));
        await using var session = await cancellationProvider.CreateSessionAsync(
            SessionRequest(),
            CancellationToken.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var cancelled = await session.CompleteAsync(Request(), cancellation.Token);

        Assert.AreEqual(AssistantResponseStatus.Cancelled, cancelled.Status);
        Assert.IsNull(cancelled.Failure);
    }

    [TestMethod]
    public async Task RetriesOnlyRetryableProviderErrors()
    {
        await using var retryServer = await ConformanceServer.StartAsync(async (context, call) =>
        {
            if (call == 1)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }

            await WriteCompletionAsync(
                context,
                "open-loops_propose-task",
                """{"title":"Retried"}""",
                null,
                3,
                1);
        });
        var retried = await CompleteAsync(
            Provider(retryServer.Client, new MutableCredentialStore(CredentialValue)));

        Assert.AreEqual(AssistantResponseStatus.Completed, retried.Status);
        Assert.AreEqual(1, retried.Usage.RetryCount);
        Assert.AreEqual(2, retryServer.CallCount);

        await using var rejectedServer = await ConformanceServer.StartAsync((context, _) =>
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        });
        var rejected = await CompleteAsync(
            Provider(rejectedServer.Client, new MutableCredentialStore(CredentialValue)));

        Assert.AreEqual("provider-rejected-request", rejected.Failure?.Code);
        Assert.IsFalse(rejected.Failure?.IsTransient);
        Assert.AreEqual(1, rejectedServer.CallCount);
    }

    [TestMethod]
    public async Task RejectsOversizedResponsesBeforeJsonParsing()
    {
        await using var server = await ConformanceServer.StartAsync(async (context, _) =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(new string('x', 2048));
        });
        var provider = Provider(
            server.Client,
            new MutableCredentialStore(CredentialValue),
            policy: Policy(maximumResponseBodyBytes: 1024));

        var response = await CompleteAsync(provider);

        Assert.AreEqual("provider-response-too-large", response.Failure?.Code);
    }

    [TestMethod]
    public async Task RejectsRedirectsAndEndpointConfusion()
    {
        await using var server = await ConformanceServer.StartAsync((context, _) =>
        {
            context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
            context.Response.Headers.Location = "https://not-allowlisted.example/v1/chat/completions";
            return Task.CompletedTask;
        });
        var redirected = await CompleteAsync(
            Provider(server.Client, new MutableCredentialStore(CredentialValue)));

        Assert.AreEqual("provider-redirect-rejected", redirected.Failure?.Code);
        Assert.AreEqual(1, server.CallCount);

        var confused = Provider(
            server.Client,
            new MutableCredentialStore(CredentialValue),
            Profile() with { Endpoint = new Uri("http://127.0.0.1/v1") },
            Policy());
        var endpointRejected = await CompleteAsync(confused);

        Assert.AreEqual("endpoint-not-allowed", endpointRejected.Failure?.Code);
        Assert.AreEqual(1, server.CallCount);
    }

    [TestMethod]
    public async Task ProviderErrorsNeverExposeCredentialOrResponseBody()
    {
        await using var server = await ConformanceServer.StartAsync(async (context, _) =>
        {
            await AssertRequestAsync(context, CredentialValue);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync($"invalid key: {CredentialValue}");
        });
        var response = await CompleteAsync(
            Provider(server.Client, new MutableCredentialStore(CredentialValue)));
        var serialized = JsonSerializer.Serialize(response);

        Assert.AreEqual("provider-rejected-request", response.Failure?.Code);
        Assert.DoesNotContain(CredentialValue, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid key", serialized, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task MetricsContainOnlyContentFreeBoundedTags()
    {
        var observedTags = new ConcurrentBag<KeyValuePair<string, object?>>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == OpenAiCompatibleMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>(
            (_, _, tags, _) =>
            {
                foreach (var tag in tags)
                {
                    observedTags.Add(tag);
                }
            });
        listener.SetMeasurementEventCallback<double>(
            (_, _, tags, _) =>
            {
                foreach (var tag in tags)
                {
                    observedTags.Add(tag);
                }
            });
        listener.Start();
        await using var server = await ConformanceServer.StartAsync((context, _) =>
            WriteCompletionAsync(
                context,
                "open-loops_propose-task",
                """{"title":"Metric canary task"}""",
                "Metric canary response",
                4,
                2));

        var response = await CompleteAsync(
            Provider(server.Client, new MutableCredentialStore(CredentialValue)));

        Assert.AreEqual(AssistantResponseStatus.Completed, response.Status);
        Assert.IsTrue(observedTags.Any(
            tag => tag.Key == "result.class"
                && string.Equals(tag.Value as string, "completed", StringComparison.Ordinal)));
        foreach (var tag in observedTags)
        {
            Assert.AreEqual("result.class", tag.Key);
        }
        var serializedTags = JsonSerializer.Serialize(observedTags);
        Assert.DoesNotContain(CredentialValue, serializedTags, StringComparison.Ordinal);
        Assert.DoesNotContain("canary", serializedTags, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("conformance-model", serializedTags, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task CredentialRotationAndRevocationApplyWithoutRestart()
    {
        var expectedCredential = "first-credential";
        await using var server = await ConformanceServer.StartAsync(async (context, call) =>
        {
            await AssertRequestAsync(context, expectedCredential);
            await WriteCompletionAsync(
                context,
                "open-loops_propose-task",
                $$"""{"title":"Call {{call}}"}""",
                null,
                2,
                1);
        });
        var store = new MutableCredentialStore(expectedCredential);
        var provider = Provider(server.Client, store);

        Assert.AreEqual(AssistantResponseStatus.Completed, (await CompleteAsync(provider)).Status);
        expectedCredential = "rotated-credential";
        store.Rotate(expectedCredential);
        Assert.AreEqual(AssistantResponseStatus.Completed, (await CompleteAsync(provider)).Status);

        store.Revoke();
        var revoked = await CompleteAsync(provider);

        Assert.AreEqual("credential-revoked", revoked.Failure?.Code);
        Assert.AreEqual(2, server.CallCount);
    }

    [TestMethod]
    public async Task ExternalEndpointStopsBeforeCallWithoutNumericBudgetEnvelope()
    {
        await using var server = await ConformanceServer.StartAsync((context, _) =>
            WriteCompletionAsync(
                context,
                "open-loops_propose-task",
                """{"title":"Must not run"}""",
                null,
                1,
                1));
        var endpoint = new Uri("https://provider.example/v1");
        var provider = Provider(
            server.Client,
            new MutableCredentialStore(CredentialValue),
            Profile() with { Endpoint = endpoint },
            Policy(endpoint, approvedExternalTotalUnits: 0));

        var response = await CompleteAsync(provider);

        Assert.AreEqual("budget-exhausted", response.Failure?.Code);
        Assert.AreEqual(0, server.CallCount);
    }

    [TestMethod]
    public async Task ApprovedHttpsEndpointCanUseBoundedEnvelope()
    {
        await using var server = await ConformanceServer.StartAsync((context, _) =>
            WriteCompletionAsync(
                context,
                "open-loops_propose-task",
                """{"title":"Approved call"}""",
                null,
                3,
                1));
        var endpoint = new Uri("https://provider.example/v1");
        var profile = Profile() with { Endpoint = endpoint };
        var provider = Provider(
            server.Client,
            new MutableCredentialStore(CredentialValue),
            profile,
            Policy(
                endpoint,
                approvedExternalTotalUnits:
                    profile.MaximumInputUnits!.Value + profile.MaximumOutputUnits!.Value));

        var response = await CompleteAsync(provider);

        Assert.IsTrue(
            response.Status == AssistantResponseStatus.Completed,
            response.Failure?.Code);
        Assert.AreEqual(1, server.CallCount);
    }

    [TestMethod]
    public async Task DeterministicProviderRemainsOfflineFallback()
    {
        var provider = OpenLoopsSkill.CreateDeterministicProvider();
        await using var session = await provider.CreateSessionAsync(
            SessionRequest(),
            CancellationToken.None);

        var response = await session.CompleteAsync(Request(), CancellationToken.None);

        Assert.AreEqual(AssistantResponseStatus.Completed, response.Status);
        Assert.AreEqual("deterministic", response.Usage.Provider);
        Assert.ContainsSingle(response.ToolCalls);
    }

    [TestMethod]
    public void OpenLoopsSelectionResolvesConfiguredByokAdapter()
    {
        var configuration = CreateByokConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAndrejaOpenLoops(
            configuration,
            new TestEnvironment { EnvironmentName = Environments.Development });
        using var serviceProvider = services.BuildServiceProvider();

        var provider = serviceProvider.GetRequiredService<IAssistantProvider>();

        Assert.IsInstanceOfType<OpenAiCompatibleAssistantAdapter>(provider);
    }

    [TestMethod]
    [DataRow("Endpoint", "")]
    [DataRow("Endpoint", "not a uri")]
    [DataRow("Endpoint", "https://user@provider.example/v1")]
    [DataRow("Endpoint", "https://provider.example/v1#fragment")]
    [DataRow("Endpoint", "http://provider.example/v1")]
    [DataRow("AllowedEndpoints:0", "")]
    [DataRow("AllowedEndpoints:0", "not a uri")]
    [DataRow("AllowedEndpoints:0", "https://other.example/v1")]
    [DataRow("Model", "")]
    [DataRow("CredentialHandle", "")]
    [DataRow("CredentialHandle", "raw-secret-value")]
    [DataRow("ApprovedExternalTotalUnits", "-1")]
    [DataRow("ApprovedExternalTotalUnits", "1")]
    [DataRow("MaximumInputUnits", "0")]
    public void InvalidByokOptionsFailThroughStableOptionsValidation(
        string option,
        string value)
    {
        var configuration = CreateByokConfiguration(option, value);
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddAndrejaOpenLoops(
            configuration,
            new TestEnvironment { EnvironmentName = Environments.Development });
        using var serviceProvider = services.BuildServiceProvider();
        var exception = Assert.ThrowsExactly<OptionsValidationException>(
            () => serviceProvider.GetRequiredService<IAssistantProvider>());
        Assert.Contains(
            OpenLoopsServiceCollectionExtensions.OpenAiValidationMessage,
            exception.Failures);
        Assert.IsNull(exception.InnerException);
    }

    [TestMethod]
    public void ProductionByokHandlerKeepsPlatformTlsValidationAndRedirectsDisabled()
    {
        using var handler = OpenLoopsServiceCollectionExtensions.CreateOpenAiHandler(Profile());

        Assert.IsNull(handler.SslOptions.RemoteCertificateValidationCallback);
        Assert.IsFalse(handler.AllowAutoRedirect);
        Assert.AreEqual(DecompressionMethods.None, handler.AutomaticDecompression);
    }

    [TestMethod]
    public async Task FileCredentialStoreReadsRotationAndEmptyFileAsRevocation()
    {
        var path = Path.Join(
            AppContext.BaseDirectory,
            $"assistant-credential-{Guid.CreateVersion7():N}");
        try
        {
            await WriteReadOnlySecretAsync(path, "first-file-secret");
            var store = new FileAssistantCredentialStore(
                new Dictionary<string, string>
                {
                    ["credential://assistant/file-test"] = path,
                });

            using (var first = await store.ResolveAsync(
                "credential://assistant/file-test",
                CancellationToken.None))
            {
                Assert.AreEqual("first-file-secret", new string(first!.Value));
            }

            MakeWritable(path);
            await WriteReadOnlySecretAsync(path, "rotated-file-secret");
            using (var rotated = await store.ResolveAsync(
                "credential://assistant/file-test",
                CancellationToken.None))
            {
                Assert.AreEqual("rotated-file-secret", new string(rotated!.Value));
            }

            MakeWritable(path);
            await WriteReadOnlySecretAsync(path, string.Empty);
            Assert.IsNull(await store.ResolveAsync(
                "credential://assistant/file-test",
                CancellationToken.None));

            MakeWritable(path);
            File.Delete(path);
            await Assert.ThrowsExactlyAsync<FileNotFoundException>(async () =>
                await store.ResolveAsync(
                    "credential://assistant/file-test",
                    CancellationToken.None));
        }
        finally
        {
            if (File.Exists(path))
            {
                MakeWritable(path);
                File.Delete(path);
            }
        }
    }

    [TestMethod]
    public async Task CredentialFileFailuresAreContentFreeTypedFailures()
    {
        var missingPath = Path.Join(
            AppContext.BaseDirectory,
            $"missing-assistant-credential-{Guid.CreateVersion7():N}");
        var missingStore = Store(missingPath);
        await AssertCredentialUnavailableAsync(
            missingStore,
            missingPath,
            "missing-secret-canary");

        var writablePath = Path.Join(
            AppContext.BaseDirectory,
            $"writable-assistant-credential-{Guid.CreateVersion7():N}");
        var oversizedPath = Path.Join(
            AppContext.BaseDirectory,
            $"oversized-assistant-credential-{Guid.CreateVersion7():N}");
        var invalidUtf8Path = Path.Join(
            AppContext.BaseDirectory,
            $"invalid-utf8-assistant-credential-{Guid.CreateVersion7():N}");
        try
        {
            await File.WriteAllTextAsync(writablePath, "writable-secret-canary");
            MakeWritable(writablePath);
            await AssertCredentialUnavailableAsync(
                Store(writablePath),
                writablePath,
                "writable-secret-canary");

            await WriteReadOnlyBytesAsync(
                oversizedPath,
                Enumerable.Repeat((byte)'x', 4097).ToArray());
            await AssertCredentialUnavailableAsync(
                Store(oversizedPath),
                oversizedPath,
                new string('x', 32));

            byte[] invalidUtf8 = [0xC3, 0x28];
            await WriteReadOnlyBytesAsync(invalidUtf8Path, invalidUtf8);
            var invalidStore = Store(invalidUtf8Path);
            var invalidException = await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
                await invalidStore.ResolveAsync(
                    "credential://assistant/conformance",
                    CancellationToken.None));
            Assert.IsInstanceOfType<DecoderFallbackException>(invalidException.InnerException);
            await AssertCredentialUnavailableAsync(
                invalidStore,
                invalidUtf8Path,
                Convert.ToHexString(invalidUtf8));
        }
        finally
        {
            DeleteCredentialFile(writablePath);
            DeleteCredentialFile(oversizedPath);
            DeleteCredentialFile(invalidUtf8Path);
        }
    }

    [TestMethod]
    public async Task AccessDeniedIsTypedButProgrammerFailureIsNotCaught()
    {
        const string deniedDetail = "denied-path-and-secret-canary";
        await AssertCredentialUnavailableAsync(
            new ThrowingCredentialStore(new UnauthorizedAccessException(deniedDetail)),
            deniedDetail);

        var programmerFailure = new InvalidOperationException("programmer-failure-canary");
        using var client = CreateNeverSendClient();
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            CompleteAsync(
                Provider(
                    client,
                    new ThrowingCredentialStore(programmerFailure))));
        Assert.AreSame(programmerFailure, exception);
    }

    [TestMethod]
    public void AssistantCredentialDisposalZeroesObservableBuffer()
    {
        var credential = new AssistantCredential("buffer-secret-canary");
        var field = typeof(AssistantCredential).GetField(
            "value",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var buffer = Assert.IsInstanceOfType<char[]>(field!.GetValue(credential));

        credential.Dispose();

        foreach (var character in buffer)
        {
            Assert.AreEqual('\0', character);
        }
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = credential.Value.Length);
    }

    private static IConfiguration CreateByokConfiguration(
        string? overrideOption = null,
        string? overrideValue = null)
    {
        var prefix = $"{OpenLoopsOptions.SectionName}:OpenAiCompatible";
        var values = new Dictionary<string, string?>
        {
            [$"{AndrejaOperationsOptions.SectionName}:Database:Enabled"] = "false",
            [$"{OpenLoopsOptions.SectionName}:Enabled"] = "true",
            [$"{OpenLoopsOptions.SectionName}:PublicOrigin"] = "https://localhost:5001",
            [$"{OpenLoopsOptions.SectionName}:AssistantProvider"] = "openai-compatible",
            [$"{prefix}:Endpoint"] = "https://provider.example/v1",
            [$"{prefix}:AllowedEndpoints:0"] = "https://provider.example/v1",
            [$"{prefix}:Model"] = "conformance-model",
            [$"{prefix}:CredentialHandle"] = "credential://assistant/conformance",
            [$"{prefix}:CredentialFile"] =
                Path.Join(AppContext.BaseDirectory, "not-present-secret"),
            [$"{prefix}:ProviderDisclosure"] =
                "The selected provider receives submitted task content.",
            [$"{prefix}:RetentionDisclosure"] =
                "The selected provider retention policy was reviewed.",
            [$"{prefix}:MaximumInputUnits"] = "10000",
            [$"{prefix}:MaximumOutputUnits"] = "2000",
            [$"{prefix}:ApprovedExternalTotalUnits"] = "0",
        };
        if (overrideOption is not null)
        {
            values[$"{prefix}:{overrideOption}"] = overrideValue;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static FileAssistantCredentialStore Store(string path) =>
        new(
            new Dictionary<string, string>
            {
                ["credential://assistant/conformance"] = path,
            });

    private static async Task AssertCredentialUnavailableAsync(
        IAssistantCredentialStore store,
        params string[] forbiddenValues)
    {
        using var client = CreateNeverSendClient();
        var response = await CompleteAsync(Provider(client, store));

        Assert.AreEqual(AssistantResponseStatus.Failed, response.Status);
        Assert.AreEqual("credential-unavailable", response.Failure?.Code);
        Assert.IsFalse(response.Failure?.IsTransient);
        var serialized = JsonSerializer.Serialize(response);
        foreach (var forbidden in forbiddenValues)
        {
            Assert.DoesNotContain(forbidden, serialized, StringComparison.Ordinal);
        }
    }

    private static HttpClient CreateNeverSendClient() =>
        new(new NeverSendHandler())
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

    private static OpenAiCompatibleAssistantAdapter Provider(
        HttpClient client,
        IAssistantCredentialStore credentialStore,
        AssistantProviderProfile? profile = null,
        OpenAiCompatibleTransportPolicy? policy = null)
    {
        var selectedProfile = profile ?? Profile();
        return new(
            selectedProfile,
            new OpenAiCompatibleTransport(
                client,
                credentialStore,
                policy ?? Policy(selectedProfile.Endpoint)));
    }

    private static async Task<AssistantResponse> CompleteAsync(
        OpenAiCompatibleAssistantAdapter provider)
    {
        await using var session = await provider.CreateSessionAsync(
            SessionRequest(),
            CancellationToken.None);
        return await session.CompleteAsync(Request(), CancellationToken.None);
    }

    private static AssistantProviderProfile Profile() =>
        new(
            new Uri("http://localhost/v1"),
            "conformance-model",
            "credential://assistant/conformance",
            TimeSpan.FromSeconds(10),
            "The selected provider receives submitted task content.",
            "The conformance server retains no content.",
            100_000,
            2_000);

    private static OpenAiCompatibleTransportPolicy Policy(
        Uri? endpoint = null,
        int maximumResponseBodyBytes = 64 * 1024,
        long approvedExternalTotalUnits = 0) =>
        new(
            [endpoint ?? new Uri("http://localhost/v1")],
            maximumResponseBodyBytes,
            2,
            TimeSpan.FromMilliseconds(1),
            approvedExternalTotalUnits);

    private static AssistantSessionRequest SessionRequest() =>
        new(
            Guid.CreateVersion7(),
            new(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                OpenLoopsPolicy.Purpose),
            [Tool()]);

    private static AssistantRequest Request() =>
        new(
            Guid.CreateVersion7(),
            "Prepare a task proposal.",
            [OpenLoopsSkill.ProposeTaskTool]);

    private static ToolDefinition Tool() =>
        new(
            OpenLoopsSkill.ProposeTaskTool,
            "1.0.0",
            "Propose a task.",
            OpenLoopsPolicy.ProposeOperation,
            OpenLoopsPolicy.TaskDataClass,
            DisclosureLevel.Summary,
            [
                new("title", ToolValueKind.Text, true),
                new("details", ToolValueKind.Text, false),
                new("dueAt", ToolValueKind.Text, false),
            ],
            [OpenLoopsPolicy.ProposeCapability],
            [OpenLoopsPolicy.Purpose]);

    private static async Task AssertRequestAsync(
        HttpContext context,
        string expectedCredential)
    {
        Assert.AreEqual(
            $"Bearer {expectedCredential}",
            context.Request.Headers.Authorization.ToString());
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        Assert.DoesNotContain(expectedCredential, body, StringComparison.Ordinal);
        var request = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.AreEqual("conformance-model", request.GetProperty("model").GetString());
        var function = request
            .GetProperty("tools")[0]
            .GetProperty("function");
        Assert.AreEqual(
            "open-loops_propose-task",
            function.GetProperty("name").GetString());
        Assert.IsFalse(
            function.GetProperty("parameters").GetProperty("additionalProperties").GetBoolean());
    }

    private static Task WriteCompletionAsync(
        HttpContext context,
        string toolName,
        string arguments,
        string? content,
        long inputUnits,
        long outputUnits) =>
        context.Response.WriteAsJsonAsync(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content,
                        tool_calls = new[]
                        {
                            new
                            {
                                type = "function",
                                function = new
                                {
                                    name = toolName,
                                    arguments,
                                },
                            },
                        },
                    },
                },
            },
            usage = new
            {
                prompt_tokens = inputUnits,
                completion_tokens = outputUnits,
                total_tokens = inputUnits + outputUnits,
            },
        });

    private static async Task WriteReadOnlySecretAsync(string path, string value)
    {
        await File.WriteAllTextAsync(path, value);
        MakeReadOnly(path);
    }

    private static async Task WriteReadOnlyBytesAsync(string path, byte[] value)
    {
        await File.WriteAllBytesAsync(path, value);
        MakeReadOnly(path);
    }

    private static void MakeReadOnly(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }
        else
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead);
        }
    }

    private static void DeleteCredentialFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        MakeWritable(path);
        File.Delete(path);
    }

    private static void MakeWritable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }
        else
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private sealed class MutableCredentialStore(string? credential)
        : IAssistantCredentialStore
    {
        private string? credential = credential;

        public ValueTask<AssistantCredential?> ResolveAsync(
            string credentialHandle,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = Volatile.Read(ref credential);
            return ValueTask.FromResult(
                current is null ? null : new AssistantCredential(current));
        }

        public void Rotate(string value) => Volatile.Write(ref credential, value);

        public void Revoke() => Volatile.Write(ref credential, null);
    }

    private sealed class ThrowingCredentialStore(Exception exception)
        : IAssistantCredentialStore
    {
        public ValueTask<AssistantCredential?> ResolveAsync(
            string credentialHandle,
            CancellationToken cancellationToken) =>
            ValueTask.FromException<AssistantCredential?>(exception);
    }

    private sealed class NeverSendHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(
                new InvalidOperationException("A credential failure attempted network I/O."));
    }

    private sealed class ConformanceServer : IAsyncDisposable
    {
        private readonly WebApplication application;
        private int callCount;

        private ConformanceServer(WebApplication application, HttpClient client)
        {
            this.application = application;
            Client = client;
        }

        public HttpClient Client { get; }

        public int CallCount => Volatile.Read(ref callCount);

        public static async Task<ConformanceServer> StartAsync(
            Func<HttpContext, int, Task> handler)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration["AllowedHosts"] = "*";
            var application = builder.Build();
            ConformanceServer? server = null;
            application.MapPost("/v1/chat/completions", async context =>
            {
                var call = Interlocked.Increment(ref server!.callCount);
                await handler(context, call);
            });
            await application.StartAsync();
            var client = application.GetTestClient();
            client.Timeout = Timeout.InfiniteTimeSpan;
            server = new(application, client);
            return server;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await application.DisposeAsync();
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Andreja.UnitTests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = AppContext.BaseDirectory;

        public string EnvironmentName { get; set; } = Environments.Development;

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
