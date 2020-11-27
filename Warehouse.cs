using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Newtonsoft.Json.Schema;
using System.Threading;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Diagnostics;

namespace AmazoomDebug
{
    /// <summary>
    /// contains all the global constant for the entire system
    /// </summary>
    class Warehouse
    {
        public static Mutex addingOrder = new Mutex();
        public static SemaphoreSlim dockLocking = new SemaphoreSlim(0);
        public static SemaphoreSlim waitDocking = new SemaphoreSlim(0);

        public static int LoadingDockRow { get; set; }
        public static int Rows { get; set; }
        public static int Columns { get; set; }
        public static int TravelTime { get; set; }
        public static int RobotCapacity { get; set; } 
        public static double TruckCapacityVol { get; set; }
        public static double TruckCapacityWeight { get; set; }
        public static int Shelves { get; set; }
        public static ConcurrentQueue<Jobs> LoadedToTruck { get; set; } = new ConcurrentQueue<Jobs>();
        public static List<Products> AllProducts { get; set; } = new List<Products>();
        public static List<Jobs> AllJobs { get; set; } = new List<Jobs>();
        public static List<Orders> LocalOrders { get; set; } = new List<Orders>();

        private static List<Coordinate> isEmpty = new List<Coordinate>();
        private static List<Coordinate> isOccupied = new List<Coordinate>();
        private static List<Coordinate> accessibleLocations = new List<Coordinate>();

        private static List<Robot> operationalRobots = new List<Robot>();
        private static List<ShippingTruck> operationalShippingTrucks = new List<ShippingTruck>();

        private static Dictionary<string, int> partialOrders = new Dictionary<string, int>();



        private static Stopwatch dockTimer = new Stopwatch();
        private static List<Products> waitForShip = new List<Products>();
        private static List<bool> shippingTrucks = new List<bool>() { true, true };
        private static double carryVol;
        private static double carryWeight;

        /// <summary>
        /// reads setup file and initializes the warehouse with all the primary global constants
        /// </summary>
        public Warehouse()
        {
            // read file to initialize warehouse
            System.IO.StreamReader setup = new System.IO.StreamReader("InitializationSetup/Setup_Warehouse.txt");
            string line = setup.ReadLine();

            // #rows, #columns, #shelves, robotCapacity, travelTime
            string[] keys = line.Split(' ');

            try
            {
                Rows = Int32.Parse(keys[0]);                    //5
                Columns = Int32.Parse(keys[1]);                 //8
                Shelves = Int32.Parse(keys[2]);                 //6
                RobotCapacity = Int32.Parse(keys[3]);           //30
                TravelTime = Int32.Parse(keys[4]);              //500
                LoadingDockRow = Rows + 1;
                TruckCapacityVol = Int32.Parse(keys[5]);        //10
                TruckCapacityWeight = Int32.Parse(keys[6]);     //200

                carryVol = TruckCapacityVol;
                carryWeight = TruckCapacityWeight;

                setup.Close();
            }
            catch
            {
                setup.Close();
                Console.WriteLine("File not loaded properly and warehouse cannot be instantiated");
            }
        }

        public void Deploy()
        {
            // Initialization of Automated Warehouse
            GenerateLayout();

            string path = AppDomain.CurrentDomain.BaseDirectory + @"amazoom-c1397-firebase-adminsdk-ho7z7-6572726fc6.json";
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);
            FirestoreDb database = FirestoreDb.Create("amazoom-c1397");

            InstantiateRobots(database).Wait();
            InstantiateTrucks(4);
            FetchData(database).Wait();

            // Deploying robots
            Task[] robots = new Task[operationalRobots.Count];

            int index = 0;
            foreach(var opRobot in operationalRobots)
            {
                robots[index] = Task.Run(() => opRobot.Deploy());
                index++;
            }

            // Deploying shipping and invertory trucks
            Task[] Trucks = new Task[operationalShippingTrucks.Count];
            //Task[] inventoryTruck = new Task[operationalShippingTrucks.Count];

            //int tIndex = 0;
            int sIndex = 0;
            foreach (var opShipTruck in operationalShippingTrucks)
            {
                Trucks[sIndex] = Task.Run(() => opShipTruck.Deploy());
                sIndex++;
            }
           /* foreach (var opInvTruck in operationalInventoryTrucks)
            {
                inventoryTruck[tIndex] = Task.Run(() => opInvTruck.Deploy());
                tIndex++;
            }*/

