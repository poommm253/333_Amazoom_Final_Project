using System.Collections.Generic;

namespace AmazoomDebug
{
    /// <summary>
    /// Products Class contains information of each individual products
    /// It contains the product's name, id, a list of location within the warehose, weight, volume, stock, and price
    /// </summary>
    class Products
    {
        public string ProductName { get; set; }
        public string ProductID { get; set; }
        public List<Coordinate> Location { get; set; } = new List<Coordinate>();
        public double Weight { get; set; }
        public double Volume { get; set; }
        public int InStock { get; set; }
        public double Price { get; set; }

        public Products(string name, string id, List<Coordinate> pos, double weight, double volume, int inStock, double price)
        {
            ProductName = name;
            ProductID = id;
            Location = pos;
            Weight = weight;
            Volume = volume;
            InStock = inStock;
            Price = price;
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

        /// <summary>
        /// Convert the list of coordinates containing all the locations of this specific product inside the warehouse into a list of string
        /// </summary>
        /// <returns>List of string of coordinates formatted as "row column shelf rightleft"</returns>
        public List<string> CoordToArray()
        {
            List<string> toArray = new List<string>();

            foreach (var coord in Location)
            {
                string temp = coord.Row + " " + coord.Column + " " + coord.Shelf + " " + coord.RightLeft;

                toArray.Add(temp);
            }

            return toArray;
        }
    }
}