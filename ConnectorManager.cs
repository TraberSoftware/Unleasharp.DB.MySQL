using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Text;
using Unleasharp.DB.Base;

namespace Unleasharp.DB.MySQL;

public class ConnectorManager : 
    ConnectorManager<ConnectorManager, Connector, MySqlConnectionStringBuilder>, 
    IConnectorManager<QueryBuilder, Connector, Query, MySqlConnection, MySqlConnectionStringBuilder> 
{
    public ConnectorManager()                                           : base() { }
    public ConnectorManager(MySqlConnectionStringBuilder stringBuilder) : base(stringBuilder) { }
    public ConnectorManager(string connectionString)                    : base(connectionString) { }

    public QueryBuilder QueryBuilder() {
        return new QueryBuilder(this.GetInstance());
    }

    public QueryBuilder DetachedQueryBuilder() {
        return new QueryBuilder(this.GetDetachedInstance());
    }

    public QueryBuilder QueryBuilder(Query query) {
        return new QueryBuilder(this.GetInstance(), query);
    }

    public QueryBuilder DetachedQueryBuilder(Query query) {
        return new QueryBuilder(this.GetDetachedInstance(), query);
    }
}
