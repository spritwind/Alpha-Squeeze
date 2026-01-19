using AlphaSqueeze.Api.Services;
using AlphaSqueeze.Data;
using AlphaSqueeze.Shared.Grpc;
using AlphaSqueeze.Shared.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ===================
// 資料層服務
// ===================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddAlphaSqueezeData(connectionString);

// ===================
// gRPC Client
// ===================
var grpcHost = builder.Configuration["GrpcServer:Host"] ?? "localhost";
var grpcPort = builder.Configuration.GetValue("GrpcServer:Port", 50051);
var grpcAddress = $"http://{grpcHost}:{grpcPort}";

builder.Services.AddGrpcClient<SqueezeEngine.SqueezeEngineClient>(options =>
{
    options.Address = new Uri(grpcAddress);
});
builder.Services.AddScoped<ISqueezeEngineClient, SqueezeEngineClient>();

// ===================
// LINE Notify 服務
// ===================
builder.Services.AddHttpClient<ILineNotifyService, LineNotifyService>();

// ===================
// 背景服務
// ===================
builder.Services.AddHostedService<DailyAlertService>();

// ===================
// API Controllers
// ===================
builder.Services.AddControllers();

// ===================
// Swagger / OpenAPI
// ===================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Alpha Squeeze API",
        Version = "v1",
        Description = "台股軋空訊號分析 API - 提供股票指標查詢、軋空潛力分析等功能",
        Contact = new OpenApiContact
        {
            Name = "Alpha Squeeze Team"
        }
    });

    // 包含 XML 註解 (如果有的話)
    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// ===================
// CORS (開發環境)
// ===================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ===================
// HTTP Pipeline
// ===================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Alpha Squeeze API v1");
        options.RoutePrefix = string.Empty; // Swagger UI 在根路徑
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.MapControllers();

// 健康檢查端點
app.MapGet("/health", () => new
{
    Status = "Healthy",
    Timestamp = DateTime.Now.ToString("o"),
    Version = "1.0.0"
}).WithTags("Health");

app.Run();

// 讓測試專案可以存取 Program 類別
public partial class Program { }
