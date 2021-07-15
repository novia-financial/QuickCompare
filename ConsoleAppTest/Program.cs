namespace ConsoleAppTest
{
    using System;
    using Microsoft.Extensions.Options;
    using QuickCompareModel;

    public class Program
    {
        static void Main(string[] args)
        {
            var settings = new QuickCompareOptions
            {
                ConnectionString1 = "Data Source=localhost\\SQLEXPRESS;Initial Catalog=Database1;Integrated Security=True",
                ConnectionString2 = "Data Source=localhost\\SQLEXPRESS;Initial Catalog=Database2;Integrated Security=True",
            };

            var builder = new DifferenceBuilder(Options.Create(settings));
            builder.BuildDifferences();

            Console.Write(builder.Differences.ToString());
            Console.ReadKey();
        }
    }
}
