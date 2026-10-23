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
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Budget { get; set; }
    public required string Description { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Open;
    public long? AcceptedOfferId { get; set; }
    public JobOffer? AcceptedOffer { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<JobOffer> Offers { get; } = [];
}

/// <summary>A contractor's price proposal for a job.</summary>
public sealed class JobOffer
{
    public long Id { get; set; }
    public long JobId { get; set; }
    public Job Job { get; set; } = null!;
    public long ContractorId { get; set; }
    public Contractor Contractor { get; set; } = null!;
    public decimal Price { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
