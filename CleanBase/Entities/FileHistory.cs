namespace CleanBase.Entities;

public class FileHistory : EntityRoot
{
    public string Name { get; set; }
    public DateTime ProcessedAt { get; set; }
}