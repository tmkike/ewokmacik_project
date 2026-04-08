using DataAccess;
using Domain;
using MongoDB.Driver;

namespace WebApi.Repos;

public class LoanRepository(IMongoDbConnectionFactory dbFactory) : ILoanRepository
{
    private IMongoCollection<Loan> Collection => dbFactory.GetDatabase().GetCollection<Loan>("loans");

    public async Task<IReadOnlyList<Loan>> GetLoans(CancellationToken ct = default)
    {
        return await Collection.Find(Builders<Loan>.Filter.Empty).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Loan>> GetActiveLoans(CancellationToken ct = default)
    {
        return await Collection.Find(loan => !loan.Returned).ToListAsync(ct);
    }

    public async Task<Loan?> GetLoanById(string id, CancellationToken ct = default)
    {
        return await Collection.Find(loan => loan.Id == id).SingleOrDefaultAsync(ct);
    }

    public async Task<Loan?> GetActiveLoanByBookId(string bookId, CancellationToken ct = default)
    {
        return await Collection.Find(loan => loan.BookId == bookId && !loan.Returned).SingleOrDefaultAsync(ct);
    }

    public async Task<Loan> CreateLoan(Loan loan, CancellationToken ct = default)
    {
        await Collection.InsertOneAsync(loan, cancellationToken: ct);
        return loan;
    }

    public async Task<Loan?> MarkAsReturned(string id, DateOnly returnDate, CancellationToken ct = default)
    {
        var update = Builders<Loan>.Update
            .Set(loan => loan.Returned, true)
            .Set(loan => loan.ReturnDate, returnDate);

        return await Collection.FindOneAndUpdateAsync(
            loan => loan.Id == id && !loan.Returned,
            update,
            new FindOneAndUpdateOptions<Loan>
            {
                ReturnDocument = ReturnDocument.After
            },
            ct);
    }

    public async Task<bool> HasActiveLoanForBook(string bookId, CancellationToken ct = default)
    {
        long count = await Collection.CountDocumentsAsync(
            loan => loan.BookId == bookId && !loan.Returned,
            cancellationToken: ct);

        return count > 0;
    }
}
