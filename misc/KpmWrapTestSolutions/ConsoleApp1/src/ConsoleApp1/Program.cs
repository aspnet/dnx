using System;

namespace ConsoleApp1
{
    public class Program
    {
        public void Main(string[] args)
        {
            Console.WriteLine(typeof(LibraryAlpha.Class1).Assembly.Location);
            Console.WriteLine(typeof(LibraryBeta.PCL.Desktop.Class1).Assembly.Location);
            Console.WriteLine(typeof(LibraryEpsilon.Class1).Assembly.Location);
            Console.WriteLine(typeof(LibraryGamma.Class1).Assembly.Location);
            Console.WriteLine(typeof(System.Data.Entity.Database).Assembly.Location);
            Console.WriteLine(typeof(System.Data.Entity.SqlServer.SqlFunctions).Assembly.Location);
            Console.WriteLine(typeof(LibraryDelta.Class1).Assembly.Location);
        }
    }
}
