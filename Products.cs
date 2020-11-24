using System;
using System.Collections.Generic;

namespace AmazoomDebug
{
    class Products
    {
        public string ProductName { get; set; }
        public string ProductID { get; set; }
        public List<Coordinate> Location { get; set; } = new List<Coordinate>();
        public double Weight { get; set; }
        public double Volume { get; set; }
        public int InStock { get; set; }
        public double Price { get; set; }


        public Products(string name, string id, List<Coordinate> pos, double weight, double volume, int inStock)
        {
            ProductName = name;
            ProductID = id;
            Location = pos;
            Weight = weight;
            Volume = volume;
            InStock = inStock;
        }

        public Products(string name, string id, double weight, double volume, int inStock, double price)
        {
            ProductName = name;
            ProductID = id;
            Weight = weight;
            Volume = volume;
            InStock = inStock;
            Price = price;
        }

        public List<Dictionary<string, int>> CoordToArray()
        {
            List<Dictionary<string, int>> toArray = new List<Dictionary<string, int>>();
            

            foreach(var coord in Location)
            {
                Dictionary<string, int> temp = new Dictionary<string, int>();
                temp["Row"] = coord.Row;
                temp["Column"] = coord.Column;
                temp["Shelf"] = coord.Shelf;

                toArray.Add(temp);
            }
            
            return toArray;
        }

    }
}
