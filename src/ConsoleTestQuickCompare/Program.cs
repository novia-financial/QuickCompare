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
                // Get a DifferenceBuilder instance from the DI container
                var provider = GetServiceProvider();
                var builder = provider.GetService<IDifferenceBuilder>();

                // Add (optional) status event handler
                builder.ComparisonStatusChanged += HandleStatusChangeEvent;

                // Generate a report of differences
                builder.BuildDifferences();
                var report = builder.Differences.ToString();

                // Output the report
                Console.WriteLine();
                Console.Write(report);
                Trace.Write(report);
            }
            catch (Exception ex)
            {
                // Display error details to the output
                Console.Write(ex.ToString());
                Trace.Write(ex.ToString());
            }
            finally
            {
                // Wait for user input (rather than just closing the window)
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
