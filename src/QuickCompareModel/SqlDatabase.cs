namespace QuickCompareModel
{
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.IO;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using QuickCompareModel.DatabaseSchema;

    /// <summary>
    /// Class for running database queries and building lists that detail the content of the database schema.
    /// </summary>
    public class SqlDatabase
    {
        private readonly string connectionString;
        private readonly QuickCompareOptions options;

        /// <summary>
        /// Initialises a new instance of the <see cref="SqlDatabase"/> class with a connection string and setting options.
        /// </summary>
        /// <param name="connectionString">The database connection string for the current instance being inspected.</param>
        /// <param name="options">Collection of configuration settings.</param>
        public SqlDatabase(string connectionString, QuickCompareOptions options)
        {
            this.connectionString = connectionString;
            this.options = options;
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="SqlDatabase"/> class with a connection string.
        /// </summary>
        /// <param name="connectionString">The database connection string for the current instance being inspected.</param>
        public SqlDatabase(string connectionString)
            : this(connectionString, new QuickCompareOptions())
        {
        }

        /// <summary>
        /// Friendly name for the database instance, including both server name and database name.
        /// </summary>
        public string FriendlyName
        {
            get
            {
                var builder = new SqlConnectionStringBuilder(this.connectionString);
                return $"[{builder.DataSource}].[{builder.InitialCatalog}]";
            }
        }

        /// <summary> Gets or sets a list of <see cref="SqlTable"/> instances, indexed by table name. </summary>
        public Dictionary<string, SqlTable> Tables { get; set; } = new Dictionary<string, SqlTable>();

        /// <summary> Gets or sets a list of database views, indexed by view name. </summary>
        public Dictionary<string, string> Views { get; set; } = new Dictionary<string, string>();

        /// <summary> Gets or sets a list of SQL synonyms, indexed by synonym name. </summary>
        public Dictionary<string, string> Synonyms { get; set; } = new Dictionary<string, string>();

        /// <summary> Gets or sets a list of <see cref="SqlUserRoutine"/> instances, indexed by routine name. </summary>
        /// <remarks> User routines include views, functions and stored procedures. </remarks>
        public Dictionary<string, SqlUserRoutine> UserRoutines { get; set; } = new Dictionary<string, SqlUserRoutine>();

        /// <summary> Gets or sets a list of <see cref="SqlPermission"/> instances, for both roles and users. </summary>
        public List<SqlPermission> Permissions { get; set; } = new List<SqlPermission>();

        /// <summary> Gets or sets a list of <see cref="SqlExtendedProperty"/> instances for the database itself. </summary>
        public List<SqlExtendedProperty> ExtendedProperties { get; set; } = new List<SqlExtendedProperty>();

        /// <summary>
        /// Populate the models based on the supplied connection string.
        /// </summary>
        public void PopulateSchemaModel()
        {
            using var connection = new SqlConnection(this.connectionString);
            LoadTableNames(connection);

            if (options.CompareIndexes)
            {
                foreach (var table in Tables.Keys)
                {
                    LoadIndexes(connection, table);

                    foreach (var index in Tables[table].Indexes)
                    {
                        LoadIncludedColumnsForIndex(connection, index);
                    }
                }
            }

            LoadRelations(connection);
            LoadColumnDetails(connection);

            if (options.ComparePermissions)
            {
                LoadRolePermissions(connection);
                LoadUserPermissions(connection);
            }

            if (options.CompareProperties)
            {
                LoadExtendedProperties(connection);
            }

            if (options.CompareTriggers)
            {
                LoadTriggers(connection);
            }

            if (options.CompareSynonyms)
            {
                LoadSynonyms(connection);
            }

            if (options.CompareObjects)
            {
                LoadViews(connection);
                LoadUserRoutines(connection);
                LoadUserRoutineDefinitions(connection);
            }
        }

        /// <summary>
        /// Helper method to return embedded SQL resource by filename.
        /// </summary>
        /// <param name="queryName">Name of the SQL file without the extension.</param>
        /// <returns>SQL query text.</returns>
        public string LoadQueryFromResource(string queryName)
        {
            var resourceName = $"{nameof(QuickCompareModel)}.{nameof(DatabaseSchema)}.Queries.{queryName}.sql";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            return stream != null ? new StreamReader(stream).ReadToEnd() : string.Empty;
        }

        #region Load methods

        private void LoadTableNames(SqlConnection connection)
        {
            using var command = new SqlCommand(LoadQueryFromResource("TableNames"), connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                Tables.Add(dr.GetString(0), new SqlTable());
            }
        }

        private void LoadIndexes(SqlConnection connection, string table)
        {
            using var command = new SqlCommand("sp_helpindex", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@objname", table);

            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                Tables[table].Indexes.Add(LoadIndex(dr));
            }
        }

        private void LoadIncludedColumnsForIndex(SqlConnection connection, SqlIndex index)
        {
            using var command = new SqlCommand(LoadQueryFromResource("IncludedColumnsForIndex"), connection);
            command.Parameters.AddWithValue("@TableName", index.TableName);
            command.Parameters.AddWithValue("@IndexName", index.IndexName);

            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                if (dr.GetBoolean(3))
                {
                    index.IncludedColumns.Add(dr.GetString(1), dr.GetBoolean(2));
                }
            }
        }

        private void LoadRelations(SqlConnection connection)
        {
            using var command = new SqlCommand(LoadQueryFromResource("Relations"), connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                var relation = LoadRelation(dr);

                if (Tables.ContainsKey(relation.ChildTable))
                {
                    Tables[relation.ChildTable].Relations.Add(relation);
                }
            }
        }

        private void LoadColumnDetails(SqlConnection connection)
        {
            using var command = new SqlCommand(LoadQueryFromResource("ColumnDetails"), connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                var detail = LoadColumnDetail(dr);

                if (Tables.ContainsKey(detail.TableName))
                {
                    Tables[detail.TableName].ColumnDetails.Add(detail);
                }
            }
        }

        private void LoadRolePermissions(SqlConnection connection)
        {
            using var command = new SqlCommand(LoadQueryFromResource("RolePermissions"), connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                Permissions.Add(LoadPermission(dr));
            }
        }

        private void LoadUserPermissions(SqlConnection connection)
        {
            using var command = new SqlCommand(LoadQueryFromResource("UserPermissions"), connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                Permissions.Add(LoadPermission(dr));
            }
        }

        private void LoadExtendedProperties(SqlConnection connection)
        {
            using var command = new SqlCommand(LoadQueryFromResource("ExtendedProperties"), connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                ExtendedProperties.Add(LoadExtendedProperty(dr));
            }
        }

        private void LoadTriggers(SqlConnection connection)
        {
            using var command = new SqlCommand(LoadQueryFromResource("Triggers"), connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                var trigger = LoadTrigger(dr);

                if (Tables.ContainsKey(trigger.TableName))
                {
                    Tables[trigger.TableName].Triggers.Add(trigger);
                }
            }
        }

        private void LoadSynonyms(SqlConnection connection)
        {
            using var command = new SqlCommand(LoadQueryFromResource("Synonyms"), connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                var i = 0;
                var name = string.Empty;
                var def = string.Empty;
                while (i < dr.FieldCount)
                {
                    switch (dr.GetName(i))
                    {
                        case "SYNONYM_NAME":
                            name = dr.GetString(i);
                            break;
                        case "BASE_OBJECT_NAME":
                            def = dr.GetString(i);
                            break;
                    }

                    i++;
                }

                Synonyms.Add(name, def);
            }
        }

        private void LoadViews(SqlConnection connection)
        {
            using var command = new SqlCommand(LoadQueryFromResource("Views"), connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                var i = 0;
                var name = string.Empty;
                var def = string.Empty;
                while (i < dr.FieldCount)
                {
                    switch (dr.GetName(i))
                    {
                        case "VIEW_NAME":
                            name = dr.GetString(i);
                            break;
                        case "VIEW_DEFINITION":
                            def = dr.GetString(i);
                            break;
                    }

                    i++;
                }

                Views.Add(name, def);
            }
        }

        private void LoadUserRoutines(SqlConnection connection)
        {
            using var command = new SqlCommand(LoadQueryFromResource("UserRoutines"), connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                var routine = new SqlUserRoutine();
                var name = string.Empty;
                var i = 0;
                while (i < dr.FieldCount)
                {
                    switch (dr.GetName(i))
                    {
                        case "ROUTINE_NAME":
                            name = dr.GetString(i);
                            break;
                        case nameof(SqlUserRoutine.RoutineType):
                            routine.RoutineType = dr.GetString(i);
                            break;
                    }

                    i++;
                }

                UserRoutines.Add(name, routine);
            }
        }

        private void LoadUserRoutineDefinitions(SqlConnection connection)
        {
            using var command = new SqlCommand(LoadQueryFromResource("UserRoutineDefinitions"), connection);
            command.Parameters.Add("@routinename", SqlDbType.VarChar, 128);
            foreach (var routine in UserRoutines.Keys)
            {
                command.Parameters["@routinename"].Value = routine;
                connection.Open();
                using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
                while (dr.Read())
                {
                    UserRoutines[routine].RoutineDefinition += dr.GetString(0);
                }
            }
        }

        private static SqlIndex LoadIndex(SqlDataReader dr)
        {
            var index = new SqlIndex();
            string desc;
            var i = 0;
            while (i < dr.FieldCount)
            {
                switch (dr.GetName(i))
                {
                    case "index_name":
                        index.IndexName = dr.GetString(i);
                        break;
                    case "index_keys":
                        index.SetColumnsFromString(dr.GetString(i));
                        break;
                    case "index_description":
                        desc = dr.GetString(i);
                        index.IsPrimaryKey = desc.IndexOf("primary key") >= 0;
                        index.Clustered = desc.IndexOf("nonclustered") == -1;
                        index.Unique = desc.IndexOf("unique") > 0;
                        index.IsUniqueKey = desc.IndexOf("unique key") > 0;
                        index.FileGroup = Regex.Match(desc, "located on  (.*)$").Groups[1].Value;
                        break;
                }
                i++;
            }

            return index;
        }

        private static SqlRelation LoadRelation(SqlDataReader dr)
        {
            var relation = new SqlRelation();
            var i = 0;
            while (i < dr.FieldCount)
            {
                switch (dr.GetName(i))
                {
                    case nameof(SqlRelation.RelationName):
                        relation.RelationName = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.ChildTable):
                        relation.ChildTable = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.ChildColumns):
                        relation.ChildColumns = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.UniqueConstraintName):
                        relation.UniqueConstraintName = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.ParentTable):
                        relation.ParentTable = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.ParentColumns):
                        relation.ParentColumns = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.UpdateRule):
                        relation.UpdateRule = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.DeleteRule):
                        relation.DeleteRule = dr.GetString(i);
                        break;
                }

                i++;
            }

            return relation;
        }

        private static SqlColumnDetail LoadColumnDetail(SqlDataReader dr)
        {
            var i = 0;
            var detail = new SqlColumnDetail();
            while (i < dr.FieldCount)
            {
                switch (dr.GetName(i))
                {
                    case nameof(SqlColumnDetail.TableSchema):
                        detail.TableSchema = dr.GetString(i);
                        break;
                    case nameof(SqlColumnDetail.TableName):
                        detail.TableName = dr.GetString(i);
                        break;
                    case nameof(SqlColumnDetail.ColumnName):
                        detail.ColumnName = dr.GetString(i);
                        break;
                    case nameof(SqlColumnDetail.OrdinalPosition):
                        detail.OrdinalPosition = dr.GetInt32(i);
                        break;
                    case nameof(SqlColumnDetail.ColumnDefault):
                        if (!dr.IsDBNull(i))
                        {
                            detail.ColumnDefault = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.IsNullable):
                        if (!dr.IsDBNull(i))
                        {
                            detail.IsNullable = dr.GetString(i) == "YES";
                        }
                        break;
                    case nameof(SqlColumnDetail.DataType):
                        detail.DataType = dr.GetString(i);
                        break;
                    case nameof(SqlColumnDetail.CharacterMaximumLength):
                        if (!dr.IsDBNull(i))
                        {
                            detail.CharacterMaximumLength = dr.GetInt32(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.CharacterOctetLength):
                        if (!dr.IsDBNull(i))
                        {
                            detail.CharacterOctetLength = dr.GetInt32(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.NumericPrecision):
                        if (!dr.IsDBNull(i))
                        {
                            detail.NumericPrecision = dr.GetByte(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.NumericPrecisionRadix):
                        if (!dr.IsDBNull(i))
                        {
                            detail.NumericPrecisionRadix = dr.GetInt16(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.NumericScale):
                        if (!dr.IsDBNull(i))
                        {
                            detail.NumericScale = dr.GetInt32(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.DatetimePrecision):
                        if (!dr.IsDBNull(i))
                        {
                            detail.DatetimePrecision = dr.GetInt16(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.CharacterSetName):
                        if (!dr.IsDBNull(i))
                        {
                            detail.CharacterSetName = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.CollationName):
                        if (!dr.IsDBNull(i))
                        {
                            detail.CollationName = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.IsFullTextIndexed):
                        detail.IsFullTextIndexed = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlColumnDetail.IsComputed):
                        detail.IsComputed = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlColumnDetail.IsIdentity):
                        detail.IsIdentity = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlColumnDetail.IdentitySeed):
                        if (!dr.IsDBNull(i))
                        {
                            detail.IdentitySeed = dr.GetDecimal(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.IdentityIncrement):
                        if (!dr.IsDBNull(i))
                        {
                            detail.IdentityIncrement = dr.GetDecimal(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.IsSparse):
                        if (!dr.IsDBNull(i))
                        {
                            detail.IsSparse = dr.GetInt32(i) == 1;
                        }
                        break;
                    case nameof(SqlColumnDetail.IsColumnSet):
                        if (!dr.IsDBNull(i))
                        {
                            detail.IsColumnSet = dr.GetInt32(i) == 1;
                        }
                        break;
                }

                i++;
            }

            return detail;
        }

        private static SqlPermission LoadPermission(SqlDataReader dr)
        {
            var i = 0;
            var permission = new SqlPermission();
            while (i < dr.FieldCount)
            {
                switch (dr.GetName(i))
                {
                    case nameof(SqlPermission.UserName):
                        permission.UserName = dr.GetString(i);
                        break;
                    case nameof(SqlPermission.RoleName):
                        if (!dr.IsDBNull(i))
                        {
                            permission.ColumnName = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlPermission.PermissionType):
                        if (!dr.IsDBNull(i))
                        {
                            permission.ColumnName = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlPermission.PermissionState):
                        if (!dr.IsDBNull(i))
                        {
                            permission.ColumnName = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlPermission.ObjectType):
                        if (!dr.IsDBNull(i))
                        {
                            permission.ColumnName = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlPermission.ObjectName):
                        if (!dr.IsDBNull(i))
                        {
                            permission.ColumnName = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlPermission.ColumnName):
                        if (!dr.IsDBNull(i))
                        {
                            permission.ColumnName = dr.GetString(i);
                        }
                        break;
                }

                i++;
            }

            return permission;
        }

        private static SqlExtendedProperty LoadExtendedProperty(SqlDataReader dr)
        {
            var i = 0;
            var property = new SqlExtendedProperty();
            while (i < dr.FieldCount)
            {
                switch (dr.GetName(i))
                {
                    case nameof(SqlExtendedProperty.PropertyType):
                        property.PropertyType = dr.GetString(i);
                        break;
                    case nameof(SqlExtendedProperty.ObjectName):
                        if (!dr.IsDBNull(i))
                        {
                            property.ObjectName = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlExtendedProperty.ColumnName):
                        if (!dr.IsDBNull(i))
                        {
                            property.ColumnName = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlExtendedProperty.PropertyName):
                        property.PropertyName = dr.GetString(i);
                        break;
                    case nameof(SqlExtendedProperty.PropertyValue):
                        if (!dr.IsDBNull(i))
                        {
                            property.PropertyValue = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlExtendedProperty.IndexName):
                        if (!dr.IsDBNull(i))
                        {
                            property.IndexName = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlExtendedProperty.TableName):
                        if (!dr.IsDBNull(i))
                        {
                            property.TableName = dr.GetString(i);
                        }
                        break;
                }

                i++;
            }

            return property;
        }

        private static SqlTrigger LoadTrigger(SqlDataReader dr)
        {
            var i = 0;
            var trigger = new SqlTrigger();
            while (i < dr.FieldCount)
            {
                switch (dr.GetName(i))
                {
                    case nameof(SqlTrigger.TriggerName):
                        trigger.TriggerName = dr.GetString(i);
                        break;
                    case nameof(SqlTrigger.TriggerOwner):
                        if (!dr.IsDBNull(i))
                        {
                            trigger.TriggerName = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlTrigger.TableSchema):
                        if (!dr.IsDBNull(i))
                        {
                            trigger.TriggerName = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlTrigger.TableName):
                        if (!dr.IsDBNull(i))
                        {
                            trigger.TriggerName = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlTrigger.IsUpdate):
                        trigger.IsUpdate = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlTrigger.IsDelete):
                        trigger.IsDelete = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlTrigger.IsInsert):
                        trigger.IsInsert = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlTrigger.IsAfter):
                        trigger.IsAfter = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlTrigger.IsInsteadOf):
                        trigger.IsInsteadOf = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlTrigger.IsDisabled):
                        trigger.IsDisabled = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlTrigger.TriggerContent):
                        trigger.TriggerName = dr.GetString(i);
                        break;
                }

                i++;
            }

            return trigger;
        }

        #endregion
    }
}
