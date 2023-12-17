// See https://aka.ms/new-console-template for more information

using Producer;

var service = new FileParserService();
string workingDirectory = Environment.CurrentDirectory;
string projectDirectory = Directory.GetParent(workingDirectory).Parent.Parent.FullName;
service.ParseXmlFiles(projectDirectory);
Console.WriteLine(" Press [enter] to exit.");
Console.ReadLine();