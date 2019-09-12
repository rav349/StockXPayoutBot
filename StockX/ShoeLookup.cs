namespace StockX
{
    public class ShoeLookup
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
}