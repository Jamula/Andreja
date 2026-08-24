using Andreja.Modules.Channels;
using Andreja.Modules.Skills;
using Andreja.Platform.Contracts.Channels;
using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Skills;
using System.Text.Json;

namespace Andreja.UnitTests;

public sealed class ManifestContractTests
{
    [Fact]
    public void SkillAndChannelManifestsRoundTripEveryRequiredContractSection()
    {
        var skill = ExecutionContractFixture.SkillManifest();
        var channel = ExecutionContractFixture.ChannelManifest();

        var skillRoundTrip = Assert.IsType<SkillManifest>(
            JsonSerializer.Deserialize<SkillManifest>(JsonSerializer.Serialize(skill)));
        var channelRoundTrip = Assert.IsType<ChannelManifest>(
            JsonSerializer.Deserialize<ChannelManifest>(JsonSerializer.Serialize(channel)));

        Assert.Equal(
            JsonSerializer.Serialize(skill),
            JsonSerializer.Serialize(skillRoundTrip));
        Assert.Equal(
            JsonSerializer.Serialize(channel),
            JsonSerializer.Serialize(channelRoundTrip));
        Assert.Equal("andreja.skill-manifest.v1", skillRoundTrip.SchemaVersion);
        Assert.Equal("andreja.channel-manifest.v1", channelRoundTrip.SchemaVersion);
        Assert.Equal("1.0.0", skillRoundTrip.Version);
        Assert.Equal("1.0.0", channelRoundTrip.Version);
        Assert.NotEmpty(skillRoundTrip.Permissions.DeclaredCapabilities);
        Assert.NotEmpty(skillRoundTrip.Permissions.AllowedPurposes);
        Assert.NotEmpty(skillRoundTrip.Permissions.DataClasses);
        Assert.NotEmpty(skillRoundTrip.HelpSupport.SupportRoute);
        Assert.NotEmpty(skillRoundTrip.Compatibility.SupportedPlatformVersions);
        Assert.NotEmpty(channelRoundTrip.Permissions.DeclaredCapabilities);
        Assert.NotEmpty(channelRoundTrip.Provider.DeliveryTopology.Reason!);
        Assert.All(
            ExplicitNotApplicableFields(skillRoundTrip).Concat(
                ExplicitNotApplicableFields(channelRoundTrip)),
            field =>
            {
                Assert.Equal(ManifestApplicability.NotApplicable, field.Applicability);
                Assert.True(field.HasNullValue);
                Assert.False(string.IsNullOrWhiteSpace(field.Reason));
            });
    }

    [Theory]
    [InlineData("1")]
    [InlineData("v1.0.0")]
    [InlineData("1.0")]
    [InlineData("")]
    public void HostsRejectNonSemanticArtifactVersions(string version)
    {
        var skill = ExecutionContractFixture.SkillManifest() with { Version = version };
        var channel = ExecutionContractFixture.ChannelManifest() with { Version = version };

        Assert.Throws<ArgumentException>(() =>
            new InMemorySkillHost().Register(
                skill,
                new Dictionary<string, SkillToolHandler>
                {
                    [ExecutionContractFixture.ToolName] = (_, _, _) =>
                        ValueTask.FromResult(new SkillResult(
                            SkillResultStatus.Completed,
                            null,
                            null,
                            null)),
                }));
        Assert.Throws<ArgumentException>(() =>
            new InMemoryChannelHost().Register(
                channel,
                new Dictionary<string, ChannelOperationHandler>
                {
                    [ExecutionContractFixture.ChannelOperationName] = (_, _, _) =>
                        ValueTask.FromResult(new ChannelResult(
                            ChannelResultStatus.Completed,
                            null,
                            null)),
                }));
    }

    [Fact]
    public void MissingExplicitNonApplicabilityReasonFailsRegistration()
    {
        var skill = ExecutionContractFixture.SkillManifest();
        var invalid = skill with
        {
            Integrity = skill.Integrity with
            {
                Signature = new(ManifestApplicability.NotApplicable, null, null),
            },
        };

        Assert.Throws<ArgumentException>(() =>
            new InMemorySkillHost().Register(
                invalid,
                new Dictionary<string, SkillToolHandler>
                {
                    [ExecutionContractFixture.ToolName] = (_, _, _) =>
                        ValueTask.FromResult(new SkillResult(
                            SkillResultStatus.Completed,
                            null,
                            null,
                            null)),
                }));
    }

