namespace QuickCompareModel.DatabaseDifferences
{
    using System.Collections.Generic;
    using System.Text;

    public class DatabaseObjectDifferenceList : BaseDifference
    {
        public DatabaseObjectDifferenceList(bool existsInDatabase1, bool existsInDatabase2)
            : base(existsInDatabase1, existsInDatabase2)
        {
        }

        public string ObjectDefinition1 { get; set; }

        public string ObjectDefinition2 { get; set; }

        public Dictionary<string, ExtendedPropertyDifference> ExtendedPropertyDifferences { get; set; }
            = new Dictionary<string, ExtendedPropertyDifference>();

        public Dictionary<string, BaseDifference> PermissionDifferences { get; set; }
            = new Dictionary<string, BaseDifference>();

        public bool DefinitionsAreDifferent => CleanDefinitionText(ObjectDefinition1, true) != CleanDefinitionText(ObjectDefinition2, true);

        public bool HasPermissionDifferences
        {
            get
            {
                foreach (var prop in ExtendedPropertyDifferences.Values)
                {
                    if (prop.IsDifferent)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasExtendedPropertyDifferences
        {
            get
            {
                foreach (var prop in ExtendedPropertyDifferences.Values)
                {
                    if (prop.IsDifferent)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsDifferent => !ExistsInBothDatabases || DefinitionsAreDifferent || HasPermissionDifferences || HasExtendedPropertyDifferences;

        public override string ToString()
        {
            if (!IsDifferent)
            {
                return string.Empty;
            }

            var sb = new StringBuilder("\r\n");

            if (DefinitionsAreDifferent)
            {
                sb.AppendLine($"{TabIndent}Definitions are different");
            }

            if (HasExtendedPropertyDifferences)
            {
                foreach (var diff in ExtendedPropertyDifferences)
                {
                    if (diff.Value.IsDifferent)
                    {
                        sb.Append($"{TabIndent}Extended property: [{diff.Key}] {diff.Value}");
                    }
                }
            }

            if (HasPermissionDifferences)
            {
                foreach (var diff in PermissionDifferences)
                {
                    if (!diff.Value.ExistsInBothDatabases)
                    {
                        sb.Append($"{TabIndent}Permission: [{diff.Key}] {diff.Value}");
                    }
                }
            }

            return sb.ToString();
        }
    }
}
