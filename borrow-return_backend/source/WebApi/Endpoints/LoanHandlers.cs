using Domain;
using Microsoft.AspNetCore.Mvc;
using WebApi.Contracts;
using WebApi.Repos;
using WebApi.Services;

namespace WebApi.Endpoints;

public static class LoanHandlers
{
    public static RouteGroupBuilder MapLoanEndpoints(this RouteGroupBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/loans").WithTags("Loans");

        group.MapGet("", async ([FromServices] ILoanRepository repository, CancellationToken ct) =>
        {
            IReadOnlyList<Loan> loans = await repository.GetLoans(ct);
            return Results.Ok(loans);
        });

        group.MapGet("/active", async ([FromServices] ILoanRepository repository, CancellationToken ct) =>
        {
            IReadOnlyList<Loan> loans = await repository.GetActiveLoans(ct);
            return Results.Ok(loans);
        });

        group.MapPost("", async ([FromBody] CreateLoanRequest request, [FromServices] ILoanService loanService, CancellationToken ct) =>
        {
            LoanServiceResult result = await loanService.CreateLoan(request, ct);

            if (!result.Success)
            {
                return Results.Json(new MessageResponse(result.ErrorMessage ?? "Loan creation failed."), statusCode: result.StatusCode);
            }

            return Results.Created($"/api/loans/{result.Loan!.Id}", result.Loan);
        });

        group.MapPut("/{id}/return", async ([FromRoute] string id, [FromServices] ILoanService loanService, CancellationToken ct) =>
        {
            ReturnLoanResult result = await loanService.ReturnLoan(id, ct);

            if (!result.Success)
            {
                return Results.Json(new MessageResponse(result.ErrorMessage ?? "Loan return failed."), statusCode: result.StatusCode);
            }

            return Results.Ok(new ReturnLoanResponse(
                result.Loan!.Id!,
                result.Loan.Returned,
                result.Loan.ReturnDate));
        });

        return group;
    }
}
