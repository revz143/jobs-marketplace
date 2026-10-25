using Marketplace.Core;

namespace Marketplace.Api;

/// <summary>Translates marketplace application results into the stable HTTP API contract.</summary>
public static class MarketplaceRouteHandlers
{
    /// <summary>Gets a customer profile.</summary>
    public static async Task<IResult> GetCustomerAsync(long id, IProfileQueries profiles, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetCustomerAsync(id, cancellationToken).ConfigureAwait(false);
        return profile is null ? Results.NotFound() : Results.Ok(profile);
    }

    /// <summary>Searches customer profiles.</summary>
    public static async Task<IResult> SearchCustomersAsync(string? lastName, long? cursor, int? limit, IProfileQueries profiles, CancellationToken cancellationToken) =>
        ToHttp(await profiles.SearchCustomersAsync(lastName, cursor, limit, cancellationToken).ConfigureAwait(false), Results.Ok);

    /// <summary>Gets a contractor profile.</summary>
    public static async Task<IResult> GetContractorAsync(long id, IProfileQueries profiles, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetContractorAsync(id, cancellationToken).ConfigureAwait(false);
        return profile is null ? Results.NotFound() : Results.Ok(profile);
    }

    /// <summary>Searches contractor profiles.</summary>
    public static async Task<IResult> SearchContractorsAsync(string? name, long? cursor, int? limit, IProfileQueries profiles, CancellationToken cancellationToken) =>
        ToHttp(await profiles.SearchContractorsAsync(name, cursor, limit, cancellationToken).ConfigureAwait(false), Results.Ok);

    /// <summary>Lists jobs using the supplied filters.</summary>
    public static async Task<IResult> ListJobsAsync(string? q, string? status, string? scope, decimal? minBudget, decimal? maxBudget, DateOnly? dueFrom, DateOnly? dueTo, long? cursor, int? limit, IJobQueries jobs, CancellationToken cancellationToken) =>
        ToHttp(await jobs.ListJobsAsync(new JobSearch(q, status, scope, minBudget, maxBudget, dueFrom, dueTo, cursor, limit), cancellationToken).ConfigureAwait(false), Results.Ok);

    /// <summary>Gets a job.</summary>
    public static async Task<IResult> GetJobAsync(long jobId, IJobQueries jobs, CancellationToken cancellationToken)
    {
        var job = await jobs.GetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        return job is null ? Results.NotFound() : Results.Ok(job);
    }

    /// <summary>Creates a job.</summary>
    public static async Task<IResult> CreateJobAsync(SaveJobRequest request, IJobCommands jobs, CancellationToken cancellationToken)
    {
        var result = await jobs.CreateJobAsync(new SaveJobCommand(request.StartDate, request.DueDate, request.Budget, request.Description), cancellationToken).ConfigureAwait(false);
        return ToHttp(result, job => Results.Created($"/api/v1/jobs/{job.Id}", job));
    }

    /// <summary>Updates a job.</summary>
    public static async Task<IResult> UpdateJobAsync(long jobId, SaveJobRequest request, IJobCommands jobs, CancellationToken cancellationToken) =>
        ToHttp(await jobs.UpdateJobAsync(jobId, new SaveJobCommand(request.StartDate, request.DueDate, request.Budget, request.Description), cancellationToken).ConfigureAwait(false), Results.Ok);

    /// <summary>Deletes a job.</summary>
    public static async Task<IResult> DeleteJobAsync(long jobId, IJobCommands jobs, CancellationToken cancellationToken) =>
        ToHttp(await jobs.DeleteJobAsync(jobId, cancellationToken).ConfigureAwait(false), _ => Results.NoContent());

    /// <summary>Lists offers visible to the current actor.</summary>
    public static async Task<IResult> ListOffersAsync(long jobId, IJobQueries jobs, CancellationToken cancellationToken) =>
        ToHttp(await jobs.ListOffersAsync(jobId, cancellationToken).ConfigureAwait(false), Results.Ok);

    /// <summary>Creates an offer.</summary>
    public static async Task<IResult> CreateOfferAsync(long jobId, SaveOfferRequest request, IJobCommands jobs, CancellationToken cancellationToken)
    {
        var result = await jobs.CreateOfferAsync(jobId, new SaveOfferCommand(request.Price), cancellationToken).ConfigureAwait(false);
        return ToHttp(result, offer => Results.Created($"/api/v1/jobs/{jobId}/offers/{offer.Id}", offer));
    }

    /// <summary>Updates an offer.</summary>
    public static async Task<IResult> UpdateOfferAsync(long jobId, long offerId, SaveOfferRequest request, IJobCommands jobs, CancellationToken cancellationToken) =>
        ToHttp(await jobs.UpdateOfferAsync(jobId, offerId, new SaveOfferCommand(request.Price), cancellationToken).ConfigureAwait(false), Results.Ok);

    /// <summary>Withdraws an offer.</summary>
    public static async Task<IResult> DeleteOfferAsync(long jobId, long offerId, IJobCommands jobs, CancellationToken cancellationToken) =>
        ToHttp(await jobs.DeleteOfferAsync(jobId, offerId, cancellationToken).ConfigureAwait(false), _ => Results.NoContent());

    /// <summary>Awards an offer.</summary>
    public static async Task<IResult> AcceptOfferAsync(long jobId, long offerId, IJobCommands jobs, CancellationToken cancellationToken) =>
        ToHttp(await jobs.AcceptOfferAsync(jobId, offerId, cancellationToken).ConfigureAwait(false), Results.Ok);

    private static IResult ToHttp<T>(OperationResult<T> result, Func<T, IResult> success) => result.Status switch
    {
        OperationStatus.Success => success(result.Value!),
        OperationStatus.NotFound => Results.NotFound(),
        OperationStatus.Forbidden => Results.Forbid(),
        OperationStatus.Conflict => Results.Conflict(new { title = result.ErrorMessage }),
        OperationStatus.ValidationFailed => Results.ValidationProblem(new Dictionary<string, string[]> { [result.ErrorKey ?? "request"] = [result.ErrorMessage ?? "The request is invalid."] }),
        _ => throw new InvalidOperationException($"Unhandled operation status: {result.Status}.")
    };
}
