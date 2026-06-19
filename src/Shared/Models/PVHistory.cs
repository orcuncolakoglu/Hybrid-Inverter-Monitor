using System.Text.Json.Serialization;

namespace InverterMon.Shared.Models;

public class PVHistory
{
    public string Period { get; set; } = "week";
    public int Offset { get; set; }
    public bool IsCurrentPeriod { get; set; }
    public string RangeName { get; set; }
    public decimal TotalKiloWattHours { get; set; }
    public decimal AverageKiloWattHours { get; set; }
    public decimal BestDayKiloWattHours { get; set; }
    public int BestDayNumber { get; set; }
    public string BestDayName { get; set; }
    public int DaysRecorded { get; set; }
    public IEnumerable<DayKwh> Points { get; set; } = Enumerable.Empty<DayKwh>();
    public IEnumerable<DayKwh> BestDays { get; set; } = Enumerable.Empty<DayKwh>();

    public class DayKwh
    {
        public int DayNumber { get; set; }

        [JsonPropertyName("Label")]
        public string Label { get; set; }

        [JsonPropertyName("kWh")]
        public decimal KiloWattHours { get; set; }
    }
}
