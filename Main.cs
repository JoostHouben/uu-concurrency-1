using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace b3cc_opgave1
{
    static class ConsoleWrapper
    {
        private static string[] tokens;
        private static int index;

        public static bool IsEmpty
        {
            get
            {
                string v = Read();
                index--;
                return v == null;
            }
        }

        public static string Read()
        {
            if (index < 0)
                return null;
            if (tokens == null || index == tokens.Length)
            {
                string input = Console.ReadLine();
                if (input == null)
                {
                    index = -1;
                    return null;
                }
                tokens = input.Split(' ');
                index = 0;
            }
            return tokens[index++];
        }

        public static int ReadInt()
        {
            return int.Parse(Read());
        }

        public static long ReadLong()
        {
            return long.Parse(Read());
        }

        public static double ReadDouble()
        {
            return double.Parse(Read());
        }
    }

    class Main
    {
        static void Main(string[] args)
        {
            int l = ConsoleWrapper.ReadInt();
            int b = ConsoleWrapper.ReadInt();
            int e = ConsoleWrapper.ReadInt();
            int m = ConsoleWrapper.ReadInt();
            int p = ConsoleWrapper.ReadInt();
            int u = ConsoleWrapper.ReadInt();
            string h = "";
            if(u == 2)
                h = ConsoleWrapper.Read();


        }
    }
}