            // Check for incoming order in the background and assign jobs to the robots all in the background and adding tasks to the robot
            Task orderCheck = Task.Run(() => OrderListener(database));

            // Check for truck loading
            Task shippingCheck = Task.Run(() => ShippingVerificationV2(database));
            Task restockingCheck = Task.Run(() => LoadingToTruck());
            //and unloading from the shipping and inventory trucks


            // Wait all
            Task.WaitAll(robots);
            //Task.WaitAll(shippingTrucks);
            //Task.WaitAll(inventoryTruck);
            orderCheck.Wait();
            shippingCheck.Wait();
        }

        private void GenerateLayout()
        {
            // Instantiate all Coordinate Location inside the warehouse
            for(int row = 1; row <= Rows; row++)
            {
                for(int col = 1; col <= Columns; col++)
                {
                    for(int shelf = 1; shelf <= Shelves; shelf++)
                    {
                        Coordinate generateLayout = new Coordinate(row, col, shelf);
                        accessibleLocations.Add(generateLayout);
                    }
                }
            }
        }

        private void InstantiateTrucks(int quantity)
        {
            for(int i = 0; i < quantity; i++)
            {
                operationalShippingTrucks.Add(new ShippingTruck("truck_" + i));
            }
        }
        private async Task InstantiateRobots(FirestoreDb database)
        {
            // Check firestore for previously initialized robots and Instantiate robots based on previously saved locations
            CollectionReference allRobot = database.Collection("All robot");
            QuerySnapshot fetchedRobot = await allRobot.GetSnapshotAsync();

            if (fetchedRobot.Count != 0)
            {
                foreach(DocumentSnapshot robotInfo in fetchedRobot.Documents)
                {
                    Dictionary<string, Object> robotDetail = robotInfo.ToDictionary();

                    string[] fetchedCoordinate = robotDetail["coordinate"].ToString().Split(" ");

                    operationalRobots.Add(new Robot(
                        robotInfo.Id,
                        new Battery(Convert.ToInt32(robotDetail["battery"])),
                        new Coordinate(Convert.ToInt32(fetchedCoordinate[0]), Convert.ToInt32(fetchedCoordinate[1]), Convert.ToInt32(fetchedCoordinate[2]))
                        ));
                }
                Console.WriteLine("Robots info fetched sucessfully.");
            }
            else
            {
                // Instantiating new robots
                for (int i = 1; i <= Columns; i++)
                {
                    operationalRobots.Add(new Robot("AMAZOOM_AW_" + i.ToString(), new Battery(100), new Coordinate(0, i)));
                }

                int docId = 1;
                foreach(var robots in operationalRobots)
                {
                    DocumentReference addRobot = database.Collection("All robot").Document("AMAZOOM_AW_" + docId.ToString());
                    Dictionary<string, string> initialRobotParams = new Dictionary<string, string>();

                    initialRobotParams.Add("battery", "100");
                    initialRobotParams.Add("coordinate", robots.Sector.CoordToString());

                    await addRobot.SetAsync(initialRobotParams);
                    docId++;
                }
                Console.WriteLine("Robot added to database successfully.");
            }
        }

