using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConsoleAppReferencingWrappedProject
{
    public class Program
    {
        public void Main(string[] args)
        {
            Console.WriteLine($"Using types from {typeof(Net45Library.Class1).AssemblyQualifiedName}");
        }
    }
}
