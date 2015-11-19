using System;
using System.Reflection;
using P2;

namespace P1
{
    public class Program
    {
        public static void Main(string[] args)
        {
            new Derived().Test();
        }
    }

    public class Derived : BaseClass
    {
        public override void Test()
        {
            Console.WriteLine("Derived.Test");
            base.Test();
        }
    }
}
