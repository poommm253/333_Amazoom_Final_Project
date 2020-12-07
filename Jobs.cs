namespace AmazoomDebug
{
    /// <summary>
    /// Jobs containing information about restocking position, retreving position, orderId, and product information
    /// A job is assigned to individual robots by the warehouse central computer
    /// </summary>
    class Jobs
    {
        public Products ProdId { get; set; }
        public string OrderId { get; set; }
        public bool Restock { get; set; }
        public bool Retrieve { get; set; }
        public Coordinate RestockCoord { get; set; }
        public Coordinate RetrieveCoord { get; set; }

        public Jobs()
        {
        }

        public Jobs (Products prodId, string orderId, bool restock, bool retrieve, Coordinate retrieveCoord, Coordinate restockCoord)
        {
            ProdId = prodId;
            OrderId = orderId;
            Restock = restock;
            Retrieve = retrieve;
            RestockCoord = restockCoord;
            RetrieveCoord = retrieveCoord;
        }
    }
}