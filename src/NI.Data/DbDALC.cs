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
	/// Database Data Access Layer Component
	/// </summary>
	public class DbDalc : ISqlDalc, IDisposable {
		Dictionary<string, IDbDataAdapter> updateAdapterCache = new Dictionary<string, IDbDataAdapter>();
		
		/// <summary>
		/// Get or set database commands generator
		/// </summary>
		public IDbCommandGenerator CommandGenerator { get; set; }
		
		/// <summary>
		/// Get or set adapter wrapper factory component
		/// </summary>
		public IDbDalcFactory DbFactory { get; set; }
		
		/// <summary>
		/// Get or set database connection
		/// </summary>
		public virtual IDbConnection Connection { get; set; }

		public IDbDalcEventsMediator DbDalcEventsMediator { get; set; }

		/// <summary>
		/// Initializes a new instance of the DbDalc for specified factory and connection.
		/// </summary>
		public DbDalc(IDbDalcFactory factory, IDbConnection connection) {
			DbFactory = factory;
			Connection = connection;
			CommandGenerator = new DbCommandGenerator(factory);
		}

		/// <summary>
		/// Initializes a new instance of the DbDalc for specified factory and connection.
		/// </summary>
		public DbDalc(IDbDalcFactory factory, string connectionStr) {
			DbFactory = factory;
			Connection = factory.CreateConnection();
			Connection.ConnectionString = connectionStr;
			CommandGenerator = new DbCommandGenerator(factory);			
		}

		public DbDalc(IDbDalcFactory factory, IDbConnection connection, IDbCommandGenerator cmdGenerator) {
			DbFactory = factory;
			Connection = connection;
			CommandGenerator = cmdGenerator;
		}

		
		/// <summary>
		/// Load data to dataset by query
		/// </summary>
		public virtual DataTable Load(Query query, DataSet ds) {
			using (var selectCmd = CommandGenerator.ComposeSelect(query)) {
				QSourceName source = query.SourceName;

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

		/// <summary>
		/// Delete data by query
		/// </summary>
		public virtual int Delete(Query query) {
			using (var deleteCmd = CommandGenerator.ComposeDelete(query)) {
				return ExecuteInternal(deleteCmd, query.SourceName, StatementType.Delete);
			}
		}


		
		/// <summary>
		/// Update one table in DataSet
		/// </summary>
		public virtual void Update(DataTable t) {
			var tableName = t.TableName;

			IDbDataAdapter adapter;
			if (!updateAdapterCache.ContainsKey(tableName)) {
				adapter = DbFactory.CreateDataAdapter(OnRowUpdating,OnRowUpdated);
				GenerateAdapterCommands(adapter, t);
				updateAdapterCache[tableName] = adapter;
			} else {
				adapter = updateAdapterCache[tableName];
			}
			
			adapter.InsertCommand.Connection = Connection;
			adapter.UpdateCommand.Connection = Connection;
			adapter.DeleteCommand.Connection = Connection;
			
			if (adapter is DbDataAdapter)
				((DbDataAdapter)adapter).Update(t.DataSet, tableName);
			else
				adapter.Update(t.DataSet);
		}
		
		/// <summary>
		/// Update data from dictionary container to datasource by query
		/// </summary>
		/// <param name="data">Container with record changes</param>
		/// <param name="query">query</param>
		public virtual int Update(Query query, IDictionary<string,IQueryValue> data) {
			using (var cmd = CommandGenerator.ComposeUpdate(data, query)) {
				cmd.Connection = Connection;
				return ExecuteInternal(cmd, query.SourceName, StatementType.Update);
			}
		}

		/// <summary>
		/// <see cref="IDalc.Insert"/>
		/// </summary>
		public virtual void Insert(string sourceName, IDictionary<string,IQueryValue> data) {
			using (var cmd = CommandGenerator.ComposeInsert(data, sourceName)) {
				cmd.Connection = Connection;
				ExecuteInternal(cmd, sourceName, StatementType.Insert);
			}
		}
		
		
		/// <summary>
		/// Execute SQL command
		/// </summary>
		/// <param name="sqlText">SQL command text to execute</param>
		/// <returns>number of rows affected</returns>
		public virtual int ExecuteNonQuery(string sqlText) {
			using (var cmd = DbFactory.CreateCommand()) {
				cmd.Connection = Connection;
				cmd.CommandText = sqlText;
				return ExecuteInternal(cmd, null, StatementType.Update);
			}
		}

        protected virtual void ExecuteReaderInternal(IDbCommand cmd, string sourceName, Action<IDataReader> callback) {
			DataHelper.EnsureConnectionOpen(Connection, () => {
				OnCommandExecuting(sourceName, StatementType.Select, cmd);
				using (var rdr = cmd.ExecuteReader()) {
					try {
						OnCommandExecuted(sourceName, StatementType.Select, cmd);
						callback(rdr);
					} finally {
						if (!rdr.IsClosed)
							rdr.Close();
					}
				}
			});
        }

        /// <summary>
        /// Load data into datareader by custom SQL
        /// </summary>
		public virtual void ExecuteReader(string sqlText, Action<IDataReader> callback) {
			using (var cmd = DbFactory.CreateCommand()) {
				cmd.Connection = Connection;
				cmd.CommandText = sqlText;

				ExecuteReaderInternal(cmd, null, callback);
			}
		}

        /// <summary>
        /// Load data into datareader by query
        /// </summary>
        public virtual void ExecuteReader(Query q, Action<IDataReader> callback) {
			using (var cmd = CommandGenerator.ComposeSelect(q)) {
				cmd.Connection = Connection;
				ExecuteReaderInternal(cmd, q.SourceName, callback);
			}
        }
		
        /// <summary>
        /// Load data into dataset by custom SQL
        /// </summary>
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

		protected virtual void OnCommandExecuting(string sourceName, StatementType type, IDbCommand cmd) {
			if (DbDalcEventsMediator!=null)
				DbDalcEventsMediator.OnCommandExecuting(this, new DbCommandEventArgs(sourceName, type, cmd) );
		}
		
		protected virtual void OnCommandExecuted(string sourceName, StatementType type, IDbCommand cmd) {
			if (DbDalcEventsMediator!=null)
				DbDalcEventsMediator.OnCommandExecuted(this, new DbCommandEventArgs(sourceName, type, cmd) );
		}

		/// <summary>
		/// This method should be called before row updating
		/// </summary>
		protected virtual void OnRowUpdating(object sender, RowUpdatingEventArgs e) {
			//Trace.WriteLine( e.Command.CommandText, "SQL" );
			OnCommandExecuting(e.Row.Table.TableName, StatementType.Update, e.Command);
			if (DbDalcEventsMediator!=null)
				DbDalcEventsMediator.OnRowUpdating(this, e);
		}
		
		/// <summary>
		/// This method should be called after row updated
		/// </summary>
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
			
			if (DbDalcEventsMediator!=null)
				DbDalcEventsMediator.OnRowUpdated(this, e);
			OnCommandExecuted(e.Row.Table.TableName, StatementType.Update, e.Command);
		}

		/// <summary>
		/// Automatically generates Insert/Update/Delete commands for Adapter
		/// </summary>
		protected virtual void GenerateAdapterCommands(IDbDataAdapter adapter, DataTable table) {
			// Init DataAdapter
			adapter.UpdateCommand = CommandGenerator.ComposeUpdate(table);
			adapter.InsertCommand = CommandGenerator.ComposeInsert(table);
			adapter.DeleteCommand = CommandGenerator.ComposeDelete(table);
		}

		/// <summary>
		/// Execute SQL command
		/// </summary>
		virtual protected int ExecuteInternal(IDbCommand cmd, string sourceName, StatementType commandType) {
			cmd.Connection = Connection;
			
			//Trace.WriteLine( cmdWrapper.Command.CommandText, "SQL" );
			int res = 0;
			DataHelper.EnsureConnectionOpen(cmd.Connection, () => {
				OnCommandExecuting(sourceName, commandType, cmd);
				res = cmd.ExecuteNonQuery();
				OnCommandExecuted(sourceName, commandType, cmd);
			});
			
			return res;
		}		
		
#endregion				
		
		private bool disposed = false;

		public void Dispose() {
			Dispose(true);
		}

		protected virtual void Dispose(bool disposing) {
			if (!this.disposed) {
				disposed = true;
				foreach (var adapter in updateAdapterCache.Values) {
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
				updateAdapterCache.Clear();
			}
		}
	}
	
	
}