using MySqlConnector;
using System;
using System.Data;
using System.Threading.Tasks;
using Unleasharp.ExtensionMethods;

namespace Unleasharp.DB.MySQL;

/// <summary>
/// Provides functionality for building and executing database queries using a MySQL connection.
/// </summary>
/// <remarks>This class extends the base query builder functionality to support MySQL-specific query execution. It
/// allows for the construction, execution, and management of queries, including support for synchronous and
/// asynchronous operations. The class handles query preparation, parameter binding, and result processing.</remarks>
public class QueryBuilder : Base.QueryBuilder<QueryBuilder, Connector, Query, MySqlConnection, MySqlConnectionStringBuilder> {
    /// <inheritdoc />
    public QueryBuilder(Connector dbConnector) : base(dbConnector) { }

    /// <inheritdoc />
    public QueryBuilder(Connector dbConnector, Query query) : base(dbConnector, query) { }

    #region Query execution
    /// <inheritdoc />
    protected override bool _Execute() {
        this.DBQuery.RenderPrepared();

        try {
            using (MySqlCommand queryCommand = new MySqlCommand(this.DBQuery.QueryPreparedString, this.Connector.Connection)) {
                this._PrepareDbCommand(queryCommand);

                switch (this.DBQuery.QueryType) {
                    case Base.QueryBuilding.QueryType.COUNT:
                        if (queryCommand.ExecuteScalar().TryConvert<int>(out int scalarCount)) {
                            this.TotalCount = scalarCount;
                        }
                        break;
                    case Base.QueryBuilding.QueryType.SELECT:
                    case Base.QueryBuilding.QueryType.SELECT_UNION:
                        using (MySqlDataReader queryReader = queryCommand.ExecuteReader()) {
                            this._HandleQueryResult(queryReader);
                        }
                        break;
                    case Base.QueryBuilding.QueryType.INSERT:
                        this.AffectedRows = queryCommand.ExecuteNonQuery();
                        if (this.DBQuery.QueryValues.Count == 1) {
                            this.LastInsertedId = queryCommand.LastInsertedId;
                        }
                        break;
                    case Base.QueryBuilding.QueryType.UPDATE:
                    default:
                        this.AffectedRows = queryCommand.ExecuteNonQuery();
                        break;
                }

                return true;
            }
        }
        catch (Exception ex) {
            this._OnQueryException(ex);
        }

        return false;
    }

    /// <inheritdoc />
    protected override T _ExecuteScalar<T>() {
        this.DBQuery.RenderPrepared();

        try {
            using (MySqlCommand queryCommand = new MySqlCommand(this.DBQuery.QueryPreparedString, this.Connector.Connection)) {
                this._PrepareDbCommand(queryCommand);
                if (queryCommand.ExecuteScalar().TryConvert<T>(out T scalarResult)) {
                    return scalarResult;
                }
            }
        }
        catch (Exception ex) {
            this._OnQueryException(ex);
        }

        return default(T);
    }

    /// <summary>
    /// Prepares the specified <see cref="MySqlCommand"/> by adding parameters based on the query's prepared data.
    /// </summary>
    /// <remarks>If a value in the prepared data is an <see cref="Enum"/>, its description (retrieved via a
    /// custom extension method) is used as the parameter value. Otherwise, the value itself is used. After all
    /// parameters are added, the command is prepared by calling <see cref="MySqlCommand.Prepare"/>.</remarks>
    /// <param name="queryCommand">The <see cref="MySqlCommand"/> to be prepared. This command will have its parameters populated using the keys
    /// and values from the query's prepared data.</param>
    private void _PrepareDbCommand(MySqlCommand queryCommand) {
        foreach (string queryPreparedDataKey in this.DBQuery.QueryPreparedData.Keys) {
            if (this.DBQuery.QueryPreparedData[queryPreparedDataKey].Value is Enum) {
                queryCommand.Parameters.AddWithValue(queryPreparedDataKey, ((Enum) this.DBQuery.QueryPreparedData[queryPreparedDataKey].Value).GetDescription());
                continue;
            }
            queryCommand.Parameters.AddWithValue(queryPreparedDataKey, this.DBQuery.QueryPreparedData[queryPreparedDataKey].Value);
        }
        queryCommand.Prepare();
    }

    /// <inheritdoc />
    protected override async Task<bool> _ExecuteAsync() {
        this.DBQuery.RenderPrepared();

        try {
            using (MySqlCommand queryCommand = new MySqlCommand(this.DBQuery.QueryPreparedString, this.Connector.Connection)) {
                switch (this.DBQuery.QueryType) {
                    case Base.QueryBuilding.QueryType.COUNT:
                        if ((await queryCommand.ExecuteScalarAsync()).TryConvert<int>(out int scalarCount)) {
                            this.TotalCount = scalarCount;
                        }
                        return true;
                    case Base.QueryBuilding.QueryType.SELECT:
                        foreach (string queryPreparedDataKey in this.DBQuery.QueryPreparedData.Keys) {
                            queryCommand.Parameters.AddWithValue(queryPreparedDataKey, this.DBQuery.QueryPreparedData[queryPreparedDataKey].Value);
                        }
                        queryCommand.Prepare();

                        using (MySqlDataReader queryReader = await queryCommand.ExecuteReaderAsync()) {
                            await this._HandleQueryResultAsync(queryReader);
                        }
                        return true;
                    default:
                        this.AffectedRows = await queryCommand.ExecuteNonQueryAsync();
                        return true;
                }
            }
        }

        catch (Exception ex) { 
        }

        return false;
    }

