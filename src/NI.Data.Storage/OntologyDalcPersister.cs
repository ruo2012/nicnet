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
using System.Data;
using System.Threading.Tasks;

using NI.Data;
using NI.Data.Storage.Model;

namespace NI.Data.Storage {
    
	public class OntologyDalcPersister {

		protected DataRowDalcMapper DbManager { get; set; }

		protected ObjectDalcMapper<Class> ClassPersister { get; set; }
		protected ObjectDalcMapper<Property> PropertyPersister { get; set; }
		protected ObjectDalcMapper<RelationshipData> RelationshipPersister { get; set; }
		protected ObjectDalcMapper<PropertyToClass> PropertyToClassPersister { get; set; }

		public string ClassSourceName { get; set; }
		public IDictionary<string, string> ClassFieldMapping { get; private set; }

		public string PropertySourceName { get; set; }
		public IDictionary<string, string> PropertyFieldMapping { get; private set; }

		public string RelationshipSourceName { get; set; }
		public IDictionary<string, string> RelationshipFieldMapping { get; private set; }

		public string PropertyToClassSourceName { get; set; }
		public IDictionary<string, string> PropertyToClassFieldMapping { get; private set; }

		public OntologyDalcPersister(DataRowDalcMapper dbMgr) {
			DbManager = dbMgr;

			ClassSourceName = "ontology_classes";
			ClassFieldMapping = new Dictionary<string, string>() {
				{"id", "ID"},
				{"name", "Name"},
				{"hidden", "Hidden"},
				{"indexable", "Indexable"},
				{"predefined", "Predefined"},
				{"predicate", "IsPredicate"},
				{"compact_id", "CompactID"}
			};
			ClassPersister = new ObjectDalcMapper<Class>(DbManager, ClassSourceName, ClassFieldMapping);

			PropertySourceName = "ontology_properties";
			PropertyFieldMapping = new Dictionary<string, string>() {
				{"id", "ID"},
				{"name", "Name"},
				{"predefined", "Predefined"},
				{"hidden", "Hidden"},
				{"indexable", "Indexable"},
				{"multivalue", "Multivalue"},
				{"compact_id", "CompactID"}
			};
			PropertyPersister = new ObjectDalcMapper<Property>(DbManager, PropertySourceName,
				new OntologyPropertyMapper(PropertyFieldMapping) );

			RelationshipSourceName = "ontology_class_relationships";
			RelationshipFieldMapping = new Dictionary<string, string>() {
				{"subject_class_id", "SubjectClassID"},
				{"predicate_class_id", "PredicateClassID"},
				{"object_class_id", "ObjectClassID"},
				{"subject_multiplicity", "SubjectMultiplicity"},
				{"object_multiplicity", "ObjectMultiplicity"}
			};
			RelationshipPersister = new ObjectDalcMapper<RelationshipData>(DbManager, RelationshipSourceName, RelationshipFieldMapping);

			PropertyToClassSourceName = "ontology_property_to_class";
			PropertyToClassFieldMapping = new Dictionary<string, string>() {
				{"class_id", "ClassID"},
				{"property_id", "PropertyID"}
			};
			PropertyToClassPersister = new ObjectDalcMapper<PropertyToClass>(DbManager, PropertyToClassSourceName, PropertyToClassFieldMapping);
		}

		public Ontology GetOntology() {
			var classes = ClassPersister.LoadAll(new Query(ClassSourceName) );
			var props = PropertyPersister.LoadAll(new Query(PropertySourceName) );

			var relData = RelationshipPersister.LoadAll(new Query(RelationshipSourceName));
			var propToClass = PropertyToClassPersister.LoadAll(new Query(PropertyToClassSourceName));
			
			var ontology = new Ontology(classes, props);

			foreach (var p2c in propToClass) {
				var c = ontology.FindClassByID(p2c.ClassID);
				var p = ontology.FindPropertyByID(p2c.PropertyID);
				if (c != null && p != null)
					ontology.AddClassProperty(c, p);
			}

			foreach (var r in relData) {
				var subjClass = ontology.FindClassByID(r.SubjectClassID);
				var objClass = ontology.FindClassByID(r.ObjectClassID);
				var predClass = ontology.FindClassByID(r.PredicateClassID);
				if (subjClass != null && objClass != null && predClass != null) {
					ontology.AddRelationship(new Relationship() {
						Subject = subjClass,
						Object = objClass,
						Predicate = predClass,
						Multiplicity = r.ObjectMultiplicity,
						Reversed = false
					});
					ontology.AddRelationship(new Relationship() {
						Subject = objClass,
						Object = subjClass,
						Predicate = predClass,
						Multiplicity = r.SubjectMultiplicity,
						Reversed = true
					});
				}
			}

			return ontology;
		}

		protected class PropertyToClass {
			public string ClassID { get; set; }
			public string PropertyID { get; set; }
		}

		protected class RelationshipData {
			public string ID { get; set; }
			public string SubjectClassID { get; set; }
			public string PredicateClassID { get; set; }
			public string ObjectClassID { get; set; }
			public bool SubjectMultiplicity { get; set; }
			public bool ObjectMultiplicity { get; set; }
		}

		protected class OntologyPropertyMapper : PropertyDataRowMapper {
			public OntologyPropertyMapper(IDictionary<string,string> fieldToProperty) : base(fieldToProperty) {
			}
			public override void MapTo(DataRow r, object o) {
				base.MapTo(r, o);
				if (r.Table.Columns.Contains("datatype") && !r.IsNull("datatype") ) {
					var dt = PropertyDataType.FindByID( Convert.ToString(r["datatype"]) );
					if (o is Property)
						((Property)o).DataType = dt;
				}
			}
		}

	}

}