        private async Task FetchData(FirestoreDb database)
        {
            try
            {
                Query allProducts = database.Collection("All products");
                QuerySnapshot fetchedData = await allProducts.GetSnapshotAsync();

                if (fetchedData.Count != 0)
                {
                    foreach(DocumentSnapshot productInfo in fetchedData.Documents)
                    {
                        Dictionary<string, Object> prodDetail = productInfo.ToDictionary();
                        
                        List<Coordinate> assignCoord = new List<Coordinate>();
                        List<Object> fetchedCoordinates = (List<Object>) prodDetail["coordinate"];

                        foreach(var coord in fetchedCoordinates)
                        {
                            string[] assign = coord.ToString().Split(" ");

                            // Row, Column, Shelf
                            Coordinate fetched = new Coordinate(Convert.ToInt32(assign[0]), Convert.ToInt32(assign[1]), Convert.ToInt32(assign[2]));
                            assignCoord.Add(fetched);

                            isOccupied.Add(fetched);
                        }

                        // pharsing the coordinate and creating a list of Coordinate classes from Cloud Firestore
                        /*int stock = 0;
                        for (int i = 0; i< Convert.ToInt32(prodDetail["inStock"]); i++)
                        {
                            string key = "coordinate" + stock;
                            string[] assign = prodDetail[key].ToString().Split();

                            // Row, Column
                            Coordinate add = new Coordinate(Convert.ToInt32(assign[0]), Convert.ToInt32(assign[1]), Convert.ToInt32(assign[2]));

                            assignCoord.Add(add);
                        }*/
                        
                        // Creating Product object for all of the documents on Cloud Firestore
                        AllProducts.Add(new Products(
                            prodDetail["name"].ToString(),
                            productInfo.Id,     
                            assignCoord,
                            Convert.ToDouble(prodDetail["weight"]),
                            Convert.ToDouble(prodDetail["volume"]),
                            Convert.ToInt32(prodDetail["inStock"]),
                            Convert.ToDouble(prodDetail["price"])));

                    }

                    // Initializing the List isEmpty and isOccupied
                    foreach(var emptyShelf in accessibleLocations)
                    {
                        if (isOccupied.Contains(emptyShelf))
                        {
                            continue;
                        }
                        else
                        {
                            isEmpty.Add(emptyShelf);
                        }
                    }

                    Console.WriteLine("Products fetched sucessfully.");

                    foreach (var element in AllProducts)
                    {
                        Console.WriteLine(element.ProductID + " " + element.Location[0].Row + element.Location[0].Column + element.Location[0].Shelf);
                    }
                }
                else
                {
                    InitialCoordinateRandomizer(database);    // If and only if the warehouse is initially empty, restock with random distribution
                }
            }
            catch (Exception error)
            {
                Console.WriteLine(error);
            }
        }

        private void InitialCoordinateRandomizer(FirestoreDb database)
        {
            // Add new products if no data on Cloud Firestore
            List<Products> newProducts = new List<Products>()
            {
                new Products("TV", "1", 12.0, 0.373, 40, 5999.0),
                new Products("Sofa", "2", 30.0, 1.293, 40, 1250.0),
                new Products("Book", "3", 0.2, 0.005, 40, 12.0),
                new Products("Desk", "4", 22.1, 1.1, 40, 70.0),
                new Products("Phone", "5", 0.6, 0.001, 40, 1299.0),
                new Products("Bed", "6", 15, 0.73, 40, 199.0),
            };

            Random indexRandomizer = new Random();
            int totalIndex = (Rows * Columns * Shelves);

            for (int i = 0; i < totalIndex; i++)
            {
                int currentIndex = indexRandomizer.Next(totalIndex);
                isEmpty.Add(accessibleLocations[currentIndex]);
            }

            // Assigning Products to a random coordinate and update to Cloud Firestore
            foreach (var element in newProducts)
            {
                for (int i = 1; i <= element.InStock; i++)
                {
                    element.Location.Add(isEmpty[0]);
                    isOccupied.Add(isEmpty[0]);    // Once assigned to a Coordinate, toggle to isOccupied
                    isEmpty.RemoveAt(0);           // Remove the spot in isEmpty
                }
            }
            AddProductToFirebase(database, newProducts).Wait();
        }

        private async Task AddProductToFirebase(FirestoreDb database, List<Products> initialProducts)
        {
            try
            {
                CollectionReference addingProd = database.Collection("All products");

                foreach (var prod in initialProducts)
                {
                    Dictionary<string, Object> conversion = new Dictionary<string, object>();

                    //Dictionary<string, string> test =(Dictionary<string,string>)conversion["coordinate"];
                    
                    /*List<string> coordConversion = prod.CoordToArray();

                    int stock = 0;
                    foreach (var c in coordConversion)
                    {
                        string key = "coordinate" + stock;
                        conversion.Add(key, c);
                        stock++;
                    }*/
                    
                    conversion.Add("coordinate", prod.CoordToArray());
                    conversion.Add("inStock", prod.InStock);
                    conversion.Add("name", prod.ProductName);
                    conversion.Add("price", prod.Price);
                    conversion.Add("volume", prod.Volume);
                    conversion.Add("weight", prod.Weight);

                    await addingProd.AddAsync(conversion);
                }
                Console.WriteLine("Products added to database sucessfully");

            }
            catch (Exception error)
            {
                Console.WriteLine(error);
            }
        }

