namespace QuickCompareModel.DatabaseDifferences
{
    using System.Text.RegularExpressions;

    /// <summary>
    /// Model to represent the most basic element and track the existence in both databases.
    /// </summary>
    public class BaseDifference
    {
        public const string TabIndent = "     ";

        public BaseDifference(bool existsInDatabase1, bool existsInDatabase2)
        {
            ExistsInDatabase1 = existsInDatabase1;
            ExistsInDatabase2 = existsInDatabase2;
        }

        public bool ExistsInDatabase1 { get; set; }

        public bool ExistsInDatabase2 { get; set; }

        public bool ExistsInBothDatabases => ExistsInDatabase1 && ExistsInDatabase2;

        public override string ToString() => ExistsInBothDatabases ? string.Empty : $"does not exist in database {(ExistsInDatabase1 ? 2 : 1)}";

        /// <summary>
        /// A helper method to trim whitespace and comments from text for comparison purposes.
        /// </summary>
        /// <param name="definition">Text to modify.</param>
        /// <param name="stripWhiteSpace">Reduces all whitespace to a single character.</param>
        /// <returns>Modified text ready for comparison.</returns>
        public static string CleanDefinitionText(string definition, bool stripWhiteSpace)
        {
            if (string.IsNullOrEmpty(definition))
            {
                return string.Empty;
            }

            var multiLineCommentRegex = new Regex(@"/\*[^*]*\*+([^/*][^*]*\*+)*/");
            definition = multiLineCommentRegex.Replace(definition, " ");

            var inlineCommentRegex = new Regex(@"(--(.*|[\r\n]))");
            definition = inlineCommentRegex.Replace(definition, string.Empty);

            var dboRegex = new Regex(@"(\[dbo\]\.)|(dbo\.)");
            definition = dboRegex.Replace(definition, string.Empty);

            var squareBracketsRegex = new Regex(@"(\[)|(\])");
            definition = squareBracketsRegex.Replace(definition, string.Empty);

            if (stripWhiteSpace)
            {
                var commaWhitespaceRegex = new Regex(@"\s*,\s*");
                definition = commaWhitespaceRegex.Replace(definition, ",");

                var allCommasRegex = new Regex(@"[,]");
                definition = allCommasRegex.Replace(definition, ", ");

                var whitespaceRegex = new Regex(@"[\s]{2,}");
                definition = whitespaceRegex.Replace(definition, " ");
            }

            return definition.Trim();
        }
    }
}
