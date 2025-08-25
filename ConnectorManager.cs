using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Text;
using Unleasharp.DB.Base;

namespace Unleasharp.DB.MySQL;

/// <summary>
/// Manager class for MySQL database connections that provides access to query builders
/// for constructing and executing SQL queries.
/// </summary>
public class ConnectorManager : 
    ConnectorManager<ConnectorManager, Connector, MySqlConnectionStringBuilder, MySqlConnection, QueryBuilder, Query>
{
    /// <inheritdoc />
    public ConnectorManager()                                           : base() { }

    /// <inheritdoc />
    public ConnectorManager(MySqlConnectionStringBuilder stringBuilder) : base(stringBuilder) { }

    /// <inheritdoc />
    public ConnectorManager(string connectionString)                    : base(connectionString) { }
}