        private void OrderListener(FirestoreDb database)
        {
            Query incomingOrders = database.Collection("User Orders");

            FirestoreChangeListener notifier = incomingOrders.Listen(orders =>
            {
                //Console.WriteLine("New order received...");
                foreach (DocumentChange newOrders in orders.Changes)
                {
                    Dictionary<string, Object> newOrderDetail = newOrders.Document.ToDictionary();

                    List<Object> prodInOrder = (List<Object>)newOrderDetail["Items"];
                    List<Products> tempProd = new List<Products>();

                    if (Convert.ToBoolean(newOrderDetail["isShipped"]))
                    {
                        continue;
                    }
                    else
                    {
                        // Create Jobs and Store a copy of Orders locally
                        //Console.WriteLine("Creating jobs...");
                        foreach (var prod in prodInOrder)
                        {
                            // search id for the corresponding Product
                            foreach (var item in AllProducts)
                            {
                                if (prod.ToString() == item.ProductID)
                                {
                                    Jobs newJob = new Jobs(item, newOrders.Document.Id, false, true, item.Location[0], null);
                                    tempProd.Add(item);

                                    //Console.WriteLine("item Coord: " + item.ProductName + " " + item.Location[0].Row + item.Location[0].Column+ item.Location[0].Shelf);
                                    
                                    // Decrement stock when order is placed
                                    //item.InStock--;

                                    // TODO: Might move to when robot loaded products onto the trucks
                                    // Updating product remaining coordinates
                                    isEmpty.Add(item.Location[0]);
                                    isOccupied.Remove(item.Location[0]);
                                    item.Location.RemoveAt(0);


                                    //Console.WriteLine("latested Coord: " + item.ProductName + " " + item.Location[0].Row + item.Location[0].Column + item.Location[0].Shelf);

                                    AllJobs.Add(newJob);
                                    //Console.WriteLine("New job created sucessfully... " + newJob.ProdId.ProductName + " " + newJob.RetrieveCoord.Row + newJob.RetrieveCoord.Column + newJob.RetrieveCoord.Shelf + "\nShould be assigned to robot: " + newJob.RetrieveCoord.Column);

                                    // Instantiating orders locally
                                    addingOrder.WaitOne();
                                    LocalOrders.Add(new Orders(
                                        newOrders.Document.Id,
                                        tempProd,
                                        newOrderDetail["user"].ToString(),
                                        Convert.ToBoolean(newOrderDetail["isShipped"])
                                        ));
                                    addingOrder.ReleaseMutex();

                                    break;
                                }
                            }
                        }

                        // Assigning Jobs to robots by Column
                        foreach (var currentJobs in AllJobs)
                        {
                            Console.WriteLine("job location: " + currentJobs.RetrieveCoord.Row + currentJobs.RetrieveCoord.Column + currentJobs.RetrieveCoord.Shelf);

                            int toAssign = (currentJobs.RetrieveCoord.Column) - 1;    // calculating product location and the corresponding robot in that columns

                            Console.WriteLine("This job is assigned to robot: " + toAssign + " to retrieve " + currentJobs.ProdId.ProductName);
                            operationalRobots[toAssign].AddJob(currentJobs);

                        }

                        // Removing Jobs that are assigned to a robot
                        AllJobs.Clear();

                        Console.WriteLine("Jobs count: " + AllJobs.Count);
                    }
                    
                }
            });

            notifier.ListenerTask.Wait();
        }

        public static void AddToTruck(Jobs toTruck)
        {
            LoadedToTruck.Enqueue(toTruck);
        }


