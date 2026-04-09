using System.Globalization;

static class LoanDateRules
{
    private const string ClientDateFormat = "yyyy-MM-dd";

    public static DateOnly GetCurrentUtcDate() => DateOnly.FromDateTime(DateTime.UtcNow);

    public static bool TryParseClientDate(string? value, out DateOnly parsedDate)
    {
        return DateOnly.TryParseExact(
            value,
            ClientDateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out parsedDate);
    }

    public static DateTime ToUtcDateTime(DateOnly value)
    {
        return DateTime.SpecifyKind(value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    }

    public static bool IsBeforeLoanedAt(DateOnly candidate, DateTime loanedAt)
    {
        return candidate < DateOnly.FromDateTime(loanedAt);
    }
}
