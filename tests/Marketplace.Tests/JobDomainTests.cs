using FluentAssertions;
using Marketplace.Core;
using Marketplace.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Marketplace.Tests;

/// <summary>Unit tests for marketplace aggregate invariants.</summary>
[TestClass]
public sealed class JobDomainTests
{
    /// <summary>Ensures a job cannot enter the model with invalid scheduling or price data.</summary>
    [TestMethod]
    public void Create_RejectsInvalidJobDetails()
    {
        var command = new SaveJobCommand(new DateOnly(2026, 2, 2), new DateOnly(2026, 2, 1), 100m, "Valid description");

        var action = () => Job.Create(1, command, DateTimeOffset.UtcNow);

        action.Should().Throw<ArgumentException>();
    }

    /// <summary>Ensures an awarded job cannot be changed or deleted.</summary>
    [TestMethod]
    public void Award_PreventsFurtherJobChanges()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var job = Job.Create(1, new SaveJobCommand(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 2), 100m, "Valid description"), now);
        var offer = JobOffer.Create(job.Id, 2, new SaveOfferCommand(90m), now);

        job.Award(offer, now.AddMinutes(1));

        job.Status.Should().Be(JobStatus.Awarded);
        var update = () => job.Update(new SaveJobCommand(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 3), 110m, "Updated description"), now.AddMinutes(2));
        var delete = () => job.Delete(now.AddMinutes(2));

        update.Should().Throw<InvalidOperationException>();
        delete.Should().Throw<InvalidOperationException>();
    }

    /// <summary>Ensures an offer cannot be changed after withdrawal.</summary>
    [TestMethod]
    public void Withdraw_PreventsPriceChanges()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var offer = JobOffer.Create(1, 2, new SaveOfferCommand(90m), now);

        offer.Withdraw(now.AddMinutes(1));

        offer.IsDeleted.Should().BeTrue();
        var changePrice = () => offer.ChangePrice(new SaveOfferCommand(95m), now.AddMinutes(2));
        changePrice.Should().Throw<InvalidOperationException>();
    }
}