        private static void ShippingVerificationV2(FirestoreDb database)
        {
            while (true)
            {
                // perform check against LocalOrder and update to Firebase to notify user when every product in their order is shipped
                addingOrder.WaitOne();
                foreach (var loadedProduct in LoadedToTruck)
                {
                    string partialOrderId = loadedProduct.OrderId;
                    Products partialProduct = loadedProduct.ProdId;

                    foreach (var completeOrder in LocalOrders)
                    {
                        if (partialOrderId == completeOrder.OrderId)
                        {
                            if (completeOrder.Ordered.Contains(partialProduct))
                            {
                                // add new key value to dictionary, if key: orderId and value: product count == product count in actual order, then set status to true
                                if (partialOrders.ContainsKey(completeOrder.OrderId))
                                {
                                    partialOrders[completeOrder.OrderId]--;
                                }
                                else
                                {
                                    partialOrders.Add(completeOrder.OrderId, completeOrder.Ordered.Count - 1);
                                }
                                completeOrder.Ordered.Remove(partialProduct);
                                break;
                            }
                        }
                    }
                }
                addingOrder.ReleaseMutex();


                foreach (var pair in partialOrders)
                {
                    for (int i = 0; i < LocalOrders.Count; i++)
                    {
                        int emptyCount = 0;

                        if (pair.Key == LocalOrders[i].OrderId && pair.Value == emptyCount)
                        {
                            DocumentReference updateOrderStatus = database.Collection("User Orders").Document(LocalOrders[i].OrderId);
                            Dictionary<string, Object> toggleOrderStatus = new Dictionary<string, Object>();
                            toggleOrderStatus.Add("isShipped", true);

                            updateOrderStatus.UpdateAsync(toggleOrderStatus);
                            LocalOrders.RemoveAt(i);
                            break;
                        }
                    }
                }
                

























/*                dockTimer.Start();

                if (LoadedToTruck.TryDequeue(out Jobs currentJob) || waitForShip.Count != 0)
                {
                    int truckId = 1;
                    if (currentJob != null)
                    {
                        foreach (var availableTruck in shippingTrucks)
                        {
                            if (availableTruck)
                            {
                                break;
                            }
                            else
                            {
                                truckId++;
                            }
                        }

                        if ((carryWeight - currentJob.ProdId.Weight >= 0 && carryVol - currentJob.ProdId.Volume >= 0))
                        {
                            carryWeight -= currentJob.ProdId.Weight;
                            carryVol -= currentJob.ProdId.Volume;

                            Console.WriteLine("Allow loading to truck {0}",truckId);    // It worked just the truckId is weird.

                            waitForShip.Add(currentJob.ProdId);
                        }
                    }
                    else if (dockTimer.ElapsedMilliseconds >= 5000)
                    {
                        Console.WriteLine("Time warning: " + dockTimer.ElapsedMilliseconds);
                        dockTimer.Reset();

                        Console.WriteLine("Items in TruckID: ship{0} : ", truckId);    // It worked just the truckId is weird.
                        foreach (var item in waitForShip)
                        {
                            Console.WriteLine(item.ProductName);
                        }

                        waitForShip.Clear();
                        carryVol = TruckCapacityVol;
                        carryWeight = TruckCapacityWeight;

                        Console.WriteLine("TruckID: ship{0} leaving dock...", truckId);     // It worked just the truckId is weird.

                        shippingTrucks[truckId - 1] = !shippingTrucks[truckId - 1];

                        Task.Run(() =>
                        {
                            Random simulatedDeliveryTime = new Random();
                            int resetTime = simulatedDeliveryTime.Next(10000, 20000);

                            Thread.Sleep(resetTime);
                            Console.WriteLine("TruckID: ship{0} returned to docking station", truckId);     // It worked just the truckId is weird.

                            shippingTrucks[truckId - 1] = true;
                        });
                    }
                }*/
            }
        }
        private void LoadingToTruck()
        {
            while (true)
            {
                dockLocking.Release();


                waitDocking.Wait();
                
                foreach (var dockedTrucks in operationalShippingTrucks)
                {
                    if (dockedTrucks.IsAvailable)
                    {
                        int loadCount = LoadedToTruck.Count;

                        for(int i = 0; i < loadCount; i++)
                        {
                            LoadedToTruck.TryDequeue(out Jobs current);
                            if(dockedTrucks.LoadProduct(current.ProdId) == false)
                            {
                                break;
                            }
                        }
                    }
                }
            }   
        }
        private void RestockingVerification(FirestoreDb database)
        {
            Query checkStock = database.Collection("All products");

            FirestoreChangeListener lowStockAlert = checkStock.Listen(alert =>
            {
                foreach(var currentStock in alert.Changes)
                {
                    Dictionary<string, Object> lowAlertDict = currentStock.Document.ToDictionary();

                    if (Convert.ToInt32(lowAlertDict["inStock"]) == 10)
                    {
                        // Signal Restock and update on Firebase
                        int quantity = Convert.ToInt32(lowAlertDict["admin restock"]);
                        DocumentReference restock = database.Collection("All products").Document(currentStock.Document.Id);
                        lowAlertDict["inStock"] = quantity;
                    }
                }
            });
            lowStockAlert.ListenerTask.Wait();
        }
    }
}