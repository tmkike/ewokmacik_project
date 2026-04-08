using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace DataAccess;

public class MongoDbConnectionFactory(IConfiguration configuration) : IMongoDbConnectionFactory
{
    private readonly Lazy<IMongoDatabase> _database = new(() =>
    {
        const string connectionStringKey = "MongoDb:ConnectionString";
        const string databaseNameKey = "MongoDb:DatabaseName";

        string connectionString = configuration[connectionStringKey]
            ?? throw new InvalidOperationException($"Missing configuration value: {connectionStringKey}");
        string databaseName = configuration[databaseNameKey]
            ?? throw new InvalidOperationException($"Missing configuration value: {databaseNameKey}");

        var client = new MongoClient(connectionString);
        return client.GetDatabase(databaseName);
    });

    public IMongoDatabase GetDatabase() => _database.Value;
}
