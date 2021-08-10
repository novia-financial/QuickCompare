namespace QuickCompareTests
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
            diff.ToString().Should().Be("does not exist in database 1\r\n");
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
            diff.ToString().Should().Be("does not exist in database 2\r\n");
        }

        [Fact]
        public void ColumnsInBothDatabases_AreNotReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.TableDifferences[tableName]
                .ColumnDifferences.Should().ContainKey(columnName);

            var diff = builder.Differences.TableDifferences[tableName].ColumnDifferences[columnName];
            diff.ExistsInBothDatabases.Should().BeTrue();
            diff.ToString().Should().Be(string.Empty);
        }

        [Fact]
        public void OrdinalPositionDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].OrdinalPosition = 2;
            builder.Database2.Tables[tableName].ColumnDetails[0].OrdinalPosition = 1;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("ordinal position");
        }

        [Fact]
        public void ColumnDefaultDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].ColumnDefault = "foo";
            builder.Database2.Tables[tableName].ColumnDetails[0].ColumnDefault = "bar";

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("default value");
        }

        [Fact]
        public void IsNullableDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].IsNullable = true;
            builder.Database2.Tables[tableName].ColumnDetails[0].IsNullable = false;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("is allowed null");
            diff1.ToString().Should().Contain("is not allowed null");
        }

        [Fact]
        public void DataTypeDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].DataType = "foo";
            builder.Database2.Tables[tableName].ColumnDetails[0].DataType = "bar";

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("data type");
        }

        [Fact]
        public void CharacterMaxLengthDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].CharacterMaximumLength = 10;
            builder.Database2.Tables[tableName].ColumnDetails[0].CharacterMaximumLength = 20;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("max length");
        }

        [Fact]
        public void CharacterOctetLengthDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].CharacterOctetLength = 2;
            builder.Database2.Tables[tableName].ColumnDetails[0].CharacterOctetLength = 1;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("character octet length");
        }

        [Fact]
        public void NumericPrecisionDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].NumericPrecision = 2;
            builder.Database2.Tables[tableName].ColumnDetails[0].NumericPrecision = 1;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("numeric precision");
        }

        [Fact]
        public void NumericPrecisionRadixDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].NumericPrecisionRadix = 2;
            builder.Database2.Tables[tableName].ColumnDetails[0].NumericPrecisionRadix = 1;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("numeric precision");
        }

        [Fact]
        public void NumericScaleDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].NumericScale = 2;
            builder.Database2.Tables[tableName].ColumnDetails[0].NumericScale = 1;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("numeric scale");
        }

        [Fact]
        public void DateTimePrecisionDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].DatetimePrecision = 2;
            builder.Database2.Tables[tableName].ColumnDetails[0].DatetimePrecision = 1;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("datetime precision");
        }

        [Fact]
        public void CharacterSetNameDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].CharacterSetName = "foo";
            builder.Database2.Tables[tableName].ColumnDetails[0].CharacterSetName = "bar";

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("character set");
        }

        [Fact]
        public void CollationNameDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].CollationName = "foo";
            builder.Database2.Tables[tableName].ColumnDetails[0].CollationName = "bar";

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("collation");
        }

        [Fact]
        public void IsFullTextIndexedDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].IsFullTextIndexed = true;
            builder.Database2.Tables[tableName].ColumnDetails[0].IsFullTextIndexed = false;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("is full-text indexed");
            diff1.ToString().Should().Contain("is not full-text indexed");
        }

        [Fact]
        public void IsComputedDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].IsComputed = true;
            builder.Database2.Tables[tableName].ColumnDetails[0].IsComputed = false;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("is computed");
            diff1.ToString().Should().Contain("is not computed");
        }

        [Fact]
        public void IsIdentityDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].IsIdentity = true;
            builder.Database2.Tables[tableName].ColumnDetails[0].IsIdentity = false;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("is an identity");
            diff1.ToString().Should().Contain("is not an identity");
        }

        [Fact]
        public void IdentitySeedDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].IsIdentity = true;
            builder.Database2.Tables[tableName].ColumnDetails[0].IsIdentity = true;
            builder.Database1.Tables[tableName].ColumnDetails[0].IdentitySeed = 2;
            builder.Database2.Tables[tableName].ColumnDetails[0].IdentitySeed = 1;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("identity seed");
        }

        [Fact]
        public void IdentityIncrementDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].IsIdentity = true;
            builder.Database2.Tables[tableName].ColumnDetails[0].IsIdentity = true;
            builder.Database1.Tables[tableName].ColumnDetails[0].IdentityIncrement = 2;
            builder.Database2.Tables[tableName].ColumnDetails[0].IdentityIncrement = 1;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("identity increment");
        }

        [Fact]
        public void IsSparseDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].IsSparse = true;
            builder.Database2.Tables[tableName].ColumnDetails[0].IsSparse = false;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("is sparse");
            diff1.ToString().Should().Contain("is not sparse");
        }

        [Fact]
        public void IsColumnSetDifference_IsReported()
        {
            // Arrange
            var tableName = "Table1";
            var columnName = "Column1";
            var builder = TestHelper.GetBuilderWithSingleTable(tableName, columnName);

            builder.Database1.Tables[tableName].ColumnDetails[0].IsColumnSet = true;
            builder.Database2.Tables[tableName].ColumnDetails[0].IsColumnSet = false;

            // Act
            builder.BuildDifferences();

            // Assert
            var tableDiff = builder.Differences.TableDifferences[tableName];
            tableDiff.ColumnDifferences.Should().ContainKey(columnName);

            var diff1 = tableDiff.ColumnDifferences[columnName];
            diff1.ExistsInBothDatabases.Should().BeTrue();
            diff1.Differences.Count.Should().Be(1);
            diff1.ToString().Should().Contain("is a column-set");
            diff1.ToString().Should().Contain("is not a column-set");
        }
    }
}
