using MongoDB.Bson;
using MongoDB.Driver;

public class constants
{
    public static string botId = "6125366829:AAHlUTq9c1xL-ALcbl_p_Vu8FMPM_wPAb2M";
    public static string host = "api20230521200313.azurewebsites.net";
    public static MongoClient mongoClient;
    public static IMongoDatabase database;
    public static IMongoCollection<BsonDocument> collection;
    public static string apikey = "Pufy6VVfDV5JZLM5zXPDhve87WIQckZ0wM6qLlvw";
}