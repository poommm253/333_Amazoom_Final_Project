using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Text;
using System.Threading;

namespace AmazoomDebug
{
    class Trucks
    {
        
        private double truckVol = Warehouse.TruckCapacityVol;
        private double truckWeight = Warehouse.TruckCapacityWeight;
        private static double carryVol = 0;
        private static double carryWeight = 0;
        public bool IsAvailable { get; set; } = true;
        public string TruckId { get; set; }
        public List<Products> ItemInTruck { get; set; } = new List<Products>();

        public Trucks (string id)
        {
            TruckId = id;
        }

        public bool LoadProduct(Products toLoad)
        {
            if (IsAvailable)
            {
                if(carryVol + toLoad.Volume <= truckVol && carryVol + toLoad.Weight <= carryWeight)
                {
                    carryVol += toLoad.Volume;
                    carryWeight += toLoad.Weight;

                    return true;
                }
            }
            return false;
        }
        protected void NotifyArrival()
        {
            Random simulatedDeliveryTime = new Random();
            int resetTime = simulatedDeliveryTime.Next(5000, 10000);    // randomize a time between 5 sec and 10 sec for a full cycle travel

            Thread.Sleep(resetTime);    // Simulate a random travel time for each truck to leave and return to the warehouse
        }
    }

    class ShippingTruck : Trucks
    {
        public ShippingTruck(string id): base(id) { }
        public void Deploy()
        {
            while (true)
            {
                if (IsAvailable == true)
                {
                    Thread.Sleep(10000);    // sleep stand by time
                    IsAvailable = !IsAvailable;
                }
                else
                {
                    NotifyArrival();
                    IsAvailable = !IsAvailable;
                }
            }
        }
    }

    class InventoryTruck : Trucks
    {
        public InventoryTruck(string id) : base(id) { }

        public void Deploy()
        {
            while (true)
            {
            }
        }
    }
}
