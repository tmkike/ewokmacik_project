using WebApi.Endpoints;

namespace WebApi.Configuration;

public static class EndpointConfiguration
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder api = routes.MapGroup("/api");

        api.MapBookEndpoints();
        api.MapLoanEndpoints();

        return routes;
    }
}
