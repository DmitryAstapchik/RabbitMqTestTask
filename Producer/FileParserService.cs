using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;

namespace Producer
{
    internal class FileParserService
    {
        static readonly Random random = new();
        private System.Timers.Timer _timer;
        private Queue<string> _files;
        private static void ChangeModuleStateValues(string xmlFilePath)
        {
            XmlDocument doc = new();
            doc.Load(xmlFilePath);
            XmlNode root = doc.DocumentElement!;
            XmlNodeList myNodes = root.SelectNodes("//ModuleState")!;
            var states = new[] { "Online", "Run", "NotReady", "Offline" };
            foreach (XmlNode myNode in myNodes)
            {
                var randomIndex = random.Next(states.Length);
                myNode.InnerText = states[randomIndex];
            }
            doc.Save(xmlFilePath);
        }

        private static string ConvertXmlToJson(string xmlFilePath)
        {
            XmlDocument doc = new();
            doc.Load(xmlFilePath);
            return JsonConvert.SerializeXmlNode(doc);
        }
        private static void Send(string message)
        {
            var hostname = ConfigurationManager.AppSettings.Get("HostName");
            var queueName = ConfigurationManager.AppSettings.Get("QueueName");
            var factory = new ConnectionFactory() { HostName = hostname };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.QueueDeclare(queue: queueName,
                           durable: false,
                           exclusive: false,
                           autoDelete: false,
                           arguments: null);
            var body = Encoding.UTF8.GetBytes(message);
            channel.BasicPublish(exchange: "",
                           routingKey: queueName,
                           basicProperties: null,
                           body: body);
            Console.WriteLine($" [x] Sent {message}");
        }

        internal void ReadXmlFiles(string directoryPath)
        {
            _timer = new(1000);
            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = true;
            _files = new(Directory.EnumerateFiles(directoryPath, "*.xml"));
            _timer.Start();
        }

        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            if (_files.TryDequeue(out var file))
            {
                new Thread(() =>
                {
                    ChangeModuleStateValues(file);
                    var json = ConvertXmlToJson(file);
                    Send(json);
                }).Start();
            }
            else
            {
                _timer.Stop();
                _timer.Dispose();
            }
        }
    }
}
