using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Unleasharp.DB.Base.QueryBuilding;
using Unleasharp.DB.Base.SchemaDefinition;
using Unleasharp.ExtensionMethods;

namespace Unleasharp.DB.MySQL;

public class Query : Unleasharp.DB.Base.Query<Query> {
    #region Custom MySQL query data
    public bool    QueryForUpdate { get; private set; } = false;
    #endregion

    public const string FieldDelimiter = "`";
    public const string ValueDelimiter = "'";

    #region Query rendering
    #region Query fragment rendering
    public override void _RenderPrepared(bool Force) {
        this._Render(Force);

        string Rendered = this.QueryPreparedString;
        foreach (KeyValuePair<string, PreparedValue> PreparedDataItem in this.QueryPreparedData) {
            if (PreparedDataItem.Value.Value == null) {
                Rendered = Rendered.Replace(PreparedDataItem.Key, "NULL");
            }
            else {
                Rendered = Rendered.Replace(PreparedDataItem.Key, this.__RenderWhereValue(PreparedDataItem.Value.Value, PreparedDataItem.Value.EscapeValue));
            }
        }

        this.QueryRendered = Rendered;
    }

    public string RenderSelect(Select<Query> Fragment) {
        if (Fragment.Subquery != null) {
            return "(" + Fragment.Subquery.WithParentQuery(this.ParentQuery != null ? this.ParentQuery : this).Render() + ")";
        }

        return Fragment.Field.Render() + (!string.IsNullOrWhiteSpace(Fragment.Alias) ? $" AS {Fragment.Alias}" : "");
    }

    public string RenderFrom(From<Query> Fragment) {
        if (Fragment.Subquery != null) {
            return "(" + Fragment.Subquery.WithParentQuery(this.ParentQuery != null ? this.ParentQuery : this).Render() + ")";
        }

        string Rendered = string.Empty;

        if (!string.IsNullOrWhiteSpace(Fragment.Table)) {
            if (Fragment.EscapeTable) {
                Rendered = FieldDelimiter + Fragment.Table + FieldDelimiter;
            }
            else {
                Rendered = Fragment.Table;
            }
        }

        return Rendered + (Fragment.TableAlias != string.Empty ? $" {Fragment.TableAlias}" : "");
    }

    public string RenderJoin(Join<Query> Fragment) {
        return $"{(Fragment.EscapeTable ? FieldDelimiter + Fragment.Table + FieldDelimiter : Fragment.Table)} ON {this.RenderWhere(Fragment.Condition)}";
    }

    public string RenderGroupBy(GroupBy Fragment) {
        List<string> ToRender = new List<string>();

        if (!string.IsNullOrWhiteSpace(Fragment.Field.Table)) {
            if (Fragment.Field.Escape) {
                ToRender.Add(FieldDelimiter + Fragment.Field.Table + FieldDelimiter);
            }
            else {
                ToRender.Add(Fragment.Field.Table);
            }
        }

        if (!string.IsNullOrWhiteSpace(Fragment.Field.Field)) {
            if (Fragment.Field.Escape) {
                ToRender.Add(FieldDelimiter + Fragment.Field.Field + FieldDelimiter);
            }
            else {
                ToRender.Add(Fragment.Field.Field);
            }
        }

        return String.Join('.', ToRender);
    }

    public string RenderWhere(Where<Query> Fragment) {
        if (Fragment.Subquery != null) {
            return $"{Fragment.Field.Render()} {Fragment.Comparer.GetDescription()} ({Fragment.Subquery.WithParentQuery(this.ParentQuery != null ? this.ParentQuery : this).Render()})";
        }

        List<string> ToRender = new List<string>();

        ToRender.Add(Fragment.Field.Render());

        // We are comparing fields, not values
        if (Fragment.ValueField != null) {
            ToRender.Add(Fragment.ValueField.Render());
        }
        else {
            if (Fragment.Value == null) {
                Fragment.Comparer = WhereComparer.IS;
                ToRender.Add("NULL");
            }
            else {
                if (Fragment.EscapeValue) {
                    ToRender.Add(this.PrepareQueryValue(Fragment.Value, Fragment.EscapeValue));
                }
                else {
                    ToRender.Add(this.__RenderWhereValue(Fragment.Value, false));
                }
            }
        }

        return String.Join(Fragment.Comparer.GetDescription(), ToRender);
    }

