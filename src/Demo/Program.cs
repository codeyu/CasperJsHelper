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
            const string jsPath = "Scripts/screenshot.js";
            var casperJs = new CasperJsHelper.CasperJsHelper("casperjs-1.1.3");
            casperJs.OutputReceived += (sender, e) =>
            {
                Console.WriteLine(e.Data);
            };
            casperJs.Run(string.Format("{0}/{1}", currentPath, jsPath), new []{"codeyu","codeyu.jpg"});
            Console.ReadLine();
        }

        private static string GetExecutingDirectoryName()
        {
            var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
            return Path.GetDirectoryName(location.AbsolutePath);
        }
    }
}
