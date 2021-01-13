using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncFixer.Samples
{
    internal class BlockingCallInsideAsync
    {
		private async void Test1Async()
		{
			await Task.Delay(1);
		}

		private async Task Test2Async(object obj, EventArgs a)
		{
			await Task.Delay(1);
		}

		private async void Test3Async()
		{
			await Task.Delay(1);
			await Task.Delay(1);
		}

		private async void Test4Async(object obj)
		{
			await Task.Delay(1);
			await Task.Delay(1);
		}

		public static async void foo1(object arg)
        {
            var t = Task.Delay(2000);
            await t.ConfigureAwait(false);

            Thread.Sleep(100);

            await foo2();
        }

        public static async Task foo2()
        {
            StreamReader reader = null;

            var str = reader.ReadToEnd();

            var b = await GetRequestStreamAsync();
        }

        public static async Task foo3()
        {
            var task = GetRequestStreamAsync();
            await task;

            var stream = task.Result;
        }

        public static Task<Stream> GetRequestStreamAsync()
        {
            return GetRequestStreamAsync();
        }

        public static async Task<int> GetRequestStreamAsync(int b)
        {
            //int c = await Task.Run(() => { return 3; });
            if (b > 5)
            {
                return 3;
            }
            return await GetRequestStreamAsync(b);
        }

        public async Task<int> boo(int b)
        {
            if (b > 5)
            {
                return await Task.Run(() => { return 3; });
            }
            return await boo(b);
        }

        public async Task<int> RequestAsync(int b)
        {
            using (new StreamReader(""))
            {
                return await RequestAsync(b).ConfigureAwait(false);
            }
        }

        protected async void OnInitialize()
        {
            try
            {
                foo1();
                await Task.Delay(100);
            }
            catch (Exception)
            {
            }
            finally
            {
            }
        }

        private void foo1()
        { 
        }

        public async Task<int> CreateTablesAsync(params Type[] types)
        {
            return await Task.Factory.StartNew(() => { return 3; });
        }

        public static async Task foo3(int i)
        {
            if (i > 2)
            {
                await Task.Delay(2000);
            }
        }

        public static async Task foo4(int i)
        {
            if (i > 2)
            {
                await Task.Delay(3000);
                await Task.Delay(2000);
            }
        }


        public async Task Test1()
        {
            var str = new MemoryStream();
            str.Write(new byte[0], 0, 0);
            Foo(0);
            Foo(0);
            var a = FooAsync(0).Result;
            Bar(0);
            Bar(0);
        }

        public void Foo(int bar)
        {
        }

        [Obsolete("asdasdasd")]
        public async Task<int> FooAsync(int baz)
        {
            return baz;
        }

        public void Bar(int bar)
        {
        }

        public async Task BarAsync(string baz)
        {
        }
    }
}