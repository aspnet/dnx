
using System;
using System.IO;
using System.Reflection;

namespace EmbeddedResources
{
    public class Program
    {
        public void Main(string[] args)
        {
            var stream = typeof(Program).GetTypeInfo().Assembly.GetManifestResourceStream("EmbeddedResources.compiler.resources.Hello.txt");
            Console.WriteLine(ReadStream(stream));

            stream = typeof(Program).GetTypeInfo().Assembly.GetManifestResourceStream("EmbeddedResources.compiler.resources.Basic.Test.html");
            Console.WriteLine(ReadStream(stream));
        }

        public string ReadStream(Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            {
                return streamReader.ReadToEnd();
            }
        }
    }
}
