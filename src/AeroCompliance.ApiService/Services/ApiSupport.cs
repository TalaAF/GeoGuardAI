using AeroCompliance.Contracts.Dtos.Common;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.ApiService.Services;

public static class ApiSupport
{
    public static bool IsValidPagination(int page, int pageSize) => page >= 1 && pageSize is >= 1 and <= 100;

    public static string? NormalizeSearch(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    public static async Task<PagedResponse<T>> ToPagedAsync<T>(this IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResponse<T>(items, page, pageSize, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }
}
