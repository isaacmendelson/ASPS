using System;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Test application running on .NET 6");
            Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
