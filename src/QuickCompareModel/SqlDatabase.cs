namespace QuickCompareModel
{
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Text.RegularExpressions;
    using QuickCompareModel.DatabaseSchema;

    internal class SqlDatabase
    {
        private readonly bool populateObjects;
        private readonly bool checkIndexes;
        private readonly bool checkPermissions;
        private readonly bool checkExtendedProperties;
        private readonly bool checkTriggers;
        private readonly bool checkSynonyms;

        public SqlDatabase(string connectionString, bool populateObjects, bool checkIndexes, bool checkPermissions, bool checkExtendedProperties, bool checkTriggers, bool checkSynonyms)
        {
            ConnectionStr = connectionString;
            this.populateObjects = populateObjects;
            this.checkIndexes = checkIndexes;
            this.checkPermissions = checkPermissions;
            this.checkExtendedProperties = checkExtendedProperties;
            this.checkTriggers = checkTriggers;
            this.checkSynonyms = checkSynonyms;

            PopulateSchemaModel();
        }

        public SqlDatabase(string connectionString)
            : this(connectionString, true, true, true, true, true, true)
        {
        }

        public string ConnectionStr { get; set; }

        public string FriendlyName
        {
            get
            {
                var builder = new SqlConnectionStringBuilder(ConnectionStr);
                return $"[{builder.DataSource}].[{builder.InitialCatalog}]";
            }
        }

        public Dictionary<string, SqlTable> SqlTables { get; set; } = new Dictionary<string, SqlTable>();

        public Dictionary<string, string> Views { get; set; } = new Dictionary<string, string>();

        public Dictionary<string, string> SqlSynonyms { get; set; } = new Dictionary<string, string>();

        public Dictionary<string, SqlUserRoutine> UserRoutines { get; set; } = new Dictionary<string, SqlUserRoutine>();

        public List<SqlPermission> SqlPermissions { get; set; } = new List<SqlPermission>();

        public List<SqlExtendedProperty> SqlExtendedProperties { get; set; } = new List<SqlExtendedProperty>();

        private void PopulateSchemaModel()
        {
            using var connection = new SqlConnection(ConnectionStr);
            LoadTableNames(connection);

            if (checkIndexes)
            {
                foreach (var table in SqlTables.Keys)
                {
                    LoadIndexes(connection, table);

                    foreach (var index in SqlTables[table].Indexes)
                    {
                        LoadIncludedColumnsForIndex(connection, index);
                    }
                }
            }

            LoadRelations(connection);

            LoadColumnDetails(connection);

            if (checkPermissions)
            {
                LoadRolePermissions(connection);

                LoadUserPermissions(connection);
            }

            if (checkExtendedProperties)
            {
                LoadExtendedProperties(connection);
            }

            if (checkTriggers)
            {
                LoadTriggers(connection);
            }

            if (checkSynonyms)
            {
                LoadSynonyms(connection);
            }

            if (populateObjects)
            {
                LoadViews(connection);

                LoadUserRoutines(connection);

                LoadUserRoutineDefinitions(connection);
            }
        }

        #region Load methods

        private void LoadTableNames(SqlConnection connection)
        {
            using var command = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE (TABLE_TYPE = 'BASE TABLE') AND (TABLE_NAME <> 'dtproperties') AND (TABLE_NAME NOT LIKE 'sys%') AND (TABLE_NAME NOT LIKE 'MS%') ORDER BY TABLE_NAME", connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                SqlTables.Add(dr.GetString(0), new SqlTable());
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
                SqlTables[table].Indexes.Add(LoadIndex(dr));
            }
        }

        private static void LoadIncludedColumnsForIndex(SqlConnection connection, SqlIndex index)
        {
            using var command = new SqlCommand("SELECT (CASE ic.key_ordinal WHEN 0 THEN CAST(1 AS tinyint) ELSE ic.key_ordinal END) AS ORDINAL, clmns.name AS COLUMN_NAME, ic.is_descending_key AS IS_DESCENDING, ic.is_included_column AS IS_INCLUDED FROM sys.tables AS tbl INNER JOIN sys.indexes AS i ON (i.index_id > 0 AND i.is_hypothetical = 0) AND (i.object_id = tbl.object_id) INNER JOIN sys.index_columns AS ic ON (ic.column_id > 0 AND (ic.key_ordinal > 0 OR ic.partition_ordinal = 0 OR ic.is_included_column != 0)) AND (ic.index_id = CAST(i.index_id AS int) AND ic.object_id = i.object_id) INNER JOIN sys.columns AS clmns ON clmns.object_id = ic.object_id AND clmns.column_id = ic.column_id WHERE (i.name = @IndexName) AND (tbl.name = @TableName) ORDER BY ic.key_ordinal", connection);
            command.Parameters.AddWithValue("@TableName", index.TABLE_NAME);
            command.Parameters.AddWithValue("@IndexName", index.INDEX_NAME);

            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                if (dr.GetBoolean(3))
                {
                    index.INCLUDED_COLUMNS.Add(dr.GetString(1), dr.GetBoolean(2));
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

                if (SqlTables.ContainsKey(relation.CHILD_TABLE))
                {
                    SqlTables[relation.CHILD_TABLE].Relations.Add(relation);
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

                if (SqlTables.ContainsKey(detail.TABLE_NAME))
                {
                    SqlTables[detail.TABLE_NAME].ColumnDetails.Add(detail);
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
                SqlPermissions.Add(LoadPermission(dr));
            }
        }

        private void LoadUserPermissions(SqlConnection connection)
        {
            using var command = new SqlCommand("SELECT [UserName] = CASE princ.[type] WHEN 'S' THEN princ.[name] WHEN 'U' THEN ulogin.[name] COLLATE Latin1_General_CI_AI END, [UserType] = CASE princ.[type] WHEN 'S' THEN 'SQL User' WHEN 'U' THEN 'Windows User' END, [USER_NAME] = princ.[name], [ROLE_NAME] = null, [PERMISSION_TYPE] = perm.[permission_name], [PERMISSION_STATE] = perm.[state_desc], [OBJECT_TYPE] = obj.type_desc, [OBJECT_NAME] = OBJECT_NAME(perm.major_id), [COLUMN_NAME] = col.[name] FROM sys.database_principals princ LEFT JOIN sys.login_token ulogin on princ.[sid] = ulogin.[sid] LEFT JOIN sys.database_permissions perm ON perm.[grantee_principal_id] = princ.[principal_id] LEFT JOIN sys.columns col ON col.[object_id] = perm.major_id AND col.[column_id] = perm.[minor_id] LEFT JOIN sys.objects obj ON perm.[major_id] = obj.[object_id] WHERE princ.[type] in ('S','U')", connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                SqlPermissions.Add(LoadPermission(dr));
            }
        }

        private void LoadExtendedProperties(SqlConnection connection)
        {
            using var command = new SqlCommand("SELECT class_desc AS PROPERTY_TYPE, o.name AS OBJECT_NAME, c.name AS COLUMN_NAME, ep.name AS PROPERTY_NAME, value AS PROPERTY_VALUE, t.name AS TABLE_NAME, s.name AS INDEX_NAME FROM sys.extended_properties AS ep LEFT OUTER JOIN sys.objects AS o ON o.object_id = ep.major_id LEFT OUTER JOIN sys.tables AS t ON t.object_id = ep.major_id LEFT OUTER JOIN sys.columns AS c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id AND ep.class = 1 LEFT OUTER JOIN sys.indexes AS s ON s.object_id = ep.major_id AND s.index_id = ep.minor_id WHERE (ep.name <> 'microsoft_database_tools_support') AND ((NOT t.name IS NULL AND NOT c.name IS NULL AND ep.Name = 'MS_Description') OR ep.name NOT LIKE 'MS_%')", connection);
            connection.Open();
            using var dr = command.ExecuteReader(CommandBehavior.CloseConnection);
            while (dr.Read())
            {
                SqlExtendedProperties.Add(LoadExtendedProperty(dr));
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

                if (SqlTables.ContainsKey(trigger.TABLE_NAME))
                {
                    SqlTables[trigger.TABLE_NAME].Triggers.Add(trigger);
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

                SqlSynonyms.Add(name, def);
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
                        case nameof(SqlUserRoutine.ROUTINE_TYPE):
                            routine.ROUTINE_TYPE = dr.GetString(i);
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
                    UserRoutines[routine].ROUTINE_DEFINITION += dr.GetString(0);
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
                        index.INDEX_NAME = dr.GetString(i);
                        break;
                    case "index_keys":
                        index.SetColumnsFromString(dr.GetString(i));
                        break;
                    case "index_description":
                        desc = dr.GetString(i);
                        index.IS_PRIMARY_KEY = desc.IndexOf("primary key") >= 0;
                        index.CLUSTERED = desc.IndexOf("nonclustered") == -1;
                        index.UNIQUE = desc.IndexOf("unique") > 0;
                        index.IS_UNIQUE_KEY = desc.IndexOf("unique key") > 0;
                        index.FILEGROUP = Regex.Match(desc, "located on  (.*)$").Groups[1].Value;
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
                    case nameof(SqlRelation.RELATION_NAME):
                        relation.RELATION_NAME = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.CHILD_TABLE):
                        relation.CHILD_TABLE = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.CHILD_COLUMNS):
                        relation.CHILD_COLUMNS = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.UNIQUE_CONSTRAINT_NAME):
                        relation.UNIQUE_CONSTRAINT_NAME = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.PARENT_TABLE):
                        relation.PARENT_TABLE = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.PARENT_COLUMNS):
                        relation.PARENT_COLUMNS = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.UPDATE_RULE):
                        relation.UPDATE_RULE = dr.GetString(i);
                        break;
                    case nameof(SqlRelation.DELETE_RULE):
                        relation.DELETE_RULE = dr.GetString(i);
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
                    case nameof(SqlColumnDetail.TABLE_SCHEMA):
                        detail.TABLE_SCHEMA = dr.GetString(i);
                        break;
                    case nameof(SqlColumnDetail.TABLE_NAME):
                        detail.TABLE_NAME = dr.GetString(i);
                        break;
                    case nameof(SqlColumnDetail.COLUMN_NAME):
                        detail.COLUMN_NAME = dr.GetString(i);
                        break;
                    case nameof(SqlColumnDetail.ORDINAL_POSITION):
                        detail.ORDINAL_POSITION = dr.GetInt32(i);
                        break;
                    case nameof(SqlColumnDetail.COLUMN_DEFAULT):
                        if (!dr.IsDBNull(i))
                        {
                            detail.COLUMN_DEFAULT = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.IS_NULLABLE):
                        if (!dr.IsDBNull(i))
                        {
                            detail.IS_NULLABLE = dr.GetString(i) == "YES";
                        }
                        break;
                    case nameof(SqlColumnDetail.DATA_TYPE):
                        detail.DATA_TYPE = dr.GetString(i);
                        break;
                    case nameof(SqlColumnDetail.CHARACTER_MAXIMUM_LENGTH):
                        if (!dr.IsDBNull(i))
                        {
                            detail.CHARACTER_MAXIMUM_LENGTH = dr.GetInt32(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.CHARACTER_OCTET_LENGTH):
                        if (!dr.IsDBNull(i))
                        {
                            detail.CHARACTER_OCTET_LENGTH = dr.GetInt32(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.NUMERIC_PRECISION):
                        if (!dr.IsDBNull(i))
                        {
                            detail.NUMERIC_PRECISION = dr.GetByte(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.NUMERIC_PRECISION_RADIX):
                        if (!dr.IsDBNull(i))
                        {
                            detail.NUMERIC_PRECISION_RADIX = dr.GetInt16(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.NUMERIC_SCALE):
                        if (!dr.IsDBNull(i))
                        {
                            detail.NUMERIC_SCALE = dr.GetInt32(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.DATETIME_PRECISION):
                        if (!dr.IsDBNull(i))
                        {
                            detail.DATETIME_PRECISION = dr.GetInt16(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.CHARACTER_SET_NAME):
                        if (!dr.IsDBNull(i))
                        {
                            detail.CHARACTER_SET_NAME = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.COLLATION_NAME):
                        if (!dr.IsDBNull(i))
                        {
                            detail.COLLATION_NAME = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.IS_FULL_TEXT_INDEXED):
                        detail.IS_FULL_TEXT_INDEXED = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlColumnDetail.IS_COMPUTED):
                        detail.IS_COMPUTED = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlColumnDetail.IS_IDENTITY):
                        detail.IS_IDENTITY = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlColumnDetail.IDENTITY_SEED):
                        if (!dr.IsDBNull(i))
                        {
                            detail.IDENTITY_SEED = dr.GetDecimal(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.IDENTITY_INCREMENT):
                        if (!dr.IsDBNull(i))
                        {
                            detail.IDENTITY_INCREMENT = dr.GetDecimal(i);
                        }
                        break;
                    case nameof(SqlColumnDetail.IS_SPARSE):
                        if (!dr.IsDBNull(i))
                        {
                            detail.IS_SPARSE = dr.GetInt32(i) == 1;
                        }
                        break;
                    case nameof(SqlColumnDetail.IS_COLUMN_SET):
                        if (!dr.IsDBNull(i))
                        {
                            detail.IS_COLUMN_SET = dr.GetInt32(i) == 1;
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
                    case nameof(SqlPermission.USER_NAME):
                        permission.USER_NAME = dr.GetString(i);
                        break;
                    case nameof(SqlPermission.ROLE_NAME):
                        if (!dr.IsDBNull(i))
                        {
                            permission.COLUMN_NAME = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlPermission.PERMISSION_TYPE):
                        if (!dr.IsDBNull(i))
                        {
                            permission.COLUMN_NAME = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlPermission.PERMISSION_STATE):
                        if (!dr.IsDBNull(i))
                        {
                            permission.COLUMN_NAME = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlPermission.OBJECT_TYPE):
                        if (!dr.IsDBNull(i))
                        {
                            permission.COLUMN_NAME = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlPermission.OBJECT_NAME):
                        if (!dr.IsDBNull(i))
                        {
                            permission.COLUMN_NAME = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlPermission.COLUMN_NAME):
                        if (!dr.IsDBNull(i))
                        {
                            permission.COLUMN_NAME = dr.GetString(i);
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
                    case nameof(SqlExtendedProperty.PROPERTY_TYPE):
                        property.PROPERTY_TYPE = dr.GetString(i);
                        break;
                    case nameof(SqlExtendedProperty.OBJECT_NAME):
                        if (!dr.IsDBNull(i))
                        {
                            property.OBJECT_NAME = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlExtendedProperty.COLUMN_NAME):
                        if (!dr.IsDBNull(i))
                        {
                            property.COLUMN_NAME = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlExtendedProperty.PROPERTY_NAME):
                        property.PROPERTY_NAME = dr.GetString(i);
                        break;
                    case nameof(SqlExtendedProperty.PROPERTY_VALUE):
                        if (!dr.IsDBNull(i))
                        {
                            property.PROPERTY_VALUE = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlExtendedProperty.INDEX_NAME):
                        if (!dr.IsDBNull(i))
                        {
                            property.INDEX_NAME = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlExtendedProperty.TABLE_NAME):
                        if (!dr.IsDBNull(i))
                        {
                            property.TABLE_NAME = dr.GetString(i);
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
                    case nameof(SqlTrigger.TRIGGER_NAME):
                        trigger.TRIGGER_NAME = dr.GetString(i);
                        break;
                    case nameof(SqlTrigger.TRIGGER_OWNER):
                        if (!dr.IsDBNull(i))
                        {
                            trigger.TRIGGER_NAME = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlTrigger.TABLE_SCHEMA):
                        if (!dr.IsDBNull(i))
                        {
                            trigger.TRIGGER_NAME = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlTrigger.TABLE_NAME):
                        if (!dr.IsDBNull(i))
                        {
                            trigger.TRIGGER_NAME = dr.GetString(i);
                        }
                        break;
                    case nameof(SqlTrigger.IS_UPDATE):
                        trigger.IS_UPDATE = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlTrigger.IS_DELETE):
                        trigger.IS_DELETE = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlTrigger.IS_INSERT):
                        trigger.IS_INSERT = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlTrigger.IS_AFTER):
                        trigger.IS_AFTER = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlTrigger.IS_INSTEAD_OF):
                        trigger.IS_INSTEAD_OF = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlTrigger.IS_DISABLED):
                        trigger.IS_DISABLED = dr.GetInt32(i) == 1;
                        break;
                    case nameof(SqlTrigger.TRIGGER_CONTENT):
                        trigger.TRIGGER_NAME = dr.GetString(i);
                        break;
                }

                i++;
            }

            return trigger;
        }

        #endregion
    }
}
