namespace QuickCompareModel.DatabaseSchema
{
    internal class SqlPermission
    {
        public string ROLE_NAME { get; set; }

        public string USER_NAME { get; set; }

        public string PERMISSION_TYPE { get; set; }

        public string PERMISSION_STATE { get; set; }

        public string OBJECT_TYPE { get; set; }

        public string OBJECT_NAME { get; set; }

        public string COLUMN_NAME { get; set; }

        public string FULL_ID => !string.IsNullOrEmpty(ROLE_NAME)
                    ? $"[{ROLE_NAME}].[].[{PERMISSION_TYPE}].[{PERMISSION_STATE}].[{OBJECT_TYPE}].[{OBJECT_NAME}].[{COLUMN_NAME}]"
                    : $"[].[{USER_NAME}].[{PERMISSION_TYPE}].[{PERMISSION_STATE}].[{OBJECT_TYPE}].[{OBJECT_NAME}].[{COLUMN_NAME}]";

        public PERMISSION_OBJECT_TYPE TYPE => OBJECT_TYPE switch
        {
            "SQL_STORED_PROCEDURE" => PERMISSION_OBJECT_TYPE.SQL_STORED_PROCEDURE,
            "USER_TABLE" => PERMISSION_OBJECT_TYPE.USER_TABLE,
            "SYNONYM" => PERMISSION_OBJECT_TYPE.SYNONYM,
            "VIEW" => PERMISSION_OBJECT_TYPE.VIEW,
            "SQL_SCALAR_FUNCTION" => PERMISSION_OBJECT_TYPE.SQL_FUNCTION,
            "SQL_TABLE_VALUED_FUNCTION" => PERMISSION_OBJECT_TYPE.SQL_FUNCTION,
            "SQL_INLINE_TABLE_VALUED_FUNCTION" => PERMISSION_OBJECT_TYPE.SQL_FUNCTION,
            _ => PERMISSION_OBJECT_TYPE.DATABASE,
        };

        public override string ToString() => PERMISSION_TYPE == "REFERENCES"
                ? string.Format(
                    "REFERENCES column: [{0}] {1}for {2}: [{3}]",
                    COLUMN_NAME,
                    PERMISSION_STATE == "GRANT" ? string.Empty : "DENIED ",
                    string.IsNullOrEmpty(ROLE_NAME) ? "user" : "role",
                    string.IsNullOrEmpty(ROLE_NAME) ? USER_NAME : ROLE_NAME)
                : string.Format(
                    "[{0}] {1}for {2}: [{3}]",
                    PERMISSION_TYPE,
                    PERMISSION_STATE == "GRANT" ? string.Empty : "DENIED ",
                    string.IsNullOrEmpty(ROLE_NAME) ? "user" : "role",
                    string.IsNullOrEmpty(ROLE_NAME) ? USER_NAME : ROLE_NAME);
    }

    internal enum PERMISSION_OBJECT_TYPE
    {
        DATABASE,
        SQL_STORED_PROCEDURE,
        SQL_FUNCTION,
        SYNONYM,
        USER_TABLE,
        VIEW,
    }
}
