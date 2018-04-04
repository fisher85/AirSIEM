using MongoDB.Bson;
using MongoDB.Driver;
using NLog;
using System;
using System.Configuration;
using System.Text;

namespace AirSIEM
{
    class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static CorrelationEngine engine;
        private static RabbitMQConsumer consumer;

        // Read configuration from App.config
        // RabbitMQ settings
        private static string rabbitUri = GetAppSetting("RabbitMQUri", "amqp://localhost");
        private static string queueName = GetAppSetting("QueueName", "AirSIEM_ConnectorQueue");

        // MongoDB settings
        private static bool translateAlertsToDB = GetAppSetting("TranslateAlertsToDB", "true").Equals("true") ? true : false;
        private static string MongoDBConnectionString = GetAppSetting("MongoDBConnectionString", "mongodb://localhost:27017");

        public static string GetAppSetting(string key, string def = "not exists") 
        {
            if (ConfigurationManager.AppSettings[key] != null)
                return ConfigurationManager.AppSettings[key].ToString();
            return def;
        }

        static void Main(string[] args)
        {
            try
            {
                logger.Debug("----- AirSIEM start -----");

                // Create correlation core
                engine = new CorrelationEngine();
                engine.onAlertReceived += new CorrelationEngine.onReceiveAlert(handleAlert);

                // Parse rules
                engine.ParseRuleDir(GetAppSetting("RuleFolder"));

                // Create FireQueues for all rules with nonzero frequency
                engine.GenerateQueueList(engine.ruleList, ref engine.fireDictionary);

                // Create RabbitMQ consumer and start listening to the ConnectorQueue
                consumer = new RabbitMQConsumer(rabbitUri, queueName);
                consumer.onMessageReceived += new RabbitMQConsumer.onReceiveMessage(handleMessage);
                consumer.Consume();

                engine.LogStatistics();
                logger.Debug("----- AirSIEM stop -----");
            }
            catch (Exception ex)
            {
                logger.Error("Exception: " + ex.ToString());
            }
        }

        // Callback for message receive
        public static void handleMessage(byte[] utfMessage)
        {
            try
            {
                var message = Encoding.UTF8.GetString(utfMessage);
                if (message.Contains("ApacheConnector:"))
                {
                    SecurityEvent securityEvent = new SecurityEvent(message, LogMessageType.ApacheLog);
                    logger.Trace(securityEvent.ToString());

                    // Process received message
                    engine.ProcessMessage(securityEvent);
                }
            }
            catch (Exception ex)
            {
                logger.Warn("HandleMessage exception: " + ex.ToString());
            }
        }

        // Callback for alert receive
        public static void handleAlert(SecurityEvent securityEvent, Rule rule)
        {
            logger.Trace("{0}  Rule {1} matched", Assistant.GetPadding(), rule.ID);
            logger.Trace("{0}  ALERT: LEVEL {1} - {2}", Assistant.GetPadding(), rule.level, rule.description);
            Console.WriteLine("ALERT: LEVEL {1} - {2}", Assistant.GetPadding(), rule.level, rule.description);

            try
            {
                if (translateAlertsToDB)
                {
                    DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    long timeStamp = (long)(DateTime.UtcNow - UnixEpoch).TotalSeconds;

                    // http://mongodb.github.io/mongo-csharp-driver/2.2/getting_started/quick_tour/
                    var client = new MongoClient(MongoDBConnectionString);
                    var dataBase = client.GetDatabase("AirSIEM");
                    var collection = dataBase.GetCollection<BsonDocument>("alerts");

                    var alert = new BsonDocument
                    {
                        { "matching_rule_SID", rule.ID },
                        { "message", rule.description },
                        { "src_IP", securityEvent.srcIP },
                        { "src_port", securityEvent.srcPort },
                        { "dest_IP", securityEvent.destIP },
                        { "dest_port", securityEvent.destPort },
                        { "log_string", securityEvent.logString },
                        { "level", rule.level},
                        { "timestamp", DateTime.Now.ToString("HH:mm:ss") },
                        { "rule_chain", new BsonArray(new[] { 12345, 1234, 123 }) }
                    };

                    collection.InsertOne(alert);
                }
            }
            catch (Exception ex)
            {
                logger.Warn("HandleAlert exception: " + ex.ToString());
            }
        }
    }
}
