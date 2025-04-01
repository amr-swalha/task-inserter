using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Akka.Actor;
using CleanBase.CleanAbstractions.CleanOperation;
using CleanBase.Dtos;
using CleanBase.Entities;
using CleanBase.Validator;
using CsvHelper;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CleanAPI.Controllers
{
    public class WorkerZoneAssignmentEndpoints : Endpoint<WorkerZoneAssignmentRequestDto>
    {
        public IMemoryCache _cache { get; set; }
        public IRepository<WorkerZoneAssignment> _workerZoneAssignmentRepository { get; set; }

        public override void Configure()
        {
            Post("/api/WorkerZoneAssignment/ProcessFile");
            AllowFileUploads();
            AllowAnonymous();
        }

        public override async Task HandleAsync(WorkerZoneAssignmentRequestDto request, CancellationToken ct)
        {
            var processor = new WorkerZoneAssignmentProcessor(request, _cache, _workerZoneAssignmentRepository);
            await SendAsync(processor.Process());
        }
    }
}

public class WorkerZoneAssignmentRequestDto
{
    public string Name { get; set; }
    public IFormFile ProcessFile { get; set; }
}

public class WorkerZoneAssignmentProcessor
{
    private readonly WorkerZoneAssignmentRequestDto _request;
    private readonly IMemoryCache _cache;
    private readonly IRepository<WorkerZoneAssignment> _workerZoneAssignmentRepository;
    private HashSet<string> invalidWorkersIds = new HashSet<string>();
    HashSet<string> doesNotWorkersIds = new HashSet<string>();
    HashSet<string> workersIds = new HashSet<string>();
    HashSet<string> invalidZoneIds = new HashSet<string>();
    HashSet<string> doesnotExitsZoneIds = new HashSet<string>();
    HashSet<string> zoneIds = new HashSet<string>();
    private List<WorkerZoneAssignmentFileDto> records = new List<WorkerZoneAssignmentFileDto>();

    public WorkerZoneAssignmentProcessor(WorkerZoneAssignmentRequestDto request, IMemoryCache cache,
        IRepository<WorkerZoneAssignment> workerZoneAssignmentRepository)
    {
        _request = request;
        _cache = cache;
        _workerZoneAssignmentRepository = workerZoneAssignmentRepository;
    }

    public object Process()
    {
        using (var reader = new StreamReader(_request.ProcessFile.OpenReadStream()))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            records = csv.GetRecords<WorkerZoneAssignmentFileDto>().ToList();
            var fileCheck = ValidateFile();
            if (!fileCheck.passed)
            {
                return fileCheck.error;
            }

