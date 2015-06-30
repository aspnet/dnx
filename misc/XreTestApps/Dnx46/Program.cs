using System;

namespace Dnx46
{
    public class Program
    {
        public void Main(string[] args)
        {
            Console.WriteLine(AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName);
        }
    }
}
