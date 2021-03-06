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
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace NI.Vfs
{
	/// <summary>
	/// In-memory filesystem provider implementation.
	/// </summary>
	public class MemoryFileSystem : IFileSystem
	{
		protected IDictionary<string,MemoryFile> MemoryFilesMap;
		
		public IEnumerable<MemoryFile> MemoryFiles {
			get { return MemoryFilesMap.Values; }
		}
	
		public MemoryFileSystem()
		{
			MemoryFilesMap = new Dictionary<string,MemoryFile>();
		}
		
	
		public IFileObject Root {
			get { return ResolveFile(""); }
		}
		
		public IFileObject ResolveFile(string name) {
			if (name.Length>0) {
				if (name.Contains("..")) {
					var fakeRootPath = "f:\\";
					var fakeAbsolutePath = Path.GetFullPath( Path.Combine(fakeRootPath, name) );
					name = fakeAbsolutePath.Substring(Path.GetPathRoot(fakeAbsolutePath).Length);
				}

				// use only one symbol as directory separator
				name = name.Replace( Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar );
				// normalize file name
				name = Path.Combine( Path.GetDirectoryName(name), Path.GetFileName(name) );
			}
			
			if (!MemoryFilesMap.ContainsKey(name)) {
				var newFile = new MemoryFile( name, name.Length>0 ? FileType.Imaginary : FileType.Folder, this);
			}
			return MemoryFilesMap[name];
		}

		internal void AddFile(MemoryFile f) {
			MemoryFilesMap[f.Name] = f;
		}
		
		public void Clear() {
			MemoryFilesMap.Clear();
		}

		
		
		
	}	

}
