using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

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

        private List<Coordinate> IsEmpty = new List<Coordinate>();
        private List<Coordinate> IsStored = new List<Coordinate>();
        private List<Coordinate> accessibleLocations = new List<Coordinate>();
        private List<Robot> operationalRobots = new List<Robot>();

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
            catch (Exception e)
            {
                Console.WriteLine(e);
                setup.Close();
                Console.WriteLine("File not loaded properly and warehouse cannot be instantiated");
            }

            GenerateLayout();
            InstantiateRobots();
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

            // Randomize Product Coordinates (empty locations)
            Random indexRandomizer = new Random();
            int totalIndex = (Rows * Columns * Shelves) - 1;

            for(int i=0; i < totalIndex; i++)
            {
                int currentIndex = indexRandomizer.Next(totalIndex);
                IsEmpty.Add(accessibleLocations[currentIndex]);
            }
            
            // Assigning Products to a random coordinate
            // Get product from firebase
        }

        private void InstantiateRobots()
        {
            // Instantiating robots
            for (int i = 1; i <= Columns; i++)
            {
                operationalRobots.Add(new Robot("AMAZOOM_AW_" + i.ToString(), new Battery(100), new Coordinate(0, i)));
            }

            Task[] robots = new Task[Columns];

            // Deploying robots
            for (int i = 0; i < Columns; i++)
            {
                robots[i] = Task.Run(() => operationalRobots[i].Deploy());
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