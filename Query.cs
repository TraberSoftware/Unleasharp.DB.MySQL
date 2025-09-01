using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Unleasharp.DB.Base.ExtensionMethods;
using Unleasharp.DB.Base.QueryBuilding;
using Unleasharp.DB.Base.SchemaDefinition;
using Unleasharp.ExtensionMethods;

namespace Unleasharp.DB.MySQL;

/// <summary>
/// MySQL-specific query builder that extends the generic <see cref="Unleasharp.DB.Base.Query{Query}"/>.
/// Provides MySQL syntax and rendering for SQL statements.
/// </summary>
public class Query : Unleasharp.DB.Base.Query<Query> {
    /// <inheritdoc/>
    protected override DatabaseEngine _Engine { get { return DatabaseEngine.MySQL; } }

    #region Custom MySQL query data
    /// <summary>
    /// Gets a value indicating whether the query should use FOR UPDATE.
    /// </summary>
    public bool QueryForUpdate { get; private set; } = false;
    #endregion

    /// <summary>
    /// The delimiter used for escaping field names in MySQL.
    /// </summary>
    public const string FieldDelimiter = "`";
    /// <summary>
    /// The delimiter used for escaping values in MySQL.
    /// </summary>
    public const string ValueDelimiter = "'";

    #region Query rendering
    #region Query fragment rendering
    /// <inheritdoc/>
    public override void _RenderPrepared() {
        this._Render();

        string rendered = this.QueryPreparedString;
        foreach (KeyValuePair<string, PreparedValue> preparedDataItem in this.QueryPreparedData) {
            if (preparedDataItem.Value.Value == null) {
                rendered = rendered.Replace(preparedDataItem.Key, "NULL");
            }
            else {
                rendered = rendered.Replace(preparedDataItem.Key, this.__RenderWhereValue(preparedDataItem.Value.Value, preparedDataItem.Value.EscapeValue));
            }
        }

        this.QueryRenderedString = rendered;
    }

    /// <summary>
    /// Renders a SQL SELECT fragment as a string, including subqueries and aliases if applicable.
    /// </summary>
    /// <remarks>This method handles both simple field rendering and subquery rendering. If the fragment
    /// includes a subquery, the subquery is rendered  in the context of the current query or its parent query, if
    /// available. If the fragment includes an alias, it is appended to the rendered  field using the "AS"
    /// keyword.</remarks>
    /// <param name="fragment">The <see cref="Select{T}"/> object representing the SQL SELECT fragment to render.</param>
    /// <returns>A string representation of the SQL SELECT fragment. If the fragment contains a subquery, the rendered subquery
    /// is enclosed in parentheses.  If an alias is specified, it is appended to the rendered field with the "AS"
    /// keyword.</returns>
    public string RenderSelect(Select<Query> fragment) {
        if (fragment.Subquery != null) {
            return "(" + fragment.Subquery.WithParentQuery(this.ParentQuery != null ? this.ParentQuery : this).Render() + ")";
        }

        return fragment.Field.Render() + (!string.IsNullOrWhiteSpace(fragment.Alias) ? $" AS {fragment.Alias}" : "");
    }

    /// <summary>
    /// Renders a SQL "FROM" clause based on the specified <see cref="From{T}"/> fragment.
    /// </summary>
    /// <remarks>This method supports rendering both subqueries and table names for use in SQL "FROM" clauses.
    /// If a subquery is provided, it is rendered with the appropriate parent query context. If a table name is
    /// provided, it can be optionally escaped using the <c>FieldDelimiter</c>.</remarks>
    /// <param name="fragment">The <see cref="From{T}"/> fragment containing the table, subquery, and alias information to be rendered into a
    /// SQL "FROM" clause. The <paramref name="fragment"/> must not be null.</param>
    /// <returns>A string representing the rendered SQL "FROM" clause. If the fragment contains a subquery, the subquery is
    /// rendered and enclosed in parentheses. If the fragment specifies a table, the table name is rendered, optionally
    /// escaped, and followed by the table alias if provided.</returns>
    public string RenderFrom(From<Query> fragment) {
        if (fragment.Subquery != null) {
            return "(" + fragment.Subquery.WithParentQuery(this.ParentQuery != null ? this.ParentQuery : this).Render() + ")";
        }

        string rendered = string.Empty;

        if (!string.IsNullOrWhiteSpace(fragment.Table)) {
            if (fragment.EscapeTable) {
                rendered = FieldDelimiter + fragment.Table + FieldDelimiter;
            }
            else {
                rendered = fragment.Table;
            }
        }

        return rendered + (fragment.TableAlias != string.Empty ? $" {fragment.TableAlias}" : "");
    }

