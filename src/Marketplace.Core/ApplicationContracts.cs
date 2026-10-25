namespace Marketplace.Core;

/// <summary>Describes the outcome of an application operation without coupling it to HTTP.</summary>
public enum OperationStatus { Success, NotFound, Forbidden, Conflict, ValidationFailed }

/// <summary>Represents an application-layer result.</summary>
/// <typeparam name="T">The successful value type.</typeparam>
public sealed record OperationResult<T>(OperationStatus Status, T? Value = default, string? ErrorKey = null, string? ErrorMessage = null)
{
    /// <summary>Creates a successful result.</summary>
    public static OperationResult<T> Success(T value) => new(OperationStatus.Success, value);
    /// <summary>Creates a not-found result.</summary>
    public static OperationResult<T> NotFound() => new(OperationStatus.NotFound);
    /// <summary>Creates a forbidden result.</summary>
    public static OperationResult<T> Forbidden() => new(OperationStatus.Forbidden);
    /// <summary>Creates a conflict result.</summary>
    public static OperationResult<T> Conflict(string message) => new(OperationStatus.Conflict, default, null, message);
    /// <summary>Creates a validation failure result.</summary>
    public static OperationResult<T> Validation(string key, string message) => new(OperationStatus.ValidationFailed, default, key, message);
}

/// <summary>Customer profile data exposed by marketplace queries.</summary>
public sealed record CustomerResponse(long Id, string FirstName, string LastName);
/// <summary>Contractor profile data exposed by marketplace queries.</summary>
public sealed record ContractorResponse(long Id, string Name, decimal Rating);
/// <summary>Job data exposed by marketplace queries and commands.</summary>
public sealed record JobResponse(long Id, long CustomerId, DateOnly StartDate, DateOnly DueDate, decimal Budget, string Description, JobStatus Status, long? AcceptedBy, int? OfferCount, bool HasCurrentUserOffer, DateTimeOffset CreatedAt);
/// <summary>Offer data exposed by marketplace queries and commands.</summary>
public sealed record OfferResponse(long Id, long JobId, long ContractorId, string ContractorName, decimal ContractorRating, decimal Price, bool IsOverBudget, DateTimeOffset CreatedAt);
/// <summary>Cursor-paged data exposed by marketplace queries.</summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);

/// <summary>Validated input used when creating or changing a job.</summary>
public sealed record SaveJobCommand(DateOnly StartDate, DateOnly DueDate, decimal Budget, string Description);
/// <summary>Validated input used when creating or changing an offer.</summary>
public sealed record SaveOfferCommand(decimal Price);
/// <summary>Filters accepted by the job listing query.</summary>
public sealed record JobSearch(string? Query, string? Status, string? Scope, decimal? MinBudget, decimal? MaxBudget, DateOnly? DueFrom, DateOnly? DueTo, long? Cursor, int? Limit);

/// <summary>Provides the authenticated marketplace profiles for the current request.</summary>
public interface ICurrentActor
{
    /// <summary>Gets the current customer's profile identifier, if any.</summary>
    Task<long?> CustomerIdAsync(CancellationToken cancellationToken);
    /// <summary>Gets the current contractor's profile identifier, if any.</summary>
    Task<long?> ContractorIdAsync(CancellationToken cancellationToken);
}

/// <summary>Provides read-only customer and contractor queries.</summary>
public interface IProfileQueries
{
    /// <summary>Gets a customer profile by identifier.</summary>
    Task<CustomerResponse?> GetCustomerAsync(long id, CancellationToken cancellationToken);
    /// <summary>Searches customers by last name.</summary>
    Task<OperationResult<CursorPage<CustomerResponse>>> SearchCustomersAsync(string? lastName, long? cursor, int? limit, CancellationToken cancellationToken);
    /// <summary>Gets a contractor profile by identifier.</summary>
    Task<ContractorResponse?> GetContractorAsync(long id, CancellationToken cancellationToken);
    /// <summary>Searches contractors by name.</summary>
    Task<OperationResult<CursorPage<ContractorResponse>>> SearchContractorsAsync(string? name, long? cursor, int? limit, CancellationToken cancellationToken);
}

/// <summary>Provides read-only job and offer queries.</summary>
public interface IJobQueries
{
    /// <summary>Lists jobs using the supplied filters.</summary>
    Task<OperationResult<CursorPage<JobResponse>>> ListJobsAsync(JobSearch search, CancellationToken cancellationToken);
    /// <summary>Gets one job.</summary>
    Task<JobResponse?> GetJobAsync(long jobId, CancellationToken cancellationToken);
    /// <summary>Lists offers visible to the current actor.</summary>
    Task<OperationResult<IReadOnlyList<OfferResponse>>> ListOffersAsync(long jobId, CancellationToken cancellationToken);
}

/// <summary>Handles state-changing job and offer operations.</summary>
public interface IJobCommands
{
    /// <summary>Creates a job for the current customer.</summary>
    Task<OperationResult<JobResponse>> CreateJobAsync(SaveJobCommand command, CancellationToken cancellationToken);
    /// <summary>Updates one job.</summary>
    Task<OperationResult<JobResponse>> UpdateJobAsync(long jobId, SaveJobCommand command, CancellationToken cancellationToken);
    /// <summary>Deletes one job.</summary>
    Task<OperationResult<bool>> DeleteJobAsync(long jobId, CancellationToken cancellationToken);
    /// <summary>Creates an offer.</summary>
    Task<OperationResult<OfferResponse>> CreateOfferAsync(long jobId, SaveOfferCommand command, CancellationToken cancellationToken);
    /// <summary>Updates an offer.</summary>
    Task<OperationResult<OfferResponse>> UpdateOfferAsync(long jobId, long offerId, SaveOfferCommand command, CancellationToken cancellationToken);
    /// <summary>Withdraws an offer.</summary>
    Task<OperationResult<bool>> DeleteOfferAsync(long jobId, long offerId, CancellationToken cancellationToken);
    /// <summary>Awards an offer to the current customer.</summary>
    Task<OperationResult<JobResponse>> AcceptOfferAsync(long jobId, long offerId, CancellationToken cancellationToken);
}

/// <summary>Validates marketplace application inputs independently of HTTP.</summary>
public static class MarketplaceValidation
{
    /// <summary>Returns a job-input error message, or null when the input is valid.</summary>
    public static string? JobError(SaveJobCommand command) => command.StartDate == default || command.DueDate < command.StartDate || command.Budget <= 0 || decimal.Round(command.Budget, 2) != command.Budget || string.IsNullOrWhiteSpace(command.Description)
        ? "Supply valid dates, a positive two-decimal budget, and a description."
        : null;

    /// <summary>Returns an offer-price error message, or null when the price is valid.</summary>
    public static string? OfferPriceError(SaveOfferCommand command) => command.Price <= 0 || decimal.Round(command.Price, 2) != command.Price
        ? "Must be a positive amount with at most two decimal places."
        : null;

    /// <summary>Returns a page-size error message, or null when it is valid.</summary>
    public static string? PageSizeError(int? value) => value is < 1 or > 100 ? "Must be from 1 to 100." : null;

    /// <summary>Returns a search error message, or null when it is valid.</summary>
    public static string? SearchError(string? value) => string.IsNullOrWhiteSpace(value) || value.Trim().Length is < 2 or > 100
        ? "Search text must contain 2 to 100 characters."
        : null;
}
