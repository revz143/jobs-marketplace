using Marketplace.Core;
using Marketplace.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api;

/// <summary>Initializes the local PostgreSQL schema and idempotent demonstration data.</summary>
public sealed class DatabaseInitializer(MarketplaceDbContext db, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, ILogger<DatabaseInitializer> logger)
{
    /// <summary>Creates the schema, indexes, roles, and deterministic presentation data.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm; CREATE INDEX IF NOT EXISTS ix_customers_last_name_trgm ON customers USING gin (lower(last_name) gin_trgm_ops); CREATE INDEX IF NOT EXISTS ix_contractors_name_trgm ON contractors USING gin (lower(name) gin_trgm_ops);", cancellationToken).ConfigureAwait(false);
        foreach (var role in new[] { AppRoles.Customer, AppRoles.Contractor })
            if (!await roles.RoleExistsAsync(role).ConfigureAwait(false)) await roles.CreateAsync(new IdentityRole(role)).ConfigureAwait(false);
        if (await db.Customers.AnyAsync(cancellationToken).ConfigureAwait(false)) return;

        var customerUsers = await CreateDemoUsersAsync("customer", AppRoles.Customer, cancellationToken).ConfigureAwait(false);
        var contractorUsers = await CreateDemoUsersAsync("contractor", AppRoles.Contractor, cancellationToken).ConfigureAwait(false);
        var firstNames = new[] { "Mia", "Noah", "Ava", "Ethan", "Sofia", "Lucas", "Isla", "Mateo", "Hana", "Theo" };
        var lastNames = new[] { "Smith", "Santos", "Reyes", "Garcia", "Khan", "Dela Cruz", "Tan", "Wilson", "Lim", "Bautista" };
        var businessTerms = new[] { "Northline", "Riverstone", "Atlas", "Brightworks", "Harbor", "Civic", "Lumen", "Pioneer", "Foundry", "Solis" };
        var customers = Enumerable.Range(0, 200).Select(i => new Customer { FirstName = firstNames[i % firstNames.Length], LastName = lastNames[(i / firstNames.Length) % lastNames.Length], UserId = i < customerUsers.Count ? customerUsers[i].Id : null }).ToList();
        var contractors = Enumerable.Range(0, 50).Select(i => new Contractor { Name = $"{businessTerms[i % businessTerms.Length]} {new[] { "Build", "Works", "Studio", "Services", "Contracting" }[i % 5]}", Rating = 3m + (i % 21) / 10m, UserId = i < contractorUsers.Count ? contractorUsers[i].Id : null }).ToList();
        db.AddRange(customers); db.AddRange(contractors); await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var jobs = Enumerable.Range(0, 120).Select(i => Job.Create(customers[i % customers.Count].Id, new SaveJobCommand(today.AddDays(i % 14), today.AddDays(14 + i % 45), 10000m + (i % 12) * 5000m, $"{new[] { "Office refresh", "Electrical inspection", "Kitchen fit-out", "Facade repair", "Workshop extension", "Roof assessment" }[i % 6]} — presentation job #{i + 1}."), now)).ToList();
        db.AddRange(jobs); await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var offers = Enumerable.Range(0, 350).Select(i => JobOffer.Create(jobs[i % jobs.Count].Id, contractors[(i / jobs.Count + i) % contractors.Count].Id, new SaveOfferCommand(9000m + (i % 20) * 2750m), now)).GroupBy(x => new { x.JobId, x.ContractorId }).Select(x => x.First()).ToList();
        db.AddRange(offers); await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Seeded {Customers} customers, {Contractors} contractors, {Jobs} jobs and {Offers} offers.", customers.Count, contractors.Count, jobs.Count, offers.Count);
    }

    private async Task<List<ApplicationUser>> CreateDemoUsersAsync(string prefix, string role, CancellationToken cancellationToken)
    {
        var result = new List<ApplicationUser>();
        for (var i = 1; i <= 2; i++)
        {
            var email = $"{prefix}{i}@marketplace.test";
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var created = await users.CreateAsync(user, "Demo!12345").ConfigureAwait(false);
            if (!created.Succeeded) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(x => x.Description)));
            var added = await users.AddToRoleAsync(user, role).ConfigureAwait(false);
            if (!added.Succeeded) throw new InvalidOperationException(string.Join("; ", added.Errors.Select(x => x.Description)));
            result.Add(user);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