    /// <summary>
    /// Renders a SQL JOIN clause as a string based on the specified join fragment.
    /// </summary>
    /// <remarks>The table name is optionally escaped based on the <see cref="Join{T}.EscapeTable"/> property.
    /// The ON condition is rendered using the <see cref="RenderWhere"/> method.</remarks>
    /// <param name="fragment">The <see cref="Join{T}"/> object representing the join details, including the table name,  whether the table
    /// name should be escaped, and the join condition.</param>
    /// <returns>A string representing the SQL JOIN clause, including the table name and the ON condition.</returns>
    public string RenderJoin(Join<Query> fragment) {
        return $"{(fragment.EscapeTable ? FieldDelimiter + fragment.Table + FieldDelimiter : fragment.Table)} ON {this.RenderWhere(fragment.Condition)}";
    }

    /// <summary>
    /// Renders a SQL "GROUP BY" clause based on the specified <see cref="GroupBy"/> fragment.
    /// </summary>
    /// <remarks>The method constructs the "GROUP BY" clause by combining the table and field names specified
    /// in the <paramref name="fragment"/>. If either the table or field name is null, empty, or consists only of
    /// whitespace, it will be excluded from the output. Escaping is applied to the table and field names if the
    /// <c>Escape</c> property of the <paramref name="fragment"/> is set to <see langword="true"/>.</remarks>
    /// <param name="fragment">The <see cref="GroupBy"/> fragment containing the table and field information to be rendered.</param>
    /// <returns>A string representing the "GROUP BY" clause, formatted with the table and field names. If the table or field
    /// names are marked for escaping, they will be enclosed with the appropriate delimiters.</returns>
    public string RenderGroupBy(GroupBy fragment) {
        List<string> toRender = new List<string>();

        if (!string.IsNullOrWhiteSpace(fragment.Field.Table)) {
            if (fragment.Field.Escape) {
                toRender.Add(FieldDelimiter + fragment.Field.Table + FieldDelimiter);
            }
            else {
                toRender.Add(fragment.Field.Table);
            }
        }

        if (!string.IsNullOrWhiteSpace(fragment.Field.Field)) {
            if (fragment.Field.Escape) {
                toRender.Add(FieldDelimiter + fragment.Field.Field + FieldDelimiter);
            }
            else {
                toRender.Add(fragment.Field.Field);
            }
        }

        return String.Join('.', toRender);
    }

    /// <summary>
    /// Renders a SQL WHERE clause based on the specified query fragment.
    /// </summary>
    /// <remarks>This method generates a SQL WHERE clause by combining the field, comparer, and value  (or
    /// value field) specified in the <paramref name="fragment"/>. If a subquery is provided,  it is rendered and
    /// included in the output. Special handling is applied for null values  and escaping, depending on the fragment's
    /// configuration.</remarks>
    /// <param name="fragment">The <see cref="Where{T}"/> object representing the query fragment to render.  This includes the field, comparer,
    /// value, and optional subquery or value field.</param>
    /// <returns>A <see cref="string"/> containing the rendered SQL WHERE clause.  The result is formatted based on the provided
    /// fragment's properties, including  handling subqueries, null values, and value escaping as necessary.</returns>
    public string RenderWhere(Where<Query> fragment) {
        if (fragment.Subquery != null) {
            return $"{fragment.Field.Render()} {fragment.Comparer.GetDescription()} ({fragment.Subquery.WithParentQuery(this.ParentQuery != null ? this.ParentQuery : this).Render()})";
        }

        List<string> toRender = new List<string>();

        toRender.Add(fragment.Field.Render());

        // We are comparing fields, not values
        if (fragment.ValueField != null) {
            toRender.Add(fragment.ValueField.Render());
        }
        else {
            if (fragment.Value == null) {
                fragment.Comparer = WhereComparer.IS;
                toRender.Add("NULL");
            }
            else {
                if (fragment.EscapeValue) {
                    toRender.Add(this.PrepareQueryValue(fragment.Value, fragment.EscapeValue));
                }
                else {
                    toRender.Add(this.__RenderWhereValue(fragment.Value, false));
                }
            }
        }

        return String.Join(fragment.Comparer.GetDescription(), toRender);
    }

