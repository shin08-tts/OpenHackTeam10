using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Configuration;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;

namespace GetRating
{
    public static class CreateRating
    {
        [FunctionName("CreateRating")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string userId = req.Query["userId"];
            string productId = req.Query["productId"];
            string locationName = req.Query["locationName"];
            string rating = req.Query["rating"];
            string userNotes = req.Query["userNotes"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            userId = userId ?? data?.userId;
            productId = productId ?? data?.productId;
            locationName = locationName ?? data?.locationName;
            rating = rating ?? data?.rating;
            userNotes = userNotes ?? data?.userNotes;

            var userIdCheck = await CheckUserId(userId);
            var productIdCheck = await CheckProductId(productId);
            bool ratingCheck = false;
            int test = int.Parse(rating);
            if (0 <= int.Parse(rating) && int.Parse(rating) <= 5)
            {
                ratingCheck = true;
            }

            if(userIdCheck && productIdCheck && ratingCheck)
            {
                var newItem = new Rating();
                newItem.id = Guid.NewGuid().ToString();
                newItem.locationName = locationName;
                newItem.productId = productId;
                newItem.rating = int.Parse(rating);
                //newItem.timestamp = DateTime.UtcNow.ToString();
                newItem.userId = userId;
                newItem.userNotes = userNotes;

                // TODOFConnect DB
                try
                {
                    Console.WriteLine("Beginning operations...\n");
                    CosmosDBAccess p = new CosmosDBAccess();
                    var response = p.CerateRatingDataAsync(newItem);
                }
                catch (CosmosException de)
                {
                    Exception baseException = de.GetBaseException();
                    Console.WriteLine("{0} error occurred: {1}", de.StatusCode, de);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: {0}", e);
                }
                finally
                {
                    Console.WriteLine("End of demo, press any key to exit.");
                    //Console.ReadKey();
                }
                return (ActionResult)new OkObjectResult($"success_{newItem}");
            }

            
            // Error Message
            string errorMessage = null;
            if (!userIdCheck)
            {
                errorMessage += "Incorrect userId";
            }
            if (!productIdCheck)
            {
                errorMessage += " Incorrect productId";
            }
            if (!ratingCheck)
            {
                errorMessage += " Incorect rating";
            }


            return userIdCheck && productIdCheck && ratingCheck
                ? (ActionResult)new OkObjectResult($"Succeed. TODO Create JSON")
                : new BadRequestObjectResult($"Error:{errorMessage}");
        }

        private async static Task<bool> CheckUserId(string userid)
        {
            HttpResponseMessage response = new HttpResponseMessage();
            using (var client = new HttpClient())
            {
                response = await client.GetAsync($"https://serverlessohuser.trafficmanager.net/api/GetUser?userid={userid}");
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
               return false;
            }
            
            var json = await response.Content.ReadAsStringAsync();
            var jobj = JObject.Parse(json);
            JValue userIdValue = (JValue)jobj["userId"];
            string uId = (string)userIdValue.Value;
            
            return true;
        }

        private async static Task<bool> CheckProductId(string productid)
        {
            HttpResponseMessage response = new HttpResponseMessage();
            using (var client = new HttpClient())
            {
                response = await client.GetAsync($"https://serverlessohproduct.trafficmanager.net/api/GetProduct?productid={productid}");


            }
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                return false;
            }
            var json = await response.Content.ReadAsStringAsync();
            var jobj = JObject.Parse(json);
            JValue productIdValue = (JValue)jobj["productId"];
            string pId = (string)productIdValue.Value;

            return true;
        }

        public class CosmosDBAccess
        {
            // ADD THIS PART TO YOUR CODE

            // The Azure Cosmos DB endpoint for running this sample.
            private static readonly string EndpointUri = System.Environment.GetEnvironmentVariable("CosmosDBEndpointUri");

            // The primary key for the Azure Cosmos account.
            private static readonly string PrimaryKey = System.Environment.GetEnvironmentVariable("CosmosDBPrimaryKey");

            // The Cosmos client instance
            private CosmosClient cosmosClient;

            // The database we will create
            private Database database;

            // The container we will create.
            private Container container;

            // The name of the database and container we will create
            private string databaseId = "RatingDatabase";
            private string containerId = "RatingContainer";
            public CosmosDBAccess()
            {
                // Create a new instance of the Cosmos Client
                this.cosmosClient = new CosmosClient(EndpointUri, PrimaryKey);
            }
            public async Task GetStartedDemoAsync()
            {
                //ADD THIS PART TO YOUR CODE
                await this.CreateDatabaseAsync();
            }

            public async Task<ItemResponse<Rating>> CerateRatingDataAsync(Rating newItem)
            {
                //ADD THIS PART TO YOUR CODE
                await this.CreateDatabaseAsync();

                this.container = await this.database.CreateContainerIfNotExistsAsync(containerId, "/userId");

                ItemResponse<Rating> itemReponse = await this.container.CreateItemAsync<Rating>(newItem,new PartitionKey(newItem.userId));

                return itemReponse;
            }


            /// <summary>
            /// Create the database if it does not exist
            /// </summary>
            private async Task CreateDatabaseAsync()
            {
                // Create a new database
                this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
                Console.WriteLine("Created Database: {0}\n", this.database.Id);
            }
        }
    }
}
