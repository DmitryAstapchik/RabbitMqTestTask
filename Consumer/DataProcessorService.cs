using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Configuration;

namespace Consumer
{
    internal class DataProcessorService
    {
        private readonly ApplicationContext _context = new();
        public DataProcessorService()
        {
            _context.Database.EnsureCreated();
        }
        internal void Receive()
        {
            var hostname = ConfigurationManager.AppSettings.Get("HostName");
            var queueName = ConfigurationManager.AppSettings.Get("QueueName");
            var factory = new ConnectionFactory { HostName = hostname };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.QueueDeclare(queue: queueName,
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);
            Console.WriteLine(" [*] Waiting for messages.");
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [x] Received {message}");
                Save(message);
            };
            channel.BasicConsume(queue: queueName,
                                 autoAck: true,
                                 consumer: consumer);
            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }

        private void Save(string json)
        {
            JObject o = JObject.Parse(json);
            foreach (var status in o["InstrumentStatus"]["DeviceStatus"])
            {
                var newInfo = new ModuleInfo
                {
                    ModuleCategoryId = (string)status["ModuleCategoryID"],
                    ModuleState = (string)status.SelectToken("$..ModuleState")
                };
                var dbInfo = _context.ModuleInfos.SingleOrDefault(m => m.ModuleCategoryId == newInfo.ModuleCategoryId);
                if (dbInfo != null)
                {
                    dbInfo.ModuleState = newInfo.ModuleState;
                }
                else
                {
                    _context.Add(newInfo);
                }
            }
            _context.SaveChanges();
        }
    }
}
