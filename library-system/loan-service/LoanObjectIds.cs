using MongoDB.Bson;

static class LoanObjectIds
{
    public static bool TryParse(string? value, out ObjectId objectId) => ObjectId.TryParse(value, out objectId);

    public static string ToString(ObjectId objectId) => objectId.ToString();
}
