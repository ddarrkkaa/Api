using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace nast.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class nasa_controller : ControllerBase
    {
        [HttpPost("post_daily_photo")]
        public async Task<ActionResult<string>> PostDailyPhoto(long chatid, string datetime)
        {
            HttpClient httpClient = new HttpClient();
            ITelegramBotClient bot = new TelegramBotClient(constants.botId);

            var response = await httpClient.GetAsync($"https://api.nasa.gov/planetary/apod?api_key={constants.apikey}&date={datetime}");
            string jsonResult = await response.Content.ReadAsStringAsync();

            dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonResult);
            string imageUrl = result.url;

            string linkText = "\u2063"; // символ-замінник
            string messageText = $"{linkText}[Астрономічне фото дня]({imageUrl})";

            await bot.SendTextMessageAsync(
                chatId: chatid,
                text: messageText,
                parseMode: ParseMode.MarkdownV2
            );

            return Ok();
        }
        [HttpGet("get_asteroids")]
        public async Task<ActionResult<List<Asteroid>>> GetAsteroids(string start_datetime, string end_datetime)
        {
            HttpClient httpClient = new HttpClient();

            var response = await httpClient.GetAsync($"http://api.nasa.gov/neo/rest/v1/feed?start_date={start_datetime}&end_date={end_datetime}&detailed=false&api_key={constants.apikey}");
            string result = await response.Content.ReadAsStringAsync();

            // Десериализация JSON в объекты Asteroid
            var jsonResult = JsonConvert.DeserializeObject<JObject>(result);
            var asteroidList = new List<Asteroid>();

            var nearEarthObjects = jsonResult["near_earth_objects"] as JObject;
            foreach (var dateAsteroids in nearEarthObjects.Properties())
            {
                foreach (var asteroidData in dateAsteroids.Value)
                {
                    string name = asteroidData["name"].ToString();
                    long epochDateCloseApproach = (long)asteroidData["close_approach_data"][0]["epoch_date_close_approach"];
                    double kilometersPerSecond = (double)asteroidData["close_approach_data"][0]["relative_velocity"]["kilometers_per_second"];
                    bool isPotentiallyHazardous = (bool)asteroidData["is_potentially_hazardous_asteroid"];
                    double estimatedDiameterMin = (double)asteroidData["estimated_diameter"]["kilometers"]["estimated_diameter_min"];
                    double estimatedDiameterMax = (double)asteroidData["estimated_diameter"]["kilometers"]["estimated_diameter_max"];

                    Asteroid asteroid = new Asteroid(name, epochDateCloseApproach, kilometersPerSecond, isPotentiallyHazardous, estimatedDiameterMin, estimatedDiameterMax);
                    long unixTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
                    if (epochDateCloseApproach / 1000 > unixTimestamp) asteroidList.Add(asteroid);
                }
            }

            return Ok(asteroidList);
        }
        [HttpPost("post_asteroids_list")]
        public async Task<ActionResult> PostAsteroidsList(long id)
        {
            HttpClient httpClient = new HttpClient();
            ITelegramBotClient bot = new TelegramBotClient(constants.botId);
            var response = await httpClient.GetAsync($"https://{constants.host}/nasa_/get_asteroids?start_datetime={DateTime.UtcNow.ToString("yyyy-MM-dd")}&end_datetime={DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd")}");
            var result = await response.Content.ReadAsStringAsync();
            List<Asteroid> asteroids = JsonConvert.DeserializeObject<List<Asteroid>>(result);
            List<Asteroid> sortedAsteroids = asteroids.OrderBy(a => a.EpochDateCloseApproach).ToList();
            int i = 1;
            foreach (Asteroid asteroid in sortedAsteroids)
            {
                DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(asteroid.EpochDateCloseApproach / 1000);
                string formattedDate = dateTimeOffset.ToString("yyyy-MM-dd, HH:mm:ss");
                await bot.SendTextMessageAsync(id, $"Номер астероїду: {i}\nНазва астероїду: {asteroid.Name}\nПриблизний час наближення до землі: {formattedDate}");
                i++;
            }
            await bot.SendTextMessageAsync(id, $"Якщо хочете додати астероїд до свого списку, використайте команду /add_asteroid_to_my_list");
            return Ok();
        }
        [HttpPut("put_asteroid_to_list")]
        public async Task<ActionResult<Asteroid>> PutAsteroidToList(long id, int number)
        {
            HttpClient httpClient = new HttpClient();
            ITelegramBotClient bot = new TelegramBotClient(constants.botId);

            var response = await httpClient.GetAsync($"https://{constants.host}/nasa_/get_asteroids?start_datetime={DateTime.UtcNow.ToString("yyyy-MM-dd")}&end_datetime={DateTime.Now.AddDays(1).ToString("yyyy-MM-dd")}");
            var result = await response.Content.ReadAsStringAsync();
            List<Asteroid> asteroids = JsonConvert.DeserializeObject<List<Asteroid>>(result);
            List<Asteroid> sortedAsteroids = asteroids.OrderBy(a => a.EpochDateCloseApproach).ToList();
            if (number > asteroids.Count || number < 1)
            {
                await bot.SendTextMessageAsync(id, "Цього астероїда немає у списку. Щоб подивитися список використайте /asteroids");
                return Ok();
            }
            Asteroid asteroid = sortedAsteroids[number - 1];

            var filter = Builders<BsonDocument>.Filter.Eq("user_id", id);
            var document = constants.collection.Find(filter).FirstOrDefault();

            var my_asteroids = document["asteroids"].AsBsonArray;
            BsonValue bson_asteroid = BsonDocument.Parse(asteroid.ToJson());

            bool hasDuplicate = my_asteroids.AsQueryable().Any(a => a["Name"].AsString == asteroid.Name);


            if (hasDuplicate)
            {
                await bot.SendTextMessageAsync(id, "Цей астероїд вже є у вашому списку.");
            }
            else
            {
                my_asteroids.Add(bson_asteroid);
                var update = Builders<BsonDocument>.Update.Set("asteroids", my_asteroids);
                constants.collection.UpdateOne(filter, update);
                await bot.SendTextMessageAsync(id, "Астероїд успішно додано до вашого списку");
            }
            return Ok();
        }
        [HttpPost("post_my_asteroids_list")]
        public async Task<ActionResult> PostMyAsteroidsList(long id)
        {
            HttpClient httpClient = new HttpClient();
            ITelegramBotClient bot = new TelegramBotClient(constants.botId);

            var filter = Builders<BsonDocument>.Filter.Eq("user_id", id);
            var document = constants.collection.Find(filter).FirstOrDefault();

            var myAsteroids = document["asteroids"].AsBsonArray;
            if (myAsteroids.Count==0)
            {
                await bot.SendTextMessageAsync(id, "Ваш список порожній");
            }
            else
            {
                for (int i = 0; i < myAsteroids.Count; i++)
                {
                    await bot.SendTextMessageAsync(id, $"Номер астероїда у вашому списку: {i + 1}\n\nЗагальна інформація про астероїд:\nНазва астероїду: {myAsteroids[i]["Name"]}\nПриблизний час наближення до Землі: {DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(myAsteroids[i]["EpochDateCloseApproach"]) / 1000).ToString("yyyy-MM-dd, HH:mm:ss")}\nШвидкість у км/с: {myAsteroids[i]["KilometersPerSecond"]}\nПотенційно небезпечний? {(myAsteroids[i]["IsPotentiallyHazardous"] == true ? "так" : "ні")}\nМаксимальний діаметр астероїда у км: {myAsteroids[i]["EstimatedDiameterMax"]}\nМінімальний діаметр астероїда у км: {myAsteroids[i]["EstimatedDiameterMin"]}");

                }
            }
            //await bot.SendTextMessageAsync(id, Convert.ToString(myAsteroids));
            return Ok();
        }

        [HttpDelete("delete_asteroid_from_list")]
        public async Task<ActionResult> DeleteAsteroidFromList(long id, int number)
        {
            HttpClient httpClient = new HttpClient();
            ITelegramBotClient bot = new TelegramBotClient(constants.botId);

            var filter = Builders<BsonDocument>.Filter.Eq("user_id", id);
            var document = constants.collection.Find(filter).FirstOrDefault();

            var myAsteroids = document["asteroids"].AsBsonArray;
            try
            {
                myAsteroids.RemoveAt(number - 1);
                var update = Builders<BsonDocument>.Update.Set("asteroids", myAsteroids);
                constants.collection.UpdateOne(filter, update);
                await bot.SendTextMessageAsync(id, "Астероїд успішно видалено з вашого списку");
            }
            catch
            {
                await bot.SendTextMessageAsync(id, "Помилка");
            }
            return Ok();
        }



        [HttpPut("user_is_subscribed_to_updates/{id}")]
        public ActionResult<string> UserIsSubscribedToUpdates(long id, bool b)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("user_id", id);
            var update = Builders<BsonDocument>.Update.Set("user_is_subscribed_to_updates", b);
            var result = constants.collection.UpdateOne(filter, update);
            return Ok();
        }

        [HttpGet("user_is_subscribed_to_updates/{id}")]
        public ActionResult<bool> UserIsSubscribedToUpdates(long id)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("user_id", id);
            var document = constants.collection.Find(filter).FirstOrDefault();
            if (document != null && document.TryGetValue("user_is_subscribed_to_updates", out BsonValue value))
            {
                return value.AsBoolean;
            }

            return NotFound();
        }
        [HttpPut("bot_is_waiting_for_number_of_asteroid_to_add/{id}")]
        public ActionResult<string> BotIsWaitingForNumberOfAsteroidToAdd(long id, bool b)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("user_id", id);
            var update = Builders<BsonDocument>.Update.Set("bot_is_waiting_for_number_of_asteroid_to_add", b);
            var result = constants.collection.UpdateOne(filter, update);
            return Ok();
        }

        [HttpGet("bot_is_waiting_for_number_of_asteroid_to_add/{id}")]
        public ActionResult<bool> BotIsWaitingForNumberOfAsteroidToAdd(long id)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("user_id", id);
            var document = constants.collection.Find(filter).FirstOrDefault();
            if (document != null && document.TryGetValue("bot_is_waiting_for_number_of_asteroid_to_add", out BsonValue value))
            {
                return value.AsBoolean;
            }

            return NotFound();
        }
        [HttpPut("bot_is_waiting_for_number_of_asteroid_to_delete/{id}")]
        public ActionResult<string> BotIsWaitingForNumberOfAsteroidToDelete(long id, bool b)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("user_id", id);
            var update = Builders<BsonDocument>.Update.Set("bot_is_waiting_for_number_of_asteroid_to_delete", b);
            var result = constants.collection.UpdateOne(filter, update);
            return Ok();
        }

        [HttpGet("bot_is_waiting_for_number_of_asteroid_to_delete/{id}")]
        public ActionResult<bool> BotIsWaitingForNumberOfAsteroidToDelete(long id)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("user_id", id);
            var document = constants.collection.Find(filter).FirstOrDefault();
            if (document != null && document.TryGetValue("bot_is_waiting_for_number_of_asteroid_to_delete", out BsonValue value))
            {
                return value.AsBoolean;
            }

            return NotFound();
        }

        [HttpPut("bot_is_waiting_for_photo_data/{id}")]
        public ActionResult<string> BotIsWaitingForPhotoData(long id, bool b)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("user_id", id);
            var update = Builders<BsonDocument>.Update.Set("bot_is_waiting_for_photo_data", b);
            var result = constants.collection.UpdateOne(filter, update);
            return Ok();
        }

        [HttpGet("bot_is_waiting_for_photo_data/{id}")]
        public ActionResult<bool> BotIsWaitingForPhotoData(long id)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("user_id", id);
            var document = constants.collection.Find(filter).FirstOrDefault();
            if (document != null && document.TryGetValue("bot_is_waiting_for_photo_data", out BsonValue value))
            {
                return value.AsBoolean;
            }

            return NotFound();
        }
    }
}