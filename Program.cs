using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmazoomDebug
{
    partial class Program
    {

        static void Main()
        {
            //List<Products> testProduct = new List<Products>() { new Products("soap", "sp1", new Coordinate(7, 1), 2.5, 0.5, 1) };
            //Orders testOrder = new Orders("order1",testProduct, "poom", false);

            // initializes the warehouse
            Warehouse testWarehouse = new Warehouse();

            List<Jobs> testJob = new List<Jobs>() { new Jobs(new Products("soap", "sp1", new Coordinate(1, 1), 2.5, 0.5, 1), "order1", false, true), new Jobs(new Products("book", "bk1", new Coordinate(3, 1), 2.5, 0.5, 1), "order1", false, true) };
            Jobs additional = new Jobs(new Products("soap", "sp1", new Coordinate(5, 1), 4, 0.5, 1), "order1", false, true);
            Robot test1 = new Robot("id1", new Battery(100), new Coordinate(4, 1));
            
            /*/// test creating a robot task that runs forever and wait for new tasks!!!
            /// Initializing the robot. YAS MAN! I figured it out
            Task testTask1 = Task.Run(() => test1.Deploy());

            //Thread.Sleep(5000);
            Console.WriteLine("adding additional job to robot 1 throught CPU.");
            test1.AddJob(additional);
            test1.AddJob(testJob[0]);
            test1.AddJob(testJob[1]);

            /// wait so that the Main() doesnt end and run forever
            testTask1.Wait();*/
        }
    }
}