using MongoDB.Driver;

sealed class MongoDbContext
{
    public MongoDbContext(IMongoDatabase database)
    {
        Database = database;
    }

    private IMongoDatabase Database { get; }
    private bool _indexesEnsured;

    public IMongoCollection<LoanDocument> Loans => Database.GetCollection<LoanDocument>(LoanCollectionNames.Loans);
    public IMongoCollection<PendingBookReleaseDocument> PendingBookReleases => Database.GetCollection<PendingBookReleaseDocument>(LoanCollectionNames.PendingBookReleases);

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_indexesEnsured)
        {
            return;
        }

        var uniqueActiveLoanIndex = new CreateIndexModel<LoanDocument>(
            Builders<LoanDocument>.IndexKeys.Ascending(loan => loan.BookId).Ascending(loan => loan.Status),
            new CreateIndexOptions<LoanDocument>
            {
                Name = "unique_active_loan_per_book",
                Unique = true,
                PartialFilterExpression = Builders<LoanDocument>.Filter.Eq(loan => loan.Status, LoanStatus.Active),
            });

        var loansByStatusAndDateIndex = new CreateIndexModel<LoanDocument>(
            Builders<LoanDocument>.IndexKeys.Ascending(loan => loan.Status).Descending(loan => loan.LoanedAt),
            new CreateIndexOptions<LoanDocument> { Name = "loans_by_status_and_date" });

        var pendingReleaseBookIndex = new CreateIndexModel<PendingBookReleaseDocument>(
            Builders<PendingBookReleaseDocument>.IndexKeys.Ascending(item => item.BookId),
            new CreateIndexOptions<PendingBookReleaseDocument>
            {
                Name = "pending_release_by_book",
                Unique = true,
            });

        await Loans.Indexes.CreateManyAsync([uniqueActiveLoanIndex, loansByStatusAndDateIndex], cancellationToken);
        await PendingBookReleases.Indexes.CreateOneAsync(pendingReleaseBookIndex, cancellationToken: cancellationToken);
        _indexesEnsured = true;
    }
}
