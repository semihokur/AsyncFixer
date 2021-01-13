using System.Threading.Tasks;

namespace AsyncFixer.Samples
{
    internal class AsyncVoid
    {
        private static async void foo()
        {
            await Task.Delay(300);
            await Task.Delay(100);
        }

        private static bool foo2()
        {
            Task.Delay(300);
            return true;
        }

        private static Task<bool> foo3()
        {
            Task.Delay(300);
            return Task.FromResult(true);
        }

        private static async Task<bool> foo4()
        {
            Task.Delay(300);
            return true;
        }
    }
}