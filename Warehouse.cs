using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using System.Threading;
using System.IO;

namespace AmazoomDebug
{
    /// <summary>
    /// contains all the global constant for the entire system
    /// Warehouse central computer system that communicates with the Cloud Firestore, all the robots to assign jobs to, and the trucks going in and out of the warehouse
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
        public static List<InventoryTruck> operationalInvTrucks = new List<InventoryTruck>();

        private static Dictionary<string, int> partialOrders = new Dictionary<string, int>();

        private readonly double carryWeight;
        private readonly double carryVol;

        /// <summary>
        /// Constructor
        /// Prompts for a PIN code (123123 for the purpose of this demo) and the admin have 3 attempts
        /// Reads setup file and initializes the warehouse with all the primary global constants when a new Warehouse Object is instantiated
        /// Ask for admin authentication and file path
        /// </summary>
        public Warehouse()
        {
            bool loggedIn = false;

            for(int trial = 3; trial > 0; trial--)
            {
                Console.WriteLine("Please enter authentication PIN:");
                int pinCode = Convert.ToInt32(Console.ReadLine());

                if (pinCode == 123123)
                {
                    Console.WriteLine("PIN Correct");
                    Console.WriteLine("Enter a file path: (InitializationSetup/Setup_Warehouse.txt)");
                    string filePath = Console.ReadLine();

                    // read file to initialize warehouse
                    System.IO.StreamReader setup = new System.IO.StreamReader(filePath);
                    string line = setup.ReadLine();

                    // #rows, #columns, #shelves, robotCapacity, travelTime
                    string[] keys = line.Split(' ');

                    // Catch and print an error message if the file is inaccessible
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

                        loggedIn = true;
                        setup.Close();

                        Console.WriteLine("Setup complete!");
                    }
                    catch
                    {
                        setup.Close();
                        Console.WriteLine("File not loaded properly and warehouse cannot be instantiated");
                    }
                    break;
                }
                else
                {
                    Console.WriteLine("Wrong PIN. {0} attempts left", trial - 1);
                }
            }

