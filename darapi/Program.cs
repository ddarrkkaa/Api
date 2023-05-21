using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Driver;

constants.mongoClient = new MongoClient("mongodb+srv://ddd:polki@cluster0.za7o538.mongodb.net/");
constants.database = constants.mongoClient.GetDatabase("nasa");
constants.collection = constants.database.GetCollection<BsonDocument>("collection1");

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "My API",
        Version = "v1"
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    c.RoutePrefix = string.Empty;
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
