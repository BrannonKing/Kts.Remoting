using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Kts.Remoting.Server
{
    public static class PerIdentityThreadPool
    {
		public static void Queue(string id, int maxThreads, IIdentity identity, Action method)
		{
			// add our method to a queue along with its task
			// signal our threads;
			// if they are all busy make a new one if we are not at max count
			// all threads should check the queue before going idle
		}

		public static void Purge(string id)
		{
			// stop all the threads
			// remove the queue for that id
		}
    }
}
