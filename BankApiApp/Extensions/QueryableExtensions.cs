using BankApiApp.DTOs.Common;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Extensions;

public static class QueryableExtensions
{
    // istenilen IQueryable-i sehifelenmis neticeye cevirir
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PaginationParams pagination)
    {
        var totalCount = await query.CountAsync();   // umumi say (Skip/Take-den evvel)

        var items = await query
            .Skip((pagination.Page - 1) * pagination.PageSize)   // evvelki sehifeleri kec
            .Take(pagination.PageSize)                            // bu sehifeni gotur
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