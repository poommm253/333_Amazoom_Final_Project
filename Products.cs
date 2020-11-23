using System;

namespace AmazoomDebug
{
    class Products
    {
        public string ProductName { get; set; }
        public string ProductID { get; set; }
        public Coordinate Location { get; set; }
        public double Weight { get; set; }
        public double Volume { get; set; }
        public int InStock { get; set; }
        
        public Products(string name, string id, Coordinate pos, double weight, double volume, int inStock)
        {
            ProductName = name;
            ProductID = id;
            Location = pos;
            Weight = weight;
            Volume = volume;
            InStock = inStock;
        }
    }
}
