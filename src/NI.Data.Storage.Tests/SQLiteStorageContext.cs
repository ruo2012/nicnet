﻿#region License
/*
 * Open NIC.NET library (http://nicnet.googlecode.com/)
 * Copyright 2013 Vitalii Fedorchenko
 * Copyright 2014 NewtonIdeas
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using System.Data;
using NI.Data;
using NI.Data.Storage.Model;
using NI.Data.Storage;

using NI.Data.SQLite;
using System.Data.SQLite;

namespace NI.Data.Storage.Tests {

	public class SQLiteStorageContext {

		DbDalc InternalDalc;
		public IDbConnection Connection;
		string dbFileName;
		public DataRowDalcMapper StorageDbMgr;
		public IObjectContainerStorage ObjectContainerStorage;
		public IDataSchemaStorage DataSchemaStorage;
		public IDalc StorageDalc;

		public SQLiteStorageContext(Func<DataRowDalcMapper, IObjectContainerStorage, IDataSchemaStorage> getSchemaStorage) {
			dbFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".db" );
			var connStr = String.Format("Data Source={0};FailIfMissing=false;Pooling=False;", dbFileName);
			var sqliteDalcFactory = new SQLiteDalcFactory();
			Connection = sqliteDalcFactory.CreateConnection();
			Connection.ConnectionString = connStr;
			InternalDalc = new DbDalc(sqliteDalcFactory, Connection, new [] {
				new DbDalcView("objects_view", @"
					SELECT @SqlFields
					FROM objects
					@Joins
					@SqlWhere[where {0}]
					@SqlOrderBy[order by {0}]
				") {
					FieldMapping = new Dictionary<string,string>() {
						{"id", "objects.id"},
						{"compact_class_id", "objects.compact_class_id"}
					}
				},

				new DbDalcView("object_relations_view", @"
					SELECT @SqlFields
					FROM object_relations r
					LEFT JOIN objects subj ON (subj.id=subject_id)
					LEFT JOIN objects obj ON (obj.id=object_id)
					@SqlWhere[where {0}]
					@SqlOrderBy[order by {0}]					
				", 
				"subject_id,predicate_class_compact_id,object_id,subj.compact_class_id as subject_compact_class_id,obj.compact_class_id as object_compact_class_id", 
				"count(r.id)")
			});
			var dbEventsBroker = new DataEventBroker(InternalDalc);
			var sqlTraceLogger = new NI.Data.Triggers.SqlCommandTraceTrigger(dbEventsBroker);

			InitDbSchema();

			StorageDbMgr = new DataRowDalcMapper(InternalDalc, new StorageDataSetPrv(CreateStorageSchemaDS()).GetDataSet);

			var objStorage = new ObjectContainerSqlDalcStorage(StorageDbMgr, InternalDalc, () => { return DataSchemaStorage.GetSchema(); } );
			objStorage.DeriveTypeMapping = new Dictionary<string,string>() {
				{"getDateYear", "CAST(strftime('%Y', {0}) as integer)"}
			};
			DataSchemaStorage = getSchemaStorage(StorageDbMgr, objStorage);

			objStorage.ObjectViewName = "objects_view";
			objStorage.ObjectRelationViewName = "object_relations_view";
			ObjectContainerStorage = objStorage;

			StorageDalc = new StorageDalc(InternalDalc, ObjectContainerStorage, DataSchemaStorage.GetSchema );
		}


		public void Destroy() {
			((SQLiteConnection)Connection).Dispose();
			SQLiteConnection.ClearAllPools();
			GC.Collect();
			if (dbFileName != null && File.Exists(dbFileName))
				File.Delete(dbFileName);

		}

		public void CreateTestDataSchema() {
			StorageDbMgr.Insert( "metadata_classes", new Dictionary<string,object>() {
				{"id", "companies"},
				{"name", "Company"},
				{"predicate", false},
				{"compact_id", 1},
				{"object_location", "ObjectTable"}
			});
			StorageDbMgr.Insert("metadata_classes", new Dictionary<string, object>() {
				{"id", "contacts"},
				{"name", "Contact"},
				{"predicate", false},
				{"compact_id", 2},
				{"object_location", "ObjectTable"}
			});

			StorageDbMgr.Insert("metadata_classes", new Dictionary<string, object>() {
				{"id", "employee"},
				{"name", "Employee"},
				{"predicate", true},
				{"compact_id", 3},
				{"object_location", "ObjectTable"}
			});

			StorageDbMgr.Insert("metadata_classes", new Dictionary<string, object>() {
				{"id", "countries"},
				{"name", "Country Lookup"},
				{"predicate", false},
				{"compact_id", 4},
				{"object_location", "ObjectTable"}
			});

			StorageDbMgr.Insert("metadata_classes", new Dictionary<string, object>() {
				{"id", "country"},
				{"name", "Country"},
				{"predicate", true},
				{"compact_id", 5},
				{"object_location", "ObjectTable"}
			});


			StorageDbMgr.Insert("metadata_class_relationships", new Dictionary<string, object>() {
				{"subject_class_id", "contacts"},
				{"predicate_class_id", "employee"},
				{"object_class_id", "companies"},
				{"subject_multiplicity", true},
				{"object_multiplicity", false}
			});

			StorageDbMgr.Insert("metadata_class_relationships", new Dictionary<string, object>() {
				{"subject_class_id", "companies"},
				{"predicate_class_id", "country"},
				{"object_class_id", "countries"},
				{"subject_multiplicity", true},
				{"object_multiplicity", false}
			});


			StorageDbMgr.Insert("metadata_properties", new Dictionary<string, object>() {
				{"id", "name"},
				{"name", "Name"},
				{"datatype", "string"},
				{"compact_id", 1}
			});

			StorageDbMgr.Insert("metadata_properties", new Dictionary<string, object>() {
				{"id", "birthday"},
				{"name", "Birthday"},
				{"datatype", "date"},
				{"compact_id", 2}
			});
			StorageDbMgr.Insert("metadata_properties", new Dictionary<string, object>() {
				{"id", "is_primary"},
				{"name", "Is Primary?"},
				{"datatype", "boolean"},
				{"compact_id", 3}
			});
			StorageDbMgr.Insert("metadata_properties", new Dictionary<string, object>() {
				{"id", "id"},
				{"name", "ID"},
				{"datatype", "integer"},
				{"compact_id", 4},
				{"primary_key", true}
			});

			StorageDbMgr.Insert("metadata_properties", new Dictionary<string, object>() {
				{"id", "created"},
				{"name", "Created"},
				{"datatype", "datetime"},
				{"compact_id", 5}
			});
			StorageDbMgr.Insert("metadata_properties", new Dictionary<string, object>() {
				{"id", "created_year"},
				{"name", "Created (Year)"},
				{"datatype", "integer"},
				{"compact_id", 6}
			});
			StorageDbMgr.Insert("metadata_properties", new Dictionary<string, object>() {
				{"id", "id_derived"},
				{"name", "IDx2"},
				{"datatype", "integer"},
				{"compact_id", 7}
			});

			StorageDbMgr.Insert("metadata_property_to_class", new Dictionary<string, object>() {
				{"property_id", "name"},
				{"class_id", "companies"},
				{"value_location", "ValueTable"}
			} );
			StorageDbMgr.Insert("metadata_property_to_class", new Dictionary<string, object>() {
				{"property_id", "created"},
				{"class_id", "companies"},
				{"value_location", "ValueTable"} });

			StorageDbMgr.Insert("metadata_property_to_class", new Dictionary<string, object>() {
				{"property_id", "created_year"},
				{"class_id", "companies"},
				{"value_location", "Derived"},
				{"derive_type","getDateYear"},
				{"derived_from_property_id","created"}
			});
			StorageDbMgr.Insert("metadata_property_to_class", new Dictionary<string, object>() {
				{"property_id", "id_derived"},
				{"class_id", "companies"},
				{"value_location", "Derived"},
				{"derive_type","{0}*2"},
				{"derived_from_property_id","id"}
			});

			StorageDbMgr.Insert("metadata_property_to_class", new Dictionary<string, object>() {
				{"property_id", "is_primary"},
				{"class_id", "contacts"},
				{"value_location", "ValueTable"} });
			StorageDbMgr.Insert("metadata_property_to_class", new Dictionary<string, object>() {
				{"property_id", "name"},
				{"class_id", "contacts"},
				{"value_location", "ValueTable"} });
			StorageDbMgr.Insert("metadata_property_to_class", new Dictionary<string, object>() {
				{"property_id", "birthday"},
				{"class_id", "contacts"},
				{"value_location", "ValueTable"} });
			StorageDbMgr.Insert("metadata_property_to_class", new Dictionary<string, object>() {
				{"property_id", "name"},
				{"class_id", "countries"},
				{"value_location", "ValueTable"} });

			StorageDbMgr.Insert("metadata_property_to_class", new Dictionary<string, object>() {
				{"property_id", "id"},
				{"class_id", "countries"},
				{"value_location", "TableColumn"},
				{"column_name", "id"}  });
			StorageDbMgr.Insert("metadata_property_to_class", new Dictionary<string, object>() {
				{"property_id", "id"},
				{"class_id", "contacts"},
				{"value_location", "TableColumn"},
				{"column_name", "id"} });
			StorageDbMgr.Insert("metadata_property_to_class", new Dictionary<string, object>() {
				{"property_id", "id"},
				{"class_id", "companies"},
				{"value_location", "TableColumn"},
				{"column_name", "id"} });
		}

		void InitDbSchema() {

			InternalDalc.ExecuteNonQuery(@"
				CREATE TABLE [metadata_classes]  (
					[id] TEXT PRIMARY KEY,
					[name] TEXT,
					[predicate] INTEGER,
					[compact_id] INTEGER,
					[object_location] TEXT
				)");

			InternalDalc.ExecuteNonQuery(@"
				CREATE TABLE [metadata_properties]  (
					[id] TEXT PRIMARY KEY,
					[name] TEXT,
					[datatype] TEXT,
					[compact_id] INTEGER,
					[primary_key] INTEGER
				)");

			InternalDalc.ExecuteNonQuery(@"
				CREATE TABLE [metadata_class_relationships]  (
					[subject_class_id] TEXT,
					[predicate_class_id] TEXT,
					[object_class_id] TEXT,
					[subject_multiplicity] INTEGER,
					[object_multiplicity] INTEGER,
					PRIMARY KEY (subject_class_id,predicate_class_id,object_class_id)
				)");

			InternalDalc.ExecuteNonQuery(@"
				CREATE TABLE [metadata_property_to_class]  (
					[property_id] TEXT,
					[class_id] TEXT,
					[value_location] TEXT,
					[column_name] TEXT,
					[derive_type] TEXT,
					[derived_from_property_id] TEXT,
					PRIMARY KEY (property_id, class_id)
				)");

			
			InternalDalc.ExecuteNonQuery(@"
				CREATE TABLE [objects]  (
					[id] INTEGER PRIMARY KEY AUTOINCREMENT,
					[compact_class_id] INTEGER
				)");
			InternalDalc.ExecuteNonQuery(@"
				CREATE TABLE [objects_log]  (
					[id] INTEGER PRIMARY KEY AUTOINCREMENT,
					[compact_class_id] INTEGER,
					[object_id] INTEGER,
					[account_id] INTEGER,
					[timestamp] TEXT,
					[action] TEXT
				)");

			InternalDalc.ExecuteNonQuery(@"
				CREATE TABLE [object_relations]  (
					[subject_id] INTEGER,
					[predicate_class_compact_id] INTEGER,
					[object_id] INTEGER,
					PRIMARY KEY (subject_id,predicate_class_compact_id,object_id)
				)");
			InternalDalc.ExecuteNonQuery(@"
				CREATE TABLE [object_relations_log]  (
					[id] INTEGER PRIMARY KEY AUTOINCREMENT,
					[subject_id] INTEGER,
					[predicate_class_compact_id] INTEGER,
					[object_id] INTEGER,
					[account_id] INTEGER,
					[timestamp] TEXT,
					[deleted] INTEGER
				)");

			var valueTableCreateSqlTemplate = @"
				CREATE TABLE [{0}]  (
					[id] INTEGER PRIMARY KEY AUTOINCREMENT,
					[object_id] INTEGER,
					[property_compact_id] INTEGER,
					[value] {1}
				)
			";

			var valueLogTableCreateSqlTemplate = @"
				CREATE TABLE [{0}]  (
					[id] INTEGER PRIMARY KEY AUTOINCREMENT,
					[object_id] INTEGER,
					[property_compact_id] INTEGER,
					[value] {1},
					[account_id] INTEGER,
					[timestamp] TEXT,
					[deleted] INTEGER
				)
			";

			InternalDalc.ExecuteNonQuery(String.Format(valueTableCreateSqlTemplate, "object_datetime_values", "TEXT"));
			InternalDalc.ExecuteNonQuery(String.Format(valueTableCreateSqlTemplate, "object_decimal_values", "REAL"));
			InternalDalc.ExecuteNonQuery(String.Format(valueTableCreateSqlTemplate, "object_integer_values", "INTEGER"));
			InternalDalc.ExecuteNonQuery(String.Format(valueTableCreateSqlTemplate, "object_string_values", "TEXT"));

			InternalDalc.ExecuteNonQuery(String.Format(valueLogTableCreateSqlTemplate, "object_datetime_values_log", "TEXT"));
			InternalDalc.ExecuteNonQuery(String.Format(valueLogTableCreateSqlTemplate, "object_decimal_values_log", "REAL"));
			InternalDalc.ExecuteNonQuery(String.Format(valueLogTableCreateSqlTemplate, "object_integer_values_log", "INTEGER"));
			InternalDalc.ExecuteNonQuery(String.Format(valueLogTableCreateSqlTemplate, "object_string_values_log", "TEXT"));


			InternalDalc.ExecuteNonQuery(@"
				CREATE TABLE [users]  (
					[id] INTEGER PRIMARY KEY AUTOINCREMENT,
					[name] TEXT,
					[age] INTEGER,
					[group_id] INTEGER
				)");
			InternalDalc.ExecuteNonQuery(@"
				CREATE TABLE [user_groups]  (
					[id] INTEGER PRIMARY KEY AUTOINCREMENT,
					[caption] TEXT
				)");
		}

		public class StorageDataSetPrv : IDataSetFactory {
			DataSet ds;
			public StorageDataSetPrv(DataSet sampleDs) {
				ds = sampleDs;
			}
			public DataSet GetDataSet(string context) {
				var tblName = Convert.ToString(context);
				var newDs = new DataSet();
				if (ds.Tables.Contains(tblName)) {
					newDs.Tables.Add( ds.Tables[tblName].Clone() );
				}
				return newDs;
			}
		}

		protected DataSet CreateStorageSchemaDS() {
			var ds = new DataSet();

			ds.Tables.Add(DataSetStorageContext.CreateValueTable("object_datetime_values", typeof(DateTime)));
			ds.Tables.Add(DataSetStorageContext.CreateValueLogTable("object_datetime_values_log", typeof(DateTime)));
			
			ds.Tables.Add(DataSetStorageContext.CreateValueTable("object_decimal_values", typeof(decimal)));
			ds.Tables.Add(DataSetStorageContext.CreateValueLogTable("object_decimal_values_log", typeof(decimal)));

			ds.Tables.Add(DataSetStorageContext.CreateValueTable("object_integer_values", typeof(long)));
			ds.Tables.Add(DataSetStorageContext.CreateValueLogTable("object_integer_values_log", typeof(long)));

			ds.Tables.Add(DataSetStorageContext.CreateValueTable("object_string_values", typeof(string)));
			ds.Tables.Add(DataSetStorageContext.CreateValueLogTable("object_string_values_log", typeof(string)));

			ds.Tables.Add(DataSetStorageContext.CreateRelationsTable());
			ds.Tables.Add(DataSetStorageContext.CreateRelationsLogTable());

			ds.Tables.Add(DataSetStorageContext.CreateObjectTable());
			ds.Tables.Add(DataSetStorageContext.CreateObjectLogTable());

			ds.Tables.Add(DataSetStorageContext.CreateMetadataClassTable());
			ds.Tables.Add(DataSetStorageContext.CreateMetadataPropertyTable());
			ds.Tables.Add(DataSetStorageContext.CreateMetadataPropertyToClassTable());

			ds.Tables.Add(DataSetStorageContext.CreateMetadataRelationshipTable());
			return ds;
		}


	}
}
