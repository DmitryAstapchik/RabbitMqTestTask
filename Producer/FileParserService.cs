using CsCodeGenerator;
using CsCodeGenerator.Enums;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
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
        const string GeneratedClassesDirectory = "GeneratedClasses";
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

        private static void GenerateClasses(string fromFile, string toDirectory = @$"..\..\..\{GeneratedClassesDirectory}")
        {
            ClearDirectory(toDirectory);
            List<ClassModel> classes = new();
            XmlDocument xmlDoc = new();
            xmlDoc.Load(fromFile);
            ParseNode(xmlDoc.DocumentElement);

            CsGenerator csGenerator = new() { OutputDirectory = toDirectory };
            foreach (var @class in classes)
            {
                if (!File.Exists(Path.Combine(toDirectory, $"{@class.Name}.cs")))
                {
                    FileModel file = new(@class.Name)
                    {
                        Namespace = $"Producer.{GeneratedClassesDirectory}",
                    };
                    file.Classes.Add(@class);
                    csGenerator.Files.Add(file);
                }
            }
            csGenerator.CreateFiles();

            void ParseNode(XmlNode node)
            {
                if (node.InnerText != node.InnerXml)
                {
                    var @class = CreateClass(node.Name);
                    foreach (XmlAttribute attr in node.Attributes)
                    {
                        AddFieldToClass(@class, new Field(BuiltInDataType.String, attr.LocalName));
                    }
                    if (node.ParentNode.ParentNode != null)
                    {
                        var parentClass = classes.Single(c => c.Name == node.ParentNode.Name);
                        if (node.NextSibling?.Name == node.Name)
                        {
                            AddFieldToClass(parentClass, new Field($"List<{node.Name}>", node.Name));
                        }
                        else
                        {
                            AddFieldToClass(parentClass, new Field(node.Name, node.Name));
                        }
                    }
                    foreach (XmlNode childNode in node.ChildNodes)
                    {
                        ParseNode(childNode);
                    }
                }
                else
                {
                    var nodeText = node.InnerText;
                    if (!string.IsNullOrEmpty(nodeText))
                    {
                        var parentClass = classes.Single(c => c.Name == node.ParentNode.Name);
                        if (bool.TryParse(nodeText, out bool _))
                        {
                            AddFieldToClass(parentClass, new Field(BuiltInDataType.Bool, node.Name));
                        }
                        else if (int.TryParse(nodeText, out int _))
                        {
                            AddFieldToClass(parentClass, new Field(BuiltInDataType.Int, node.Name));
                        }
                        else if (double.TryParse(nodeText, NumberStyles.Float, CultureInfo.InvariantCulture, out double _))
                        {
                            AddFieldToClass(parentClass, new Field(BuiltInDataType.Double, node.Name));
                        }
                        else
                        {
                            AddFieldToClass(parentClass, new Field(BuiltInDataType.String, node.Name));
                        }
                    }
                }
            }

            void AddFieldToClass(ClassModel @class, Field field)
            {
                if (!@class.Fields.Any(f => f.Name == field.Name))
                {
                    field.AccessModifier = AccessModifier.Public;
                    @class.Fields.Add(field);
                }
            }

            ClassModel CreateClass(string className)
            {
                if (!classes.Any(c => c.Name == className))
                {
                    ClassModel newClass = new(className);
                    classes.Add(newClass);
                    return newClass;
                }
                else
                {
                    return classes.Single(c => c.Name == className);
                }
            }

            void ClearDirectory(string directory)
            {
                if (Directory.Exists(directory))
                {
                    DirectoryInfo di = new(directory);
                    foreach (FileInfo file in di.GetFiles())
                    {
                        file.Delete();
                    }
                    foreach (DirectoryInfo dir in di.GetDirectories())
                    {
                        dir.Delete(true);
                    }
                }
            }
        }

        internal void ParseXmlFiles(string directoryPath)
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
                    GenerateClasses(file);
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
