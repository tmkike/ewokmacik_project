using WebApi.Contracts;

namespace WebApi.Services;

public interface ILoanService
{
    Task<LoanServiceResult> CreateLoan(CreateLoanRequest request, CancellationToken ct = default);
    Task<ReturnLoanResult> ReturnLoan(string id, CancellationToken ct = default);
}
