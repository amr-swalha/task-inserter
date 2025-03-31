using CleanBase.Dtos;

namespace CleanBase.Entities;

public class WorkerZoneAssignment : EntityRoot
{
    public int WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public int ZoneId { get; set; }
    public Zone? Zone { get; set; }
    public DateTime EffectiveDate { get; set; }
    public WorkerZoneAssignmentFileDto ExtraDetails { get; set; }
}
