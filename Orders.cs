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

        public Orders(string id, string userId, bool status)
        {
            OrderId = id;
            UserId = userId;
            IsShipped = status;
        }

        public double GetTotalWeight()
        {
            double weight = 0;

            foreach (var product in this.Ordered)
            {
                weight += product.Weight;
            }

            return weight;
        }

        public double GetTotalVolume()
        {
            double volume = 0;

            foreach (var product in this.Ordered)
            {
                volume += product.Volume;
            }

            return volume;
        }

        public void FetchOrders()
        {
            // connect to firebase and fetch order
            // ordered = fetchedOrder from firebase
        }
    }
}
