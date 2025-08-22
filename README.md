# 🐬 Unleasharp.DB.MySQL

[![NuGet version (Unleasharp.DB.MySQL)](https://img.shields.io/nuget/v/Unleasharp.DB.MySQL.svg?style=flat-square)](https://www.nuget.org/packages/Unleasharp.DB.MySQL/)

[![Unleasharp.DB.MySQL](https://socialify.git.ci/TraberSoftware/Unleasharp.DB.MySQL/image?description=1&font=Inter&logo=https%3A%2F%2Fraw.githubusercontent.com%2FTraberSoftware%2FUnleasharp%2Frefs%2Fheads%2Fmain%2Fassets%2Flogo-small.png&name=1&owner=1&pattern=Circuit+Board&theme=Light)](https://github.com/TraberSoftware/Unleasharp.DB.MySQL)

MySQL implementation of Unleasharp.DB.Base. This repository provides a MySQL-specific implementation that leverages the base abstraction layer for common database operations.

## 📦 Installation

Install the NuGet package using one of the following methods:

### Package Manager Console
```powershell
Install-Package Unleasharp.DB.MySQL
```

### .NET CLI
```bash
dotnet add package Unleasharp.DB.MySQL
```

### PackageReference (Manual)
```xml
<PackageReference Include="Unleasharp.DB.MySQL" Version="1.1.1" />
```

## 🎯 Features

- **MySQL-Specific Query Rendering**: Custom query building and rendering tailored for MySQL
- **Connection Management**: Robust connection handling through ConnectorManager
- **Query Builder Integration**: Seamless integration with the base QueryBuilder
- **Schema Definition Support**: Full support for table and column attributes

## 🚀 Connection Initialization

The `ConnectorManager` handles database connections. You can initialize it using a connection string or `MySqlConnectionStringBuilder`.

### Using Connection String
```csharp
ConnectorManager DBConnector = new ConnectorManager("Server=localhost;Database=unleasharp;Uid=unleasharp;Pwd=unleasharp;");
```

### Using Fluent Configuration
```csharp
ConnectorManager DBConnector = new ConnectorManager()
    .WithAutomaticConnectionRenewal(true)
    .WithAutomaticConnectionRenewalInterval(TimeSpan.FromHours(1))
    .Configure(config => {
        config.ConnectionString = "Server=localhost;Database=unleasharp;Uid=unleasharp;Pwd=unleasharp;";
    })
	.WithOnQueryExceptionAction(ex => Console.WriteLine(ex.Message))
;
```

### Using MySqlConnectionStringBuilder
```csharp
ConnectorManager DBConnector = new ConnectorManager(
    new MySqlConnectionStringBuilder("Server=localhost;Database=unleasharp;Uid=unleasharp;Pwd=unleasharp;")
);
```

## 📝 Usage Examples

### Sample Table Structure

```csharp
using System.ComponentModel;
using Unleasharp.DB.Base.SchemaDefinition;

namespace Unleasharp.DB.MySQL.Sample;

[Table("example_table")]
[PrimaryKey("id")]
[UniqueKey("id", "id", "_enum")]
public class ExampleTable {
    [Column("id",          ColumnDataType.UInt64, Unsigned = true, PrimaryKey = true, AutoIncrement = true, NotNull = true)]
    public long?  Id              { get; set; }

    [Column("_mediumtext", ColumnDataType.Text)]
    public string MediumText      { get; set; }

    [Column("_longtext",   ColumnDataType.Text)]
    public string Longtext        { get; set; }

    [Column("_json",       ColumnDataType.Json)]
    public string Json            { get; set; }

    [Column("_longblob",   ColumnDataType.Binary)]
    public byte[] CustomFieldName { get; set; }

    [Column("_enum",       ColumnDataType.Enum)]
    public EnumExample? Enum      { get; set; }

    [Column("_varchar",    "varchar", Length = 255)]
    public string Varchar         { get; set; }
}

public enum EnumExample 
{
    NONE,
    [Description("Y")]
    Y,
    [Description("NEGATIVE")]
    N
}
```

### Sample Program

```csharp
using System;
using System.Collections.Generic;
using Unleasharp.DB.MySQL;
using Unleasharp.DB.Base.QueryBuilding;

namespace Unleasharp.DB.MySQL.Sample;

internal class Program 
{
    static void Main(string[] args) 
    {
        // Initialize database connection
        ConnectorManager DBConnector = new ConnectorManager("Server=192.168.1.8;Database=unleasharp;Uid=unleasharp;Pwd=unleasharp;");
        
        // Create table
        DBConnector.QueryBuilder().Build(Query => Query.Create<ExampleTable>()).Execute();
        
        // Insert data
        DBConnector.QueryBuilder().Build(Query => { Query
            .From<ExampleTable>()
            .Value(new ExampleTable {
                MediumText = "Medium text example value",
                _enum      = EnumExample.N
            })
            .Values(new List<ExampleTable> {
                new ExampleTable {
                    _json           = @"{""sample_json_field"": ""sample_json_value""}",
                    _enum           = EnumExample.Y,
                    CustomFieldName = new byte[8] { 81,47,15,21,12,16,23,39 }
                },
                new ExampleTable {
                    _longtext = "Long text example value",
                    ID        = 999 // RandomID placeholder
                }
            })
            .Insert();
        }).Execute();
        
        // Select single row
        ExampleTable Row = DBConnector.QueryBuilder().Build(Query => Query
            .From("example_table")
            .OrderBy("id", OrderDirection.ASC)
            .Limit(1)
            .Select()
        ).FirstOrDefault<ExampleTable>();
        
        // Select multiple rows with different class naming
        List<example_table> Rows = DBConnector.QueryBuilder().Build(Query => Query
            .From("example_table")
            .OrderBy("id", OrderDirection.DESC)
            .Select()
        ).ToList<example_table>();
    }
}
```

### Sample Query Rendering

```csharp
// Complex query demonstration with subqueries
Query VeryComplexQuery = Query.GetInstance()
    .Select("query_field")
    .Select($"COUNT({new FieldSelector("table_x", "table_y")})", true)
    .From("query_from")
    .Where("field", "value")
    .WhereIn(
        "field_list",
        Query.GetInstance()
            .Select("*", false)
            .From("subquery_table")
            .Where("subquery_field", true)
            .WhereIn(
                "subquery_in_field",
                Query.GetInstance()
                    .Select("subquery_subquery_in_field")
                    .From("subquery_subquery_in_table")
                    .Where("subquery_subquery_in_where", true)
            )
            .Limit(100)
    )
    .WhereIn("field_list", new List<dynamic> { null, 123, 456, "789" })
    .Join("another_table", new FieldSelector("table_x", "field_x"), new FieldSelector("table_y", "field_y"))
    .OrderBy(new OrderBy {
        Field     = new FieldSelector("order_field"),
        Direction = OrderDirection.DESC
    })
    .GroupBy("group_first")
    .GroupBy("group_second")
    .Limit(100);

// Render raw SQL query
Console.WriteLine(VeryComplexQuery.Render());

// Render prepared statement query (with placeholders)
Console.WriteLine(VeryComplexQuery.RenderPrepared());
```

## 📦 Dependencies

- [Unleasharp.DB.Base](https://github.com/TraberSoftware/Unleasharp.DB.Base) - Base abstraction layer
- [MySqlConnector](https://github.com/mysql-net/MySqlConnector) - MySQL driver for .NET

## 📋 Version Compatibility

This library targets .NET 8.0 and later versions. For specific version requirements, please check the package dependencies.

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

---

*For more information about Unleasharp.DB.Base, visit: [Unleasharp.DB.Base](https://github.com/TraberSoftware/Unleasharp.DB.Base)*