namespace QuickCompareModel.DatabaseDifferences
{
    public class ExtendedPropertyDifference : BaseDifference
    {
        public ExtendedPropertyDifference(bool existsInDatabase1, bool existsInDatabase2)
            : base(existsInDatabase1, existsInDatabase2)
        {
        }

        public string Value1 { get; set; }

        public string Value2 { get; set; }

        public bool IsDifferent => !ExistsInBothDatabases || Value1 != Value2;

        public override string ToString() => IsDifferent
                ? !ExistsInBothDatabases ? ExistenceDifference() : $"value is different; [{Value1}] in database 1, [{Value2}] in database 2\r\n"
                : string.Empty;
    }
}
