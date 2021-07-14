﻿namespace QuickCompareTests
{
    using FluentAssertions;
    using QuickCompareModel.DatabaseSchema;
    using Xunit;

    public class ColumnCompareTests
    {
        [Fact]
        public void ColumnMissingFromDatabase1_IsReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var tableName = "Table1";
            var columnName = "Column1";
            builder.Database1.Tables.Add(tableName, new SqlTable());
            builder.Database2.Tables.Add(tableName, new SqlTable());
            builder.Database2.Tables[tableName].ColumnDetails.Add(new SqlColumnDetail { ColumnName = columnName });

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.TableDifferences[tableName]
                .ColumnDifferences.Should().ContainKey(columnName);

            var diff = builder.Differences.TableDifferences[tableName].ColumnDifferences[columnName];
            diff.ExistsInDatabase1.Should().BeFalse();
            diff.ExistsInDatabase2.Should().BeTrue();
            diff.ToString().Should().Be("does not exist in database 1");
        }

        [Fact]
        public void ColumnMissingFromDatabase2_IsReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var tableName = "Table1";
            var columnName = "Column1";
            builder.Database1.Tables.Add(tableName, new SqlTable());
            builder.Database1.Tables[tableName].ColumnDetails.Add(new SqlColumnDetail { ColumnName = columnName });
            builder.Database2.Tables.Add(tableName, new SqlTable());

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.TableDifferences[tableName]
                .ColumnDifferences.Should().ContainKey(columnName);

            var diff = builder.Differences.TableDifferences[tableName].ColumnDifferences[columnName];
            diff.ExistsInDatabase1.Should().BeTrue();
            diff.ExistsInDatabase2.Should().BeFalse();
            diff.ToString().Should().Be("does not exist in database 2");
        }

        [Fact]
        public void ColumnsInBothDatabases_AreNotReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var tableName = "Table1";
            var columnName = "Column1";
            builder.Database1.Tables.Add(tableName, new SqlTable());
            builder.Database1.Tables[tableName].ColumnDetails.Add(new SqlColumnDetail { ColumnName = columnName });
            builder.Database2.Tables.Add(tableName, new SqlTable());
            builder.Database2.Tables[tableName].ColumnDetails.Add(new SqlColumnDetail { ColumnName = columnName });

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.TableDifferences[tableName]
                .ColumnDifferences.Should().ContainKey(columnName);

            var diff = builder.Differences.TableDifferences[tableName].ColumnDifferences[columnName];
            diff.ExistsInBothDatabases.Should().BeTrue();
            diff.ToString().Should().Be(string.Empty);
        }
    }
}