﻿namespace QuickCompareModel.DatabaseDifferences
{
    using System.Collections.Generic;
    using System.Text;

    public class Differences
    {
        public string Database1 { get; set; }

        public string Database2 { get; set; }

        public Dictionary<string, ExtendedPropertyDifference> ExtendedPropertyDifferences { get; set; }
            = new Dictionary<string, ExtendedPropertyDifference>();

        public Dictionary<string, TableDifferenceList> TableDifferences { get; set; }
            = new Dictionary<string, TableDifferenceList>();

        public Dictionary<string, DatabaseObjectDifferenceList> FunctionDifferences { get; set; }
            = new Dictionary<string, DatabaseObjectDifferenceList>();

        public Dictionary<string, DatabaseObjectDifferenceList> StoredProcedureDifferences { get; set; }
            = new Dictionary<string, DatabaseObjectDifferenceList>();

        public Dictionary<string, DatabaseObjectDifferenceList> ViewDifferences { get; set; }
            = new Dictionary<string, DatabaseObjectDifferenceList>();

        public Dictionary<string, DatabaseObjectDifferenceList> SynonymDifferences { get; set; }
            = new Dictionary<string, DatabaseObjectDifferenceList>();

        public bool HasDifferences => ExtendedPropertyDifferences.Count + TableDifferences.Count + FunctionDifferences.Count +
            StoredProcedureDifferences.Count + ViewDifferences.Count + SynonymDifferences.Count > 0;

        public override string ToString()
        {
            var output = new StringBuilder("QuickCompare schema comparison result\r\n\r\n");
            output.AppendLine($"Database 1: {Database1}");
            output.AppendLine($"Database 2: {Database2}\r\n");

            if (!HasDifferences)
            {
                output.AppendLine("NO DIFFERENCES HAVE BEEN FOUND");
                return output.ToString();
            }

            var section = new StringBuilder();

            if (ExtendedPropertyDifferences.Count > 0)
            {
                foreach (var prop in ExtendedPropertyDifferences)
                {
                    if (prop.Value.IsDifferent)
                    {
                        section.Append($"Extended property: [{prop.Key}] - {prop.Value}");
                    }
                }

                if (section.Length > 0)
                {
                    output.Append("\r\nEXTENDED PROPERTY DIFFERENCES\r\n\r\n");
                    output.Append(section);
                    section.Length = 0;
                }
            }

            if (TableDifferences.Count > 0)
            {
                foreach (var tableDifference in TableDifferences)
                {
                    if (tableDifference.Value.IsDifferent)
                    {
                        section.AppendLine($"Table: [{tableDifference.Key}] {tableDifference.Value}");
                    }
                }

                if (section.Length > 0)
                {
                    output.Append("\r\nTABLE DIFFERENCES\r\n\r\n");
                    output.Append(section);
                    section.Length = 0;
                }
            }

            if (ViewDifferences.Count > 0)
            {
                foreach (var viewDifference in ViewDifferences)
                {
                    if (viewDifference.Value.IsDifferent)
                    {
                        section.AppendLine($"View: [{viewDifference.Key}] {viewDifference.Value}");
                    }
                }

                if (section.Length > 0)
                {
                    output.Append("\r\nVIEW DIFFERENCES\r\n\r\n");
                    output.Append(section);
                    section.Length = 0;
                }
            }

            if (FunctionDifferences.Count > 0)
            {
                foreach (var functionDifference in FunctionDifferences)
                {
                    if (functionDifference.Value.IsDifferent)
                    {
                        section.AppendLine($"Function: [{functionDifference.Key}] {functionDifference.Value}");
                    }
                }

                if (section.Length > 0)
                {
                    output.Append("\r\nFUNCTION DIFFERENCES\r\n\r\n");
                    output.Append(section);
                    section.Length = 0;
                }
            }

            if (StoredProcedureDifferences.Count > 0)
            {
                foreach (var procedureDifference in StoredProcedureDifferences)
                {
                    if (procedureDifference.Value.IsDifferent)
                    {
                        section.AppendLine($"Stored procedure: [{procedureDifference.Key}] {procedureDifference.Value}");
                    }
                }

                if (section.Length > 0)
                {
                    output.Append("\r\nSTORED PROCEDURE DIFFERENCES\r\n\r\n");
                    output.Append(section);
                    section.Length = 0;
                }
            }

            if (SynonymDifferences.Count > 0)
            {
                foreach (var synonymDifference in SynonymDifferences)
                {
                    if (synonymDifference.Value.IsDifferent)
                    {
                        section.AppendLine($"Synonym: [{synonymDifference.Key}] {synonymDifference.Value}");
                    }
                }

                if (section.Length > 0)
                {
                    output.Append("\r\nSYNONYM DIFFERENCES\r\n\r\n");
                    output.Append(section);
                    section.Length = 0;
                }
            }

            return output.ToString();
        }
    }
}