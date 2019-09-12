using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StockX
{
    class Program
    {
        private static readonly CookieContainer Cookies = new CookieContainer();
        private static readonly HttpClientHandler Handler = new HttpClientHandler();
        private static string currentShoe = "";

        private static void Main(string[] args)
        {
            try
            {
                var prog = new Program();
                prog.MainAsync().Wait();
                Console.WriteLine("Done");
                Console.ReadLine();
            }
            catch(Exception exception)
            {
                Console.WriteLine(exception);
                Console.ReadLine();
            }

        }



        private async Task MainAsync()
        {
            Handler.CookieContainer = Cookies;
            Handler.AllowAutoRedirect = true;
            HttpClient client = new HttpClient(Handler);

            Console.WriteLine(String.Format($"[{DateTime.Now:hh:mm:ss.fff}] Enter StockX email"));
            var email = Console.ReadLine();

            Console.WriteLine(String.Format($"[{DateTime.Now:hh:mm:ss.fff}] Enter StockX password"));

            string password = "";
            do
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                // Backspace Should Not Work
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else
                {
                    if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                    {
                        password = password.Substring(0, (password.Length - 1));
                        Console.Write("\b \b");
                    }
                    else if (key.Key == ConsoleKey.Enter)
                    {
                        break;
                    }
                }
            } while (true);

            List<ShoeLookup> shoeLookups = await GetUrlsAsync(client);
            bool loggedIn = await LoginAsync(email, password, client);
            if (loggedIn)
            {
                foreach (ShoeLookup shoeLookup in shoeLookups)
                {
                    var shoesList = await GetProductInfo(shoeLookup.Url, shoeLookup.Sizes, client);
                    await CheckPayoutAsync(shoesList, client);
                }
            }
        }


        private static async Task<List<ShoeLookup>> GetUrlsAsync(HttpClient client)
        {
            List<ShoeLookup> shoeLookups = new List<ShoeLookup>();
            string line;
            string accountsPath = Directory.GetCurrentDirectory() + "\\urls.txt";
            StreamReader file = new StreamReader(accountsPath);
            Console.WriteLine(String.Format($"\n[{DateTime.Now:hh:mm:ss.fff}] Found input file"));
            while ((line = file.ReadLine()) != null)
            {
                ShoeLookup shoeLookup = new ShoeLookup();
                if (!line.Contains("https://stockx.com/"))
                {
                    //SKU Lookup
                    var split = line.Split(' ');
                    var sku = split[0];
                    var lookupUrl = "https://xw7sbct9v6-dsn.algolia.net/1/indexes/products/query";
                    JObject json = new JObject(new JProperty("params", "query={" + sku + "}&hitsPerPage=100"));
                    var content = new StringContent(json.ToString(), Encoding.UTF8, "application/json");

                    if (!client.DefaultRequestHeaders.Contains("x-algolia-agent"))
                    {
                        client.DefaultRequestHeaders.Add("x-algolia-agent", "Algolia for vanilla JavaScript 3.27.1");
                    }

                    if (!client.DefaultRequestHeaders.Contains("x-algolia-api-key"))
                    {
                        client.DefaultRequestHeaders.Add("x-algolia-api-key", "6bfb5abee4dcd8cea8f0ca1ca085c2b3");
                    }

                    if (!client.DefaultRequestHeaders.Contains("x-algolia-application-id"))
                    {
                        client.DefaultRequestHeaders.Add("x-algolia-application-id", "XW7SBCT9V6");
                    }

                    var response = await client.PostAsync(lookupUrl, content);
                    var responseString = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        JObject jsonResult = JObject.Parse(responseString);
                        string urlContent = jsonResult["hits"][0]["url"].ToString();
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

                }
                else if (line.Contains(' ') & line.Contains(',') && line.Contains("https://stockx.com/"))
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
                }
                else if (line.Contains(' ') && line.Contains("https://stockx.com/"))
                {
                    var split = line.Split(' ');
                    var url = split[0];
                    url = url.Split(new[] { "https://stockx.com/" }, StringSplitOptions.None)[1];
                    url = "https://stockx.com/api/products/" + url + "?includes=market";
                    shoeLookup.Url = url;

                    string[] sizes = new string[1];
                    sizes[0] = split[1];
                    shoeLookup.Sizes = sizes;
                    shoeLookups.Add(shoeLookup);
                }
                else
                {
                    line = line.Split(new[] { "https://stockx.com/" }, StringSplitOptions.None)[1];
                    line = "https://stockx.com/api/products/" + line + "?includes=market";
                    shoeLookup.Url = line;
                    shoeLookup.Sizes = null;
                    shoeLookups.Add(shoeLookup);
                }
            }

            file.Close();
            if (!shoeLookups.Any())
            {
                Console.WriteLine("No URLs loaded from urls.txt");
                return null;
            }
            else
            {
                foreach (ShoeLookup s in shoeLookups)
                {
                    Console.WriteLine(string.Format($"[{DateTime.Now:hh:mm:ss.fff}] Loaded {s.Url}"));
                }
            }


            return shoeLookups;
        }

        private static async Task<bool> LoginAsync(string email, string password, HttpClient client)
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.131 Safari/537.36");
            JObject loginData = new JObject(new JProperty("email", email), new JProperty("password", password));
            var content = new StringContent(loginData.ToString(), Encoding.UTF8, "application/json");

            Console.WriteLine(string.Format($"[{DateTime.Now:hh:mm:ss.fff}] Logging into account {email}"));

            var response = await client.PostAsync("https://stockx.com/api/login", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                string encodedGrailsUser = Base64Encode(responseString);
                client.DefaultRequestHeaders.Add("grails-user", encodedGrailsUser);
                //response.Headers.GetValues("jwt-authorization").FirstOrDefault();

                Console.WriteLine(String.Format($"[{DateTime.Now:hh:mm:ss.fff}] Login successful"));
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
            List<Shoe> shoesList = new List<Shoe>();

            var response = await client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                JObject product = JObject.Parse(responseString);

                var title = product["Product"]["title"];
                currentShoe = title.ToString();
                Console.WriteLine(String.Format($"[{DateTime.Now:hh:mm:ss.fff}] Getting info for {title}"));

                foreach (var c in product["Product"]["children"])
                {
                    var market = c.First["market"];
                    var size = c.First["shoeSize"];
                    var lowestAsk = market["lowestAsk"];
                    var highestBid = market["highestBid"];
                    var sizeId = market["skuUuid"];
                    if (sizes == null)
                    {
                        shoesList.Add(new Shoe(size.ToString(), sizeId.ToString(), highestBid.ToString(), lowestAsk.ToString(), null, title.ToString()));
                    }
                    else
                    {
                        foreach (string s in sizes)
                        {
                            if (size.ToString().ToLower() == s || s.ToLower() == "os")
                            {
                                shoesList.Add(new Shoe(size.ToString(), sizeId.ToString(), highestBid.ToString(), lowestAsk.ToString(), null, title.ToString()));
                            } 
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine(responseString);
            }
            return shoesList;
        }
        private static async Task CheckPayoutAsync(List<Shoe> input, HttpClient client)
        {
            foreach (Shoe shoe in input)
            {
                try
                {
                    if (int.Parse(shoe.highestBid) > 0)
                    {


                        JObject pricePayload = new JObject(
                            new JProperty("context", "selling"),
                            new JProperty("products",
                            new JArray(
                                new JObject(
                                    new JProperty("sku", shoe.sizeID),
                                    new JProperty("amount", int.Parse(shoe.highestBid)),
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
                    } else
                    {
                        shoe.payout = "0";
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(string.Format($"Exception occured: {e.Message}"));
                }
                Thread.Sleep(200);
            }

            foreach (Shoe s in input)
            {
                if(s.payout != "0")
                {
                    if (!s.size.Contains('.'))
                    {
                        Console.WriteLine(string.Format($"[{DateTime.Now:hh:mm:ss.fff}] Size: {s.size}   \tPayout: ${s.payout}"));
                    }
                    else
                    {
                        Console.WriteLine(string.Format($"[{DateTime.Now:hh:mm:ss.fff}] Size: {s.size} \tPayout: ${s.payout}"));
                    }
                } else
                {
                    Console.WriteLine(string.Format($"[{DateTime.Now:hh:mm:ss.fff}] Size: {s.size} \tNo bids for this size!"));
                }

            }
        }
        private static void GetCookies(string url)
        {
            var uri = new Uri(url);
            IEnumerable<Cookie> responseCookies = Cookies.GetCookies(uri).Cast<Cookie>();
            foreach (var cookie in responseCookies)
                Console.WriteLine(cookie.Name + ": " + cookie.Value);
        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }
    }
}
