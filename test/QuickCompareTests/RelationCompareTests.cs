namespace QuickCompareTests
{
    using FluentAssertions;
    using QuickCompareModel.DatabaseSchema;
    using Xunit;

    public class RelationCompareTests
    {
        [Fact]
        public void RelationMissingFromDatabase1_IsReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var tableName = "Table1";
            var relationName = "Relation1";
            builder.Database1.Tables.Add(tableName, new SqlTable());
            builder.Database2.Tables.Add(tableName, new SqlTable());
            builder.Database2.Tables[tableName].Relations.Add(new SqlRelation { RelationName = relationName });

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.TableDifferences[tableName]
                .RelationshipDifferences.Should().ContainKey(relationName);

            var diff = builder.Differences.TableDifferences[tableName].RelationshipDifferences[relationName];
            diff.ExistsInDatabase1.Should().BeFalse();
            diff.ExistsInDatabase2.Should().BeTrue();
            diff.ToString().Should().Be("does not exist in database 1");
        }

        [Fact]
        public void RelationMissingFromDatabase2_IsReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var tableName = "Table1";
            var relationName = "Relation1";
            builder.Database1.Tables.Add(tableName, new SqlTable());
            builder.Database1.Tables[tableName].Relations.Add(new SqlRelation { RelationName = relationName });
            builder.Database2.Tables.Add(tableName, new SqlTable());

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.TableDifferences[tableName]
                .RelationshipDifferences.Should().ContainKey(relationName);

            var diff = builder.Differences.TableDifferences[tableName].RelationshipDifferences[relationName];
            diff.ExistsInDatabase1.Should().BeTrue();
            diff.ExistsInDatabase2.Should().BeFalse();
            diff.ToString().Should().Be("does not exist in database 2");
        }

        [Fact]
        public void RelationsInBothDatabases_AreNotReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var tableName = "Table1";
            var relationName = "Relation1";
            builder.Database1.Tables.Add(tableName, new SqlTable());
            builder.Database1.Tables[tableName].Relations.Add(new SqlRelation { RelationName = relationName });
            builder.Database2.Tables.Add(tableName, new SqlTable());
            builder.Database2.Tables[tableName].Relations.Add(new SqlRelation { RelationName = relationName });

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.TableDifferences[tableName]
                .RelationshipDifferences.Should().ContainKey(relationName);

            var diff = builder.Differences.TableDifferences[tableName].RelationshipDifferences[relationName];
            diff.ExistsInBothDatabases.Should().BeTrue();
            diff.ToString().Should().Be(string.Empty);
        }
    }
}