    public string RenderWhereIn(WhereIn<Query> Fragment) {
        if (Fragment.Subquery != null) {
            return $"{Fragment.Field.Render()} IN ({Fragment.Subquery.WithParentQuery(this.ParentQuery != null ? this.ParentQuery : this).Render()})";
        }

        if (Fragment.Values == null || Fragment.Values.Count == 0) {
            return String.Empty;
        }

        List<string> ToRender = new List<string>();

        foreach (dynamic FragmentValue in Fragment.Values) {
            if (Fragment.EscapeValue) {
                ToRender.Add(this.PrepareQueryValue(FragmentValue, Fragment.EscapeValue));
            }
            else {
                ToRender.Add(__RenderWhereValue(FragmentValue, Fragment.EscapeValue));
            }
        }

        return $"{Fragment.Field.Render()} IN ({String.Join(",", ToRender)})";
    }

    public string RenderFieldSelector(FieldSelector Fragment) {
        List<string> ToRender = new List<string>();

        if (!string.IsNullOrWhiteSpace(Fragment.Table)) {
            if (Fragment.Escape) {
                ToRender.Add(FieldDelimiter + Fragment.Table + FieldDelimiter);
            }
            else {
                ToRender.Add(Fragment.Table);
            }
        }

        if (!string.IsNullOrWhiteSpace(Fragment.Field)) {
            if (Fragment.Escape) {
                ToRender.Add(FieldDelimiter + Fragment.Field + FieldDelimiter);
            }
            else {
                ToRender.Add(Fragment.Field);
            }
        }

        return String.Join('.', ToRender);
    }
    #endregion

    #region Query sentence rendering
    protected override string _RenderCountSentence() {
        return "SELECT COUNT(*)";
    }

    protected override string _RenderSelectSentence() {
        List<string> Rendered = new List<string>();

        if (this.QuerySelect.Count > 0) {
            foreach (Select<Query> QueryFragment in this.QuerySelect) {
                Rendered.Add(this.RenderSelect(QueryFragment));
            }
        }
        else {
            Rendered.Add("*");
        }

        return "SELECT " + string.Join(',', Rendered);
    }

    protected override string _RenderFromSentence() {
        List<string> Rendered = new List<string>();

        foreach (From<Query> QueryFragment in this.QueryFrom) {
            Rendered.Add(this.RenderFrom(QueryFragment));
        }

        return (Rendered.Count > 0 ? "FROM " + string.Join(',', Rendered) : "");
    }

    protected override string _RenderJoinSentence() {
        List<string> Rendered = new List<string>();
        foreach (Join<Query> QueryFragment in this.QueryJoin) {
            Rendered.Add(this.RenderJoin(QueryFragment));
        }

        return (Rendered.Count > 0 ? "JOIN " + string.Join(',', Rendered) : "");
    }

    protected override string _RenderWhereSentence() {
        List<string> Rendered = new List<string>();
        foreach (Where<Query> QueryFragment in this.QueryWhere) {
            if (Rendered.Any()) {
                Rendered.Add(QueryFragment.Operator.GetDescription());
            }
            Rendered.Add(this.RenderWhere(QueryFragment));
        }
        foreach (WhereIn<Query> QueryFragment in this.QueryWhereIn) {
            if (Rendered.Any()) {
                Rendered.Add(QueryFragment.Operator.GetDescription());
            }
            Rendered.Add(this.RenderWhereIn(QueryFragment));
        }

        return (Rendered.Count > 0 ? "WHERE " + string.Join(' ', Rendered) : "");
    }

    protected override string _RenderGroupSentence() {
        List<string> Rendered = new List<string>();

        foreach (GroupBy QueryFragment in this.QueryGroup) {
            Rendered.Add(this.RenderGroupBy(QueryFragment));
        }

        return (Rendered.Count > 0 ? "GROUP BY " + string.Join(',', Rendered) : "");
    }

    protected override string _RenderHavingSentence() {
        List<string> Rendered = new List<string>();

        foreach (Where<Query> QueryFragment in this.QueryHaving) {
            Rendered.Add(this.RenderWhere(QueryFragment));
        }

        return (Rendered.Count > 0 ? "HAVING " + string.Join(',', Rendered) : "");
    }

    protected override string _RenderOrderSentence() {
        List<string> Rendered = new List<string>();

        if (this.QueryOrder != null) {
            foreach (OrderBy QueryOrderItem in this.QueryOrder) {
                List<string> RenderedSubset = new List<string>();

                RenderedSubset.Add(QueryOrderItem.Field.Render());

                if (QueryOrderItem.Direction != OrderDirection.NONE) {
                    RenderedSubset.Add(QueryOrderItem.Direction.GetDescription());
                }

                Rendered.Add(string.Join(' ', RenderedSubset));
            }
        }

        return (Rendered.Count > 0 ? "ORDER BY " + string.Join(',', Rendered) : "");
    }

