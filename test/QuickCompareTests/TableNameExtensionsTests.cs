namespace QuickCompareTests
{
    using FluentAssertions;
    using QuickCompareModel.DatabaseSchema;
    using Xunit;

    public class TableNameExtensionsTests
    {
        private const string ExpectedInput = "[dbo].[Table1]";

        [Fact]
        public void GivenExpectedInput_ReturnsExpectedSchema()
        {
            ExpectedInput.GetSchemaName()
                .Should().Be("dbo");
        }

        [Fact]
        public void GivenExpectedInput_ReturnsExpectedTableName()
        {
            ExpectedInput.GetObjectName()
                .Should().Be("Table1");
        }
    }
}
