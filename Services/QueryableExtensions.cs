using Microsoft.EntityFrameworkCore;
using Student_Management_System.Models;

namespace Student_Management_System.Services;

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        ListQuery request,
        CancellationToken cancellationToken = default)
    {
        request.Normalize();
        var total = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)request.PageSize));
        var page = Math.Min(request.Page, totalPages);
        var items = await query
            .Skip((page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = request.PageSize,
            Search = request.Search,
            Sort = request.Sort,
            Direction = request.Direction
        };
    }
}

public static class CsvExport
{
    public static byte[] Create(IEnumerable<string[]> rows)
    {
        static string Escape(string? value)
        {
            value ??= "";
            // Quoting alone does not stop spreadsheet formula execution.
            if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
                value = "'" + value;
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        var text = string.Join(Environment.NewLine, rows.Select(row => string.Join(",", row.Select(Escape))));
        return System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(text)).ToArray();
    }
}