    /// <summary>
    /// Renders a SQL "WHERE IN" clause based on the specified fragment.
    /// </summary>
    /// <remarks>If the <paramref name="fragment"/> contains a subquery, the method renders the "WHERE IN"
    /// clause using the subquery. Otherwise, it renders the clause using the provided values, applying value escaping
    /// if specified by the fragment.</remarks>
    /// <param name="fragment">The <see cref="WhereIn{T}"/> fragment containing the field, values, and optional subquery to be rendered into
    /// the "WHERE IN" clause.</param>
    /// <returns>A string representing the rendered "WHERE IN" clause. Returns an empty string if the fragment's <see
    /// cref="WhereIn{T}.Values"/> collection is null or empty.</returns>
    public string RenderWhereIn(WhereIn<Query> fragment) {
        if (fragment.Subquery != null) {
            return $"{fragment.Field.Render()} IN ({fragment.Subquery.WithParentQuery(this.ParentQuery != null ? this.ParentQuery : this).Render()})";
        }

        if (fragment.Values == null || fragment.Values.Count == 0) {
            return String.Empty;
        }

        List<string> toRender = new List<string>();

        foreach (dynamic fragmentValue in fragment.Values) {
            if (fragment.EscapeValue) {
                toRender.Add(this.PrepareQueryValue(fragmentValue, fragment.EscapeValue));
            }
            else {
                toRender.Add(__RenderWhereValue(fragmentValue, fragment.EscapeValue));
            }
        }

        return $"{fragment.Field.Render()} IN ({String.Join(",", toRender)})";
    }
    #endregion

    #region Query sentence rendering
    /// <inheritdoc/>
    protected override string _RenderCountSentence() {
        return "SELECT COUNT(*)";
    }

    /// <inheritdoc/>
    protected override string _RenderSelectSentence() {
        List<string> rendered = new List<string>();

        if (this.QuerySelect.Count > 0) {
            foreach (Select<Query> queryFragment in this.QuerySelect) {
                rendered.Add(this.RenderSelect(queryFragment));
            }
        }
        else {
            rendered.Add("*");
        }

        return $"SELECT {(this.QueryDistinct ? "DISTINCT " : "")}{string.Join(',', rendered)}";
    }

    /// <inheritdoc/>
    protected override string _RenderFromSentence() {
        List<string> rendered = new List<string>();

        foreach (From<Query> queryFragment in this.QueryFrom) {
            rendered.Add(this.RenderFrom(queryFragment));
        }

        return (rendered.Count > 0 ? "FROM " + string.Join(',', rendered) : "");
    }

    /// <inheritdoc/>
    protected override string _RenderJoinSentence() {
        List<string> rendered = new List<string>();
        foreach (Join<Query> queryFragment in this.QueryJoin) {
            rendered.Add(this.RenderJoin(queryFragment));
        }

        return (rendered.Count > 0 ? "JOIN " + string.Join(',', rendered) : "");
    }

    /// <inheritdoc/>
    protected override string _RenderWhereSentence() {
        List<string> rendered = new List<string>();
        foreach (Where<Query> queryFragment in this.QueryWhere) {
            if (rendered.Any()) {
                rendered.Add(queryFragment.Operator.GetDescription());
            }
            rendered.Add(this.RenderWhere(queryFragment));
        }
        foreach (WhereIn<Query> queryFragment in this.QueryWhereIn) {
            if (rendered.Any()) {
                rendered.Add(queryFragment.Operator.GetDescription());
            }
            rendered.Add(this.RenderWhereIn(queryFragment));
        }

        return (rendered.Count > 0 ? "WHERE " + string.Join(' ', rendered) : "");
    }

    /// <inheritdoc/>
    protected override string _RenderGroupSentence() {
        List<string> rendered = new List<string>();

        foreach (GroupBy queryFragment in this.QueryGroup) {
            rendered.Add(this.RenderGroupBy(queryFragment));
        }

        return (rendered.Count > 0 ? "GROUP BY " + string.Join(',', rendered) : "");
    }

    /// <inheritdoc/>
    protected override string _RenderHavingSentence() {
        List<string> rendered = new List<string>();

        foreach (Where<Query> queryFragment in this.QueryHaving) {
            rendered.Add(this.RenderWhere(queryFragment));
        }

        return (rendered.Count > 0 ? "HAVING " + string.Join(',', rendered) : "");
    }

