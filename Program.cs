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
        }
    }
}