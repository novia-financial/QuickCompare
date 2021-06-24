namespace QuickCompareModel.DatabaseSchema
{
    internal class SqlTrigger
    {
        public string FILEGROUP { get; set; }

        public string TRIGGER_NAME { get; set; }

        public string TRIGGER_OWNER { get; set; }

        public string TABLE_SCHEMA { get; set; }

        public string TABLE_NAME { get; set; }

        public bool IS_UPDATE { get; set; }

        public bool IS_DELETE { get; set; }

        public bool IS_INSERT { get; set; }

        public bool IS_AFTER { get; set; }

        public bool IS_INSTEAD_OF { get; set; }

        public bool IS_DISABLED { get; set; }

        public string TRIGGER_CONTENT { get; set; }
    }
}
