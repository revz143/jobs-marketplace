using System.Data;
using System.Linq.Expressions;
using Marketplace.Core;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Persistence;

/// <summary>EF Core implementation of focused customer and contractor read operations.</summary>
public sealed class EfProfileQueries(MarketplaceDbContext db, IProfileCache cache) : IProfileQueries
{
    /// <inheritdoc />
    public async Task<CustomerResponse?> GetCustomerAsync(long id, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync($"customer:{id}", token => db.Customers.AsNoTracking().Where(x => x.Id == id).Select(x => new CustomerResponse(x.Id, x.FirstName, x.LastName)).SingleOrDefaultAsync(token), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<OperationResult<CursorPage<CustomerResponse>>> SearchCustomersAsync(string? lastName, long? cursor, int? limit, CancellationToken cancellationToken)
    {
        if (MarketplaceValidation.SearchError(lastName) is { } error) return OperationResult<CursorPage<CustomerResponse>>.Validation("search", error);
        if (MarketplaceValidation.PageSizeError(limit) is { } sizeError) return OperationResult<CursorPage<CustomerResponse>>.Validation("limit", sizeError);

        var pattern = SearchPattern(lastName!);
        var pageSize = limit ?? 20;
        var rows = await db.Customers.AsNoTracking()
            .Where(x => cursor == null || x.Id > cursor)
            .Where(x => EF.Functions.Like(x.LastName.ToLower(), pattern, "\\"))
            .OrderBy(x => x.Id)
            .Select(x => new CustomerResponse(x.Id, x.FirstName, x.LastName))
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return OperationResult<CursorPage<CustomerResponse>>.Success(Page(rows, pageSize, x => x.Id));
    }

    /// <inheritdoc />
    public async Task<ContractorResponse?> GetContractorAsync(long id, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync($"contractor:{id}", token => db.Contractors.AsNoTracking().Where(x => x.Id == id).Select(x => new ContractorResponse(x.Id, x.Name, x.Rating)).SingleOrDefaultAsync(token), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<OperationResult<CursorPage<ContractorResponse>>> SearchContractorsAsync(string? name, long? cursor, int? limit, CancellationToken cancellationToken)
    {
        if (MarketplaceValidation.SearchError(name) is { } error) return OperationResult<CursorPage<ContractorResponse>>.Validation("search", error);
        if (MarketplaceValidation.PageSizeError(limit) is { } sizeError) return OperationResult<CursorPage<ContractorResponse>>.Validation("limit", sizeError);

        var pattern = SearchPattern(name!);
        var pageSize = limit ?? 20;
        var rows = await db.Contractors.AsNoTracking()
            .Where(x => cursor == null || x.Id > cursor)
            .Where(x => EF.Functions.Like(x.Name.ToLower(), pattern, "\\"))
            .OrderBy(x => x.Id)
            .Select(x => new ContractorResponse(x.Id, x.Name, x.Rating))
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return OperationResult<CursorPage<ContractorResponse>>.Success(Page(rows, pageSize, x => x.Id));
    }

    private static string SearchPattern(string value)
    {
        var normalized = SearchText.Normalize(value);
        var escaped = SearchText.EscapeLike(normalized);
        return normalized.Length == 2 ? $"{escaped}%" : $"%{escaped}%";
    }

    private static CursorPage<T> Page<T>(List<T> rows, int size, Func<T, long> id)
    {
        var page = rows.Take(size).ToList();
        return new CursorPage<T>(page, rows.Count > size ? id(page[^1]).ToString(System.Globalization.CultureInfo.InvariantCulture) : null);
    }
}

/// <summary>EF Core implementation of read-only job and offer operations.</summary>
public sealed class EfJobQueries(MarketplaceDbContext db, ICurrentActor currentActor) : IJobQueries
{
    /// <inheritdoc />
    public async Task<OperationResult<CursorPage<JobResponse>>> ListJobsAsync(JobSearch search, CancellationToken cancellationToken)
    {
        if (MarketplaceValidation.PageSizeError(search.Limit) is { } sizeError) return OperationResult<CursorPage<JobResponse>>.Validation("limit", sizeError);
        if (search.MinBudget is > 0 && search.MaxBudget is > 0 && search.MinBudget > search.MaxBudget) return OperationResult<CursorPage<JobResponse>>.Validation("maxBudget", "Must be at least minBudget.");
        if (search.Query?.Trim().Length > 100) return OperationResult<CursorPage<JobResponse>>.Validation("q", "Must be 100 characters or fewer.");
        if (!string.IsNullOrWhiteSpace(search.Scope) && search.Scope is not "all" and not "owned" and not "offered") return OperationResult<CursorPage<JobResponse>>.Validation("scope", "Use all, owned, or offered.");
        if (!string.IsNullOrWhiteSpace(search.Status) && !Enum.TryParse<JobStatus>(search.Status, true, out _)) return OperationResult<CursorPage<JobResponse>>.Validation("status", "Use Open or Awarded.");

        var customerId = await currentActor.CustomerIdAsync(cancellationToken).ConfigureAwait(false);
        var contractorId = await currentActor.ContractorIdAsync(cancellationToken).ConfigureAwait(false);
        var query = db.Jobs.AsNoTracking().Where(x => !x.IsDeleted && (search.Cursor == null || x.Id > search.Cursor));
        if (search.Scope is "owned" && customerId is long ownerId) query = query.Where(x => x.CustomerId == ownerId);
        if (search.Scope is "offered" && contractorId is long offererId) query = query.Where(x => x.Offers.Any(offer => !offer.IsDeleted && offer.ContractorId == offererId));
        if (!string.IsNullOrWhiteSpace(search.Query)) query = query.Where(x => EF.Functions.ILike(x.Description, $"%{SearchText.EscapeLike(search.Query.Trim())}%", "\\"));
        if (Enum.TryParse<JobStatus>(search.Status, true, out var status)) query = query.Where(x => x.Status == status);
        if (search.MinBudget is not null) query = query.Where(x => x.Budget >= search.MinBudget);
        if (search.MaxBudget is not null) query = query.Where(x => x.Budget <= search.MaxBudget);
        if (search.DueFrom is not null) query = query.Where(x => x.DueDate >= search.DueFrom);
        if (search.DueTo is not null) query = query.Where(x => x.DueDate <= search.DueTo);

        var pageSize = search.Limit ?? 20;
        var rows = await query.OrderBy(x => x.Id).Select(JobProjection(customerId, contractorId)).Take(pageSize + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<CursorPage<JobResponse>>.Success(Page(rows, pageSize, x => x.Id));
    }

    /// <inheritdoc />
    public async Task<JobResponse?> GetJobAsync(long jobId, CancellationToken cancellationToken)
    {
        var customerId = await currentActor.CustomerIdAsync(cancellationToken).ConfigureAwait(false);
        var contractorId = await currentActor.ContractorIdAsync(cancellationToken).ConfigureAwait(false);
        return await db.Jobs.AsNoTracking().Where(x => x.Id == jobId && !x.IsDeleted).Select(JobProjection(customerId, contractorId)).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<OfferResponse>>> ListOffersAsync(long jobId, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.AsNoTracking().Where(x => x.Id == jobId && !x.IsDeleted).Select(x => new { x.CustomerId, x.Budget }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (job is null) return OperationResult<IReadOnlyList<OfferResponse>>.NotFound();

        var customerId = await currentActor.CustomerIdAsync(cancellationToken).ConfigureAwait(false);
        var contractorId = await currentActor.ContractorIdAsync(cancellationToken).ConfigureAwait(false);
        if (customerId != job.CustomerId && contractorId is null) return OperationResult<IReadOnlyList<OfferResponse>>.Forbidden();

        var query = db.Offers.AsNoTracking().Where(x => x.JobId == jobId && !x.IsDeleted);
        if (customerId != job.CustomerId) query = query.Where(x => x.ContractorId == contractorId);
        var offers = await query.OrderBy(x => x.Id).Select(OfferProjection(job.Budget)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<IReadOnlyList<OfferResponse>>.Success(offers);
    }

    private static Expression<Func<Job, JobResponse>> JobProjection(long? customerId, long? contractorId) => x => new JobResponse(x.Id, x.CustomerId, x.StartDate, x.DueDate, x.Budget, x.Description, x.Status, x.AcceptedOffer == null ? null : x.AcceptedOffer.ContractorId, customerId != null && x.CustomerId == customerId ? x.Offers.Count(offer => !offer.IsDeleted) : null, contractorId != null && x.Offers.Any(offer => !offer.IsDeleted && offer.ContractorId == contractorId), x.CreatedAt);
    private static Expression<Func<JobOffer, OfferResponse>> OfferProjection(decimal budget) => x => new OfferResponse(x.Id, x.JobId, x.ContractorId, x.Contractor.Name, x.Contractor.Rating, x.Price, x.Price > budget, x.CreatedAt);
    private static CursorPage<T> Page<T>(List<T> rows, int size, Func<T, long> id) { var page = rows.Take(size).ToList(); return new CursorPage<T>(page, rows.Count > size ? id(page[^1]).ToString(System.Globalization.CultureInfo.InvariantCulture) : null); }
}

/// <summary>EF Core implementation of transactional job and offer state changes.</summary>
public sealed class EfJobCommands(MarketplaceDbContext db, ICurrentActor currentActor, TimeProvider timeProvider) : IJobCommands
{
    /// <inheritdoc />
    public async Task<OperationResult<JobResponse>> CreateJobAsync(SaveJobCommand command, CancellationToken cancellationToken)
    {
        if (MarketplaceValidation.JobError(command) is { } error) return OperationResult<JobResponse>.Validation("job", error);
        var customerId = await currentActor.CustomerIdAsync(cancellationToken).ConfigureAwait(false);
        if (customerId is null) return OperationResult<JobResponse>.Forbidden();
        var job = Job.Create(customerId.Value, command, timeProvider.GetUtcNow());
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<JobResponse>.Success(ToResponse(job));
    }

    /// <inheritdoc />
    public async Task<OperationResult<JobResponse>> UpdateJobAsync(long jobId, SaveJobCommand command, CancellationToken cancellationToken)
    {
        if (MarketplaceValidation.JobError(command) is { } error) return OperationResult<JobResponse>.Validation("job", error);
        var customerId = await currentActor.CustomerIdAsync(cancellationToken).ConfigureAwait(false);
        if (customerId is null) return OperationResult<JobResponse>.Forbidden();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        var job = await LockedJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null || job.IsDeleted) return OperationResult<JobResponse>.NotFound();
        if (job.CustomerId != customerId || job.Status != JobStatus.Open) return OperationResult<JobResponse>.Forbidden();
        job.Update(command, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<JobResponse>.Success(ToResponse(job));
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> DeleteJobAsync(long jobId, CancellationToken cancellationToken)
    {
        var customerId = await currentActor.CustomerIdAsync(cancellationToken).ConfigureAwait(false);
        if (customerId is null) return OperationResult<bool>.Forbidden();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        var job = await LockedJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null || job.IsDeleted) return OperationResult<bool>.NotFound();
        if (job.CustomerId != customerId || job.Status != JobStatus.Open) return OperationResult<bool>.Forbidden();
        job.Delete(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<OperationResult<OfferResponse>> CreateOfferAsync(long jobId, SaveOfferCommand command, CancellationToken cancellationToken)
    {
        if (MarketplaceValidation.OfferPriceError(command) is { } error) return OperationResult<OfferResponse>.Validation("price", error);
        var contractorId = await currentActor.ContractorIdAsync(cancellationToken).ConfigureAwait(false);
        if (contractorId is null) return OperationResult<OfferResponse>.Forbidden();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        var job = await LockedJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null || job.IsDeleted) return OperationResult<OfferResponse>.NotFound();
        if (job.Status != JobStatus.Open) return OperationResult<OfferResponse>.Conflict("Job is already awarded.");
        if (await db.Offers.AnyAsync(x => x.JobId == jobId && x.ContractorId == contractorId && !x.IsDeleted, cancellationToken).ConfigureAwait(false)) return OperationResult<OfferResponse>.Conflict("This contractor already has an active offer.");

        var offer = JobOffer.Create(jobId, contractorId.Value, command, timeProvider.GetUtcNow());
        db.Offers.Add(offer);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<OfferResponse>.Success(await ProjectOfferAsync(offer.Id, job.Budget, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<OperationResult<OfferResponse>> UpdateOfferAsync(long jobId, long offerId, SaveOfferCommand command, CancellationToken cancellationToken)
    {
        if (MarketplaceValidation.OfferPriceError(command) is { } error) return OperationResult<OfferResponse>.Validation("price", error);
        var contractorId = await currentActor.ContractorIdAsync(cancellationToken).ConfigureAwait(false);
        if (contractorId is null) return OperationResult<OfferResponse>.Forbidden();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        var job = await LockedJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null || job.IsDeleted) return OperationResult<OfferResponse>.NotFound();
        var offer = await db.Offers.SingleOrDefaultAsync(x => x.Id == offerId && x.JobId == jobId && !x.IsDeleted, cancellationToken).ConfigureAwait(false);
        if (offer is null) return OperationResult<OfferResponse>.NotFound();
        if (job.Status != JobStatus.Open || offer.ContractorId != contractorId) return OperationResult<OfferResponse>.Forbidden();
        offer.ChangePrice(command, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<OfferResponse>.Success(await ProjectOfferAsync(offer.Id, job.Budget, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> DeleteOfferAsync(long jobId, long offerId, CancellationToken cancellationToken)
    {
        var contractorId = await currentActor.ContractorIdAsync(cancellationToken).ConfigureAwait(false);
        if (contractorId is null) return OperationResult<bool>.Forbidden();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        var job = await LockedJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null || job.IsDeleted) return OperationResult<bool>.NotFound();
        var offer = await db.Offers.SingleOrDefaultAsync(x => x.Id == offerId && x.JobId == jobId && !x.IsDeleted, cancellationToken).ConfigureAwait(false);
        if (offer is null) return OperationResult<bool>.NotFound();
        if (job.Status != JobStatus.Open || offer.ContractorId != contractorId) return OperationResult<bool>.Forbidden();
        offer.Withdraw(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<OperationResult<JobResponse>> AcceptOfferAsync(long jobId, long offerId, CancellationToken cancellationToken)
    {
        var customerId = await currentActor.CustomerIdAsync(cancellationToken).ConfigureAwait(false);
        if (customerId is null) return OperationResult<JobResponse>.Forbidden();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        var job = await LockedJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null || job.IsDeleted) return OperationResult<JobResponse>.NotFound();
        if (job.CustomerId != customerId) return OperationResult<JobResponse>.Forbidden();
        if (job.Status == JobStatus.Awarded)
        {
            if (job.AcceptedOfferId != offerId) return OperationResult<JobResponse>.Conflict("A different offer has already been accepted.");
            var acceptedBy = await db.Offers.Where(x => x.Id == offerId).Select(x => (long?)x.ContractorId).SingleAsync(cancellationToken).ConfigureAwait(false);
            return OperationResult<JobResponse>.Success(ToResponse(job, acceptedBy));
        }

        var offer = await db.Offers.SingleOrDefaultAsync(x => x.Id == offerId && x.JobId == jobId && !x.IsDeleted, cancellationToken).ConfigureAwait(false);
        if (offer is null) return OperationResult<JobResponse>.NotFound();
        job.Award(offer, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult<JobResponse>.Success(ToResponse(job, offer.ContractorId));
    }

    private Task<Job?> LockedJobAsync(long id, CancellationToken cancellationToken) => db.Jobs.FromSqlInterpolated($"SELECT * FROM jobs WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
    private Task<OfferResponse> ProjectOfferAsync(long offerId, decimal budget, CancellationToken cancellationToken) => db.Offers.AsNoTracking().Where(x => x.Id == offerId).Select(OfferProjection(budget)).SingleAsync(cancellationToken);
    private static Expression<Func<JobOffer, OfferResponse>> OfferProjection(decimal budget) => x => new OfferResponse(x.Id, x.JobId, x.ContractorId, x.Contractor.Name, x.Contractor.Rating, x.Price, x.Price > budget, x.CreatedAt);
    private static JobResponse ToResponse(Job job, long? acceptedBy = null) => new(job.Id, job.CustomerId, job.StartDate, job.DueDate, job.Budget, job.Description, job.Status, acceptedBy, null, false, job.CreatedAt);
}
