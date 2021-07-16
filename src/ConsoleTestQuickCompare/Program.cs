﻿namespace TestQuickCompare
{
    using System;
    using System.Diagnostics;
    using Microsoft.Extensions.DependencyInjection;
    using QuickCompareModel;

    class Program
    {
        static void Main(string[] args)
        {
            // Get a DifferenceBuilder instance from the DI container
            var provider = GetServiceProvider();
            var builder = provider.GetService<IDifferenceBuilder>();

            // Generate a report of differences
            builder.BuildDifferences();
            var report = builder.Differences.ToString();

            // Output the report to the console and the trace
            Console.Write(report);
            Trace.Write(report);

            // Wait for user input (rather than just closing the window)
            Console.ReadKey();
        }

        /// <summary> Gets the <see cref="IServiceProvider"/> DI container configured in <see cref="Startup"/>. </summary>
        /// <returns> An instance of <see cref="IServiceProvider"/>. </returns>
        private static IServiceProvider GetServiceProvider()
        {
            var services = new ServiceCollection();
            new Startup().ConfigureServices(services);
            return services.BuildServiceProvider();
        }
    }
}
