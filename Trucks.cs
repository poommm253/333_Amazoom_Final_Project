using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Text;
using System.Threading;

namespace AmazoomDebug
{
    class Trucks
    {
        private int truckVol = Warehouse.TruckCapacityVol;
        private int truckWeight = Warehouse.TruckCapacityWeight;
        public bool IsAvailable { get; set; } = true;
        public string TruckId { get; set; }
        public List<Products> ItemInTruck { get; set; } = new List<Products>();

        public void Deploy()
        {
            while (true)
            {
                if (!IsAvailable)
                {
                    IsAvailable = NotifyArrival();
                }
            }
        }
        private bool NotifyArrival()
        {
            Random simulatedDeliveryTime = new Random();
            int resetTime = simulatedDeliveryTime.Next(5000, 10000);    // randomize a time between 5 sec and 10 sec for a full cycle travel

            Thread.Sleep(resetTime);    // Simulate a random travel time for each truck to leave and return to the warehouse
            return true;
        }
    }

    class ShippingTruck : Trucks
    {
        public void Deploy()
        {
            while (true)
            {
                if (Warehouse.LoadedToTruck.Count != 0)
                {
                    double totalWeight = 0;
                    double totalVolume = 0;

                    foreach(var itemsLoaded in Warehouse.LoadedToTruck)
                    {
                        
                    }
                }
            }
        }
    }

    class InventoryTruck : Trucks
    {

    }
}
