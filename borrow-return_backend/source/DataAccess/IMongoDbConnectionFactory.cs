using MongoDB.Driver;

namespace DataAccess;

public interface IMongoDbConnectionFactory
{
    IMongoDatabase GetDatabase();
}
