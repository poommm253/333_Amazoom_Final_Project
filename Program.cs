/// <summary>
/// You need to install Google.Cloud.Firestore NuGet Packages. Go to Project >> Manage NuGet Packages >> search for Google.Cloud.Firestore
/// Main method for instantiating the Warehouse object and deploying the warehouse
/// </summary>
namespace AmazoomDebug
{
    class Program
    {
        static void Main()
        {
            // initializes the warehouse
            Warehouse Amazoom1 = new Warehouse();
            Amazoom1.Deploy();
        }
    }
}