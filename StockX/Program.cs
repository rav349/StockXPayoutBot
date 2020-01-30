using HtmlAgilityPack;
using Newtonsoft.Json;
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
using System.Web;

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
            catch (Exception exception)
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

            LogWithTime("Enter StockX email");
            var email = Console.ReadLine();

            LogWithTime("Enter StockX password");

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

            Console.WriteLine();

            List<ShoeLookup> shoeLookups = await GetUrlsAsync(client);
            bool loggedIn = await LoginAsync(email, password, client);
            //bool loggedIn = true;
            if (loggedIn)
            {
                foreach (ShoeLookup shoeLookup in shoeLookups)
                {
                    var shoesList = await GetProductInfo(shoeLookup.Url, shoeLookup.Sizes, client);
                    await CheckPayoutAsync(shoesList, client);
                }
            }
        }

        /// <summary>
        /// Builds the endpoint for each shoe and sets search headers
        /// </summary>
        /// <param name="client"></param>
        /// <returns>List of shoe and sizes</returns>
        private async Task<List<ShoeLookup>> GetUrlsAsync(HttpClient client)
        {
            List<ShoeLookup> shoeLookups = new List<ShoeLookup>();
            string line;
            string accountsPath = Directory.GetCurrentDirectory() + "\\urls.txt";
            StreamReader file = new StreamReader(accountsPath);
            LogWithTime("Found input file");
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
                    LogWithTime($"Loaded { s.Url}");
                }
            }


            return shoeLookups;
        }
        /// <summary>
        /// Controls the whole login flow
        /// </summary>
        /// <param name="email"></param>
        /// <param name="password"></param>
        /// <param name="client"></param>
        /// <returns>Boolean of login result</returns>
        private async Task<bool> LoginAsync(string email, string password, HttpClient client)
        {
            LogWithTime($"Logging into account {email}");

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.88 Safari/537.36");
            client.DefaultRequestHeaders.Add("Postman-Token", "helloworld");

            var homeUrl = "https://stockx.com/";
            var response = await client.GetAsync(homeUrl);
            var responseString = await response.Content.ReadAsStringAsync();

            //Get session ID
            var stockXSessionId = GetSessionId(responseString);

            var authorizeEndpoint = $"https://accounts.stockx.com/authorize?client_id=OVxrt4VJqTx7LIUKd661W0DuVMpcFByD&response_type=code&scope=openid%20profile&stockx-session-id={stockXSessionId}&stockx-default-tab=login&stockx-is-gdpr=false&stockx-language=en-us&stockx-url=https%3A%2F%2Fstockx.com&redirect_uri=https%3A%2F%2Fstockx.com%2Fcallback%3Fpath%3D%2F&response_mode=query&state=%7B%7D&connection=production&audience=gateway.stockx.com&auth0Client=eyJuYW1lIjoiYXV0aDAuanMiLCJ2ZXJzaW9uIjoiOS4xMS4zIn0%3D";
            response = await client.GetAsync(authorizeEndpoint);

            //Get the redirect URL
            var loginUrl = response.RequestMessage.RequestUri;
            //Get state value from redirect URL QSP
            var state = HttpUtility.ParseQueryString(loginUrl.Query).Get("state");
            //Get CSRF token
            var csrf = GetSetCookieHeaderValue(response);

            //Get Bearer Token
            bool loginResult = await SendLoginRequest(csrf, state, email, password, loginUrl.ToString(), client);

            //Check client headers
            if (loginResult)
            {
                LogWithTime("Login successful");
                return true;
            }

            Console.WriteLine("Login Failed");
            return false;
        }
        /// <summary>
        /// Gets the product information for the product in the sizes specified 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="sizes"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task<List<Shoe>> GetProductInfo(string url, string[] sizes, HttpClient client)
        {
            List<Shoe> shoesList = new List<Shoe>();

            var response = await client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                JObject product = JObject.Parse(responseString);

                var title = product["Product"]["title"];
                currentShoe = title.ToString();
                LogWithTime($"Getting info for {title}");

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
        /// <summary>
        /// Sends request to check pay out for each shoe
        /// </summary>
        /// <param name="input"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task CheckPayoutAsync(List<Shoe> input, HttpClient client)
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
                            new JProperty("discountCodes", new JArray("")));
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
                    }
                    else
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
                if (s.payout != "0")
                {
                    if (!s.size.Contains('.'))
                    {
                        LogWithTime($"Size: {s.size}   \tPayout: ${s.payout}");
                    }
                    else
                    {
                        LogWithTime($"Size: {s.size} \tPayout: ${s.payout}");
                    }
                }
                else
                {
                    LogWithTime($"Size: {s.size} \tNo bids for this size!");
                }

            }
        }
        /// <summary>
        /// Helper method to print all cookies from a given domain
        /// </summary>
        /// <param name="domain"></param>
        private static void GetCookies(string domain)
        {
            var uri = new Uri(domain);
            IEnumerable<Cookie> responseCookies = Cookies.GetCookies(uri).Cast<Cookie>();
            foreach (var cookie in responseCookies)
                Console.WriteLine(cookie.Name + ": " + cookie.Value);
        }
        /// <summary>
        /// Helper method to Base64 Encode
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns>Base64 encoded string</returns>
        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }
        /// <summary>
        /// Extracts the session ID from the HTML Page
        /// </summary>
        /// <param name="html"></param>
        /// <returns>StockX Session ID as string</returns>
        private static string GetSessionId(string html)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            var x = htmlDoc.DocumentNode.Descendants("script");
            string sessionId = "";
            foreach (var script in x)
            {
                if (script.OuterHtml.Contains("window.sessionId"))
                {
                    sessionId = script.OuterHtml.Split('"')[1];
                    break;//Session ID only occurs once in page
                }
            }

            return sessionId;
        }
        /// <summary>
        /// Gets the CSRF Token from the response headers
        /// </summary>
        /// <param name="response"></param>
        /// <returns>CSRF Token as string</returns>
        private static string GetSetCookieHeaderValue(HttpResponseMessage response)
        {
            var value = "";
            var headers = response.Headers.Concat(response.Content.Headers);
            foreach (var header in headers)
            {
                if (header.Key == "Set-Cookie")
                {
                    var setCookieToken = header.Value.ElementAt(0);
                    value = setCookieToken
                        .Split('=')[1] //Start of token
                        .Split(';')[0];//End of token
                }
            }

            return value;
        }
        /// <summary>
        /// Controls the sequence of requests to login and sets the required headers
        /// </summary>
        /// <param name="csrf"></param>
        /// <param name="state"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="loginUrl"></param>
        /// <param name="client"></param>
        /// <returns>Boolean result of login</returns>
        private static async Task<bool> SendLoginRequest(string csrf, string state, string username, string password, string loginUrl, HttpClient client)
        {
            var authenticationResponse = await AuthenticateAsync(csrf, state, username, password, loginUrl, client);

            var customerJsonString = await GetCustomerJsonAsync(authenticationResponse, client);
            if (client.DefaultRequestHeaders.Contains("Authorization"))
            {
                var grailsUserJObject = ProcessCustomerJson(customerJsonString);
                var base64CustomerJson = Base64Encode(grailsUserJObject.ToString());
                client.DefaultRequestHeaders.Add("grails-user", base64CustomerJson);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sends initial request to login endpoint and 
        /// </summary>
        /// <param name="csrf"></param>
        /// <param name="state"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="loginUrl"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private static async Task<string> AuthenticateAsync(string csrf, string state, string username, string password, string loginUrl, HttpClient client)
        {
            var loginEndpoint = "https://accounts.stockx.com/usernamepassword/login";

            JObject loginData = new JObject(
                new JProperty("client_id", "OVxrt4VJqTx7LIUKd661W0DuVMpcFByD"),
                new JProperty("redirect_uri", "https://stockx.com/callback?path=/"),
                new JProperty("tenant", "stockx-prod"),
                new JProperty("response_type", "code"),
                new JProperty("scope", "openid profile"),
                new JProperty("audience", "gateway.stockx.com"),
                new JProperty("_csrf", csrf),
                new JProperty("state", state),
                new JProperty("_intstate", "deprecated"),
                new JProperty("username", username),
                new JProperty("password", password),
                new JProperty("connection", "production"));
            var content = new StringContent(loginData.ToString(), Encoding.UTF8, "application/json");

            client.DefaultRequestHeaders.Add("Referer", loginUrl);

            var response = await client.PostAsync(loginEndpoint, content);
            var responseString = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return responseString;
            }

            throw new Exception($"ERROR!\n{responseString}");
        }
        /// <summary>
        /// Sends request to callback endpoint to retrieve customer information
        /// </summary>
        /// <param name="authResponse"></param>
        /// <param name="client"></param>
        /// <returns>Customer JSON string</returns>
        private static async Task<string>  GetCustomerJsonAsync(string authResponse, HttpClient client)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(authResponse);
            var x = htmlDoc.DocumentNode.Descendants("input");
            var wa = "";
            var wresult = "";
            var wctx = "";
            foreach (var input in x)
            {
                if (input.Attributes["name"] != null)
                {
                    if (input.Attributes["name"].Value == "wa")
                    {
                        wa = input.Attributes["value"].Value;
                    }
                    else if (input.Attributes["name"].Value == "wresult")
                    {
                        wresult = input.Attributes["value"].Value;
                    }
                    else if (input.Attributes["name"].Value == "wctx")
                    {
                        wctx = input.Attributes["value"].Value;
                    }
                }
            }

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("wa", wa),
                new KeyValuePair<string, string>("wresult", wresult),
                new KeyValuePair<string, string>("wctx", wctx.Replace("&#34;", "\""))
            });
            var callbackUrl = "https://accounts.stockx.com/login/callback";
            var response = await client.PostAsync(callbackUrl, formContent);
            var responseString = await response.Content.ReadAsStringAsync();

            htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(responseString);
            x = htmlDoc.DocumentNode.Descendants("script");
            var jsonString = "";
            foreach (var scripts in x)
            {
                if (scripts.InnerHtml.Contains("window.preLoadedBaseProps"))
                {
                    jsonString = scripts.InnerHtml.Split(new string[] {"customer\":"}, StringSplitOptions.None)[1];
                    jsonString = jsonString.Split(new string[] { ",\"locale\"" }, StringSplitOptions.None)[0];
                }
            }

            var bearerToken = GetSetCookieHeaderValue(response);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearerToken}");

            return jsonString;
        }
        /// <summary>
        /// Extracts and builds the grails-user JObject
        /// </summary>
        /// <param name="customerJsonString"></param>
        /// <returns></returns>
        private static JObject ProcessCustomerJson(string customerJsonString)
        {
            JObject customerJson = JObject.Parse(customerJsonString);
            var customerBilling = customerJson["Billing"];
            var customerShipping = customerJson["Shipping"];
            var uuid = customerJson["uuid"];
            var id = customerJson["id"];
            var hasBuyerReward = customerJson["hasBuyerReward"];

            JObject grailsUserJObject = new JObject(
                new JProperty("Customer",
                    new JObject(
                        new JProperty("Billing", customerBilling),
                        new JProperty("Shipping", customerShipping),
                        new JProperty("uuid", uuid),
                        new JProperty("id", id),
                        new JProperty("hasBuyerReward", hasBuyerReward))));

            return grailsUserJObject;
        }
        /// <summary>
        /// Helper method to log messages to console with the correct formatted date time
        /// </summary>
        /// <param name="message"></param>
        private void LogWithTime(string message)
        {
            Console.WriteLine($"[{DateTime.Now:hh:mm:ss.fff}] {message}");
        }
    }
}
