using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;

namespace PCLG
{
	class TypeDefinition
	{
		public string Namespace { get; set; }
		public string Name { get; set; }
	}

	class EnumDefinition : TypeDefinition
	{
		public EnumDefinition ()
		{
			this.Enums = new List<string> ();
		}
		public List<string> Enums { get; private set; }
	}

	class StructClassDefinition : TypeDefinition
	{
		public StructClassDefinition (Type type)
		{
			this.type = type;
			this.Interfaces = new List<Type> ();
			this.Properties = new List<PropertyInfo> ();
			this.Methods = new List<MethodInfo> ();
			this.Constructors = new List<ConstructorInfo> ();
		}
		public Type type {get; private set;}
		public List<Type> Interfaces { get; private set; }
		public List<PropertyInfo> Properties { get; private set; }
		public List<MethodInfo> Methods { get; private set; }
		public List<ConstructorInfo> Constructors { get; private set; }
	}

	class AssemblyDefinition
	{
		public AssemblyDefinition ()
		{
			Enumerations = new Dictionary<string, EnumDefinition> ();
			Interfaces = new List<StructClassDefinition> ();
			Structures = new List<StructClassDefinition> ();
			Classes = new List<StructClassDefinition> ();
		}
		public Dictionary<string, EnumDefinition> Enumerations { get; private set; }
		public List<StructClassDefinition> Interfaces { get; private set; }
		public List<StructClassDefinition> Structures { get; private set; }
		public List<StructClassDefinition> Classes { get; private set; }

