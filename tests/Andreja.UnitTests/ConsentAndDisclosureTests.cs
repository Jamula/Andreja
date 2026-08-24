using Andreja.Modules.Sharing;
using Andreja.Platform.Contracts.Sharing;

namespace Andreja.UnitTests;

public sealed class ConsentAndDisclosureTests
{
    [Fact]
    public void BilateralConsentRequiresReceiverAcceptanceAndOffererActivation()
    {
        var (record, offerer, receiver, now) = CreateConsent();

        var accepted = ConsentPolicy.Transition(
            record,
            ConsentState.Accepted,
            receiver,
            now.AddMinutes(1));
        var active = ConsentPolicy.Transition(
            accepted,
            ConsentState.Active,
            offerer,
            now.AddMinutes(2));

        Assert.Equal(
            [ConsentState.Offered, ConsentState.Accepted, ConsentState.Active],
            active.Timeline.Select(item => item.State));
        Assert.Throws<InvalidOperationException>(
            () => ConsentPolicy.Transition(record, ConsentState.Active, offerer, now.AddMinutes(1)));
    }

    [Fact]
    public void GrantRequiresActiveConsentAndExactPurpose()
    {
        var (record, offerer, receiver, now) = CreateConsent();
        var active = ConsentPolicy.Transition(
            ConsentPolicy.Transition(record, ConsentState.Accepted, receiver, now.AddMinutes(1)),
            ConsentState.Active,
            offerer,
            now.AddMinutes(2));
        var grant = new Grant(
            record.GrantId,
            "1",
            Guid.CreateVersion7(),
            "trip/7",
            "itinerary",
            receiver,
            "trip.plan",
            DisclosureLevel.Summary,
            new HashSet<string>(["read"], StringComparer.Ordinal),
            now,
            now.AddHours(1),
            false,
            null,
            record.ConsentId);

        Assert.True(ConsentPolicy.IsGrantActive(
            grant,
            active,
            receiver,
            "trip.plan",
            "read",
            now.AddMinutes(3)));
        Assert.False(ConsentPolicy.IsGrantActive(
            grant,
            active,
            receiver,
            "profile.publish",
            "read",
            now.AddMinutes(3)));
    }

    [Fact]
    public void ActiveConsentCanBeRevokedButNotReactivated()
    {
        var (record, offerer, receiver, now) = CreateConsent();
        var active = ConsentPolicy.Transition(
            ConsentPolicy.Transition(record, ConsentState.Accepted, receiver, now.AddMinutes(1)),
            ConsentState.Active,
            offerer,
            now.AddMinutes(2));

        var revoked = ConsentPolicy.Transition(
            active,
            ConsentState.Revoked,
            receiver,
            now.AddMinutes(3));

        Assert.Equal(ConsentState.Revoked, revoked.Timeline[^1].State);
        Assert.Throws<InvalidOperationException>(
            () => ConsentPolicy.Transition(
                revoked,
                ConsentState.Active,
                offerer,
                now.AddMinutes(4)));
    }

    [Fact]
    public void DisclosurePolicyCanReduceButNeverWiden()
    {
        Assert.Equal(
            DisclosureLevel.Timing,
            DisclosurePolicy.Reduce(
                DisclosureLevel.Full,
                DisclosureLevel.Summary,
                DisclosureLevel.Timing));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DisclosurePolicy.Reduce(
                (DisclosureLevel)99,
                DisclosureLevel.Full,
                DisclosureLevel.Full));
    }

    [Fact]
    public async Task ShareAuditRecordsAllowAndDenyWithoutPayloadContent()
    {
        var sink = new InMemoryShareAuditSink();
        var (_, actor, peer, now) = CreateConsent();
        var tenant = Guid.CreateVersion7();
        var grant = Guid.CreateVersion7();
        var consent = Guid.CreateVersion7();
        var allowed = new ShareAuditEntry(
            Guid.CreateVersion7(),
            "1",
            tenant,
            actor,
            peer,
            grant,
            consent,
            "trip/7",
            "itinerary",
            "trip.plan",
            DisclosureLevel.Timing,
            "read",
            ShareAuditOutcome.Allowed,
            "ENVELOPE-DIGEST",
            "PAYLOAD-DIGEST",
            now);
        var denied = allowed with
        {
            AuditId = Guid.CreateVersion7(),
            Outcome = ShareAuditOutcome.Denied,
            DisclosureLevel = DisclosureLevel.Full,
        };

        await sink.AppendAsync(allowed, CancellationToken.None);
        await sink.AppendAsync(denied, CancellationToken.None);

        Assert.Equal([ShareAuditOutcome.Allowed, ShareAuditOutcome.Denied], sink.Entries.Select(x => x.Outcome));
        Assert.All(sink.Entries, entry => Assert.DoesNotContain("content", entry.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static (ConsentRecord Record, Guid Offerer, Guid Receiver, DateTimeOffset Now)
        CreateConsent()
    {
        var now = new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
        var offerer = Guid.CreateVersion7();
        var receiver = Guid.CreateVersion7();
        var grantId = Guid.CreateVersion7();
        var record = new ConsentRecord(
            Guid.CreateVersion7(),
            "1",
            grantId,
            offerer,
            receiver,
            new(
                "trip.plan",
                DisclosureLevel.Summary,
                new HashSet<string>(["read"], StringComparer.Ordinal),
                now,
                now.AddHours(1)),
            [new(ConsentState.Offered, offerer, now)]);
        return (record, offerer, receiver, now);
    }
}
