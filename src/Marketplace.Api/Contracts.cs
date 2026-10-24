using System.ComponentModel.DataAnnotations;
using Marketplace.Core;

namespace Marketplace.Api;

/// <summary>Credentials accepted by the local demo login endpoint.</summary>
public sealed record LoginRequest([property: Required, EmailAddress] string Email, [property: Required, StringLength(200, MinimumLength = 8)] string Password);
/// <summary>Authenticated account details.</summary>
public sealed record MeResponse(string Email, string Role, long ProfileId, string DisplayName);
/// <summary>Customer search projection.</summary>
public sealed record CustomerResponse(long Id, string FirstName, string LastName);
/// <summary>Contractor search projection.</summary>
public sealed record ContractorResponse(long Id, string Name, decimal Rating);
/// <summary>Job representation returned by the API.</summary>
public sealed record JobResponse(long Id, long CustomerId, DateOnly StartDate, DateOnly DueDate, decimal Budget, string Description, JobStatus Status, long? AcceptedBy, int? OfferCount, bool HasCurrentUserOffer, DateTimeOffset CreatedAt);
/// <summary>Offer representation returned only to its contractor or the job customer.</summary>
public sealed record OfferResponse(long Id, long JobId, long ContractorId, string ContractorName, decimal ContractorRating, decimal Price, bool IsOverBudget, DateTimeOffset CreatedAt);
/// <summary>Input for creating or changing a job.</summary>
public sealed record SaveJobRequest([property: Required] DateOnly StartDate, [property: Required] DateOnly DueDate, [property: Range(typeof(decimal), "0.01", "9999999999999999.99")] decimal Budget, [property: Required, StringLength(5000, MinimumLength = 1)] string Description);
/// <summary>Input for creating or changing an offer.</summary>
public sealed record SaveOfferRequest([property: Range(typeof(decimal), "0.01", "9999999999999999.99")] decimal Price);
/// <summary>Cursor-paged JSON response.</summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
