﻿using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

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

        private static Queue<Coordinate> isEmpty = new Queue<Coordinate>();    // TODO: Change to Stack class or Queue class
        private static Queue<Coordinate> isOccupied = new Queue<Coordinate>();
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

            // Add tasks to robots somehow

            // Await for all tasks to finish executing
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
                    foreach(DocumentSnapshot productInfo in fetchedData)
                    {
                        Dictionary<string, Object> prodDetail = productInfo.ToDictionary();

                        // Creating Product object for all of the documents on Cloud Firestore
                        Warehouse.allProducts.Add(new Products(
                            prodDetail["name"].ToString(),
                            productInfo.Id,     
                                                                   // TODO: Randomize distribute and storage, might need to move this class into the Warehouse class
                            Convert.ToDouble(prodDetail["weight"]),
                            Convert.ToDouble(prodDetail["volume"]),
                            Convert.ToInt32(prodDetail["inStock"]),
                            Convert.ToDouble(prodDetail["price"])));
                    }

                    foreach(var element in Warehouse.allProducts)
                    {
                        Console.WriteLine(element.ProductID);
                    }
                }
                else
                {
                    InitialCoordinateRandomizer(database);
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
                new Products("Book", "3",0.2, 0.005, 40, 12.0),
                new Products("Desk", "4", 22.1, 1.1, 40, 70.0),
                new Products("Phone", "5", 0.6, 0.001, 40, 1299.0),
                new Products("Bed", "6", 15, 0.73, 40, 199.0),
            };

            Random indexRandomizer = new Random();
            int totalIndex = (Rows * Columns * Shelves);

            for (int i = 0; i < totalIndex; i++)
            {
                int currentIndex = indexRandomizer.Next(totalIndex);
                isEmpty.Enqueue(accessibleLocations[currentIndex]);
            }

            // Assigning Products to a random coordinate and update to Cloud Firestore
            foreach (var element in newProducts)
            {
                for (int i = 1; i <= element.InStock; i++)
                {
                    element.Location.Add(isEmpty.Peek());
                    isOccupied.Enqueue(isEmpty.Dequeue());
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

                    await addingProd.AddAsync(conversion);
                }
            }
            catch (Exception error)
            {
                Console.WriteLine(error);
            }
        }

        public static void AddToTruck(Jobs toTruck)
        {
            LoadedToTruck.Add(toTruck);
        }

        public static void RandomDistribution()
        {
            
        }

        public static void LoadingTruckVerification()
        {

        }
    }
}