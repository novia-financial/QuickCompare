namespace QuickCompareModel.DatabaseSchema
{
    public class SqlPermission
    {
        public string RoleName { get; set; }

        public string UserName { get; set; }

        public string PermissionType { get; set; }

        public string PermissionState { get; set; }

        public string ObjectType { get; set; }

        public string ObjectName { get; set; }

        public string ColumnName { get; set; }

        public string FullId => $"[{RoleName}].[{UserName}].[{PermissionType}].[{PermissionState}].[{ObjectType}].[{ObjectName}].[{ColumnName}]";

        public PermissionObjectType Type => ObjectType switch
        {
            "SQL_STORED_PROCEDURE" => PermissionObjectType.SqlStoredProcedure,
            "USER_TABLE" => PermissionObjectType.UserTable,
            "SYNONYM" => PermissionObjectType.Synonym,
            "VIEW" => PermissionObjectType.View,
            "SQL_SCALAR_FUNCTION" => PermissionObjectType.SqlFunction,
            "SQL_TABLE_VALUED_FUNCTION" => PermissionObjectType.SqlFunction,
            "SQL_INLINE_TABLE_VALUED_FUNCTION" => PermissionObjectType.SqlFunction,
            _ => PermissionObjectType.Database,
        };

        public override string ToString() => PermissionType == "REFERENCES"
                ? $"REFERENCES column: [{ColumnName}] {(PermissionState == "GRANT" ? string.Empty : "DENIED ")}for {(string.IsNullOrEmpty(RoleName) ? "user" : "role")}: [{(string.IsNullOrEmpty(RoleName) ? UserName : RoleName)}]"
                : $"[{PermissionType}] {(PermissionState == "GRANT" ? string.Empty : "DENIED ")}for {(string.IsNullOrEmpty(RoleName) ? "user" : "role")}: [{(string.IsNullOrEmpty(RoleName) ? UserName : RoleName)}]";
    }

    public enum PermissionObjectType
    {
        Database,
        SqlStoredProcedure,
        SqlFunction,
        Synonym,
        UserTable,
        View,
    }
}
