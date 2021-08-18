namespace TestQuickCompare
{
    using System;
    using System.Diagnostics;
    using Microsoft.Extensions.DependencyInjection;
    using QuickCompareModel;

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var provider = GetServiceProvider();
                var builder = provider.GetService<IDifferenceBuilder>();

                builder.ComparisonStatusChanged += HandleStatusChangeEvent; // (optional status handler)

                // Generate report
                builder.BuildDifferences();
                var report = builder.Differences.ToString();

                Console.WriteLine("\r\n--------------------------------");
                Console.Write(report);
                Trace.Write(report);
            }
            catch (Exception ex)
            {
                Console.Write(ex.ToString());
                Trace.Write(ex.ToString());
            }
            finally
            {
                Console.ReadKey();
            }
        }

        /// <summary> Gets the <see cref="IServiceProvider"/> DI container configured in <see cref="Startup"/>. </summary>
        /// <returns> An instance of <see cref="IServiceProvider"/>. </returns>
        private static IServiceProvider GetServiceProvider()
        {
            var services = new ServiceCollection();
            new Startup().ConfigureServices(services);
            return services.BuildServiceProvider();
        }

        private static void HandleStatusChangeEvent(object sender, StatusChangedEventArgs e)
        {
            // Write each status event to the same line (padded right to clear previous status)
            Console.Write($"\r{e.StatusMessage,-50}");

            // Write to trace for debugging
            Trace.WriteLine(e.StatusMessage);
        }
    }
}
