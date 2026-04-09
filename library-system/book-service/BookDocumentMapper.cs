static class BookDocumentMapper
{
    public static BookResponse MapBookResponse(BookDocument book, bool hasActiveLoan = false)
    {
        return new BookResponse(
            book.Id.ToString(),
            book.Title,
            book.Author,
            book.Year,
            book.Genre,
            book.Available,
            hasActiveLoan);
    }

    public static BookInventoryResponse MapBookInventoryResponse(BookDocument book)
    {
        return new BookInventoryResponse(
            book.Id.ToString(),
            book.Title,
            book.Author,
            book.Year,
            book.Genre,
            book.Available);
    }

    public static BookDetailResponse MapBookDetailResponse(BookDocument book, LoanResponse? activeLoan)
    {
        return new BookDetailResponse(
            book.Id.ToString(),
            book.Title,
            book.Author,
            book.Year,
            book.Genre,
            book.Available,
            activeLoan is not null,
            activeLoan);
    }

    public static string GetId(BookDocument book) => book.Id.ToString();
}
