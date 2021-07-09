namespace QuickCompareModel.DatabaseDifferences
{
    using System.Collections.Generic;
    using System.Text;

    public class TableSubItemWithPropertiesDifferenceList
        : TableSubItemDifferenceList
    {
        public TableSubItemWithPropertiesDifferenceList(bool existsInDatabase1, bool existsInDatabase2)
            : base(existsInDatabase1, existsInDatabase2)
        {
        }

        public TableSubItemWithPropertiesDifferenceList(bool existsInDatabase1, bool existsInDatabase2, string itemType)
            : base(existsInDatabase1, existsInDatabase2) => this.ItemType = itemType;

        public Dictionary<string, ExtendedPropertyDifference> ExtendedPropertyDifferences { get; set; }
            = new Dictionary<string, ExtendedPropertyDifference>();

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

        public override bool IsDifferent => base.IsDifferent || HasExtendedPropertyDifferences;

        public override string ToString()
        {
            if (!IsDifferent)
            {
                return string.Empty;
            }

            if (!ExistsInBothDatabases)
            {
                return base.ToString();
            }

            var sb = new StringBuilder(DifferenceList());
            if (HasExtendedPropertyDifferences)
            {
                foreach (var diff in ExtendedPropertyDifferences)
                {
                    if (diff.Value.IsDifferent)
                    {
                        sb.AppendFormat(
                            "{0}{0}Extended property: [{1}] - {2}",
                            TabIndent,
                            diff.Key,
                            diff.Value);
                    }
                }
            }

            return sb.ToString();
        }
    }
}