    protected override string _RenderLimitSentence() {
        List<string> Rendered = new List<string>();
        if (this.QueryLimit != null) {
            if (this.QueryLimit.Offset >= 0) {
                Rendered.Add(this.QueryLimit.Offset.ToString());
            }
            if (this.QueryLimit.Count > 0) {
                Rendered.Add(this.QueryLimit.Count.ToString());
            }
        }

        return (Rendered.Count > 0 ? "LIMIT " + string.Join(',', Rendered) : "");
    }


    protected override string _RenderDeleteSentence() {
        From<Query> From = this.QueryFrom.FirstOrDefault();

        if (From != null) {
            return $"DELETE FROM {From.Table}{(!string.IsNullOrWhiteSpace(From.TableAlias) ? $" AS {From.TableAlias}" : "")}";
        }

        return string.Empty;
    }
    protected override string _RenderUpdateSentence() { 
        From<Query> From = this.QueryFrom.FirstOrDefault();

        if (From != null) {
            return $"UPDATE {this.RenderFrom(From)}";
        }

        return string.Empty;
    }

    protected override string _RenderSetSentence() {
        List<string> Rendered = new List<string>();

        if (this.QueryOrder != null) {
            foreach (Where<Query> QuerySetItem in this.QuerySet) {
                QuerySetItem.Comparer = WhereComparer.EQUALS;

                Rendered.Add(this.RenderWhere(QuerySetItem));
            }
        }

        return (Rendered.Count > 0 ? "SET " + string.Join(',', Rendered) : "");
    }

    protected override string _RenderInsertIntoSentence() { 
        From<Query> From = this.QueryFrom.FirstOrDefault();

        if (From != null) {
            return $"INSERT INTO {From.Table} ({string.Join(',', this.QueryColumns)})";
        }

        return string.Empty;
    }

    protected override string _RenderInsertValuesSentence() {
        List<string> Rendered = new List<string>();

        if (this.QueryValues != null) {
            foreach (Dictionary<string, dynamic> QueryValue in QueryValues) {
                List<string> ToRender = new List<string>();

                // In order to get a valid query, insert the values in the same column order
                foreach (string QueryColumn in this.QueryColumns) {
                    if (QueryValue.ContainsKey(QueryColumn) && QueryValue[QueryColumn] != null) {
                        ToRender.Add(this.PrepareQueryValue(QueryValue[QueryColumn], true));
                    }
                    else {
                        ToRender.Add("NULL");
                    }
                }

                Rendered.Add($"({string.Join(",", ToRender)})");
            }
        }

        return (Rendered.Count > 0 ? "VALUES " + string.Join(',', Rendered) : "");
    }


    protected override string _RenderCreateSentence<T>() {
        return this._RenderCreateSentence(typeof(T));
    }

    protected override string _RenderCreateSentence(Type TableType) {
        Table TypeTable = TableType.GetCustomAttribute<Table>();
        if (TypeTable == null) {
            throw new InvalidOperationException("Missing [Table] attribute");
        }

        StringBuilder Rendered = new StringBuilder();

        Rendered.Append("CREATE ");
        if (TypeTable.Temporary) {
            Rendered.Append("TEMPORARY ");
        }

        Rendered.Append("TABLE ");
        if (TypeTable.IfNotExists) {
            Rendered.Append("IF NOT EXISTS ");
        }
        Rendered.Append($"{Query.FieldDelimiter}{TypeTable.Name}{Query.FieldDelimiter} (");

        PropertyInfo[] TableProperties = TableType.GetProperties();
        FieldInfo   [] TableFields     = TableType.GetFields();

        IEnumerable<string?> TableEntries = TableProperties.Select(TableProperty => {
            return this.__GetColumnDefinition(TableProperty, TableProperty.GetCustomAttribute<Column>());
        }).Where(renderedColumn => renderedColumn != null);
        IEnumerable<string?> TableKeys = TableType.GetCustomAttributes<Key>().Select(TableKey => {
            return this.__GetKeyDefinition(TableKey);
        }).Where(renderedColumn => renderedColumn != null);

        Rendered.Append(string.Join(",", TableEntries.Concat(TableKeys ?? Enumerable.Empty<string>())));
        Rendered.Append(")");

        // Table options
        var TableOptions = TableType.GetCustomAttributes<TableOption>();
        foreach (TableOption TableOption in TableOptions) {
            Rendered.Append($" {TableOption.Name}={TableOption.Value}");
        }

        return Rendered.ToString();
    }

