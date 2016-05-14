using System;
using System.IO;
using System.Reflection;
namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            var currentPath = GetExecutingDirectoryName();
            var jsPath = "Scripts/HelloWorld.js";
            var casperJs = new CasperJsHelper.CasperJsHelper("casperjs-1.1.1");
            casperJs.OutputReceived += (sender, e) =>
            {
                Console.WriteLine(e.Data);
            };
            casperJs.Run(string.Format("{0}/{1}", currentPath, jsPath), null);
            Console.ReadKey();
        }

        private static string GetExecutingDirectoryName()
        {
            var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
            return new FileInfo(location.AbsolutePath).Directory.FullName;
        }
    }
}
