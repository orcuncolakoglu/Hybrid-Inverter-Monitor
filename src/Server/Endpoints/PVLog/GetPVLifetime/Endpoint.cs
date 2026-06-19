using InverterMon.Server.Persistance;
using InverterMon.Server.Persistance.PVGen;
using InverterMon.Shared.Models;

namespace InverterMon.Server.Endpoints.PVLog.GetPVLifetime;

public class Endpoint : EndpointWithoutRequest<PVLifetime>
{
    public Database Db { get; set; }

    public override void Configure()
    {
        Get("/pv-log/get-pv-lifetime");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken c)
    {
        var records = Db.GetAllPvGen().Where(g => g.TotalWattHours > 0).ToList();

        if (Env.IsDevelopment() && records.Count == 0)
        {
            var today = DateOnly.FromDateTime(DateTime.Now).DayNumber;
            for (var i = 0; i < 45; i++)
                records.Add(new PVGeneration { Id = today - i, TotalWattHours = Random.Shared.Next(1000, 9000) });
        }

        if (records.Count == 0)
            return Task.CompletedTask;

        var best = records.MaxBy(g => g.TotalWattHours)!;

        Response.DaysRecorded = records.Count;
        Response.TotalKiloWattHours = Math.Round(records.Sum(g => g.TotalWattHours) / 1000, 2);
        Response.AverageKiloWattHours = Math.Round(Response.TotalKiloWattHours / records.Count, 2);
        Response.BestDayKiloWattHours = Math.Round(best.TotalWattHours / 1000, 2);
        Response.BestDayNumber = best.Id;
        Response.BestDayName = DateOnly.FromDayNumber(best.Id).ToString("yyyy.MM.dd");
        Response.FirstDayName = DateOnly.FromDayNumber(records.Min(g => g.Id)).ToString("yyyy.MM.dd");
        Response.LastDayName = DateOnly.FromDayNumber(records.Max(g => g.Id)).ToString("yyyy.MM.dd");

        return Task.CompletedTask;
    }
}