    /// <inheritdoc/>
    protected override string _RenderOrderSentence() {
        List<string> rendered = new List<string>();

        if (this.QueryOrder != null) {
            foreach (OrderBy queryOrderItem in this.QueryOrder) {
                List<string> renderedSubset = new List<string>();

                renderedSubset.Add(queryOrderItem.Field.Render());

                if (queryOrderItem.Direction != OrderDirection.NONE) {
                    renderedSubset.Add(queryOrderItem.Direction.GetDescription());
                }

                rendered.Add(string.Join(' ', renderedSubset));
            }
        }

        return (rendered.Count > 0 ? "ORDER BY " + string.Join(',', rendered) : "");
    }

    /// <inheritdoc/>
    protected override string _RenderLimitSentence() {
        List<string> rendered = new List<string>();
        if (this.QueryLimit != null) {
            if (this.QueryLimit.Offset >= 0) {
                rendered.Add(this.QueryLimit.Offset.ToString());
            }
            if (this.QueryLimit.Count > 0) {
                rendered.Add(this.QueryLimit.Count.ToString());
            }
        }

        return (rendered.Count > 0 ? "LIMIT " + string.Join(',', rendered) : "");
    }


    /// <inheritdoc/>
    protected override string _RenderDeleteSentence() {
        From<Query> from = this.QueryFrom.FirstOrDefault();

        if (from != null) {
            return $"DELETE FROM {from.Table}{(!string.IsNullOrWhiteSpace(from.TableAlias) ? $" AS {from.TableAlias}" : "")}";
        }

        return string.Empty;
    }
    /// <inheritdoc/>
    protected override string _RenderUpdateSentence() { 
        From<Query> from = this.QueryFrom.FirstOrDefault();

        if (from != null) {
            return $"UPDATE {this.RenderFrom(from)}";
        }

        return string.Empty;
    }

    /// <inheritdoc/>
    protected override string _RenderSetSentence() {
        List<string> rendered = new List<string>();

        if (this.QueryOrder != null) {
            foreach (Where<Query> querySetItem in this.QuerySet) {
                querySetItem.Comparer = WhereComparer.EQUALS;

                rendered.Add(this.RenderWhere(querySetItem));
            }
        }

        return (rendered.Count > 0 ? "SET " + string.Join(',', rendered) : "");
    }

    /// <inheritdoc/>
    protected override string _RenderInsertIntoSentence() { 
        From<Query> from = this.QueryFrom.FirstOrDefault();

        if (from != null) {
            return $"INSERT INTO {from.Table} ({string.Join(',', this.QueryColumns)})";
        }

        return string.Empty;
    }

    /// <inheritdoc/>
    protected override string _RenderInsertValuesSentence() {
        List<string> rendered = new List<string>();

        if (this.QueryValues != null) {
            foreach (Dictionary<string, dynamic> queryValue in QueryValues) {
                List<string> toRender = new List<string>();

                // In order to get a valid query, insert the values in the same column order
                foreach (string queryColumn in this.QueryColumns) {
                    if (queryValue.ContainsKey(queryColumn) && queryValue[queryColumn] != null) {
                        toRender.Add(this.PrepareQueryValue(queryValue[queryColumn], true));
                    }
                    else {
                        toRender.Add("NULL");
                    }
                }

                rendered.Add($"({string.Join(",", toRender)})");
            }
        }

        return (rendered.Count > 0 ? "VALUES " + string.Join(',', rendered) : "");
    }


    /// <inheritdoc/>
    protected override string _RenderCreateSentence<T>() {
        return this._RenderCreateSentence(typeof(T));
    }

    /// <inheritdoc/>
    protected override string _RenderCreateSentence(Type tableType) {
        Table typeTable = tableType.GetCustomAttribute<Table>();
        if (typeTable == null) {
            throw new InvalidOperationException("Missing [Table] attribute");
        }

        StringBuilder rendered = new StringBuilder();

        rendered.Append("CREATE ");
        if (typeTable.Temporary) {
            rendered.Append("TEMPORARY ");
        }

        rendered.Append("TABLE ");
        if (typeTable.IfNotExists) {
            rendered.Append("IF NOT EXISTS ");
        }
        rendered.Append($"{Query.FieldDelimiter}{typeTable.Name}{Query.FieldDelimiter} (");

        PropertyInfo[] tableProperties = tableType.GetProperties();
        FieldInfo   [] tableFields     = tableType.GetFields();

        IEnumerable<string?> tableColumnDefinitions = this.__GetTableColumnDefinitions(tableType);
        IEnumerable<string?> tableKeyDefinitions    = this.__GetTableKeyDefinitions(tableType);

        rendered.Append(string.Join(",", tableColumnDefinitions.Concat(tableKeyDefinitions ?? Enumerable.Empty<string>())));
        rendered.Append(")");

        // Table options
        var tableOptions = tableType.GetCustomAttributes<TableOption>();
        foreach (TableOption tableOption in tableOptions) {
            rendered.Append($" {tableOption.Name}={tableOption.Value}");
        }

        return rendered.ToString();
    }

