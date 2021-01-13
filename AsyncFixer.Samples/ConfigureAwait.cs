using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace AsyncFixer.Samples
{
    internal class ConfigureAwait
    {
        public async Task foo()
        {
            HttpWebResponse response = null;

            await Task.Delay(100);

            var a = await Task.Run(() => { return 3; });

            using (var stream = response.GetResponseStream())
            {
                using (var reader = new StreamReader(stream))
                {
                    var str = await reader.ReadToEndAsync();
                }
            }
        }
    }
}