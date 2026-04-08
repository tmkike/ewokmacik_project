namespace WebApi.Contracts;

public record CreateLoanRequest(
    string BookId,
    string UserName,
    DateOnly BorrowDate);
