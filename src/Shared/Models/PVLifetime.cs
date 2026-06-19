namespace InverterMon.Shared.Models;

public class PVLifetime
{
    public decimal TotalKiloWattHours { get; set; }
    public int DaysRecorded { get; set; }
    public decimal AverageKiloWattHours { get; set; }
    public decimal BestDayKiloWattHours { get; set; }
    public int BestDayNumber { get; set; }
    public string BestDayName { get; set; } = "—";
    public string FirstDayName { get; set; } = "—";
    public string LastDayName { get; set; } = "—";
}
