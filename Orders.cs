using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;

namespace AmazoomDebug
{
    class Orders
    {
        public string OrderId { get; set; }
        private List<Products> ordered { get; set; } = new List<Products>();
        public string UserId { get; set; }
        public bool IsShipped { get; set; }

        public Orders(string id, List<Products> orders, string userId, bool status)
        {
            OrderId = id;
            ordered = orders;
            UserId = userId;
            IsShipped = status;
        }

        public double GetTotalWeight()
        {
            double weight = 0;

            foreach (var product in this.ordered)
            {
                weight += product.Weight;
            }

            return weight;
        }

        public double GetTotalVolume()
        {
            double volume = 0;

            foreach (var product in this.ordered)
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
