using System;
using System.Linq;

namespace Task.Api
{
    public class Program
    {
        public static async System.Threading.Tasks.Task Main(string[] args)
        {
            if (args.Length > 0 && args[0].Equals("serve", StringComparison.OrdinalIgnoreCase))
            {
                await ServerHost.RunAsync(args.Skip(1).ToArray());
                return;
            }

            await ServerHost.RunAsync(args);
        }
    }
}
