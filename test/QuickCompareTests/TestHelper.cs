namespace QuickCompareTests
{
    using Microsoft.Extensions.Options;
    using QuickCompareModel;

    public static class TestHelper
    {
        public static DifferenceBuilder GetBasicBuilder()
        {
            var options = GetDefaultOptions();

            var database1 = new SqlDatabase(options.Value.ConnectionString1);
            var database2 = new SqlDatabase(options.Value.ConnectionString2);

            return new DifferenceBuilder(options, database1, database2);
        }

        private static IOptions<QuickCompareOptions> GetDefaultOptions()
        {
            var settings = new QuickCompareOptions
            {
                ConnectionString1 = "Data Source=localhost\\SQLEXPRESS;Initial Catalog=Northwind1;Integrated Security=True",
                ConnectionString2 = "Data Source=localhost\\SQLEXPRESS;Initial Catalog=Northwind2;Integrated Security=True",
            };

            return Options.Create(settings);
        }
    }
}
