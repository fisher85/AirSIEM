using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Diagnostics;
using System.Linq;

namespace MongoBenchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new MongoClient("mongodb://192.168.137.1:27017");
            var dataBase = client.GetDatabase("test");
            var collection = dataBase.GetCollection<BsonDocument>("events");

            // Deleting all documents 
            collection.DeleteMany(new BsonDocument());

            var template = new BsonDocument
            {
                { "matching_rule_SID", 12345 },
                { "message", "SSH brute force trying to get access to the system" },
                { "src_IP", "217.25.214.12" },
                { "src_port", 58251 },
                { "dest_IP", "192.168.0.2" },
                { "dest_port", 22 },
                { "log_string", String.Join(Environment.NewLine,
                    "Jan 01 13:06:24 FTP-SERVER SSH: No user. Possible reasons: ",
                    "Invalid username, invalid license, error while accessing user database ",
                    "< SessionID = 8416817, Listener = 192.168.0.2:22, ",
                    "Client = 217.25.214.12:58251, User = anonymous >") },
                { "level", 1 },
                { "timestamp", "1512911184" },
                { "rule_chain", new BsonArray(new[] { 123, 1234, 12345 }) }
            };

            double documentRate, operationRate;
            int documentNum, operationNum;
            int[] groupByArray = { 1, 2, 5, 10, 50, 100, 250, 500, 1000, 5000, 10000, 20000 };

            // Sequential write performance (individual and batch)
            documentNum = 20000;
            var stopWatch = new Stopwatch();

            foreach (int groupBy in groupByArray)
            {
                operationNum = documentNum / groupBy;
                stopWatch.Restart();

                for (int i = 0; i < operationNum; i++)
                {
                    var documents = Enumerable.Range(0, groupBy).Select(x => new BsonDocument(template));
                    collection.InsertMany(documents);
                }

                stopWatch.Stop();
                operationRate = operationNum / stopWatch.Elapsed.TotalSeconds;
                documentRate = documentNum / stopWatch.Elapsed.TotalSeconds;

                Debug.WriteLine("InsertMany by {0}: {1} ops in {2:F} seconds ({3:F} ops/sec) => {4:F} docs/sec",
                    groupBy, operationNum, stopWatch.Elapsed.TotalSeconds, operationRate, documentRate);
            }

            // Random read performance
            operationNum = 10000;
            stopWatch.Restart();

            for (int i = 0; i < operationNum; i++)
            {
                var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.GenerateNewId());
                var entity = collection.Find(filter).FirstOrDefault();
            }

            stopWatch.Stop();
            operationRate = operationNum / stopWatch.Elapsed.TotalSeconds;

            Debug.WriteLine("Find: {0} ops in {1:F} seconds ({2:F} ops/sec)",
                operationNum, stopWatch.Elapsed.TotalSeconds, operationRate);
        }
    }
}