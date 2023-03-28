// See https://aka.ms/new-console-template for more information
using System;
using System.IO;
using System.Text;
using System.Text.Encodings;

namespace Ass
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Usage: Ass <file> <millisecond>");
                return;
            }
            var file = args[1];
            if(!File.Exists(file)) {
                Console.WriteLine($"file {file} not found");
                return;
            }
            if (!int.TryParse(args[2], out var millisecond)) {
                Console.WriteLine("millisecond must be an integer");
            }

            var ass = new Ass(file);
            if (millisecond > 0) {
                ass.Delay(millisecond);
            } else {
                ass.Hurry(millisecond);
            }
            ass.Save();
        }
    }
}
