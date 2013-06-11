#region License
/*
 * Open NIC.NET library (http://nicnet.googlecode.com/)
 * Copyright 2004-2012 NewtonIdeas
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
using System.Collections;
using System.Security.Principal;
using System.Text.RegularExpressions;

using NI.Data;

namespace NI.Data.Permissions
{
	/// <summary>
	/// DALC condition composer
	/// </summary>
	public class DalcConditionComposer : IDalcConditionComposer
	{
		IDalcConditionDescriptor[] _ConditionDescriptors;

		public IDalcConditionDescriptor[] ConditionDescriptors {
			get { return _ConditionDescriptors; }
			set { _ConditionDescriptors = value; }
		}

		
		public DalcConditionComposer() {
		}

		/// <summary>
		/// <see cref="IDalcConditionComposer.Compose"/>
		/// </summary>
		public QueryNode Compose(IPrincipal user, DalcOperation operation, string sourceName) {
			QueryGroupNode groupAnd = new QueryGroupNode(GroupType.And);
			for (int i=0; i<ConditionDescriptors.Length; i++)
				if (ConditionDescriptors[i].Operation==operation && 
					ConditionDescriptors[i].SourceName==sourceName) {
					QueryNode condition = ConditionDescriptors[i].ConditionProvider(user);
					if (condition!=null)
						groupAnd.Nodes.Add( condition );
				}
			
			if (groupAnd.Nodes.Count==0) return null;
			if (groupAnd.Nodes.Count==1) return groupAnd.Nodes[0];
			return groupAnd;
		}
		
	}
}