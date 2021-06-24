namespace QuickCompareModel
{
    public class QuickCompareOptions
    {
        public string ConnectionString1 { get; set; }

        public string ConnectionString2 { get; set; }

        public bool CompareColumns { get; set; }

        public bool CompareRelations { get; set; }

        public bool CompareObjects { get; set; }

        public bool IgnoreSQLComments { get; set; }

        public bool CompareIndexes { get; set; }

        public bool ComparePermissions { get; set; }

        public bool CompareProperties { get; set; }

        public bool CompareTriggers { get; set; }

        public bool CompareSynonyms { get; set; }

        public bool CompareOrdinalPositions { get; set; }
    }
}
