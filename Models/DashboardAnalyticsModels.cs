using System.ComponentModel.DataAnnotations;

namespace Student_Management_System.Models;

public sealed class DashboardFilterInput
{
    [MaxLength(24)] public string Period { get; set; } = "30d";
    [DataType(DataType.Date)] public DateTime? From { get; set; }
    [DataType(DataType.Date)] public DateTime? To { get; set; }
    public int? CampusId { get; set; }
    public int? DepartmentId { get; set; }
    public int? ClassId { get; set; }
    public int? SubjectId { get; set; }
    [MaxLength(40)] public string? Status { get; set; }
    public bool Compare { get; set; } = true;
}

public sealed record DashboardDateRange(
    DateTime FromUtc,
    DateTime ToExclusiveUtc,
    DateTime PreviousFromUtc,
    DateTime PreviousToExclusiveUtc,
    string Label)
{
    public int Days => Math.Max(1, (int)(ToExclusiveUtc - FromUtc).TotalDays);
}

public sealed record DashboardOption(string Value, string Label);

public sealed record DashboardKpi(
    string Title,
    string Value,
    string Icon,
    string Tone,
    string Explanation,
    decimal? Current = null,
    decimal? Previous = null,
    decimal? ChangePercent = null,
    string? DrillDownUrl = null,
    string Format = "number");

public sealed class DashboardChartDataset
{
    public string Label { get; init; } = "";
    public IReadOnlyList<decimal?> Values { get; init; } = [];
    public string? Type { get; init; }
    public string? Axis { get; init; }
    public string? Tone { get; init; }
    public bool Fill { get; init; }
}

public sealed class DashboardChart
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Type { get; init; } = "bar";
    public string Format { get; init; } = "number";
    public string Unit { get; init; } = "";
    public IReadOnlyList<string> Labels { get; init; } = [];
    public IReadOnlyList<DashboardChartDataset> Datasets { get; init; } = [];
    public string EmptyMessage { get; init; } = "No data is available for this period.";
    public string AccessibleSummary { get; init; } = "";
    public string? DrillDownUrl { get; init; }
    public bool Stacked { get; init; }
    public bool Horizontal { get; init; }
    public bool ShowTable { get; init; } = true;
    public bool IsEmpty => Datasets.Count == 0 || Datasets.All(d => d.Values.All(v => !v.HasValue || v == 0));
}

public sealed record DashboardRiskItem(
    string Label,
    string Reason,
    string Tone,
    string? Url = null);

public sealed class RoleDashboardViewModel
{
    public string Role { get; init; } = "";
    public string Eyebrow { get; init; } = "Analytics";
    public string Title { get; init; } = "Dashboard";
    public string Description { get; init; } = "";
    public string ScopeDescription { get; init; } = "";
    public DateTime RefreshedUtc { get; init; } = DateTime.UtcNow;
    public DashboardFilterInput Filter { get; init; } = new();
    public DashboardDateRange Range { get; init; } = new(
        DateTime.UtcNow.Date.AddDays(-29), DateTime.UtcNow.Date.AddDays(1),
        DateTime.UtcNow.Date.AddDays(-59), DateTime.UtcNow.Date.AddDays(-29), "Last 30 days");
    public IReadOnlyList<DashboardOption> Campuses { get; init; } = [];
    public IReadOnlyList<DashboardOption> Departments { get; init; } = [];
    public IReadOnlyList<DashboardOption> Classes { get; init; } = [];
    public IReadOnlyList<DashboardOption> Subjects { get; init; } = [];
    public IReadOnlyList<DashboardKpi> Kpis { get; init; } = [];
    public IReadOnlyList<DashboardChart> Charts { get; init; } = [];
    public IReadOnlyList<DashboardRiskItem> Alerts { get; init; } = [];
    public IReadOnlyList<DashboardRiskItem> Timeline { get; init; } = [];
    public string? PrimaryActionUrl { get; init; }
    public string? PrimaryActionLabel { get; init; }
}

public static class DashboardMath
{
    public static decimal Percentage(decimal numerator, decimal denominator) =>
        denominator == 0 ? 0 : Math.Round(numerator * 100m / denominator, 1, MidpointRounding.AwayFromZero);

    public static decimal? ChangePercent(decimal current, decimal previous) =>
        previous == 0 ? current == 0 ? 0 : null :
        Math.Round((current - previous) * 100m / Math.Abs(previous), 1, MidpointRounding.AwayFromZero);

    public static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static DashboardDateRange ResolveRange(DashboardFilterInput input, DateTime utcNow)
    {
        var today = utcNow.Date;
        DateTime from;
        DateTime toExclusive;
        string label;
        if (input.From.HasValue || input.To.HasValue)
        {
            from = (input.From ?? input.To ?? today).Date;
            var to = (input.To ?? input.From ?? today).Date;
            if (to < from) (from, to) = (to, from);
            if ((to - from).TotalDays > 365) to = from.AddDays(365);
            toExclusive = to.AddDays(1);
            label = $"{from:dd MMM yyyy} – {to:dd MMM yyyy}";
        }
        else
        {
            (from, toExclusive, label) = input.Period.ToLowerInvariant() switch
            {
                "today" => (today, today.AddDays(1), "Today"),
                "7d" => (today.AddDays(-6), today.AddDays(1), "Last 7 days"),
                "month" => (new DateTime(today.Year, today.Month, 1), today.AddDays(1), "Current month"),
                "year" => (new DateTime(today.Year, 1, 1), today.AddDays(1), "Current year"),
                _ => (today.AddDays(-29), today.AddDays(1), "Last 30 days")
            };
        }
        var length = toExclusive - from;
        return new DashboardDateRange(from, toExclusive, from - length, from, label);
    }
}
