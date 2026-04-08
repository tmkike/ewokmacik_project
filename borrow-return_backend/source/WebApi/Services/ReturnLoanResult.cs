using Domain;

namespace WebApi.Services;

public record ReturnLoanResult(bool Success, int StatusCode, string? ErrorMessage = null, Loan? Loan = null);
