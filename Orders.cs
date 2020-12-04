using System;
using System.Collections.Generic;

namespace AmazoomDebug
{
    class Orders
    {
        public string OrderId { get; set; }
        public List<Products> Ordered { get; set; } = new List<Products>();
        public string UserId { get; set; }
        public bool IsShipped { get; set; }

        public Orders(string id, List<Products> orders, string userId, bool status)
        {
            OrderId = id;
            Ordered = orders;
            UserId = userId;
            IsShipped = status;
        }
    }
}
