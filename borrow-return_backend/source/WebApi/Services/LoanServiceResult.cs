using Domain;

namespace WebApi.Services;

public record LoanServiceResult(bool Success, int StatusCode, string? ErrorMessage = null, Loan? Loan = null);
