using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncFixer.Samples
{
    class NestedTaskToOuterTask
    {
        // nested task can be unwrapped and awaited.
        // For this scenario, overloads of Task.Run are provided
        // to accept async functions and automatically unwrap the nested task

        async Task foo()
        {
            Console.WriteLine("Hello");
            await Task.Factory.StartNew(() => Task.Delay(1000));
            Console.WriteLine("World");
        }
    }
}
