namespace QuickCompareModel
{
    using System;
    using Microsoft.Extensions.Options;
    using QuickCompareModel.DatabaseDifferences;
    using QuickCompareModel.DatabaseSchema;

    /// <summary>
    /// Class responsible for building a set of differences between two database instances.
    /// </summary>
    public class DifferenceBuilder
    {
        private readonly QuickCompareOptions options;

        /// <summary>
        /// Initialises a new instance of the <see cref="DifferenceBuilder"/> class.
        /// </summary>
        /// <param name="options">Option settings for the database comparison.</param>
        public DifferenceBuilder(IOptions<QuickCompareOptions> options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this.options = options.Value;
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="DifferenceBuilder"/> class with ready <see cref="SqlDatabase"/> instances.
        /// </summary>
        /// <param name="options">Option settings for the database comparison.</param>
        /// <param name="database1">Instance of <see cref="SqlDatabase"/> representing the first database to compare.</param>
        /// <param name="database2">Instance of <see cref="SqlDatabase"/> representing the second database to compare.</param>
        public DifferenceBuilder(IOptions<QuickCompareOptions> options, SqlDatabase database1, SqlDatabase database2)
            : this(options)
        {
            this.Database1 = database1;
            this.Database2 = database2;
        }

        /// <summary> Gets or sets the model for database 1. </summary>
        public SqlDatabase Database1 { get; set; }

        /// <summary> Gets or sets the model for database 2. </summary>
        public SqlDatabase Database2 { get; set; }

        /// <summary> Handler for when the status message changes. </summary>
        public event EventHandler<StatusChangedEventArgs> ComparisonStatusChanged;

        /// <summary> Model representing the differences between two databases. </summary>
        public Differences Differences { get; set; }

        /// <summary> Inspect two database schemas and build the <see cref="Differences"/> model. </summary>
        public void BuildDifferences()
        {
            if (Database1 == null)
            {
                LoadDatabaseSchemas();
            }

            Differences = new Differences
            {
                Database1 = this.Database1.FriendlyName,
                Database2 = this.Database2.FriendlyName,
            };

            OnStatusChanged("Inspecting differences");

            if (options.CompareProperties)
            {
                InspectDatabaseExtendedProperties();
            }

            InspectTables();
            InspectTableDifferences();

            if (options.CompareSynonyms)
            {
                InspectSynonyms();
            }

            if (options.CompareObjects)
            {
                InspectViews();
                InspectRoutines();
            }
        }

        protected virtual void OnStatusChanged(string message)
        {
            var handler = this.ComparisonStatusChanged;
            handler?.Invoke(this, new StatusChangedEventArgs(message));
        }

        private void LoadDatabaseSchemas()
        {
            if (string.IsNullOrEmpty(options.ConnectionString1) || string.IsNullOrEmpty(options.ConnectionString2))
            {
                throw new InvalidOperationException("Connection strings must be set");
            }

            if (options.ConnectionString1.ToLower() == options.ConnectionString2.ToLower())
            {
                throw new InvalidOperationException("Connection strings must be different");
            }

            OnStatusChanged("Inspecting schema for database 1");
            Database1 = new SqlDatabase(options.ConnectionString1, options);
            Database1.PopulateSchemaModel();

            OnStatusChanged("Inspecting schema for database 2");
            Database2 = new SqlDatabase(options.ConnectionString2, options);
            Database2.PopulateSchemaModel();
        }

        private void InspectDatabaseExtendedProperties()
        {
            foreach (var property1 in Database1.ExtendedProperties)
            {
                if (property1.Type == PropertyObjectType.Database)
                {
                    var diff = new ExtendedPropertyDifference(true, false);
                    foreach (var property2 in Database2.ExtendedProperties)
                    {
                        if (property2.FullId == property1.FullId)
                        {
                            diff.ExistsInDatabase2 = true;
                            diff.Value1 = property1.PropertyValue;
                            diff.Value2 = property2.PropertyValue;
                            break;
                        }
                    }

                    Differences.ExtendedPropertyDifferences.Add(property1.PropertyName, diff);
                }
            }

            foreach (var property2 in Database2.ExtendedProperties)
            {
                if (property2.Type == PropertyObjectType.Database && !Differences.ExtendedPropertyDifferences.ContainsKey(property2.PropertyName))
                {
                    Differences.ExtendedPropertyDifferences.Add(property2.PropertyName, new ExtendedPropertyDifference(false, true));
                }
            }
        }

        private void InspectTables()
        {
            foreach (var table1 in Database1.Tables.Keys)
            {
                var diff = new TableDifferenceList(true, false);
                foreach (var table2 in Database2.Tables.Keys)
                {
                    if (table2 == table1)
                    {
                        diff.ExistsInDatabase2 = true;
                        break;
                    }
                }

                Differences.TableDifferences.Add(table1, diff);
            }

            foreach (var table2 in Database2.Tables.Keys)
            {
                if (!Differences.TableDifferences.ContainsKey(table2))
                {
                    Differences.TableDifferences.Add(table2, new TableDifferenceList(false, true));
                }
            }
        }

        private void InspectTableDifferences()
        {
            foreach (var tableName in Differences.TableDifferences.Keys)
            {
                if (Differences.TableDifferences[tableName].ExistsInBothDatabases)
                {
                    if (options.CompareColumns)
                    {
                        InspectTableColumns(tableName);
                    }

                    if (options.CompareIndexes)
                    {
                        InspectIndexes(tableName);
                    }

                    if (options.CompareRelations)
                    {
                        InspectRelations(tableName);
                    }

                    if (options.ComparePermissions)
                    {
                        InspectPermissions(tableName);
                    }

                    if (options.CompareProperties)
                    {
                        InspectTableProperties(tableName);
                    }

                    if (options.CompareTriggers)
                    {
                        InspectTriggers(tableName);
                    }
                }
            }
        }

        private void InspectTableColumns(string tableName)
        {
            foreach (var column1 in Database1.Tables[tableName].ColumnDetails)
            {
                var diff = new TableSubItemWithPropertiesDifferenceList(true, false);
                foreach (var column2 in Database2.Tables[tableName].ColumnDetails)
                {
                    if (column2.ColumnName == column1.ColumnName)
                    {
                        InspectColumns(tableName, diff, column1, column2);
                        break;
                    }
                }

                Differences.TableDifferences[tableName].ColumnDifferences.Add(column1.ColumnName, diff);
            }

            foreach (var column2 in Database2.Tables[tableName].ColumnDetails)
            {
                if (!Differences.TableDifferences[tableName].ColumnDifferences.ContainsKey(column2.ColumnName))
                {
                    Differences.TableDifferences[tableName].ColumnDifferences.Add(column2.ColumnName, new TableSubItemWithPropertiesDifferenceList(false, true));
                }
            }
        }

        private void InspectColumns(string tableName, TableSubItemWithPropertiesDifferenceList diff, SqlColumnDetail column1, SqlColumnDetail column2)
        {
            if (options.CompareOrdinalPositions)
            {
                if (column2.OrdinalPosition != column1.OrdinalPosition)
                {
                    diff.Differences.Add($"ordinal position is different - is [{column1.OrdinalPosition}] in database 1 and is [{column2.OrdinalPosition}] in database 2");
                }
            }

            if (column2.DataType != column1.DataType)
            {
                diff.Differences.Add($"data type is different - Database 1 has type of {column1.DataType.ToUpper()} and database 2 has type of {column2.DataType.ToUpper()}");
            }

            if (column2.CharacterMaximumLength.HasValue && column1.CharacterMaximumLength.HasValue)
            {
                if (column2.CharacterMaximumLength != column1.CharacterMaximumLength)
                {
                    diff.Differences.Add($"max length is different - Database 1 has max length of [{column1.CharacterMaximumLength:n0}] and database 2 has max length of [{column2.CharacterMaximumLength:n0}]");
                }
            }

            if (column2.IsNullable != column1.IsNullable)
            {
                diff.Differences.Add($"is {(column1.IsNullable ? string.Empty : "not ")}allowed null in database 1 and is {(column2.IsNullable ? string.Empty : "not ")}allowed null in database 2");
            }

            if (column2.NumericPrecision.HasValue && column1.NumericPrecision.HasValue)
            {
                if (column2.NumericPrecision.Value != column1.NumericPrecision.Value)
                {
                    diff.Differences.Add($"numeric precision is different - is [{column1.NumericPrecision.Value}] in database 1 and is [{column2.NumericPrecision.Value}] in database 2");
                }
            }

            if (column2.NumericPrecisionRadix.HasValue && column1.NumericPrecisionRadix.HasValue)
            {
                if (column2.NumericPrecisionRadix.Value != column1.NumericPrecisionRadix.Value)
                {
                    diff.Differences.Add($"numeric precision radix is different - is [{column1.NumericPrecisionRadix.Value}] in database 1 and is [{column2.NumericPrecisionRadix.Value}] in database 2");
                }
            }

            if (column2.NumericScale.HasValue && column1.NumericScale.HasValue)
            {
                if (column2.NumericScale.Value != column1.NumericScale.Value)
                {
                    diff.Differences.Add($"numeric scale is different - is [{column1.NumericScale.Value}] in database 1 and is [{column2.NumericScale.Value}] in database 2");
                }
            }

            if (column2.DatetimePrecision.HasValue && column1.DatetimePrecision.HasValue)
            {
                if (column2.DatetimePrecision.Value != column1.DatetimePrecision.Value)
                {
                    diff.Differences.Add($"datetime precision is different - is [{column1.DatetimePrecision.Value}] in database 1 and is [{column2.DatetimePrecision.Value}] in database 2");
                }
            }

            if (column2.ColumnDefault != column1.ColumnDefault)
            {
                diff.Differences.Add($"default value is different - is {column1.ColumnDefault} in database 1 and is {column2.ColumnDefault} in database 2");
            }

            if (column2.CollationName != null && column1.CollationName != null)
            {
                if (column2.CollationName != column1.CollationName)
                {
                    diff.Differences.Add($"collation is different - is [{column1.CollationName}] in database 1 and is [{column2.CollationName}] in database 2");
                }
            }

            if (column2.CharacterSetName != null && column1.CharacterSetName != null)
            {
                if (column2.CharacterSetName != column1.CharacterSetName)
                {
                    diff.Differences.Add($"character set is different - is [{column1.CharacterSetName}] in database 1 and is [{column2.CharacterSetName}] in database 2");
                }
            }

            if (Database2.Tables[tableName].ColumnHasUniqueIndex(column2.ColumnName) != Database1.Tables[tableName].ColumnHasUniqueIndex(column1.ColumnName))
            {
                diff.Differences.Add($"{(Database1.Tables[tableName].ColumnHasUniqueIndex(column1.ColumnName) ? "has" : "does not have")} a unique constraint in database 1 and {(Database2.Tables[tableName].ColumnHasUniqueIndex(column2.ColumnName) ? "has" : "does not have")} a unique constraint in database 2");
            }

            if (column2.IsFullTextIndexed != column1.IsFullTextIndexed)
            {
                diff.Differences.Add($"is{(column1.IsFullTextIndexed ? string.Empty : " not")} full-text indexed in database 1 and is{(column1.IsFullTextIndexed ? string.Empty : " not")} full-text indexed in database 2");
            }

            if (column2.IsComputed != column1.IsComputed)
            {
                diff.Differences.Add($"is{(column1.IsComputed ? string.Empty : " not")} computed in database 1 and is{(column1.IsComputed ? string.Empty : " not")} computed in database 2");
            }

            if (column2.IsIdentity != column1.IsIdentity)
            {
                diff.Differences.Add($"is{(column1.IsIdentity ? string.Empty : " not")} an identity column in database 1 and is{(column1.IsIdentity ? string.Empty : " not")} an identity column in database 2");
            }

            if (column2.IsIdentity && column1.IsIdentity)
            {
                if (column2.IdentitySeed != column1.IdentitySeed)
                {
                    diff.Differences.Add($"identity seed is different - is [{column1.IdentitySeed}] in database 1 and is [{column2.IdentitySeed}] in database 2");
                }

                if (column2.IdentityIncrement != column1.IdentityIncrement)
                {
                    diff.Differences.Add($"identity increment is different - is [{column1.IdentityIncrement}] in database 1 and is [{column2.IdentityIncrement}] in database 2");
                }
            }

            if (column2.IsSparse != column1.IsSparse)
            {
                diff.Differences.Add($"is{(column1.IsSparse ? string.Empty : " not")} sparse in database 1 and is{(column2.IsSparse ? string.Empty : " not")} sparse in database 2");
            }

            if (column2.IsColumnSet != column1.IsColumnSet)
            {
                diff.Differences.Add($"is{(column1.IsColumnSet ? string.Empty : " not")} a column-set in database 1 and is{(column2.IsColumnSet ? string.Empty : " not")} a column-set in database 2");
            }

            if (options.CompareProperties)
            {
                InspectColumnProperties(tableName, column2.ColumnName, diff);
            }

            diff.ExistsInDatabase2 = true;
        }

        private void InspectColumnProperties(string tableName, string columnName, TableSubItemWithPropertiesDifferenceList columnDiff)
        {
            var hasFoundColumn1Description = false;

            foreach (var property1 in Database1.ExtendedProperties)
            {
                if (property1.Type == PropertyObjectType.TableColumn && property1.ObjectName == tableName && property1.ColumnName == columnName)
                {
                    var diff = new ExtendedPropertyDifference(true, false);
                    foreach (var property2 in Database2.ExtendedProperties)
                    {
                        if (property2.FullId == property1.FullId)
                        {
                            diff.ExistsInDatabase2 = true;
                            diff.Value1 = property1.PropertyValue;
                            diff.Value2 = property2.PropertyValue;
                            break;
                        }
                    }

                    if (property1.PropertyName == "MS_Description")
                    {
                        hasFoundColumn1Description = true;
                        if (!diff.ExistsInDatabase2)
                        {
                            columnDiff.Differences.Add("description exists in database 1 and does not exist in database 2");
                        }
                        else if (diff.Value1 != diff.Value2)
                        {
                            columnDiff.Differences.Add($"description is different - is [{diff.Value1}] in database 1 and is [{diff.Value2}] in database 2");
                        }
                    }
                    else
                    {
                        columnDiff.ExtendedPropertyDifferences.Add(property1.PropertyName, diff);
                    }
                }
            }

            foreach (var property2 in Database2.ExtendedProperties)
            {
                if (property2.Type == PropertyObjectType.TableColumn && property2.ObjectName == tableName && property2.ColumnName == columnName)
                {
                    if (property2.PropertyName == "MS_Description")
                    {
                        if (!hasFoundColumn1Description)
                        {
                            columnDiff.Differences.Add("description exists in database 2 and does not exist in database 1");
                        }
                    }
                    else if (!columnDiff.ExtendedPropertyDifferences.ContainsKey(property2.PropertyName))
                    {
                        columnDiff.ExtendedPropertyDifferences.Add(property2.PropertyName, new ExtendedPropertyDifference(false, true));
                    }
                }
            }
        }

        private void InspectIndexes(string tableName)
        {
            foreach (var index1 in Database1.Tables[tableName].Indexes)
            {
                var diff = new TableSubItemWithPropertiesDifferenceList(true, false, index1.ItemType);

                foreach (var index2 in Database2.Tables[tableName].Indexes)
                {
                    if (index2.FullId == index1.FullId)
                    {
                        if (index2.Clustered != index1.Clustered)
                        {
                            diff.Differences.Add($"clustering is different - is{(index1.Clustered ? string.Empty : " not")} clustered in database 1 and is{(index2.Clustered ? string.Empty : " not")} clustered in database 2");
                        }

                        if (index2.Unique != index1.Unique)
                        {
                            diff.Differences.Add($"uniqueness is different - is{(index1.Unique ? string.Empty : " not")} unique in database 1 and is{(index2.Unique ? string.Empty : " not")} unique in database 2");
                        }

                        if (index2.IsUniqueKey != index1.IsUniqueKey)
                        {
                            diff.Differences.Add($"type is different - {(index1.IsUniqueKey ? "unique key" : "index")} in database 1 and {(index2.Unique ? string.Empty : " not")} in database 2");
                        }

                        if (index2.IsPrimaryKey != index1.IsPrimaryKey)
                        {
                            diff.Differences.Add($"primary is different - is{(index1.IsPrimaryKey ? string.Empty : " not")} a primary key in database 1 and is{(index2.IsPrimaryKey ? string.Empty : " not")} a primary key in database 2");
                        }

                        if (index2.FileGroup != index1.FileGroup)
                        {
                            diff.Differences.Add($"filegroup is different - [{index1.FileGroup}] in database 1 and [{index2.FileGroup}] in database 2");
                        }

                        if (index2.ColumnsToString != index1.ColumnsToString)
                        {
                            foreach (var column in index1.Columns.Keys)
                            {
                                if (index2.Columns.ContainsKey(column))
                                {
                                    if (index1.Columns[column] != index2.Columns[column])
                                    {
                                        diff.Differences.Add($"[{column}] ordering is different - {(index1.Columns[column] ? "a" : "de")}scending on database 1 and {(index2.Columns[column] ? "a" : "de")}scending on database 2");
                                    }
                                }
                                else
                                {
                                    diff.Differences.Add($"[{column}] column does not exist in database 2 index");
                                }
                            }

                            foreach (var column in index2.Columns.Keys)
                            {
                                if (!index1.Columns.ContainsKey(column))
                                {
                                    diff.Differences.Add($"[{column}] column does not exist in database 1 index");
                                }
                            }
                        }

                        if (index2.IncludedColumnsToString != index1.IncludedColumnsToString)
                        {
                            foreach (var column in index1.IncludedColumns.Keys)
                            {
                                if (index2.IncludedColumns.ContainsKey(column))
                                {
                                    if (index1.IncludedColumns[column] != index2.IncludedColumns[column])
                                    {
                                        diff.Differences.Add($"[{column}] \"included column\" ordering is different - {(index1.IncludedColumns[column] ? "a" : "de")}scending on database 1 and {(index2.IncludedColumns[column] ? "a" : "de")}scending on database 2");
                                    }
                                }
                                else
                                {
                                    diff.Differences.Add($"[{column}] \"included column\" does not exist in database 2 index");
                                }
                            }

                            foreach (var column in index2.IncludedColumns.Keys)
                            {
                                if (!index1.IncludedColumns.ContainsKey(column))
                                {
                                    diff.Differences.Add($"[{column}] \"included column\" does not exist in database 1 index");
                                }
                            }
                        }

                        if (options.CompareProperties)
                        {
                            InspectIndexProperties(tableName, index2.IndexName, diff);
                        }

                        diff.ExistsInDatabase2 = true;
                        break;
                    }
                }

                Differences.TableDifferences[tableName].IndexDifferences.Add(index1.IndexName, diff);
            }

            foreach (var index2 in Database2.Tables[tableName].Indexes)
            {
                if (!Differences.TableDifferences[tableName].IndexDifferences.ContainsKey(index2.IndexName))
                {
                    Differences.TableDifferences[tableName].IndexDifferences.Add(index2.IndexName, new TableSubItemWithPropertiesDifferenceList(false, true));
                }
            }
        }

        private void InspectIndexProperties(string tableName, string indexName, TableSubItemWithPropertiesDifferenceList indexDiff)
        {
            foreach (var property1 in Database1.ExtendedProperties)
            {
                if (property1.Type == PropertyObjectType.Index && property1.TableName == tableName && property1.IndexName == indexName)
                {
                    var diff = new ExtendedPropertyDifference(true, false);
                    foreach (var property2 in Database2.ExtendedProperties)
                    {
                        if (property2.FullId == property1.FullId)
                        {
                            diff.ExistsInDatabase2 = true;
                            diff.Value1 = property1.PropertyValue;
                            diff.Value2 = property2.PropertyValue;
                            break;
                        }
                    }

                    indexDiff.ExtendedPropertyDifferences.Add(property1.PropertyName, diff);
                }
            }

            foreach (var property2 in Database2.ExtendedProperties)
            {
                if (property2.Type == PropertyObjectType.Index && property2.TableName == tableName && property2.IndexName == indexName)
                {
                    if (!indexDiff.ExtendedPropertyDifferences.ContainsKey(property2.PropertyName))
                    {
                        indexDiff.ExtendedPropertyDifferences.Add(property2.PropertyName, new ExtendedPropertyDifference(false, true));
                    }
                }
            }
        }

        private void InspectRelations(string tableName)
        {
            foreach (var relation1 in Database1.Tables[tableName].Relations)
            {
                var diff = new TableSubItemDifferenceList(true, false);
                foreach (var relation2 in Database2.Tables[tableName].Relations)
                {
                    if (relation2.RelationName == relation1.RelationName)
                    {
                        if (relation2.ChildColumns != relation1.ChildColumns)
                        {
                            diff.Differences.Add($"child column list is different - is \"{relation1.ChildColumns}\" in database 1 and is \"{relation2.ChildColumns}\" in database 2");
                        }

                        if (relation2.ParentColumns != relation1.ParentColumns)
                        {
                            diff.Differences.Add($"parent column list is different - is \"{relation1.ParentColumns}\" in database 1 and is \"{relation2.ParentColumns}\" in database 2");
                        }

                        if (relation2.DeleteRule != relation1.DeleteRule)
                        {
                            diff.Differences.Add($"delete rule is different - is \"{relation1.DeleteRule}\" in database 1 and is \"{relation2.DeleteRule}\" in database 2");
                        }

                        if (relation2.UpdateRule != relation1.UpdateRule)
                        {
                            diff.Differences.Add($"update rule is different - is \"{relation1.UpdateRule}\" in database 1 and is \"{relation2.UpdateRule}\" in database 2");
                        }

                        diff.ExistsInDatabase2 = true;
                        break;
                    }
                }

                Differences.TableDifferences[tableName].RelationshipDifferences.Add(relation1.RelationName, diff);
            }

            foreach (var relation2 in Database2.Tables[tableName].Relations)
            {
                if (!Differences.TableDifferences[tableName].RelationshipDifferences.ContainsKey(relation2.RelationName))
                {
                    Differences.TableDifferences[tableName].RelationshipDifferences.Add(relation2.RelationName, new TableSubItemDifferenceList(false, true));
                }
            }
        }

        private void InspectPermissions(string tableName)
        {
            foreach (var permission1 in Database1.Permissions)
            {
                if (permission1.Type == PermissionObjectType.UserTable && permission1.ObjectName == tableName)
                {
                    var diff = new BaseDifference(true, false);
                    foreach (var permission2 in Database2.Permissions)
                    {
                        if (permission2.FullId == permission1.FullId)
                        {
                            diff.ExistsInDatabase2 = true;
                            break;
                        }
                    }

                    if (!Differences.TableDifferences[tableName].PermissionDifferences.ContainsKey(permission1.ToString()))
                    {
                        Differences.TableDifferences[tableName].PermissionDifferences.Add(permission1.ToString(), diff);
                    }
                }
            }

            foreach (var permission2 in Database2.Permissions)
            {
                if (permission2.Type == PermissionObjectType.UserTable && permission2.ObjectName == tableName)
                {
                    if (!Differences.TableDifferences[tableName].PermissionDifferences.ContainsKey(permission2.ToString()))
                    {
                        Differences.TableDifferences[tableName].PermissionDifferences.Add(permission2.ToString(), new BaseDifference(false, true));
                    }
                }
            }
        }

        private void InspectTableProperties(string tableName)
        {
            foreach (var property1 in Database1.ExtendedProperties)
            {
                if (property1.Type == PropertyObjectType.Table && property1.TableName == tableName)
                {
                    var diff = new ExtendedPropertyDifference(true, false);
                    foreach (var property2 in Database2.ExtendedProperties)
                    {
                        if (property2.FullId == property1.FullId)
                        {
                            diff.ExistsInDatabase2 = true;
                            diff.Value1 = property1.PropertyValue;
                            diff.Value2 = property2.PropertyValue;
                            break;
                        }
                    }

                    Differences.TableDifferences[tableName].ExtendedPropertyDifferences.Add(property1.PropertyName, diff);
                }
            }

            foreach (var property2 in Database2.ExtendedProperties)
            {
                if (property2.Type == PropertyObjectType.Table && property2.TableName == tableName)
                {
                    if (!Differences.TableDifferences[tableName].ExtendedPropertyDifferences.ContainsKey(property2.PropertyName))
                    {
                        Differences.TableDifferences[tableName].ExtendedPropertyDifferences.Add(property2.PropertyName, new ExtendedPropertyDifference(false, true));
                    }
                }
            }
        }

        private void InspectTriggers(string tableName)
        {
            foreach (var trigger1 in Database1.Tables[tableName].Triggers)
            {
                var diff = new TableSubItemDifferenceList(true, false);
                foreach (var trigger2 in Database2.Tables[tableName].Triggers)
                {
                    if (trigger2.TableName == tableName && trigger2.TriggerName == trigger1.TriggerName)
                    {
                        if (trigger2.FileGroup != trigger1.FileGroup)
                        {
                            diff.Differences.Add($"filegroup is different - is {trigger1.FileGroup} in database 1 and is {trigger2.FileGroup} in database 2");
                        }

                        if (trigger2.TriggerOwner != trigger1.TriggerOwner)
                        {
                            diff.Differences.Add($"owner is different - is {trigger1.TriggerOwner} in database 1 and is {trigger2.TriggerOwner} in database 2");
                        }

                        if (trigger2.IsUpdate != trigger1.IsUpdate)
                        {
                            diff.Differences.Add($"update is different - is {(trigger1.IsUpdate ? string.Empty : "not ")}update in database 1 and is {(trigger2.IsUpdate ? string.Empty : "not ")}update in database 2");
                        }

                        if (trigger2.IsDelete != trigger1.IsDelete)
                        {
                            diff.Differences.Add($"delete is different - is {(trigger1.IsDelete ? string.Empty : "not ")}delete in database 1 and is {(trigger2.IsDelete ? string.Empty : "not ")}delete in database 2");
                        }

                        if (trigger2.IsInsert != trigger1.IsInsert)
                        {
                            diff.Differences.Add($"insert is different - is {(trigger1.IsInsert ? string.Empty : "not ")}insert in database 1 and is {(trigger2.IsInsert ? string.Empty : "not ")}insert in database 2");
                        }

                        if (trigger2.IsAfter != trigger1.IsAfter)
                        {
                            diff.Differences.Add($"after is different - is {(trigger1.IsAfter ? string.Empty : "not ")}after in database 1 and is {(trigger2.IsAfter ? string.Empty : "not ")}after in database 2");
                        }

                        if (trigger2.IsInsteadOf != trigger1.IsInsteadOf)
                        {
                            diff.Differences.Add($"instead-of is different - is {(trigger1.IsInsteadOf ? string.Empty : "not ")}instead-of in database 1 and is {(trigger2.IsInsteadOf ? string.Empty : "not ")}instead-of in database 2");
                        }

                        if (trigger2.IsDisabled != trigger1.IsDisabled)
                        {
                            diff.Differences.Add($"disabled is different - is {(trigger1.IsDisabled ? string.Empty : "not ")}disabled in database 1 and is {(trigger2.IsDisabled ? string.Empty : "not ")}disabled in database 2");
                        }

                        if (BaseDifference.CleanDefinitionText(trigger1.TriggerContent, true) != BaseDifference.CleanDefinitionText(trigger2.TriggerContent, true))
                        {
                            diff.Differences.Add("definitions are different");
                        }

                        diff.ExistsInDatabase2 = true;
                        break;
                    }
                }

                Differences.TableDifferences[tableName].TriggerDifferences.Add(trigger1.TriggerName, diff);
            }

            foreach (var trigger2 in Database2.Tables[tableName].Triggers)
            {
                if (!Differences.TableDifferences[tableName].TriggerDifferences.ContainsKey(trigger2.TriggerName))
                {
                    Differences.TableDifferences[tableName].TriggerDifferences.Add(trigger2.TriggerName, new TableSubItemDifferenceList(false, true));
                }
            }
        }

        private void InspectSynonyms()
        {
            foreach (var synonym1 in Database1.Synonyms.Keys)
            {
                var diff = new DatabaseObjectDifferenceList(true, false);
                foreach (var synonym2 in Database2.Synonyms.Keys)
                {
                    if (synonym2 == synonym1)
                    {
                        if (this.options.CompareProperties)
                        {
                            InspectObjectProperties(synonym2, diff);
                        }

                        if (this.options.ComparePermissions)
                        {
                            InspectObjectPermissions(synonym2, PermissionObjectType.Synonym, diff);
                        }

                        diff.ObjectDefinition1 = Database1.Synonyms[synonym2];
                        diff.ObjectDefinition2 = Database2.Synonyms[synonym2];

                        diff.ExistsInDatabase2 = true;
                        break;
                    }
                }

                Differences.SynonymDifferences.Add(synonym1, diff);
            }

            foreach (var synonym2 in Database2.Synonyms.Keys)
            {
                if (!Differences.SynonymDifferences.ContainsKey(synonym2))
                {
                    Differences.SynonymDifferences.Add(synonym2, new DatabaseObjectDifferenceList(false, true));
                }
            }
        }

        private void InspectViews()
        {
            foreach (var view1 in Database1.Views.Keys)
            {
                var diff = new DatabaseObjectDifferenceList(true, false);
                foreach (var view2 in Database2.Views.Keys)
                {
                    if (view2 == view1)
                    {
                        if (this.options.CompareProperties)
                        {
                            InspectObjectProperties(view2, diff);
                        }

                        if (this.options.ComparePermissions)
                        {
                            InspectObjectPermissions(view2, PermissionObjectType.View, diff);
                        }

                        diff.ObjectDefinition1 = Database1.Synonyms[view2];
                        diff.ObjectDefinition2 = Database2.Synonyms[view2];

                        diff.ExistsInDatabase2 = true;
                        break;
                    }
                }

                Differences.ViewDifferences.Add(view1, diff);
            }

            foreach (var view2 in Database2.Views.Keys)
            {
                if (!Differences.ViewDifferences.ContainsKey(view2))
                {
                    Differences.ViewDifferences.Add(view2, new DatabaseObjectDifferenceList(false, true));
                }
            }
        }

        private void InspectRoutines()
        {
            foreach (var routine1 in Database1.UserRoutines.Keys)
            {
                var diff = new DatabaseObjectDifferenceList(true, false);
                var isFunction = Database1.UserRoutines[routine1].RoutineType.ToLower() == "function";
                foreach (var routine2 in Database2.UserRoutines.Keys)
                {
                    if (routine2 == routine1)
                    {
                        if (this.options.CompareProperties)
                        {
                            InspectObjectProperties(routine2, diff);
                        }

                        if (this.options.ComparePermissions)
                        {
                            InspectObjectPermissions(routine2, isFunction ? PermissionObjectType.SqlFunction : PermissionObjectType.SqlStoredProcedure, diff);
                        }

                        diff.ObjectDefinition1 = Database1.Synonyms[routine2];
                        diff.ObjectDefinition2 = Database2.Synonyms[routine2];

                        diff.ExistsInDatabase2 = true;
                        break;
                    }
                }

                if (isFunction)
                {
                    Differences.FunctionDifferences.Add(routine1, diff);
                }
                else
                {
                    Differences.StoredProcedureDifferences.Add(routine1, diff);
                }
            }

            foreach (var routine2 in Database2.UserRoutines.Keys)
            {
                if (Database2.UserRoutines[routine2].RoutineType.ToLower() == "function")
                {
                    if (!Differences.FunctionDifferences.ContainsKey(routine2))
                    {
                        Differences.FunctionDifferences.Add(routine2, new DatabaseObjectDifferenceList(false, true));
                    }
                }
                else
                {
                    if (!Differences.StoredProcedureDifferences.ContainsKey(routine2))
                    {
                        Differences.StoredProcedureDifferences.Add(routine2, new DatabaseObjectDifferenceList(false, true));
                    }
                }
            }
        }

        private void InspectObjectProperties(string objectName, DatabaseObjectDifferenceList objectDiff)
        {
            foreach (var property1 in Database1.ExtendedProperties)
            {
                if (property1.Type == PropertyObjectType.Routine && property1.ObjectName == objectName)
                {
                    var diff = new ExtendedPropertyDifference(true, false);
                    foreach (var property2 in Database2.ExtendedProperties)
                    {
                        if (property2.FullId == property1.FullId)
                        {
                            diff.ExistsInDatabase2 = true;
                            diff.Value1 = property1.PropertyValue;
                            diff.Value2 = property2.PropertyValue;
                            break;
                        }
                    }

                    objectDiff.ExtendedPropertyDifferences.Add(property1.PropertyName, diff);
                }
            }

            foreach (var property2 in Database2.ExtendedProperties)
            {
                if (property2.Type == PropertyObjectType.Routine && property2.ObjectName == objectName)
                {
                    if (!objectDiff.ExtendedPropertyDifferences.ContainsKey(property2.PropertyName))
                    {
                        objectDiff.ExtendedPropertyDifferences.Add(property2.PropertyName, new ExtendedPropertyDifference(false, true));
                    }
                }
            }
        }

        private void InspectObjectPermissions(string objectName, PermissionObjectType objectType, DatabaseObjectDifferenceList objectDiff)
        {
            foreach (var permission1 in Database1.Permissions)
            {
                if (permission1.Type == objectType && permission1.ObjectName == objectName)
                {
                    var diff = new BaseDifference(true, false);
                    foreach (var permission2 in Database2.Permissions)
                    {
                        if (permission2.FullId == permission1.FullId)
                        {
                            diff.ExistsInDatabase2 = true;
                            break;
                        }
                    }

                    objectDiff.PermissionDifferences.Add(permission1.ToString(), diff);
                }
            }

            foreach (var permission2 in Database2.Permissions)
            {
                if (permission2.Type == objectType && permission2.ObjectName == objectName)
                {
                    if (!objectDiff.PermissionDifferences.ContainsKey(permission2.ToString()))
                    {
                        objectDiff.PermissionDifferences.Add(permission2.ToString(), new BaseDifference(false, true));
                    }
                }
            }
        }
    }
}
