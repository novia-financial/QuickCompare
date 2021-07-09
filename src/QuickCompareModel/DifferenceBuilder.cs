namespace QuickCompareModel
{
    using System;
    using Microsoft.Extensions.Options;
    using QuickCompareModel.DatabaseDifferences;
    using QuickCompareModel.DatabaseSchema;

    public class DifferenceBuilder
    {
        private readonly QuickCompareOptions options;

        public DifferenceBuilder(IOptions<QuickCompareOptions> options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this.options = options.Value;
        }

        internal SqlDatabase Database1 { get; set; }

        internal SqlDatabase Database2 { get; set; }

        public Differences Differences { get; set; }

        public string GetDifferenceReport()
        {
            GetDatabaseSchemas();

            Differences = new Differences
            {
                Database1 = this.Database1.FriendlyName,
                Database2 = this.Database2.FriendlyName,
            };

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
                InspectRoutines();
            }

            return Differences.ToString();
        }

        private void InspectDatabaseExtendedProperties()
        {
            foreach (var property1 in Database1.SqlExtendedProperties)
            {
                if (property1.TYPE == PROPERTY_OBJECT_TYPE.DATABASE)
                {
                    var diff = new ExtendedPropertyDifference(true, false);
                    foreach (var property2 in Database2.SqlExtendedProperties)
                    {
                        if (property2.FULL_ID == property1.FULL_ID)
                        {
                            diff.ExistsInDatabase2 = true;
                            diff.Value1 = property1.PROPERTY_VALUE;
                            diff.Value2 = property2.PROPERTY_VALUE;
                            break;
                        }
                    }

                    Differences.ExtendedPropertyDifferences.Add(property1.PROPERTY_NAME, diff);
                }
            }

            foreach (var property2 in Database2.SqlExtendedProperties)
            {
                if (property2.TYPE == PROPERTY_OBJECT_TYPE.DATABASE && !Differences.ExtendedPropertyDifferences.ContainsKey(property2.PROPERTY_NAME))
                {
                    Differences.ExtendedPropertyDifferences.Add(property2.PROPERTY_NAME, new ExtendedPropertyDifference(false, true));
                }
            }
        }

        private void InspectTables()
        {
            foreach (var table1 in Database1.SqlTables.Keys)
            {
                var diff = new TableDifferenceList(true, false);
                foreach (var table2 in Database2.SqlTables.Keys)
                {
                    if (table2 == table1)
                    {
                        diff.ExistsInDatabase2 = true;
                        break;
                    }
                }

                foreach (var table2 in Database2.SqlTables.Keys)
                {
                    if (!Differences.TableDifferences.ContainsKey(table2))
                    {
                        Differences.TableDifferences.Add(table2, new TableDifferenceList(false, true));
                    }
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
            foreach (var column1 in Database1.SqlTables[tableName].ColumnDetails)
            {
                var diff = new TableSubItemWithPropertiesDifferenceList(true, false);
                foreach (var column2 in Database2.SqlTables[tableName].ColumnDetails)
                {
                    if (column2.COLUMN_NAME == column1.COLUMN_NAME)
                    {
                        InspectColumns(tableName, diff, column1, column2);
                        break;
                    }
                }

                Differences.TableDifferences[tableName].ColumnDifferences.Add(column1.COLUMN_NAME, diff);
            }

            foreach (var column2 in Database2.SqlTables[tableName].ColumnDetails)
            {
                if (!Differences.TableDifferences[tableName].ColumnDifferences.ContainsKey(column2.COLUMN_NAME))
                {
                    Differences.TableDifferences[tableName].ColumnDifferences.Add(column2.COLUMN_NAME, new TableSubItemWithPropertiesDifferenceList(false, true));
                }
            }
        }

        private void InspectColumns(string tableName, TableSubItemWithPropertiesDifferenceList diff, SqlColumnDetail column1, SqlColumnDetail column2)
        {
            if (options.CompareOrdinalPositions)
            {
                if (column2.ORDINAL_POSITION != column1.ORDINAL_POSITION)
                {
                    diff.Differences.Add($"ordinal position is different - is [{column1.ORDINAL_POSITION}] in database 1 and is [{column2.ORDINAL_POSITION}] in database 2");
                }
            }

            if (column2.DATA_TYPE != column1.DATA_TYPE)
            {
                diff.Differences.Add($"data type is different - Database 1 has type of {column1.DATA_TYPE.ToUpper()} and database 2 has type of {column2.DATA_TYPE.ToUpper()}");
            }

            if (column2.CHARACTER_MAXIMUM_LENGTH.HasValue && column1.CHARACTER_MAXIMUM_LENGTH.HasValue)
            {
                if (column2.CHARACTER_MAXIMUM_LENGTH != column1.CHARACTER_MAXIMUM_LENGTH)
                {
                    diff.Differences.Add($"max length is different - Database 1 has max length of [{column1.CHARACTER_MAXIMUM_LENGTH:n0}] and database 2 has max length of [{column2.CHARACTER_MAXIMUM_LENGTH:n0}]");
                }
            }

            if (column2.IS_NULLABLE != column1.IS_NULLABLE)
            {
                diff.Differences.Add($"is {(column1.IS_NULLABLE ? string.Empty : "not ")}allowed null in database 1 and is {(column2.IS_NULLABLE ? string.Empty : "not ")}allowed null in database 2");
            }

            if (column2.NUMERIC_PRECISION.HasValue && column1.NUMERIC_PRECISION.HasValue)
            {
                if (column2.NUMERIC_PRECISION.Value != column1.NUMERIC_PRECISION.Value)
                {
                    diff.Differences.Add($"numeric precision is different - is [{column1.NUMERIC_PRECISION.Value}] in database 1 and is [{column2.NUMERIC_PRECISION.Value}] in database 2");
                }
            }

            if (column2.NUMERIC_PRECISION_RADIX.HasValue && column1.NUMERIC_PRECISION_RADIX.HasValue)
            {
                if (column2.NUMERIC_PRECISION_RADIX.Value != column1.NUMERIC_PRECISION_RADIX.Value)
                {
                    diff.Differences.Add($"numeric precision radix is different - is [{column1.NUMERIC_PRECISION_RADIX.Value}] in database 1 and is [{column2.NUMERIC_PRECISION_RADIX.Value}] in database 2");
                }
            }

            if (column2.NUMERIC_SCALE.HasValue && column1.NUMERIC_SCALE.HasValue)
            {
                if (column2.NUMERIC_SCALE.Value != column1.NUMERIC_SCALE.Value)
                {
                    diff.Differences.Add($"numeric scale is different - is [{column1.NUMERIC_SCALE.Value}] in database 1 and is [{column2.NUMERIC_SCALE.Value}] in database 2");
                }
            }

            if (column2.DATETIME_PRECISION.HasValue && column1.DATETIME_PRECISION.HasValue)
            {
                if (column2.DATETIME_PRECISION.Value != column1.DATETIME_PRECISION.Value)
                {
                    diff.Differences.Add($"datetime precision is different - is [{column1.DATETIME_PRECISION.Value}] in database 1 and is [{column2.DATETIME_PRECISION.Value}] in database 2");
                }
            }

            if (column2.COLUMN_DEFAULT != column1.COLUMN_DEFAULT)
            {
                diff.Differences.Add($"default value is different - is {column1.COLUMN_DEFAULT} in database 1 and is {column2.COLUMN_DEFAULT} in database 2");
            }

            if (column2.COLLATION_NAME != null && column1.COLLATION_NAME != null)
            {
                if (column2.COLLATION_NAME != column1.COLLATION_NAME)
                {
                    diff.Differences.Add($"collation is different - is [{column1.COLLATION_NAME}] in database 1 and is [{column2.COLLATION_NAME}] in database 2");
                }
            }

            if (column2.CHARACTER_SET_NAME != null && column1.CHARACTER_SET_NAME != null)
            {
                if (column2.CHARACTER_SET_NAME != column1.CHARACTER_SET_NAME)
                {
                    diff.Differences.Add($"character set is different - is [{column1.CHARACTER_SET_NAME}] in database 1 and is [{column2.CHARACTER_SET_NAME}] in database 2");
                }
            }

            if (Database2.SqlTables[tableName].ColumnHasUniqueIndex(column2.COLUMN_NAME) != Database1.SqlTables[tableName].ColumnHasUniqueIndex(column1.COLUMN_NAME))
            {
                diff.Differences.Add($"{(Database1.SqlTables[tableName].ColumnHasUniqueIndex(column1.COLUMN_NAME) ? "has" : "does not have")} a unique constraint in database 1 and {(Database2.SqlTables[tableName].ColumnHasUniqueIndex(column2.COLUMN_NAME) ? "has" : "does not have")} a unique constraint in database 2");
            }

            if (column2.IS_FULL_TEXT_INDEXED != column1.IS_FULL_TEXT_INDEXED)
            {
                diff.Differences.Add($"is{(column1.IS_FULL_TEXT_INDEXED ? string.Empty : " not")} full-text indexed in database 1 and is{(column1.IS_FULL_TEXT_INDEXED ? string.Empty : " not")} full-text indexed in database 2");
            }

            if (column2.IS_COMPUTED != column1.IS_COMPUTED)
            {
                diff.Differences.Add($"is{(column1.IS_COMPUTED ? string.Empty : " not")} computed in database 1 and is{(column1.IS_COMPUTED ? string.Empty : " not")} computed in database 2");
            }

            if (column2.IS_IDENTITY != column1.IS_IDENTITY)
            {
                diff.Differences.Add($"is{(column1.IS_IDENTITY ? string.Empty : " not")} an identity column in database 1 and is{(column1.IS_IDENTITY ? string.Empty : " not")} an identity column in database 2");
            }

            if (column2.IS_IDENTITY && column1.IS_IDENTITY)
            {
                if (column2.IDENTITY_SEED != column1.IDENTITY_SEED)
                {
                    diff.Differences.Add($"identity seed is different - is [{column1.IDENTITY_SEED}] in database 1 and is [{column2.IDENTITY_SEED}] in database 2");
                }

                if (column2.IDENTITY_INCREMENT != column1.IDENTITY_INCREMENT)
                {
                    diff.Differences.Add($"identity increment is different - is [{column1.IDENTITY_INCREMENT}] in database 1 and is [{column2.IDENTITY_INCREMENT}] in database 2");
                }
            }

            if (column2.IS_SPARSE != column1.IS_SPARSE)
            {
                diff.Differences.Add($"is{(column1.IS_SPARSE ? string.Empty : " not")} sparse in database 1 and is{(column2.IS_SPARSE ? string.Empty : " not")} sparse in database 2");
            }

            if (column2.IS_COLUMN_SET != column1.IS_COLUMN_SET)
            {
                diff.Differences.Add($"is{(column1.IS_COLUMN_SET ? string.Empty : " not")} a column-set in database 1 and is{(column2.IS_COLUMN_SET ? string.Empty : " not")} a column-set in database 2");
            }

            if (options.CompareProperties)
            {
                InspectColumnProperties(tableName, column2.COLUMN_NAME, diff);
            }

            diff.ExistsInDatabase2 = true;
        }

        private void InspectColumnProperties(string tableName, string columnName, TableSubItemWithPropertiesDifferenceList columnDiff)
        {
            var hasFoundColumn1Description = false;

            foreach (var property1 in Database1.SqlExtendedProperties)
            {
                if (property1.TYPE == PROPERTY_OBJECT_TYPE.TABLE_COLUMN && property1.OBJECT_NAME == tableName && property1.COLUMN_NAME == columnName)
                {
                    var diff = new ExtendedPropertyDifference(true, false);
                    foreach (var property2 in Database2.SqlExtendedProperties)
                    {
                        if (property2.FULL_ID == property1.FULL_ID)
                        {
                            diff.ExistsInDatabase2 = true;
                            diff.Value1 = property1.PROPERTY_VALUE;
                            diff.Value2 = property2.PROPERTY_VALUE;
                            break;
                        }
                    }

                    if (property1.PROPERTY_NAME == "MS_Description")
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
                        columnDiff.ExtendedPropertyDifferences.Add(property1.PROPERTY_NAME, diff);
                    }
                }
            }

            foreach (var property2 in Database2.SqlExtendedProperties)
            {
                if (property2.TYPE == PROPERTY_OBJECT_TYPE.TABLE_COLUMN && property2.OBJECT_NAME == tableName && property2.COLUMN_NAME == columnName)
                {
                    if (property2.PROPERTY_NAME == "MS_Description")
                    {
                        if (!hasFoundColumn1Description)
                        {
                            columnDiff.Differences.Add("description exists in database 2 and does not exist in database 1");
                        }
                    }
                    else if (!columnDiff.ExtendedPropertyDifferences.ContainsKey(property2.PROPERTY_NAME))
                    {
                        columnDiff.ExtendedPropertyDifferences.Add(property2.PROPERTY_NAME, new ExtendedPropertyDifference(false, true));
                    }
                }
            }
        }

        private void InspectIndexes(string tableName)
        {
            foreach (var index1 in Database1.SqlTables[tableName].Indexes)
            {
                var diff = new TableSubItemWithPropertiesDifferenceList(true, false, index1.ITEM_TYPE);

                foreach (var index2 in Database2.SqlTables[tableName].Indexes)
                {
                    if (index2.FULL_ID == index1.FULL_ID)
                    {
                        if (index2.CLUSTERED != index1.CLUSTERED)
                        {
                            diff.Differences.Add($"clustering is different - is{(index1.CLUSTERED ? string.Empty : " not")} clustered in database 1 and is{(index2.CLUSTERED ? string.Empty : " not")} clustered in database 2");
                        }

                        if (index2.UNIQUE != index1.UNIQUE)
                        {
                            diff.Differences.Add($"uniqueness is different - is{(index1.UNIQUE ? string.Empty : " not")} unique in database 1 and is{(index2.UNIQUE ? string.Empty : " not")} unique in database 2");
                        }

                        if (index2.IS_UNIQUE_KEY != index1.IS_UNIQUE_KEY)
                        {
                            diff.Differences.Add($"type is different - {(index1.IS_UNIQUE_KEY ? "unique key" : "index")} in database 1 and {(index2.UNIQUE ? string.Empty : " not")} in database 2");
                        }

                        if (index2.IS_PRIMARY_KEY != index1.IS_PRIMARY_KEY)
                        {
                            diff.Differences.Add($"primary is different - is{(index1.IS_PRIMARY_KEY ? string.Empty : " not")} a primary key in database 1 and is{(index2.IS_PRIMARY_KEY ? string.Empty : " not")} a primary key in database 2");
                        }

                        if (index2.FILEGROUP != index1.FILEGROUP)
                        {
                            diff.Differences.Add($"filegroup is different - [{index1.FILEGROUP}] in database 1 and [{index2.FILEGROUP}] in database 2");
                        }

                        if (index2.COLUMNS_ToString != index1.COLUMNS_ToString)
                        {
                            foreach (var column in index1.COLUMNS.Keys)
                            {
                                if (index2.COLUMNS.ContainsKey(column))
                                {
                                    if (index1.COLUMNS[column] != index2.COLUMNS[column])
                                    {
                                        diff.Differences.Add($"[{column}] ordering is different - {(index1.COLUMNS[column] ? "a" : "de")}scending on database 1 and {(index2.COLUMNS[column] ? "a" : "de")}scending on database 2");
                                    }
                                }
                                else
                                {
                                    diff.Differences.Add($"[{column}] column does not exist in database 2 index");
                                }
                            }

                            foreach (var column in index2.COLUMNS.Keys)
                            {
                                if (!index1.COLUMNS.ContainsKey(column))
                                {
                                    diff.Differences.Add($"[{column}] column does not exist in database 1 index");
                                }
                            }
                        }

                        if (index2.INCLUDED_COLUMNS_ToString != index1.INCLUDED_COLUMNS_ToString)
                        {
                            foreach (var column in index1.INCLUDED_COLUMNS.Keys)
                            {
                                if (index2.INCLUDED_COLUMNS.ContainsKey(column))
                                {
                                    if (index1.INCLUDED_COLUMNS[column] != index2.INCLUDED_COLUMNS[column])
                                    {
                                        diff.Differences.Add($"[{column}] \"included column\" ordering is different - {(index1.INCLUDED_COLUMNS[column] ? "a" : "de")}scending on database 1 and {(index2.INCLUDED_COLUMNS[column] ? "a" : "de")}scending on database 2");
                                    }
                                }
                                else
                                {
                                    diff.Differences.Add($"[{column}] \"included column\" does not exist in database 2 index");
                                }
                            }

                            foreach (var column in index2.INCLUDED_COLUMNS.Keys)
                            {
                                if (!index1.INCLUDED_COLUMNS.ContainsKey(column))
                                {
                                    diff.Differences.Add($"[{column}] \"included column\" does not exist in database 1 index");
                                }
                            }
                        }

                        if (options.CompareProperties)
                        {
                            InspectIndexProperties(tableName, index2.INDEX_NAME, diff);
                        }

                        diff.ExistsInDatabase2 = true;
                        break;
                    }
                }
            }
        }

        private void InspectIndexProperties(string tableName, string indexName, TableSubItemWithPropertiesDifferenceList indexDiff)
        {
            foreach (var property1 in Database1.SqlExtendedProperties)
            {
                if (property1.TYPE == PROPERTY_OBJECT_TYPE.INDEX && property1.TABLE_NAME == tableName && property1.INDEX_NAME == indexName)
                {
                    var diff = new ExtendedPropertyDifference(true, false);
                    foreach (var property2 in Database2.SqlExtendedProperties)
                    {
                        if (property2.FULL_ID == property1.FULL_ID)
                        {
                            diff.ExistsInDatabase2 = true;
                            diff.Value1 = property1.PROPERTY_VALUE;
                            diff.Value2 = property2.PROPERTY_VALUE;
                            break;
                        }
                    }

                    indexDiff.ExtendedPropertyDifferences.Add(property1.PROPERTY_NAME, diff);
                }
            }

            foreach (var property2 in Database2.SqlExtendedProperties)
            {
                if (property2.TYPE == PROPERTY_OBJECT_TYPE.INDEX && property2.TABLE_NAME == tableName && property2.INDEX_NAME == indexName)
                {
                    if (!indexDiff.ExtendedPropertyDifferences.ContainsKey(property2.PROPERTY_NAME))
                    {
                        indexDiff.ExtendedPropertyDifferences.Add(property2.PROPERTY_NAME, new ExtendedPropertyDifference(false, true));
                    }
                }
            }
        }

        private void InspectRelations(string tableName)
        {
            foreach (var relation1 in Database1.SqlTables[tableName].Relations)
            {
                var diff = new TableSubItemDifferenceList(true, false);
                foreach (var relation2 in Database2.SqlTables[tableName].Relations)
                {
                    if (relation2.RELATION_NAME == relation1.RELATION_NAME)
                    {
                        if (relation2.CHILD_COLUMNS != relation1.CHILD_COLUMNS)
                        {
                            diff.Differences.Add($"child column list is different - is \"{relation1.CHILD_COLUMNS}\" in database 1 and is \"{relation2.CHILD_COLUMNS}\" in database 2");
                        }

                        if (relation2.PARENT_COLUMNS != relation1.PARENT_COLUMNS)
                        {
                            diff.Differences.Add($"parent column list is different - is \"{relation1.PARENT_COLUMNS}\" in database 1 and is \"{relation2.PARENT_COLUMNS}\" in database 2");
                        }

                        if (relation2.DELETE_RULE != relation1.DELETE_RULE)
                        {
                            diff.Differences.Add($"delete rule is different - is \"{relation1.DELETE_RULE}\" in database 1 and is \"{relation2.DELETE_RULE}\" in database 2");
                        }

                        if (relation2.UPDATE_RULE != relation1.UPDATE_RULE)
                        {
                            diff.Differences.Add($"update rule is different - is \"{relation1.UPDATE_RULE}\" in database 1 and is \"{relation2.UPDATE_RULE}\" in database 2");
                        }

                        diff.ExistsInDatabase2 = true;
                        break;
                    }
                }

                Differences.TableDifferences[tableName].RelationshipDifferences.Add(relation1.RELATION_NAME, diff);
            }

            foreach (var relation2 in Database2.SqlTables[tableName].Relations)
            {
                if (!Differences.TableDifferences[tableName].RelationshipDifferences.ContainsKey(relation2.RELATION_NAME))
                {
                    Differences.TableDifferences[tableName].RelationshipDifferences.Add(relation2.RELATION_NAME, new TableSubItemDifferenceList(false, true));
                }
            }
        }

        private void InspectPermissions(string tableName)
        {
            foreach (var permission1 in Database1.SqlPermissions)
            {
                if (permission1.TYPE == PERMISSION_OBJECT_TYPE.USER_TABLE && permission1.OBJECT_NAME == tableName)
                {
                    var diff = new BaseDifference(true, false);
                    foreach (var permission2 in Database2.SqlPermissions)
                    {
                        if (permission2.FULL_ID == permission1.FULL_ID)
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

            foreach (var permission2 in Database2.SqlPermissions)
            {
                if (permission2.TYPE == PERMISSION_OBJECT_TYPE.USER_TABLE && permission2.OBJECT_NAME == tableName)
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
            foreach (var property1 in Database1.SqlExtendedProperties)
            {
                if (property1.TYPE == PROPERTY_OBJECT_TYPE.TABLE && property1.TABLE_NAME == tableName)
                {
                    var diff = new ExtendedPropertyDifference(true, false);
                    foreach (var property2 in Database2.SqlExtendedProperties)
                    {
                        if (property2.FULL_ID == property1.FULL_ID)
                        {
                            diff.ExistsInDatabase2 = true;
                            diff.Value1 = property1.PROPERTY_VALUE;
                            diff.Value2 = property2.PROPERTY_VALUE;
                            break;
                        }
                    }

                    Differences.TableDifferences[tableName].ExtendedPropertyDifferences.Add(property1.PROPERTY_NAME, diff);
                }
            }

            foreach (var property2 in Database2.SqlExtendedProperties)
            {
                if (property2.TYPE == PROPERTY_OBJECT_TYPE.TABLE && property2.TABLE_NAME == tableName)
                {
                    if (!Differences.TableDifferences[tableName].ExtendedPropertyDifferences.ContainsKey(property2.PROPERTY_NAME))
                    {
                        Differences.TableDifferences[tableName].ExtendedPropertyDifferences.Add(property2.PROPERTY_NAME, new ExtendedPropertyDifference(false, true));
                    }
                }
            }
        }

        private void InspectTriggers(string tableName)
        {
            foreach (var trigger1 in Database1.SqlTables[tableName].Triggers)
            {
                var diff = new TableSubItemDifferenceList(true, false);
                foreach (var trigger2 in Database2.SqlTables[tableName].Triggers)
                {
                    if (trigger2.TABLE_NAME == tableName && trigger2.TRIGGER_NAME == trigger1.TRIGGER_NAME)
                    {
                        if (trigger2.IS_UPDATE != trigger1.IS_UPDATE)
                        {
                            diff.Differences.Add($"update is different - is {(trigger1.IS_UPDATE ? string.Empty : "not ")}update in database 1 and is {(trigger2.IS_UPDATE ? string.Empty : "not ")}update in database 2");
                        }

                        if (trigger2.IS_DELETE != trigger1.IS_DELETE)
                        {
                            diff.Differences.Add($"delete is different - is {(trigger1.IS_DELETE ? string.Empty : "not ")}delete in database 1 and is {(trigger2.IS_DELETE ? string.Empty : "not ")}delete in database 2");
                        }

                        if (trigger2.IS_INSERT != trigger1.IS_INSERT)
                        {
                            diff.Differences.Add($"insert is different - is {(trigger1.IS_INSERT ? string.Empty : "not ")}insert in database 1 and is {(trigger2.IS_INSERT ? string.Empty : "not ")}insert in database 2");
                        }

                        if (trigger2.IS_AFTER != trigger1.IS_AFTER)
                        {
                            diff.Differences.Add($"after is different - is {(trigger1.IS_AFTER ? string.Empty : "not ")}after in database 1 and is {(trigger2.IS_AFTER ? string.Empty : "not ")}after in database 2");
                        }

                        if (trigger2.IS_INSTEAD_OF != trigger1.IS_INSTEAD_OF)
                        {
                            diff.Differences.Add($"instead-of is different - is {(trigger1.IS_INSTEAD_OF ? string.Empty : "not ")}instead-of in database 1 and is {(trigger2.IS_INSTEAD_OF ? string.Empty : "not ")}instead-of in database 2");
                        }

                        if (trigger2.IS_DISABLED != trigger1.IS_DISABLED)
                        {
                            diff.Differences.Add($"disabled is different - is {(trigger1.IS_DISABLED ? string.Empty : "not ")}disabled in database 1 and is {(trigger2.IS_DISABLED ? string.Empty : "not ")}disabled in database 2");
                        }

                        if (BaseDifference.CleanDefinitionText(trigger1.TRIGGER_CONTENT, true) != BaseDifference.CleanDefinitionText(trigger2.TRIGGER_CONTENT, true))
                        {
                            diff.Differences.Add("definitions are different");
                        }

                        diff.ExistsInDatabase2 = true;
                        break;
                    }
                }

                Differences.TableDifferences[tableName].TriggerDifferences.Add(trigger1.TRIGGER_NAME, diff);
            }

            foreach (var trigger2 in Database2.SqlTables[tableName].Triggers)
            {
                if (!Differences.TableDifferences[tableName].TriggerDifferences.ContainsKey(trigger2.TRIGGER_NAME))
                {
                    Differences.TableDifferences[tableName].TriggerDifferences.Add(trigger2.TRIGGER_NAME, new TableSubItemDifferenceList(false, true));
                }
            }
        }

        private void InspectSynonyms()
        {
            foreach (var synonym1 in Database1.SqlSynonyms.Keys)
            {
                var diff = new DatabaseObjectDifferenceList(true, false);
                foreach (var synonym2 in Database2.SqlSynonyms.Keys)
                {
                    if (synonym2 == synonym1)
                    {
                        if (this.options.CompareProperties)
                        {
                            InspectObjectProperties(synonym2, diff);
                        }

                        if (this.options.ComparePermissions)
                        {
                            InspectObjectPermissions(synonym2, PERMISSION_OBJECT_TYPE.SYNONYM, diff);
                        }

                        diff.ObjectDefinition1 = Database1.SqlSynonyms[synonym2];
                        diff.ObjectDefinition2 = Database2.SqlSynonyms[synonym2];

                        diff.ExistsInDatabase2 = true;
                        break;
                    }
                }

                Differences.SynonymDifferences.Add(synonym1, diff);
            }

            foreach (var synonym2 in Database2.SqlSynonyms.Keys)
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
                            InspectObjectPermissions(view2, PERMISSION_OBJECT_TYPE.VIEW, diff);
                        }

                        diff.ObjectDefinition1 = Database1.SqlSynonyms[view2];
                        diff.ObjectDefinition2 = Database2.SqlSynonyms[view2];

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
                var isFunction = Database1.UserRoutines[routine1].ROUTINE_TYPE.ToLower() == "function";
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
                            InspectObjectPermissions(routine2, isFunction ? PERMISSION_OBJECT_TYPE.SQL_FUNCTION : PERMISSION_OBJECT_TYPE.SQL_STORED_PROCEDURE, diff);
                        }

                        diff.ObjectDefinition1 = Database1.SqlSynonyms[routine2];
                        diff.ObjectDefinition2 = Database2.SqlSynonyms[routine2];

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
                if (Database2.UserRoutines[routine2].ROUTINE_TYPE.ToLower() == "function")
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
            foreach (var property1 in Database1.SqlExtendedProperties)
            {
                if (property1.TYPE == PROPERTY_OBJECT_TYPE.ROUTINE && property1.OBJECT_NAME == objectName)
                {
                    var diff = new ExtendedPropertyDifference(true, false);
                    foreach (var property2 in Database2.SqlExtendedProperties)
                    {
                        if (property2.FULL_ID == property1.FULL_ID)
                        {
                            diff.ExistsInDatabase2 = true;
                            diff.Value1 = property1.PROPERTY_VALUE;
                            diff.Value2 = property2.PROPERTY_VALUE;
                            break;
                        }
                    }

                    objectDiff.ExtendedPropertyDifferences.Add(property1.PROPERTY_NAME, diff);
                }
            }

            foreach (var property2 in Database2.SqlExtendedProperties)
            {
                if (property2.TYPE == PROPERTY_OBJECT_TYPE.ROUTINE && property2.OBJECT_NAME == objectName)
                {
                    if (!objectDiff.ExtendedPropertyDifferences.ContainsKey(property2.PROPERTY_NAME))
                    {
                        objectDiff.ExtendedPropertyDifferences.Add(property2.PROPERTY_NAME, new ExtendedPropertyDifference(false, true));
                    }
                }
            }
        }

        private void InspectObjectPermissions(string objectName, PERMISSION_OBJECT_TYPE objectType, DatabaseObjectDifferenceList objectDiff)
        {
            foreach (var permission1 in Database1.SqlPermissions)
            {
                if (permission1.TYPE == objectType && permission1.OBJECT_NAME == objectName)
                {
                    var diff = new BaseDifference(true, false);
                    foreach (var permission2 in Database2.SqlPermissions)
                    {
                        if (permission2.FULL_ID == permission1.FULL_ID)
                        {
                            diff.ExistsInDatabase2 = true;
                            break;
                        }
                    }

                    if (!objectDiff.PermissionDifferences.ContainsKey(permission1.ToString()))
                    {
                        objectDiff.PermissionDifferences.Add(permission1.ToString(), diff);
                    }
                }
            }

            foreach (var permission2 in Database2.SqlPermissions)
            {
                if (permission2.TYPE == objectType && permission2.OBJECT_NAME == objectName)
                {
                    if (!objectDiff.PermissionDifferences.ContainsKey(permission2.ToString()))
                    {
                        objectDiff.PermissionDifferences.Add(permission2.ToString(), new BaseDifference(false, true));
                    }
                }
            }
        }

        private void GetDatabaseSchemas()
        {
            if (options.ConnectionString1.ToLower() == options.ConnectionString2.ToLower())
            {
                throw new InvalidOperationException("Connection strings should be different");
            }

            Database1 = new SqlDatabase(options.ConnectionString1, options.CompareObjects, options.CompareIndexes, options.ComparePermissions, options.CompareProperties, options.CompareTriggers, options.CompareSynonyms);

            Database2 = new SqlDatabase(options.ConnectionString2, options.CompareObjects, options.CompareIndexes, options.ComparePermissions, options.CompareProperties, options.CompareTriggers, options.CompareSynonyms);
        }
    }
}
