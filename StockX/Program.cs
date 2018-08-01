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
        private static string currentShoe = "";


        class Shoe
        {
            public Shoe(string size, string sizeID, string highestBid, string lowestAsk, string payout,string name)
            {
                this.size = size;
                this.sizeID = sizeID;
                this.highestBid = highestBid;
                this.lowestAsk = lowestAsk;
                this.payout = payout;
                this.name = name;
            }

            public string size { get; set; }
            public string sizeID { get; set; }
            public string highestBid { get; set; }
            public string lowestAsk { get; set; }
            public string payout { get; set; }
            public string name { get; set; }
        }

        class ShoeLookup
        {
            public ShoeLookup()
            {
            }

            public ShoeLookup(string url, string[] sizes)
            {
                Url = url;
                Sizes = sizes;

            }

            public string Url { get; set; }
            public string[] Sizes { get; set; }
        }

        static void Main(string[] args)
        {
            var prog = new Program();
            prog.MainAsync().Wait();
            Console.WriteLine("Done");
            Console.ReadLine();
        }



        private async Task MainAsync()
        {
            handler.CookieContainer = cookies;
            handler.AllowAutoRedirect = true;
            HttpClient client = new HttpClient(handler);

            Console.WriteLine(String.Format($"[{DateTime.Now.ToString("hh:mm:ss.fff")}] Enter StockX email"));
            var email = Console.ReadLine();

            Console.WriteLine(String.Format($"[{DateTime.Now.ToString("hh:mm:ss.fff")}] Enter StockX password"));
            var password = Console.ReadLine();

            List<ShoeLookup> shoeLookups = await GetUrlsAsync(client);
            bool LoggedIn = await LoginAsync(email, password, client);
            //bool LoggedIn = true;
            if (LoggedIn)
            {
                foreach (ShoeLookup shoeLookup in shoeLookups)
                {
                    var ShoesList = await GetProductInfo(shoeLookup.Url, shoeLookup.Sizes, client);
                    await CheckPayoutAsync(ShoesList, client);
                }

            }
        }


        private static async Task<List<ShoeLookup>> GetUrlsAsync(HttpClient client)
        {
            List<ShoeLookup> shoeLookups = new List<ShoeLookup>();
            string line = "";
            string accountsPath = Directory.GetCurrentDirectory() + "\\urls.txt";
            StreamReader file = new StreamReader(accountsPath);
            Console.WriteLine(String.Format($"[{DateTime.Now.ToString("hh:mm:ss.fff")}] Found input file"));
            while ((line = file.ReadLine()) != null)
            {
                ShoeLookup shoeLookup = new ShoeLookup();
                if (!line.Contains("https://stockx.com/"))
                {
                    //SKU Lookup
                    var split = line.Split(' ');
                    var sku = split[0];
                    var lookupUrl = "https://xw7sbct9v6-dsn.algolia.net/1/indexes/products/query";
                    JObject json = new JObject(new JProperty("params", "query={"+sku+"}&hitsPerPage=100"));
                    var content = new StringContent(json.ToString(), Encoding.UTF8, "application/json");
                    client.DefaultRequestHeaders.Add("x-algolia-agent", "Algolia for vanilla JavaScript 3.27.1");
                    client.DefaultRequestHeaders.Add("x-algolia-api-key", "6bfb5abee4dcd8cea8f0ca1ca085c2b3");
                    client.DefaultRequestHeaders.Add("x-algolia-application-id", "XW7SBCT9V6");
                    //client.DefaultRequestHeaders.Add("x-algolia-agent", "Algolia for vanilla JavaScript 3.27.1");

                    //Console.WriteLine(String.Format($"[{DateTime.Now.ToString("hh:mm:ss.fff")}] Searching for {sku}"));

                    var response = await client.PostAsync(lookupUrl, content);
                    var responseString = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        JObject jsonResult = JObject.Parse(responseString);
                        string urlContent = jsonResult["hits"][0]["url"].ToString();
                        var name = jsonResult["hits"][0]["name"].ToString();
                        //Console.WriteLine(String.Format($"[{DateTime.Now.ToString("hh:mm:ss.fff")}] Found {name}"));
                        var url = "https://stockx.com/api/products/" + urlContent + "?includes=market";
                        shoeLookup.Url = url;

                        var sizesString = split[1];
                        var sizes = sizesString.Split(',');
                        shoeLookup.Sizes = sizes;
                        shoeLookups.Add(shoeLookup);
                    }
                    else
                    {
                        Console.WriteLine(responseString);
                    }

                } else if (line.Contains(' ') & line.Contains(',') && line.Contains("https://stockx.com/"))
                {
                    var split = line.Split(' ');

                    //add "/products/api' and '?includes=market' to link
                    var url = split[0];
                    url = url.Split(new[] { "https://stockx.com/" }, StringSplitOptions.None)[1];
                    url = "https://stockx.com/api/products/" + url + "?includes=market";
                    shoeLookup.Url = url;

                    var sizesString = split[1];
                    var sizes = sizesString.Split(',');
                    shoeLookup.Sizes = sizes;
                    shoeLookups.Add(shoeLookup);
                } else
                {
                    line = line.Split(new[] { "https://stockx.com/" }, StringSplitOptions.None)[1];
                    line = "https://stockx.com/api/products/" + line + "?includes=market";
                    shoeLookup.Url = line;
                    shoeLookup.Sizes = null;
                    shoeLookups.Add(shoeLookup);
                }
            }

            file.Close();
            if (shoeLookups.Count() == 0)
            {
                Console.WriteLine("No URLs loaded from urls.txt");
                return null;
            } else
            {
                //Console.WriteLine(String.Format($"Loaded {shoeLookups.Count()} URLs/SKUs"));
                foreach (ShoeLookup s in shoeLookups)
                {
                    Console.WriteLine(String.Format($"[{DateTime.Now.ToString("hh:mm:ss.fff")}] Loaded {s.Url}"));
                }
            }
            

            return shoeLookups;
        }
    
        private static async Task<bool> LoginAsync(string email, string password, HttpClient client)
        {
            JObject loginData = new JObject(new JProperty("email", email), new JProperty("password", password));
            var content = new StringContent(loginData.ToString(), Encoding.UTF8, "application/json");

            Console.WriteLine(String.Format($"[{DateTime.Now.ToString("hh:mm:ss.fff")}] Logging into account {email}"));

            var response = await client.PostAsync("https://stockx.com/api/login", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                string encodedGrailsUser = Base64Encode(responseString);
                client.DefaultRequestHeaders.Add("grails-user", encodedGrailsUser);
                string jwt = response.Headers.GetValues("jwt-authorization").FirstOrDefault();

                Console.WriteLine(String.Format($"[{DateTime.Now.ToString("hh:mm:ss.fff")}] Login successful... skrt"));
                return true;
            }
            else
            {
                Console.WriteLine(responseString);
                return false;
            }
        }

        private static async Task<List<Shoe>> GetProductInfo(string url, string[] sizes, HttpClient client)
        {
            List<Shoe> ShoesList = new List<Shoe>();

            var response = await client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                JObject product = JObject.Parse(responseString);

                var title = product["Product"]["title"];
                CreateEmptyFile(title.ToString());
                currentShoe = title.ToString();
                Console.WriteLine(String.Format($"[{DateTime.Now.ToString("hh:mm:ss.fff")}] Getting info for {title}"));

                foreach (var c in product["Product"]["children"])
                {
                    var market = c.First["market"];
                    var size = market["lowestAskSize"];
                    var lowestAsk = market["lowestAsk"];
                    var highestBid = market["highestBid"];
                    var sizeID = market["skuUuid"];
                    if(sizes == null)
                    {
                        ShoesList.Add(new Shoe(size.ToString(), sizeID.ToString(), highestBid.ToString(), lowestAsk.ToString(), null, title.ToString()));
                    } else
                    {
                        foreach (string s in sizes)
                        {
                            if (size.ToString() == s)
                            {
                                ShoesList.Add(new Shoe(size.ToString(), sizeID.ToString(), highestBid.ToString(), lowestAsk.ToString(), null, title.ToString()));

                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine(responseString);
            }
            return ShoesList;
        }
        private static async Task CheckPayoutAsync(List<Shoe> input, HttpClient client)
        {
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
                if(!s.size.Contains('.'))
                {
                    Console.WriteLine(String.Format($"[{DateTime.Now.ToString("hh:mm:ss.fff")}] Size: {s.size}   \tPayout: ${s.payout}"));

                } else
                {
                    Console.WriteLine(String.Format($"[{DateTime.Now.ToString("hh:mm:ss.fff")}] Size: {s.size} \tPayout: ${s.payout}"));
                }
            }

            using (StreamWriter file = new StreamWriter(String.Format($"{Directory.GetCurrentDirectory()}\\{currentShoe}",true)))
            {
                foreach (Shoe s in input)
                {
                    file.WriteLine(String.Format($"{s.size}: {s.payout}"));
                }
                file.Close();
            }
            //Console.WriteLine("\n");
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
        public static void CreateEmptyFile(string filename)
        {
            File.Create(filename).Dispose();
        }

    }
}
