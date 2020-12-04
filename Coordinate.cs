using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace AmazoomDebug
{
    public class Coordinate : IEquatable<Coordinate>
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

        public override bool Equals(object compareTo)
        {
            if (compareTo == null)
            {
                return false;
            }

            Coordinate convert = (Coordinate)compareTo;
            if (convert == null)
            {
                return false;
            }
            else
            {
                return Equals(convert);
            }
        }

        public override int GetHashCode()
        {
            return Row + Column + Shelf + RightLeft;
        }

        public bool Equals(Coordinate other)
        {
            if (other == null)
            {
                return false;
            }

            return (this.Row.Equals(other.Row) && this.Column.Equals(other.Column) && this.Shelf.Equals(other.Shelf) && this.RightLeft.Equals(other.RightLeft));
        }

        /// <summary>
        /// Equals method override to check if the given object is equal to the current object of Coordinate class
        /// </summary>
        /// <param name="obj"> object to be check with the current object of Coordinate class</param>
        /// <returns>Return true if the two are equal as in same row, same column, and same shelf interger values. Return false otherwise.</returns>

    }
}