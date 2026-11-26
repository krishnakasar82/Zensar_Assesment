using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using OT.Assessment.Application;
using OT.Assessment.Shared;

namespace OT.Assessment.Api.Endpoints;

public static class CasinoWagerEndpoints
{
    public static WebApplication MapCasinoWagerEndpoints(this WebApplication app)
    {
        app.MapPost("/api/player/casinowager",
                async Task<Results<Accepted<WagerAcceptedResponse>, ValidationProblem>> (
                    CasinoWagerRequest request,
                    ICasinoWagerService service,
                    CancellationToken cancellationToken) =>
                {
                    var errors = CasinoWagerRequestValidator.Validate(request);
                    if (errors.Count > 0)
                    {
                        return TypedResults.ValidationProblem(errors);
                    }

                    await service.PublishAsync(new CasinoWagerMessage
                    {
                        WagerId = request.WagerId,
                        AccountId = request.AccountId,
                        Username = request.Username.Trim(),
                        Provider = request.Provider.Trim(),
                        GameName = request.GameName.Trim(),
                        Amount = request.Amount,
                        CreatedDateTime = request.CreatedDateTime
                    }, cancellationToken);

                    return TypedResults.Accepted(
                        $"/api/player/{request.AccountId}/casino",
                        new WagerAcceptedResponse(request.WagerId, "accepted"));
                })
            .WithName("SubmitCasinoWager")
            .WithTags("Player Wagers")
            .WithSummary("Accept a casino wager")
            .WithDescription(
                "Validates and publishes a wager for asynchronous persistence. A 202 response means the message broker confirmed publication; it does not mean the wager has already been stored.")
            .Produces<WagerAcceptedResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/player/{playerId:guid}/casino",
                async Task<Results<Ok<CasinoWagerPageResponse>, ValidationProblem>> (
                    Guid playerId,
                    int? pageSize,
                    int? page,
                    ICasinoWagerService service,
                    CancellationToken cancellationToken) =>
                {
                    var requestedPage = page ?? 1;
                    var requestedPageSize = pageSize ?? 10;
                    var errors = new Dictionary<string, string[]>();

                    if (requestedPage < 1)
                    {
                        errors["page"] = ["Page must be greater than zero."];
                    }

                    if (requestedPageSize is < 1 or > 100)
                    {
                        errors["pageSize"] = ["Page size must be between 1 and 100."];
                    }

                    if (errors.Count > 0)
                    {
                        return TypedResults.ValidationProblem(errors);
                    }

                    var result = await service.GetHistoryAsync(
                        playerId,
                        requestedPage,
                        requestedPageSize,
                        cancellationToken);

                    var response = new CasinoWagerPageResponse(
                        result.Data.Select(wager => new CasinoWagerResponse(
                            wager.WagerId,
                            wager.GameName,
                            wager.Provider,
                            wager.Amount,
                            wager.CreatedDateTime)).ToArray(),
                        result.Page,
                        result.PageSize,
                        result.Total,
                        result.TotalPages);

                    return TypedResults.Ok(response);
                })
            .WithName("GetPlayerCasinoWagers")
            .WithTags("Player Wagers")
            .WithSummary("Get a player's latest casino wagers")
            .WithDescription(
                "Returns wagers for the account identified by playerId, ordered newest first. page defaults to 1; pageSize defaults to 10 and must be between 1 and 100.")
            .Produces<CasinoWagerPageResponse>()
            .ProducesValidationProblem();

        app.MapGet("/api/player/topSpenders",
                async Task<Results<Ok<IReadOnlyList<TopSpenderResponse>>, ValidationProblem>> (
                    int? count,
                    ICasinoWagerService service,
                    CancellationToken cancellationToken) =>
                {
                    var requestedCount = count ?? 10;
                    if (requestedCount is < 1 or > 100)
                    {
                        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                        {
                            ["count"] = ["Count must be between 1 and 100."]
                        });
                    }

                    var spenders = await service.GetTopSpendersAsync(requestedCount, cancellationToken);
                    IReadOnlyList<TopSpenderResponse> response = spenders
                        .Select(spender => new TopSpenderResponse(
                            spender.AccountId,
                            spender.Username,
                            spender.TotalAmountSpend))
                        .ToArray();

                    return TypedResults.Ok(response);
                })
            .WithName("GetTopSpenders")
            .WithTags("Player Wagers")
            .WithSummary("Get the highest-spending accounts")
            .WithDescription(
                "Ranks accounts by the sum of their stored wager amounts in descending order. count defaults to 10 and must be between 1 and 100.")
            .Produces<IReadOnlyList<TopSpenderResponse>>()
            .ProducesValidationProblem();

        return app;
    }
}
