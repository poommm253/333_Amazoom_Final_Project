using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmazoomDebug
{
    class Coordinate
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public int Shelf { get; set; }

        /// <summary>
        /// Constructor for Coordinate class
        /// </summary>
        /// <param name="row"> initial row </param>
        /// <param name="column"> initial column; fixed for a robot </param>
        /// <param name="shelf"> initial shelf location, defaults to 0 for a robot </param>
        public Coordinate(int row, int column, int shelf = -1)
        {
            Row = row;
            Column = column;
            Shelf = shelf;
        }

        public string CoordToString()
        {
            string coordniateString = Row + " " + Column + " " + Shelf;

            return coordniateString;
        }
    }
}