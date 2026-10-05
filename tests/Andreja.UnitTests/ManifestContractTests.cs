using Andreja.Modules.Channels;
using Andreja.Modules.Skills;
using Andreja.Platform.Contracts.Channels;
using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Skills;
using System.Text.Json;

namespace Andreja.UnitTests;

[TestClass]
public sealed class ManifestContractTests
{
    [TestMethod]
    public void SkillAndChannelManifestsRoundTripEveryRequiredContractSection()
    {
        var skill = ExecutionContractFixture.SkillManifest();
        var channel = ExecutionContractFixture.ChannelManifest();

        var skillRoundTrip = Assert.IsInstanceOfType<SkillManifest>(
            JsonSerializer.Deserialize<SkillManifest>(JsonSerializer.Serialize(skill)));
        var channelRoundTrip = Assert.IsInstanceOfType<ChannelManifest>(
            JsonSerializer.Deserialize<ChannelManifest>(JsonSerializer.Serialize(channel)));

        Assert.AreEqual(
            JsonSerializer.Serialize(skill),
            JsonSerializer.Serialize(skillRoundTrip));
        Assert.AreEqual(
            JsonSerializer.Serialize(channel),
            JsonSerializer.Serialize(channelRoundTrip));
        Assert.AreEqual("andreja.skill-manifest.v1", skillRoundTrip.SchemaVersion);
        Assert.AreEqual("andreja.channel-manifest.v1", channelRoundTrip.SchemaVersion);
        Assert.AreEqual("1.0.0", skillRoundTrip.Version);
        Assert.AreEqual("1.0.0", channelRoundTrip.Version);
        Assert.IsNotEmpty(skillRoundTrip.Permissions.DeclaredCapabilities);
        Assert.IsNotEmpty(skillRoundTrip.Permissions.AllowedPurposes);
        Assert.IsNotEmpty(skillRoundTrip.Permissions.DataClasses);
        Assert.IsNotEmpty(skillRoundTrip.HelpSupport.SupportRoute);
        Assert.IsNotEmpty(skillRoundTrip.Compatibility.SupportedPlatformVersions);
        Assert.IsNotEmpty(channelRoundTrip.Permissions.DeclaredCapabilities);
        Assert.IsNotEmpty(channelRoundTrip.Provider.DeliveryTopology.Reason!);
        foreach (var field in ExplicitNotApplicableFields(skillRoundTrip).Concat(
                     ExplicitNotApplicableFields(channelRoundTrip)))
        {
            Assert.AreEqual(ManifestApplicability.NotApplicable, field.Applicability);
            Assert.IsTrue(field.HasNullValue);
            Assert.IsFalse(string.IsNullOrWhiteSpace(field.Reason));
        }
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("v1.0.0")]
    [DataRow("1.0")]
    [DataRow("")]
    public void HostsRejectNonSemanticArtifactVersions(string version)
    {
        var skill = ExecutionContractFixture.SkillManifest() with { Version = version };
        var channel = ExecutionContractFixture.ChannelManifest() with { Version = version };

        Assert.ThrowsExactly<ArgumentException>(() =>
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
        Assert.ThrowsExactly<ArgumentException>(() =>
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

    [TestMethod]
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

        Assert.ThrowsExactly<ArgumentException>(() =>
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

    [TestMethod]
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

        Assert.ThrowsExactly<ArgumentException>(() =>
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
        Assert.ThrowsExactly<ArgumentException>(() =>
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

    [TestMethod]
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

        Assert.ThrowsExactly<ArgumentException>(() =>
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
