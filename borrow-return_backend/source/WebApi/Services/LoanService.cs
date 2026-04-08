using Domain;
using WebApi.Contracts;
using WebApi.Repos;

namespace WebApi.Services;

public class LoanService(IBookRepository bookRepository, ILoanRepository loanRepository) : ILoanService
{
    public async Task<LoanServiceResult> CreateLoan(CreateLoanRequest request, CancellationToken ct = default)
    {
        Book? book = await bookRepository.GetBookById(request.BookId, ct);
        if (book is null)
        {
            return new LoanServiceResult(false, StatusCodes.Status404NotFound, "Book not found.");
        }

        if (!book.Available)
        {
            return new LoanServiceResult(false, StatusCodes.Status409Conflict, "Book is not available.");
        }

        bool hasActiveLoan = await loanRepository.HasActiveLoanForBook(request.BookId, ct);
        if (hasActiveLoan)
        {
            return new LoanServiceResult(false, StatusCodes.Status409Conflict, "Book already has an active loan.");
        }

        var loan = new Loan
        {
            BookId = request.BookId,
            UserName = request.UserName,
            BorrowDate = request.BorrowDate,
            Returned = false,
            ReturnDate = null
        };

        Loan createdLoan = await loanRepository.CreateLoan(loan, ct);
        await bookRepository.SetAvailability(request.BookId, false, ct);

        return new LoanServiceResult(true, StatusCodes.Status201Created, Loan: createdLoan);
    }

    public async Task<ReturnLoanResult> ReturnLoan(string id, CancellationToken ct = default)
    {
        Loan? existingLoan = await loanRepository.GetLoanById(id, ct);
        if (existingLoan is null)
        {
            return new ReturnLoanResult(false, StatusCodes.Status404NotFound, "Loan not found.");
        }

        if (existingLoan.Returned)
        {
            return new ReturnLoanResult(false, StatusCodes.Status409Conflict, "Loan is already returned.");
        }

        DateOnly returnDate = DateOnly.FromDateTime(DateTime.UtcNow);
        Loan? updatedLoan = await loanRepository.MarkAsReturned(id, returnDate, ct);
        if (updatedLoan is null)
        {
            return new ReturnLoanResult(false, StatusCodes.Status409Conflict, "Loan could not be returned.");
        }

        await bookRepository.SetAvailability(existingLoan.BookId, true, ct);

        return new ReturnLoanResult(true, StatusCodes.Status200OK, Loan: updatedLoan);
    }
}
