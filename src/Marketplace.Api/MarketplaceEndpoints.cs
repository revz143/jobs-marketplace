using System.Data;
using Marketplace.Core;
using Marketplace.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api;

/// <summary>Registers version-one marketplace REST API endpoints.</summary>
public static class MarketplaceEndpoints
{
    /// <summary>Maps profile, job, and offer endpoints under the supplied route group.</summary>
    public static void MapMarketplaceEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/customers/{id:long}", GetCustomerAsync).RequireAuthorization();
        api.MapGet("/customers", SearchCustomersAsync).RequireAuthorization();
        api.MapGet("/contractors/{id:long}", GetContractorAsync).RequireAuthorization();
        api.MapGet("/contractors", SearchContractorsAsync).RequireAuthorization();
        api.MapGet("/jobs", ListJobsAsync).RequireAuthorization();
        api.MapPost("/jobs", CreateJobAsync).RequireAuthorization(AppRoles.Customer).RequireRateLimiting("write");
        api.MapGet("/jobs/{jobId:long}", GetJobAsync).RequireAuthorization();
        api.MapPut("/jobs/{jobId:long}", UpdateJobAsync).RequireAuthorization(AppRoles.Customer).RequireRateLimiting("write");
        api.MapDelete("/jobs/{jobId:long}", DeleteJobAsync).RequireAuthorization(AppRoles.Customer).RequireRateLimiting("write");
        api.MapGet("/jobs/{jobId:long}/offers", ListOffersAsync).RequireAuthorization();
        api.MapPost("/jobs/{jobId:long}/offers", CreateOfferAsync).RequireAuthorization(AppRoles.Contractor).RequireRateLimiting("write");
        api.MapPut("/jobs/{jobId:long}/offers/{offerId:long}", UpdateOfferAsync).RequireAuthorization(AppRoles.Contractor).RequireRateLimiting("write");
        api.MapDelete("/jobs/{jobId:long}/offers/{offerId:long}", DeleteOfferAsync).RequireAuthorization(AppRoles.Contractor).RequireRateLimiting("write");
        api.MapPost("/jobs/{jobId:long}/offers/{offerId:long}/accept", AcceptOfferAsync).RequireAuthorization(AppRoles.Customer).RequireRateLimiting("write");
    }

    private static async Task<IResult> GetCustomerAsync(long id, MarketplaceDbContext db, IProfileCache cache, CancellationToken ct)
    {
        var response = await cache.GetOrCreateAsync($"customer:{id}", async token => await db.Customers.AsNoTracking().Where(x => x.Id == id).Select(x => new CustomerResponse(x.Id, x.FirstName, x.LastName)).SingleOrDefaultAsync(token).ConfigureAwait(false), ct).ConfigureAwait(false);
        return response is null ? Results.NotFound() : Results.Ok(response);
    }

    private static async Task<IResult> SearchCustomersAsync(string? lastName, long? cursor, int? limit, MarketplaceDbContext db, CancellationToken ct)
    {
        var term = ValidateSearch(lastName); if (term.Error is not null) return term.Error;
        var size = PageSize(limit); if (size.Error is not null) return size.Error; var pageSize = size.Value ?? 20;
        var normalized = SearchText.Normalize(lastName!); var escaped = SearchText.EscapeLike(normalized); var pattern = normalized.Length == 2 ? $"{escaped}%" : $"%{escaped}%";
        var query = db.Customers.AsNoTracking().Where(x => cursor == null || x.Id > cursor).Where(x => EF.Functions.Like(x.LastName.ToLower(), pattern, "\\"));
        var items = await query.OrderBy(x => x.Id).Select(x => new CustomerResponse(x.Id, x.FirstName, x.LastName)).Take(pageSize + 1).ToListAsync(ct).ConfigureAwait(false);
        return Results.Ok(Page(items, pageSize, x => x.Id));
    }

    private static async Task<IResult> GetContractorAsync(long id, MarketplaceDbContext db, IProfileCache cache, CancellationToken ct)
    {
        var response = await cache.GetOrCreateAsync($"contractor:{id}", async token => await db.Contractors.AsNoTracking().Where(x => x.Id == id).Select(x => new ContractorResponse(x.Id, x.Name, x.Rating)).SingleOrDefaultAsync(token).ConfigureAwait(false), ct).ConfigureAwait(false);
        return response is null ? Results.NotFound() : Results.Ok(response);
    }

    private static async Task<IResult> SearchContractorsAsync(string? name, long? cursor, int? limit, MarketplaceDbContext db, CancellationToken ct)
    {
        var term = ValidateSearch(name); if (term.Error is not null) return term.Error;
        var size = PageSize(limit); if (size.Error is not null) return size.Error; var pageSize = size.Value ?? 20;
        var normalized = SearchText.Normalize(name!); var escaped = SearchText.EscapeLike(normalized); var pattern = normalized.Length == 2 ? $"{escaped}%" : $"%{escaped}%";
        var query = db.Contractors.AsNoTracking().Where(x => cursor == null || x.Id > cursor).Where(x => EF.Functions.Like(x.Name.ToLower(), pattern, "\\"));
        var items = await query.OrderBy(x => x.Id).Select(x => new ContractorResponse(x.Id, x.Name, x.Rating)).Take(pageSize + 1).ToListAsync(ct).ConfigureAwait(false);
        return Results.Ok(Page(items, pageSize, x => x.Id));
    }

    private static async Task<IResult> ListJobsAsync(string? q, string? status, decimal? minBudget, decimal? maxBudget, DateOnly? dueFrom, DateOnly? dueTo, long? cursor, int? limit, MarketplaceDbContext db, CancellationToken ct)
    {
        var size = PageSize(limit); if (size.Error is not null) return size.Error; var pageSize = size.Value ?? 20;
        if (minBudget is > 0 && maxBudget is > 0 && minBudget > maxBudget) return Results.ValidationProblem(new Dictionary<string, string[]> { ["maxBudget"] = ["Must be at least minBudget."] });
        var query = db.Jobs.AsNoTracking().Where(x => !x.IsDeleted && (cursor == null || x.Id > cursor));
        if (!string.IsNullOrWhiteSpace(q)) { if (q.Trim().Length > 100) return Results.ValidationProblem(new Dictionary<string, string[]> { ["q"] = ["Must be 100 characters or fewer."] }); query = query.Where(x => EF.Functions.ILike(x.Description, $"%{SearchText.EscapeLike(q.Trim())}%", "\\")); }
        if (Enum.TryParse<JobStatus>(status, true, out var parsed)) query = query.Where(x => x.Status == parsed); else if (!string.IsNullOrWhiteSpace(status)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Use Open or Awarded."] });
        if (minBudget is not null) query = query.Where(x => x.Budget >= minBudget); if (maxBudget is not null) query = query.Where(x => x.Budget <= maxBudget); if (dueFrom is not null) query = query.Where(x => x.DueDate >= dueFrom); if (dueTo is not null) query = query.Where(x => x.DueDate <= dueTo);
        var items = await query.OrderBy(x => x.Id).Select(JobProjection).Take(pageSize + 1).ToListAsync(ct).ConfigureAwait(false);
        return Results.Ok(Page(items, pageSize, x => x.Id));
    }

    private static async Task<IResult> GetJobAsync(long jobId, MarketplaceDbContext db, CancellationToken ct)
    {
        var job = await db.Jobs.AsNoTracking().Where(x => x.Id == jobId && !x.IsDeleted).Select(JobProjection).SingleOrDefaultAsync(ct).ConfigureAwait(false);
        return job is null ? Results.NotFound() : Results.Ok(job);
    }

    private static async Task<IResult> CreateJobAsync(SaveJobRequest request, HttpContext context, ActorResolver actors, MarketplaceDbContext db, CancellationToken ct)
    {
        var error = ValidateJob(request); if (error is not null) return error;
        var customerId = await actors.CustomerIdAsync(context.User, ct); if (customerId is null) return Results.Forbid();
        var job = new Job { CustomerId = customerId.Value, StartDate = request.StartDate, DueDate = request.DueDate, Budget = request.Budget, Description = request.Description.Trim() };
        db.Jobs.Add(job); await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Results.Created($"/api/v1/jobs/{job.Id}", ToResponse(job));
    }

    private static async Task<IResult> UpdateJobAsync(long jobId, SaveJobRequest request, HttpContext context, ActorResolver actors, MarketplaceDbContext db, CancellationToken ct)
    {
        var error = ValidateJob(request); if (error is not null) return error; var customerId = await actors.CustomerIdAsync(context.User, ct); if (customerId is null) return Results.Forbid();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false); var job = await LockedJobAsync(db, jobId, ct); if (job is null || job.IsDeleted) return Results.NotFound(); if (job.CustomerId != customerId || job.Status != JobStatus.Open) return Results.Forbid();
        job.StartDate = request.StartDate; job.DueDate = request.DueDate; job.Budget = request.Budget; job.Description = request.Description.Trim(); job.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct).ConfigureAwait(false); await transaction.CommitAsync(ct).ConfigureAwait(false); return Results.Ok(ToResponse(job));
    }

    private static async Task<IResult> DeleteJobAsync(long jobId, HttpContext context, ActorResolver actors, MarketplaceDbContext db, CancellationToken ct)
    {
        var customerId = await actors.CustomerIdAsync(context.User, ct); if (customerId is null) return Results.Forbid(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false); var job = await LockedJobAsync(db, jobId, ct); if (job is null || job.IsDeleted) return Results.NotFound(); if (job.CustomerId != customerId || job.Status != JobStatus.Open) return Results.Forbid();
        job.IsDeleted = true; job.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct).ConfigureAwait(false); await transaction.CommitAsync(ct).ConfigureAwait(false); return Results.NoContent();
    }

    private static async Task<IResult> ListOffersAsync(long jobId, HttpContext context, ActorResolver actors, MarketplaceDbContext db, CancellationToken ct)
    {
        var job = await db.Jobs.AsNoTracking().Where(x => x.Id == jobId && !x.IsDeleted).Select(x => new { x.CustomerId, x.Budget }).SingleOrDefaultAsync(ct).ConfigureAwait(false); if (job is null) return Results.NotFound();
        var customerId = await actors.CustomerIdAsync(context.User, ct); var contractorId = await actors.ContractorIdAsync(context.User, ct); if (customerId != job.CustomerId && contractorId is null) return Results.Forbid();
        var offers = db.Offers.AsNoTracking().Where(x => x.JobId == jobId && !x.IsDeleted); if (customerId != job.CustomerId) offers = offers.Where(x => x.ContractorId == contractorId);
        return Results.Ok(await offers.OrderBy(x => x.Id).Select(x => new OfferResponse(x.Id, x.JobId, x.ContractorId, x.Price, x.Price > job.Budget, x.CreatedAt)).ToListAsync(ct).ConfigureAwait(false));
    }

    private static async Task<IResult> CreateOfferAsync(long jobId, SaveOfferRequest request, HttpContext context, ActorResolver actors, MarketplaceDbContext db, CancellationToken ct)
    {
        if (request.Price <= 0 || decimal.Round(request.Price, 2) != request.Price) return Results.ValidationProblem(new Dictionary<string, string[]> { ["price"] = ["Must be a positive amount with at most two decimal places."] });
        var contractorId = await actors.ContractorIdAsync(context.User, ct); if (contractorId is null) return Results.Forbid(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false); var job = await LockedJobAsync(db, jobId, ct); if (job is null || job.IsDeleted) return Results.NotFound(); if (job.Status != JobStatus.Open) return Results.Conflict(new { title = "Job is already awarded." });
        if (await db.Offers.AnyAsync(x => x.JobId == jobId && x.ContractorId == contractorId && !x.IsDeleted, ct).ConfigureAwait(false)) return Results.Conflict(new { title = "This contractor already has an active offer." });
        var offer = new JobOffer { JobId = jobId, ContractorId = contractorId.Value, Price = request.Price }; db.Offers.Add(offer); await db.SaveChangesAsync(ct).ConfigureAwait(false); await transaction.CommitAsync(ct).ConfigureAwait(false); return Results.Created($"/api/v1/jobs/{jobId}/offers/{offer.Id}", new OfferResponse(offer.Id, jobId, offer.ContractorId, offer.Price, offer.Price > job.Budget, offer.CreatedAt));
    }

    private static async Task<IResult> UpdateOfferAsync(long jobId, long offerId, SaveOfferRequest request, HttpContext context, ActorResolver actors, MarketplaceDbContext db, CancellationToken ct)
    {
        if (request.Price <= 0 || decimal.Round(request.Price, 2) != request.Price) return Results.ValidationProblem(new Dictionary<string, string[]> { ["price"] = ["Must be a positive amount with at most two decimal places."] });
        var contractorId = await actors.ContractorIdAsync(context.User, ct); if (contractorId is null) return Results.Forbid(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false); var job = await LockedJobAsync(db, jobId, ct); if (job is null || job.IsDeleted) return Results.NotFound(); var offer = await db.Offers.SingleOrDefaultAsync(x => x.Id == offerId && x.JobId == jobId && !x.IsDeleted, ct).ConfigureAwait(false); if (offer is null) return Results.NotFound(); if (job.Status != JobStatus.Open || offer.ContractorId != contractorId) return Results.Forbid(); offer.Price = request.Price; offer.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct).ConfigureAwait(false); await transaction.CommitAsync(ct).ConfigureAwait(false); return Results.Ok(new OfferResponse(offer.Id, offer.JobId, offer.ContractorId, offer.Price, offer.Price > job.Budget, offer.CreatedAt));
    }

    private static async Task<IResult> DeleteOfferAsync(long jobId, long offerId, HttpContext context, ActorResolver actors, MarketplaceDbContext db, CancellationToken ct)
    {
        var contractorId = await actors.ContractorIdAsync(context.User, ct); if (contractorId is null) return Results.Forbid(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false); var job = await LockedJobAsync(db, jobId, ct); if (job is null || job.IsDeleted) return Results.NotFound(); var offer = await db.Offers.SingleOrDefaultAsync(x => x.Id == offerId && x.JobId == jobId && !x.IsDeleted, ct).ConfigureAwait(false); if (offer is null) return Results.NotFound(); if (job.Status != JobStatus.Open || offer.ContractorId != contractorId) return Results.Forbid(); offer.IsDeleted = true; offer.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct).ConfigureAwait(false); await transaction.CommitAsync(ct).ConfigureAwait(false); return Results.NoContent();
    }

    private static async Task<IResult> AcceptOfferAsync(long jobId, long offerId, HttpContext context, ActorResolver actors, MarketplaceDbContext db, CancellationToken ct)
    {
        var customerId = await actors.CustomerIdAsync(context.User, ct); if (customerId is null) return Results.Forbid(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false); var job = await LockedJobAsync(db, jobId, ct); if (job is null || job.IsDeleted) return Results.NotFound(); if (job.CustomerId != customerId) return Results.Forbid();
        if (job.Status == JobStatus.Awarded)
        {
            if (job.AcceptedOfferId != offerId) return Results.Conflict(new { title = "A different offer has already been accepted." });
            var acceptedBy = await db.Offers.Where(x => x.Id == offerId).Select(x => (long?)x.ContractorId).SingleAsync(ct).ConfigureAwait(false);
            return Results.Ok(ToResponse(job, acceptedBy));
        }
        var offer = await db.Offers.SingleOrDefaultAsync(x => x.Id == offerId && x.JobId == jobId && !x.IsDeleted, ct).ConfigureAwait(false); if (offer is null) return Results.NotFound(); job.AcceptedOfferId = offer.Id; job.Status = JobStatus.Awarded; job.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct).ConfigureAwait(false); await transaction.CommitAsync(ct).ConfigureAwait(false); return Results.Ok(ToResponse(job, offer.ContractorId));
    }

    private static async Task<Job?> LockedJobAsync(MarketplaceDbContext db, long id, CancellationToken ct) => await db.Jobs.FromSqlInterpolated($"SELECT * FROM jobs WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct).ConfigureAwait(false);
    private static readonly System.Linq.Expressions.Expression<Func<Job, JobResponse>> JobProjection = x => new JobResponse(x.Id, x.CustomerId, x.StartDate, x.DueDate, x.Budget, x.Description, x.Status, x.AcceptedOffer == null ? null : x.AcceptedOffer.ContractorId, x.CreatedAt);
    private static JobResponse ToResponse(Job job, long? acceptedBy = null) => new(job.Id, job.CustomerId, job.StartDate, job.DueDate, job.Budget, job.Description, job.Status, acceptedBy, job.CreatedAt);
    private static IResult? ValidateJob(SaveJobRequest request) => request.StartDate == default || request.DueDate < request.StartDate || request.Budget <= 0 || decimal.Round(request.Budget, 2) != request.Budget || string.IsNullOrWhiteSpace(request.Description) ? Results.ValidationProblem(new Dictionary<string, string[]> { ["job"] = ["Supply valid dates, a positive two-decimal budget, and a description."] }) : null;
    private static (IResult? Error, int? Value) PageSize(int? value) => value is < 1 or > 100 ? (Results.ValidationProblem(new Dictionary<string, string[]> { ["limit"] = ["Must be from 1 to 100."] }), null) : (null, value ?? 20);
    private static (IResult? Error, string? Value) ValidateSearch(string? value) => string.IsNullOrWhiteSpace(value) || value.Trim().Length is < 2 or > 100 ? (Results.ValidationProblem(new Dictionary<string, string[]> { ["search"] = ["Search text must contain 2 to 100 characters."] }), null) : (null, value);
    private static CursorPage<T> Page<T>(List<T> rows, int size, Func<T, long> id) { var page = rows.Take(size).ToList(); return new CursorPage<T>(page, rows.Count > size ? id(page[^1]).ToString(System.Globalization.CultureInfo.InvariantCulture) : null); }
}
