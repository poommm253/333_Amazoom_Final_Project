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

        public Trucks(string id)
        {
            TruckId = id;
        }

        public bool LoadProduct(Products toLoad)
        {
            if (IsAvailable)
            {
                if (carryVol + toLoad.Volume <= truckVol && carryWeight + toLoad.Weight <= truckWeight)
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
            int resetTime = simulatedDeliveryTime.Next(3000, 5000);    // randomize a time between 5 sec and 10 sec for a full cycle travel

            Thread.Sleep(resetTime);    // Simulate a random travel time for each truck to leave and return to the warehouse
        }
    }

    class ShippingTruck : Trucks
    {
        public ShippingTruck(string id) : base(id) { }

        public void Deploy()
        {
            while (true)
            {
                Warehouse.dockLocking.Wait();
                Console.WriteLine("IsAvailable: " + IsAvailable + " TruckID: " + TruckId);

                if (IsAvailable)
                {
                    Console.WriteLine("{0} waiting.................................", TruckId);

                    int loadCount = Warehouse.LoadedToTruck.Count;

                    for (int i = 0; i < loadCount; i++)
                    {
                        Warehouse.LoadedToTruck.TryDequeue(out Jobs current);
                        if (LoadProduct(current.ProdId) == false)
                        {
                            Console.WriteLine("Alert!!!! TRUCK IS FULL!!!!!!!!!!!!!!!!");
                            IsAvailable = false;
                            break;
                        }
                    }

                    if (IsAvailable == true)
                    {
                        Thread.Sleep(5000);
                        IsAvailable = false;
                    }

                    Warehouse.waitDocking.Release();
                    Console.WriteLine("Truck relasing dock");

                    Console.WriteLine("{0} leaving...................................", TruckId);
                    NotifyArrival();
                    IsAvailable = true;
                }
            }
        }
    }

    class InventoryTruck : Trucks
    {
        public ConcurrentQueue<Products> ItemInTruck { get; set; } = new ConcurrentQueue<Products>();
        public InventoryTruck(string id) : base(id) { }
        public bool IsReady { get; set; } = false;

        private double carryVol = 0;
        private double carryWeight = 0;

        public void RestockItem(Products restock)
        {
            ItemInTruck.Enqueue(restock);
        }

        public void Deploy()
        {
            while (true)
            {
                IsReady = false;

                if(Warehouse.RestockItem.Count != 0)
                {
                    // Check truck capacity for restocking
                    if (IsAvailable)
                    {
                        int LoadRestockToTruck = Warehouse.RestockItem.Count;

                        for (int i = 0; i < LoadRestockToTruck; i++)
                        {
                            Warehouse.RestockItem.TryDequeue(out Products currentProduct);
                            if (LoadProduct(currentProduct) == false)
                            {
                                Console.WriteLine("Alert!!!! TRUCK IS FULL!!!!!!!!!!!!!!!!");
                                IsAvailable = false;
                                break;
                            }
                            else
                            {
                                ItemInTruck.Enqueue(currentProduct);
                            }
                        }

                        IsReady = true;
                        Warehouse.dockLocking.Wait();

                        Warehouse.createRestockingJob.Wait();

                        if (ItemInTruck.Count == 0)
                        {
                            Warehouse.waitDocking.Release();
                        }
                    }
                }
                else
                {
                    Thread.Sleep(10000);
                }
            }
        }
    }
}
