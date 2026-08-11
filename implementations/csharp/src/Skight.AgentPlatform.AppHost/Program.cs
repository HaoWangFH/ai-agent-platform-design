using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Skight_AgentPlatform>("csharp-agent-platform");

builder.Build().Run();
