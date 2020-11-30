using System;
using System.Drawing;

namespace AmazoomDebug
{
    class Coordinate
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public int Shelf { get; set; }
        public int RightLeft { get; set; }    // right = 1 and left = 2

        /// <summary>
        /// Constructor for Coordinate class
        /// </summary>
        /// <param name="row"> initial row </param>
        /// <param name="column"> initial column; fixed for a robot </param>
        /// <param name="shelf"> initial shelf location, defaults to 0 for a robot </param>
        public Coordinate(int row, int column, int shelf = -1, int orientation = -1)
        {
            Row = row;
            Column = column;
            Shelf = shelf;
            RightLeft = orientation;
        }

        public string CoordToString()
        {
            string coordniateString = Row + " " + Column + " " + Shelf + " " + RightLeft;

            return coordniateString;
        }

        /// <summary>
        /// Equals method override to check if the given object is equal to the current object of Coordinate class
        /// </summary>
        /// <param name="obj"> object to be check with the current object of Coordinate class</param>
        /// <returns>Return true if the two are equal as in same row, same column, and same shelf interger values. Return false otherwise.</returns>
        public override bool Equals(object obj)
        {
            return obj is Coordinate coordinate &&
                   Row == coordinate.Row &&
                   Column == coordinate.Column &&
                   Shelf == coordinate.Shelf &&
                   RightLeft == coordinate.RightLeft;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Row, Column, Shelf, RightLeft);
        }
    }
}