#region License
/*
 * Open NIC.NET library (http://nicnet.googlecode.com/)
 * Copyright 2004-2012 NewtonIdeas
 * Copyright 2008-2013 Vitalii Fedorchenko (changes and v.2)
 * Distributed under the LGPL licence
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion

using System;
using System.Data;
using System.Data.Common;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;


namespace NI.Data {

	/// <summary>
	/// Data Access Layer Component based on ADO.NET database provider
	/// </summary>
	/// <sort>1</sort>
	/// <assemblyLink>https://code.google.com/p/nicnet/source/browse/src/NI.Data?name=nicnet2</assemblyLink>
	public class DbDalc : ISqlDalc {
		
		/// <summary>
		/// Get or set database commands generator
		/// </summary>
		public IDbCommandGenerator CommandGenerator { get; set; }
		
		/// <summary>
		/// Get or set adapter wrapper factory component
		/// </summary>
		public IDbProviderFactory DbFactory { get; set; }
		
		/// <summary>
		/// Get or set database connection
		/// </summary>
		public virtual IDbConnection Connection { get; set; }

		/// <summary>
		/// Occurs when Dalc executes DB command, but before a command is executed against the data source.
		/// </summary>
		public event EventHandler<DbCommandExecutingEventArgs> DbCommandExecuting;

		/// <summary>
		/// Occurs when Dalc executes DB command, but after a command is executed against the data source.
		/// </summary>
		public event EventHandler<DbCommandExecutedEventArgs> DbCommandExecuted;

		/// <summary>
		/// Occurs during Update before a command is executed against the data source.
		/// </summary>
		public event EventHandler<RowUpdatingEventArgs> RowUpdating;

		/// <summary>
		/// Occurs during Update after a command is executed against the data source.
		/// </summary>
		public event EventHandler<RowUpdatedEventArgs> RowUpdated;


		/// <summary>
		/// Initializes a new instance of the DbDalc with specified factory and connection.
		/// </summary>
		public DbDalc(IDbProviderFactory factory, IDbConnection connection) {
			DbFactory = factory;
			Connection = connection;
			CommandGenerator = new DbCommandGenerator(factory);
		}

		/// <summary>
		/// Initializes a new instance of the DbDalc for specified factory and connection string.
		/// </summary>
		public DbDalc(IDbProviderFactory factory, string connectionStr) {
			DbFactory = factory;
			Connection = factory.CreateConnection();
			Connection.ConnectionString = connectionStr;
			CommandGenerator = new DbCommandGenerator(factory);			
		}

		/// <summary>
		/// Initializes a new instance of the DbDalc with specified DALC factory, DB connection and command generator
		/// </summary>
		public DbDalc(IDbProviderFactory factory, IDbConnection connection, IDbCommandGenerator cmdGenerator) {
			DbFactory = factory;
			Connection = connection;
			CommandGenerator = cmdGenerator;
		}

		/// <summary>
		/// Initializes a new instance of the DbDalc with specified DALC factory, DB connection and list of DALC data views
		/// </summary>
		public DbDalc(IDbProviderFactory factory, IDbConnection connection, IDbDalcView[] views) {
			DbFactory = factory;
			Connection = connection;
			CommandGenerator = new DbCommandGenerator(factory, views);
		}

		/// <see cref="NI.Data.IDalc.Load(NI.Data.Query,System.Data.DataSet)"/>
		public virtual DataTable Load(Query query, DataSet ds) {
			using (var selectCmd = CommandGenerator.ComposeSelect(query)) {
				QTable source = query.Table;

				selectCmd.Connection = Connection;

				OnCommandExecuting(source.Name, StatementType.Select, selectCmd);

				var adapter = DbFactory.CreateDataAdapter(OnRowUpdating, OnRowUpdated);
				try {
					adapter.SelectCommand = selectCmd;
					if (adapter is DbDataAdapter) {
						((DbDataAdapter)adapter).Fill(ds, query.StartRecord, query.RecordCount, source.Name);
					} else {
						adapter.Fill(ds);
					}
				} finally {
					// some implementations are sensitive to explicit dispose
					if (adapter is IDisposable)
						((IDisposable)adapter).Dispose();
				}

				OnCommandExecuted(source.Name, StatementType.Select, selectCmd);
				return ds.Tables[source.Name];
			}
			
		}

		/// <see cref="NI.Data.IDalc.Delete(NI.Data.Query)"/>
		public virtual int Delete(Query query) {
			using (var deleteCmd = CommandGenerator.ComposeDelete(query)) {
				return ExecuteInternal(deleteCmd, query.Table.Name, StatementType.Delete);
			}
		}

		/// <see cref="NI.Data.IDalc.Update(System.Data.DataTable)"/>
		public virtual void Update(DataTable t) {
			var tableName = t.TableName;

			IDbDataAdapter adapter = DbFactory.CreateDataAdapter(OnRowUpdating, OnRowUpdated);
			CommandGenerator.ComposeAdapterUpdateCommands(adapter, t);
			
			adapter.InsertCommand.Connection = Connection;
			adapter.UpdateCommand.Connection = Connection;
			adapter.DeleteCommand.Connection = Connection;

			try {
				if (adapter is DbDataAdapter)
					((DbDataAdapter)adapter).Update(t.DataSet, tableName);
				else
					adapter.Update(t.DataSet);
			} finally {
				DisposeAdapter(adapter);
			}
		}

		protected void DisposeAdapter(IDbDataAdapter adapter) {
			if (adapter.SelectCommand != null) {
				adapter.SelectCommand.Dispose();
				adapter.SelectCommand = null;
			}
			if (adapter.UpdateCommand != null) {
				adapter.UpdateCommand.Dispose();
				adapter.UpdateCommand = null;
			}
			if (adapter.DeleteCommand != null) {
				adapter.DeleteCommand.Dispose();
				adapter.DeleteCommand = null;
			}
			if (adapter.InsertCommand != null) {
				adapter.InsertCommand.Dispose();
				adapter.InsertCommand = null;
			}
			// some implementations are sensitive to explicit dispose
			if (adapter is IDisposable)
				((IDisposable)adapter).Dispose();
		}

		/// <see cref="NI.Data.IDalc.Update(NI.Data.Query,System.Collections.Generic.IDictionary<System.String,NI.Data.IQueryValue>)"/>
		public virtual int Update(Query query, IDictionary<string,IQueryValue> data) {
			using (var cmd = CommandGenerator.ComposeUpdate(query, data)) {
				cmd.Connection = Connection;
				return ExecuteInternal(cmd, query.Table.Name, StatementType.Update);
			}
		}

		/// <see cref="NI.Data.IDalc.Insert(System.String,System.Collections.Generic.IDictionary<System.String,NI.Data.IQueryValue>)"/>
		public virtual void Insert(string tableName, IDictionary<string,IQueryValue> data) {
			using (var cmd = CommandGenerator.ComposeInsert(tableName, data)) {
				cmd.Connection = Connection;
				ExecuteInternal(cmd, tableName, StatementType.Insert);
			}
		}

		/// <see cref="NI.Data.ISqlDalc.ExecuteNonQuery(System.String)"/>
		public virtual int ExecuteNonQuery(string sqlText) {
			using (var cmd = DbFactory.CreateCommand()) {
				cmd.Connection = Connection;
				cmd.CommandText = sqlText;
				return ExecuteInternal(cmd, null, StatementType.Update);
			}
		}

        protected virtual void ExecuteReaderInternal(IDbCommand cmd, string tableName, Action<IDataReader> callback) {
			DataHelper.EnsureConnectionOpen(Connection, () => {
				OnCommandExecuting(tableName, StatementType.Select, cmd);
				using (var rdr = cmd.ExecuteReader()) {
					try {
						OnCommandExecuted(tableName, StatementType.Select, cmd);
						callback(rdr);
					} finally {
						if (!rdr.IsClosed)
							rdr.Close();
					}
				}
			});
        }

		/// <see cref="NI.Data.ISqlDalc.ExecuteReader(System.String,System.Action<System.Data.IDataReader>)"/>
		public virtual void ExecuteReader(string sqlText, Action<IDataReader> callback) {
			using (var cmd = DbFactory.CreateCommand()) {
				cmd.Connection = Connection;
				cmd.CommandText = sqlText;

				ExecuteReaderInternal(cmd, null, callback);
			}
		}

		/// <see cref="NI.Data.IDalc.ExecuteReader(System.String,System.Action<System.Data.IDataReader>)"/>
        public virtual void ExecuteReader(Query q, Action<IDataReader> callback) {
			using (var cmd = CommandGenerator.ComposeSelect(q)) {
				cmd.Connection = Connection;
				ExecuteReaderInternal(cmd, q.Table, callback);
			}
        }

		/// <see cref="NI.Data.ISqlDalc.Load(System.String,System.Data.DataSet)"/>
        public virtual void Load(string sqlText, DataSet ds) {
			using (var cmd = DbFactory.CreateCommand()) {
				cmd.Connection = Connection;
				cmd.CommandText = sqlText;

				OnCommandExecuting(null, StatementType.Select, cmd);

				var adapter = DbFactory.CreateDataAdapter(OnRowUpdating, OnRowUpdated);
				try {
					adapter.SelectCommand = cmd;
					adapter.Fill(ds);
				} finally {
					// some implementations are sensitive to explicit dispose
					if (adapter is IDisposable)
						((IDisposable)adapter).Dispose();
				}

				OnCommandExecuted(null, StatementType.Select, cmd);
			}
		}

#region Internal methods

		protected virtual void OnCommandExecuting(string tableName, StatementType type, IDbCommand cmd) {
			if (DbCommandExecuting != null)
				DbCommandExecuting(this, new DbCommandExecutingEventArgs(tableName, type, cmd));
		}
		
		protected virtual void OnCommandExecuted(string tableName, StatementType type, IDbCommand cmd) {
			if (DbCommandExecuted != null)
				DbCommandExecuted(this, new DbCommandExecutedEventArgs(tableName, type, cmd));
		}

		protected virtual void OnRowUpdating(object sender, RowUpdatingEventArgs e) {
			OnCommandExecuting(e.Row.Table.TableName, StatementType.Update, e.Command);
			if (RowUpdating != null)
				RowUpdating(this, e);
		}
		
		protected virtual void OnRowUpdated(object sender, RowUpdatedEventArgs e) {
			if (e.StatementType == StatementType.Insert) {
				// extract insert id
				object insertId = DbFactory.GetInsertId(Connection);
				
				if (insertId!=null && insertId!=DBNull.Value)
					foreach (DataColumn col in e.Row.Table.Columns)
						if (col.AutoIncrement) {
							bool readOnly = col.ReadOnly;
							try {
								col.ReadOnly = false;
								e.Row[col] = insertId;
							} finally {
								col.ReadOnly = readOnly;
							}
							break;
						}
			}

			if (RowUpdated != null)
				RowUpdated(this, e);

			OnCommandExecuted(e.Row.Table.TableName, StatementType.Update, e.Command);
		}


		/// <summary>
		/// Execute SQL command
		/// </summary>
		virtual protected int ExecuteInternal(IDbCommand cmd, string tableName, StatementType commandType) {
			cmd.Connection = Connection;
			
			//Trace.WriteLine( cmdWrapper.Command.CommandText, "SQL" );
			int res = 0;
			DataHelper.EnsureConnectionOpen(cmd.Connection, () => {
				OnCommandExecuting(tableName, commandType, cmd);
				res = cmd.ExecuteNonQuery();
				OnCommandExecuted(tableName, commandType, cmd);
			});
			
			return res;
		}		
		
#endregion				
		

	}
	
	
}
