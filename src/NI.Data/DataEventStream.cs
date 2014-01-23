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

namespace NI.Data
{
	/// <summary>
	/// Generic implementation of data event stream.
	/// </summary>
	public class DataEventStream : IEventStream
	{

		/// <summary>
		/// Occurs for every data event
		/// </summary>
		public event EventHandler<EventArgs> DataEvent;

		public DataEventStream() {
		}


		public void Push(object sender, object eventData) {
			
		}

	}
}
