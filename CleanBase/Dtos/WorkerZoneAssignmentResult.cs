namespace CleanBase.Dtos;

public class WorkerZoneAssignmentResult
{
    public Dictionary<string, string> data { get; set; } = new();
    public Dictionary<string, string> error { get; set; } = new();
}
