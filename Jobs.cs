using System;
using System.Collections.Generic;
using System.Text;

namespace AmazoomDebug
{
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

        public Jobs (Products prodId, string orderId, bool restock, bool retrieve)
        {
            ProdId = prodId;
            OrderId = orderId;
            Restock = restock;
            Retrieve = retrieve;
        }
    }
}