﻿using System;
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
public class Connector : Unleasharp.DB.Base.Connector<Connector, MySqlConnectionStringBuilder> {
    /// <summary>
    /// Gets the current MySQL database connection.
    /// </summary>
    public MySqlConnection Connection { get; private set; }

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

    #region Connection management
    /// <inheritdoc />
    protected override bool _Connected() {
        switch (this.Connection.State) {
            // If any of this cases, the connection is open
            case System.Data.ConnectionState.Open:
            case System.Data.ConnectionState.Fetching:
            case System.Data.ConnectionState.Executing:
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    protected override bool _Connect(bool force = false) {
        if (this.Connection == null) {
            this.Connection = new MySqlConnection(this.StringBuilder.ConnectionString);
        }

        if (
            !this._Connected()     // If not connected, it should be obvious to create the connection
            ||                     //
            (                      //
                force              // Reaching this statement means the connection is open but we are forcing the connection to be closed first
                &&                 //
                this._Disconnect() // Appending the disconnect disables the need to actively check again if connection is open to be closed
            ) 
        ) { 
            this.Connection.Open();

            this.ConnectionTimestamp = DateTime.UtcNow;
        }

        return this._Connected();
    }

    /// <inheritdoc />
    protected override bool _Disconnect() {
        if (this.Connection != null) {
            this.Connection.Close();
        }

        return this._Connected();
    }
    #endregion
}
