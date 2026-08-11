namespace Skight.AgentPlatform.AppHost;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);
        var app = builder.Build();
        app.Run();
    }
}
