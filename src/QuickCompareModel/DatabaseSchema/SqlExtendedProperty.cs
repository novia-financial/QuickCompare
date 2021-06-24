namespace QuickCompareModel.DatabaseSchema
{
    internal class SqlExtendedProperty
    {
        public string PROPERTY_TYPE { get; set; }

        public string OBJECT_NAME { get; set; }

        public string COLUMN_NAME { get; set; }

        public string PROPERTY_NAME { get; set; }

        public string PROPERTY_VALUE { get; set; }

        public string TABLE_NAME { get; set; }

        public string INDEX_NAME { get; set; }

        public string FULL_ID => !string.IsNullOrEmpty(OBJECT_NAME)
                    ? string.IsNullOrEmpty(COLUMN_NAME)
                        ? $"[{OBJECT_NAME}].[{PROPERTY_NAME}].[{TYPE}]"
                        : $"[{OBJECT_NAME}].[{PROPERTY_NAME}].[{COLUMN_NAME}].[{TYPE}]"
                    : PROPERTY_NAME;

        public PROPERTY_OBJECT_TYPE TYPE => PROPERTY_TYPE != "INDEX"
                    ? !string.IsNullOrEmpty(TABLE_NAME)
                        ? string.IsNullOrEmpty(COLUMN_NAME) ? PROPERTY_OBJECT_TYPE.TABLE : PROPERTY_OBJECT_TYPE.TABLE_COLUMN
                        : PROPERTY_TYPE != "DATABASE"
                            ? string.IsNullOrEmpty(COLUMN_NAME) ? PROPERTY_OBJECT_TYPE.ROUTINE : PROPERTY_OBJECT_TYPE.ROUTINE_COLUMN
                            : PROPERTY_OBJECT_TYPE.DATABASE
                    : PROPERTY_OBJECT_TYPE.INDEX;
    }

    internal enum PROPERTY_OBJECT_TYPE
    {
        DATABASE,
        ROUTINE,
        ROUTINE_COLUMN,
        TABLE,
        TABLE_COLUMN,
        INDEX,
    }
}
