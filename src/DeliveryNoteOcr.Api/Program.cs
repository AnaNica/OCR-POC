using System.Text.Json.Serialization;
using DeliveryNoteOcr.Api.Data;
using DeliveryNoteOcr.Api.Services;
using DeliveryNoteOcr.Api.Services.Audit;
using DeliveryNoteOcr.Api.Services.Extraction;
using DeliveryNoteOcr.Api.Services.Storage;
using DeliveryNoteOcr.Api.Services.Training;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));

var connection = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=data/app.db";
EnsureSqliteDirectory(builder, connection);
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));

builder.Services.Configure<LocalFileStorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<AzureDocumentIntelligenceOptions>(
    builder.Configuration.GetSection("DocumentIntelligence"));
builder.Services.Configure<TrainingOptions>(builder.Configuration.GetSection("Training"));

builder.Services.AddSingleton<IDocumentStorage, LocalFileStorage>();
builder.Services.AddSingleton<IDocumentExtractor, AzureDocumentIntelligenceExtractor>();
builder.Services.AddSingleton<IAuditService, AuditService>();
builder.Services.AddScoped<ICompanyResolver, CompanyResolver>();
builder.Services.AddScoped<ITrainingLabelWriter, TrainingLabelWriter>();
builder.Services.AddScoped<IRetrainCoordinator, RetrainCoordinator>();
builder.Services.AddScoped<ICurrentUser, DevCurrentUser>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", at = DateTimeOffset.UtcNow }));

app.Run();

static void EnsureSqliteDirectory(WebApplicationBuilder b, string connection)
{
    const string key = "Data Source=";
    var i = connection.IndexOf(key, StringComparison.OrdinalIgnoreCase);
    if (i < 0) return;
    var rest = connection[(i + key.Length)..];
    var semi = rest.IndexOf(';');
    var path = (semi < 0 ? rest : rest[..semi]).Trim();
    var absolute = Path.IsPathRooted(path) ? path : Path.Combine(b.Environment.ContentRootPath, path);
    var dir = Path.GetDirectoryName(absolute);
    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
}
