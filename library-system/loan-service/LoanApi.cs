static class LoanApi
{
    public static void Map(WebApplication app)
    {
        var loans = app.MapGroup("/api/loans")
            .WithTags("Loans");

        LoanQueryEndpoints.Map(loans);
        LoanCommandEndpoints.Map(loans);

        var internalLoans = app.MapGroup("/internal/loans");
        LoanQueryEndpoints.MapInternal(internalLoans);
    }
}
