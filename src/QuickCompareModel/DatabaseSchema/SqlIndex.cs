namespace QuickCompareModel.DatabaseSchema
{
    using System.Collections.Generic;
    using System.Text;

    internal class SqlIndex
    {
        public bool IS_PRIMARY_KEY { get; set; }

        public string TABLE_NAME { get; set; }

        public string INDEX_NAME { get; set; }

        public bool CLUSTERED { get; set; }

        public bool UNIQUE { get; set; }

        public bool IS_UNIQUE_KEY { get; set; }

        public Dictionary<int, KeyValuePair<string, bool>> INDEX_COLUMNS { get; set; }

        public string FILEGROUP { get; set; }

        public Dictionary<string, bool> COLUMNS { get; set; } = new Dictionary<string, bool>();

        public Dictionary<string, bool> INCLUDED_COLUMNS { get; set; } = new Dictionary<string, bool>();

        public string FULL_ID => $"[{TABLE_NAME}].[{INDEX_NAME}]";

        public string COLUMNS_ToString => FlagListToString(COLUMNS);

        public string INCLUDED_COLUMNS_ToString => FlagListToString(INCLUDED_COLUMNS);

        public string ITEM_TYPE => !IS_PRIMARY_KEY ? IS_UNIQUE_KEY ? "Unique key" : "Index" : "Primary key";

        public void SetColumnsFromString(string value)
        {
            var columnNames = value.Split(',');
            foreach (var columnName in columnNames)
            {
                if (columnName.IndexOf("(-)") > 0)
                {
                    COLUMNS.Add(columnName.Replace("(-)", "").Trim(), false);
                }
                else
                {
                    COLUMNS.Add(columnName.Trim(), true);
                }
            }
        }

        private static string FlagListToString(Dictionary<string, bool> flagList)
        {
            if (flagList == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var pair in flagList)
            {
                sb.AppendLine($"{pair.Key}, {pair.Value}");
            }

            return sb.ToString();
        }
    }
}
