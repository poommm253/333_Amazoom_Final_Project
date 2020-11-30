using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using System.Threading;
using System.Linq;
using System.IO.MemoryMappedFiles;

namespace AmazoomDebug
{
    /// <summary>
    /// contains all the global constant for the entire system
    /// </summary>
    class Warehouse
    {
        private static readonly Object addingOrderLock = new Object();
        private static readonly Object toggleWarehouseSpaceLock = new Object();
        private static readonly Object addingJobs = new Object();

        public static SemaphoreSlim dockLocking = new SemaphoreSlim(0);
        public static SemaphoreSlim waitDocking = new SemaphoreSlim(0);
        public static SemaphoreSlim invTruckLoading = new SemaphoreSlim(0);
        public static SemaphoreSlim createRestockingJob = new SemaphoreSlim(0);

        public static int LoadingDockRow { get; set; }
        public static int Rows { get; set; }
        public static int Columns { get; set; }
        public static int TravelTime { get; set; }
        public static int RobotCapacity { get; set; } 
        public static double TruckCapacityVol { get; set; }
        public static double TruckCapacityWeight { get; set; }
        public static int Shelves { get; set; }
        public static ConcurrentQueue<Jobs> LoadedToTruck { get; set; } = new ConcurrentQueue<Jobs>();
        public static ConcurrentQueue<Products> RestockItem { get; set; } = new ConcurrentQueue<Products>();
        public static List<Products> AllProducts { get; set; } = new List<Products>();
        public static List<Jobs> AllJobs { get; set; } = new List<Jobs>();
        public static List<Orders> LocalOrders { get; set; } = new List<Orders>();

        private static List<Coordinate> isEmpty = new List<Coordinate>();
        private static List<Coordinate> isOccupied = new List<Coordinate>();
        private static List<Coordinate> accessibleLocations = new List<Coordinate>();

        private static List<Robot> operationalRobots = new List<Robot>();
        private static List<ShippingTruck> operationalShippingTrucks = new List<ShippingTruck>();
        private static List<InventoryTruck> operationalInvTrucks = new List<InventoryTruck>();

        private static Dictionary<string, int> partialOrders = new Dictionary<string, int>();


        private readonly double carryWeight;
        private readonly double carryVol;
        private int invTruckNumber = 2;

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
                TravelTime = Int32.Parse(keys[4]);              //1000
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
            InstantiateTrucks(2, 1);
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
            Task[] shippingTrucks = new Task[operationalShippingTrucks.Count];
            Task[] inventoryTruck = new Task[operationalShippingTrucks.Count];

            int tIndex = 0;
            int sIndex = 0;
            foreach (var opShipTruck in operationalShippingTrucks)
            {
                shippingTrucks[sIndex] = Task.Run(() => opShipTruck.Deploy());
                sIndex++;
            }
            foreach (var opInvTruck in operationalInvTrucks)
            {
                inventoryTruck[tIndex] = Task.Run(() => opInvTruck.Deploy());
                tIndex++;
            }

            // Check for incoming order in the background and assign jobs to the robots all in the background and adding tasks to the robot
            Task orderCheck = Task.Run(() => OrderListener(database));

            // Check for truck loading
            Task shippingCheck = Task.Run(() => ShippingVerificationV2(database));
            Task loadToTruck = Task.Run(() => LoadingToTruck());

            // Automatic low stock alert
            Task restockingCheck = Task.Run(() => RestockingVerification(database));

            // Wait all
            Task.WaitAll(robots);
            Task.WaitAll(shippingTrucks);
            Task.WaitAll(inventoryTruck);
            loadToTruck.Wait();
            restockingCheck.Wait();
            orderCheck.Wait();
            shippingCheck.Wait();
        }

        private void GenerateLayout()
        {
            // Instantiate all Coordinate Location inside the warehouse
            for(int row = 1; row <= Rows; row++)
            {
                for (int col = 1; col <= Columns; col++)
                {
                    int rightLeft;

                    if (col == 1 || col == Columns)
                    {
                        rightLeft = 2;
                    }
                    else
                    {
                        rightLeft = 1;
                    }

                    for(int shelf = 1; shelf <= Shelves; shelf++)
                    {
                        for(; rightLeft <= 2; rightLeft++)
                        {
                            Coordinate generateLayout = new Coordinate(row, col, shelf, rightLeft);
                            accessibleLocations.Add(generateLayout);
                        }
                    }
                }
            }
        }

