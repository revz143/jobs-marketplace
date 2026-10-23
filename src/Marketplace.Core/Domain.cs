namespace Marketplace.Core;

/// <summary>States that determine whether a job can receive offers.</summary>
public enum JobStatus { Open, Awarded }

/// <summary>Application roles used to distinguish marketplace parties.</summary>
public static class AppRoles
{
    /// <summary>Customer role.</summary>
    public const string Customer = "Customer";
    /// <summary>Contractor role.</summary>
    public const string Contractor = "Contractor";
}

/// <summary>Describes a cursor-paged API result.</summary>
public sealed record PageResult<T>(IReadOnlyList<T> Items, string? NextCursor);

/// <summary>Normalizes human-entered search text.</summary>
public static class SearchText
{
    /// <summary>Returns a trimmed, lowercase search key.</summary>
    public static string Normalize(string value) => value.Trim().ToLowerInvariant();
    /// <summary>Escapes SQL LIKE metacharacters using backslash.</summary>
    public static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
