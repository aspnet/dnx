using System;
using Microsoft.Extensions.CompilationAbstractions;

namespace GetExports
{
    public class Program
    {
        public void Main(string[] args)
        {
            var exporter = CompilationServices.Default.LibraryExporter;
            var projectExport = exporter.GetAllExports("GetExports");
            var packageExport = exporter.GetAllExports("Microsoft.Extensions.CompilationAbstractions");
            
            Console.WriteLine($"Project: {projectExport.MetadataReferences[0].Name}");
            Console.WriteLine($"Package: {packageExport.MetadataReferences[0].Name}");
        }
    }
}
