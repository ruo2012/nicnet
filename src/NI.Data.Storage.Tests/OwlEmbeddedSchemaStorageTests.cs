﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Threading.Tasks;

using NUnit.Framework;
using NI.Data;

using NI.Data.Storage.Model;

namespace NI.Data.Storage.Tests {
	
	[TestFixture]
	public class OwlEmbeddedSchemaStorageTests {
		
		SQLiteStorageContext StorageContext;
		OwlEmbeddedSchemaStorage OwlSchemaStorage;

		[SetUp]
		public void SetUp() {
			StorageContext = new SQLiteStorageContext( (StorageDbMgr, ObjectContainerStorage) => {
				OwlSchemaStorage = new OwlEmbeddedSchemaStorage(ObjectContainerStorage);
				return OwlSchemaStorage;
			});

			Logger.SetInfo((t, msg) => {
				Console.WriteLine("[{0}] {1}", t, msg);
			});
		}

		[TearDown]
		public void CleanUp() {
			StorageContext.Destroy();
		}

		void addOwlSchema(IDictionary<string,long> dataTypeMap) {
			var owlClassInstances = new string[] {
				OwlSchemaStorage.OwlConfig.ObjectClassID,
				OwlSchemaStorage.OwlConfig.ObjectPropertyClassID,
				OwlSchemaStorage.OwlConfig.DatatypePropertyClassID,
				OwlSchemaStorage.OwlConfig.DatatypeClassID,
				OwlSchemaStorage.OwlConfig.DomainClassID,
				OwlSchemaStorage.OwlConfig.RangeClassID,
				OwlSchemaStorage.OwlConfig.LabelClassID,
				OwlSchemaStorage.OwlConfig.RdfTypeClassID,
				OwlSchemaStorage.OwlConfig.FunctionalPropertyClassID,
				OwlSchemaStorage.OwlConfig.InverseFunctionalPropertyClassID,
				OwlSchemaStorage.OwlConfig.PkPropertyID
			};
			var owlClassIdToCompactId = new Dictionary<string,long>();
			foreach (var owlClassInstanceId in owlClassInstances) {
				var objClassRow = StorageContext.StorageDbMgr.Insert("objects", new Dictionary<string,object>() {
					{"compact_class_id", OwlSchemaStorage.OwlConfig.SuperClassCompactID}
				});
				StorageContext.StorageDbMgr.Insert("object_string_values", new Dictionary<string,object>() {
					{"object_id", objClassRow["id"]},
					{"property_compact_id", OwlSchemaStorage.OwlConfig.SuperIdPropertyCompactID },
					{"value", owlClassInstanceId}
				});
				owlClassIdToCompactId[owlClassInstanceId] = Convert.ToInt64( objClassRow["id"] );
			}

			foreach (var dataType in PropertyDataType.KnownDataTypes) {
				var objClassRow = StorageContext.StorageDbMgr.Insert("objects", new Dictionary<string,object>() {
					{"compact_class_id", owlClassIdToCompactId[OwlSchemaStorage.OwlConfig.DatatypeClassID]}
				});
				StorageContext.StorageDbMgr.Insert("object_string_values", new Dictionary<string,object>() {
					{"object_id", objClassRow["id"]},
					{"property_compact_id", OwlSchemaStorage.OwlConfig.SuperIdPropertyCompactID },
					{"value", dataType.ID}
				});
				dataTypeMap[dataType.ID] = Convert.ToInt64(objClassRow["id"]);
			}

		}

		void addTestDataSchema() {
			OwlSchemaStorage.CreateClass("cities", "City");
			OwlSchemaStorage.CreateDatatypeProperty("title", "Title", PropertyDataType.String, true);
			OwlSchemaStorage.SetDatatypePropertyDomain("title", OwlSchemaStorage.GetSchema().FindClassByID("cities") );

			OwlSchemaStorage.CreateClass("persons", "Person");
			OwlSchemaStorage.CreateDatatypeProperty("name", "Name", PropertyDataType.String, true);
			OwlSchemaStorage.CreateDatatypeProperty("birthday", "Birthday", PropertyDataType.Date, true);
			OwlSchemaStorage.SetDatatypePropertyDomain("name", OwlSchemaStorage.GetSchema().FindClassByID("persons") );
			OwlSchemaStorage.SetDatatypePropertyDomain("birthday", OwlSchemaStorage.GetSchema().FindClassByID("persons") );
			
			OwlSchemaStorage.CreateObjectProperty("cityOf", "City", false, true);
			OwlSchemaStorage.SetObjectPropertyRange("cityOf", OwlSchemaStorage.GetSchema().FindClassByID("persons") );
			OwlSchemaStorage.SetObjectPropertyDomain("cityOf", OwlSchemaStorage.GetSchema().FindClassByID("cities") );
		}

		[Test]
		public void OwlClassesCheck() {
			var datatypeMap = new Dictionary<string,long>();
			addOwlSchema(datatypeMap);

			var schema = OwlSchemaStorage.GetSchema();
			Assert.AreEqual(12, schema.Classes.Count() );
		}

		[Test]
		public void DataOntologyCheck() {
			var datatypeMap = new Dictionary<string,long>();
			addOwlSchema(datatypeMap);
			var schema = OwlSchemaStorage.GetSchema();

			addTestDataSchema();
			OwlSchemaStorage.Refresh();

			schema = OwlSchemaStorage.GetSchema();
			Assert.NotNull( schema.FindClassByID("persons") );
			Assert.IsTrue( schema.FindClassByID("cityOf").IsPredicate );

			Assert.AreEqual("Person", schema.FindClassByID("persons").Name );
			Assert.AreEqual(3, schema.FindClassByID("persons").Properties.Count() );
			Assert.AreEqual(1, schema.FindClassByID("persons").Relationships.Count() );

			Assert.IsFalse( schema.FindPropertyByID("name").Multivalue );

			Assert.AreEqual(1, schema.FindClassByID("persons").Relationships.Count() );
			var personToCityRel = schema.FindClassByID("persons").FindRelationship(
					schema.FindClassByID("cityOf"), schema.FindClassByID("cities") );
			
			Assert.IsTrue( personToCityRel.Reversed);
			Assert.IsFalse( personToCityRel.Multiplicity);
			Assert.IsTrue( personToCityRel.ReversedRelationship.Multiplicity);
		}


	}
}
