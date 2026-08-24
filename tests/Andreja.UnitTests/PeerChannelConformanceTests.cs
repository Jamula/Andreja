using Andreja.Modules.Channels;
using Andreja.Platform.Contracts.Peers;
using System.Text;

namespace Andreja.UnitTests;

public sealed class PeerChannelConformanceTests
{
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("local-conformance-key-32-bytes!!");
    private static readonly Guid Sender = Guid.Parse("018f0000-0000-7000-8000-000000000001");
    private static readonly Guid Recipient = Guid.Parse("018f0000-0000-7000-8000-000000000002");
    private static readonly Guid Grant = Guid.Parse("018f0000-0000-7000-8000-000000000003");
    private static readonly DateTimeOffset Now =
        new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CanonicalizationAndSignatureAreDeterministic()
    {
        var envelope = CreateEnvelope();

        var first = LocalPeerChannel.SignFixture(envelope, Key);
        var second = LocalPeerChannel.SignFixture(envelope, Key);

        Assert.Equal(first.Signature, second.Signature);
        Assert.Equal("OCz+dPbQLsy0izfmzUAZn3TX2y0UxeYGgnsRbayRVsw=", first.Signature);
        Assert.Equal(
            Convert.ToHexString(LocalPeerChannel.Canonicalize(first)),
            Convert.ToHexString(LocalPeerChannel.Canonicalize(second)));
    }

    [Fact]
    public async Task TamperAndExactReplayAreRejected()
    {
        var channel = CreateChannel();
        var signed = LocalPeerChannel.SignFixture(CreateEnvelope(), Key);

        var accepted = await channel.ReceiveAsync(signed, Context(), CancellationToken.None);
        var replay = await channel.ReceiveAsync(signed, Context(), CancellationToken.None);
        var tampered = await channel.ReceiveAsync(
            signed with { PayloadDigest = new string('B', 64) },
            Context(),
            CancellationToken.None);

        Assert.Equal(PeerReceiveOutcome.Accepted, accepted.Outcome);
        Assert.True(accepted.EffectApplied);
        Assert.Equal(PeerReceiveOutcome.Replay, replay.Outcome);
        Assert.Equal(PeerReceiveOutcome.InvalidSignature, tampered.Outcome);
    }

    [Theory]
    [InlineData("audience", PeerReceiveOutcome.WrongAudience)]
    [InlineData("purpose", PeerReceiveOutcome.WrongPurpose)]
    [InlineData("expiry", PeerReceiveOutcome.Expired)]
    [InlineData("version", PeerReceiveOutcome.InvalidVersion)]
    [InlineData("algorithm", PeerReceiveOutcome.UnknownAlgorithm)]
    [InlineData("key", PeerReceiveOutcome.UnknownKey)]
    public async Task EnvelopePolicyFailsClosed(string mutation, PeerReceiveOutcome expected)
    {
        var envelope = CreateEnvelope();
        envelope = mutation switch
        {
            "audience" => envelope with { RecipientId = Guid.CreateVersion7() },
            "purpose" => envelope with { Purpose = "profile.publish" },
            "expiry" => envelope with { ExpiresAt = Now },
            "version" => envelope with { ProtocolVersion = "andreja.peer/2" },
            "algorithm" => envelope with { SigningAlgorithm = "RS256" },
            "key" => envelope with { KeyId = "unknown-key" },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };
        var signed = LocalPeerChannel.SignFixture(envelope, Key);

        var result = await CreateChannel().ReceiveAsync(signed, Context(), CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
        Assert.False(result.EffectApplied);
    }

    [Fact]
    public async Task NewEnvelopeWithSameOperationIsIdempotent()
    {
        var channel = CreateChannel();
        var first = LocalPeerChannel.SignFixture(CreateEnvelope(), Key);
        var retry = LocalPeerChannel.SignFixture(
            CreateEnvelope() with
            {
                EnvelopeId = Guid.Parse("018f0000-0000-7000-8000-000000000099"),
                Nonce = "nonce-002",
            },
            Key);

        var accepted = await channel.ReceiveAsync(first, Context(), CancellationToken.None);
        var idempotent = await channel.ReceiveAsync(retry, Context(), CancellationToken.None);

        Assert.Equal(PeerReceiveOutcome.Accepted, accepted.Outcome);
        Assert.Equal(PeerReceiveOutcome.IdempotentReplay, idempotent.Outcome);
        Assert.False(idempotent.EffectApplied);
        Assert.Equal(accepted.ReceiptId, idempotent.ReceiptId);
    }

    private static LocalPeerChannel CreateChannel() =>
        new(new Dictionary<string, byte[]> { ["peer-key-1"] = Key });

    private static PeerReceiveContext Context() =>
        new(Sender, Recipient, Grant, "trip.plan", Now);

    private static SignedPeerEnvelope CreateEnvelope() =>
        new(
            LocalPeerChannel.ProtocolVersion,
            Guid.Parse("018f0000-0000-7000-8000-000000000010"),
            Sender,
            Recipient,
            Grant,
            "trip.plan",
            "nonce-001",
            "operation-001",
            Now.AddMinutes(-1),
            Now.AddMinutes(4),
            "andreja.trip-summary/1",
            new string('A', 64),
            LocalPeerChannel.FixtureAlgorithm,
            "peer-key-1",
            string.Empty);
}
