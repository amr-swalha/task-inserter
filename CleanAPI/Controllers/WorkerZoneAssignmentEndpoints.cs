using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CleanBase.CleanAbstractions.CleanOperation;
using CleanBase.Dtos;
using CleanBase.Entities;
using CleanBase.Validator;
using CsvHelper;
using FastEndpoints;
using Microsoft.Extensions.Caching.Memory;

namespace CleanAPI.Controllers
{
    public class WorkerZoneAssignmentEndpoints : Endpoint<WorkerZoneAssignmentDto>
    {
        public IMemoryCache  _cache { get; set; }
        public IRepository<WorkerZoneAssignment> _workerZoneAssignmentRepository { get; set; }
        public override void Configure()
        {
            Post("/api/WorkerZoneAssignment/ProcessFile");
            AllowFileUploads();
            AllowAnonymous();
        }

        public override async Task HandleAsync(WorkerZoneAssignmentDto request, CancellationToken ct)
        {
            using (var reader = new StreamReader(request.ProcessFile.OpenReadStream()))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<WorkerZoneAssignmentFileDto>().ToList();
                if (records.Count() > 50000 || !records.Any())
                {
                    await SendAsync("File is invalid, file should contains at least one record and not exceed 50,000");
                    return;
                }
                
                // Hashset of all the ids
                // determine what's valid and what's not
                // fail the incorrect early
                HashSet<string> invalidWorkersIds = new HashSet<string>();
                HashSet<string> doesNotWorkersIds = new HashSet<string>();
                invalidWorkersIds = records.Where(y => y.Worker_Code.Length > 10)
                    .Select(y => y.Worker_Code).ToHashSet();
                HashSet<string> workersIds = new HashSet<string>();
                workersIds = records.Select(t => t.Worker_Code)
                    .Where(y => !invalidWorkersIds.Contains(y)).ToHashSet();
                var exists = _workerZoneAssignmentRepository.GetAppDataContext()
                    .Set<Worker>().Where(y => workersIds.Contains(y.Code))
                    .Select(y=> new Worker(){ Code = y.Code, Id = y.Id})
                    .ToList();
                foreach (var worker in workersIds.Where(y => !exists.Select(z => z.Code).Contains(y)))
                {
                    doesNotWorkersIds.Add(worker);
                }
                workersIds.RemoveWhere(y => invalidWorkersIds.Contains(y));  
                HashSet<string> invalidZoneIds = new HashSet<string>();
                HashSet<string> doesnotExitsZoneIds = new HashSet<string>();
                HashSet<string> zoneIds = new HashSet<string>();
                invalidZoneIds = records.Where(y => y.Zone_Code.Length > 10)
                    .Select(y  => y.Zone_Code).ToHashSet();
                zoneIds = records.Select(t => t.Zone_Code)
                    .Where(y => !invalidZoneIds.Contains(y)).ToHashSet();
                
                var zoneExits = _workerZoneAssignmentRepository.GetAppDataContext()
                    .Set<Zone>().Where(y => zoneIds.Contains(y.Code))
                    .Select(y=> new Zone(){ Code = y.Code, Id = y.Id})
                    .ToList();
                foreach (var worker in workersIds.Where(y => !zoneExits.Select(z => z.Code).Contains(y)))
                {
                    doesnotExitsZoneIds.Add(worker);
                }
                zoneIds.RemoveWhere(y => invalidZoneIds.Contains(y));  
                List<WorkerZoneAssignment> assignments = new List<WorkerZoneAssignment>(records.Count());
                List<WorkerZoneAssignmentResult> results = new List<WorkerZoneAssignmentResult>(records.Count());
                foreach (var record in records)
                {
                    WorkerZoneAssignmentResult assignment = new WorkerZoneAssignmentResult();
                    assignment.data.Add(nameof(record.Worker_Code), record.Worker_Code);
                    assignment.data.Add(nameof(record.Zone_Code), record.Zone_Code);
                    assignment.data.Add(nameof(record.Assignment_Date), record.Assignment_Date);
                    if (!Regex.IsMatch(record.Assignment_Date,"^\\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$"))
                    {
                        assignment.error.Add(nameof(record.Worker_Code),"Date is invalid");
                    }
                    if (doesNotWorkersIds.Contains(record.Worker_Code))
                    {
                        assignment.error.Add(nameof(record.Worker_Code),"Worker Code does not exist");
                    }

                    if (invalidWorkersIds.Contains(record.Worker_Code))
                    {
                        assignment.error.Add(nameof(record.Worker_Code),"Worker Code exceeds 10 characters");
                    }

                    if (doesnotExitsZoneIds.Contains(record.Zone_Code))
                    {
                        assignment.error.Add(nameof(record.Zone_Code),"Zone Code does not exist");
                    }

                    if (invalidZoneIds.Contains(record.Zone_Code))
                    {
                        assignment.error.Add(nameof(record.Zone_Code),"Zone Code exceeds 10 characters");
                    }
                    if (!assignment.error.Any())
                    {
                        var zoneId = _cache.Get<int>(record.Zone_Code);
                        var workerId = _cache.Get<int>(record.Worker_Code);
                        var tempDate  = DateTime.Parse(record.Assignment_Date);
                        var hasExactAssignment = _workerZoneAssignmentRepository
                            .GetAppDataContext().Set<WorkerZoneAssignment>()
                            .Any(y => y.ZoneId  == zoneId && y.WorkerId == workerId
                            && y.EffectiveDate == tempDate);
                        if (!hasExactAssignment)
                        {
                            _workerZoneAssignmentRepository
                                .GetAppDataContext().Set<WorkerZoneAssignment>()
                                .Add(new WorkerZoneAssignment() { EffectiveDate = tempDate , WorkerId =  workerId ,  ZoneId = zoneId });
                        }
                        else
                        {
                            assignment.error.Add(nameof(record.Assignment_Date),"Assignment Date already exists");
                        }
                    }
                    results.Add(assignment);
                }
                _workerZoneAssignmentRepository.GetAppDataContext().SaveChanges();
                await SendAsync(results);
            }
        }
    }

    public class WorkerZoneAssignmentDto
    {
        public string Name { get; set; }
        public IFormFile ProcessFile { get; set; }
    }
}
