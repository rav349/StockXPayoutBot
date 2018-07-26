using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace StockX
{
    class Program
    {
        static void Main(string[] args)
        {
            var prog = new Program();
            prog.MainAsync().Wait();
            Console.ReadLine();
        }

        private async Task MainAsync()
        {
            HttpClient client = new HttpClient();
            
            

            //client.PostAsync("https://stockx.com/api/login");
        }
        }
}