    /// <summary>
    /// Gets the column definitions for a table type.
    /// </summary>
    /// <param name="tableType">The table type.</param>
    /// <returns>The column definitions as strings.</returns>
    private IEnumerable<string?> __GetTableColumnDefinitions(Type tableType) {
        PropertyInfo[] tableProperties = tableType.GetProperties();
        FieldInfo   [] tableFields     = tableType.GetFields();

        return tableProperties.Select(tableProperty => {
            return this.__GetColumnDefinition(tableProperty, tableProperty.GetCustomAttribute<Column>());
        }).Where(renderedColumn => renderedColumn != null);
    }

    /// <summary>
    /// Generates a collection of SQL key constraint definitions for the specified table type.
    /// </summary>
    /// <remarks>This method inspects the custom attributes applied to the specified table type to generate SQL key
    /// constraint  definitions. Supported key attributes include: <list type="bullet"> <item><description><see
    /// cref="Key"/>: Defines a general key constraint.</description></item> <item><description><see cref="PrimaryKey"/>:
    /// Defines a primary key constraint.</description></item> <item><description><see cref="UniqueKey"/>: Defines a unique
    /// key constraint.</description></item> <item><description><see cref="ForeignKey"/>: Defines a foreign key constraint,
    /// including references to another table.</description></item> </list> The generated SQL definitions include details
    /// such as constraint names, column names, and optional index types  (e.g., BTREE, HASH). For foreign keys, additional
    /// clauses like <c>ON DELETE</c> and <c>ON UPDATE</c> are included  if specified.</remarks>
    /// <param name="tableType">The type representing the table for which key constraint definitions are generated.  This type must have attributes
    /// defining keys, such as <see cref="Key"/>, <see cref="PrimaryKey"/>,  <see cref="UniqueKey"/>, or <see
    /// cref="ForeignKey"/>.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of strings, where each string represents a SQL key constraint definition  (e.g.,
    /// primary key, unique key, foreign key) for the specified table type. If no key attributes are found,  the collection
    /// will be empty.</returns>
    private IEnumerable<string?> __GetTableKeyDefinitions(Type tableType) {
        List<string> definitions = new List<string>();

        foreach (Key key in tableType.GetCustomAttributes<Key>()) {
            definitions.Add(
                $"CONSTRAINT {Query.FieldDelimiter}k_{key.Name}{Query.FieldDelimiter} KEY" +
                $"{(key.IndexType != IndexType.NONE ? $" USING {key.IndexType.GetDescription()} " : "")}" + 
                $"({string.Join(", ", key.Columns.Select(column => $"{Query.FieldDelimiter}{tableType.GetColumnName(column)}{Query.FieldDelimiter}"))})"
            );
        }
        foreach (PrimaryKey pKey in tableType.GetCustomAttributes<PrimaryKey>()) {
            definitions.Add(
                $"CONSTRAINT {Query.FieldDelimiter}pk_{pKey.Name}{Query.FieldDelimiter} PRIMARY KEY" +
                $"{(pKey.IndexType != IndexType.NONE ? $" USING {pKey.IndexType.GetDescription()} " : "")}" +
                $"({string.Join(", ", pKey.Columns.Select(column => $"{Query.FieldDelimiter}{tableType.GetColumnName(column)}{Query.FieldDelimiter}"))})"
            );
        }
        foreach (UniqueKey uKey in tableType.GetCustomAttributes<UniqueKey>()) {
            definitions.Add(
                $"CONSTRAINT {Query.FieldDelimiter}uk_{uKey.Name}{Query.FieldDelimiter} UNIQUE " +
                $"{(uKey.IndexType != IndexType.NONE ? $" USING {uKey.IndexType.GetDescription()} " : "")}" +
                $"({string.Join(", ", uKey.Columns.Select(column => $"{Query.FieldDelimiter}{tableType.GetColumnName(column)}{Query.FieldDelimiter}"))})"
            );
        }
        foreach (ForeignKey fKey in tableType.GetCustomAttributes<ForeignKey>()) {
            definitions.Add(
                $"CONSTRAINT {Query.FieldDelimiter}fk_{fKey.Name}{Query.FieldDelimiter} FOREIGN KEY " +
                $"({string.Join(", ", fKey.Columns.Select(column => $"{Query.FieldDelimiter}{column}{Query.FieldDelimiter}"))}) " + 
                $" REFERENCES {Query.FieldDelimiter}{fKey.ReferencedTable}{Query.FieldDelimiter}" +
                $"({string.Join(", ", fKey.ReferencedColumns.Select(column => $"{Query.FieldDelimiter}{column}{Query.FieldDelimiter}"))})" + 
                $"{(!string.IsNullOrWhiteSpace(fKey.OnDelete) ? $" ON DELETE {fKey.OnDelete}" : "")}" + 
                $"{(!string.IsNullOrWhiteSpace(fKey.OnUpdate) ? $" ON UPDATE {fKey.OnUpdate}" : "")}" 
            );
        }

        return definitions;
    }

