namespace QuickCompareTests
{
    using FluentAssertions;
    using QuickCompareModel;
    using QuickCompareModel.DatabaseSchema;
    using Xunit;

    public class TableCompareTests
    {
        [Fact]
        public void Database_FriendlyName_ReturnsAsExpected()
        {
            var expectedResult = "[localhost].[Northwind]";

            new SqlDatabase("Data Source=localhost;Initial Catalog=Northwind;Integrated Security=True")
                .FriendlyName.Should().Be(expectedResult);

            new SqlDatabase("Server=localhost;Database=Northwind;Integrated Security=True")
                .FriendlyName.Should().Be(expectedResult);
        }

        [Fact]
        public void TableMissingFromDatabase1_IsReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var tableName = "Table1";
            builder.Database2.Tables.Add(tableName, new SqlTable());

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.TableDifferences.Should().ContainKey(tableName);

            var diff = builder.Differences.TableDifferences[tableName];
            diff.ExistsInDatabase1.Should().BeFalse();
            diff.ExistsInDatabase2.Should().BeTrue();
            diff.ToString().Should().Be("does not exist in database 1");
        }

        [Fact]
        public void TableMissingFromDatabase2_IsReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var tableName = "Table1";
            builder.Database1.Tables.Add(tableName, new SqlTable());

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.TableDifferences.Should().ContainKey(tableName);

            var diff = builder.Differences.TableDifferences[tableName];
            diff.ExistsInDatabase1.Should().BeTrue();
            diff.ExistsInDatabase2.Should().BeFalse();
            diff.ToString().Should().Be("does not exist in database 2");
        }

        [Fact]
        public void TablesInBothDatabases_AreNotReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var tableName = "Table1";
            builder.Database1.Tables.Add(tableName, new SqlTable());
            builder.Database2.Tables.Add(tableName, new SqlTable());

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.TableDifferences.Should().ContainKey(tableName);

            var diff = builder.Differences.TableDifferences[tableName];
            diff.ExistsInBothDatabases.Should().BeTrue();
            diff.ToString().Should().Be(string.Empty);
        }
    }
}
