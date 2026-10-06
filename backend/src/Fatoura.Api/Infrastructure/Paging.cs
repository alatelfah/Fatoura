using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Infrastructure;

public sealed record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public static class Paging
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, int? page, int? pageSize, CancellationToken ct)
    {
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var number = Math.Max(page ?? 1, 1);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((number - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<T>(items, total, number, size);
    }

    /// <summary>SQL LIKE pattern with wildcards escaped, for "contains" searches.</summary>
    public static string Like(string term) =>
        "%" + term.Trim().Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal).Replace("_", "[_]", StringComparison.Ordinal) + "%";
}
