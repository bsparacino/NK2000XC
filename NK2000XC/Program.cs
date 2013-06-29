using System;
using System.Text;

namespace NK2000XC
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Enter COM Port:");
            string port = "COM"+Console.ReadLine(); 

            Console.WriteLine("Getting Times..."+port);
            new Times(port);
        }
    }
}
