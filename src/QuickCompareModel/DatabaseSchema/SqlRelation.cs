namespace QuickCompareModel.DatabaseSchema
{
    internal class SqlRelation
    {
        public string RELATION_NAME { get; set; }

        public string CHILD_TABLE { get; set; }

        public string CHILD_COLUMNS { get; set; }

        public string UNIQUE_CONSTRAINT_NAME { get; set; }

        public string PARENT_TABLE { get; set; }

        public string PARENT_COLUMNS { get; set; }

        public string UPDATE_RULE { get; set; }

        public string DELETE_RULE { get; set; }
    }
}