    private string? __GetKeyDefinition(Key TableKey) {
        if (TableKey == null) {
            return null;
        }

        string KeyName = TableKey.Name ?? string.Join("_", TableKey.Fields);

        switch (TableKey.KeyType) {
            case KeyType.NONE:
                return 
                    $"KEY {Query.FieldDelimiter}{TableKey.Name}{Query.FieldDelimiter} " + 
                    $"({string.Join(", ", TableKey.Fields.Select(field => $"{Query.FieldDelimiter}{field}{Query.FieldDelimiter}"))}) " + 
                    $"USING {TableKey.IndexType}"
                ;
            case KeyType.PRIMARY:
            case KeyType.UNIQUE:
                return
                    $"{TableKey.KeyType.GetDescription()} KEY {Query.FieldDelimiter}pk_{TableKey.Name}{Query.FieldDelimiter} " +
                    $"({string.Join(", ", TableKey.Fields.Select(field => $"{Query.FieldDelimiter}{field}{Query.FieldDelimiter}"))})"
                ;
            case KeyType.FOREIGN:
                StringBuilder fkBuilder = new StringBuilder();

                fkBuilder.Append($"CONSTRAINT {Query.FieldDelimiter}fk_{TableKey.Name}{Query.FieldDelimiter} ");
                fkBuilder.Append($"REFERENCES {Query.FieldDelimiter}{TableKey.References.Table}{Query.FieldDelimiter}");
                fkBuilder.Append($"({Query.FieldDelimiter}{TableKey.References.Field}{Query.FieldDelimiter}) ");
                if (!string.IsNullOrWhiteSpace(TableKey.OnDelete)) {
                    fkBuilder.Append($"ON DELETE {TableKey.OnDelete}");
                }
                if (!string.IsNullOrWhiteSpace(TableKey.OnUpdate)) {
                    fkBuilder.Append($"ON UPDATE {TableKey.OnUpdate}");
                }

                return fkBuilder.ToString();
        }

        return null;
    }

    private string? __GetColumnDefinition(PropertyInfo Property, Column TableColumn) {
        if (TableColumn == null) {
            return null;
        }

        Type ColumnType = Property.PropertyType;
        if (Nullable.GetUnderlyingType(ColumnType) != null) {
            ColumnType = Nullable.GetUnderlyingType(ColumnType);
        }

        StringBuilder ColumnBuilder = new StringBuilder($"{Query.FieldDelimiter}{TableColumn.Name}{Query.FieldDelimiter} {TableColumn.DataType}");
        if (TableColumn.Length > 0)
            ColumnBuilder.Append($" ({TableColumn.Length}{(TableColumn.Precision > 0 ? $",{TableColumn.Precision}" : "")})");
        if (ColumnType.IsEnum) {

            List<string> EnumValues = new List<string>();

            bool First = true;
            foreach (Enum EnumValue in Enum.GetValues(ColumnType)) {
                if (First) {
                    First = false;
                    continue;
                }
                string EnumValueString = EnumValue.ToString();

                if (!string.IsNullOrWhiteSpace(EnumValue.GetDescription())) {
                    EnumValues.Add(this.__RenderWhereValue(EnumValue.GetDescription(), true));
                }
                else {
                    EnumValues.Add(this.__RenderWhereValue(EnumValue.ToString(), true));
                }
            }
            ColumnBuilder.Append($"({string.Join(',', EnumValues)})");
        }
        if (TableColumn.Unsigned)
            ColumnBuilder.Append(" UNSIGNED");
        if (TableColumn.NotNull)
            ColumnBuilder.Append(" NOT NULL");
        if (TableColumn.AutoIncrement)
            ColumnBuilder.Append(" AUTO_INCREMENT");
        if (TableColumn.Unique && !TableColumn.PrimaryKey)
            ColumnBuilder.Append(" UNIQUE");
        if (TableColumn.Default != null)
            ColumnBuilder.Append($" DEFAULT {TableColumn.Default}");
        if (TableColumn.Comment != null)
            ColumnBuilder.Append($" COMMENT '{TableColumn.Comment}'");
        return ColumnBuilder.ToString();
    }

    protected override string _RenderSelectExtraSentence() {
        if (this.QueryForUpdate) {
            return "FOR UPDATE";
        }

        return string.Empty;
    }
    #endregion

    #endregion

    #region Specific MySQL query operators
    public Query ForUpdate(bool ForUpdate = true) {
        QueryForUpdate = ForUpdate;

        return this;
    }
    #endregion

    #region Helper functions
    public string __RenderWhereValue(dynamic Value, bool Escape) {
        if (Value is string
            ||
            Value is DateTime
            ||
            Value is Enum
        ) {
            if (Escape) {
                return ValueDelimiter + Value + ValueDelimiter;
            }
        }

        return Value.ToString();
    }
    #endregion
}
