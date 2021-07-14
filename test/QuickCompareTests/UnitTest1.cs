namespace QuickCompareTests
{
    using FluentAssertions;
    using QuickCompareModel;
    using Xunit;

    public class UnitTest1
    {
        [Fact]
        public void LoadTableNameQueryFromResource_ReturnsString()
        {
            new SqlDatabase("foobar")
                .LoadQueryFromResource("TableNames")
                .Should().MatchRegex("^[SELECT]");
        }
    }
}
