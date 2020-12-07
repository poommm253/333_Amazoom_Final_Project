using System;
using System.Collections.Concurrent;
using System.Threading;

namespace AmazoomDebug
{
    class Trucks
    {
        private double truckVol = Warehouse.TruckCapacityVol;
        private double truckWeight = Warehouse.TruckCapacityWeight;
        public static double carryVol = 0;
        public static double carryWeight = 0;
        public bool IsAvailable { get; set; } = true;
        public string TruckId { get; set; }

        public Trucks(string id)
        {
            TruckId = id;
        }

        /// <summary>
        /// Check if it is possible to load more products to the truck according to the truck's max weight capacity and max volume capacity
        /// </summary>
        /// <param name="toLoad"> Product to be checked and loaded to the truck </param>
        /// <returns> Return true if the porduct can successfully be loaded. Otherwise return false </returns>
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

        /// <summary>
        /// Stimulates a random interval that signals the return of the truck
        /// The truck will be gone between 3 seconds to 7 seconds
        /// </summary>
        protected void NotifyArrival()
        {
            Random simulatedDeliveryTime = new Random();
            int resetTime = simulatedDeliveryTime.Next(3000, 7000);    // randomize a time between 5 sec and 7 sec for a full cycle travel

            Thread.Sleep(resetTime);    // Simulate a random travel time for each truck to leave and return to the warehouse
        }
    }

    /// <summary>
    /// Shipping Truck class inherited from the Truck class
    /// Ships all the products and orders that are loaded to the shipping truck
    /// </summary>
    class ShippingTruck : Trucks
    {
        public ShippingTruck(string id) : base(id) { }

        /// <summary>
        /// Load items to truck (if any) once it is allowed to dock. Leave the once the truck is full and relases the dock for the next truck to dock.
        /// </summary>
        public void Deploy()
        {
            while (true)
            {
                Warehouse.dockLocking.Wait();
                Console.WriteLine("IsAvailable: " + IsAvailable + " TruckID: " + TruckId);

                if (IsAvailable)
                {
                    // Testing to see if the correct truck enters the docking area
                    Console.WriteLine("{0} waiting.................................", TruckId);

                    int loadCount = Warehouse.LoadedToTruck.Count;

                    for (int i = 0; i < loadCount; i++)
                    {
                        Warehouse.LoadedToTruck.TryDequeue(out Jobs current);
                        if (LoadProduct(current.ProdId) == false)
                        {
                            carryVol = 0;
                            carryWeight = 0;

                            Console.WriteLine("Alert!!!! SHIPPING TRUCK IS FULL!!!!!!!!!!!!!!!!");

                            IsAvailable = false;
                            break;
                        }
                    }

                    // Stimulate the time it needs for the truck driver to confirm and grant permission to commence the delivery
                    if (IsAvailable == true)
                    {
                        Thread.Sleep(5000);
                        IsAvailable = false;
                    }

                    // Release the docking area to other trucks that are waiting
                    Warehouse.waitDocking.Release();
                    Console.WriteLine("Truck relasing dock");

                    Console.WriteLine("{0} leaving...................................", TruckId);

                    NotifyArrival();    // Random return to the waiting area and notify the central computer that the truck is available for use.
                    IsAvailable = true;
                }
            }
        }
    }

    /// <summary>
    /// Inventory Truck class inherited from the Truck class
    /// Docks at the docking area with the products to be restocked by the robots
    /// </summary>
    class InventoryTruck : Trucks
    {
        public ConcurrentQueue<Products> ItemInTruck { get; set; } = new ConcurrentQueue<Products>();
        public InventoryTruck(string id) : base(id) { }
        public bool IsReady { get; set; } = false;

        /// <summary>
        /// Inventory truck starts waiting and idle
        /// Once restocking intructions are received, it proceeds to enter the docking area and unload items in the truck
        /// It releases the docking area once it is done
        /// </summary>
        public void Deploy()
        {
            while (true)
            {
                IsReady = false;
                carryVol = 0;
                carryWeight = 0;

                if (Warehouse.RestockItem.Count != 0)
                {
                    
                    int LoadRestockToTruck = Warehouse.RestockItem.Count;

                    for (int i = 0; i < LoadRestockToTruck; i++)
                    {
                        Warehouse.RestockItem.TryDequeue(out Products currentProduct);
                        if (LoadProduct(currentProduct) == false)
                        {
                            Console.WriteLine("Alert!!!! INVENTORY TRUCK IS FULL!!!!!!!!!!!!!!!!");
                            break;
                        }
                        else
                        {
                            ItemInTruck.Enqueue(currentProduct);
                        }
                    }

                    IsReady = true;
                    Warehouse.dockLocking.Wait();
                    Console.WriteLine("IsAvailable: " + IsAvailable + " TruckID: " + TruckId);

                    Warehouse.createRestockingJob.Wait();

                    Thread.Sleep(5000);
                    Console.WriteLine("Unloaded and inventory truck is leaving");
                    ItemInTruck.Clear();

                    Warehouse.waitDocking.Release();
                }
                else
                {
                    Thread.Sleep(5000);
                }
            }
        }
    }
}
