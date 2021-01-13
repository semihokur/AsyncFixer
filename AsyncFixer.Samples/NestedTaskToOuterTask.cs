using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncFixer.Samples
{
    class NestedTaskToOuterTask
    {
        async void main()
        {
            await Task.Factory.StartNew(() => foo());
        }

        TaskCanceledException foo()
        {
            return new TaskCanceledException();
        }
    }
}