    [Fact]
    public void UnknownManifestSchemaVersionsFailRegistration()
    {
        var skill = ExecutionContractFixture.SkillManifest() with
        {
            SchemaVersion = "andreja.skill-manifest.v2",
        };
        var channel = ExecutionContractFixture.ChannelManifest() with
        {
            SchemaVersion = "andreja.channel-manifest.v2",
        };

        Assert.Throws<ArgumentException>(() =>
            new InMemorySkillHost().Register(
                skill,
                new Dictionary<string, SkillToolHandler>
                {
                    [ExecutionContractFixture.ToolName] = (_, _, _) =>
                        ValueTask.FromResult(new SkillResult(
                            SkillResultStatus.Completed,
                            null,
                            null,
                            null)),
                }));
        Assert.Throws<ArgumentException>(() =>
            new InMemoryChannelHost().Register(
                channel,
                new Dictionary<string, ChannelOperationHandler>
                {
                    [ExecutionContractFixture.ChannelOperationName] = (_, _, _) =>
                        ValueTask.FromResult(new ChannelResult(
                            ChannelResultStatus.Completed,
                            null,
                            null)),
                }));
    }

    [Fact]
    public void ApplicableMetadataCannotSmuggleANonApplicabilityReason()
    {
        var channel = ExecutionContractFixture.ChannelManifest();
        var invalid = channel with
        {
            Provider = channel.Provider with
            {
                Provider = new(
                    ManifestApplicability.Applicable,
                    "provider",
                    "Contradictory not-applicable reason."),
            },
        };

        Assert.Throws<ArgumentException>(() =>
            new InMemoryChannelHost().Register(
                invalid,
                new Dictionary<string, ChannelOperationHandler>
                {
                    [ExecutionContractFixture.ChannelOperationName] = (_, _, _) =>
                        ValueTask.FromResult(new ChannelResult(
                            ChannelResultStatus.Completed,
                            null,
                            null)),
                }));
    }

    private static IEnumerable<ApplicabilityProjection> ExplicitNotApplicableFields(
        SkillManifest manifest)
    {
        yield return Project(manifest.Lifecycle.DeprecationNotice);
        yield return Project(manifest.Lifecycle.ReplacementArtifact);
        yield return Project(manifest.Execution.NetworkDestinations);
        yield return Project(manifest.Execution.RemoteProtocol);
        yield return Project(manifest.DataHandling.SettingsSchema);
        yield return Project(manifest.DataHandling.ResourceLimits);
        yield return Project(manifest.Compatibility.MinimumProtocolVersion);
        yield return Project(manifest.Integrity.PackageDigest);
        yield return Project(manifest.Integrity.Signature);
        yield return Project(manifest.Integrity.Provenance);
        yield return Project(manifest.Integrity.Sbom);
        yield return Project(manifest.ChannelDependencies);
    }

    private static IEnumerable<ApplicabilityProjection> ExplicitNotApplicableFields(
        ChannelManifest manifest)
    {
        yield return Project(manifest.Lifecycle.DeprecationNotice);
        yield return Project(manifest.Lifecycle.ReplacementArtifact);
        yield return Project(manifest.Execution.NetworkDestinations);
        yield return Project(manifest.Execution.RemoteProtocol);
        yield return Project(manifest.DataHandling.SettingsSchema);
        yield return Project(manifest.DataHandling.ResourceLimits);
        yield return Project(manifest.Compatibility.MinimumProtocolVersion);
        yield return Project(manifest.Integrity.PackageDigest);
        yield return Project(manifest.Integrity.Signature);
        yield return Project(manifest.Integrity.Provenance);
        yield return Project(manifest.Integrity.Sbom);
        yield return Project(manifest.Provider.Provider);
        yield return Project(manifest.Provider.AccountTypes);
        yield return Project(manifest.Provider.OAuthScopes);
        yield return Project(manifest.Provider.QueryMode);
        yield return Project(manifest.Provider.SyncMode);
        yield return Project(manifest.Provider.PublishMode);
        yield return Project(manifest.Provider.WebhookSupport);
        yield return Project(manifest.Provider.ChangeFeedSupport);
        yield return Project(manifest.Provider.CachePolicy);
        yield return Project(manifest.Provider.CostModel);
        yield return Project(manifest.Provider.DeliveryTopology);
    }

    private static ApplicabilityProjection Project<T>(ManifestField<T> field) =>
        new(field.Applicability, field.Value is null, field.Reason);

    private sealed record ApplicabilityProjection(
        ManifestApplicability Applicability,
        bool HasNullValue,
        string? Reason);
}
