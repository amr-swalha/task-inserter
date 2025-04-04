﻿using CleanBase;
using CleanBase.Entities;
using CleanBase.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanOperation.DataAccess;

public class AppDataContext : DbContext
{
    public AppDataContext(DbContextOptions<AppDataContext> options) : base(options)
    {

    }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
    }
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp without time zone");
        configurationBuilder.Properties<DateTime?>().HaveColumnType("timestamp with time zone");
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Worker>().ToTable(nameof(Worker).ToSnakeCase());
        modelBuilder.Entity<Zone>().ToTable(nameof(Zone).ToSnakeCase());
        modelBuilder.Entity<FileHistory>().ToTable(nameof(FileHistory).ToSnakeCase());
        modelBuilder.Entity<WorkerZoneAssignment>().ToTable(nameof(WorkerZoneAssignment).ToSnakeCase());
        modelBuilder.Entity<WorkerZoneAssignment>().HasOne(r => r.Worker);
        modelBuilder.Entity<WorkerZoneAssignment>().HasOne(r => r.Zone);
        modelBuilder.Entity<WorkerZoneAssignment>().Ignore(r => r.ExtraDetails);
        EntityPropertyMapper(modelBuilder);
    }
    private void EntityPropertyMapper(ModelBuilder modelBuilder)
    {

        
        IEnumerable<IMutableEntityType> mappedEntities = modelBuilder.Model.GetEntityTypes().Where(y => y.ClrType.BaseType.Name.Contains(nameof(EntityRoot))).ToList();

        foreach (IMutableEntityType entity in mappedEntities)
        {
            EntityTypeBuilder entityTypeBuilder = modelBuilder.Entity(entity.ClrType);
            entityTypeBuilder.Property(nameof(EntityRoot.Id)).ValueGeneratedOnAdd();
        }
    }
}
