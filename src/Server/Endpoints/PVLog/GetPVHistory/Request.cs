namespace InverterMon.Server.Endpoints.PVLog.GetPVHistory;

public class Request
{
    public string Period { get; set; } = "week";
    public int Offset { get; set; }
}
