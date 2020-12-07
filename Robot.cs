using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace AmazoomDebug
{
    /// <summary>
    /// Robot Class containing specific robot information such as its battery level, sector, a List of current jobs, a list of items currently carrying,
    /// and the robot's capacity.
    /// 
    /// This class automatically updates the robot's location to Cloud Firestore
    /// 
    /// each moves takes 0.5 seconds for the purpose of the simulation; can be changed in the Warehouse setup file
    /// </summary>
    class Robot
    {
        private static SemaphoreSlim avoidCollision = new SemaphoreSlim(1);
        private double carryingCapacity = Warehouse.RobotCapacity;    // max carrying weight of 5kg; limited only by weight and not volume
        private int chargingDockLocation = 0;
        public Battery Battery { get; set; }
        public string RobotId { get; set; }
        public Coordinate Sector { get; set; }
        public List<Jobs> JobList { get; set; } = new List<Jobs>();
        public List<Jobs> CarryingItem { get; set; } = new List<Jobs>();
        private FirestoreDb database;

        public Robot(string id, Battery battery, Coordinate sector)
        {
            RobotId = id;
            Battery = battery;
            Sector = sector;

            // Established a connection to Cloud Firestore with a unique application id
            string path = AppDomain.CurrentDomain.BaseDirectory + @"amazoom-c1397-firebase-adminsdk-ho7z7-6572726fc6.json";
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);
            database = FirestoreDb.Create("amazoom-c1397");
        }

        /// <summary>
        /// Adding jobs to the current list of jobs the robot has to finish
        /// </summary>
        /// <param name="add">Job class object to be added to the list</param>
        public void AddJob(Jobs add)
        {
            Console.WriteLine("Robot: " + Sector.Column + " received new job.");
            JobList.Add(add);
        }

        public void Deploy()
        {
            while (true)
            {
                if (JobList.Count != 0)
                {
                    RetrieveAndRestock();
                }
                else
                {
                    Movement(chargingDockLocation);
                    Battery.Charge();

                    Thread.Sleep(5000);    // robots become idle to release resources from multi threading
                }
            }
        }

        /// <summary>
        /// Retrieve and restock jobs from specific location within the robot's column. 
        /// Retrieve closest order first, if carrying capacity is full, then load the shipping trucks.
        /// Always check distance between next destination and the loading docks, always choose the shortest path if the robot is carrying a product
        /// </summary>
        private void RetrieveAndRestock()
        {
            // Item1: closestPath; Item2: corresponding Job
            var path = ShortestPathCalc(JobList);
            Jobs currentJob = path.Item2;

            // check distance from current location to the loading doc if the robot is carrying something
            // else go pick up next item
            int toLoadingDock = Math.Abs(Sector.Row - Warehouse.LoadingDockRow);

            // if closestPath is to restock from the loading dock, then restock
            if (path.Item2.Restock)
            {
                Restock(currentJob).Wait();
            }
            // if closestPath is to retrive
            else
            {
                // Check if the closest path is to the loading dock or not and if the robot is carrying an item
                // If so, load the current item to the truck first
                if (carryingCapacity < Warehouse.RobotCapacity && toLoadingDock < path.Item1)
                {
                    AvoidCollisionLoading();
                }

                // If the robot can still carry more item, then it will continue to the next shortest path location to retrieve more item
                else if (carryingCapacity - currentJob.ProdId.Weight >= 0)
                {
                    Movement(path.Item2.RetrieveCoord.Row);
                    carryingCapacity -= path.Item2.ProdId.Weight;    // update carrying capacity

                    CarryingItem.Add(path.Item2);
                    JobList.Remove(path.Item2);    // remove item that is retrieved
                    Console.WriteLine("Product Retrieved: " + path.Item2.ProdId.ProductName + "  " + Sector.Column + "  at  " + path.Item2.RetrieveCoord.Row + path.Item2.RetrieveCoord.Column + path.Item2.RetrieveCoord.Shelf);
                    Console.WriteLine("Carrying Cap: " + carryingCapacity);
                }

                // If the robot reached its max carrying capacity, then it is forced to load items to the shipping truck
                else
                {
                    AvoidCollisionLoading();
                }

                // head straight to shipping if it is the last job and load the item on to the truck
                if (JobList.Count == 0 && CarryingItem.Count != 0)
                {
                    AvoidCollisionLoading();
                }
            }
        }

        /// <summary>
        /// Only releases one robot into the "loading dock" row at a time to avoid collision
        /// </summary>
        private void AvoidCollisionLoading()
        {
            Movement(Warehouse.Rows);    // travel to the last row and wait for semaphore to allow a robot to enter one at a time

            Console.WriteLine("Waiting for gate");

            // Critical section and should be thread safe
            avoidCollision.Wait();
            LoadShipment();
            avoidCollision.Release();
            // Release of critical section

            Console.WriteLine("Release");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="restockInfo"></param>
        /// <returns></returns>
        private async Task Restock(Jobs restockInfo)
        {
            // Move to inventory truck
            Movement(Warehouse.Rows);
            avoidCollision.Wait();
            Movement(Warehouse.LoadingDockRow);
            avoidCollision.Release();

            // Move to destination to restock
            Movement(restockInfo.RestockCoord.Row);

            Console.WriteLine("Product Restocked: " + restockInfo.ProdId.ProductName + " " + Sector.Column + " at " + restockInfo.RestockCoord.Row + restockInfo.RestockCoord.Column + restockInfo.RestockCoord.Shelf);
            Console.WriteLine(RobotId + " restocked complete.");


            foreach(var allProd in Warehouse.AllProducts)
            {
                if (allProd.ProductID == restockInfo.ProdId.ProductID)
                {
                    allProd.Location.Add(restockInfo.RestockCoord);

                    // Update Cloud Firestore that a new product has been restocked
                    DocumentReference restock = database.Collection("All products").Document(restockInfo.ProdId.ProductID);
                    Dictionary<string, Object> lowStockUpdate = new Dictionary<string, object>();

                    // increment inStock by +1 in Cloud Firestore
                    //lowStockUpdate.Add("inStock", FieldValue.Increment(1));
                    lowStockUpdate.Add("coordinate", allProd.CoordToArray());
                    await restock.UpdateAsync(lowStockUpdate);

                    break;
                }
            }


            JobList.Remove(restockInfo);
        }

        /// <summary>
        /// Load the items that the robot are currently carrying and resetting the carrying capacity to max
        /// </summary>
        private void LoadShipment()
        {
            Movement(Warehouse.LoadingDockRow);
            carryingCapacity = Warehouse.RobotCapacity;    // resetting the carrying capacity to max

            // Add everything to the truck
            foreach (var goingToLoad in CarryingItem)
            {
                Warehouse.AddToTruck(goingToLoad);
            }

            // Testing; used to check which products got loaded to truck
            foreach (var element in Warehouse.LoadedToTruck)
            {
                Console.WriteLine(element.ProdId.ProductName);
            }

            CarryingItem.Clear();    // empty products to the truck

            Console.WriteLine("empty: " + (CarryingItem.Count == 0));

            Movement(Warehouse.Rows);    // move out of the docking zone
        }

        /// <summary>
        /// Move robot to specified location, drain battery by 10% every 6 unit movement == 3 seconds
        /// If robot has battery of 10%, enter power saving mode and move back to the charging dock at charging origin (row = 0) and charge
        /// </summary>
        /// <param name="productLocation"> Where the robot will move to (rows) </param>
        private void Movement(int productLocation)
        {
            int totalUnitMovement = 0;

            while (Sector.Row != productLocation)
            {
                if (Sector.Row <= productLocation)    // Move down the row
                {
                    totalUnitMovement++;
                    Sector.Row++;
                    Console.WriteLine("Position: " + Sector.Row + " , Robot id: " + Sector.Column);
                    UpdatePositionDB().Wait();

                    Thread.Sleep(Warehouse.TravelTime);    // Simulated travel time
                }
                else                                  // Move up the row
                {
                    totalUnitMovement++;
                    Sector.Row--;
                    Console.WriteLine("Position: " + Sector.Row + " , Robot id: " + Sector.Column);
                    UpdatePositionDB().Wait();

                    Thread.Sleep(Warehouse.TravelTime);    // Simulated travel time
                }

                if (totalUnitMovement % 3 == 0)    // 3 movements == battery life -10%
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
        /// Update robot's current position real time to the Cloud Firestore
        /// </summary>
        /// <returns> It is an asynchronous task that must be waited once called </returns>
        private async Task UpdatePositionDB()
        {
            DocumentReference updatePos = database.Collection("All robot").Document(RobotId);
            Dictionary<string, Object> update = new Dictionary<string, Object>();

            string currentPos = Sector.Row + " " + Sector.Column + " " + Sector.Shelf;

            update.Add("coordinate", currentPos);
            update.Add("battery", Battery.BatteryLevel.ToString());

            await updatePos.UpdateAsync(update);    // Sending coordinate updates to Cloud Firestore
        }

        /// <summary>
        /// Find the shortest path based on the robot's current coordinates to its next destination
        /// Always priotitize restocking jobs over retrieving jobs.
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
                // Robot prioritizes restocking over retrieving
                if (toRetrieve.Restock)
                {
                    return (-1, toRetrieve);
                }
                else
                {
                    int destination = Math.Abs(toRetrieve.RetrieveCoord.Row - Sector.Row);

                    if (destination <= closestPath)
                    {
                        closestPath = destination;    // find the closest destination
                        retreval = toRetrieve;
                    }
                    index++;
                }
            }
            return (closestPath, retreval);
        }
    }
}