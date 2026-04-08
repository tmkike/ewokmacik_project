namespace WebApi.Contracts;

public record ReturnLoanResponse(
    string Id,
    bool Returned,
    DateOnly? ReturnDate);
