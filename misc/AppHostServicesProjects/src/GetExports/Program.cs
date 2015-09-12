using System;
using Microsoft.Dnx.Compilation;

namespace GetExports
{
    public class Program
    {
        private readonly ILibraryExporter _exporter;

        public Program(ILibraryExporter exporter)
        {
            _exporter = exporter;
        }

        public void Main(string[] args)
        {
            var projectExport = _exporter.GetAllExports("GetExports");
            var packageExport = _exporter.GetAllExports("Microsoft.Dnx.Compilation.Abstractions");
            
            Console.WriteLine($"Project: {projectExport.MetadataReferences[0].Name}");
            Console.WriteLine($"Package: {packageExport.MetadataReferences[0].Name}");
        }
    }
}
