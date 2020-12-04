using System;
using System.Threading;

namespace AmazoomDebug
{
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

        // reporting battery level in 10% intervals; critical level = 10%
        // max 60 sec of use before charging from full charge
        // looses 10% of charge every 6 sec == 6000 ms
        public bool Usage()
        {
            if (BatteryLevel == 10)
            {
                return false;    // doesnt allow usage once battery is 10%
            }
            else
            {
                BatteryLevel -= 10;
                Console.WriteLine("Battery: " + BatteryLevel);
            }
            return true;
        }

        // reporting battery level when 100%, 75%, 50%, 25%, 10% (low battery need to charge)
        // takes 6 sec to fully charge, therefore 5.4 seconds for 10% - 100%
        public void Charge()
        {
            double chargeTime = (100 - BatteryLevel)/(chargeRate);
            Thread.Sleep(Convert.ToInt32(chargeTime));
            BatteryLevel = 100;
        }
    }
}
