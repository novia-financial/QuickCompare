namespace QuickCompareTests
{
    using FluentAssertions;
    using QuickCompareModel;
    using Xunit;

    public class DatabaseCompareTests
    {
        /// <summary>
        /// Test to ensure the correct server/database names are derived from the connection string.
        /// </summary>
        [Fact]
        public void Database_FriendlyName_ReturnsAsExpected()
        {
            var expectedResult = "[localhost].[Northwind]";

            new SqlDatabase("Data Source=localhost;Initial Catalog=Northwind;Integrated Security=True")
                .FriendlyName.Should().Be(expectedResult);

            new SqlDatabase("Server=localhost;Database=Northwind;Integrated Security=True")
                .FriendlyName.Should().Be(expectedResult);
        }
    }
}
