using NLog;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;

namespace AirSIEM
{
    class RabbitMQConsumer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        protected int maxLen = 100; // Max length of the output message
        protected string queueName;
        protected string uri;

        // Used to pass messages back for processing
        public delegate void onReceiveMessage(byte[] message);
        public event onReceiveMessage onMessageReceived;

        public RabbitMQConsumer(string vUri, string vQueueName)
        {
            logger.Debug("RabbitMQConsumer init");
            queueName = vQueueName;
            uri = vUri;
        }

        public void Consume()
        {
            logger.Debug("RabbitMQConsumer start");

            ConnectionFactory connectionFactory = new ConnectionFactory();
            connectionFactory.Uri = new Uri(uri);

            using (IConnection connection = connectionFactory.CreateConnection())
            {
                using (IModel channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queueName, false, false, false, null);
                    EventingBasicConsumer consumer = new EventingBasicConsumer(channel);

                    consumer.Received += (o, e) =>
                    {
                        byte[] body = e.Body;
                        string str = Encoding.ASCII.GetString(body);
                        logger.Trace("RMQ message received: {0}...", str.Substring(0, maxLen));

                        // Process the message
                        onMessageReceived(body);
                    };

                    string consumerTag = channel.BasicConsume(queueName, true, consumer);
                    Console.ReadLine();
                }
            }

            logger.Debug("RabbitMQConsumer stop");
        }
    }
}
