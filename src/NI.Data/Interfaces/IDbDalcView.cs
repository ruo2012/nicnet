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
using System.Collections;

namespace NI.Data
{
	/// <summary>
	/// Represents DALC data view methods and properties
	/// </summary>
	public interface IDbDalcView
	{
	
		/// <summary>
		/// Determines whether this dataview matches given table
		/// </summary>
		bool IsMatchTable(QTable table);
		
		/// <summary>
		/// Compose dataview SQL select text by specified query
		/// </summary>
		/// <param name="q">query to this dataview</param>
		/// <param name="sqlBuilder">SQL builder</param>
		/// <returns>dataview SQL select text</returns>
		string ComposeSelect(Query q, IDbSqlBuilder sqlBuilder);
		
	}
}
