using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace StockX
{
    class Program
    {
        private static CookieContainer cookies = new CookieContainer();
        private static HttpClientHandler handler = new HttpClientHandler();
        private static List<string> urls = new List<string>();


        class Shoe
        {
            public Shoe(string size, string sizeID, string highestBid, string lowestAsk, string payout)
            {
                this.size = size;
                this.sizeID = sizeID;
                this.highestBid = highestBid;
                this.lowestAsk = lowestAsk;
                this.payout = payout;
            }

            public string size { get; set; }
            public string sizeID { get; set; }
            public string highestBid { get; set; }
            public string lowestAsk { get; set; }
            public string payout { get; set; }
        }

        static void Main(string[] args)
        {
            var prog = new Program();
            prog.MainAsync().Wait();
            Console.ReadLine();
        }



        private async Task MainAsync()
        {
            handler.CookieContainer = cookies;
            handler.AllowAutoRedirect = true;
            HttpClient client = new HttpClient(handler);

            var email = "rav349@gmail.com";
            var password = "120522760";
            GetUrls();
            //bool LoggedIn = await LoginAsync(email, password, client);
            bool LoggedIn = true;
            if (LoggedIn)
            {
                foreach(string url in urls)
                {
                    var ShoesList = await GetProductInfo(url, client);
                    await CheckPayoutAsync(ShoesList, client);
                }

            }
        }


        private void GetUrls()
        {
            string line = "";
            string accountsPath = Directory.GetCurrentDirectory() + "\\urls.txt";
            //Console.WriteLine("Trying to find accounts file at " + accountsPath);
            StreamReader file = new StreamReader(accountsPath);
            if (urls.Count != 0)
            {
                urls.Clear();
            }
            while ((line = file.ReadLine()) != null)
            {
                urls.Add(line);
                Console.WriteLine(String.Format($"{DateTime.Now.ToString("hh:mm:ss.fff")}: Added {line}"));
            }

            file.Close();
            if (urls.Count() == 0)
            {
                Console.WriteLine("No URLs found in urls.txt");
                return;
            }
        }

        private static async Task<bool> LoginAsync(string email, string password, HttpClient client)
        {
            JObject loginData = new JObject(new JProperty("email", email), new JProperty("password", password));
            var content = new StringContent(loginData.ToString(), Encoding.UTF8, "application/json");

            Console.WriteLine(String.Format($"{DateTime.Now.ToString("hh:mm:ss.fff")}: Logging into account {email}"));

            var response = await client.PostAsync("https://stockx.com/api/login", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                string encodedGrailsUser = Base64Encode(responseString);
                client.DefaultRequestHeaders.Add("grails-user", encodedGrailsUser);
                string jwt = response.Headers.GetValues("jwt-authorization").FirstOrDefault();

                Console.WriteLine(String.Format($"{DateTime.Now.ToString("hh:mm:ss.fff")}: Login successful... skrt"));
                return true;
            }
            else
            {
                Console.WriteLine(responseString);
                return false;
            }
        }

        private static async Task<List<Shoe>> GetProductInfo(string url, HttpClient client)
        {
            List<Shoe> ShoesList = new List<Shoe>();

            if (!url.Contains("?includes=market"))
            {
                url = url + "?includes=market";
            }
            var response = await client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                JObject product = JObject.Parse(responseString);

                var title = product["Product"]["title"];
                Console.WriteLine(String.Format($"{DateTime.Now.ToString("hh:mm:ss.fff")}: Getting info for {title}"));

                foreach (var c in product["Product"]["children"])
                {
                    //Console.WriteLine(String.Format($""));
                    //var sizeId = c.Path.Split('.').Last();

                    var market = c.First["market"];
                    var size = market["lowestAskSize"];
                    var lowestAsk = market["lowestAsk"];
                    var highestBid = market["highestBid"];
                    var sizeID = market["skuUuid"];

                    ShoesList.Add(new Shoe(size.ToString(), sizeID.ToString(), highestBid.ToString(), lowestAsk.ToString(), null));
                }

                //Console.WriteLine(String.Format($"Size: {size} \tLowest Ask: {lowestAsk} \tHighest Bid: {highestBid} \tSize ID: {sizeID}"));
            }
            return ShoesList;
        }

        private static async Task CheckPayoutAsync(List<Shoe> input, HttpClient client)
        {
            //List < KeyValuePair<string, int> > payouts = new List<KeyValuePair<string, int>>();
            foreach (Shoe shoe in input)
            {
                try
                {
                    JObject pricePayload = new JObject(
                        new JProperty("context", "selling"),
                        new JProperty("products",
                        new JArray(
                            new JObject(
                                new JProperty("sku", shoe.sizeID),
                                new JProperty("amount", Int32.Parse(shoe.highestBid)),
                                new JProperty("quantity", 1)))),
                        new JProperty("discountCodes", new JArray()));
                    var content = new StringContent(pricePayload.ToString(), Encoding.UTF8, "application/json");

                    Console.WriteLine(String.Format($"{DateTime.Now.ToString("hh:mm:ss.fff")}: Sending request for size {shoe.size} ({shoe.sizeID})"));

                    var response = await client.PostAsync("https://stockx.com/api/pricing", content);
                    var responseString = await response.Content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode)
                    {
                        JObject jsonResult = JObject.Parse(responseString);

                        double payoutDouble = Math.Round(double.Parse(jsonResult["total"].ToString()), 2);
                        shoe.payout = payoutDouble.ToString();
                    }
                    else
                    {
                        Console.WriteLine(responseString);
                    }
                } catch (Exception e)
                {
                    Console.WriteLine(String.Format($"Exception occured: {e.Message}"));
                }


            }
            foreach (Shoe s in input)
            {
                Console.WriteLine(String.Format($"Size: {s.size} \tHighest Bid: {s.highestBid} \tSize ID: {s.sizeID} \tPAYOUT: {s.payout}"));
            }
        }

        private static void GetCookies(string url)
        {
            Uri uri = new Uri(url);
            IEnumerable<Cookie> responseCookies = cookies.GetCookies(uri).Cast<Cookie>();
            foreach (Cookie cookie in responseCookies)
                Console.WriteLine(cookie.Name + ": " + cookie.Value);
        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }


    }
}
