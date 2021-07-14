namespace QuickCompareModel.DatabaseDifferences
{
    using System.Collections.Generic;
    using System.Text;

    public class TableDifference : BaseDifference
    {
        public TableDifference(bool existsInDatabase1, bool existsInDatabase2)
            : base(existsInDatabase1, existsInDatabase2)
        {
        }

        public Dictionary<string, TableSubItemWithPropertiesDifference> ColumnDifferences { get; set; }
            = new Dictionary<string, TableSubItemWithPropertiesDifference>();

        public Dictionary<string, TableSubItemDifference> RelationshipDifferences { get; set; }
            = new Dictionary<string, TableSubItemDifference>();

        public Dictionary<string, TableSubItemWithPropertiesDifference> IndexDifferences { get; set; }
            = new Dictionary<string, TableSubItemWithPropertiesDifference>();

        public Dictionary<string, TableSubItemDifference> TriggerDifferences { get; set; }
            = new Dictionary<string, TableSubItemDifference>();

        public Dictionary<string, ExtendedPropertyDifference> ExtendedPropertyDifferences { get; set; }
            = new Dictionary<string, ExtendedPropertyDifference>();

        public Dictionary<string, BaseDifference> PermissionDifferences { get; set; }
            = new Dictionary<string, BaseDifference>();

        public bool HasColumnDifferences
        {
            get
            {
                foreach (var column in ColumnDifferences.Values)
                {
                    if (column.IsDifferent)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasRelationshipDifferences
        {
            get
            {
                foreach (var relation in RelationshipDifferences.Values)
                {
                    if (relation.IsDifferent)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasIndexDifferences
        {
            get
            {
                foreach (var index in IndexDifferences.Values)
                {
                    if (index.IsDifferent)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasTriggerDifferences
        {
            get
            {
                foreach (var trigger in TriggerDifferences.Values)
                {
                    if (trigger.IsDifferent)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasPermissionDifferences
        {
            get
            {
                foreach (var permission in PermissionDifferences.Values)
                {
                    if (!permission.ExistsInBothDatabases)
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

        public bool IsDifferent
        {
            get
            {
                return !ExistsInBothDatabases || HasColumnDifferences ||
                    HasRelationshipDifferences || HasIndexDifferences || HasTriggerDifferences ||
                    HasExtendedPropertyDifferences || HasPermissionDifferences;
            }
        }

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

            var sb = new StringBuilder("\r\n");

            if (HasColumnDifferences)
            {
                foreach (var colDiff in ColumnDifferences)
                {
                    if (colDiff.Value.IsDifferent)
                    {
                        sb.Append($"{TabIndent}[{colDiff.Key}] {colDiff.Value}");
                    }
                }
            }

            if (HasTriggerDifferences)
            {
                foreach (var triggerDiff in TriggerDifferences)
                {
                    if (triggerDiff.Value.IsDifferent)
                    {
                        sb.Append($"{TabIndent}Trigger: [{triggerDiff.Key}] {triggerDiff.Value}");
                    }
                }
            }

            if (HasIndexDifferences)
            {
                foreach (var indexDiff in IndexDifferences)
                {
                    if (indexDiff.Value.IsDifferent)
                    {
                        sb.Append($"{TabIndent}{indexDiff.Value.ItemType}: [{indexDiff.Key}] {indexDiff.Value}");
                    }
                }
            }

            if (HasRelationshipDifferences)
            {
                foreach (var relationDiff in RelationshipDifferences)
                {
                    if (relationDiff.Value.IsDifferent)
                    {
                        sb.Append($"{TabIndent}Relation: [{relationDiff.Key}] {relationDiff.Value}");
                    }
                }
            }

            if (HasExtendedPropertyDifferences)
            {
                foreach (var propDiff in ExtendedPropertyDifferences)
                {
                    if (propDiff.Value.IsDifferent)
                    {
                        sb.Append($"{TabIndent}Extended property: [{propDiff.Key}] {propDiff.Value}");
                    }
                }
            }

            if (HasPermissionDifferences)
            {
                foreach (var permissionDiff in PermissionDifferences)
                {
                    if (!permissionDiff.Value.ExistsInBothDatabases)
                    {
                        sb.Append($"{TabIndent}Permission: [{permissionDiff.Key}] {permissionDiff.Value}");
                    }
                }
            }

            return sb.ToString();
        }
    }
}
