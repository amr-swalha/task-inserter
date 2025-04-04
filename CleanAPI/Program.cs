using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Sql.Hosting;
using CleanBase;
using CleanBase.CleanAbstractions.CleanOperation;
using CleanBusiness.Actors;
using CleanOperation.DataAccess;
using CleanOperation.Operations;
using FastEndpoints;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Batch;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using RepoDb;
using Scalar.AspNetCore;
using Serilog;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using CleanBase.Entities;

namespace CleanAPI;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
.WriteTo.Console()
.CreateLogger();

        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddAuthentication()
        .AddJwtBearer(jwtOptions =>
        {
            jwtOptions.RequireHttpsMetadata = false;
            jwtOptions.Authority = builder.Configuration["Auth:Authority"];
            jwtOptions.Audience = builder.Configuration["Auth:Audience"];
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidAudiences = builder.Configuration.GetSection("Auth:ValidAudiences").Get<string[]>(),
                ValidIssuers = builder.Configuration.GetSection("Auth:ValidIssuers").Get<string[]>(),
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Auth:Key"]))
            };

            jwtOptions.MapInboundClaims = false;
        });
        builder.Services.AddMemoryCache();
        builder.Services.AddOpenApi();
        builder.Services.AddSerilog();
        builder.Services.AddFastEndpoints().AddResponseCaching();
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        builder.Services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.SmallestSize;
        });
        builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        // Add services to the container.
        builder.Services.AddDbContext<AppDataContext>(y =>
        {
            var dbConnection = builder.Configuration["ConnectionStrings:DefaultConnection"];
            y.UseNpgsql(builder.Configuration["ConnectionStrings:DefaultConnection"],
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseSnakeCaseNamingConvention();
            //y.UseInMemoryDatabase("Main");
            y.EnableDetailedErrors();
            y.EnableSensitiveDataLogging();
            y.ConfigureWarnings(y => y.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            y.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            y.LogTo(Console.WriteLine, LogLevel.Information);
        });
        builder.Services.AddHostedService<InitializeCacheService>();

        var defaultBatchHandler = new DefaultODataBatchHandler();
        defaultBatchHandler.MessageQuotas.MaxNestingDepth = 3;
        defaultBatchHandler.MessageQuotas.MaxOperationsPerChangeset = 10;
        defaultBatchHandler.MessageQuotas.MaxReceivedMessageSize = 1000;
        builder.Services.AddFluentValidationClientsideAdapters();
        //builder.Services.AddValidatorsFromAssemblyContaining<TodoListValidation>(); ;
        builder.Services.AddControllers()
            .AddJsonOptions(x =>
            {
                x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                x.JsonSerializerOptions.TypeInfoResolverChain.Add(AppJsonSerializerContext.Default);
            })
        .AddOData(opt => opt.Select().Filter().Expand().Count().SetMaxTop(10).EnableQueryFeatures()
        .AddRouteComponents("odata", GetEdmModel(), defaultBatchHandler));

        builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        builder.Services.AddAkka("clean-system", (akkaBuilder, provider) =>
        {
            //akkaBuilder.WithSqlPersistence(builder.Configuration["ConnectionStrings:DefaultConnection"],
            //    ProviderName.SqlServer2022)
            //.StartActors((actors, registry, resolver) =>
            //{
            //    var regStatus = registry.TryRegister<SampleTodoListActor>(actors.ActorOf<SampleTodoListActor>());
            //    Log.Information("Registry Status: " + regStatus);
            //});

        });

        //var assembly = typeof(ICleanActor).Assembly;
        //var types = assembly.ExportedTypes
        //   // filter types that are unrelated
        //   .Where(x => x.IsClass && x.IsPublic && x.GetInterface(nameof(ICleanActor)) != null);
        //foreach (var type in types)
        //{
        //    builder.Services.AddSingleton(provider =>
        //    {
        //        var actorSystem = provider.GetRequiredService<ActorSystem>();
        //        var props = DependencyResolver.For(actorSystem).Props(type);
        //        return actorSystem.ActorOf(props, type.Name);
        //    });
        //}

        var assembly = typeof(ICleanOperation).Assembly;
        foreach (
            var operation in assembly
                .GetExportedTypes()
                .Where(r => r.Name.EndsWith("Operation") && r.IsClass)
        )
        {
            builder.Services.AddScoped(
                operation.GetInterface($"I{operation.Name}"),
                operation
            );
        }


        var app = builder.Build();

        app.UseResponseCompression();

        app.UseResponseCaching()
            .UseFastEndpoints(y => y.Serializer.Options.ReferenceHandler = ReferenceHandler.IgnoreCycles);
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
            app.UseSerilogRequestLogging();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();


        app.MapControllers();

        app.Run();
    }
    private static IEdmModel GetEdmModel()
    {
        var assembly = typeof(EntityRoot).Assembly;
        var types = assembly.ExportedTypes
           // filter types that are unrelated
           .Where(x => x.IsClass && x.IsPublic && x.BaseType == typeof(CleanBase.EntityRoot));
        ODataConventionModelBuilder builder = new ODataConventionModelBuilder();

        foreach (var type in types)
        {
            var entityType = builder.AddEntityType(type);
            PropertyInfo key = new EntityRoot().GetType().GetProperty("Id");
            entityType.HasKey(key);
            builder.AddEntitySet(type.Name, entityType);
        }
        return builder.GetEdmModel();
    }
}