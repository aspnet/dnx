using System;
using System.IO;

namespace ConsoleApp1
{
    public class Program
    {
        public void Main(string[] args)
        {
            PrintReferencedAssemblyFileName(typeof(LibraryBeta.PCL.Desktop.Class1));
            PrintReferencedAssemblyFileName(typeof(LibraryEpsilon.Class1));
            PrintReferencedAssemblyFileName(typeof(LibraryGamma.Class1));
            PrintReferencedAssemblyFileName(typeof(System.Data.Entity.Database));
            PrintReferencedAssemblyFileName(typeof(System.Data.Entity.SqlServer.SqlFunctions));
            PrintReferencedAssemblyFileName(typeof(LibraryDelta.Class1));
        }

        public void PrintReferencedAssemblyFileName(Type type)
        {
            Console.WriteLine("Referencing {0}", Path.GetFileName(type.Assembly.Location));
        }
    }
}
