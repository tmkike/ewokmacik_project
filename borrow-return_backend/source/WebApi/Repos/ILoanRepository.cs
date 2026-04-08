using Domain;

namespace WebApi.Repos;

public interface ILoanRepository
{
    Task<IReadOnlyList<Loan>> GetLoans(CancellationToken ct = default);
    Task<IReadOnlyList<Loan>> GetActiveLoans(CancellationToken ct = default);
    Task<Loan?> GetLoanById(string id, CancellationToken ct = default);
    Task<Loan?> GetActiveLoanByBookId(string bookId, CancellationToken ct = default);
    Task<Loan> CreateLoan(Loan loan, CancellationToken ct = default);
    Task<Loan?> MarkAsReturned(string id, DateOnly returnDate, CancellationToken ct = default);
    Task<bool> HasActiveLoanForBook(string bookId, CancellationToken ct = default);
}