    /// <summary>
    /// Generates the SQL column definition string for a given property and table column.
    /// </summary>
    /// <remarks>The generated column definition includes the column name, data type, length, precision,
    /// constraints (e.g., NOT NULL, UNIQUE), default values, comments, and other attributes based on the provided
    /// <paramref name="tableColumn"/> metadata. Special handling is applied for nullable types, enums, and specific
    /// data types such as VARCHAR and CHAR.</remarks>
    /// <param name="property">The <see cref="PropertyInfo"/> representing the property to map to the column.</param>
    /// <param name="tableColumn">The <see cref="Column"/> object containing metadata about the table column.</param>
    /// <returns>A string representing the SQL column definition, or <see langword="null"/> if <paramref name="tableColumn"/> is
    /// <see langword="null"/>.</returns>
    private string? __GetColumnDefinition(PropertyInfo property, Column tableColumn) {
        if (tableColumn == null) {
            return null;
        }

        Type columnType = property.PropertyType;
        if (Nullable.GetUnderlyingType(columnType) != null) {
            columnType = Nullable.GetUnderlyingType(columnType);
        }

        string columnDataTypeString = tableColumn.DataTypeString ?? this.GetColumnDataTypeString(tableColumn.DataType);

        StringBuilder columnBuilder = new StringBuilder($"{Query.FieldDelimiter}{tableColumn.Name}{Query.FieldDelimiter} {columnDataTypeString}");
        if (tableColumn.Length > 0 && !columnType.IsEnum)
            columnBuilder.Append($" ({tableColumn.Length}{(tableColumn.Precision > 0 ? $",{tableColumn.Precision}" : "")})");
        if (tableColumn.Length == -1) {
            if (tableColumn.DataType == ColumnDataType.Char)
                columnBuilder.Append($" (255)");
            if (tableColumn.DataType == ColumnDataType.Varchar)
                columnBuilder.Append($" (65535)");
        }
        if (columnType.IsEnum) {
            List<string> enumValues = new List<string>();

            bool first = true;
            foreach (Enum enumValue in Enum.GetValues(columnType)) {
                if (first) {
                    first = false;
                    continue;
                }
                string enumValueString = enumValue.ToString();

                if (!string.IsNullOrWhiteSpace(enumValue.GetDescription())) {
                    enumValues.Add(this.__RenderWhereValue(enumValue.GetDescription(), true));
                }
                else {
                    enumValues.Add(this.__RenderWhereValue(enumValue.ToString(), true));
                }
            }
            columnBuilder.Append($"({string.Join(',', enumValues)})");
        }
        if (tableColumn.Unsigned)
            columnBuilder.Append(" UNSIGNED");
        if (tableColumn.NotNull)
            columnBuilder.Append(" NOT NULL");
        if (tableColumn.AutoIncrement)
            columnBuilder.Append(" AUTO_INCREMENT");
        if (tableColumn.Unique && !tableColumn.PrimaryKey)
            columnBuilder.Append(" UNIQUE");
        if (tableColumn.Default != null)
            columnBuilder.Append($" DEFAULT {tableColumn.Default}");
        if (tableColumn.Comment != null)
            columnBuilder.Append($" COMMENT '{tableColumn.Comment}'");
        return columnBuilder.ToString();
    }

