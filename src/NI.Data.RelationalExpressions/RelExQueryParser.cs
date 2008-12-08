#region License
/*
 * Open NIC.NET library (http://nicnet.googlecode.com/)
 * Copyright 2004-2008 NewtonIdeas
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
using System.Text.RegularExpressions;
using System.Text;

using NI.Data.Dalc;
using NI.Common;

namespace NI.Data.RelationalExpressions
{
	/// <summary>
	/// </summary>
	public class RelExQueryParser : IRelExQueryParser
	{
		static readonly string[] nameGroups = new string[] { "and", "or"};
		static readonly string[] delimiterGroups = new string[] { "&&", "||"};
		static readonly GroupType[] enumGroups = new GroupType[] { GroupType.And, GroupType.Or };
		
		static readonly string[] delimiterConds = new string[] {
			"==", "=",
			"<>", "!=",
			">", ">=",
			"<", "<="};
		static readonly string[] nameConds = new string[] {
			"in", "like" };
			
		static readonly string nullField = "null";
		
		static readonly Conditions[] enumDelimConds = new Conditions[] {
			Conditions.Equal, Conditions.Equal,
			Conditions.Not|Conditions.Equal, Conditions.Not|Conditions.Equal,
			Conditions.GreaterThan, Conditions.GreaterThan|Conditions.Equal,
			Conditions.LessThan, Conditions.LessThan|Conditions.Equal
		};

		static readonly Conditions[] enumNameConds = new Conditions[] {
			Conditions.In, Conditions.Like
		};

		
		static readonly char[] delimiters = new char[] {
			'(', ')', '[', ']', ':', ',', '=', '<', '>', '!', '&', '|', '*', '{', '}'};
		static readonly char charQuote = '"';
		static readonly char[] specialNameChars = new char[] {
			'.', '-', '_' };
		static readonly char[] arrayValuesSeparators = new char[] {
			'\0', ';', ',', '\t' }; // order is important!
		
		static readonly string[] typeNames;
		static readonly string[] arrayTypeNames;

		
		public enum LexemType {
			Unknown,
			Name,
			Delimiter,
			QuotedConstant,
			Constant,
			Stop
		}
		
		bool _AllowDumpConstants = true;
		IQueryModifier _QueryModifier = null;
		bool _AllowLazyConstType = false;
		
		public bool AllowLazyConstType {
			get { return _AllowLazyConstType; }
			set { _AllowLazyConstType = value; }
		}
		
		/// <summary>
		/// Get or set flag that indicates whether 'dump' constants are allowed
		/// </summary>
		[Dependency(Required=false)]
		public bool AllowDumpConstants {
			get { return _AllowDumpConstants; }
			set { _AllowDumpConstants = value; }
		}

		[Dependency(Required=false)]
		public IQueryModifier QueryModifier {
			get { return _QueryModifier; }
			set { _QueryModifier = value; }
		}
		
		static RelExQueryParser() {
			typeNames = Enum.GetNames(typeof(TypeCode));
			arrayTypeNames = new string[typeNames.Length+1];
			for (int i=0; i<typeNames.Length; i++) {
				typeNames[i] = typeNames[i].ToLower();
				arrayTypeNames[i] = typeNames[i] + "[]";
			}
			arrayTypeNames[arrayTypeNames.Length-1] = "sql";

		}
		
		public RelExQueryParser() {
		}

		public RelExQueryParser(bool allowDumpConsts) {
			AllowDumpConstants = allowDumpConsts;
		}
		
		protected LexemType GetLexemType(string s, int startIdx, out int endIdx) {
			LexemType lexemType = LexemType.Unknown;
			endIdx = startIdx;
			while (endIdx<s.Length) {
				if (Array.IndexOf(delimiters,s[endIdx])>=0) {
					if (lexemType==LexemType.Unknown) {
						endIdx++;
						return LexemType.Delimiter;
					}
					if (lexemType!=LexemType.QuotedConstant)
						return lexemType;
				} else if (Char.IsSeparator(s[endIdx])) {
					if (lexemType!=LexemType.QuotedConstant && lexemType!=LexemType.Unknown)
						return lexemType; // done
				} else if (Char.IsLetter(s[endIdx])) {
					if (lexemType==LexemType.Unknown)
						lexemType=LexemType.Name;
				} else if (Char.IsDigit(s[endIdx])) {
					if (lexemType==LexemType.Unknown)
						lexemType=LexemType.Constant;
				} else if (Array.IndexOf(specialNameChars,s[endIdx])>=0) {
					if (lexemType==LexemType.Unknown)
						lexemType=LexemType.Constant;
					if (lexemType!=LexemType.Name && lexemType!=LexemType.Constant && lexemType!=LexemType.QuotedConstant)
						throw new Exception(
							String.Format("Invalid syntax (position: {0}, expression: {1}", startIdx, s ) );
				} else if (s[endIdx]==charQuote) {
					if (lexemType==LexemType.Unknown)
						lexemType = LexemType.QuotedConstant;
					else {
						if (lexemType==LexemType.QuotedConstant) {
							// check for "" combination
							
							
							if ( ( (endIdx+1)<s.Length && s[endIdx+1]!=charQuote) ) {
								endIdx++;
								return lexemType;
							} else
								if ((endIdx+1)<s.Length) endIdx++; // skip next quote
								
						}
					}
				} else if (Char.IsControl(s[endIdx]) && lexemType!=LexemType.Unknown && lexemType!=LexemType.QuotedConstant)
					return lexemType;
				
				// goto next char
				endIdx++;
			}
			
			if (lexemType==LexemType.Unknown) return LexemType.Stop;
			if (lexemType==LexemType.Constant)
				throw new Exception(
					String.Format("Unterminated constant (position: {0}, expression: {1}", startIdx, s ) );
			return lexemType;
		}

		protected string GetLexem(string s, int startIdx, int endIdx, LexemType lexemType) {
			string lexem = GetLexem(s, startIdx, endIdx);
			if (lexemType==null || lexemType!=LexemType.QuotedConstant)
				return lexem;
			// remove first and last chars
			string constant = lexem.Substring(1, lexem.Length-2); 
			// replace "" with "
			return constant.Replace( "\"\"", "\"" );
		}
		
		protected string GetLexem(string s, int startIdx, int endIdx) {
			return s.Substring(startIdx, endIdx-startIdx).Trim();
		}

		
		protected void GetAllDelimiters(string s, int startIdx, out int endIdx) {
			endIdx = startIdx;
			while (Array.IndexOf(delimiters, s[endIdx])>=0 && (endIdx+1)<s.Length )
				endIdx++;
		}
		
		protected bool GetGroupType(LexemType lexemType, string s, int startIdx, ref int endIdx, ref GroupType groupType) {
			string lexem = GetLexem(s, startIdx, endIdx).ToLower();
			if (lexemType==LexemType.Name) {
				int idx = Array.IndexOf(nameGroups, lexem);
				if (idx<0) return false;
				groupType = enumGroups[idx];
				return true;
			}
			if (lexemType==LexemType.Delimiter) {
				// read all available delimiters...
				GetAllDelimiters(s, endIdx, out endIdx);
				lexem = GetLexem(s, startIdx, endIdx);
				
				int idx = Array.IndexOf(delimiterGroups, lexem);
				if (idx<0) return false;
				groupType = enumGroups[idx];
				return true;
			}
			return false;
		}
		
		protected bool GetCondition(LexemType lexemType, string s, int startIdx, ref int endIdx, ref Conditions conditions) {
			string lexem = GetLexem(s, startIdx, endIdx).ToLower();
			if (lexemType==LexemType.Name) {
				int idx = Array.IndexOf(nameConds, lexem);
				if (idx>=0) {
					conditions = enumNameConds[idx];
					return true;
				}
			}
			
			if (lexemType==LexemType.Delimiter) {
				// read all available delimiters...
				GetAllDelimiters(s, endIdx, out endIdx);
				lexem = GetLexem(s, startIdx, endIdx);

				int idx = Array.IndexOf(delimiterConds, lexem);
				if (idx<0) {
					if (lexem=="!") {
						int newEndIdx;
						Conditions innerConditions = Conditions.Equal;
						
						LexemType newLexemType = GetLexemType(s, endIdx, out newEndIdx);
						if (GetCondition(newLexemType, s, endIdx, ref newEndIdx, ref innerConditions)) {
							endIdx = newEndIdx;
							conditions = innerConditions|Conditions.Not;
							return true;
						}
					}
					return false;
				}
				conditions = enumDelimConds[idx];
				return true;
			}
			return false;
		}
		
		
		public virtual IQuery Parse(string relEx) {
			int endIdx;
			IQueryValue qValue = ParseInternal(relEx, 0, out endIdx );
			if (!(qValue is IQuery)) 
				throw new Exception("Invalid expression: result is not a query");
			IQuery q = (IQuery)qValue;
			if (QueryModifier!=null)
				q = QueryModifier.Modify(q);
			return q;
		}
		
		protected virtual IQueryValue ParseTypedConstant(string typeCodeString, string constant) {
			typeCodeString = typeCodeString.ToLower();
			// sql type
			if (typeCodeString=="sql")
				return new QRawConst(constant);
			// simple type
			int typeNameIdx = Array.IndexOf(typeNames, typeCodeString);
			if (typeNameIdx>=0) {
				TypeCode typeCode = (TypeCode)Enum.Parse(typeof(TypeCode), typeCodeString, true);
				try {
					object typedConstant = Convert.ChangeType(constant, typeCode);
					return new QConst(typedConstant);
				} catch (Exception ex) {
					if (AllowLazyConstType)
						return new QConst(constant, typeCode);
					throw new InvalidCastException(
						 String.Format("Cannot parse typed constant \"{0}\":{1}",constant, typeCodeString),ex);
				}
			}
			// array
			typeNameIdx = Array.IndexOf(arrayTypeNames, typeCodeString);
			if (typeNameIdx>=0) {
				TypeCode typeCode = (TypeCode)Enum.Parse(typeof(TypeCode), typeNames[typeNameIdx], true);
				string[] arrayValues = SplitArrayValues(constant);
				object[] array = new object[arrayValues.Length];
				for (int i=0; i<array.Length; i++)
					array[i] = Convert.ChangeType(arrayValues[i], typeCode);
				return new QConst(array);
			}

			throw new InvalidCastException(
				String.Format("Cannot parse typed constant \"{0}\":{1}",
					constant, typeCodeString) );
		}
		
		protected string[] SplitArrayValues(string str) {
			for (int i=0; i<arrayValuesSeparators.Length; i++)
				if (str.IndexOf(arrayValuesSeparators[i])>=0)
					return str.Split(arrayValuesSeparators[i]);
			return str.Split( arrayValuesSeparators );
		}
		
		
		protected virtual IQueryValue ParseInternal(string input, int startIdx, out int endIdx) {
			LexemType lexemType = GetLexemType(input, startIdx, out endIdx);
			string lexem = GetLexem(input, startIdx, endIdx);
						
			if (lexemType==LexemType.Constant)
				return (QConst)lexem;
			
			if (lexemType==LexemType.QuotedConstant) {
				// remove first and last chars
				string constant = lexem.Substring(1, lexem.Length-2); 
				// replace "" with "
				constant = constant.Replace( "\"\"", "\"" );
				// typed?
				int newEndIdx;
				if ( GetLexemType(input, endIdx, out newEndIdx)==LexemType.Delimiter &&
					 GetLexem(input, endIdx, newEndIdx)==":" ) {
					int typeEndIdx;
					if (GetLexemType(input, newEndIdx, out typeEndIdx)==LexemType.Name) {
						string typeCodeString = GetLexem(input, newEndIdx, typeEndIdx);
						endIdx = typeEndIdx;
						// read [] at the end if specified
						if (GetLexemType(input, endIdx, out newEndIdx)==LexemType.Delimiter &&
							GetLexem(input, endIdx, newEndIdx)=="[")
							if (GetLexemType(input, newEndIdx, out typeEndIdx)==LexemType.Delimiter &&
								GetLexem(input, newEndIdx, typeEndIdx)=="]") {
								endIdx = typeEndIdx;
								typeCodeString += "[]";
							}
						
						return ParseTypedConstant(typeCodeString, constant);
					}
				}
				
				return (QConst)constant;
			}
			
			if (lexemType==LexemType.Name) {
				int nextEndIdx;
				
				// query
				string sourceName = lexem;
				IQueryNode rootCondition = null;
				string[] fields = null;
				
				LexemType nextLexemType = GetLexemType(input, endIdx, out nextEndIdx);
				string nextLexem = GetLexem(input, endIdx, nextEndIdx);
				if (nextLexemType==LexemType.Delimiter && nextLexem=="(") {
					// compose conditions
					rootCondition = ParseConditionGroup(input, nextEndIdx, out endIdx);
					// read ')'
					nextLexemType = GetLexemType(input, endIdx, out nextEndIdx);
					if (nextLexemType!=LexemType.Delimiter || GetLexem(input, endIdx,nextEndIdx)!=")")
						throw new Exception(
							String.Format("Invalid syntax (position: {0}, expression: {1})", endIdx, input ) );
					
					// read next lexem
					nextLexemType = GetLexemType(input, nextEndIdx, out endIdx);
					nextLexem = GetLexem(input, nextEndIdx, endIdx);
					nextEndIdx = endIdx;
				}
				
				if (nextLexemType==LexemType.Delimiter && nextLexem=="[") {
					nextLexemType = GetLexemType(input, nextEndIdx, out endIdx);
					nextLexem = GetLexem(input, nextEndIdx, endIdx, nextLexemType);
					nextEndIdx = endIdx;
					
					if (nextLexemType==LexemType.Delimiter && nextLexem=="*") {
						// just read next lexem...
						endIdx = nextEndIdx;
						nextLexemType = GetLexemType(input, endIdx, out nextEndIdx);
						nextLexem = GetLexem(input, endIdx, nextEndIdx);
						if (nextLexemType!=LexemType.Delimiter || nextLexem!="]")
							throw new Exception(
								String.Format("Invalid syntax (position: {0}, expression: {1})", endIdx, input ) );
					} else {
						StringBuilder fieldsBuilder = new StringBuilder();
						fieldsBuilder.Append( nextLexem );
						do {
							nextLexemType = GetLexemType(input, endIdx, out nextEndIdx);
							nextLexem = GetLexem(input, endIdx, nextEndIdx, nextLexemType);
							endIdx = nextEndIdx;
							if (nextLexemType==LexemType.Delimiter && nextLexem=="]")
								break;
							if (nextLexemType==LexemType.Stop)
								break;
							fieldsBuilder.Append( nextLexem );
						} while (true);
						fields = fieldsBuilder.ToString().Split(',');
					}
				} else {
					// if brackets [] not specified near the name, threat it as field name
					if (AllowDumpConstants && lexem.ToLower()!=nullField)
						return (QConst)lexem;
					else
						return (QField)lexem;
				}
				endIdx = nextEndIdx;

				Query q = new Query( sourceName, rootCondition);
				
				// limits?
				nextLexemType = GetLexemType(input, endIdx, out nextEndIdx);
				nextLexem = GetLexem(input, endIdx, nextEndIdx);
				if (nextLexemType==LexemType.Delimiter && nextLexem=="{") {
					// read start record
					endIdx = nextEndIdx;
					nextLexemType = GetLexemType(input, endIdx, out nextEndIdx);
					nextLexem = GetLexem(input, endIdx, nextEndIdx);
					if (nextLexemType!=LexemType.Constant)
						throw new Exception(
							String.Format("Invalid syntax (position: {0}, expression: {1})", endIdx, input ) );
					q.StartRecord = Int32.Parse(nextLexem);
					// read comma
					endIdx = nextEndIdx;
					nextLexemType = GetLexemType(input, endIdx, out nextEndIdx);
					nextLexem = GetLexem(input, endIdx, nextEndIdx);
					if (nextLexemType!=LexemType.Delimiter || nextLexem!=",")
						throw new Exception(
							String.Format("Invalid syntax (position: {0}, expression: {1})", endIdx, input ) );
						
					// read record count
					endIdx = nextEndIdx;
					nextLexemType = GetLexemType(input, endIdx, out nextEndIdx);
					nextLexem = GetLexem(input, endIdx, nextEndIdx);
					if (nextLexemType!=LexemType.Constant)
						throw new Exception(
							String.Format("Invalid syntax (position: {0}, expression: {1})", endIdx, input ) );
					q.RecordCount = Int32.Parse(nextLexem);				
					
					// read close part '}'
					endIdx = nextEndIdx;
					nextLexemType = GetLexemType(input, endIdx, out nextEndIdx);
					nextLexem = GetLexem(input, endIdx, nextEndIdx);
					if (nextLexemType!=LexemType.Delimiter || nextLexem!="}")
						throw new Exception(
							String.Format("Invalid syntax (position: {0}, expression: {1})", endIdx, input ) );

					endIdx = nextEndIdx;
				}
								
				q.Fields = fields;
				return q;
			}
			
			throw new Exception(
				String.Format("Invalid syntax (position: {0}, expression: {1})", endIdx, input ) );
		}

		protected string ParseNodeName(string input, int startIdx, out int endIdx) {
			string nodeName = null;
			// check for node name - starts with '<'
			LexemType lexemType = GetLexemType(input, startIdx, out endIdx);
			string lexem = GetLexem(input, startIdx, endIdx);
			if (lexemType==LexemType.Delimiter && lexem=="<") {
				startIdx = endIdx;
				lexemType = GetLexemType(input, startIdx, out endIdx);
				if (lexemType!=LexemType.Name && lexemType!=LexemType.Constant && lexemType!=LexemType.QuotedConstant)
					throw new Exception(
						String.Format("Invalid syntax - node name expected (position: {0}, expression: {1})", startIdx, input ) );
				nodeName = GetLexem(input, startIdx, endIdx);
				startIdx = endIdx;
				// read closing delimiter '>'
				lexemType = GetLexemType(input, startIdx, out endIdx);
				lexem = GetLexem(input, startIdx, endIdx);
				if (lexemType!=LexemType.Delimiter || lexem!=">")
					throw new Exception(
						String.Format("Invalid syntax (position: {0}, expression: {1})", startIdx, input ) );
			} else {
				endIdx = startIdx; 
			}

			return nodeName;
		}
		
		protected IQueryNode ParseConditionGroup(string input, int startIdx, out int endIdx) {
			int nextEndIdx;
			LexemType lexemType = GetLexemType(input, startIdx, out nextEndIdx);
			string lexem = GetLexem(input, startIdx, nextEndIdx);
			
			IQueryNode node;
			if (lexemType==LexemType.Delimiter && lexem=="(") {
				string nodeName = ParseNodeName(input, nextEndIdx, out endIdx);
				nextEndIdx = endIdx;

				// check for empty group
				lexemType = GetLexemType(input, nextEndIdx, out endIdx);
				if (lexemType==LexemType.Delimiter && GetLexem(input,nextEndIdx,endIdx)==")") {
					node = null;
					// push back
					endIdx = nextEndIdx;
				} else
					node = ParseConditionGroup(input, nextEndIdx, out endIdx);

				if (nodeName!=null) {
					if (node==null)
						node = new QueryGroupNode(GroupType.And);
					if (node is QueryNode)
						((QueryNode)node).Name = nodeName;
					// for some reason QueryGroupNode is not derived from QueryNode...
					if (node is QueryGroupNode)
						((QueryGroupNode)node).Name = nodeName;
				}

				// read ')'
				lexemType = GetLexemType(input, endIdx, out nextEndIdx);
				if (lexemType!=LexemType.Delimiter || GetLexem(input,endIdx,nextEndIdx)!=")")
					throw new Exception(
						String.Format("Invalid syntax (position: {0}, expression: {1})", endIdx, input ) );
				endIdx = nextEndIdx;
			} else {
				node = ParseCondition(input, startIdx, out nextEndIdx);
				endIdx = nextEndIdx;
			}
			
			// check for group
			lexemType = GetLexemType(input, endIdx, out nextEndIdx);
			GroupType groupType = GroupType.And;
			if (GetGroupType(lexemType, input, endIdx, ref nextEndIdx, ref groupType)) {
				QueryGroupNode group = new QueryGroupNode(groupType);
				group.Nodes.Add(node);
				group.Nodes.Add( ParseConditionGroup(input, nextEndIdx, out endIdx) );
				return group;
			}

			return node;		
		}
		
		protected IQueryNode ParseCondition(string input, int startIdx, out int endIdx) {
			
			IQueryValue leftValue = ParseInternal(input, startIdx, out endIdx); 
			// special case if legacy 'allow dump constants' mode is on
			if (AllowDumpConstants && leftValue is QConst)
				leftValue = new QField( ((QConst)leftValue).Value.ToString() );
			
			int nextEndIdx;
			Conditions conditions = Conditions.Equal;

			LexemType nextLexemType = GetLexemType(input, endIdx, out nextEndIdx);
			if (!GetCondition(nextLexemType, input, endIdx, ref nextEndIdx, ref conditions))
				throw new Exception(
					String.Format("Invalid syntax (position: {0}, expression: {1})", startIdx, input ) );

			IQueryValue rightValue = ParseInternal(input, nextEndIdx, out endIdx);
			IQueryNode node;
			if (IsNullValue(rightValue)) {
				if ( (conditions & Conditions.Equal)!=0 )
					node = new QueryConditionNode( leftValue, Conditions.Null | (conditions & ~Conditions.Equal), null);
				else
					throw new Exception(
						String.Format("Invalid syntax - such condition cannot be used with 'null' (position: {0}, expression: {1})", startIdx, input ) );
			} else
				node = new QueryConditionNode( leftValue, conditions, rightValue);
			
			return node;
		}
		
		protected bool IsNullValue(IQueryValue value) {
			return ((value is IQueryFieldValue) && ((IQueryFieldValue)value).Name.ToLower()==nullField);
		}
		

		/// <summary>
		/// Parse [conditions] from string representation
		/// </summary>
		/// <remarks>
		/// TODO: implement normal recursive parsing instead very simplified procedure
		/// </remarks>
		/*protected virtual IQueryNode ParseGroup(string input, int startIdx, out int endIdx) {
			int condEndIdx;
			IQueryNode cond = ParseCondition(input, startIdx, out condEndIdx);
			
			Match match = GroupRegEx.Match( input, condEndIdx );
			if (!match.Success || match.Index!=condEndIdx) {
				endIdx = condEndIdx;
				return cond;
			}
			
			QueryGroupNode groupNode = new QueryGroupNode( enumGroups[ Array.IndexOf(strGroups, match.Groups["group"].Value) ] );
			groupNode.Nodes.Add( cond );
			groupNode.Nodes.Add( ParseGroup(input, match.Groups["tail"].Index, out endIdx) );
			return groupNode;
		}*/
		
		
		
		
		
	}
}

