static class LoanStatus
{
    public const string Active = "active";
    public const string Returned = "returned";

    public static bool IsActive(string? status) => string.Equals(status, Active, StringComparison.OrdinalIgnoreCase);
}
