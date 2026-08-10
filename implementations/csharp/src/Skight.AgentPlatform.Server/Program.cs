using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Skight.AgentPlatform;
using Skight.AgentPlatform.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var dbPath = builder.Configuration["DatabasePath"] ?? "agent_platform_server.db";
IMemoryStore memoryStore = new SqliteMemoryStore(dbPath);
builder.Services.AddSingleton(memoryStore);

var defaultConfig = new AgentConfig
{
    Model = builder.Configuration["Agent:Model"] ?? "gpt-4o",
    ApiKey = builder.Configuration["Agent:ApiKey"] ?? "dummy_key",
    ContextWindowLimit = 30,
    MaxIterations = 10
};
builder.Services.AddSingleton(defaultConfig);
builder.Services.AddSingleton<AgentSessionManager>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Skight Agent Platform API v1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();
app.MapControllers();
app.MapGrpcService<AgentGrpcService>();

app.MapGet("/", () => "Skight AI Agent Platform WebAPI & gRPC Server is running.");

app.Run();