            PrepareWorkerData();
            PrepareZoneData();
            List<WorkerZoneAssignment> assignments = new List<WorkerZoneAssignment>(records.Count());
            List<WorkerZoneAssignmentResult> results = new List<WorkerZoneAssignmentResult>(records.Count());
            ProcessRecords(assignments, results);
            var insertedCount = ProcessInsertion(assignments, results);
            _workerZoneAssignmentRepository.GetAppDataContext()
                .Set<FileHistory>()
                .Add(new FileHistory()
                {
                    ProcessedAt = DateTime.Now,
                    InsertedRecords = insertedCount,
                    ProcessedRecords = records.Count(),
                    Name = _request.ProcessFile.Name
                });
            _workerZoneAssignmentRepository.GetAppDataContext()
                .SaveChanges();
            return results;
        }
    }

    private int ProcessInsertion(List<WorkerZoneAssignment> assignments, List<WorkerZoneAssignmentResult> results)
    {
        var valuesClause = string.Join(", ",
            assignments.Select(a =>
                $"({a.WorkerId}, {a.ZoneId}, '{a.EffectiveDate:yyyy-MM-dd}'::date)"));

        var sql = $@"
        WITH required_assignments AS (
            SELECT * FROM (VALUES {valuesClause}) 
            AS t(worker_id, zone_id, effective_date)
        )
        INSERT INTO worker_zone_assignment (worker_id, zone_id, effective_date)
        SELECT r.worker_id, r.zone_id, r.effective_date
        FROM required_assignments r
        LEFT JOIN worker_zone_assignment wza 
            ON r.worker_id = wza.worker_id 
            AND r.zone_id = wza.zone_id 
            AND r.effective_date = wza.effective_date
        WHERE wza.id IS NULL
        ON CONFLICT (worker_id, effective_date) 
        DO NOTHING
        RETURNING id, worker_id, zone_id, effective_date";
        var inserted = _workerZoneAssignmentRepository.GetAppDataContext().Set<WorkerZoneAssignment>()
            .FromSqlRaw(sql)
            .ToList();
        foreach (var assignmentData in assignments.Where(r =>
                     !inserted.Any(z => z.EffectiveDate == r.EffectiveDate
                                        && z.WorkerId == r.WorkerId && z.ZoneId == r.ZoneId)))
        {
            WorkerZoneAssignmentResult assignment = new();
            assignment.data.Add(nameof(WorkerZoneAssignmentFileDto.Worker_Code),
                assignmentData.ExtraDetails.Worker_Code);
            assignment.data.Add(nameof(WorkerZoneAssignmentFileDto.Zone_Code), assignmentData.ExtraDetails.Zone_Code);
            assignment.data.Add(nameof(WorkerZoneAssignmentFileDto.Assignment_Date),
                assignmentData.ExtraDetails.Assignment_Date);
            assignment.error.Add(nameof(WorkerZoneAssignmentFileDto.Assignment_Date), "Assignment Date already exists");
            results.Add(assignment);
        }
        return inserted.Count;
    }

    private void PrepareWorkerData()
    {
        invalidWorkersIds = records.Where(y => y.Worker_Code.Length > 10)
            .Select(y => y.Worker_Code).ToHashSet();
        workersIds = records.Select(t => t.Worker_Code)
            .Where(y => !invalidWorkersIds.Contains(y)).ToHashSet();
        var exists = _workerZoneAssignmentRepository.GetAppDataContext()
            .Set<Worker>().Where(y => workersIds.Contains(y.Code))
            .Select(y => new Worker() {Code = y.Code, Id = y.Id})
            .ToList();
        foreach (var worker in workersIds.Where(y => !exists.Select(z => z.Code).Contains(y)))
        {
            doesNotWorkersIds.Add(worker);
        }

        workersIds.RemoveWhere(y => invalidWorkersIds.Contains(y));
    }

    private void PrepareZoneData()
    {
        invalidZoneIds = records.Where(y => y.Zone_Code.Length > 10)
            .Select(y => y.Zone_Code).ToHashSet();
        zoneIds = records.Select(t => t.Zone_Code)
            .Where(y => !invalidZoneIds.Contains(y)).ToHashSet();

        var zoneExits = _workerZoneAssignmentRepository.GetAppDataContext()
            .Set<Zone>().Where(y => zoneIds.Contains(y.Code))
            .Select(y => new Zone() {Code = y.Code, Id = y.Id})
            .ToList();
        foreach (var worker in zoneIds.Where(y => !zoneExits.Select(z => z.Code).Contains(y)))
        {
            doesnotExitsZoneIds.Add(worker);
        }

        zoneIds.RemoveWhere(y => invalidZoneIds.Contains(y));
    }

    public (bool passed, string error) ValidateFile()
    {
        if (records.Count() > 50000 || !records.Any())
        {
            return (false, "File is invalid, file should contains at least one record and not exceed 50,000");
        }

        return (true, null)!;
    }

    private void ProcessRecords(List<WorkerZoneAssignment> assignments, List<WorkerZoneAssignmentResult> results)
    {
        foreach (var record in records)
        {
            WorkerZoneAssignmentResult assignment = new WorkerZoneAssignmentResult();
            assignment.data.Add(nameof(record.Worker_Code), record.Worker_Code);
            assignment.data.Add(nameof(record.Zone_Code), record.Zone_Code);
            assignment.data.Add(nameof(record.Assignment_Date), record.Assignment_Date);
            if (!Regex.IsMatch(record.Assignment_Date, "^\\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$")
                || !DateTime.TryParse(record.Assignment_Date, out _))
            {
                assignment.error.Add(nameof(record.Worker_Code), "Date is invalid");
            }

            if (doesNotWorkersIds.Contains(record.Worker_Code))
            {
                assignment.error.Add(nameof(record.Worker_Code), "Worker Code does not exist");
            }

            if (invalidWorkersIds.Contains(record.Worker_Code))
            {
                assignment.error.Add(nameof(record.Worker_Code), "Worker Code exceeds 10 characters");
            }

            if (doesnotExitsZoneIds.Contains(record.Zone_Code))
            {
                assignment.error.Add(nameof(record.Zone_Code), "Zone Code does not exist");
            }

            if (invalidZoneIds.Contains(record.Zone_Code))
            {
                assignment.error.Add(nameof(record.Zone_Code), "Zone Code exceeds 10 characters");
            }

            if (!assignment.error.Any())
            {
                var zoneId = _cache.Get<int>(record.Zone_Code);
                var workerId = _cache.Get<int>(record.Worker_Code);
                var tempDate = DateTime.Parse(record.Assignment_Date);
                assignments.Add(new WorkerZoneAssignment()
                {
                    EffectiveDate = tempDate, WorkerId = workerId, ZoneId = zoneId,
                    ExtraDetails = new()
                    {
                        Worker_Code = record.Worker_Code,
                        Zone_Code = record.Zone_Code, Assignment_Date = record.Assignment_Date
                    }
                });
            }
            else
                results.Add(assignment);
        }
    }
}

