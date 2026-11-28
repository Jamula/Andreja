using Andreja.Adapters.OpenTelemetry;
using Andreja.Adapters.PostgreSql;
using Andreja.AppHost.Hosting;
using Andreja.Modules.Channels;
using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Andreja.Modules.Proposals;
using Andreja.Modules.Skills;
using Andreja.Platform.Contracts.Channels;
using Andreja.Platform.Contracts.Portability;
using Andreja.Platform.Contracts.Sharing;
using Andreja.Platform.Contracts.Skills;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Andreja.UnitTests;

[TestClass]
public sealed class OperationsContractTests
{
    [TestMethod]
    public void TelemetryProcessorRetainsOnlyAllowedAttributes()
    {
        using var activity = new Activity("request").Start();
        activity.SetTag("http.request.method", "GET");
        activity.SetTag("task", "CANARY-TASK-CONTENT");
        activity.SetTag("user.id", "CANARY-USER");
        activity.SetTag("custom.attribute", "CANARY-UNKNOWN");

        new ContentSuppressingActivityProcessor().OnEnd(activity);

        Assert.AreEqual("GET", activity.GetTagItem("http.request.method"));
        Assert.IsNull(activity.GetTagItem("task"));
        Assert.IsNull(activity.GetTagItem("user.id"));
        Assert.IsNull(activity.GetTagItem("custom.attribute"));
    }