            if (loggedIn == false)
            {
                Console.WriteLine("Set up failed. Please restart the system");
            }
        }

        /// <summary>
        /// Generate warehouse layout, robots, trucks, and products location. 
        /// Product information and location are fetched from Cloud Firestore
        /// 
        /// Run 2 threads to listen for updates from Cloud Firestore and are only activated when changes have been made to the database
        /// Run 1 thread for regulating docking area so only one truck can enter at a time
        /// Run 1 thread for sending updates of user's orders and its shipping status to Cloud Firestore
        /// </summary>
        public void Deploy()
        {
            // Initialization of Automated Warehouse
            GenerateLayout();

            // Established a connection to Cloud Firestore with a unique application id
            string path = AppDomain.CurrentDomain.BaseDirectory + @"amazoom-c1397-firebase-adminsdk-ho7z7-6572726fc6.json";
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);
            FirestoreDb database = FirestoreDb.Create("amazoom-c1397");

            InstantiateRobots(database).Wait();
            InstantiateTrucks(2, 1);
            FetchData(database).Wait();

            // Deploying robots
            Task[] robots = new Task[operationalRobots.Count];

            int index = 0;
            foreach (var opRobot in operationalRobots)
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

            // Automatic low stock alert
            Task restockingCheck = Task.Run(() => RestockingVerification(database));

            // Check for truck loading
            Task shippingCheck = Task.Run(() => ShippingVerificationV2(database));

            Task loadToTruck = Task.Run(() => LoadingToTruck());

            // Wait all
            Task.WaitAll(robots);
            Task.WaitAll(shippingTrucks);
            Task.WaitAll(inventoryTruck);
            orderCheck.Wait();
            shippingCheck.Wait();
            loadToTruck.Wait();
            restockingCheck.Wait();
        }

        /// <summary>
        /// Generate layout based on the given information from the setup file
        /// Store all accessible coordinates into a list called accessibleLocations
        /// </summary>
        private void GenerateLayout()
        {
            // Instantiate all Coordinate Location inside the warehouse
            for(int row = 1; row <= Rows; row++)
            {
                for (int col = 1; col <= Columns; col++)
                {
                    for(int shelf = 1; shelf <= Shelves; shelf++)
                    {
                        for(int orientation = 1; orientation <= 2; orientation++)
                        {
                            Coordinate generateLayout = new Coordinate(row, col, shelf, orientation);
                            accessibleLocations.Add(generateLayout);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Instantiating shipping and inventory trucks
        /// </summary>
        /// <param name="shipQuantity"> number of shipping trucks to be instantiated</param>
        /// <param name="invQuantity"> number of inventory trucks to be instantiated</param>
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

        /// <summary>
        /// Instantiate equal number of robots as the number of columns in the warehouse. Assign each robot to one column.
        /// </summary>
        /// <param name="database"> Firestore database instance </param>
        /// <returns> It is an asynchronous task that must be waited once called </returns>
        private async Task InstantiateRobots(FirestoreDb database)
        {
            // Catch and print errors regarding database connection
            try
            {
                Console.WriteLine("Robot Instantiation");

                // Check firestore for previously initialized robots and Instantiate robots based on previously saved locations
                Query allRobot = database.Collection("All robot");
                QuerySnapshot fetchedRobot = await allRobot.GetSnapshotAsync();

                if (fetchedRobot.Count != 0)
                {
                    foreach (DocumentSnapshot robotInfo in fetchedRobot.Documents)
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

                    // Updating robots information to Cloud Firestore with a unique id for each robot at 100% battery and starting at the origin
                    int docId = 1;
                    foreach (var robots in operationalRobots)
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
            catch (Exception error)
            {
                Console.WriteLine(error);
            }
        }

        /// <summary>
        /// Initial fetching of product information from Cloud Firestore
        /// If there is no information from Cloud Firestore, then the warehouse automatically generate 6 default products and randomly distribute them within the warehouse
        /// The newly generated products will then be updated to Cloud Firestore
        /// </summary>
        /// <param name="database"> Firestore database instance </param>
        /// <returns> It is an asynchronous task that must be waited once called </returns>
        private async Task FetchData(FirestoreDb database)
        {
            // Catch and print errors regarding database connection
            try
            {
                Query allProducts = database.Collection("All products");
                QuerySnapshot fetchedData = await allProducts.GetSnapshotAsync();

                // Use product information from Cloud Firestore if they exist
                if (fetchedData.Count != 0)
                {
                    foreach(DocumentSnapshot productInfo in fetchedData.Documents)
                    {
                        Dictionary<string, Object> prodDetail = productInfo.ToDictionary();
                        
                        List<Coordinate> assignCoord = new List<Coordinate>();
                        List<Object> fetchedCoordinates = (List<Object>) prodDetail["coordinate"];

                        // Creating and Storing strings of coordinates from Cloud Firestore as a Coordinate class
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
                        foreach(var emptySpace in accessibleLocations)
                        {
                            if(isOccupied.Contains(emptySpace) == false)
                            {
                                isEmpty.Add(emptySpace);
                            }
                        }
                    }

                    foreach (var element in AllProducts)
                    {
                        Console.WriteLine(element.ProductID + " " + element.Location[0].Row + element.Location[0].Column + element.Location[0].Shelf);
                    }
                }
                // If no product information is found on Cloud Firestore, the warehouse automatically generate new products
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

        /// <summary>
        /// Randomly distribute and store products within the warehouse. Update Cloud Firestore of each product's location
        /// </summary>
        /// <param name="database"> Firestore database instance </param>
        private void InitialCoordinateRandomizer(FirestoreDb database)
        {
            // Add new products if no data on Cloud Firestore
            List<Products> newProducts = new List<Products>()
            {
                new Products("TV", "1", 12.0, 0.373, 80, 5999.0),
                new Products("Sofa", "2", 30.0, 1.293, 80, 1250.0),
                new Products("Book", "3", 0.2, 0.005, 80, 12.0),
                new Products("Desk", "4", 22.1, 1.1, 80, 70.0),
                new Products("Phone", "5", 0.6, 0.001, 80, 1299.0),
                new Products("Bed", "6", 15, 0.73, 80, 199.0),
            };

            Random indexRandomizer = new Random();
            int totalIndex = (2 * Rows * Columns * Shelves);    // 480 for this demo
            List<int> usedIndex = new List<int>();

            lock (toggleWarehouseSpaceLock)
            {
                while(usedIndex.Count != totalIndex)
                {
                    int currentIndex = indexRandomizer.Next(totalIndex);

                    if(usedIndex.Contains(currentIndex) == false)
                    {
                        usedIndex.Add(currentIndex);
                        isEmpty.Add(accessibleLocations[currentIndex]);
                    }                    
                }

                // Assigning Products to a random coordinate and update to Cloud Firestore
                foreach (var element in newProducts)
                {
                    for (int i = 1; i <= element.InStock; i++)
                    {
                        element.Location.Add(isEmpty[0]);    // Adding a new random coordinate for the product
                        isOccupied.Add(isEmpty[0]);          // Once assigned to a Coordinate, toggle to isOccupied
                        isEmpty.RemoveAt(0);                 // Remove the spot in isEmpty
                    }
                }
            }
            AddProductToFirebase(database, newProducts).Wait();    // Adding product information to Cloud Firestore
        }

        /// <summary>
        /// Asynchronously add products information to Cloud Firestore
        /// </summary>
        /// <param name="database"> Firestore database instance </param>
        /// <param name="initialProducts"> List of products to be added to Cloud Firestore </param>
        /// <returns> It is an asynchronous task that must be waited once called</returns>
        private async Task AddProductToFirebase(FirestoreDb database, List<Products> initialProducts)
        {
            // Catch and print errors regarding database connection
            try
            {
                CollectionReference addingProd = database.Collection("All products");

                foreach (var prod in initialProducts)
                {
                    Dictionary<string, Object> conversion = new Dictionary<string, object>();
                    
                    conversion.Add("coordinate", prod.CoordToArray());    // storing all the coordinates as a List of strings on Firestore
                    conversion.Add("inStock", prod.InStock);
                    conversion.Add("name", prod.ProductName);
                    conversion.Add("price", prod.Price);
                    conversion.Add("volume", prod.Volume);
                    conversion.Add("weight", prod.Weight);
                    conversion.Add("admin restock", 80);

                    await addingProd.AddAsync(conversion);
                }
                Console.WriteLine("Products added to database sucessfully");
            }
            catch (Exception error)
            {
                Console.WriteLine(error);
            }
        }

        /// <summary>
        /// Background listener that only gets activated once there is a change in the "User Orders" collection on Cloud Firestore
        /// </summary>
        /// <param name="database"> Firestore database instance</param>
        private void OrderListener(FirestoreDb database)
        {
            Query incomingOrders = database.Collection("User Orders");

            FirestoreChangeListener notifier = incomingOrders.Listen(async orders =>
            {
                // TESTING: that a new order triggers this method and a new order is stored locally: PASSED
                Console.WriteLine("New order received...");

                foreach (DocumentChange newOrders in orders.Changes)
                {
                    Dictionary<string, Object> newOrderDetail = newOrders.Document.ToDictionary();

                    List<Object> prodInOrder = (List<Object>)newOrderDetail["Items"];
                    List<Products> tempProd = new List<Products>();
                    
                    // Only locally store incomplete orders in the warehouse central computer to be processed
                    if (Convert.ToBoolean(newOrderDetail["isShipped"]))
                    {
                        continue;
                    }
                    else
                    {
                        // Create Jobs and Store a copy of Orders locally
                        foreach (var prod in prodInOrder)
                        {
                            // Search id for the corresponding Product
                            foreach (var item in AllProducts)
                            {
                                if (prod.ToString() == item.ProductID)
                                {
                                    Console.WriteLine("Creating jobs...");

                                    // Creating a retrieval job
                                    Jobs newJob = new Jobs(item, newOrders.Document.Id, false, true, item.Location[0], null);
                                    tempProd.Add(item);

                                    Console.WriteLine("item Coord: " + item.ProductName + " " + item.Location[0].Row + item.Location[0].Column+ item.Location[0].Shelf + item.Location[0].RightLeft);

                                    // Updating empty shelves in the warehouse
                                    lock (toggleWarehouseSpaceLock)
                                    {
                                        isEmpty.Add(item.Location[0]);
                                        isOccupied.Remove(item.Location[0]);
                                        item.Location.RemoveAt(0);
                                    }
                                   
                                    Console.WriteLine("latested Coord: " + item.ProductName + " " + item.Location[0].Row + item.Location[0].Column + item.Location[0].Shelf + item.Location[0].RightLeft);

                                    lock (addingJobs)
                                    {
                                        AllJobs.Add(newJob);
                                    }
                                    
                                    // TESTING: to see if new jobs is created. Product name and retrieval coordinate should match with the one on Cloud Firestore: PASSED
                                    Console.WriteLine("New job created sucessfully... " + newJob.ProdId.ProductName + " " + newJob.RetrieveCoord.Row + newJob.RetrieveCoord.Column + newJob.RetrieveCoord.Shelf + item.Location[0].RightLeft + "\nShould be assigned to robot: " + newJob.RetrieveCoord.Column);

                                    lock (addingOrderLock)
                                    {
                                        // Instantiating incomplete jobs locally
                                        LocalOrders.Add(new Orders(
                                        newOrders.Document.Id,
                                        tempProd,
                                        newOrderDetail["user"].ToString(),
                                        Convert.ToBoolean(newOrderDetail["isShipped"])
                                        ));
                                    }

                                    break;
                                }
                            }
                        }

                        // Update Cloud Firestore of the latest coordinates for each product. Coordinates should be removed.
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
                                if (currentJobs.Retrieve)
                                {
                                    // TESTING: Check if the right coordinate is assigned to the correct robot by printing out the current coordinate. Robot from that role should receive the job : PASSED
                                    Console.WriteLine("job location: " + currentJobs.RetrieveCoord.Row + currentJobs.RetrieveCoord.Column + currentJobs.RetrieveCoord.Shelf);

                                    int toAssign = (currentJobs.RetrieveCoord.Column) - 1;    // calculating product location and the corresponding robot in that columns

                                    // TESTING: Check if the right robot got assigned to the job by printing out the assigned robot and the product name : PASSED
                                    Console.WriteLine("This job is assigned to robot: " + toAssign + " to retrieve " + currentJobs.ProdId.ProductName);
                                    operationalRobots[toAssign].AddJob(currentJobs);
                                }
                            }
                            // Removing Jobs that are already assigned to a robot
                            AllJobs.Clear();
                        }
                    }
                }
            });

            // Thread does not end, but only get activated once there is an update in Cloud Firestore "User Orders" collection
            notifier.ListenerTask.Wait();
        }

        /// <summary>
        /// Add item to shipping truck
        /// </summary>
        /// <param name="toTruck"> Job that contains the order id and product information that will be loaded to the shipping truck </param>
        public static void AddToTruck(Jobs toTruck)
        {
            LoadedToTruck.Enqueue(toTruck);
        }

        /// <summary>
        /// Verify and update order status to Cloud Firestore to true if all of the products within that order is loaded to a shipping truck
        /// </summary>
        /// <param name="database"> Firestore database instance </param>
        private async void ShippingVerificationV2(FirestoreDb database)
        {
            while (true)
            {
                // perform check against LocalOrder and update to Firebase to notify user when every product in their order is shipped
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
                                    // decrement item count that is left for the corresponding order id
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

                // For each partial orders, check to see if an order is complete, if yes, change the shipping status to true and update on to Cloud Firestore
                foreach (var pair in partialOrders)
                {
                    for (int i = 0; i < LocalOrders.Count; i++)
                    {
                        int emptyCount = 0;

                        // For a complete order, the dictionary containing the order id and item count; the item count should be zero indicating that the order is complete
                        if (pair.Key == LocalOrders[i].OrderId && pair.Value == emptyCount)
                        {
                            DocumentReference updateOrderStatus = database.Collection("User Orders").Document(LocalOrders[i].OrderId);
                            Dictionary<string, Object> toggleOrderStatus = new Dictionary<string, Object>();
                            toggleOrderStatus.Add("isShipped", true);

                            // Sync to Cloud Firestore
                            await updateOrderStatus.UpdateAsync(toggleOrderStatus);

                            LocalOrders.RemoveAt(i);

                            break;
                        }
                    }
                }
                // Releaseing resources for this thread. This thread does not need to update instantly
                Thread.Sleep(2000);
            }
        }

        /// <summary>
        /// Regulates the docking area of the warehouse. Only allow one truck to enter the docking area at a time
        /// For invertory trucks, check and create restocking orders for the corresponding robots in the corresponding columns
        /// The restock is still randomly distributed
        /// </summary>
        private void LoadingToTruck()
        {
            while (true)
            {
                // Allowing a new truck (thread) to enter the docking area
                dockLocking.Release();
                Console.WriteLine("Warehouse releasing dock");

                // Check for restocking and inventory trucks
                foreach(var invTruck in operationalInvTrucks)
                {
                    if (invTruck.IsReady)
                    {
                        // create restocking job for the robot
                        lock (addingJobs)
                        {
                            foreach (var restockJob in invTruck.ItemInTruck)
                            {
                                lock (toggleWarehouseSpaceLock)
                                {
                                    // Updating empty shelves in the warehouse
                                    isOccupied.Add(isEmpty[0]);

                                    // Creating a new restocking job with any available shelf unit. This will allow for a random restocking of product
                                    Jobs restock = new Jobs(restockJob, null, true, false, null, isEmpty[0]);

                                    isEmpty.RemoveAt(0);
                                    AllJobs.Add(restock);
                                }
                            }
                            invTruck.ItemInTruck.Clear();

                            foreach(var currentJobs in AllJobs)
                            {
                                if (currentJobs.Restock)
                                {
                                    // assigning a restocking job to the corresponding robot 
                                    int toAssign = (currentJobs.RestockCoord.Column) - 1;
                                    operationalRobots[toAssign].AddJob(currentJobs);
                                }
                            }
                            AllJobs.Clear();    // clear jobs that have already been assigned to a robot
                        }
                    }
                }
                // For inventory truck
                createRestockingJob.Release();

                // Wait for the current truck to leave the docking area first before signalling another truck in the waiting area that it can be docked
                Console.WriteLine("Stuck waiting for release");
                waitDocking.Wait();
                Console.WriteLine("Warehouse waiting for truck to release");
             }
        }

        /// <summary>
        /// Background listener that only gets activated once there is a change in the "All product" collection on Cloud Firestore and if the product has "inStock" of less than 60
        /// </summary>
        /// <param name="database"> Firestore database instance </param>
        private void RestockingVerification(FirestoreDb database)
        {
            // setting low stock to be 60 (for the purpose of the demo) and get an aleart for restocking prompt
            Query checkStock = database.Collection("All products").WhereLessThanOrEqualTo("inStock", 60);

            FirestoreChangeListener lowStockAlert = checkStock.Listen(async alert =>
            {
                foreach(var currentStock in alert.Documents)
                {
                    Dictionary<string, Object> lowAlertDict = currentStock.ToDictionary();

                    int quantity = Convert.ToInt32(lowAlertDict["admin restock"]);
                    int inStock = Convert.ToInt32(lowAlertDict["inStock"]);

                    // Loading product to inventory truck; check weight and volume and creating a truck
                    foreach (var allProd in AllProducts)
                    {
                        if(allProd.ProductID.Equals(currentStock.Id))
                        {
                            // loading restocking items to an available inventory truck by the difference in the amount of stock remaining and the automatic restock setting "admin restock"
                            for (int i = 0; i < Math.Abs(quantity-inStock); i++)
                            {
                                RestockItem.Enqueue(allProd);
                                Console.WriteLine("Restock confirmed");
                            }

                            // new dictionary for updating to Cloud Firestore
                            Dictionary<string, Object> updateStock = new Dictionary<string, object>
                            {
                                { "inStock", quantity }
                            };

                            DocumentReference update = database.Collection("All products").Document(currentStock.Id);

                            // Updating the stock of the product to Cloud Firestore. This allows for user to order the same product even though the robot havent restocked the product back to a shelf yet
                            // This is made possible because the robot prioritizes restocking in addition to finding the shortest path
                            await update.UpdateAsync(updateStock);

                            break;
                        }
                    }
                }
            });
            // Thread does not end, but only get activated once there is an update in Cloud Firestore "All products" collection with "inStock" of less than 60
            lowStockAlert.ListenerTask.Wait();
        }
    }
}