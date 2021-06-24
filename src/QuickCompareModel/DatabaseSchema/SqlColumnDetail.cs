namespace QuickCompareModel.DatabaseSchema
{
    internal class SqlColumnDetail
    {
        public string TABLE_SCHEMA { get; set; }

        public string TABLE_NAME { get; set; }

        public string COLUMN_NAME { get; set; }

        public int ORDINAL_POSITION { get; set; }

        public string COLUMN_DEFAULT { get; set; }

        public bool IS_NULLABLE { get; set; }

        public string DATA_TYPE { get; set; }

        public int? CHARACTER_MAXIMUM_LENGTH { get; set; }

        public int? CHARACTER_OCTET_LENGTH { get; set; }

        public int? NUMERIC_PRECISION { get; set; }

        public int? NUMERIC_PRECISION_RADIX { get; set; }

        public int? NUMERIC_SCALE { get; set; }

        public int? DATETIME_PRECISION { get; set; }

        public string CHARACTER_SET_NAME { get; set; }

        public string COLLATION_NAME { get; set; }

        public bool IS_FULL_TEXT_INDEXED { get; set; }

        public bool IS_COMPUTED { get; set; }

        public bool IS_IDENTITY { get; set; }

        public decimal? IDENTITY_SEED { get; set; }

        public decimal? IDENTITY_INCREMENT { get; set; }

        public bool IS_SPARSE { get; set; }

        public bool IS_COLUMN_SET { get; set; }

        public string FULL_ID => $"[{TABLE_NAME}].[{COLUMN_NAME}]";
    }
}
