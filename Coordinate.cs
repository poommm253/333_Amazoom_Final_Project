using System;

namespace AmazoomDebug
{
    /// <summary>
    /// Coordinate class for keeping track of locations in the warehouse
    /// </summary>
    public class Coordinate : IEquatable<Coordinate>
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public int Shelf { get; set; }
        public int RightLeft { get; set; }    // right = 1 and left = 2

        public Coordinate(int row, int column, int shelf = -1, int orientation = -1)
        {
            Row = row;
            Column = column;
            Shelf = shelf;
            RightLeft = orientation;
        }

        /// <summary>
        /// Converting coordinate class into string
        /// </summary>
        /// <returns>String formatted as "row column shelf rightleft"</returns>
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

        /// <summary>
        /// Get Hash Code method for Coordinate class object identification
        /// </summary>
        /// <returns>unique hash code for each coordinate class object</returns>
        public override int GetHashCode()
        {
            return Row + Column + Shelf + RightLeft;
        }

        /// <summary>
        /// Check if the given coordinate class matches with the current coordinate class
        /// </summary>
        /// <param name="other">Coordinate class object that will be compared to the current Coordinate class object</param>
        /// <returns>Returns true if the two coordinates are the same. Otherwise returns false</returns>
        public bool Equals(Coordinate other)
        {
            if (other == null)
            {
                return false;
            }

            return (this.Row.Equals(other.Row) && this.Column.Equals(other.Column) && this.Shelf.Equals(other.Shelf) && this.RightLeft.Equals(other.RightLeft));
        }
    }
}