using System.Collections.Immutable;
using System.Globalization;
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
                invalidWorkersIds = records.Where(y => y.Worker_Code.Length > 10)
                    .Select(y => y.Worker_Code).ToHashSet();
                HashSet<string> workersIds = new HashSet<string>();
                workersIds = records.Select(t => t.Worker_Code)
                    .Where(y => !invalidWorkersIds.Contains(y)).ToHashSet();
                var exists = _workerZoneAssignmentRepository.GetAppDataContext()
                    .Set<Worker>().Where(y => workersIds.Contains(y.Code))
                    .Select(y=> new Worker(){ Code = y.Code, Id = y.Id})
                    .ToList();
                HashSet<string> invalidZoneIds = new HashSet<string>();
                HashSet<string> zoneIds = new HashSet<string>();
                invalidZoneIds = records.Where(y => y.Zone_Code.Length > 10)
                    .Select(y  => y.Zone_Code).ToHashSet();
                zoneIds = records.Select(t => t.Zone_Code)
                    .Where(y => !invalidZoneIds.Contains(y)).ToHashSet();
                
                var zoneExits = _workerZoneAssignmentRepository.GetAppDataContext()
                    .Set<Zone>().Where(y => zoneIds.Contains(y.Code))
                    .Select(y=> new Zone(){ Code = y.Code, Id = y.Id})
                    .ToList();
                
                List<WorkerZoneAssignment> assignments = new List<WorkerZoneAssignment>(records.Count());
                List<WorkerZoneAssignmentResult> results = new List<WorkerZoneAssignmentResult>(records.Count());
                foreach (var record in records)
                {
                    WorkerValidator validator = new WorkerValidator();
                    var validationResult = validator.Validate(record);
                    if (!validationResult.IsValid)
                        Console.WriteLine($"{record.Worker_Code} - {record.Zone_Code}");
                    else
                    {
                        
                        
                        var temp = DateTime.Parse(record.Assignment_Date);
                        _workerZoneAssignmentRepository.GetAppDataContext().Set<Worker>()
                            .Where(y => y.Code == record.Worker_Code)
                            .Select(y => "Worker_Code");
                        _workerZoneAssignmentRepository.GetAppDataContext().Set<Zone>()
                            .Where(y => y.Code == record.Zone_Code)
                            .Select(y => "Zone_Code");

                        var valid = true;
                        if (!valid)
                        {
                            assignments.Add(new WorkerZoneAssignment()
                            {
                                EffectiveDate = temp,
                                
                            });
                        }
                    }
                }
            }
        }
    }

    public class WorkerZoneAssignmentDto
    {
        public string Name { get; set; }
        public IFormFile ProcessFile { get; set; }
    }
}
