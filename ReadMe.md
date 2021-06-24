# QuickCompare
__A simple fast database schema comparison library written in C#__

## How it works

Using some SQL queries (mainly targeting the INFORMATION_SCHEMA models), the solution uses low-level DataReader instances to populate models in the DatabaseSchema namespace.

Next the engine inspects models of both databases, building a set of Difference objects for each database element.

The Difference objects also act as a report generator, with overridden ToString methods a generated report will list all database differences.

Input parameters are accepted via an IOptions implementation, __QuickCompareOptions__.