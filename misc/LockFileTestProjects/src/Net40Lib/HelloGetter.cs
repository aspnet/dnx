using System;

namespace Net40Lib
{
    public static class HelloGetter
    {
        public static string GetHello()
        {
            return "Hello World";
        }
    }

    public class Program
    {
        public void Main(string[] args)
        {
            Console.WriteLine((new Newtonsoft.Json.Linq.JObject()).ToString());
        }
    }
}
