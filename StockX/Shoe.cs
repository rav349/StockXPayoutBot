namespace StockX
{
    public class Shoe
    {
        public Shoe(string size, string sizeID, string highestBid, string lowestAsk, string payout, string name)
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
}