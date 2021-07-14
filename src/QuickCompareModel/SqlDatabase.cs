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
    internal class SqlDatabase
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

            PopulateSchemaModel();
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

        private void PopulateSchemaModel()
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

        #region Load methods

        private string LoadQueryFromResource(string queryName)
        {
            var sqlQuery = string.Empty;

            var resourceName = $"QuickCompareModel.{queryName}.sql";
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    sqlQuery = new StreamReader(stream).ReadToEnd();
                }
            }

            return sqlQuery;
        }

        private void LoadTableNames(SqlConnection connection)
        {
            using var command = new SqlCommand(LoadQueryFromResource("TableNames.sql"), connection);
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

        private static void LoadIncludedColumnsForIndex(SqlConnection connection, SqlIndex index)
        {
            using var command = new SqlCommand("SELECT (CASE ic.key_ordinal WHEN 0 THEN CAST(1 AS tinyint) ELSE ic.key_ordinal END) AS ORDINAL, clmns.name AS COLUMN_NAME, ic.is_descending_key AS IS_DESCENDING, ic.is_included_column AS IS_INCLUDED FROM sys.tables AS tbl INNER JOIN sys.indexes AS i ON (i.index_id > 0 AND i.is_hypothetical = 0) AND (i.object_id = tbl.object_id) INNER JOIN sys.index_columns AS ic ON (ic.column_id > 0 AND (ic.key_ordinal > 0 OR ic.partition_ordinal = 0 OR ic.is_included_column != 0)) AND (ic.index_id = CAST(i.index_id AS int) AND ic.object_id = i.object_id) INNER JOIN sys.columns AS clmns ON clmns.object_id = ic.object_id AND clmns.column_id = ic.column_id WHERE (i.name = @IndexName) AND (tbl.name = @TableName) ORDER BY ic.key_ordinal", connection);
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
            using var command = new SqlCommand("SELECT DISTINCT R1.RELATION_NAME, R1.CHILD_TABLE, STUFF((SELECT ', ' + R2.CHILD_COLUMN FROM (SELECT Child.CONSTRAINT_NAME AS RELATION_NAME, Child.COLUMN_NAME AS CHILD_COLUMN, Child.ORDINAL_POSITION FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS RC INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE Child ON Child.CONSTRAINT_CATALOG = RC.CONSTRAINT_CATALOG AND Child.CONSTRAINT_SCHEMA = RC.CONSTRAINT_SCHEMA AND Child.CONSTRAINT_NAME = RC.CONSTRAINT_NAME) R2 WHERE R2.RELATION_NAME = R1.RELATION_NAME ORDER BY R2.ORDINAL_POSITION FOR XML PATH(''), TYPE).value('.','VARCHAR(MAX)'),1,2,'') AS CHILD_COLUMNS, R1.UNIQUE_CONSTRAINT_NAME, R1.PARENT_TABLE, STUFF((SELECT ', ' + R2.PARENT_COLUMN FROM (SELECT Child.CONSTRAINT_NAME AS RELATION_NAME, Parent.COLUMN_NAME AS PARENT_COLUMN, Parent.ORDINAL_POSITION FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS RC INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE Child ON Child.CONSTRAINT_CATALOG = RC.CONSTRAINT_CATALOG AND Child.CONSTRAINT_SCHEMA = RC.CONSTRAINT_SCHEMA AND Child.CONSTRAINT_NAME = RC.CONSTRAINT_NAME INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE Parent ON Parent.CONSTRAINT_CATALOG = RC.UNIQUE_CONSTRAINT_CATALOG AND Parent.CONSTRAINT_SCHEMA = RC.UNIQUE_CONSTRAINT_SCHEMA AND Parent.CONSTRAINT_NAME = RC.UNIQUE_CONSTRAINT_NAME AND Parent.ORDINAL_POSITION = Child.ORDINAL_POSITION) R2 WHERE R2.RELATION_NAME = R1.RELATION_NAME ORDER BY R2.ORDINAL_POSITION FOR XML PATH(''), TYPE).value('.','VARCHAR(MAX)'),1,2,'') AS PARENT_COLUMNS, R1.UPDATE_RULE, R1.DELETE_RULE FROM (SELECT Child.CONSTRAINT_NAME AS RELATION_NAME, Child.TABLE_NAME AS CHILD_TABLE, Child.COLUMN_NAME AS CHILD_COLUMN, Parent.CONSTRAINT_NAME AS UNIQUE_CONSTRAINT_NAME, Parent.TABLE_NAME AS PARENT_TABLE, Parent.COLUMN_NAME AS PARENT_COLUMN, RC.UPDATE_RULE, RC.DELETE_RULE FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS RC INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE Child ON Child.CONSTRAINT_CATALOG = RC.CONSTRAINT_CATALOG AND Child.CONSTRAINT_SCHEMA = RC.CONSTRAINT_SCHEMA AND Child.CONSTRAINT_NAME = RC.CONSTRAINT_NAME INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE Parent ON Parent.CONSTRAINT_CATALOG = RC.UNIQUE_CONSTRAINT_CATALOG AND Parent.CONSTRAINT_SCHEMA = RC.UNIQUE_CONSTRAINT_SCHEMA AND Parent.CONSTRAINT_NAME = RC.UNIQUE_CONSTRAINT_NAME AND Parent.ORDINAL_POSITION = Child.ORDINAL_POSITION) R1", connection);
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
            using var command = new SqlCommand("SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, ORDINAL_POSITION, COLUMN_DEFAULT, IS_NULLABLE, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, CHARACTER_OCTET_LENGTH, NUMERIC_PRECISION, NUMERIC_PRECISION_RADIX, NUMERIC_SCALE, DATETIME_PRECISION, CHARACTER_SET_NAME, COLLATION_NAME, COLUMNPROPERTY(OBJECT_ID(TABLE_NAME), COLUMN_NAME, N'IsFulltextIndexed') AS IS_FULL_TEXT_INDEXED, COLUMNPROPERTY(OBJECT_ID(TABLE_NAME), COLUMN_NAME, N'IsComputed') AS IS_COMPUTED, COLUMNPROPERTY(OBJECT_ID(TABLE_NAME), COLUMN_NAME, N'IsIdentity') AS IS_IDENTITY, IDENT_SEED(TABLE_NAME) AS IDENTITY_SEED, IDENT_INCR(TABLE_NAME) AS IDENTITY_INCREMENT, COLUMNPROPERTY(OBJECT_ID(TABLE_NAME), COLUMN_NAME, N'IsSparse') AS IS_SPARSE, COLUMNPROPERTY(OBJECT_ID(TABLE_NAME), COLUMN_NAME, N'IsColumnSet') AS IS_COLUMN_SET FROM INFORMATION_SCHEMA.COLUMNS WHERE (TABLE_NAME NOT LIKE 'sys%') AND (TABLE_NAME NOT LIKE 'MS%')", connection);
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
            using var command = new SqlCommand("SELECT [UserName] = CASE memberprinc.[type] WHEN 'S' THEN memberprinc.[name] WHEN 'U' THEN ulogin.[name] COLLATE Latin1_General_CI_AI END, [UserType] = CASE memberprinc.[type] WHEN 'S' THEN 'SQL User' WHEN 'U' THEN 'Windows User' END, [USER_NAME] = memberprinc.[name], [ROLE_NAME] = roleprinc.[name], [PERMISSION_TYPE] = perm.[permission_name], [PERMISSION_STATE] = perm.[state_desc], [OBJECT_TYPE] = obj.type_desc, [OBJECT_NAME] = OBJECT_NAME(perm.major_id), [COLUMN_NAME] = col.[name] FROM sys.database_role_members members JOIN sys.database_principals roleprinc ON roleprinc.[principal_id] = members.[role_principal_id] JOIN sys.database_principals memberprinc ON memberprinc.[principal_id] = members.[member_principal_id] LEFT JOIN sys.login_token ulogin on memberprinc.[sid] = ulogin.[sid] LEFT JOIN sys.database_permissions perm ON perm.[grantee_principal_id] = roleprinc.[principal_id] LEFT JOIN sys.columns col on col.[object_id] = perm.major_id AND col.[column_id] = perm.[minor_id] LEFT JOIN sys.objects obj ON perm.[major_id] = obj.[object_id]", connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                Permissions.Add(LoadPermission(dr));
            }
        }

        private void LoadUserPermissions(SqlConnection connection)
        {
            using var command = new SqlCommand("SELECT [UserName] = CASE princ.[type] WHEN 'S' THEN princ.[name] WHEN 'U' THEN ulogin.[name] COLLATE Latin1_General_CI_AI END, [UserType] = CASE princ.[type] WHEN 'S' THEN 'SQL User' WHEN 'U' THEN 'Windows User' END, [USER_NAME] = princ.[name], [ROLE_NAME] = null, [PERMISSION_TYPE] = perm.[permission_name], [PERMISSION_STATE] = perm.[state_desc], [OBJECT_TYPE] = obj.type_desc, [OBJECT_NAME] = OBJECT_NAME(perm.major_id), [COLUMN_NAME] = col.[name] FROM sys.database_principals princ LEFT JOIN sys.login_token ulogin on princ.[sid] = ulogin.[sid] LEFT JOIN sys.database_permissions perm ON perm.[grantee_principal_id] = princ.[principal_id] LEFT JOIN sys.columns col ON col.[object_id] = perm.major_id AND col.[column_id] = perm.[minor_id] LEFT JOIN sys.objects obj ON perm.[major_id] = obj.[object_id] WHERE princ.[type] in ('S','U')", connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                Permissions.Add(LoadPermission(dr));
            }
        }

        private void LoadExtendedProperties(SqlConnection connection)
        {
            using var command = new SqlCommand("SELECT class_desc AS PROPERTY_TYPE, o.name AS OBJECT_NAME, c.name AS COLUMN_NAME, ep.name AS PROPERTY_NAME, value AS PROPERTY_VALUE, t.name AS TABLE_NAME, s.name AS INDEX_NAME FROM sys.extended_properties AS ep LEFT OUTER JOIN sys.objects AS o ON o.object_id = ep.major_id LEFT OUTER JOIN sys.tables AS t ON t.object_id = ep.major_id LEFT OUTER JOIN sys.columns AS c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id AND ep.class = 1 LEFT OUTER JOIN sys.indexes AS s ON s.object_id = ep.major_id AND s.index_id = ep.minor_id WHERE (ep.name <> 'microsoft_database_tools_support') AND ((NOT t.name IS NULL AND NOT c.name IS NULL AND ep.Name = 'MS_Description') OR ep.name NOT LIKE 'MS_%')", connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                ExtendedProperties.Add(LoadExtendedProperty(dr));
            }
        }

        private void LoadTriggers(SqlConnection connection)
        {
            using var command = new SqlCommand("SELECT so.name AS TRIGGER_NAME, USER_NAME(so.uid) AS TRIGGER_OWNER, USER_NAME(so2.uid) AS TABLE_SCHEMA, OBJECT_NAME(so.[parent_obj]) AS TABLE_NAME, OBJECTPROPERTY(so.id, 'ExecIsUpdateTrigger') AS IS_UPDATE, OBJECTPROPERTY(so.id, 'ExecIsDeleteTrigger') AS IS_DELETE, OBJECTPROPERTY(so.id, 'ExecIsInsertTrigger') AS IS_INSERT, OBJECTPROPERTY(so.id, 'ExecIsAfterTrigger') AS IS_AFTER, OBJECTPROPERTY(so.id, 'ExecIsInsteadOfTrigger') AS IS_INSTEAD_OF, OBJECTPROPERTY(so.id, 'ExecIsTriggerDisabled') AS IS_DISABLED, object_definition(so.id) AS [TRIGGER_CONTENT] FROM sysobjects AS so INNER JOIN sysobjects AS so2 ON so.parent_obj = so2.Id WHERE so.type = 'TR'", connection);
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
            using var command = new SqlCommand("SELECT name AS SYNONYM_NAME, base_object_name AS BASE_OBJECT_NAME FROM sys.synonyms", connection);
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
            using var command = new SqlCommand("SELECT TABLE_NAME AS VIEW_NAME, VIEW_DEFINITION FROM INFORMATION_SCHEMA.VIEWS WHERE (TABLE_NAME NOT LIKE 'sys%') AND (TABLE_NAME NOT LIKE 'syncobj%') ORDER BY TABLE_NAME", connection);
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
            using var command = new SqlCommand("SELECT ROUTINE_NAME, ROUTINE_TYPE FROM INFORMATION_SCHEMA.ROUTINES WHERE (NOT (SPECIFIC_NAME LIKE 'dt_%')) AND (NOT (SPECIFIC_NAME = 'fn_diagramobjects')) AND (NOT (SPECIFIC_NAME = 'sp_dropdiagram')) AND (NOT (SPECIFIC_NAME = 'sp_alterdiagram')) AND (NOT (SPECIFIC_NAME = 'sp_renamediagram')) AND (NOT (SPECIFIC_NAME = 'sp_creatediagram')) AND (NOT (SPECIFIC_NAME = 'sp_helpdiagramdefinition')) AND (NOT (SPECIFIC_NAME = 'sp_helpdiagrams')) AND (NOT (SPECIFIC_NAME = 'sp_upgraddiagrams')) ORDER BY ROUTINE_NAME", connection);
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
            // todo: consider using OBJECT_DEFINITION instead
            using var command = new SqlCommand("SELECT text FROM syscomments WHERE (id = OBJECT_ID(@routinename)) ORDER BY colid", connection);
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
