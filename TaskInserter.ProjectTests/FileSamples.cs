using System.Globalization;
using Bogus;
using CsvHelper;

namespace TaskInserter.ProjectTests;

public class FileSamples
{
    [Fact]
    public void GenerateRandom50K()
    {
        Faker<WorkerZoneAssignmentFileDto> faker = new Faker<WorkerZoneAssignmentFileDto>();
        faker.StrictMode(true);
        faker.RuleFor(y => y.Zone_Code,((faker1, dto) => dto.Zone_Code = $"Z{faker1.Random.Number(1,1500)}") );
        faker.RuleFor(y => y.Worker_Code,((faker1, dto) => dto.Worker_Code = $"W{faker1.Random.Number(1,1500)}") );
        faker.RuleFor(y => y.Assignment_Date, (faker1, dto) => dto.Assignment_Date = $"2027-0{faker1.Random.Number(1,9)}-{faker1.Random.Number(1,30)}");
        var records = faker.GenerateLazy(50000);
        using (var writer = new StreamWriter("Sample50k.csv"))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(records);
        }
    }
    [Fact]
    public void GenerateAllValid50K()
    {
        Faker<WorkerZoneAssignmentFileDto> faker = new Faker<WorkerZoneAssignmentFileDto>();
        faker.StrictMode(true);
        faker.RuleFor(y => y.Zone_Code,((faker1, dto) => dto.Zone_Code = $"Z{faker1.Random.Number(1,1000)}") );
        faker.RuleFor(y => y.Worker_Code,((faker1, dto) => dto.Worker_Code = $"W{faker1.Random.Number(1, 50000)}") );
        faker.RuleFor(y => y.Assignment_Date, (faker1, dto) => dto.Assignment_Date = $"2027-0{faker1.Random.Number(1,9)}-{faker1.Random.Number(1,30)}");
        var records = faker.GenerateLazy(50000);
        using (var writer = new StreamWriter("AllValidSample50k.csv"))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(records);
        }
    }
    [Fact]
    public void GenerateGreaterThan50K()
    {
        Faker<WorkerZoneAssignmentFileDto> faker = new Faker<WorkerZoneAssignmentFileDto>();
        faker.StrictMode(true);
        faker.RuleFor(y => y.Zone_Code,((faker1, dto) => dto.Zone_Code = $"Z{faker1.Random.Number(1,1500)}") );
        faker.RuleFor(y => y.Worker_Code,((faker1, dto) => dto.Worker_Code = $"W{faker1.Random.Number(1,1500)}") );
        faker.RuleFor(y => y.Assignment_Date, (faker1, dto) => dto.Assignment_Date = $"2027-0{faker1.Random.Number(1,9)}-{faker1.Random.Number(1,30)}");
        var records = faker.GenerateLazy(50001);
        using (var writer = new StreamWriter("Sample50k+1.csv"))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(records);
        }
    }
}

public class WorkerZoneAssignmentFileDto
{
    public string Worker_Code { get; set; }
    public string Zone_Code { get; set; }
    public string Assignment_Date { get; set; }
}