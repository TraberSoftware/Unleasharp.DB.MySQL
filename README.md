![Unleasharp.DB.MySQL](https://socialify.git.ci/TraberSoftware/Unleasharp.DB.MySQL/image?description=1&font=Inter&logo=https%3A%2F%2Fi.ibb.co%2FfmvBtLM%2Flogo-small.png&name=1&owner=1&pattern=Circuit+Board&theme=Light)

MySQL impementation of Unleasharp.DB.Base. As most of the logic is handled by the Base abstraction, this repo should handle:
 * Specific MySQL **Query** rendering
 * Specific **ConnectorManager** around the database connection
 * Specific **QueryBuilder** around the MySQL **Query** Type

### Connection initialization
The **ConnectionManager** is the responsible of managing the database connections, see [Unleasharp.DB.Base](https://github.com/TraberSoftware/Unleasharp.DB.Base) for reference.

You can also instantiate a ConnectionManager using a MySqlConnectionStringBuilder 
```csharp
ConnectorManager DBConnector = new ConnectorManager(
    new MySqlConnectionStringBuilder("Server=localhost;Database=unleasharp;Uid=unleasharp;Pwd=unleasharp;")
);
```

### Usage samples

#### Sample table structure
```csharp
// Example table structure

using System.ComponentModel;
using Unleasharp.DB.Base.SchemaDefinition;

namespace Unleasharp.DB.MySQL.Sample;

[Table("example_table")]
[Key("id", Field = "id", KeyType = Unleasharp.DB.Base.QueryBuilding.KeyType.PRIMARY)]
public class ExampleTable {
    [Column("id", "bigint", Unsigned = true, PrimaryKey = true, AutoIncrement = true, NotNull = true, Length = 20)]
    public ulong? ID         { get; set; }

    [Column("_mediumtext", "mediumtext")]
    public string MediumText { get; set; }

    [Column("_longtext", "longtext")]
    public string _longtext  { get; set; }

    [Column("_json", "longtext")]
    public string _json      { get; set; }

    [Column("_longblob", "longblob")]
    public byte[] CustomFieldName { get; set; }

    [Column("_enum", "enum")]
    public EnumExample? _enum { get; set; }

    [Column("_varchar", "varchar", Length = 255)]
    public string _varchar { get; set; }
}

public enum EnumExample {
    NONE,
    Y,
    [Description("NEGATIVE")]
    N
}
```

#### Sample program

This program will create the table ExampleTable, insert values, and perform selects on that table.

```csharp
using System;
using System.Collections.Generic;
using Unleasharp.DB.MySQL;
using Unleasharp.DB.Base.QueryBuilding;

namespace Unleasharp.DB.MySQL.Sample;

internal class Program {
    static void Main(string[] args) {
        ConnectorManager DBConnector = new ConnectorManager("Server=192.168.1.8;Database=unleasharp;Uid=unleasharp;Pwd=unleasharp;");

        DBConnector.QueryBuilder().Build(Query => Query.Create<ExampleTable>()).Execute();
        DBConnector.QueryBuilder().Build(Query => { Query
            // Select table by class, using [Table()] Attribute
            .From<ExampleTable>()
            // Insert single class value
            .Value(new ExampleTable {
                MediumText = "Medium text example value",
                _enum      = EnumExample.N
            })
            // Insert multiple class values
            .Values(new List<ExampleTable> {
                new ExampleTable {
                    _json           = @"{""sample_json_field"": ""sample_json_value""}",
                    _enum           = EnumExample.Y,
                    CustomFieldName = new byte[8] {
                        81,47,15,21,12,16,23,39
                    }
                },
                new ExampleTable {
                    _longtext = "Long text example value",
                    ID        = RandomID
                }
            })
            // Define query type
            .Insert();
        }).Execute();

        // You can retrieve a single row from a query using standard syntax
        ExampleTable Row = DBConnector.QueryBuilder().Build(Query => Query
            // The table selector can use a string too
            .From("example_table")
            .OrderBy("id", OrderDirection.ASC)
            .Limit(1)
            .Select()
        ).FirstOrDefault<ExampleTable>();

        // You don't forcefully need to use Attributed class or properties
        // to retrieve data from database as long as fields are named the same
        // in both database and class definition
        List<example_table> Rows = DBConnector.QueryBuilder().Build(Query => Query
            // The table selector can use a string too
            .From("example_table")
            .OrderBy("id", OrderDirection.DESC)
            .Select()
        ).ToList<example_table>();
    }
}
```

#### Sample query rendering
You can generate a query and render both raw query or prepared query, for debugging purposes.

```csharp
// This sample illustrates how to performa a complex query,
// including subqueries in WhereIn fields. The query is non-functional
// and just for query-rendering demonstration purposes
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
    .Limit(100)
;

// Render the query as should be executed in plain SQL
Console.WriteLine(VeryComplexQuery.Render());

// Render the query as should be executed with prepared statements placeholders
Console.WriteLine(VeryComplexQuery.RenderPrepared());
```