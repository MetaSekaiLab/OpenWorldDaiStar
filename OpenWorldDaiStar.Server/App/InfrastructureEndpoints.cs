using System.Text;

namespace OpenWorldDaiStar.App;

public static class InfrastructureEndpoints
{
    public static IEndpointRouteBuilder MapInfrastructureEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/healthz", () =>
                Results.Text("ok", "text/plain", Encoding.UTF8))
            .ExcludeFromDescription();

        endpoints.MapGet("/readyz", () =>
                Results.Text("ready", "text/plain", Encoding.UTF8))
            .ExcludeFromDescription();

        return endpoints;
    }
}
