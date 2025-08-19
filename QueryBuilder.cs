using MySqlConnector;
using System;
using System.Data;
using System.Threading.Tasks;
using Unleasharp.ExtensionMethods;

namespace Unleasharp.DB.MySQL {
    public class QueryBuilder : Base.QueryBuilder<QueryBuilder, Connector, Query, MySqlConnection, MySqlConnectionStringBuilder> {
        public QueryBuilder(Connector DBConnector) : base(DBConnector) { }

        public QueryBuilder(Connector DBConnector, Query Query) : base(DBConnector, Query) { }

        #region Query execution
        protected override bool _Execute() {
            this.DBQuery.RenderPrepared();

            try {
                using (MySqlCommand QueryCommand = new MySqlCommand(this.DBQuery.QueryPreparedString, this.Connector.Connection)) {
                    switch (this.DBQuery.QueryType) {
                        case Base.QueryBuilding.QueryType.COUNT:
                            foreach (string QueryPreparedDataKey in this.DBQuery.QueryPreparedData.Keys) {
                                QueryCommand.Parameters.AddWithValue(QueryPreparedDataKey, this.DBQuery.QueryPreparedData[QueryPreparedDataKey].Value);
                            }
                            QueryCommand.Prepare();

                            if (QueryCommand.ExecuteScalar().TryConvert<int>(out int ScalarCount)) {
                                this.TotalCount = ScalarCount;
                            }
                            return true;
                        case Base.QueryBuilding.QueryType.SELECT:
                            foreach (string QueryPreparedDataKey in this.DBQuery.QueryPreparedData.Keys) {
                                QueryCommand.Parameters.AddWithValue(QueryPreparedDataKey, this.DBQuery.QueryPreparedData[QueryPreparedDataKey].Value);
                            }
                            QueryCommand.Prepare();

                            using (MySqlDataReader QueryReader = QueryCommand.ExecuteReader()) {
                                this._HandleQueryResult(QueryReader);
                            }
                            return true;
                        case Base.QueryBuilding.QueryType.UPDATE:
                        default:
                            foreach (string QueryPreparedDataKey in this.DBQuery.QueryPreparedData.Keys) {
                                QueryCommand.Parameters.AddWithValue(QueryPreparedDataKey, this.DBQuery.QueryPreparedData[QueryPreparedDataKey].Value);
                            }
                            QueryCommand.Prepare();

                            this.AffectedRows = QueryCommand.ExecuteNonQuery();

                            return true;
                    }
                }
            }
            catch (Exception ex) {
            }

            return false;
        }

        protected override async Task<bool> _ExecuteAsync() {
            this.DBQuery.RenderPrepared();

            try {
                using (MySqlCommand QueryCommand = new MySqlCommand(this.DBQuery.QueryPreparedString, this.Connector.Connection)) {
                    switch (this.DBQuery.QueryType) {
                        case Base.QueryBuilding.QueryType.COUNT:
                            if ((await QueryCommand.ExecuteScalarAsync()).TryConvert<int>(out int ScalarCount)) {
                                this.TotalCount = ScalarCount;
                            }
                            return true;
                        case Base.QueryBuilding.QueryType.SELECT:
                            foreach (string QueryPreparedDataKey in this.DBQuery.QueryPreparedData.Keys) {
                                QueryCommand.Parameters.AddWithValue(QueryPreparedDataKey, this.DBQuery.QueryPreparedData[QueryPreparedDataKey].Value);
                            }
                            QueryCommand.Prepare();

                            using (MySqlDataReader QueryReader = await QueryCommand.ExecuteReaderAsync()) {
                                await this._HandleQueryResultAsync(QueryReader);
                            }
                            return true;
                        default:
                            this.AffectedRows = await QueryCommand.ExecuteNonQueryAsync();
                            return true;
                    }
                }
            }

            catch (Exception ex) { 
            }

            return false;
        }

        private void _HandleQueryResult(MySqlDataReader QueryReader) {
            this.Result = new DataTable();

            for (int i = 0; i < QueryReader.FieldCount; i++) {
                this.Result.Columns.Add(new DataColumn(QueryReader.GetName(i), QueryReader.GetFieldType(i)));
            }

            object[] RowData = new object[this.Result.Columns.Count];

            this.Result.BeginLoadData();
            while (QueryReader.Read()) {
                QueryReader.GetValues(RowData);
                this.Result.LoadDataRow(RowData, true);

                // Reinstanciate the row data holder
                RowData = new object[this.Result.Columns.Count];
            }
            this.Result.EndLoadData();
        }

        private async Task _HandleQueryResultAsync(MySqlDataReader QueryReader) {

            this.Result = new DataTable();

            for (int i = 0; i < QueryReader.FieldCount; i++) {
                this.Result.Columns.Add(new DataColumn(QueryReader.GetName(i), QueryReader.GetFieldType(i)));
            }

            object[] RowData = new object[this.Result.Columns.Count];

            this.Result.BeginLoadData();
            while (await QueryReader.ReadAsync()) {
                QueryReader.GetValues(RowData);
                this.Result.LoadDataRow(RowData, true);

                // Reinstanciate the row data holder
                RowData = new object[this.Result.Columns.Count];
            }
            this.Result.EndLoadData();
        }
        #endregion
    }
}
