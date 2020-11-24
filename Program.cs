using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

/// <summary>
/// You need to install Google.Cloud.Firestore NuGet Packages. Go to Project >> Manage NuGet Packages >> search for Google.Cloud.Firestore
/// </summary>
namespace AmazoomDebug
{
    class Program
    {
        static void Main()
        {
            // initializes the warehouse
            Warehouse testWarehouse = new Warehouse();
            testWarehouse.Deploy();

            // Initializing Cloud Firestore
            

            // Fetch all product data from Cloud Firestore
            //FetchData().Wait();
            //AddProductToFirebase().Wait();




            //List<Products> testProduct = new List<Products>() { new Products("soap", "sp1", new Coordinate(7, 1), 2.5, 0.5, 1) };
            //Orders testOrder = new Orders("order1",testProduct, "poom", false);

            /*List<Jobs> testJob = new List<Jobs>() { new Jobs(new Products("soap", "sp1", new Coordinate(1, 1), 2.5, 0.5, 1), "order1", false, true), new Jobs(new Products("book", "bk1", new Coordinate(3, 1), 2.5, 0.5, 1), "order1", false, true) };
            Jobs additional = new Jobs(new Products("soap", "sp1", new Coordinate(5, 1), 4, 0.5, 1), "order1", false, true);
            Robot test1 = new Robot("id1", new Battery(100), new Coordinate(4, 1));*/

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

        /// <summary>
        /// Fetch data from "All products" collection on Cloud Firestore
        /// </summary>
        /// <returns> it is an asynchronous method that wait for the fetching operation to complete before continueuing</returns>
        /*static async Task FetchData()
        {
            try
            {
                Query allProducts = database.Collection("All products");
                QuerySnapshot fetchedData = await allProducts.GetSnapshotAsync();

                foreach (DocumentSnapshot productInfo in fetchedData)
                {
                    Dictionary<string, Object> prodDetail = productInfo.ToDictionary();

                    // Creating Product object for all of the documents on Cloud Firestore
                    Warehouse.allProducts.Add(new Products(
                        prodDetail["name"].ToString(),
                        productInfo.Id,     // TODO: Randomize distribute and storage, might need to move this class into the Warehouse class
                        Convert.ToDouble(prodDetail["weight"]),
                        Convert.ToDouble(prodDetail["volume"]),
                        Convert.ToInt32(prodDetail["inStock"]),
                        Convert.ToDouble(prodDetail["price"])));
                }

                foreach (var element in Warehouse.allProducts)
                {
                    Console.WriteLine(element.ProductID);
                }
            }
            catch (Exception error)
            {
                Console.WriteLine(error);
            }
        }*/

        /*static async Task AddProductToFirebase()
        {
            try
            {
                List<Products> newProducts = new List<Products>()
                {
                    new Products("TV", "1", 12.0, 0.373, 40, 5999.0),
                    new Products("Sofa", "2", 30.0, 1.293, 40, 1250.0),
                    new Products("Book", "3",0.2, 0.005, 40, 12.0),
                    new Products("Desk", "4", 22.1, 1.1, 40, 70.0),
                    new Products("Phone", "5", 0.6, 0.001, 40, 1299.0),
                    new Products("Bed", "6", 15, 0.73, 40, 199.0),
                };
                CollectionReference addingProd = database.Collection("All products");

                // Randomize Storage


                foreach (var prod in newProducts)
                {
                    Dictionary<string, Object> conversion = new Dictionary<string, object>();
                    conversion.Add("coordinate", prod.CoordToArray());
                    conversion.Add("inStock", prod.InStock);
                    conversion.Add("name", prod.ProductName);
                    conversion.Add("price", prod.Price);
                    conversion.Add("volume", prod.Volume);
                    conversion.Add("weight", prod.Weight);

                    await addingProd.AddAsync(conversion);
                }

            }
            catch (Exception error)
            {
                Console.WriteLine(error);
            }
        }*/
    }
}