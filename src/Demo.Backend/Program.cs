namespace Demo.Backend;

using Demo.Backend.Endpoints;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }));
        app.MapProductEndpoints();
        app.Run();
    }
}
