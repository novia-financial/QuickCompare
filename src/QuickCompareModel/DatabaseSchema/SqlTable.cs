namespace QuickCompareModel.DatabaseSchema
{
    using System.Collections.Generic;

    internal class SqlTable
    {
        public List<SqlColumnDetail> ColumnDetails { get; set; } = new List<SqlColumnDetail>();

        public List<SqlRelation> Relations { get; set; } = new List<SqlRelation>();

        public List<SqlIndex> Indexes { get; set; } = new List<SqlIndex>();

        public List<SqlTrigger> Triggers { get; set; } = new List<SqlTrigger>();

        public bool ColumnHasUniqueIndex(string columnName)
        {
            foreach (var index in Indexes)
            {
                if (index.UNIQUE && index.COLUMNS.ContainsKey(columnName) && index.COLUMNS.Count == 1)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
