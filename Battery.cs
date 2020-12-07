using System;
using System.Threading;

namespace AmazoomDebug
{
    /// <summary>
    /// unit = seconds
    /// max usage of 60 seconds from full charge (for the purpose of this demo, it is kept to be at 60 seconds)
    /// </summary>
    class Battery
    {
        // unit = seconds
        public int BatteryLevel { get; set; }
        private readonly double chargeRate = 16.66666;
        //private double drainRate = -1.666666;
        
        public Battery(int level)
        {
            BatteryLevel = level;  
        }

        /// <summary>
        /// reporting battery level in 10% intervals; critical battery level is 10% and must be recharged
        /// max usage 60 seconds from full charge; equivalent to -10% every 6 steps the robot takes
        /// </summary>
        /// <returns>returns true if the battery is over 10% meaning the robot can be used. Otherwise return false</returns>
        public bool Usage()
        {
            if (BatteryLevel == 10)
            {
                return false;    // Doesn't allow usage once battery is 10%
            }
            else
            {
                BatteryLevel -= 10;
                Console.WriteLine("Battery: " + BatteryLevel);
            }
            return true;
        }

        /// <summary>
        /// Charging the battery from the current battery level to full battery.
        /// Charging time varies according to the current remaining battery level.
        /// Charging rate is 16.666 meaning it takes 6 seconds to fully charge the battery
        /// </summary>
        public void Charge()
        {
            double chargeTime = (100 - BatteryLevel)/(chargeRate);
            Thread.Sleep(Convert.ToInt32(chargeTime));
            BatteryLevel = 100;
        }
    }
}