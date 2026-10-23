using System.Security.Claims;
using Marketplace.Core;
using Marketplace.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api;

/// <summary>Resolves the profile that belongs to an authenticated Identity user.</summary>
public sealed class ActorResolver(MarketplaceDbContext db)
{
    /// <summary>Gets the current customer profile identifier, or null when unavailable.</summary>
    public async Task<long?> CustomerIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = UserIdForRole(user, AppRoles.Customer);
        return userId is null ? null : await db.Customers.Where(x => x.UserId == userId).Select(x => (long?)x.Id).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
    /// <summary>Gets the current contractor profile identifier, or null when unavailable.</summary>
    public async Task<long?> ContractorIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = UserIdForRole(user, AppRoles.Contractor);
        return userId is null ? null : await db.Contractors.Where(x => x.UserId == userId).Select(x => (long?)x.Id).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? UserIdForRole(ClaimsPrincipal user, string role)
    {
        if (!user.IsInRole(role)) return null;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userId) ? null : userId;
    }
}
