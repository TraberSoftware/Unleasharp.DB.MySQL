using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Text;
using Unleasharp.DB.Base;

namespace Unleasharp.DB.MySQL {
    public class ConnectorManager : 
        ConnectorManager<ConnectorManager, Connector, MySqlConnectionStringBuilder>, 
        IConnectorManager<QueryBuilder, Connector, Query, MySqlConnection, MySqlConnectionStringBuilder> 
    {
        public ConnectorManager()                                           : base() { }
        public ConnectorManager(MySqlConnectionStringBuilder StringBuilder) : base(StringBuilder) { }
        public ConnectorManager(string ConnectionString)                    : base(ConnectionString) { }

        public QueryBuilder QueryBuilder() {
            return new QueryBuilder(this.GetInstance());
        }

        public QueryBuilder DetachedQueryBuilder() {
            return new QueryBuilder(this.GetDetachedInstance());
        }

        public QueryBuilder QueryBuilder(Query Query) {
            return new QueryBuilder(this.GetInstance(), Query);
        }

        public QueryBuilder DetachedQueryBuilder(Query Query) {
            return new QueryBuilder(this.GetDetachedInstance(), Query);
        }
    }
}
