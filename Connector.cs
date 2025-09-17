using System;
using MySqlConnector;
using Unleasharp.DB.Base;

namespace Unleasharp.DB.MySQL;

/// <summary>
/// Represents a connector for managing connections to a MySQL database.
/// </summary>
/// <remarks>This class provides functionality to establish, manage, and terminate connections to a MySQL
/// database. It extends the base functionality provided by <see cref="Unleasharp.DB.Base.Connector{TConnector,
/// TConnectionStringBuilder}"/>. Use this class to interact with a MySQL database by providing a connection string or a
/// pre-configured <see cref="MySqlConnectionStringBuilder"/>.</remarks>
public class Connector : Unleasharp.DB.Base.Connector<Connector, MySqlConnection, MySqlConnectionStringBuilder> {
    #region Default constructors
    /// <inheritdoc />
    public Connector(MySqlConnectionStringBuilder stringBuilder) : base(stringBuilder) { }
    /// <inheritdoc />
    public Connector(string connectionString)                    : base(connectionString) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Connector"/> class using the specified MySQL connection.
    /// </summary>
    /// <param name="connection">The <see cref="MySqlConnection"/> instance to be used by the connector. Cannot be <see langword="null"/>.</param>
    public Connector(MySqlConnection connection) {
        this.Connection = connection;
    }
    #endregion
}
