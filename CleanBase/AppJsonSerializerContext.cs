using CleanBase.Dtos;
using CleanBase.Entities;
using System.Text.Json.Serialization;

namespace CleanBase;

[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(Worker))]
[JsonSerializable(typeof(Zone))]
[JsonSerializable(typeof(WorkerZoneAssignment))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
