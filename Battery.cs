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

            //===== Previous Version; Time Dependent =====//

            /*int fullDepletionTime = 60000;    // default = 60000 ms
            int elapsed = 1;
            int drainRate = fullDepletionTime / 10;    // 10% interval

            batteryLife.Restart();    // start timer

            while (true)
            {
                if (batteryLife.ElapsedMilliseconds == elapsed * drainRate)
                {
                    BatteryLevel -= 10;    // update battery level every 25%
                    Console.WriteLine("Time: " + batteryLife.ElapsedMilliseconds);
                    Console.WriteLine("Battery: " + BatteryLevel);
                    elapsed++;

                    if (BatteryLevel == 10)
                    {
                        Console.WriteLine("10%");
                        Charge();
                        batteryLife.Stop();
                        //return false;
                    }
                }
            }*/
        }

        // reporting battery level when 100%, 75%, 50%, 25%, 10% (low battery need to charge)
        // takes 6 sec to fully charge, therefore 5.4 seconds for 10% - 100%
        public void Charge()
        {
            double chargeTime = (100 - BatteryLevel)/(chargeRate);
            Thread.Sleep(Convert.ToInt32(chargeTime));
            BatteryLevel = 100;
            //Console.WriteLine("Battery Recharged: " + BatteryLevel);

            //===== Previous Version; Time Dependent =====//

            /*int fullChargeTime = 6000;    // default 6000 ms
            int elapsed = 1;
            int chargeRate = fullChargeTime / 10;    // takes approximately 6 seconds to fully charge / 10% interval
            int fulBattery = 5400;     // takes 5.4 sec to charge from 10% to 100% before discharge; default = 5400 ms

            batteryLife.Restart();     // stop previous timer and start new timer

            while (batteryLife.ElapsedMilliseconds != fulBattery)
            {
                if (batteryLife.ElapsedMilliseconds == elapsed * chargeRate)
                {
                    BatteryLevel += 10;    // charge 10% every 6000 ms
                    elapsed++;
                }
            }

            Console.WriteLine("Full battery!");
            batteryLife.Stop();
            return true;*/
        }
    }
}
