using System.IO;
using System.Threading.Tasks;

namespace AsyncFixer.Samples
{
    internal class AsyncCallInsideUsingBlock
    {
        public static void foo()
        {
            using (var stream = new FileStream("", FileMode.Open))
            {

                stream.WriteAsync(new byte[] {}, 0, 0);
            }

            Task t;
            Task<int> t2 = null;
            t = t2;

            t.GetAwaiter().GetResult();
            
        }

        public static void boo()
        {
            var newStream = new FileStream("", FileMode.Create);

            using (var stream = new FileStream("existing", FileMode.Open))
            {
                newStream.CopyToAsync(stream);
            }
        }
    }
}