using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ConfigurationFilesReader;

namespace ConfiguratioFiles_Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            
            ConfigurationFile mycf = new ConfigurationFile("config.txt");
            List<string> mylist = mycf.getTable("MyTable");
        }
    }
}
