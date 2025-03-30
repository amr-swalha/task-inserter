using CleanBase.CleanAbstractions.CleanOperation;
using CleanBase.Entities;
using CleanOperation.DataAccess;
using Microsoft.Extensions.Caching.Memory;

namespace CleanAPI;

public class InitializeCacheService(IServiceScopeFactory serviceScopeFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();

        var cache = scope.ServiceProvider.GetService<IMemoryCache>();
        var repo = scope.ServiceProvider.GetService<AppDataContext>();
        var allWorkers = repo.Set<Worker>().ToArray();
        foreach (var worker in allWorkers)
        {
            cache.Set(worker.Code, worker.Id);
        }

        var allZones = repo.Set<Zone>().ToArray();
        foreach (var zone in allZones)
        {
            cache.Set(zone.Code, zone.Id);
        }


        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}