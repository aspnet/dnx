using System;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using Loader;

public class Program
{
    public static void Main(string[] args)
    {
        if(args.Length == 0)
        {
            Console.WriteLine("Runner [path]");
            return;
        }

        string path = args[0];

        string projectFilePath = Path.Combine(path, "project.json");

        if (!File.Exists(projectFilePath))
        {
            Environment.Exit(-1);
            return;
        }

        var obj = JObject.Parse(File.ReadAllText(projectFilePath));

        var exec = obj["exec"];

        if(exec != null) 
        {
            var arguments = exec.ToObject<string[]>();
            var executable = new Executable(arguments[0], Directory.GetCurrentDirectory());

            for(var i = 0; i < arguments.Length; ++i)
            {
                arguments[i] = arguments[i].Replace("$app_path", path);
            }

            executable.Execute(String.Join(" ", arguments.Skip(1))).WaitForExit();
        }
    }
}