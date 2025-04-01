namespace CleanBase.Entities;

public class FileHistory : EntityRoot
{
    public string Name { get; set; }
    public DateTime ProcessedAt { get; set; }
    public int ProcessedRecords { get; set; }
    public int InsertedRecords { get; set; }
}