    /// <inheritdoc/>
    protected override string _RenderSelectExtraSentence() {
        if (this.QueryForUpdate) {
            return "FOR UPDATE";
        }

        return string.Empty;
    }
    #endregion

    #endregion

    #region Specific MySQL query operators
    /// <summary>
    /// Sets the query to use FOR UPDATE.
    /// </summary>
    /// <param name="forUpdate">True to use FOR UPDATE; otherwise, false.</param>
    /// <returns>The current query instance.</returns>
    public Query ForUpdate(bool forUpdate = true) {
        QueryForUpdate = forUpdate;

        return this;
    }
    #endregion

    #region Helper functions
    /// <summary>
    /// Converts the specified value into a string representation suitable for use in a "WHERE" clause, optionally
    /// applying delimiters for escaping.
    /// </summary>
    /// <param name="value">The value to be rendered. Can be a string, <see cref="DateTime"/>, <see cref="Enum"/>, or other types.</param>
    /// <param name="escape"><see langword="true"/> to apply delimiters around the value for escaping;  otherwise, <see langword="false"/>.</param>
    /// <returns>A string representation of the value, with delimiters applied if <paramref name="escape"/> is <see
    /// langword="true"/>  and the value is a string or <see cref="DateTime"/>. For <see cref="Enum"/> values, the
    /// description is returned.</returns>
    public string __RenderWhereValue(dynamic value, bool escape) {
        if (value is string
            ||
            value is DateTime
        ) {
            if (escape) {
                return $"{ValueDelimiter}{value}{ValueDelimiter}";
            }
        }
        if (value is Enum) {
            return $"{ValueDelimiter}{((Enum)value).GetDescription()}{ValueDelimiter}";
        }

        return value.ToString();
    }

    /// <summary>
    /// Converts a nullable <see cref="ColumnDataType"/> value to its corresponding MySQL data type string
    /// representation.
    /// </summary>
    /// <remarks>This method maps <see cref="ColumnDataType"/> values to their equivalent MySQL data type
    /// strings. For example, <see cref="ColumnDataType.Int32"/> is mapped to "INT", and <see
    /// cref="ColumnDataType.Boolean"/> is mapped to "BOOLEAN".</remarks>
    /// <param name="type">The nullable <see cref="ColumnDataType"/> to convert. If <paramref name="type"/> is <see langword="null"/>, the
    /// method returns <see langword="null"/>.</param>
    /// <returns>A <see cref="string"/> representing the MySQL data type corresponding to the specified <see
    /// cref="ColumnDataType"/> value, or <see langword="null"/> if <paramref name="type"/> is <see langword="null"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown if the specified <see cref="ColumnDataType"/> value is not supported by MySQL.</exception>
    public string GetColumnDataTypeString(ColumnDataType? type) {
        if (type == null) {
            return null;
        }

        return type switch {
            ColumnDataType.Boolean   => "BOOLEAN",
            ColumnDataType.Int16     => "SMALLINT",
            ColumnDataType.Int       => "INT",
            ColumnDataType.Int32     => "INT",
            ColumnDataType.Int64     => "BIGINT",
            ColumnDataType.UInt16    => "SMALLINT",
            ColumnDataType.UInt      => "INT",
            ColumnDataType.UInt32    => "INT",
            ColumnDataType.UInt64    => "BIGINT",
            ColumnDataType.Decimal   => "DECIMAL",
            ColumnDataType.Float     => "FLOAT",
            ColumnDataType.Double    => "DOUBLE",
            ColumnDataType.Text      => "TEXT",
            ColumnDataType.Char      => "CHAR",
            ColumnDataType.Varchar   => "VARCHAR",
            ColumnDataType.Enum      => "ENUM",
            ColumnDataType.Date      => "DATE",
            ColumnDataType.DateTime  => "DATETIME",
            ColumnDataType.Time      => "TIME",
            ColumnDataType.Timestamp => "TIMESTAMP",
            ColumnDataType.Binary    => "BLOB",
            ColumnDataType.Guid      => "CHAR",
            ColumnDataType.Json      => "JSON",
            ColumnDataType.Xml       => "TEXT",
            _ => throw new NotSupportedException($"MySQL does not support {type}")
        };
    }
    #endregion
}