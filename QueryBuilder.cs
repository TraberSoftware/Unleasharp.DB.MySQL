using MySqlConnector;
using System;
using System.Data;
using System.Threading.Tasks;
using Unleasharp.ExtensionMethods;

namespace Unleasharp.DB.MySQL;

public class QueryBuilder : Base.QueryBuilder<QueryBuilder, Connector, Query, MySqlConnection, MySqlConnectionStringBuilder> {
    public QueryBuilder(Connector dbConnector) : base(dbConnector) { }

    public QueryBuilder(Connector dbConnector, Query query) : base(dbConnector, query) { }

    #region Query execution
    protected override bool _Execute() {
        this.DBQuery.RenderPrepared();

        try {
            using (MySqlCommand queryCommand = new MySqlCommand(this.DBQuery.QueryPreparedString, this.Connector.Connection)) {
                switch (this.DBQuery.QueryType) {
                    case Base.QueryBuilding.QueryType.COUNT:
						this._PrepareDbCommand(queryCommand);

						if (queryCommand.ExecuteScalar().TryConvert<int>(out int scalarCount)) {
                            this.TotalCount = scalarCount;
                        }
                        return true;
                    case Base.QueryBuilding.QueryType.SELECT:
						this._PrepareDbCommand(queryCommand);

						using (MySqlDataReader queryReader = queryCommand.ExecuteReader()) {
                            this._HandleQueryResult(queryReader);
                        }
                        return true;
                    case Base.QueryBuilding.QueryType.UPDATE:
                    default:
						this._PrepareDbCommand(queryCommand);

						this.AffectedRows = queryCommand.ExecuteNonQuery();

                        return true;
                }
            }
        }
        catch (Exception ex) {
        }

        return false;
    }

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

    private void _HandleQueryResult(MySqlDataReader queryReader) {
        this.Result = new DataTable();

        for (int i = 0; i < queryReader.FieldCount; i++) {
            this.Result.Columns.Add(new DataColumn(queryReader.GetName(i), queryReader.GetFieldType(i)));
        }

        object[] rowData = new object[this.Result.Columns.Count];

        this.Result.BeginLoadData();
        while (queryReader.Read()) {
            queryReader.GetValues(rowData);
            this.Result.LoadDataRow(rowData, true);

            // Reinstanciate the row data holder
            rowData = new object[this.Result.Columns.Count];
        }
        this.Result.EndLoadData();
    }

    private async Task _HandleQueryResultAsync(MySqlDataReader queryReader) {

        this.Result = new DataTable();

        for (int i = 0; i < queryReader.FieldCount; i++) {
            this.Result.Columns.Add(new DataColumn(queryReader.GetName(i), queryReader.GetFieldType(i)));
        }

        object[] rowData = new object[this.Result.Columns.Count];

        this.Result.BeginLoadData();
        while (await queryReader.ReadAsync()) {
            queryReader.GetValues(rowData);
            this.Result.LoadDataRow(rowData, true);

            // Reinstanciate the row data holder
            rowData = new object[this.Result.Columns.Count];
        }
        this.Result.EndLoadData();
    }
    #endregion
}
