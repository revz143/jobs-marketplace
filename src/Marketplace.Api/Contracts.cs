using System.ComponentModel.DataAnnotations;
using Marketplace.Core;

namespace Marketplace.Api;

/// <summary>Credentials accepted by the local demo login endpoint.</summary>
public sealed record LoginRequest([property: Required, EmailAddress] string Email, [property: Required, StringLength(200, MinimumLength = 8)] string Password);
/// <summary>Authenticated account details.</summary>
public sealed record MeResponse(string Email, string Role, long ProfileId, string DisplayName);
/// <summary>Input for creating or changing a job.</summary>
public sealed record SaveJobRequest([property: Required] DateOnly StartDate, [property: Required] DateOnly DueDate, [property: Range(typeof(decimal), "0.01", "9999999999999999.99")] decimal Budget, [property: Required, StringLength(5000, MinimumLength = 1)] string Description);
/// <summary>Input for creating or changing an offer.</summary>
public sealed record SaveOfferRequest([property: Range(typeof(decimal), "0.01", "9999999999999999.99")] decimal Price);
