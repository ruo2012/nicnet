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

namespace NI.Data
{
	/// <summary>
	/// </summary>
	public class QueryFieldValueFormatter 
	{
		char[] specialChars = new char[] {'*', '(', ')'};
		string _FormatString;
		
		public string FormatString {
			get { return _FormatString; }
			set { _FormatString = value; }
		}
	
		public QueryFieldValueFormatter()
		{
		}

		public string Format(QField fieldValue) {
			for (int i=0; i<specialChars.Length; i++)
				if (fieldValue.Name.IndexOf(specialChars[i])>=0) return fieldValue.Name;
			return String.Format(FormatString, fieldValue.Name);
		}

	}
}