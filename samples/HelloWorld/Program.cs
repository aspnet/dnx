
public class Program
{
    public void Main(string[] args)
    {
        System.Console.WriteLine("Hello World!");
        System.Console.WriteLine(HelloShared.HelloSharedCode.SharedMethod());
        foreach (var arg in args)
        {
            System.Console.WriteLine(arg);
        }
    }
}
