using System;

public class Program
{
    public void Main(string[] args)
    {
        for(int i = 0; i <= 15; i++) {
            Console.ForegroundColor = (ConsoleColor)i;
            Console.WriteLine("Hello, World!");
        }
    }
}
