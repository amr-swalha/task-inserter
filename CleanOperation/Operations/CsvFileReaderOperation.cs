using CsvHelper;
using System.Globalization;

namespace CleanOperation.Operations;

public class CsvFileReaderOperation : ICsvFileReaderOperation, IDisposable
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public IEnumerable<T> ReadFile<T>(StreamReader stream)
    {
        using var csv = new CsvReader(stream, CultureInfo.InvariantCulture);
        return csv.GetRecords<T>();
    }
}