    /// <summary>
    /// Processes the result of a SQL query and populates a <see cref="DataTable"/> with the retrieved data.
    /// </summary>
    /// <remarks>This method creates a new <see cref="DataTable"/> and populates it with the data from the
    /// provided  <see cref="MySqlDataReader"/>. Column names in the resulting <see cref="DataTable"/> are made unique 
    /// by appending a suffix if necessary. If the query result includes base table and column information,  column
    /// names are prefixed with the base table name in the format "BaseTable::BaseColumn".</remarks>
    /// <param name="queryReader">A <see cref="MySqlDataReader"/> instance containing the query result to process.  The reader must be positioned
    /// at the start of the result set.</param>
    private void _HandleQueryResult(MySqlDataReader queryReader) {
        this.Result = new DataTable();

        // Get schema information for all columns
        DataTable schemaTable = queryReader.GetSchemaTable();

        // Build column list with unique names
        for (int i = 0; i < queryReader.FieldCount; i++) {
            DataRow schemaRow = schemaTable.Rows[i];

            string columnName = (string)schemaRow["ColumnName"];     // Alias (or same as base if no alias)
            string baseTable  = schemaRow["BaseTableName"] ?.ToString();
            string baseColumn = schemaRow["BaseColumnName"]?.ToString();

            // If we have a base table, make a safe unique name
            string safeName;
            if (!string.IsNullOrEmpty(baseTable) && !string.IsNullOrEmpty(baseColumn)) {
                safeName = $"{baseTable}::{baseColumn}";
            }
            else {
                safeName = columnName;
            }

            // If still duplicated, add a suffix
            string finalName = safeName;
            int suffix = 1;
            while (this.Result.Columns.Contains(finalName)) {
                finalName = $"{safeName}_{suffix++}";
            }

            this.Result.Columns.Add(new DataColumn(finalName, queryReader.GetFieldType(i)));
        }

        // Load rows
        object[] rowData = new object[this.Result.Columns.Count];
        this.Result.BeginLoadData();
        while (queryReader.Read()) {
            queryReader.GetValues(rowData);
            this.Result.LoadDataRow(rowData, true);

            rowData = new object[this.Result.Columns.Count]; // reset
        }
        this.Result.EndLoadData();
    }

    /// <summary>
    /// Processes the results of a MySQL query asynchronously and populates a <see cref="DataTable"/> with the retrieved
    /// data.
    /// </summary>
    /// <remarks>This method reads the schema information from the query results to construct a <see
    /// cref="DataTable"/>  with uniquely named columns. If column names conflict, unique suffixes are appended to
    /// ensure no duplicates. The method then iterates through the query results, loading each row into the <see
    /// cref="DataTable"/>.  The resulting <see cref="DataTable"/> is stored in the <c>Result</c> property of the
    /// containing class.</remarks>
    /// <param name="queryReader">An instance of <see cref="MySqlDataReader"/> that provides access to the query results.  The reader must be open
    /// and positioned at the start of the result set.</param>
    /// <returns></returns>
    private async Task _HandleQueryResultAsync(MySqlDataReader queryReader) {
        this.Result = new DataTable();

        // Get schema information for all columns
        DataTable schemaTable = await queryReader.GetSchemaTableAsync();

        // Build column list with unique names
        for (int i = 0; i < queryReader.FieldCount; i++) {
            DataRow schemaRow = schemaTable.Rows[i];

            string columnName = (string)schemaRow["ColumnName"];     // Alias (or same as base if no alias)
            string baseTable  = schemaRow["BaseTableName"] ?.ToString();
            string baseColumn = schemaRow["BaseColumnName"]?.ToString();

            // If we have a base table, make a safe unique name
            string safeName;
            if (!string.IsNullOrEmpty(baseTable) && !string.IsNullOrEmpty(baseColumn)) {
                safeName = $"{baseTable}::{baseColumn}";
            }
            else {
                safeName = columnName;
            }

            // If still duplicated, add a suffix
            string finalName = safeName;
            int suffix = 1;
            while (this.Result.Columns.Contains(finalName)) {
                finalName = $"{safeName}_{suffix++}";
            }

            this.Result.Columns.Add(new DataColumn(finalName, queryReader.GetFieldType(i)));
        }

        // Load rows
        object[] rowData = new object[this.Result.Columns.Count];
        this.Result.BeginLoadData();
        while (await queryReader.ReadAsync()) {
            queryReader.GetValues(rowData);
            this.Result.LoadDataRow(rowData, true);

            rowData = new object[this.Result.Columns.Count]; // reset
        }
        this.Result.EndLoadData();
    }
    #endregion
}
