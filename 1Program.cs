using System;
using Wec.Its.Metering.LGIntervalLoadDomain.Manager;

namespace LGIntervalLoadApp
{   
    public class Program
    {
        //public static void Main(string[] args)
        public static int Main(string[] args)
        {
            try
            {               
                ApplicationManager manager = new ApplicationManager();
                manager.RunApplication();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return 8000;           
            }
            return 0;
        }
    }
}