        private void InstantiateTrucks(int shipQuantity, int invQuantity)
        {
            for(int i = 0; i < shipQuantity; i++)
            {
                operationalShippingTrucks.Add(new ShippingTruck("ShipTruck_" + i));
            }
            for(int i = 0; i < invQuantity; i++)
            {
                operationalInvTrucks.Add(new InventoryTruck("InvTruck_" + i));
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
                        new Coordinate(Convert.ToInt32(fetchedCoordinate[0]), Convert.ToInt32(fetchedCoordinate[1]))
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

                            // Row, Column, Shelf, RightLeft
                            Coordinate fetched = new Coordinate(Convert.ToInt32(assign[0]), Convert.ToInt32(assign[1]), Convert.ToInt32(assign[2]), Convert.ToInt32(assign[3]));
                            assignCoord.Add(fetched);

                            isOccupied.Add(fetched);
                        }
                        
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
                    Console.WriteLine("Products fetched sucessfully.");

                    // checking for empty spots in the warehouse by comparing the isOccupied List to the default accessibleLocations list and take the differences between the two lists
                    lock (toggleWarehouseSpaceLock)
                    {
                        isEmpty = isOccupied.Where(x => !accessibleLocations.Contains(x)).ToList();
                    }

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
            int totalIndex = (2 * Rows * Columns * Shelves);

            lock (toggleWarehouseSpaceLock)
            {
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
                    
                    conversion.Add("coordinate", prod.CoordToArray());
                    conversion.Add("inStock", prod.InStock);
                    conversion.Add("name", prod.ProductName);
                    conversion.Add("price", prod.Price);
                    conversion.Add("volume", prod.Volume);
                    conversion.Add("weight", prod.Weight);
                    conversion.Add("admin restock", 0);

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

            FirestoreChangeListener notifier = incomingOrders.Listen(async orders =>
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
                        Console.WriteLine("Creating jobs...");
                        foreach (var prod in prodInOrder)
                        {
                            // search id for the corresponding Product
                            foreach (var item in AllProducts)
                            {
                                if (prod.ToString() == item.ProductID)
                                {
                                    Jobs newJob = new Jobs(item, newOrders.Document.Id, false, true, item.Location[0], null);
                                    tempProd.Add(item);

                                    Console.WriteLine("item Coord: " + item.ProductName + " " + item.Location[0].Row + item.Location[0].Column+ item.Location[0].Shelf);

                                    // Decrement stock when order is placed
                                    //item.InStock--;

                                    // TODO: Might move to when robot loaded products onto the trucks
                                    // Updating product remaining coordinates

                                    //isEmpty = isOccupied.Where(x => !accessibleLocations.Contains(x)).ToList();

                                    lock (toggleWarehouseSpaceLock)
                                    {
                                        isEmpty.Add(item.Location[0]);
                                        isOccupied.Remove(item.Location[0]);
                                        item.Location.RemoveAt(0);
                                    }
                                   
                                    Console.WriteLine("latested Coord: " + item.ProductName + " " + item.Location[0].Row + item.Location[0].Column + item.Location[0].Shelf);

                                    // TODO: LOCK AllJobs
                                    lock (addingJobs)
                                    {
                                        AllJobs.Add(newJob);
                                    }
                                    Console.WriteLine("New job created sucessfully... " + newJob.ProdId.ProductName + " " + newJob.RetrieveCoord.Row + newJob.RetrieveCoord.Column + newJob.RetrieveCoord.Shelf + "\nShould be assigned to robot: " + newJob.RetrieveCoord.Column);

                                    // Instantiating orders locally
                                    //addingOrder.WaitOne();
                                    
                                    lock (addingOrderLock)
                                    {
                                        LocalOrders.Add(new Orders(
                                        newOrders.Document.Id,
                                        tempProd,
                                        newOrderDetail["user"].ToString(),
                                        Convert.ToBoolean(newOrderDetail["isShipped"])
                                        ));
                                    }
                                    //addingOrder.ReleaseMutex();

                                    break;
                                }
                            }
                        }

                        // TODO: Used to by at the end of ShippingVerificationV2 method (double check this)
                        foreach (var allProd in AllProducts)
                        {
                            DocumentReference updateStock = database.Collection("All products").Document(allProd.ProductID);
                            Dictionary<string, Object> update = new Dictionary<string, object>();

                            update.Add("coordinate", allProd.CoordToArray());
                            await updateStock.UpdateAsync(update);
                        }

                        // Assigning Jobs to robots by Column
                        lock (addingJobs)
                        {
                            foreach (var currentJobs in AllJobs)
                            {
                                Console.WriteLine("job location: " + currentJobs.RetrieveCoord.Row + currentJobs.RetrieveCoord.Column + currentJobs.RetrieveCoord.Shelf);

                                int toAssign = (currentJobs.RetrieveCoord.Column) - 1;    // calculating product location and the corresponding robot in that columns

                                Console.WriteLine("This job is assigned to robot: " + toAssign + " to retrieve " + currentJobs.ProdId.ProductName);
                                operationalRobots[toAssign].AddJob(currentJobs);

                            }
                            // Removing Jobs that are assigned to a robot
                            AllJobs.Clear();
                        }
                    }
                }
            });

            notifier.ListenerTask.Wait();
        }

        public static void AddToTruck(Jobs toTruck)
        {
            LoadedToTruck.Enqueue(toTruck);
        }

        private static async void ShippingVerificationV2(FirestoreDb database)
        {
            while (true)
            {
                // perform check against LocalOrder and update to Firebase to notify user when every product in their order is shipped
                //addingOrder.WaitOne();

                lock (addingOrderLock)
                {
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
                }
                //addingOrder.ReleaseMutex();

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

                            await updateOrderStatus.UpdateAsync(toggleOrderStatus);
                            LocalOrders.RemoveAt(i);

                            break;
                        }
                    }
                }
            }
        }

        private void LoadingToTruck()
        {
            while (true)
            {
                dockLocking.Release();
                Console.WriteLine("Warehouse releasing dock");

                // check wa inventory truck kun nhai ready
                foreach(var invTruck in operationalInvTrucks)
                {
                    if (invTruck.IsReady)
                    {
                        // create restocking job for the robot
                        lock (addingJobs)
                        {
                            foreach (var restockJob in invTruck.ItemInTruck)
                            {

                                isOccupied.Add(isEmpty[0]);
                                Jobs restock = new Jobs(restockJob, null, true, false, isEmpty[0], null);

                                isEmpty.RemoveAt(0);
                                AllJobs.Add(restock);

                            }
                            invTruck.ItemInTruck.Clear();

                            foreach(var currentJobs in AllJobs)
                            {
                                int toAssign = (currentJobs.RestockCoord.Column) - 1;
                                operationalRobots[toAssign].AddJob(currentJobs);
                            }

                            AllJobs.Clear();
                        }
                    }
                }

                createRestockingJob.Release();

                waitDocking.Wait();
                Console.WriteLine("Warehouse waiting for truck to release");
            }
        }

        private void RestockingVerification(FirestoreDb database)
        {
            // setting low stock to be 10 and get an aleart for restocking prompt
            Query checkStock = database.Collection("All products").WhereLessThanOrEqualTo("inStock", 10);

            FirestoreChangeListener lowStockAlert = checkStock.Listen(async alert =>
            {
                foreach(var currentStock in alert.Changes)
                {
                    Dictionary<string, Object> lowAlertDict = currentStock.Document.ToDictionary();
                    
                    // Signal Restock and update on Cloud Firestore
                    int quantity = Convert.ToInt32(lowAlertDict["admin restock"]);

                    // Loading product to inventory truck; check weight and volume and creating a truck
                    foreach(var allProd in AllProducts)
                    {
                        if(allProd.ProductID == currentStock.Document.Id)
                        {
                            for(int i = 0; i < quantity; i++)
                            {
                                RestockItem.Enqueue(allProd);
                            }
                        }
                    }

                    invTruckLoading.Release();

                    // add another lock for second inverntory truck before releasing invTruckLoading.Release() again

                    // Update on Cloud Firestore
                    DocumentReference restock = database.Collection("All products").Document(currentStock.Document.Id);
                    Dictionary<string, Object> lowStockUpdate = new Dictionary<string, object>();

                    lowStockUpdate["inStock"] = Convert.ToInt32(lowAlertDict["inStock"]) + quantity;

                    await restock.UpdateAsync(lowStockUpdate);

                    // Updating the coordinate list locally and on Cloud Firestore
                    foreach(var allProd in AllProducts)
                    {
                        if (allProd.ProductID == currentStock.Document.Id)
                        {
                            lock (toggleWarehouseSpaceLock)
                            {
                                for(int i = 0; i < quantity; i++)
                                {
                                    allProd.Location.Add(isEmpty[0]);
                                    isOccupied.Add(isEmpty[0]);
                                    isEmpty.RemoveAt(0);
                                }
                            }    
                                                                  
                            DocumentReference updateStock = database.Collection("All products").Document(allProd.ProductID);
                            Dictionary<string, Object> update = new Dictionary<string, object>();

                            update.Add("coordinate", allProd.CoordToArray());
                            await updateStock.UpdateAsync(update);
                        }
                    }
                }
            });

            lowStockAlert.ListenerTask.Wait();
        }
    }
}