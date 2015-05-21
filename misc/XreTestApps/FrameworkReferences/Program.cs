using System;
using System.IO;

public class Program 
{
    public void Main(string[] args) 
    {
        if (args.Length != 1) 
        {
            Console.Error.WriteLine("Required argument: file path");
            return;
        }

        foreach (var line in File.ReadAllLines(args[0])) 
        {
            Console.WriteLine(line);
        }
    }
}
