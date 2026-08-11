namespace Skight.AgentPlatform.AppHost;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);
        builder.AddProject("csharp-agent-platform", "../Skight.AgentPlatform/Skight.AgentPlatform.csproj");
        var app = builder.Build();
        app.Run();
    }
}
