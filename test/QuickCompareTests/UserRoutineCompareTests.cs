namespace QuickCompareTests
{
    using FluentAssertions;
    using QuickCompareModel.DatabaseSchema;
    using Xunit;

    public class UserRoutineCompareTests
    {
        [Fact]
        public void FunctionMissingFromDatabase1_IsReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var routineName = "Function1";
            builder.Database2.UserRoutines.Add(routineName, new SqlUserRoutine { RoutineType = "function" });

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.FunctionDifferences.Should().ContainKey(routineName);

            var diff = builder.Differences.FunctionDifferences[routineName];
            diff.ExistsInDatabase1.Should().BeFalse();
            diff.ExistsInDatabase2.Should().BeTrue();
            diff.ToString().Should().Be("does not exist in database 1");
        }

        [Fact]
        public void FunctionMissingFromDatabase2_IsReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var routineName = "Function1";
            builder.Database1.UserRoutines.Add(routineName, new SqlUserRoutine { RoutineType = "function" });

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.FunctionDifferences.Should().ContainKey(routineName);

            var diff = builder.Differences.FunctionDifferences[routineName];
            diff.ExistsInDatabase1.Should().BeTrue();
            diff.ExistsInDatabase2.Should().BeFalse();
            diff.ToString().Should().Be("does not exist in database 2");
        }

        [Fact]
        public void FunctionsInBothDatabases_AreNotReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var routineName = "Function1";
            builder.Database1.UserRoutines.Add(routineName, new SqlUserRoutine { RoutineType = "function" });
            builder.Database2.UserRoutines.Add(routineName, new SqlUserRoutine { RoutineType = "function" });

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.FunctionDifferences.Should().ContainKey(routineName);

            var diff = builder.Differences.FunctionDifferences[routineName];
            diff.ExistsInBothDatabases.Should().BeTrue();
            diff.ToString().Should().Be(string.Empty);
        }

        [Fact]
        public void StoredProcedureMissingFromDatabase1_IsReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var routineName = "Procedure1";
            builder.Database2.UserRoutines.Add(routineName, new SqlUserRoutine { RoutineType = "foobar" });

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.StoredProcedureDifferences.Should().ContainKey(routineName);

            var diff = builder.Differences.StoredProcedureDifferences[routineName];
            diff.ExistsInDatabase1.Should().BeFalse();
            diff.ExistsInDatabase2.Should().BeTrue();
            diff.ToString().Should().Be("does not exist in database 1");
        }

        [Fact]
        public void StoredProcedureMissingFromDatabase2_IsReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var routineName = "Procedure1";
            builder.Database1.UserRoutines.Add(routineName, new SqlUserRoutine { RoutineType = "foobar" });

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.StoredProcedureDifferences.Should().ContainKey(routineName);

            var diff = builder.Differences.StoredProcedureDifferences[routineName];
            diff.ExistsInDatabase1.Should().BeTrue();
            diff.ExistsInDatabase2.Should().BeFalse();
            diff.ToString().Should().Be("does not exist in database 2");
        }

        [Fact]
        public void StoredProceduresInBothDatabases_AreNotReported()
        {
            // Arrange
            var builder = TestHelper.GetBasicBuilder();

            var routineName = "Procedure1";
            builder.Database1.UserRoutines.Add(routineName, new SqlUserRoutine { RoutineType = "foobar" });
            builder.Database2.UserRoutines.Add(routineName, new SqlUserRoutine { RoutineType = "foobar" });

            // Act
            builder.BuildDifferences();

            // Assert
            builder.Differences.StoredProcedureDifferences.Should().ContainKey(routineName);

            var diff = builder.Differences.StoredProcedureDifferences[routineName];
            diff.ExistsInBothDatabases.Should().BeTrue();
            diff.ToString().Should().Be(string.Empty);
        }
    }
}