		public void Process (Assembly assembly)
		{
			Type[] types = assembly.GetExportedTypes ();
			foreach (var enums in types.Where (x => x.IsEnum && x.Namespace.StartsWith ("Microsoft.Xna.Framework"))) {

				var def = this.Enumerations.ContainsKey(enums.Name) ? this.Enumerations[enums.Name] : null;
				if (def == null) {
					def = new EnumDefinition ();
					this.Enumerations.Add (enums.Name, def);
				}
				def.Namespace = enums.Namespace;
				def.Name = enums.Name;
				var enumnames = enums.GetEnumNames ();
				foreach (var i in enumnames) {
					if (!def.Enums.Contains(i))
						def.Enums.Add (i);
				}
			}
			foreach (var interfacedef in types.Where (x => x.IsInterface && x.Namespace.StartsWith("Microsoft.Xna.Framework"))) {
				var s = new StructClassDefinition (interfacedef);
				s.Namespace = interfacedef.Namespace;
				s.Name = interfacedef.Name;
				foreach (var i in interfacedef.GetInterfaces ()) {
					s.Interfaces.Add (i);
				}
				foreach (var p in interfacedef.GetProperties (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)) {
					if (p.DeclaringType != interfacedef)
						continue;
					s.Properties.Add (p);
				}
				foreach (var m in interfacedef.GetMethods (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)) {
					if (m.Name.StartsWith ("get_")) continue;
					if (m.Name.StartsWith ("set_")) continue;
					if (m.Name.StartsWith ("add_")) continue;
					if (m.Name.StartsWith ("remove_")) continue;
					if (m.DeclaringType != interfacedef)
						continue;
					s.Methods.Add (m);
				}
				this.Interfaces.Add (s);
			}
			foreach (var type in types.Where (x => x.IsValueType && !x.IsEnum)) {
				var s = new StructClassDefinition (type);
				s.Namespace = type.Namespace;
				s.Name = type.Name;
				foreach (var i in type.GetInterfaces ()) {
					s.Interfaces.Add (i);
				}
				foreach (var p in type.GetProperties (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)) {
					if (p.DeclaringType != type)
						continue;
					s.Properties.Add (p);
				}
				foreach (var m in type.GetMethods (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)) {
					if (m.Name.StartsWith ("get_")) continue;
					if (m.Name.StartsWith ("set_")) continue;
					if (m.Name.StartsWith ("add_")) continue;
					if (m.Name.StartsWith ("remove_")) continue;
					if (m.DeclaringType != type)
						continue;
					s.Methods.Add (m);
				}
				this.Structures.Add (s);
			}
			
			foreach (var type in types.Where (x => x.IsClass && !x.Namespace.Contains("Microsoft.Xna.Framework.Storage"))) {
				var s = new StructClassDefinition (type);
				s.Namespace = type.Namespace;
				s.Name = type.Name;
				foreach (var i in type.GetInterfaces ().Except(type.BaseType != null ? type.BaseType.GetInterfaces() : new Type[0])) {
					if (i.IsPublic)
						s.Interfaces.Add (i);
				}
				foreach (var p in type.GetProperties (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)) {
					if (p.DeclaringType != type)
						continue;
					s.Properties.Add (p);
				}
				foreach (var c in type.GetConstructors (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
					s.Constructors.Add (c);
				}
				foreach (var m in type.GetMethods (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
					if (m.Name.StartsWith ("get_")) continue;
					if (m.Name.StartsWith ("set_")) continue;
					if (m.Name.StartsWith ("add_")) continue;
					if (m.Name.StartsWith ("remove_")) continue;
					if (m.DeclaringType != type) 
						continue;
					s.Methods.Add (m);
				}
				this.Classes.Add (s);

			}

		}

		void ExportInterfaces (List<Type> interfaces, StreamWriter sw)
		{
			var i = interfaces;
			if (i != null && i.Count () > 0) {
				foreach (var inter in i) {
					if (!inter.IsGenericType)
						sw.Write ("{0}", inter.Name);
					else {
						sw.Write ("{0}<{1}>", inter.Name.Replace("`1", String.Empty), inter.GenericTypeArguments[0].Name);
					}
					if (inter != i.Last ())
						sw.Write (",");
				}
			}
		}

		public void Export (string fileName)
		{
			var sw = File.CreateText (fileName);
			sw.WriteLine ("using System;");
			sw.WriteLine ("using System.IO;");
			sw.WriteLine ("using System.Text;");
			sw.WriteLine ("using System.Globalization;");
			sw.WriteLine ("using System.Collections;");
			sw.WriteLine ("using System.Collections.Generic;");
			sw.WriteLine ("using System.Collections.ObjectModel;");
			sw.WriteLine ("using Microsoft.Xna.Framework;");
			sw.WriteLine ("using Microsoft.Xna.Framework.Input;");
			sw.WriteLine ("using Microsoft.Xna.Framework.Content;");
			sw.WriteLine ("using Microsoft.Xna.Framework.Graphics;");
			//sw.WriteLine ("using Microsoft.Xna.Framework.Storage;");
			foreach(var e in this.Enumerations.Values) {
				sw.WriteLine ("namespace {0} {{", e.Namespace);
				sw.WriteLine ("	public enum {0} {{", e.Name);
				var enumnames = e.Enums;
				foreach (var i in enumnames) {
					sw.Write ("		{0}", i);
					if (i != enumnames.Last ())
						sw.WriteLine (",");
					else sw.WriteLine ();
				}
				sw.WriteLine ("	}");
				sw.WriteLine ("}");
				sw.WriteLine ();
			}

			foreach (var type in this.Interfaces) {
				sw.WriteLine ("namespace {0} {{", type.Namespace);
				sw.Write ("	public interface ");
				WriteType (type.type, sw);
				if (type.Interfaces.Count > 0)
					sw.Write (" : ");
				ExportInterfaces (type.Interfaces, sw);
				sw.WriteLine (" {");
				// export methods and properties
				ExportProperties (type.Properties, sw, false);
				ExportMethods (type.Methods, sw);
				sw.WriteLine ("	}");
				sw.WriteLine ("}");
				sw.WriteLine ();
			}

			foreach (var type in this.Structures) {
				sw.WriteLine ("namespace {0} {{", type.Namespace);
				sw.Write ("	public struct {0} ", type.Name);
				if ((type.type.BaseType.Name != "ValueType") || type.Interfaces.Count > 0) {
					sw.Write (" : ");
				}
				if (type.type.BaseType != null) {
					WriteType (type.type.BaseType, sw);
					if (type.Interfaces.Count > 0 && type.type.BaseType.Name != "ValueType")
						sw.Write (", ");
				}
				ExportInterfaces (type.Interfaces, sw);
				sw.WriteLine (" {");
				// export methods and properties
				ExportProperties (type.Properties, sw);
				ExportMethods (type.Methods, sw);
				sw.WriteLine ("	}");
				sw.WriteLine ("}");
				sw.WriteLine ();
			}
			foreach (var type in this.Classes) {
				sw.WriteLine ("namespace {0} {{", type.Namespace);
				sw.Write ("	public class ");
				WriteType (type.type, sw);
				if ((type.type.BaseType != null && type.type.BaseType.BaseType != null) || type.Interfaces.Count > 0)
					sw.Write (" : ");
				if (type.type.BaseType != null) {
					WriteType (type.type.BaseType, sw);
					if (type.Interfaces.Count > 0 && type.type.BaseType.BaseType != null)
						sw.Write (", ");
				}
				ExportInterfaces (type.Interfaces, sw);
				sw.WriteLine (" {");
				// export methods and properties
				ExportProperties (type.Properties, sw);
				ExportConstructors (type.Constructors, sw);
				ExportMethods (type.Methods, sw);
				sw.WriteLine ("	}");
				sw.WriteLine ("}");
				sw.WriteLine ();
			}

			sw.Flush ();
			sw.Close ();
			sw.Dispose ();

		}

		private void ExportConstructors (List<ConstructorInfo> list, StreamWriter sw)
		{
			foreach (var meth in list) {
				sw.WriteLine ();
				if (!meth.DeclaringType.IsInterface)
					sw.Write ("		public ");
				if (meth.IsStatic)
					sw.Write ("static ");
				
				if (!meth.IsGenericMethod) {
					WriteType (meth.DeclaringType, sw);
					sw.Write (" (");
				} else {
					WriteType (meth.DeclaringType, sw);
					sw.Write (" (");
					/*var gp = meth.GetGenericArguments ();
					foreach (var g in gp) {
						WriteType (g, sw, true);
						if (g != gp.Last ())
							sw.Write (",");
					}
					sw.Write (">(");*/
				}
				var parameters = meth.GetParameters ();
				foreach (var param in parameters) {
					if (param.IsOut) {
						sw.Write ("out ");
					} else if (param.ParameterType.IsByRef)
						sw.Write ("ref ");
					WriteType (param.ParameterType, sw, true);

					sw.Write (" {0} ", param.Name);
					if (param.HasDefaultValue) {
						sw.Write (" = {0}", param.DefaultValue == null ? "null" : param.DefaultValue.ToString ());
					}
					if (param != parameters.Last ())
						sw.Write (",");
				}
				sw.WriteLine (") {");
				sw.WriteLine ();
				sw.WriteLine ("			throw new NotImplementedException();");
				sw.WriteLine ("		}");
				sw.WriteLine ();
			}
		}

		private string ProcessFullName (string name)
		{
			switch (name) {
				case "Void": return "void";
				case "Boolean": return "bool";
				case "Int32": return "int";
				case "Single": return "float";
				case "Double": return "double";
				default:
					return name.Trim();
			}
		}

		private void WriteType (Type type, StreamWriter sw, bool allowobject = false)
		{
			if (type.Name == "ValueType") return;
			if (type.FullName == "System.Object" && !allowobject) 
				return;
			//if (!type.IsInterface && type.BaseType == null) return;
			if (!type.IsGenericType) {
					if (type.Name.Contains ("Nullable`1")) {
						var et = type.GetElementType ();
						sw.Write ("Nullable<{0}>", ProcessFullName(Nullable.GetUnderlyingType (et).Name));
					} else {

						sw.Write ("{0}", ProcessFullName(type.Name.Replace ("&", String.Empty)));
					}
				
			} else {
					var args = type.GetGenericArguments();
					sw.Write ("{0}<", type.Name.Replace (string.Format ("`{0}", args.Count ()), String.Empty));
					
					for (int i=0; i < args.Length; i++) {
						WriteType (args[i], sw);
						if (i < args.Length - 1)
							sw.Write (",");
					}
					sw.Write (">");
				
			}
		}

		void ExportOperatorOverload (string name, StreamWriter sw)
		{
			switch (name) {
				case "op_Equality": sw.Write (" operator ==");
					break;
				case "op_Inequality": sw.Write (" operator !=");
					break;
				case "op_Multiply": sw.Write (" operator *");
					break;
				case "op_Subtraction": sw.Write (" operator -");
					break;
				case "op_Addition": sw.Write (" operator +");
					break;
				case "op_Division": sw.Write (" operator /");
					break;
				case "op_UnaryNegation": sw.Write (" operator !");
					break;
				default:
					sw.Write(" {0}", name);
					break;
					
			}
			sw.Write (" (");
		}

		private void ExportMethods (List<MethodInfo> list, StreamWriter sw)
		{
			foreach (var meth in list) {
				if (meth.Name.StartsWith ("get_")) continue;
				if (meth.Name.StartsWith ("set_")) continue;
				if (meth.Name.StartsWith ("add_")) continue;
				if (meth.Name.StartsWith ("remove_")) continue;
				if (meth.IsConstructor) continue;
				sw.WriteLine ();
				if (!meth.DeclaringType.IsInterface)
					sw.Write ("		public ");
				if (meth.IsStatic)
					sw.Write ("static ");
				WriteType (meth.ReturnType, sw, true);
				if (meth.IsSpecialName) {
					ExportOperatorOverload( meth.Name, sw);
				} else {
					if (!meth.IsGenericMethod) {
						sw.Write (" {0} (", meth.Name);
					} else {
						sw.Write (" {0}<", meth.Name);
						var gp = meth.GetGenericArguments ();
						foreach (var g in gp) {
							WriteType (g, sw, true);
							if (g != gp.Last ())
								sw.Write (",");
						}
						sw.Write (">(");
					}
				}
				var parameters = meth.GetParameters ();
				foreach (var param in parameters) {
					if (param.IsOut) {
						sw.Write ("out ");
					} else if (param.ParameterType.IsByRef)
						sw.Write ("ref ");
					WriteType (param.ParameterType, sw, true);
					
					sw.Write (" {0} ", param.Name);
					if (param.HasDefaultValue) {
						sw.Write (" = {0}", param.DefaultValue == null ? "null" : param.DefaultValue.ToString ());
					}
					if (param != parameters.Last ())
						sw.Write (",");
				}
				sw.WriteLine (") {");
				sw.WriteLine ();
				sw.WriteLine ("			throw new NotImplementedException();");
				sw.WriteLine ("		}");
				sw.WriteLine ();
			}
		}

		private void ExportProperties (List<PropertyInfo> list, StreamWriter sw, bool writePublic = true)
		{
			foreach (var prop in list) {
				sw.WriteLine ();
				if (writePublic)
					sw.Write ("		public ");
				else sw.Write ("		");
				WriteType (prop.PropertyType, sw, true);
				sw.Write (" {0} {{", prop.Name);
				if (prop.CanRead) sw.Write ("get;");
				if (prop.CanWrite) sw.Write ("set;");
				sw.WriteLine ("}");
				sw.WriteLine ();
			}
		}
	}

	class Program
	{
		static void Main (string[] args)
		{
			var asmdef = new AssemblyDefinition ();

			Assembly asm = Assembly.LoadFrom (args[0]);
			asmdef.Process (asm);
			asmdef.Export (@"C:\Users\Dean\Documents\Projects\dellis1972\infspacestudios\PCLG\PCLG\bin\Debug\code.cs");

			/*
			foreach (var type in types.Where (x => x.IsClass && x.Namespace == "Microsoft.Xna.Framework")) {
				sw.WriteLine ("namespace {0} {{", type.Namespace);
				sw.WriteLine ("	public class {0} {{", type.Name);
				foreach (var con in type.GetConstructors ()) {
				}
				foreach (var prop in type.GetProperties (BindingFlags.Public | BindingFlags.Instance)) {
					sw.WriteLine ();
					sw.Write ("		public {0} ", prop.PropertyType.FullName);
					sw.Write ("{0} {{", prop.Name);
					if (prop.CanRead) sw.Write ("get;");
					if (prop.CanWrite) sw.Write ("set;");
					sw.WriteLine ("}");
					sw.WriteLine ();
				}
				foreach (var meth in type.GetMethods (BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)) {
					if (meth.Name.StartsWith ("get_")) continue;
					if (meth.Name.StartsWith ("set_")) continue;
					if (meth.Name.StartsWith ("add_")) continue;
					if (meth.Name.StartsWith ("remove_")) continue;
					sw.WriteLine ();
					sw.Write ("		public {0} ", meth.ReturnType.FullName);
					sw.Write ("{0} (", meth.Name);
					var parameters = meth.GetParameters ();
					foreach (var param in parameters) {
						if (param.IsOut) sw.Write ("out ");
						sw.Write ("{0} ", param.ParameterType.FullName);
						sw.Write ("{0} ", param.Name);
						if (param.HasDefaultValue) {
							sw.Write (" = {0}", param.DefaultValue == null ? "null" : param.DefaultValue.ToString ());
						}
						if (param != parameters.Last ())
							sw.Write (",");
					}
					sw.WriteLine (") {");
					sw.WriteLine ();
					sw.WriteLine ("			throw new NotImplementedException();");
					sw.WriteLine ("		}");
					sw.WriteLine ();
				}
				sw.WriteLine ("	}");
				sw.WriteLine ("}");
				sw.WriteLine ();
			}*/
			
		}
	}
}
