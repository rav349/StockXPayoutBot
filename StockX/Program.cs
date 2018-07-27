using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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

        static void Main(string[] args)
        {
            var prog = new Program();
            prog.MainAsync().Wait();
            Console.ReadLine();
        }



        private async Task MainAsync()
        {
            var email = "rav349@gmail.com";
            var password = "120522760";

            handler.CookieContainer = cookies;
            handler.AllowAutoRedirect = true;

            HttpClient client = new HttpClient(handler);

            /*JObject jobject = new JObject();

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

                response = await client.GetAsync("https://stockx.com/api/products/adidas-yeezy-boost-350-v2-butter?includes=market");
                responseString = await response.Content.ReadAsStringAsync();

                JObject product = JObject.Parse(responseString);
                var products = product["Product"]["children"];
                
                foreach(var c in products)
                {
                    Console.WriteLine("reee" + c);
                }

                //["Product"]["breadcrumbs"]["traits"]["children"]
            } else
            {
                Console.WriteLine(responseString);
            }*/

            var response = await client.GetAsync("https://stockx.com/api/products/adidas-yeezy-boost-350-v2-butter?includes=market");
            var responseString = await response.Content.ReadAsStringAsync();

            //Console.WriteLine(responseString);
            //Console.ReadLine();
            JObject product = JObject.Parse(responseString);

            var title = product["Product"]["title"];
            Console.WriteLine(title);

            foreach (var c in product["Product"]["children"])
            {
                //Console.WriteLine(String.Format($""));
                //var sizeId = c.Path.Split('.').Last();

                var market = c.First["market"];
                var size = market["lowestAskSize"];
                var lowestAsk = market["lowestAsk"];
                var highestBid = market["highestBid"];
                var sizeID = market["skuUuid"];

                Console.WriteLine(String.Format($"Size: {size} \tLowest Ask: {lowestAsk} \tHighest Bid: {highestBid} \tSize ID: {sizeID}"));
                //Console.ReadLine();
            }
        }

        private static async Task<List<KeyValuePair<string, int>>> CheckPayoutAsync(List<KeyValuePair<string, int>> input, HttpClient client)
        {
            List < KeyValuePair<string, int> > payouts = new List<KeyValuePair<string, int>>();

            JObject pricePayload = new JObject(
                    new JProperty("context", "selling"),
                    new JProperty("products",
                    new JArray(
                        new JObject(
                            new JProperty("sku", "fabfe25e-2dcf-47c0-9824-f056d0633062"),
                            new JProperty("amount", 260),
                            new JProperty("quantity", 1)))),
                    new JProperty("discountCodes", new JArray()));
            var content = new StringContent(pricePayload.ToString(), Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://stockx.com/api/pricing", content);
            var responseString = await response.Content.ReadAsStringAsync();

            JObject jsonResult = JObject.Parse(responseString);

            string payout = jsonResult["total"].ToString();
            Console.WriteLine(payout);

            return payouts;
            
        }

        private static void GetCookies(string url)
        {
            Uri uri = new Uri(url);
            IEnumerable<Cookie> responseCookies = cookies.GetCookies(uri).Cast<Cookie>();
            foreach (Cookie cookie in responseCookies)
                Console.WriteLine(cookie.Name + ": " + cookie.Value);
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

    }
}
