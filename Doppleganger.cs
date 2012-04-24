/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace utils
{
    /// <summary>
    /// Mock Interface Generator application.
    /// </summary>
    public class DopplegangerApp
    {
        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("You must specify an assembly to parse.");
                return;
            }

            Doppleganger mg = new Doppleganger();

            mg.generate(args[0]);
        }
    }

    /// <summary>
    /// Mock Interface Generator worker class
    /// </summary>
    public class Doppleganger
    {
        private int outputLevel = 0;

        private static void padToLevel(int padLevel)
        {
            for (int i = 0; i < padLevel; i++)
            {
                Console.Write("   ");
            }
        }

        /// <summary>
        /// Output the specified text.
        /// </summary>
        /// <param name="text"></param>
        private void output(string text)
        {
            // Replace the embedded \n character with indented outputLevel spacing
            string[] lines = text.Split(new char[] { '\n' });

            foreach (string line in lines)
            {
                padToLevel(outputLevel);
                Console.WriteLine(line);
            }
        }

        /// <summary>
        /// Get the field delimiter type.  This is useful for 'stringizing'
        /// string fields.  Non-string and non-char fields will not have
        /// a delimiter returned.
        /// </summary>
        /// <param name="fieldType"></param>
        /// <returns></returns>
        private static string getFieldDelim(Type fieldType)
        {
            string delimiter = "";

            if (fieldType == typeof(string))
            {
                delimiter = "\"";
            }
            else if (fieldType == typeof(char))
            {
                delimiter = "\'";
            }

            return delimiter;
        }

        /// <summary>
        /// Format the full type name.
        /// Nested type names are handled correctly.
        /// Nullable types are supported.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static string formatTypeName(Type type)
        {
            string typeSig = "";
            bool isNullableType = false;

            // Check for void type
            if (type == null || type == typeof(void))
            {
                typeSig += "void";
            }
            else if (type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // Nullable type
                NullableConverter nullableType = new NullableConverter(type);

                typeSig += nullableType.UnderlyingType.FullName;
                isNullableType = true;
            }
            else
            {
                if (type.IsGenericType || type.FullName.Contains("`"))
                {
                    Type[] genericParams = type.GetGenericArguments();
					ArrayList genericParamList = new ArrayList();

					foreach(Type genericParam in genericParams)
					{
						genericParamList.Add(formatTypeName(genericParam));
					}

					typeSig += String.Format("{0}<{1}>",
						type.FullName.Substring(0, type.FullName.IndexOf('`')),
						String.Join(",", (string[]) genericParamList.ToArray(typeof(string))));
				}
                else
                {
                    // Standard type
                    typeSig += type.FullName;
                }
            }

            // Remove the '+' from nested type names
            typeSig = typeSig.Replace('+', '.');
            // Remove IL reference syntax (out and ref)
            typeSig = typeSig.Trim(new char[] { '&' });
            if (isNullableType)
            {
                typeSig += "?";
            }

            return typeSig;
        }

        /// <summary>
        /// Get a correct default value for the type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static string getDefaultValue(Type type)
        {
            string defaultSig = "";

            if (type == typeof(bool) || type == typeof(System.Boolean))
            {
                defaultSig += "false";
            }
            else if (type == typeof(char))
            {
                defaultSig += "'\\0'";
            }
            else if (type == typeof(IntPtr))
            {
                defaultSig += "System.IntPtr.Zero";
            }
            else if (type == typeof(UIntPtr))
            {
                defaultSig += "System.UIntPtr.Zero";
            }
            else if (type.IsPrimitive)
            {
                defaultSig += "0";
            }
            else if (type.IsEnum)
            {
                defaultSig += getDefaultEnumValue(type);
            }
            else if (type.IsValueType)
            {
                defaultSig += "new " + type.FullName + "()";
            }
            else if (type != typeof(void))
            {
                defaultSig += "null";
            }

            return defaultSig;
        }

        protected static string getDefaultEnumValue(Type type)
        {
            return getEnumValueByIndex(type, 0);
        }

        protected static string getEnumValueByIndex(Type type, int index)
        {
            // Use the first enum value as the default
            string enumVal = "";
            BindingFlags binding = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                                   | BindingFlags.DeclaredOnly;
            FieldInfo[] fieldInfos = type.GetFields(binding);
            int currentIndex = 0;

            foreach (FieldInfo fieldInfo in fieldInfos)
            {
                if (String.Compare("value__", fieldInfo.Name) == 0)
                {
                    // Skip the special enum IL value
                    continue;
                }

                currentIndex++;
                if (currentIndex > index)
                {
                    enumVal += formatTypeName(fieldInfo.FieldType) + "." + fieldInfo.Name;
                    break;
                }
            }

            return enumVal;
        }

        /// <summary>
        /// Format a correct default return statement for the type.
        /// </summary>
        /// <param name="returnType"></param>
        /// <returns></returns>
        private static string formatReturnValue(Type returnType)
        {
            string returnSig = "";

            if (returnType != typeof(void))
            {
                returnSig += "return " + getDefaultValue(returnType) + ";";
            }

            return returnSig;
        }

        /// <summary>
        /// Format the function parameters
        /// </summary>
        /// <param name="paramInfos"></param>
        /// <param name="outParams"></param>
        /// <returns></returns>
        private static string formatParams(ParameterInfo[] paramInfos, out ParameterInfo[] outParams)
        {
            string paramSig = "";
            int paramNum = 1;
            ArrayList outParamArray = new ArrayList();

            foreach (ParameterInfo paramInfo in paramInfos)
            {
                if (paramNum > 1)
                {
                    paramSig += ", ";
                }

                if (paramInfo.IsIn && paramInfo.IsOut)
                {
                    paramSig += "ref ";
                }
                else if (paramInfo.IsOut)
                {
                    paramSig += "out ";
                    outParamArray.Add(paramInfo);
                }

                // Mangle the parameter name to avoid reserved word conflicts
                paramSig += formatTypeName(paramInfo.ParameterType) + " param_" + paramInfo.Name;
                paramNum++;
            }

            outParams = (ParameterInfo[])outParamArray.ToArray(typeof(ParameterInfo));
            return paramSig;
        }

        private static string formatOutParams(ParameterInfo[] outParams)
        {
            string outParamSig = "";

            foreach (ParameterInfo paramInfo in outParams)
            {
                outParamSig += "   param_" + paramInfo.Name + " = " + getDefaultValue(paramInfo.ParameterType.GetElementType()) + ";\n";
            }

            return outParamSig;
        }

        /// <summary>
        /// Overloaded hasMember() to provide default memberPrefixes parameter.
        /// </summary>
        /// <param name="memberName"></param>
        /// <param name="memberInfos"></param>
        /// <returns></returns>
        private static bool hasMember(string memberName, MemberInfo[] memberInfos)
        {
            return hasMember(memberName, memberInfos, null);
        }

        /// <summary>
        /// Check to see if the specified memberName exists in the memberInfos.
        /// The memberPrefixes array will be used to prefix the memberName as it
        /// is being searched in the memberInfos array.  This is useful when searching
        /// for an accessor function that is prefixed with get_ or set_.
        /// </summary>
        /// <param name="memberName"></param>
        /// <param name="memberInfos"></param>
        /// <param name="memberPrefixes"></param>
        /// <returns></returns>
        private static bool hasMember(string memberName, MemberInfo[] memberInfos, string[] memberPrefixes)
        {
            bool checkForMemberName = true;
            bool hasPrefixes = false;

            if (memberPrefixes != null && memberPrefixes.Length > 0)
            {
                hasPrefixes = true;
                checkForMemberName = false;
                foreach (string prefix in memberPrefixes)
                {
                    if (memberName.StartsWith(prefix))
                    {
                        checkForMemberName = true;
                        break;
                    }
                }
            }

            if (checkForMemberName)
            {
                foreach (MemberInfo memberInfo in memberInfos)
                {
                    if (hasPrefixes)
                    {
                        foreach (string prefix in memberPrefixes)
                        {
                            string fullName = prefix + memberInfo.Name;

                            if (String.Compare(fullName, memberName) == 0)
                            {
                                return true;
                            }
                        }
                    }
                    else
                    {
                        if (String.Compare(memberInfo.Name, memberName) == 0)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Declare a class, enum, or interface type.  Supports declaring nested types recursively.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="nestedTypes"></param>
        private void declareClass(Type type, Type[] nestedTypes)
        {
            string classSig = "";

            if (type.IsClass)
            {
                classSig = "public class ";
            }
            else if (type.IsEnum)
            {
                classSig = "public enum ";
            }
            else if (type.IsInterface)
            {
                classSig = "public interface ";
            }
            else if (type.IsValueType)
            {
                classSig = "public struct ";
            }

            Type[] interfaceInfos = filterPublicTypes(type.GetInterfaces());

            classSig += type.Name;
            bool setFirstInherit = false;

            if (type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
            {
                if (!(type.IsEnum && type.BaseType == typeof(Enum)))
                {
                    classSig += " : " + formatTypeName(type.BaseType);
                    setFirstInherit = true;
                }
            }

            if (!type.IsEnum && interfaceInfos.Length > 0)
            {
                foreach (Type intf in interfaceInfos)
                {
                    if (!setFirstInherit)
                    {
                        classSig += " : ";
                        setFirstInherit = true;
                    }
                    else
                    {
                        classSig += ", ";
                    }

                    classSig += formatTypeName(intf);
                }
            }

            BindingFlags publicBinding = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            BindingFlags privateBinding = BindingFlags.NonPublic | BindingFlags.Instance;
            ConstructorInfo[] constructorInfos = type.GetConstructors(publicBinding);
            FieldInfo[] fieldInfos = type.GetFields(publicBinding);
            FieldInfo[] privateFieldInfos = type.GetFields(privateBinding);
            MethodInfo[] methodInfos = type.GetMethods(publicBinding);
            EventInfo[] eventInfos = type.GetEvents(publicBinding);
            PropertyInfo[] propertyInfos = type.GetProperties(publicBinding);
            bool hasDefaultConstructor = false;

            output(classSig + "\n{");
            outputLevel++;

            string currentTypeName = formatTypeName(type);

            foreach (Type nestedType in nestedTypes)
            {
                if (nestedType.IsNested
                    && String.Compare(formatTypeName(nestedType.DeclaringType), currentTypeName) == 0)
                {
                    // This is a nested type.
                    declareType(nestedType, nestedTypes);
                }
            }

            foreach (ConstructorInfo constructor in constructorInfos)
            {
                string constructorSig = "public " + type.Name + "(";
                ParameterInfo[] outParams;
                string paramSig = formatParams(constructor.GetParameters(), out outParams);

                if (paramSig.Length == 0)
                {
                    hasDefaultConstructor = true;
                }

                constructorSig += paramSig + ")";
                if (type.IsInterface)
                {
                    constructorSig += ";";
                }
                else
                {
                    constructorSig += "\n{\n";
                    if (outParams.Length > 0)
                    {
                        constructorSig += formatOutParams(outParams);
                    }

                    constructorSig += "}";
                }

                output(constructorSig);
            }

            if (type.IsClass && !hasDefaultConstructor)
            {
                // Add a default protected constructor so that derived classes
                // can be instantiated without having to explicitly call overloaded
                // base class constructors.
                output("protected " + type.Name + "()\n{\n}");
            }

            int fieldNum = 0;

            foreach (FieldInfo fieldInfo in fieldInfos)
            {
                if (type.IsEnum && String.Compare("value__", fieldInfo.Name) == 0)
                {
                    // Skip the special enum IL value
                    continue;
                }

                string fieldSig = "";

                if (type.IsEnum)
                {
                    if (fieldNum > 0)
                    {
                        fieldSig += ", ";
                    }

                    fieldSig += fieldInfo.Name;
                }
                else
                {
                    fieldSig += "public ";

                    if (fieldInfo.IsLiteral)
                    {
                        fieldSig += "const ";
                    }
                    else if (fieldInfo.IsStatic)
                    {
                        fieldSig += "static ";
                    }

                    if (fieldInfo.IsInitOnly)
                    {
                        fieldSig += "readonly ";
                    }

                    fieldSig += formatTypeName(fieldInfo.FieldType) + " " + fieldInfo.Name;
                    if (fieldInfo.IsLiteral)
                    {
                        fieldSig += " = ";
                        if (fieldInfo.FieldType.IsEnum)
                        {
                            object defaultVal = fieldInfo.GetRawConstantValue();

                            fieldSig += getEnumValueByIndex(fieldInfo.FieldType, (int)defaultVal);
                        }
                        else
                        {
                            object defaultVal = fieldInfo.GetValue(fieldInfo);
                            string fieldDelim = getFieldDelim(fieldInfo.FieldType);

                            fieldSig += fieldDelim + defaultVal + fieldDelim;
                        }
                    }

                    fieldSig += ";";
                }

                output(fieldSig);
                fieldNum++;
            }

            foreach (PropertyInfo propertyInfo in propertyInfos)
            {
                string propSig = "";

                if (!type.IsInterface)
                {
                    propSig += "public ";
                }

                propSig += formatTypeName(propertyInfo.PropertyType) + " " + propertyInfo.Name;
                if (propertyInfo.CanRead || propertyInfo.CanWrite)
                {
                    propSig += "\n{\n";
                    if (propertyInfo.CanRead)
                    {
                        if (!type.IsInterface)
                        {
                            propSig += "   get { ";
                            propSig += formatReturnValue(propertyInfo.PropertyType);
                            propSig += " }\n";
                        }
                        else
                        {
                            propSig += "   get;\n";
                        }
                    }

                    if (propertyInfo.CanWrite)
                    {
                        if (!type.IsInterface)
                        {
                            propSig += "   set { }\n";
                        }
                        else
                        {
                            propSig += "   set;\n";
                        }
                    }

                    propSig += "}";
                }
                else
                {
                    propSig += ";";
                }

                output(propSig);
            }

            foreach (EventInfo eventInfo in eventInfos)
            {
                if (!type.IsInterface)
                {
                    // Disable the warning that event is never used
                    output("#pragma warning disable 67");
                }

                string eventSig = "";
                if (!type.IsInterface)
                {
                    eventSig += "public ";
                }

                eventSig += "event " + formatTypeName(eventInfo.EventHandlerType) + " " + eventInfo.Name;

                // Determine if this event has add/remove accessors defined.
                // If there is a private field defined for this event, then there are no accessors
                bool addAccessors = true;

                if (type.IsInterface)
                {
                    // Interfaces cannot have accessors
                    addAccessors = false;
                }
                else
                {
                    addAccessors = !hasMember(eventInfo.Name, privateFieldInfos);
                }

                if (addAccessors)
                {
                    eventSig += "\n{\n   add { }\n   remove { }\n}";
                }
                else
                {
                    eventSig += ";";
                }

                output(eventSig);
            }

            foreach (MethodInfo methodInfo in methodInfos)
            {
                // See if this is a property accessor method, or event accessor method
                if (hasMember(methodInfo.Name, propertyInfos, new string[] { "get_", "set_" })
                    || hasMember(methodInfo.Name, eventInfos, new string[] { "add_", "remove_" }))
                {
                    continue;
                }

                string methodInfoSig = "";
                if (!type.IsInterface)
                {
                    methodInfoSig += "public ";
                    if (methodInfo.IsStatic)
                    {
                        methodInfoSig += "static ";
                    }
                    else if (methodInfo.IsVirtual)
                    {
                        if (methodInfo.GetBaseDefinition().DeclaringType != type)
                        {
                            methodInfoSig += "override ";
                        }
                        else if (!type.IsValueType)
                        {
                            methodInfoSig += "virtual ";
                        }
                    }
                }

                switch (methodInfo.Name)
                {
                    case "op_Explicit":
                        methodInfoSig += "explicit operator " + formatTypeName(methodInfo.ReturnType) + "(";
                        break;
                    case "op_Implicit":
                        methodInfoSig += "implicit operator " + formatTypeName(methodInfo.ReturnType) + "(";
                        break;
                    default:
                        methodInfoSig += formatTypeName(methodInfo.ReturnType) + " " + FormatMethodName(methodInfo.Name) + "(";
                        break;
                }

                ParameterInfo[] outParams;
                methodInfoSig += formatParams(methodInfo.GetParameters(), out outParams);
                methodInfoSig += ")";

                if (type.IsInterface)
                {
                    methodInfoSig += ";";
                }
                else
                {
                    methodInfoSig += "\n{\n";
                    if (outParams.Length > 0)
                    {
                        methodInfoSig += formatOutParams(outParams);
                    }

                    string returnVal = formatReturnValue(methodInfo.ReturnType);
                    if (returnVal.Length > 0)
                    {
                        methodInfoSig += "   " + returnVal + "\n";
                    }

                    methodInfoSig += "}";
                }

                output(methodInfoSig);
            }

            outputLevel--;
            output("}");
        }

        private static string FormatMethodName(string inputMethodName)
        {
            if (!inputMethodName.StartsWith("op_"))
            {
                return inputMethodName;
            }
            switch (inputMethodName)
            {
                case "op_Equality":
                    return "operator ==";
                case "op_Inequality":
                    return "operator !=";
                // TODO - other operators
            }
            return inputMethodName;
        }

        /// <summary>
        /// Declare a delegate type.
        /// </summary>
        /// <param name="type"></param>
        private void declareDelegate(Type type)
        {
            BindingFlags binding = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            MethodInfo[] delegateMethods = type.GetMethods(binding);

            // Get the Invoke method info
            MethodInfo delegateinfo = delegateMethods[0];
            ParameterInfo[] outParams;

            string delegatesig = "public delegate ";
            delegatesig += formatTypeName(delegateinfo.ReturnType) + " " + type.Name + "(";
            delegatesig += formatParams(delegateinfo.GetParameters(), out outParams);
            delegatesig += ");";
            output(delegatesig);
        }

        /// <summary>
        /// Declare a generic type.  This supports declaring delegate, class, enum, and interface
        /// types by calling the appropriate helper functions.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="nestedTypes"></param>
        private void declareType(Type type, Type[] nestedTypes)
        {
            if (type.BaseType == typeof(System.Delegate) || type.BaseType == typeof(System.MulticastDelegate))
            {
                declareDelegate(type);
            }
            else
            {
                declareClass(type, nestedTypes);
            }
        }

        /// <summary>
        /// Filter only public types from the Type array.
        /// </summary>
        /// <param name="exportedTypes"></param>
        /// <returns></returns>
        private static Type[] filterPublicTypes(Type[] exportedTypes)
        {
            ArrayList nestedTypes = new ArrayList();

            foreach (Type type in exportedTypes)
            {
                if (type.IsPublic)
                {
                    nestedTypes.Add(type);
                }
            }

            return (Type[])nestedTypes.ToArray(typeof(Type));
        }

        /// <summary>
        /// Filter only nested types from the Type array.
        /// </summary>
        /// <param name="exportedTypes"></param>
        /// <returns></returns>
        private static Type[] filterNestedTypes(Type[] exportedTypes)
        {
            ArrayList nestedTypes = new ArrayList();

            foreach (Type type in exportedTypes)
            {
                if (type.IsNested)
                {
                    nestedTypes.Add(type);
                }
            }

            return (Type[])nestedTypes.ToArray(typeof(Type));
        }

        /// <summary>
        /// Generate the types for the specified assembly.
        /// </summary>
        /// <param name="importlib"></param>
        protected void generateTypes(Assembly importlib)
        {
            Type[] importlibTypes = importlib.GetExportedTypes();
            Type[] nestedTypes = filterNestedTypes(importlibTypes);
            string currentNamespace = null;

            foreach (Type type in importlibTypes)
            {
                if (!type.IsNested)
                {
                    if (type.Namespace != currentNamespace)
                    {
                        if (currentNamespace != null)
                        {
                            // Close the current namespace.
                            outputLevel--;
                            output("}");
                        }

                        currentNamespace = type.Namespace;
                        if (currentNamespace != null)
                        {
                            output("namespace " + currentNamespace + "\n{");
                            outputLevel++;
                        }
                    }

                    declareType(type, nestedTypes);
                }
            }

            if (currentNamespace != null)
            {
                // Close the current namespace.
                outputLevel--;
                output("}");
            }
        }

        /// <summary>
        /// Get the standard assembly copyright attribute meta-data string.
        /// </summary>
        /// <returns></returns>
        protected static string getApacheCopyrightAttribute()
        {
            return "[assembly: System.Reflection.AssemblyCopyrightAttribute(\"Copyright © Apache Software Foundation (ASF) " + DateTime.Now.Year + "\")]";
        }

        /// <summary>
        /// An explanatory _note to be prefixed to the description of the generated assembly source.
        /// </summary>
        protected readonly string assemblyDescriptionAttributeMarkup =
                "This file is a doppleganger of the original assembly.  This assembly only exposes the public API to be used in a testing compilation environment.  ";

        /// <summary>
        /// Should the specified attribute type be generated in the doppleganger source?
        /// </summary>
        /// <param name="attributeType"></param>
        /// <returns></returns>
        protected static bool shouldGenerateAttribute(Type attributeType)
        {
            if (attributeType == typeof(AssemblyCopyrightAttribute)
                || attributeType == typeof(AssemblyKeyFileAttribute)
                || attributeType == typeof(AssemblyKeyNameAttribute)
                || attributeType == typeof(AssemblyDelaySignAttribute))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Generate the assembly metedata attributes for the specified assembly.
        /// Will not replicate strong named attributes, as this information is not available
        /// and would be a violation of security restrictions.  Therefore, it is not entirely
        /// possible to compile against a doppleganger version of a strong-named assembly and then
        /// run the code unmodified with the actual assembly.  In order to run with a strong
        /// named assembly, the application must be compiled with that strong named assembly
        /// as a reference.  However, the doppleganger version of the assembly can still be used as
        /// a stand-in during the development cycle.  Just remember to do a final compile against
        /// the real strong named assembly before final deployment.
        /// </summary>
        /// <param name="importlib"></param>
        protected void generateAssemblyInfo(Assembly importlib)
        {
            IList<CustomAttributeData> attributes = CustomAttributeData.GetCustomAttributes(importlib);
            bool hasDescriptionAttribute = false;

            output(getApacheCopyrightAttribute());

            AssemblyName assemblyName = importlib.GetName();

            output("[assembly: System.Reflection.AssemblyVersionAttribute(\"" + assemblyName.Version + "\")]");

            foreach (CustomAttributeData attribute in attributes)
            {
                if (!shouldGenerateAttribute(attribute.Constructor.DeclaringType))
                {
                    continue;
                }

                string attributeSig = ("[assembly: " + attribute.Constructor.DeclaringType.FullName);

                if (attribute.ConstructorArguments.Count > 0)
                {
                    int argumentIndex = 0;

                    attributeSig += "(";
                    foreach (CustomAttributeTypedArgument argument in attribute.ConstructorArguments)
                    {
                        string fieldDelim = getFieldDelim(argument.ArgumentType);

                        if (argumentIndex > 0)
                        {
                            attributeSig += ", ";
                        }

                        attributeSig += fieldDelim;
                        if (argument.ArgumentType.IsEnum)
                        {
                            if (argument.Value != null)
                            {
                                attributeSig += formatTypeName(argument.Value.GetType()) + "." + argument.Value;
                            }
                        }
                        else if (argument.ArgumentType == typeof(bool))
                        {
                            attributeSig += argument.Value.ToString().ToLower();
                        }
                        else
                        {
                            if (attribute.Constructor.DeclaringType == typeof(AssemblyDescriptionAttribute))
                            {
                                attributeSig += assemblyDescriptionAttributeMarkup;
                                hasDescriptionAttribute = true;
                            }

                            attributeSig += argument.Value;
                        }

                        attributeSig += fieldDelim;
                        argumentIndex++;
                    }

                    attributeSig += ")";
                }

                attributeSig += "]";
                output(attributeSig);
            }

            if (!hasDescriptionAttribute)
            {
                output("[assembly: System.Reflection.AssemblyDescriptionAttribute(\""
                       + assemblyDescriptionAttributeMarkup + "\")]");
            }
        }

        /// <summary>
        /// Load the dependent assemblies of the reflected assembly.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        protected Assembly reflectionOnlyAssemblyResolveHandler(object sender, ResolveEventArgs args)
        {
            // Load the assembly by assembly name so we can find its physical location.
            Assembly assembly = Assembly.Load(args.Name);
            // Load the assembly in reflection only mode.
            Assembly reflectedAssembly = Assembly.ReflectionOnlyLoadFrom(assembly.Location);
            return reflectedAssembly;
        }

        /// <summary>
        /// Generate the doppleganger interface for the assembly.
        /// </summary>
        /// <param name="typelibName"></param>
        public void generate(string typelibName)
        {
            Assembly importlib = Assembly.ReflectionOnlyLoadFrom(typelibName);

            // Capture any assembly resolve problems and dynamically load the dependent assembly.
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += reflectionOnlyAssemblyResolveHandler;

            output("/*");
            output(" * Licensed to the Apache Software Foundation (ASF) under one or more");
            output(" * contributor license agreements.  See the NOTICE file distributed with");
            output(" * this work for additional information regarding copyright ownership.");
            output(" * The ASF licenses this file to You under the Apache License, Version 2.0");
            output(" * (the \"License\"); you may not use this file except in compliance with");
            output(" * the License.  You may obtain a copy of the License at");
            output(" *");
            output(" *     http://www.apache.org/licenses/LICENSE-2.0");
            output(" *");
            output(" * Unless required by applicable law or agreed to in writing, software");
            output(" * distributed under the License is distributed on an \"AS IS\" BASIS,");
            output(" * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.");
            output(" * See the License for the specific language governing permissions and");
            output(" * limitations under the License.");
            output(" */");

            output("/*");
            output(" * This file was auto-generated by Doppleganger.");
            output(" * Feel free to modify this file as necessary, but any changes");
            output(" * may be lost when it is regenerated.");
            output(" *");
            output(" * See http://code.google.com/p/doppleganger/ for more information.");
            output(" */");

            generateAssemblyInfo(importlib);
            generateTypes(importlib);
        }
    }
}
