using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using System.Linq;

namespace AmazoomDebug
{
    /// <summary>
    /// contains all the global constant for the entire system
    /// </summary>
    class Warehouse
    {
        
        public static int LoadingDockRow { get; set; }
        public static int Rows { get; set; }
        public static int Columns { get; set; }
        public static int TravelTime { get; set; }
        public static int RobotCapacity { get; set; } 
        public static int Shelves { get; set; }
        public static ConcurrentBag<Jobs> LoadedToTruck { get; set; } = new ConcurrentBag<Jobs>();
        public static List<Products> allProducts { get; set; } = new List<Products>();
        public static List<Jobs> IncomingOrders { get; set; } = new List<Jobs>();

        private static List<Coordinate> isEmpty = new List<Coordinate>();    // TODO: Change to Stack class or Queue class
        private static List<Coordinate> isOccupied = new List<Coordinate>();
        private static List<Coordinate> accessibleLocations = new List<Coordinate>();
        private static List<Robot> operationalRobots = new List<Robot>();

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
                Rows = Int32.Parse(keys[0]);
                Columns = Int32.Parse(keys[1]);
                Shelves = Int32.Parse(keys[2]);
                RobotCapacity = Int32.Parse(keys[3]);
                TravelTime = Int32.Parse(keys[4]);
                LoadingDockRow = Rows + 1;

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
            InstantiateRobots();

            string path = AppDomain.CurrentDomain.BaseDirectory + @"amazoom-c1397-firebase-adminsdk-ho7z7-6572726fc6.json";
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);
            FirestoreDb database = FirestoreDb.Create("amazoom-c1397");

            FetchData(database).Wait();

            // Deploying robots
            /*Task[] robots = new Task[Columns];
            for (int i = 0; i < Columns; i++)
            {
                robots[i] = Task.Run(() => operationalRobots[i].Deploy());
            }*/

            // Add tasks to robots somehow while (true)

            // Continuos check for incoming updates and order
            //Task.WaitAll(robots);
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

        private void InstantiateRobots()
        {
            // Instantiating robots
            for (int i = 1; i <= Columns; i++)
            {
                operationalRobots.Add(new Robot("AMAZOOM_AW_" + i.ToString(), new Battery(100), new Coordinate(0, i)));
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
                        List<Object> test = (List<Object>) prodDetail["coordinate"];

                        foreach(var item in test)
                        {
                            string[] assign = item.ToString().Split(" ");

                            // Row, Column, Shelf
                            Coordinate fetched = new Coordinate(Convert.ToInt32(assign[0]), Convert.ToInt32(assign[1]), Convert.ToInt32(assign[2]));
                            assignCoord.Add(fetched);
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
                        Warehouse.allProducts.Add(new Products(
                            prodDetail["name"].ToString(),
                            productInfo.Id,     
                            assignCoord,
                            Convert.ToDouble(prodDetail["weight"]),
                            Convert.ToDouble(prodDetail["volume"]),
                            Convert.ToInt32(prodDetail["inStock"]),
                            Convert.ToDouble(prodDetail["price"])));
                    }

                    foreach (var element in Warehouse.allProducts)
                    {
                        Console.WriteLine(element.Location[0].Column);
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
            // Add new products
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
            }
            catch (Exception error)
            {
                Console.WriteLine(error);
            }
        }

        private async Task OrderListener(FirestoreDb database)
        {
            Query incomingOrders = database.Collection("User Orders");
            FirestoreChangeListener notifier = incomingOrders.Listen(orders =>
            {
                Console.WriteLine("New order received");
                foreach(DocumentChange newOrders in orders.Changes)
                {
                    // Create Jobs
                }
            });
        }
        public static void AddToTruck(Jobs toTruck)
        {
            LoadedToTruck.Add(toTruck);
        }

        public static void LoadingTruckVerification()
        {

        }
    }
}