using System;
using System.Reflection;
using Newtonsoft.Json;

namespace HelloWorld
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World");

            // Use an API for something that isn't in the DNX
            JsonConvert.SerializeObject(new { X = 1, Y = 20 });
        }
    }
}
