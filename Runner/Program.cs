using System;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using Loader;

public class Program
{
    public static void Main(string[] args)
    {
        if(args.Length < 2)
        {
            Console.WriteLine("runner [workingDir] [path]");
            return;
        }

        string workingDir = args[0];
        string path = args[1];

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
            var executable = new Executable(arguments[0], workingDir);

            for(var i = 0; i < arguments.Length; ++i)
            {
                arguments[i] = arguments[i].Replace("$app_path", path);
            }

            executable.Execute(String.Join(" ", arguments.Skip(1))).WaitForExit();
        }
    }
}