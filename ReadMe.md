# QuickCompare
__A simple fast database schema comparison library written in C#__

## How it works

Using some SQL queries (mainly targeting the INFORMATION_SCHEMA models), the solution uses low-level DataReader instances to populate models in the DatabaseSchema namespace.

Next the engine inspects models of both databases, building a set of Difference objects for each database element.

The Difference objects also act as a report generator, with overridden ToString methods a generated report will list all database differences.

Input parameters are accepted via an IOptions implementation, __QuickCompareOptions__.

_Note that this was created as part of an Innovation Day event so is lacking initial unit tests and XML comments._

### Example usage

The `DifferenceBuilder` class generates the report from the `GetDifferenceReport()` method. Note that the options are usually injected from the configuration, but are explicitly created in this example for clarity;

```C#
var settings = new QuickCompareOptions
{
    ConnectionString1 = "Data Source=localhost\\SQLEXPRESS;Initial Catalog=Northwind1;Integrated Security=True",
    ConnectionString2 = "Data Source=localhost\\SQLEXPRESS;Initial Catalog=Northwind2;Integrated Security=True",
};

IOptions<QuickCompareOptions> options = Options.Create(settings);

var builder = new DifferenceBuilder(options);
string outputText = builder.GetDifferenceReport();
```

