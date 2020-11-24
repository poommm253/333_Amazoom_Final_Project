using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;

namespace AmazoomDebug
{
    // each move takes 1 sec
    class Robot
    {
        private static SemaphoreSlim avoidCollision = new SemaphoreSlim(1);

        private double carryingCapacity = Warehouse.RobotCapacity;    // max carrying weight of 5kg; limited only by weight and not volume
        public Battery Battery { get; set; }
        public string RobotId { get; set; }
        public Coordinate Sector { get; set; }
        public List<Jobs> JobQueue { get; set; } = new List<Jobs>();
        public List<Jobs> CarryingItem { get; set; } = new List<Jobs>();

        public Robot(string id, Battery battery, Coordinate sector)
        {
            RobotId = id;
            Battery = battery;
            Sector = sector;
            
        }

        public void AddJob(Jobs add)
        {
            JobQueue.Add(add);
        }

        public void Deploy()
        {
            while (true)
            {
                if (JobQueue.Count != 0)
                {
                    // TODO: need to further check if the job is a retrieve or a restock
                    Retrieve();


                    // TODO: Update Location onto Firebase
                }
                else
                {
                    Thread.Sleep(2000);    // wait 2 seconds to recheck for jobs
                    Console.WriteLine("Waiting for more jobs");
                    
                }
            }
        }

        /// <summary>
        /// Retrieve order from specific location within the robot's column. 
        /// Retrieve closest order first, if carrying capacity is full, then load the shipping trucks.
        /// Always check distance between next destination and the loading docks, always choose the shortest path if the robot is carrying a product
        /// </summary>
        /// <param name="orderDetail"> List of orders to be retrieved by the robot. Only contain orders that is within the same column the robot is located </param>
        public void Retrieve()
        {
            // Item1: closestPath; Item2: corresponding Job
            var path = ShortestPathCalc(JobQueue);
            Jobs currentJob = path.Item2;

            // check distance from current location to the loading doc if the robot is carrying something
            // else go pick up next item
            int toLoadingDock = Math.Abs(Sector.Row - Warehouse.LoadingDockRow);

            // if closestPath is to restock from the loading dock, then restock
            if (path.Item2.Restock)
            {
                Restock(path);
            }
            // if closestPath is to retrive
            else
            {
                if (carryingCapacity < Warehouse.RobotCapacity && toLoadingDock < path.Item1)
                {
                    // TODO: Maybe picking up items based on same orderId first
                    AvoidCollisionLoading();
                }
                else if (carryingCapacity - currentJob.ProdId.Weight >= 0)
                {
                    Movement(path.Item2.RetrieveCoord.Row);
                    carryingCapacity -= path.Item2.ProdId.Weight;

                    CarryingItem.Add(path.Item2);
                    JobQueue.Remove(path.Item2);    // remove item that is retrieved
                    Console.WriteLine("Product Retrieved");
                    Console.WriteLine("Carrying Cap: " + carryingCapacity);
                }
                else
                {
                    AvoidCollisionLoading();
                }

                if (JobQueue.Count == 0 && CarryingItem.Count != 0)    // head straight to shipping if it is the last job
                {
                    AvoidCollisionLoading();
                }
                /*else    // return to charging dock for idle
                {
                    Movement(0);
                    Battery.Charge();
                }*/
            }
            
        }

        /// <summary>
        /// Only releases one robot into the "loading dock" row at a time to avoid collision
        /// </summary>
        private void AvoidCollisionLoading()
        {
            Movement(Warehouse.Rows);    // travel to the last row and wait for semaphore to allow a robot to enter one at a time

            Console.WriteLine("Waiting for gate");
            avoidCollision.Wait();
            LoadShipment();
            avoidCollision.Release();
            Console.WriteLine("Release");
        }

        public void Restock((int,Jobs) restockInfo)
        {
            // TODO: Random Distribution
        }

        public void LoadShipment()
        {
            Movement(Warehouse.LoadingDockRow);
            carryingCapacity = Warehouse.RobotCapacity;    // load everything to the shipping truck
            
            foreach(var goingToLoad in CarryingItem)
            {
                Warehouse.AddToTruck(goingToLoad);
            }
            foreach(var element in Warehouse.LoadedToTruck)
            {
                Console.WriteLine(element.ProdId.ProductName);
            }

            CarryingItem.Clear();

            Console.WriteLine("empty: " + (CarryingItem.Count == 0));

            Movement(Warehouse.Rows);
        }

        /// <summary>
        /// Move robot to specified location, drain battery by 10% every 6 unit movement == 6 seconds
        /// If robot has battery of 10%, enter power saving mode and move back to the charging dock at row = 0 and charge
        /// </summary>
        /// <param name="productLocation"> Where the robot will move to (rows) </param>
        public void Movement(int productLocation)
        {
            int totalUnitMovement = 0;

            while (Sector.Row != productLocation)
            {
                // now that I got the closest path, I move the robot from its current position to productLocation
                if (Sector.Row <= productLocation)    // move down the row
                {
                    totalUnitMovement++;
                    Sector.Row++;
                    Console.WriteLine("Position: " + Sector.Row);
                    Thread.Sleep(Warehouse.TravelTime);    // travel time

                }
                else                                  // move up the row
                {
                    totalUnitMovement++;
                    Sector.Row--;
                    Console.WriteLine("Position: " + Sector.Row);
                    Thread.Sleep(Warehouse.TravelTime);    // travel time
                }

                if (totalUnitMovement % 6 == 0)    // 6 == battery life
                {
                    if (!Battery.Usage())    // check if battery is 10% then return to charging dock
                    {
                        Movement(0);    // move back to origin with power saving mode
                        Battery.Charge();
                    }
                }
            }
        }

        /// <summary>
        /// Find the shortest path based on the robot's current coordinates to its next destination
        /// </summary>
        /// <param name="allJobs"> List of products that the robot need to retrieve </param>
        /// <returns> Returns a Tuple (int closestPath from current position to next destination and the corresponding Job at the destination) </returns>
        public (int, Jobs) ShortestPathCalc(List<Jobs> allJobs)
        {
            int closestPath = Warehouse.LoadingDockRow - 1;
            int index = -1;
            Jobs retreval = new Jobs();

            foreach (var toRetrieve in allJobs)
            {
                int destination = Math.Abs(toRetrieve.RetrieveCoord.Row - Sector.Row);

                if (destination <= closestPath)
                {
                    closestPath = destination;    // find the closest destination
                    retreval = toRetrieve;
                }
                index++;
            }
            return (closestPath, retreval);
        }
    }
}