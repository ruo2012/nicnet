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

using NI.Data.Storage.Model;

namespace NI.Data.Storage {
	
	public interface IObjectPersister {
		//TODO: load by query?..
		IEnumerable<ObjectContainer> Load(params long[] ids);
		IEnumerable<ObjectContainer> Load(Property[] props = null, params long[] ids);

		void Insert(ObjectContainer obj);
		void Delete(ObjectContainer obj);
		void Update(ObjectContainer obj);

		void AddRelations(params ObjectRelation[] relations);
		void RemoveRelations(params ObjectRelation[] relations);
		
		IEnumerable<ObjectRelation> LoadRelations(ObjectContainer obj, Class[] predicates = null);
		IEnumerable<ObjectRelation> LoadRelations(ObjectContainer[] obj, Class[] predicates = null);

	}
}
