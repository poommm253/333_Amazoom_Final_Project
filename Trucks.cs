using System;
using System.Collections.Concurrent;
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

        public Trucks (string id)
        {
            TruckId = id;
        }

        public bool LoadProduct(Products toLoad)
        {
            if (IsAvailable)
            {
                if(carryVol + toLoad.Volume <= truckVol && carryWeight + toLoad.Weight <= truckWeight)
                {
                    carryVol += toLoad.Volume;
                    carryWeight += toLoad.Weight;
                    Console.WriteLine("{0} is loaded to {1}", toLoad.ProductName, TruckId);
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
        public ConcurrentQueue<Products> itemsLoaded = new ConcurrentQueue<Products>();
        public ShippingTruck(string id): base(id) { }
        
        public void Deploy()
        {
            while (true)
            {
                Warehouse.dockLocking.Wait();

                if (IsAvailable)
                {
                    Console.WriteLine("{0} waiting...", TruckId);

                    int loadCount = Warehouse.LoadedToTruck.Count;

                    for (int i = 0; i < loadCount; i++)
                    {
                        Warehouse.LoadedToTruck.TryDequeue(out Jobs current);
                        if (LoadProduct(current.ProdId) == false)
                        {
                            break;
                        }
                    }

                    Thread.Sleep(5000);
                    IsAvailable = false;

                    Warehouse.waitDocking.Release();

                    Console.WriteLine("{0} leaving...", TruckId);
                    NotifyArrival();
                    IsAvailable = true;

                }

            }
        }
    }

    class InventoryTruck : Trucks
    {
        public List<Products> ItemInTruck { get; set; } = new List<Products>();

        public InventoryTruck(string id) : base(id) { }

        public void Deploy()
        {
            while (true)
            {
                Warehouse.dockLocking.Wait();
                if (IsAvailable)
                {
                    while(ItemInTruck.Count != 0)
                    {
                        continue;
                    }
                    IsAvailable = false;
                }
                else
                {
                    NotifyArrival();
                    IsAvailable = true;
                }
                Warehouse.dockLocking.Release();
            }
        }
    }
}
