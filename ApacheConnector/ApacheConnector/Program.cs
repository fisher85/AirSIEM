using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ApacheConnector
{
    class Program
    {
        static void Main(string[] args)
        {
            int timeout = 1000;
            string connectorName = "ApacheConnector";
            string logFileName = @"c:\xampp\apache\logs\access.log";
            string rabbitUri = "amqp://siemuser:siempass@192.168.137.1";
            string rabbitQueueName = "AirSIEM_ConnectorQueue";

            WatchLogFile(logFileName, timeout, connectorName, rabbitUri, rabbitQueueName);
            Console.ReadLine(); // Prevent the process from terminating 
        }

        static async void WatchLogFile(string logFileName, int timeout,
            string connectorName, string rabbitUri, string rabbitQueueName)
        {
            Console.WriteLine("Log file: {0}", logFileName);
            Console.WriteLine("Watching log file..." + Environment.NewLine);

            try
            {
                long lastFileSize = 0;
                
                // Remember the current file size to track changes
                using (FileStream fs = new FileStream(logFileName, FileMode.Open, 
                    FileAccess.Read, FileShare.ReadWrite))
                {
                    lastFileSize = fs.Length;
                }

                // Watch log file
                while (true)
                {
                    Console.WriteLine(DateTime.Now.ToString("H:mm:ss"));

                    List<string> messageList = new List<string>();

                    using (FileStream fs = new FileStream(logFileName, FileMode.Open, 
                        FileAccess.Read, FileShare.ReadWrite))
                    {
                        // Set stream position
                        long newFileSize = fs.Length;
                        if (newFileSize >= lastFileSize) fs.Position = lastFileSize;
                        if (newFileSize < lastFileSize) lastFileSize = 0; // If the file was overwritten

                        // Read last lines
                        using (StreamReader sr = new StreamReader(fs))
                        {
                            string newFileLines = null;
                            newFileLines = sr.ReadToEnd();
                            if (newFileLines != null)
                            {
                                lastFileSize = newFileSize;
                                if (newFileLines.Length > 0)
                                    messageList.AddRange(newFileLines.Split(new string[] { Environment.NewLine },
                                        StringSplitOptions.RemoveEmptyEntries));
                            }
                        }

                        // Send new lines to RabbitMQ
                        if (messageList.Count > 0)
                        {
                            ConnectionFactory factory = new ConnectionFactory() { Uri = new Uri(rabbitUri) };
                            using (var rConnection = factory.CreateConnection())
                            using (var channel = rConnection.CreateModel())
                            {
                                channel.QueueDeclare(queue: rabbitQueueName, durable: false, 
                                    exclusive: false, autoDelete: false, arguments: null);

                                foreach (var message in messageList)
                                {
                                    byte[] body = Encoding.UTF8.GetBytes(connectorName + ":" + message);
                                    channel.BasicPublish(exchange: "", routingKey: rabbitQueueName, 
                                        basicProperties: null, body: body);
                                }
                            }

                            // Show statistics
                            Console.WriteLine(Environment.NewLine);
                            Console.WriteLine("  Lines sent to RabbitMQ: {0}", messageList.Count);
                            Console.WriteLine("  Last line: {0}", messageList[messageList.Count - 1]);
                            Console.WriteLine(Environment.NewLine);
                        }
                    }

                    // Delay without blocking a thread
                    await Task.Delay(timeout);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex.ToString());
            }
        }
    }
}