using System.Collections.Generic;

namespace AmazoomDebug
{
    /// <summary>
    /// Orders contain the orderId uniquely generated from Cloud Firestore, a list of Products Class containing all the products in that order,
    /// the user id that corresponds to the order, and the shipping status that toggles to true when all products in the order has been shipped
    /// </summary>
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