using BankApiApp.DTOs.Common;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Extensions;

public static class QueryableExtensions
{
    // istənilən IQueryable-ı səhifələnmiş nəticəyə çevirir
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PaginationParams pagination)
    {
        var totalCount = await query.CountAsync();   // ümumi say (Skip/Take-dən əvvəl)

        var items = await query
            .Skip((pagination.Page - 1) * pagination.PageSize)   // əvvəlki səhifələri keç
            .Take(pagination.PageSize)                            // bu səhifəni götür
            .ToListAsync();

        return new PagedResult<T>
        {
            Items = items,
            Page = pagination.Page,
            PageSize = pagination.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pagination.PageSize)
        };
    }
}