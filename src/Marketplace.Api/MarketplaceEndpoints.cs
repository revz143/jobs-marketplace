using Marketplace.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Marketplace.Api;

/// <summary>Registers version-one marketplace REST API endpoints.</summary>
public static class MarketplaceEndpoints
{
    /// <summary>Maps profile, job, and offer endpoints under the supplied route group.</summary>
    public static void MapMarketplaceEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/customers/{id:long}", MarketplaceRouteHandlers.GetCustomerAsync).RequireAuthorization();
        api.MapGet("/customers", MarketplaceRouteHandlers.SearchCustomersAsync).RequireAuthorization();
        api.MapGet("/contractors/{id:long}", MarketplaceRouteHandlers.GetContractorAsync).RequireAuthorization();
        api.MapGet("/contractors", MarketplaceRouteHandlers.SearchContractorsAsync).RequireAuthorization();
        api.MapGet("/jobs", MarketplaceRouteHandlers.ListJobsAsync).RequireAuthorization();
        api.MapPost("/jobs", MarketplaceRouteHandlers.CreateJobAsync).RequireAuthorization(AppRoles.Customer).RequireRateLimiting("write");
        api.MapGet("/jobs/{jobId:long}", MarketplaceRouteHandlers.GetJobAsync).RequireAuthorization();
        api.MapPut("/jobs/{jobId:long}", MarketplaceRouteHandlers.UpdateJobAsync).RequireAuthorization(AppRoles.Customer).RequireRateLimiting("write");
        api.MapDelete("/jobs/{jobId:long}", MarketplaceRouteHandlers.DeleteJobAsync).RequireAuthorization(AppRoles.Customer).RequireRateLimiting("write");
        api.MapGet("/jobs/{jobId:long}/offers", MarketplaceRouteHandlers.ListOffersAsync).RequireAuthorization();
        api.MapPost("/jobs/{jobId:long}/offers", MarketplaceRouteHandlers.CreateOfferAsync).RequireAuthorization(AppRoles.Contractor).RequireRateLimiting("write");
        api.MapPut("/jobs/{jobId:long}/offers/{offerId:long}", MarketplaceRouteHandlers.UpdateOfferAsync).RequireAuthorization(AppRoles.Contractor).RequireRateLimiting("write");
        api.MapDelete("/jobs/{jobId:long}/offers/{offerId:long}", MarketplaceRouteHandlers.DeleteOfferAsync).RequireAuthorization(AppRoles.Contractor).RequireRateLimiting("write");
        api.MapPost("/jobs/{jobId:long}/offers/{offerId:long}/accept", MarketplaceRouteHandlers.AcceptOfferAsync).RequireAuthorization(AppRoles.Customer).RequireRateLimiting("write");
    }

}