    [TestMethod]
    public void TelemetryProcessorAllowsLowCardinalityGenAiDbAndControlPlaneTagsWhileBlockingSensitiveFields()
    {
        long violations = 0;
        long suppressed = 0;
        using var testMeter = new Meter($"Andreja.Operations.Test.{Guid.NewGuid():N}");
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, testMeter))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "andreja_telemetry_policy_violation_total")
            {
                Interlocked.Add(ref violations, measurement);
            }
            else if (instrument.Name == "andreja_telemetry_suppressed_attributes_total")
            {
                Interlocked.Add(ref suppressed, measurement);
            }
        });
        meterListener.Start();

        using var canaryActivity = new Activity("andreja.telemetry.startup-canary").Start();
        canaryActivity.SetTag("andreja.evidence.canary", "synthetic");
        var processor = new ContentSuppressingActivityProcessor(testMeter);
        processor.OnEnd(canaryActivity);
        Assert.IsNull(canaryActivity.GetTagItem("andreja.evidence.canary"));
        Assert.AreEqual(0L, Interlocked.Read(ref violations));
        Assert.IsTrue(Interlocked.Read(ref suppressed) >= 1L);

        using var operationalActivity = new Activity("gen_ai.chat").Start();
        operationalActivity.SetTag("gen_ai.operation.name", "chat");
        operationalActivity.SetTag("gen_ai.provider.name", "local-loopback");
        operationalActivity.SetTag("gen_ai.request.model", "phi-4-mini");
        operationalActivity.SetTag("gen_ai.usage.input_tokens", 42);
        operationalActivity.SetTag("gen_ai.usage.output_tokens", 17);
        operationalActivity.SetTag("db.system.name", "postgresql");
        operationalActivity.SetTag("andreja.operation", "andreja.proposal.create");
        operationalActivity.SetTag("andreja.outcome", "pending");
        operationalActivity.SetTag("gen_ai.input.messages", "SECRET-PROMPT");
        operationalActivity.SetTag("gen_ai.output.messages", "SECRET-COMPLETION");
        operationalActivity.SetTag("gen_ai.tool.call.arguments", "{\"title\":\"SECRET\"}");
        operationalActivity.SetTag("db.statement", "SELECT * FROM open_loops.tasks WHERE Title='SECRET'");
        operationalActivity.SetTag("db.query.text", "SELECT * FROM identity.recovery_codes");
        operationalActivity.SetTag("url.query", "?token=SECRET");

        processor.OnEnd(operationalActivity);

        Assert.AreEqual("chat", operationalActivity.GetTagItem("gen_ai.operation.name"));
        Assert.AreEqual("local-loopback", operationalActivity.GetTagItem("gen_ai.provider.name"));
        Assert.AreEqual("phi-4-mini", operationalActivity.GetTagItem("gen_ai.request.model"));
        Assert.AreEqual(42, operationalActivity.GetTagItem("gen_ai.usage.input_tokens"));
        Assert.AreEqual(17, operationalActivity.GetTagItem("gen_ai.usage.output_tokens"));
        Assert.AreEqual("postgresql", operationalActivity.GetTagItem("db.system.name"));
        Assert.AreEqual("andreja.proposal.create", operationalActivity.GetTagItem("andreja.operation"));
        Assert.IsNull(operationalActivity.GetTagItem("gen_ai.input.messages"));
        Assert.IsNull(operationalActivity.GetTagItem("gen_ai.output.messages"));
        Assert.IsNull(operationalActivity.GetTagItem("gen_ai.tool.call.arguments"));
        Assert.IsNull(operationalActivity.GetTagItem("db.statement"));
        Assert.IsNull(operationalActivity.GetTagItem("db.query.text"));
        Assert.IsNull(operationalActivity.GetTagItem("url.query"));
        Assert.IsTrue(Interlocked.Read(ref violations) >= 6L);
    }

    [TestMethod]
    [DataRow("CaptureContent")]
    [DataRow("EnableSensitiveData")]
    public void TelemetryRegistrationFailsClosedWhenSensitiveCaptureIsEnabled(string propertyName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AndrejaTelemetryOptions.SectionName}:Enabled"] = "true",
                [$"{AndrejaTelemetryOptions.SectionName}:ServiceName"] = "andreja",
                [$"{AndrejaTelemetryOptions.SectionName}:OtlpEndpoint"] = "http://127.0.0.1:4317",
                [$"{AndrejaTelemetryOptions.SectionName}:{propertyName}"] = "true",
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            services.AddAndrejaOpenTelemetry(configuration, "test-instance"));
        Assert.Contains("prohibits content and sensitive-data capture", exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ControlPlaneTelemetryRecordsProposalLifecycleAndDenialsWithoutLeakingContent()
    {
        var recordedActivities = new List<Activity>();
        var recordedCounters = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AndrejaTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                new ContentSuppressingActivityProcessor().OnEnd(activity);
                lock (recordedActivities)
                {
                    recordedActivities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AndrejaTelemetry.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            lock (recordedCounters)
            {
                recordedCounters.Add((instrument.Name, measurement, tags.ToArray()));
            }
        });
        meterListener.Start();

        var telemetry = new OpenTelemetryControlPlaneTelemetry();
        var application = new OpenLoopsTaskApplication(
            new InMemoryOpenLoopsTaskStore(),
            new InMemoryProposalStore(),
            TimeProvider.System,
            telemetry);
        var tenantId = ExecutionContractFixture.TenantId;
        var userId = ExecutionContractFixture.AppUserId;
        var principalId = ExecutionContractFixture.PrincipalId;
        var context = new TenantPrincipalContext(
            new(tenantId),
            new(userId),
            new(principalId),
            OpenLoopsPolicy.Purpose);

        const string canaryTitle = "CANARY-PRIVATE-TITLE-312";
        const string canaryDetails = "CANARY-PRIVATE-DETAILS-312";
        var proposal = await application.ProposeAsync(
            context,
            new(canaryTitle, canaryDetails, null),
            "canary-source");
        var confirmed = await application.ConfirmAsync(
            context,
            proposal.ProposalId,
            proposal.Version,
            "confirm-312");
        var replay = await application.ConfirmAsync(
            context,
            proposal.ProposalId,
            proposal.Version,
            "confirm-312");

        var skillHost = OpenLoopsSkill.CreateHost(application, telemetry);
        var deniedSkill = await skillHost.InvokeAsync(
            new SkillInvocation(
                "undeclared-skill",
                "1.0.0",
                OpenLoopsSkill.ProposeTaskTool,
                tenantId,
                userId,
                principalId,
                OpenLoopsPolicy.Purpose,
                OpenLoopsPolicy.ProposeOperation,
                OpenLoopsPolicy.TaskDataClass,
                DisclosureLevel.Summary,
                OpenLoopsPolicy.ResourceReference,
                new Dictionary<string, JsonElement>(),
                "bad-digest"),
            new SkillExecutionContext(
                tenantId,
                userId,
                principalId,
                OpenLoopsPolicy.Purpose,
                ExecutionContractFixture.Authorization()),
            CancellationToken.None);

        var invalidSkill = await skillHost.InvokeAsync(
            new SkillInvocation(
                OpenLoopsSkill.Manifest.SkillId,
                OpenLoopsSkill.Manifest.Version,
                OpenLoopsSkill.ProposeTaskTool,
                tenantId,
                userId,
                principalId,
                OpenLoopsPolicy.Purpose,
                OpenLoopsPolicy.ProposeOperation,
                OpenLoopsPolicy.TaskDataClass,
                DisclosureLevel.Summary,
                OpenLoopsPolicy.ResourceReference,
                new Dictionary<string, JsonElement>(),
                InMemorySkillHost.ComputeManifestDigest(OpenLoopsSkill.Manifest)),
            new SkillExecutionContext(
                tenantId,
                userId,
                principalId,
                OpenLoopsPolicy.Purpose,
                ExecutionContractFixture.Authorization()),
            CancellationToken.None);

        var channelHost = new InMemoryChannelHost(telemetry);
        var channelManifest = ExecutionContractFixture.ChannelManifest();
        channelHost.Register(
            channelManifest,
            new Dictionary<string, ChannelOperationHandler>(StringComparer.Ordinal)
            {
                [ExecutionContractFixture.ChannelOperationName] = static (_, _, _) =>
                    ValueTask.FromResult(new ChannelResult(ChannelResultStatus.Completed, null, null)),
            });
        var invalidChannel = await channelHost.InvokeAsync(
            new ChannelInvocation(
                channelManifest.ChannelId,
                channelManifest.Version,
                ExecutionContractFixture.ChannelOperationName,
                tenantId,
                userId,
                principalId,
                ExecutionContractFixture.Purpose,
                ExecutionContractFixture.Operation,
                ExecutionContractFixture.DataClass,
                DisclosureLevel.Summary,
                ExecutionContractFixture.Resource,
                new Dictionary<string, JsonElement>(),
                InMemoryChannelHost.ComputeManifestDigest(channelManifest)),
            new ChannelExecutionContext(
                tenantId,
                userId,
                principalId,
                ExecutionContractFixture.Purpose,
                ExecutionContractFixture.Authorization()),
            CancellationToken.None);

        Activity? parentActivity;
        using (var cancelledCts = new CancellationTokenSource())
        {
            await cancelledCts.CancelAsync();
            using var outerSpan = AndrejaTelemetry.ActivitySource.StartActivity("parent.request", ActivityKind.Internal);
            parentActivity = outerSpan;
            var cancelledSkill = await skillHost.InvokeAsync(
                new SkillInvocation(
                    OpenLoopsSkill.Manifest.SkillId,
                    OpenLoopsSkill.Manifest.Version,
                    OpenLoopsSkill.ProposeTaskTool,
                    tenantId,
                    userId,
                    principalId,
                    OpenLoopsPolicy.Purpose,
                    OpenLoopsPolicy.ProposeOperation,
                    OpenLoopsPolicy.TaskDataClass,
                    DisclosureLevel.Summary,
                    OpenLoopsPolicy.ResourceReference,
                    new Dictionary<string, JsonElement>(),
                    InMemorySkillHost.ComputeManifestDigest(OpenLoopsSkill.Manifest)),
                new SkillExecutionContext(
                    tenantId,
                    userId,
                    principalId,
                    OpenLoopsPolicy.Purpose,
                    ExecutionContractFixture.Authorization()),
                cancelledCts.Token);
            var cancelledChannel = await channelHost.InvokeAsync(
                new ChannelInvocation(
                    channelManifest.ChannelId,
                    channelManifest.Version,
                    ExecutionContractFixture.ChannelOperationName,
                    tenantId,
                    userId,
                    principalId,
                    ExecutionContractFixture.Purpose,
                    ExecutionContractFixture.Operation,
                    ExecutionContractFixture.DataClass,
                    DisclosureLevel.Summary,
                    ExecutionContractFixture.Resource,
                    new Dictionary<string, JsonElement>(),
                    InMemoryChannelHost.ComputeManifestDigest(channelManifest)),
                new ChannelExecutionContext(
                    tenantId,
                    userId,
                    principalId,
                    ExecutionContractFixture.Purpose,
                    ExecutionContractFixture.Authorization()),
                cancelledCts.Token);
            Assert.AreEqual(SkillResultStatus.Cancelled, cancelledSkill.Status);
            Assert.AreEqual(ChannelResultStatus.Cancelled, cancelledChannel.Status);
        }

        Assert.AreEqual(Andreja.Platform.Contracts.Proposals.ProposalTransitionOutcome.Applied, confirmed.Outcome);
        Assert.AreEqual(Andreja.Platform.Contracts.Proposals.ProposalTransitionOutcome.IdempotentReplay, replay.Outcome);
        Assert.AreEqual(SkillResultStatus.Denied, deniedSkill.Status);
        Assert.AreEqual(SkillResultStatus.Invalid, invalidSkill.Status);
        Assert.AreEqual(ChannelResultStatus.Invalid, invalidChannel.Status);
        Assert.IsNull(parentActivity?.GetTagItem("andreja.outcome"));

        lock (recordedCounters)
        {
            Assert.IsTrue(recordedCounters.Any(c => c.Name == "andreja_proposals_created_total"));
            Assert.IsTrue(recordedCounters.Any(c => c.Name == "andreja_proposals_confirmed_total"));
            Assert.IsTrue(recordedCounters.Any(c => c.Name == "andreja_proposals_idempotent_replay_total"));
            Assert.IsTrue(recordedCounters.Any(c =>
                c.Name == "andreja_execution_denied_total"
                && c.Tags.Any(t => t.Key == "andreja.denial.code" && Equals(t.Value, "skill-not-declared"))));
            Assert.IsTrue(recordedCounters.Any(c =>
                c.Name == "andreja_host_invocations_total"
                && c.Tags.Any(t => t.Key == "andreja.artifact.kind" && Equals(t.Value, "skill"))
                && c.Tags.Any(t => t.Key == "andreja.outcome" && Equals(t.Value, "invalid"))));
            Assert.IsTrue(recordedCounters.Any(c =>
                c.Name == "andreja_host_invocations_total"
                && c.Tags.Any(t => t.Key == "andreja.artifact.kind" && Equals(t.Value, "channel"))
                && c.Tags.Any(t => t.Key == "andreja.outcome" && Equals(t.Value, "invalid"))));

            foreach (var text in recordedCounters
                .SelectMany(static counter => counter.Tags)
                .Select(static tag => tag.Value?.ToString() ?? string.Empty))
            {
                Assert.DoesNotContain(canaryTitle, text, StringComparison.Ordinal);
                Assert.DoesNotContain(canaryDetails, text, StringComparison.Ordinal);
                Assert.DoesNotContain(tenantId.ToString("D"), text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(userId.ToString("D"), text, StringComparison.OrdinalIgnoreCase);
            }
        }

        lock (recordedActivities)
        {
            Assert.IsTrue(recordedActivities.Any(a => a.OperationName == "andreja.proposal.create"));
            Assert.IsTrue(recordedActivities.Any(a => a.OperationName == "andreja.proposal.confirm"));
            Assert.IsTrue(recordedActivities.Any(a =>
                a.OperationName == "andreja.skill.invoke"
                && Equals(a.GetTagItem("andreja.outcome"), "invalid")));
            Assert.IsTrue(recordedActivities.Any(a =>
                a.OperationName == "andreja.skill.invoke"
                && Equals(a.GetTagItem("andreja.outcome"), "cancelled")));
            Assert.IsTrue(recordedActivities.Any(a =>
                a.OperationName == "andreja.channel.invoke"
                && Equals(a.GetTagItem("andreja.outcome"), "invalid")));
            Assert.IsTrue(recordedActivities.Any(a =>
                a.OperationName == "andreja.channel.invoke"
                && Equals(a.GetTagItem("andreja.outcome"), "cancelled")));
            foreach (var text in recordedActivities
                .SelectMany(static activity => activity.TagObjects)
                .Select(static tag => tag.Value?.ToString() ?? string.Empty))
            {
                Assert.DoesNotContain(canaryTitle, text, StringComparison.Ordinal);
                Assert.DoesNotContain(canaryDetails, text, StringComparison.Ordinal);
                Assert.DoesNotContain(tenantId.ToString("D"), text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(userId.ToString("D"), text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [TestMethod]
    public void DatabaseTracingPipelineExportsSanitizedQueriesWithoutViolationsAndDropsExceptionEventSpans()
    {
        long violations = 0;
        long suppressed = 0;
        using var testMeter = new Meter($"Andreja.Operations.Test.{Guid.NewGuid():N}");
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, testMeter))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "andreja_telemetry_policy_violation_total")
            {
                Interlocked.Add(ref violations, measurement);
            }
            else if (instrument.Name == "andreja_telemetry_suppressed_attributes_total")
            {
                Interlocked.Add(ref suppressed, measurement);
            }
        });
        meterListener.Start();

        var appSourceName = $"Test.Application.{Guid.NewGuid():N}";
        using var exporter = new CapturingActivityExporter();
        using var exportProcessor = new SimpleActivityExportProcessor(exporter);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(NpgsqlSpanSanitizingProcessor.NpgsqlActivitySourceName, appSourceName)
            .AddProcessor(new NpgsqlSpanSanitizingProcessor())
            .AddProcessor(new ContentSuppressingActivityProcessor(testMeter))
            .AddProcessor(exportProcessor)
            .Build();
        using var npgsqlSource = new ActivitySource(NpgsqlSpanSanitizingProcessor.NpgsqlActivitySourceName);
        using var appSource = new ActivitySource(appSourceName);

        using (var querySpan = npgsqlSource.StartActivity("SELECT open_loops.tasks", ActivityKind.Client))
        {
            Assert.IsNotNull(querySpan);
            querySpan.SetTag("db.system.name", "postgresql");
            querySpan.SetTag("db.operation.name", "SELECT");
            querySpan.SetTag("db.query.text", "SELECT * FROM open_loops.tasks WHERE Title='CANARY-SQL'");
            querySpan.AddEvent(new ActivityEvent("received-first-response"));
        }

        Assert.AreEqual(0L, Interlocked.Read(ref violations));
        var exportedQuery = Assert.ContainsSingle(exporter.Exported);
        Assert.AreEqual("postgresql.operation", exportedQuery.DisplayName);
        Assert.AreEqual("postgresql", exportedQuery.GetTagItem("db.system.name"));
        Assert.AreEqual("SELECT", exportedQuery.GetTagItem("db.operation.name"));
        Assert.IsNull(exportedQuery.GetTagItem("db.query.text"));

        Activity? capturedFailedSpan;
        using (var failedDbSpan = npgsqlSource.StartActivity("INSERT open_loops.tasks", ActivityKind.Client))
        {
            Assert.IsNotNull(failedDbSpan);
            failedDbSpan.SetTag("db.system.name", "postgresql");
            failedDbSpan.SetTag("db.response.status_code", "23505");
            failedDbSpan.SetStatus(ActivityStatusCode.Error, "DETAIL: Key (Title)=(CANARY-SQL-ERR) already exists.");
            failedDbSpan.AddException(new InvalidOperationException("Password=CANARY-DB-SECRET"));
            Assert.IsTrue(failedDbSpan.Recorded);
            capturedFailedSpan = failedDbSpan;
        }

        Assert.IsFalse(capturedFailedSpan.Recorded);
        Assert.IsTrue(string.IsNullOrEmpty(capturedFailedSpan.StatusDescription));
        Assert.AreEqual(0L, Interlocked.Read(ref violations));
        Assert.HasCount(1, exporter.Exported);

        using (var appExceptionSpan = appSource.StartActivity("app.unfiltered-exception", ActivityKind.Internal))
        {
            Assert.IsNotNull(appExceptionSpan);
            appExceptionSpan.AddException(new InvalidOperationException("Token=CANARY-APP-SECRET"));
        }

        Assert.AreEqual(1L, Interlocked.Read(ref violations));
        Assert.HasCount(1, exporter.Exported);
        Assert.IsTrue(Interlocked.Read(ref suppressed) >= 4L);
    }

    [TestMethod]
    public void NpgsqlMetricViewStripsConnectionPoolAndDatabaseMetadataBeforeExport()
    {
        using var metricExporter = new CapturingMetricExporter();
        using var metricReader = new BaseExportingMetricReader(metricExporter);
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(NpgsqlSpanSanitizingProcessor.NpgsqlActivitySourceName)
            .AddView(OpenTelemetryServiceCollectionExtensions.ConfigureMetricView)
            .AddReader(metricReader)
            .Build();
        using var npgsqlMeter = new Meter(NpgsqlSpanSanitizingProcessor.NpgsqlActivitySourceName);
        var duration = npgsqlMeter.CreateHistogram<double>("db.client.operation.duration");

        duration.Record(
            0.015,
            new KeyValuePair<string, object?>("db.system.name", "postgresql"),
            new KeyValuePair<string, object?>("db.operation.name", "SELECT"),
            new KeyValuePair<string, object?>(
                "db.client.connection.pool.name",
                "Host=db.internal.canary;Database=andreja_secret_db;Username=admin_canary"),
            new KeyValuePair<string, object?>("db.namespace", "andreja_secret_db"),
            new KeyValuePair<string, object?>("server.address", "db.internal.canary"));
        Assert.IsTrue(meterProvider.ForceFlush());

        var exportedTags = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var metric in metricExporter.Exported)
        {
            foreach (var point in metric.GetMetricPoints())
            {
                foreach (var tag in point.Tags)
                {
                    exportedTags[tag.Key] = tag.Value?.ToString() ?? string.Empty;
                }
            }
        }

        Assert.AreEqual("postgresql", exportedTags["db.system.name"]);
        Assert.AreEqual("SELECT", exportedTags["db.operation.name"]);
        Assert.IsFalse(exportedTags.ContainsKey("db.client.connection.pool.name"));
        Assert.IsFalse(exportedTags.ContainsKey("db.namespace"));
        Assert.IsFalse(exportedTags.ContainsKey("server.address"));
        var serialized = JsonSerializer.Serialize(exportedTags);
        Assert.DoesNotContain("canary", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("andreja_secret_db", serialized, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ExportVerifierAcceptsCompleteCleanArchive()
    {
        var files = CreateArtifacts();
        var manifest = CreateManifest(files);

        var result = await ApplicationExportVerifier.ValidateAsync(
            manifest,
            new CleanInstanceProbe(isClean: true),
            (path, _) => ValueTask.FromResult<Stream>(OpenArtifact(files, path)));

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.TargetIsClean);
        Assert.IsEmpty(result.Errors);
    }

    [TestMethod]
    public async Task ExportVerifierRejectsDirtyTargetAndMissingExclusions()
    {
        var files = CreateArtifacts();
        var complete = CreateManifest(files);
        var manifest = complete with { Exclusions = [] };

        var result = await ApplicationExportVerifier.ValidateAsync(
            manifest,
            new CleanInstanceProbe(isClean: false),
            (path, _) => ValueTask.FromResult<Stream>(OpenArtifact(files, path)));

        Assert.IsFalse(result.IsValid);
        Assert.Contains("target-not-clean", result.Errors);
        Assert.Contains("missing-required-exclusion", result.Errors);
    }

    [TestMethod]
    public async Task ExportVerifierRejectsTamperingAndNonPortablePaths()
    {
        var files = CreateArtifacts();
        files["records.ndjson"][0] ^= 0xff;
        files["..\\audit.ndjson"] = files["audit.ndjson"];
        var complete = CreateManifest(CreateArtifacts());
        var artifacts = complete.Artifacts
            .Where(artifact => artifact.DataArea != PortableDataArea.Audit)
            .Append(complete.Artifacts.Single(
                artifact => artifact.DataArea == PortableDataArea.Audit) with
            { Path = "..\\audit.ndjson" })
            .ToArray();
        var manifest = complete with { Artifacts = artifacts };

        var result = await ApplicationExportVerifier.ValidateAsync(
            manifest,
            new CleanInstanceProbe(isClean: true),
            (path, _) => ValueTask.FromResult<Stream>(OpenArtifact(files, path)));

        Assert.IsFalse(result.IsValid);
        Assert.Contains("checksum-mismatch:records.ndjson", result.Errors);
        Assert.Contains("invalid-artifact-path:..\\audit.ndjson", result.Errors);
    }

    [TestMethod]
    public async Task ExportVerifierRejectsUnsupportedSemanticContractVersion()
    {
        var files = CreateArtifacts();
        var complete = CreateManifest(files);
        var semantic = complete.Artifacts.Single(
            artifact => artifact.DataArea == PortableDataArea.Semantic);
        var manifest = complete with
        {
            Artifacts = complete.Artifacts
                .Where(artifact => artifact.DataArea != PortableDataArea.Semantic)
                .Append(semantic with { ContractVersion = "99" })
                .ToArray(),
        };

        var result = await ApplicationExportVerifier.ValidateAsync(
            manifest,
            new CleanInstanceProbe(isClean: true),
            (path, _) => ValueTask.FromResult<Stream>(OpenArtifact(files, path)));

        Assert.IsFalse(result.IsValid);
        Assert.Contains(
            $"unsupported-artifact-contract-version:{semantic.Path}",
            result.Errors);
    }

    [TestMethod]
    public async Task ExportVerifierRejectsDuplicateDataArea()
    {
        var files = CreateArtifacts();
        var complete = CreateManifest(files);
        var manifest = complete with
        {
            Artifacts =
            [
                .. complete.Artifacts,
                complete.Artifacts[0] with { Path = "duplicate-records.ndjson" },
            ],
        };
        files["duplicate-records.ndjson"] = files[complete.Artifacts[0].Path];

        var result = await ApplicationExportVerifier.ValidateAsync(
            manifest,
            new CleanInstanceProbe(isClean: true),
            (path, _) => ValueTask.FromResult<Stream>(OpenArtifact(files, path)));

        Assert.IsFalse(result.IsValid);
        Assert.Contains("duplicate-data-area", result.Errors);
    }

    [TestMethod]
    public void PortabilityZipWriterStoresOverCompressibleEntriesWithinReaderLimit()
    {
        var content = Enumerable.Repeat((byte)'A', 64 * 1024).ToArray();
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["records.ndjson"] = content,
        };
        var zipBytes = PostgreSqlApplicationPortability.CreateZip(files);
        var repeatedZipBytes = PostgreSqlApplicationPortability.CreateZip(files);
        try
        {
            CollectionAssert.AreEqual(zipBytes, repeatedZipBytes);
            using var input = new MemoryStream(zipBytes, writable: false);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read);
            var entry = Assert.ContainsSingle(archive.Entries);
            Assert.IsTrue(entry.Length <= entry.CompressedLength * 100);
            using var expanded = new MemoryStream();
            using (var stream = entry.Open())
            {
                stream.CopyTo(expanded);
            }
            CollectionAssert.AreEqual(content, expanded.ToArray());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(content);
            CryptographicOperations.ZeroMemory(zipBytes);
            CryptographicOperations.ZeroMemory(repeatedZipBytes);
        }
    }

    [TestMethod]
    public void PortabilityZipWriterRejectsReaderExpansionLimits()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PostgreSqlApplicationPortability.ValidateExpandedArchiveLengths(
                [PostgreSqlApplicationPortability.MaximumArtifactBytes + 1]));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PostgreSqlApplicationPortability.ValidateExpandedArchiveLengths(
                Enumerable.Repeat(
                    PostgreSqlApplicationPortability.MaximumArtifactBytes,
                    5)));
    }

    [TestMethod]
    [DataRow("artifacts", "null")]
    [DataRow("artifacts", "[null]")]
    [DataRow("exclusions", "[null]")]
    [DataRow("reauthorization", "[null]")]
    public void PortabilityManifestRejectsNullRequiredValues(
        string property,
        string value)
    {
        var exportId = Guid.NewGuid();
        var tenantReference = new string('a', 64);
        var manifest = $$"""
            {"applicationVersion":"1","archiveVersion":"1","artifacts":[],"createdAtUtc":"2026-08-25T00:00:00+00:00","exclusions":[],"exportId":"{{exportId:D}}","reauthorization":[],"schemaVersion":"1.0.0","tenantReference":"{{tenantReference}}"}
            """.Trim();
        manifest = manifest.Replace(
            $"\"{property}\":[]",
            $"\"{property}\":{value}",
            StringComparison.Ordinal);

        var exception = Assert.ThrowsExactly<InvalidDataException>(() =>
            PostgreSqlApplicationPortability.ParseManifest(
                PostgreSqlApplicationPortability.Canonicalize(
                    Encoding.UTF8.GetBytes(manifest))));

        Assert.Contains("null required value", exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task PortabilityArchiveReaderEnforcesBoundOnOpenedHandle()
    {
        var path = Path.Join(
            AppContext.BaseDirectory,
            $"bounded-archive-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(path, new byte[1025]);
        try
        {
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                PostgreSqlApplicationPortability.ReadBoundedFileAsync(
                    path,
                    1024,
                    CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ProductionWebApplicationFactoryDoesNotExposePortabilityMutation()
    {
        using var factory = new ProductionWebApplicationFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        foreach (var path in new[]
        {
            "/api/portability/export",
            "/api/portability/import",
            "/operations/application-export",
        })
        {
            using var response = await client.GetAsync(path);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [TestMethod]
    public async Task ReadinessRequiresWritableKeyState()
    {
        var path = Path.Join(Path.GetTempPath(), $"andreja-{Guid.NewGuid():N}");
        try
        {
            var options = Options.Create(new AndrejaOperationsOptions
            {
                DataProtectionKeysPath = path,
                Database = new DatabaseReadinessOptions { Enabled = false },
            });
            var telemetryOptions = Options.Create(new AndrejaTelemetryOptions { Enabled = false });
            var check = new OperationalReadinessHealthCheck(options, telemetryOptions);

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.AreEqual(HealthStatus.Healthy, result.Status);
            Assert.IsTrue(Directory.Exists(path));
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [TestMethod]
    public void DeploymentContractsArePinnedAndContentSafe()
    {
        var root = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Join(root, "compose.yaml"));
        var dockerfile = File.ReadAllText(Path.Join(root, "Dockerfile"));
        var collector = File.ReadAllText(Path.Join(root, "deploy", "otel-collector.yaml"));
        var collectorDashboard = File.ReadAllText(
            Path.Join(root, "deploy", "otel-collector.dashboard.yaml"));
        var dashboardCompose = File.ReadAllText(
            Path.Join(root, "deploy", "compose.dashboard.yaml"));
        var maintenance = File.ReadAllText(
            Path.Join(root, "deploy", "compose.maintenance.yaml"));
        var migration = File.ReadAllText(
            Path.Join(root, "deploy", "compose.migration.yaml"));
        var schema = File.ReadAllText(
            Path.Join(root, "docs", "operations", "application-export-v1.schema.json"));
        var portabilityCli = File.ReadAllText(
            Path.Join(root, "src", "Andreja.Portability.Cli", "Program.cs"));

        Assert.Contains("image: ${ANDREJA_IMAGE:?", compose, StringComparison.Ordinal);
        Assert.Contains(
            "${ANDREJA_PUBLIC_ORIGIN:-https://${ANDREJA_HOSTNAME:-localhost}}",
            compose,
            StringComparison.Ordinal);
        Assert.DoesNotContain("POSTGRES_PASSWORD:", compose, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_PASSWORD_FILE:", compose, StringComparison.Ordinal);
        Assert.Contains("USER $APP_UID", dockerfile, StringComparison.Ordinal);
        Assert.AreEqual(2, dockerfile.Split("@sha256:", StringSplitOptions.None).Length - 1);
        Assert.Contains(
            "org.opencontainers.image.licenses=\"Apache-2.0\"",
            dockerfile,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "org.opencontainers.image.licenses=\"MIT\"",
            dockerfile,
            StringComparison.Ordinal);
        Assert.DoesNotContain("\n  debug:", collector, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  logging:", collector, StringComparison.Ordinal);
        Assert.Contains("attributes/suppress", collector, StringComparison.Ordinal);
        Assert.Contains("gen_ai.input.messages", collector, StringComparison.Ordinal);
        Assert.Contains("db.statement", collector, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  debug:", collectorDashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  logging:", collectorDashboard, StringComparison.Ordinal);
        Assert.Contains("attributes/suppress", collectorDashboard, StringComparison.Ordinal);
        Assert.Contains("aspire-dashboard:", compose, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:${ASPIRE_DASHBOARD_PORT:-18888}:18888", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("ASPIRE_DASHBOARD_BIND_ADDRESS", compose, StringComparison.Ordinal);
        Assert.Contains("DOTNET_DASHBOARD_TELEMETRY_OPTOUT: \"true\"", compose, StringComparison.Ordinal);
        Assert.Contains("otel-collector.dashboard.yaml:/etc/otelcol-contrib/config.yaml:ro", dashboardCompose, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:", maintenance, StringComparison.Ordinal);
        Assert.Contains("migration-approval.json:ro", migration, StringComparison.Ordinal);
        Assert.Contains("backup.dump:ro", migration, StringComparison.Ordinal);
        Assert.Contains("migration.sql:ro", migration, StringComparison.Ordinal);

        var legalGate = File.ReadAllText(
            Path.Join(root, "docs", "legal", "license-evaluation.md"));
        Assert.Contains(
            "Do not publish releases, packages, containers",
            legalGate,
            StringComparison.Ordinal);

        using var document = JsonDocument.Parse(schema);
        Assert.AreEqual(
            "1",
            document.RootElement.GetProperty("properties")
                .GetProperty("archiveVersion")
                .GetProperty("const")
                .GetString());
        Assert.AreEqual(
            Enum.GetValues<PortableDataArea>().Length,
            document.RootElement.GetProperty("properties")
                .GetProperty("artifacts")
                .GetProperty("minItems")
                .GetInt32());
        Assert.AreEqual(
            Enum.GetValues<PortableDataArea>().Length,
            document.RootElement.GetProperty("properties")
                .GetProperty("artifacts")
                .GetProperty("maxItems")
                .GetInt32());
        Assert.AreEqual(
            ApplicationExportContract.RequiredExclusions.Count,
            document.RootElement.GetProperty("properties")
                .GetProperty("exclusions")
                .GetProperty("minItems")
                .GetInt32());
        Assert.AreEqual(
            ApplicationExportContract.RequiredExclusions.Count,
            document.RootElement.GetProperty("properties")
                .GetProperty("exclusions")
                .GetProperty("maxItems")
                .GetInt32());
        Assert.Contains("or TimeoutException", portabilityCli, StringComparison.Ordinal);
        foreach (var exclusion in ApplicationExportContract.RequiredExclusions)
        {
            Assert.Contains(exclusion, schema, StringComparison.Ordinal);
        }
        Assert.AreEqual(
            "1.0",
            ApplicationExportContract.RequiredContractVersions[PortableDataArea.Semantic]);
        Assert.AreEqual(
            "1.0",
            ApplicationExportContract.RequiredContractVersions[PortableDataArea.Provenance]);
    }

    private static MemoryStream OpenArtifact(Dictionary<string, byte[]> files, string path) =>
        new MemoryStream(files[path]);

    private static Dictionary<string, byte[]> CreateArtifacts() =>
        Enum.GetValues<PortableDataArea>().ToDictionary(
            area => $"{area.ToString().ToLowerInvariant()}.ndjson",
            area => Encoding.UTF8.GetBytes($"{{\"area\":\"{area}\"}}\n"),
            StringComparer.Ordinal);

    private static ApplicationExportManifest CreateManifest(
        Dictionary<string, byte[]> files) =>
        new()
        {
            ArchiveVersion = ApplicationExportContract.ArchiveVersion,
            SchemaVersion = "1.0.0",
            ApplicationVersion = "test",
            ExportId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            TenantReference = "tenant-fixture",
            Artifacts = Enum.GetValues<PortableDataArea>().Select(area =>
            {
                var path = $"{area.ToString().ToLowerInvariant()}.ndjson";
                var content = files[path];
                return new PortableArtifactDescriptor
                {
                    DataArea = area,
                    ContractVersion =
                        ApplicationExportContract.RequiredContractVersions.GetValueOrDefault(
                            area,
                            "1"),
                    Path = path,
                    Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                    ByteLength = content.LongLength,
                    RecordCount = 1,
                };
            }).ToArray(),
            Exclusions = ApplicationExportContract.RequiredExclusions.Select(code =>
                new ExportExclusion { Code = code, Reason = "Excluded by v1 contract." }).ToArray(),
            Reauthorization =
            [
                new ReauthorizationRequirement
                {
                    ProviderReference = "fixture-provider",
                    Action = "Reauthorize after import.",
                },
            ],
        };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Join(directory.FullName, "compose.yaml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed class CleanInstanceProbe(bool isClean) : ICleanInstanceImportProbe
    {
        public ValueTask<bool> IsCleanAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(isClean);
    }

    private sealed class CapturingActivityExporter : BaseExporter<Activity>
    {
        public List<Activity> Exported { get; } = [];

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                Exported.Add(activity);
            }

            return ExportResult.Success;
        }
    }

    private sealed class CapturingMetricExporter : BaseExporter<Metric>
    {
        public List<Metric> Exported { get; } = [];

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
            {
                Exported.Add(metric);
            }

            return ExportResult.Success;
        }
    }
}
