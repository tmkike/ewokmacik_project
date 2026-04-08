namespace WebApi.Contracts;

public record CreateBookRequest(
    string Title,
    string Author,
    int Year,
    string Genre);
