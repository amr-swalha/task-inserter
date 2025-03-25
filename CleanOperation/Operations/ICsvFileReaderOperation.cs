namespace CleanOperation.Operations;

/// <summary>
/// This class is used to as a proxy for csv operations
/// </summary>
public interface ICsvFileReaderOperation : ICleanOperation
{
    IEnumerable<T> ReadFile<T>(StreamReader stream);
}
