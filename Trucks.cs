using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Text;

namespace AmazoomDebug
{
    class Trucks
    {
        private int truckVol = Warehouse.TruckCapacityVol;
        private int truckWeight = Warehouse.TruckCapacityWeight;
        public string TruckId { get; set; }
        public List<Products> ItemInTruck { get; set; } = new List<Products>();


        public void Deploy()
        {

        }

        private bool NotifyArrival()
        {
            
        }
        
    }
}
