namespace QuickCompareModel.DatabaseDifferences
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class TableSubItemDifferenceList : BaseDifference
    {
        public TableSubItemDifferenceList(bool existsInDatabase1, bool existsInDatabase2)
            : base(existsInDatabase1, existsInDatabase2)
        {
        }

        public List<string> Differences { get; set; } = new List<string>();

        public string ItemType { get; set; }

        public virtual bool IsDifferent => !ExistsInBothDatabases || Differences.Count > 0;

        public string DifferenceList()
        {
            var sb = new StringBuilder();

            if (Differences.Count == 1)
            {
                sb.AppendLine($"- {Differences.Single()}");
            }
            else if (Differences.Count > 1)
            {
                sb.Append($"\r\n{TabIndent}");
                sb.Append(string.Join($"\r\n{TabIndent} - ", Differences.ToArray()));
                sb.Append("\r\n");
            }
            else
            {
                sb.Append("\r\n");
            }

            return sb.ToString();
        }

        public override string ToString() => IsDifferent
            ? !ExistsInBothDatabases ? ExistenceDifference() : DifferenceList()
            : string.Empty;
    }
}
