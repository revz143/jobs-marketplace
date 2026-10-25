using Marketplace.Core;
using Microsoft.AspNetCore.Identity;

namespace Marketplace.Persistence;

/// <summary>Identity user for a marketplace account.</summary>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>Creates an application user with a generated identifier.</summary>
    public ApplicationUser() => Id = Guid.NewGuid().ToString("N");
}

/// <summary>A customer profile, optionally linked to a sign-in account.</summary>
public sealed class Customer
{
    public long Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public List<Job> Jobs { get; } = [];
}

/// <summary>A contractor business profile, optionally linked to a sign-in account.</summary>
public sealed class Contractor
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public decimal Rating { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public List<JobOffer> Offers { get; } = [];
}

/// <summary>A job commissioned by a customer.</summary>
public sealed class Job
{
    private Job() { }

    public long Id { get; private set; }
    public long CustomerId { get; private set; }
    public Customer Customer { get; set; } = null!;
    public DateOnly StartDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal Budget { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public JobStatus Status { get; private set; } = JobStatus.Open;
    public long? AcceptedOfferId { get; private set; }
    public JobOffer? AcceptedOffer { get; set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<JobOffer> Offers { get; } = [];

    /// <summary>Creates an open job after enforcing its domain invariants.</summary>
    public static Job Create(long customerId, SaveJobCommand command, DateTimeOffset now)
    {
        EnsureValid(command);
        return new Job
        {
            CustomerId = customerId,
            StartDate = command.StartDate,
            DueDate = command.DueDate,
            Budget = command.Budget,
            Description = command.Description.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Changes an open job's editable details.</summary>
    public void Update(SaveJobCommand command, DateTimeOffset now)
    {
        if (Status != JobStatus.Open || IsDeleted) throw new InvalidOperationException("Only open jobs can be changed.");
        EnsureValid(command);
        StartDate = command.StartDate;
        DueDate = command.DueDate;
        Budget = command.Budget;
        Description = command.Description.Trim();
        UpdatedAt = now;
    }

    /// <summary>Marks an open job as deleted.</summary>
    public void Delete(DateTimeOffset now)
    {
        if (Status != JobStatus.Open || IsDeleted) throw new InvalidOperationException("Only open jobs can be deleted.");
        IsDeleted = true;
        UpdatedAt = now;
    }

    /// <summary>Awards this open job to an offer belonging to it.</summary>
    public void Award(JobOffer offer, DateTimeOffset now)
    {
        if (IsDeleted || Status != JobStatus.Open) throw new InvalidOperationException("Only open jobs can be awarded.");
        if (offer.IsDeleted || offer.JobId != Id) throw new InvalidOperationException("The offer does not belong to this job.");
        AcceptedOfferId = offer.Id;
        Status = JobStatus.Awarded;
        UpdatedAt = now;
    }

    private static void EnsureValid(SaveJobCommand command)
    {
        if (MarketplaceValidation.JobError(command) is { } error) throw new ArgumentException(error, nameof(command));
    }
}

/// <summary>A contractor's price proposal for a job.</summary>
public sealed class JobOffer
{
    private JobOffer() { }

    public long Id { get; private set; }
    public long JobId { get; private set; }
    public Job Job { get; set; } = null!;
    public long ContractorId { get; private set; }
    public Contractor Contractor { get; set; } = null!;
    public decimal Price { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Creates an offer with a valid two-decimal price.</summary>
    public static JobOffer Create(long jobId, long contractorId, SaveOfferCommand command, DateTimeOffset now)
    {
        EnsureValid(command);
        return new JobOffer { JobId = jobId, ContractorId = contractorId, Price = command.Price, CreatedAt = now, UpdatedAt = now };
    }

    /// <summary>Changes the price of an active offer.</summary>
    public void ChangePrice(SaveOfferCommand command, DateTimeOffset now)
    {
        if (IsDeleted) throw new InvalidOperationException("A withdrawn offer cannot be changed.");
        EnsureValid(command);
        Price = command.Price;
        UpdatedAt = now;
    }

    /// <summary>Withdraws an active offer.</summary>
    public void Withdraw(DateTimeOffset now)
    {
        if (IsDeleted) throw new InvalidOperationException("The offer is already withdrawn.");
        IsDeleted = true;
        UpdatedAt = now;
    }

    private static void EnsureValid(SaveOfferCommand command)
    {
        if (MarketplaceValidation.OfferPriceError(command) is { } error) throw new ArgumentException(error, nameof(command));
    }
}
