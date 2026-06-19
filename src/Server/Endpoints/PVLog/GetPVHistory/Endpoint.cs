using InverterMon.Server.Persistance;
using InverterMon.Server.Persistance.PVGen;
using InverterMon.Shared.Models;

namespace InverterMon.Server.Endpoints.PVLog.GetPVHistory;

public class Endpoint : Endpoint<Request, PVHistory>
{
    public Database Db { get; set; }

    public override void Configure()
    {
        Get("/pv-log/get-pv-history/{Period}/{Offset}");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        var period = r.Period?.ToLowerInvariant() switch
        {
            "month" => "month",
            "year" => "year",
            _ => "week"
        };

        var offset = Math.Max(0, r.Offset);
        var today = DateOnly.FromDateTime(DateTime.Now);

        // calendar-aligned ranges: week starts Monday, month on the 1st, year on Jan 1
        DateOnly startDate, endDate;
        switch (period)
        {
            case "month":
                startDate = new DateOnly(today.Year, today.Month, 1).AddMonths(-offset);
                endDate = startDate.AddMonths(1).AddDays(-1);
                break;
            case "year":
                startDate = new DateOnly(today.Year - offset, 1, 1);
                endDate = new DateOnly(today.Year - offset, 12, 31);
                break;
            default: // week
                var weekRef = today.AddDays(-offset * 7);
                var daysSinceMonday = ((int)weekRef.DayOfWeek + 6) % 7;
                startDate = weekRef.AddDays(-daysSinceMonday);
                endDate = startDate.AddDays(6);
                break;
        }

        var startDay = startDate.DayNumber;
        var endDay = endDate.DayNumber;

        var records = Db.GetPvGenForRange(startDay, endDay).ToDictionary(g => g.Id);

        if (Env.IsDevelopment() && records.Values.All(g => g.TotalWattHours <= 0))
        {
            var lastFillDay = Math.Min(endDay, today.DayNumber); // don't fabricate future days
            for (var d = startDay; d <= lastFillDay; d++)
            {
                records[d] = new PVGeneration
                {
                    Id = d,
                    TotalWattHours = Random.Shared.Next(1000, 9000)
                };
            }
        }

        Response.Period = period;
        Response.Offset = offset;
        Response.IsCurrentPeriod = offset == 0;
        Response.RangeName = period switch
        {
            "year" => startDate.ToString("yyyy"),
            "month" => startDate.ToString("yyyy.MM"),
            _ => $"{startDate:yyyy.MM.dd} - {endDate:yyyy.MM.dd}"
        };

        var dailyKwh = records.Values
            .Where(g => g.TotalWattHours > 0)
            .Select(g => new PVHistory.DayKwh
            {
                DayNumber = g.Id,
                Label = DateOnly.FromDayNumber(g.Id).ToString("yyyy.MM.dd"),
                KiloWattHours = Math.Round(g.TotalWattHours / 1000, 2)
            })
            .ToList();

        Response.DaysRecorded = dailyKwh.Count;
        Response.TotalKiloWattHours = Math.Round(dailyKwh.Sum(d => d.KiloWattHours), 2);
        Response.AverageKiloWattHours = dailyKwh.Count > 0
            ? Math.Round(Response.TotalKiloWattHours / dailyKwh.Count, 2)
            : 0;

        var best = dailyKwh.MaxBy(d => d.KiloWattHours);
        Response.BestDayKiloWattHours = best?.KiloWattHours ?? 0;
        Response.BestDayNumber = best?.DayNumber ?? 0;
        Response.BestDayName = best is not null
            ? DateOnly.FromDayNumber(best.DayNumber).ToString("yyyy.MM.dd")
            : "—";

        Response.BestDays = dailyKwh
            .OrderByDescending(d => d.KiloWattHours)
            .Take(5)
            .Select(d => new PVHistory.DayKwh
            {
                DayNumber = d.DayNumber,
                Label = DateOnly.FromDayNumber(d.DayNumber).ToString("yyyy.MM.dd"),
                KiloWattHours = d.KiloWattHours
            })
            .ToList();

        Response.Points = period == "year"
            ? BuildMonthlyPoints(startDate, endDate, records)
            : BuildDailyPoints(startDay, endDay, records);

        return Task.CompletedTask;
    }

    static IEnumerable<PVHistory.DayKwh> BuildDailyPoints(
        int startDay, int endDay, IReadOnlyDictionary<int, PVGeneration> records)
    {
        var points = new List<PVHistory.DayKwh>();

        for (var d = startDay; d <= endDay; d++)
        {
            records.TryGetValue(d, out var g);
            points.Add(new PVHistory.DayKwh
            {
                DayNumber = d,
                Label = DateOnly.FromDayNumber(d).ToString("yyyy.MM.dd"),
                KiloWattHours = g is null ? 0 : Math.Round(g.TotalWattHours / 1000, 2)
            });
        }

        return points;
    }

    static IEnumerable<PVHistory.DayKwh> BuildMonthlyPoints(
        DateOnly startDate, DateOnly endDate, IReadOnlyDictionary<int, PVGeneration> records)
    {
        var totals = records.Values
            .GroupBy(g => new DateOnly(DateOnly.FromDayNumber(g.Id).Year, DateOnly.FromDayNumber(g.Id).Month, 1))
            .ToDictionary(grp => grp.Key, grp => grp.Sum(g => g.TotalWattHours));

        var points = new List<PVHistory.DayKwh>();
        var month = new DateOnly(startDate.Year, startDate.Month, 1);
        var lastMonth = new DateOnly(endDate.Year, endDate.Month, 1);

        while (month <= lastMonth)
        {
            totals.TryGetValue(month, out var wattHours);
            points.Add(new PVHistory.DayKwh
            {
                DayNumber = month.DayNumber,
                Label = month.ToString("yyyy.MM"),
                KiloWattHours = Math.Round(wattHours / 1000, 2)
            });
            month = month.AddMonths(1);
        }

        return points;
    }
}
