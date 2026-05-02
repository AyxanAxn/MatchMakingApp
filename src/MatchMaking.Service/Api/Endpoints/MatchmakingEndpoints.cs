using MatchMaking.Service.Application.Commands;
using MatchMaking.Service.Application.Queries;
using MatchMaking.Service.Domain.Models;
using MatchMaking.Service.Infrastructure.Configuration;
using MediatR;

namespace MatchMaking.Service.Api.Endpoints;

public static class MatchmakingEndpoints
{
    public static void MapMatchmakingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/matchmaking")
            .WithTags("MatchMaking");

        group.MapPost("/search", async (SearchMatchRequest request, IMediator mediator) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
                return Results.BadRequest("UserId is required.");

            var result = await mediator.Send(new SearchMatchCommand(request.UserId));

            return result == SearchMatchResult.AlreadyInQueue
                ? Results.Conflict("Player is already in the matchmaking queue.")
                : Results.NoContent();
        })
        .WithName("SearchMatch")
        .WithSummary("Submit a match search request")
        .WithDescription("Adds the user to the matchmaking queue. When enough players are queued, a match is formed.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status429TooManyRequests)
        .RequireRateLimiting(RateLimitingOptions.PolicyName);

        group.MapGet("/match/{userId}", async (string userId, IMediator mediator) =>
        {
            if (string.IsNullOrWhiteSpace(userId))
                return Results.BadRequest("UserId is required.");

            var match = await mediator.Send(new GetMatchQuery(userId));

            return match is not null
                ? Results.Ok(match)
                : Results.NotFound();
        })
        .WithName("GetMatch")
        .WithSummary("Retrieve match information")
        .WithDescription("Returns the match formed for the user's last successful search request.")
        .Produces<MatchInfo>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest);
    }
}

public sealed record SearchMatchRequest(string UserId);
