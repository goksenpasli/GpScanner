using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;

namespace Tesseract.Internal.InteropDotNet
{
    internal static class InteropRuntimeImplementer
    {
        public static T CreateInstance<T>() where T : class
        {
            Type interfaceType = typeof(T);
            if(!typeof(T).IsInterface)
            {
                throw new Exception($"The type {interfaceType.Name} should be an interface");
            }

            if(!interfaceType.IsPublic)
            {
                throw new Exception($"The interface {interfaceType.Name} should be public");
            }

            string assemblyName = GetAssemblyName(interfaceType);

            AssemblyBuilder assemblyBuilder = Thread.GetDomain().DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);

            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);

            string typeName = GetImplementationTypeName(assemblyName, interfaceType);
            TypeBuilder typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public, typeof(object), new[] { interfaceType });
            MethodItem[] methods = BuildMethods(interfaceType);

            ImplementDelegates(assemblyName, moduleBuilder, methods);
            ImplementFields(typeBuilder, methods);
            ImplementMethods(typeBuilder, methods);
            ImplementConstructor(typeBuilder, methods);

            Type implementationType = typeBuilder.CreateType();
            return (T)Activator.CreateInstance(implementationType, LibraryLoader.Instance);
        }

        #region Main steps
        private static MethodItem[] BuildMethods(Type interfaceType)
        {
            MethodInfo[] methodInfoArray = interfaceType.GetMethods();
            MethodItem[] methods = new MethodItem[methodInfoArray.Length];
            for(int i = 0; i < methodInfoArray.Length; i++)
            {
                methods[i] = new MethodItem
                {
                    Info = methodInfoArray[i],
                    DllImportAttribute =
                        GetRuntimeDllImportAttribute(methodInfoArray[i]) ??
                            throw new Exception(
                                $"Method '{methodInfoArray[i].Name}' of interface '{interfaceType.Name}' should be marked with the RuntimeDllImport attribute")
                };
            }

            return methods;
        }

        private static void ImplementConstructor(TypeBuilder typeBuilder, MethodItem[] methods)
        {
            ConstructorBuilder ctorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                new[] { typeof(LibraryLoader) });
            _ = ctorBuilder.DefineParameter(1, ParameterAttributes.HasDefault, "loader");
            if(typeBuilder.BaseType == null)
            {
                throw new Exception("There is no a BaseType of typeBuilder");
            }

            ConstructorInfo baseCtor = typeBuilder.BaseType.GetConstructor(new Type[0]) ??
                throw new Exception("There is no a default constructor of BaseType of typeBuilder");

            List<string> libraries = new List<string>();
            foreach(MethodItem method in methods)
            {
                string libraryName = method.DllImportAttribute.LibraryFileName;
                if(!libraries.Contains(libraryName))
                {
                    libraries.Add(libraryName);
                }
            }

            ILGenerator ilGen = ctorBuilder.GetILGenerator();

            for(int i = 0; i < libraries.Count; i++)
            {
                _ = ilGen.DeclareLocal(typeof(IntPtr));
            }

            _ = ilGen.DeclareLocal(typeof(IntPtr));

            ilGen.Emit(OpCodes.Ldarg_0);

            ilGen.Emit(OpCodes.Call, baseCtor);
            for(int i = 0; i < libraries.Count; i++)
            {
                string library = libraries[i];

                ilGen.Emit(OpCodes.Ldarg_1);

                ilGen.Emit(OpCodes.Ldstr, library);

                ilGen.Emit(OpCodes.Ldnull);

                ilGen.Emit(OpCodes.Callvirt, typeof(LibraryLoader).GetMethod("LoadLibrary"));

                ilGen.Emit(OpCodes.Stloc, i);
            }

            foreach(MethodItem method in methods)
            {
                int libraryIndex = libraries.IndexOf(method.DllImportAttribute.LibraryFileName);
                string methodName = method.DllImportAttribute.EntryPoint ?? method.Info.Name;

                ilGen.Emit(OpCodes.Ldarg_1);

                ilGen.Emit(OpCodes.Ldloc, libraryIndex);

                ilGen.Emit(OpCodes.Ldstr, methodName);

                ilGen.Emit(OpCodes.Callvirt, typeof(LibraryLoader).GetMethod("GetProcAddress"));

                ilGen.Emit(OpCodes.Stloc, libraries.Count);

                ilGen.Emit(OpCodes.Ldarg_0);

                ilGen.Emit(OpCodes.Ldloc_1);

                ilGen.Emit(OpCodes.Ldtoken, method.DelegateType);

                ilGen.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));

                ilGen.Emit(OpCodes.Call, typeof(Marshal).GetMethod("GetDelegateForFunctionPointer", new[] { typeof(IntPtr), typeof(Type) }));

                ilGen.Emit(OpCodes.Castclass, method.DelegateType);

                ilGen.Emit(OpCodes.Stfld, method.FieldInfo);
            }

            ilGen.Emit(OpCodes.Ret);
        }

        private static void ImplementDelegates(string assemblyName, ModuleBuilder moduleBuilder, IEnumerable<MethodItem> methods)
        {
            foreach(MethodItem method in methods)
            {
                method.DelegateType = ImplementMethodDelegate(assemblyName, moduleBuilder, method);
            }
        }

        private static void ImplementFields(TypeBuilder typeBuilder, IEnumerable<MethodItem> methods)
        {
            foreach(MethodItem method in methods)
            {
                string fieldName = $"{method.Info.Name}Field";
                method.FieldInfo = typeBuilder.DefineField(fieldName, method.DelegateType, FieldAttributes.Private);
            }
        }

        private static Type ImplementMethodDelegate(string assemblyName, ModuleBuilder moduleBuilder, MethodItem method)
        {
            const MethodAttributes methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual;

            string delegateName = GetDelegateName(assemblyName, method.Info);
            TypeBuilder delegateBuilder = moduleBuilder.DefineType(
                delegateName,
                TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.Sealed,
                typeof(MulticastDelegate));

            RuntimeDllImportAttribute importAttribute = method.DllImportAttribute;
            ConstructorInfo attributeCtor =
                typeof(UnmanagedFunctionPointerAttribute).GetConstructor(new[] { typeof(CallingConvention) }) ??
                throw new Exception("There is no the target constructor of the UnmanagedFunctionPointerAttribute");
            CustomAttributeBuilder attributeBuilder = new CustomAttributeBuilder(
                attributeCtor,
                new object[] { importAttribute.CallingConvention },
                new[]
                {
                    typeof(UnmanagedFunctionPointerAttribute).GetField("CharSet"),
                    typeof(UnmanagedFunctionPointerAttribute).GetField("BestFitMapping"),
                    typeof(UnmanagedFunctionPointerAttribute).GetField("ThrowOnUnmappableChar"),
                    typeof(UnmanagedFunctionPointerAttribute).GetField("SetLastError")
                },
                new object[] { importAttribute.CharSet, importAttribute.BestFitMapping, importAttribute.ThrowOnUnmappableChar, importAttribute.SetLastError });
            delegateBuilder.SetCustomAttribute(attributeBuilder);

            ConstructorBuilder ctorBuilder = delegateBuilder.DefineConstructor(
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                new[] { typeof(object), typeof(IntPtr) });
            ctorBuilder.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);
            _ = ctorBuilder.DefineParameter(1, ParameterAttributes.HasDefault, "object");
            _ = ctorBuilder.DefineParameter(2, ParameterAttributes.HasDefault, "method");

            LightParameterInfo[] parameters = GetParameterInfoArray(method.Info);
            MethodBuilder methodBuilder =
                DefineMethod(delegateBuilder, "Invoke", methodAttributes, method.ReturnType, parameters);
            methodBuilder.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

            parameters = GetParameterInfoArray(method.Info, InfoArrayMode.BeginInvoke);
            methodBuilder = DefineMethod(delegateBuilder, "BeginInvoke", methodAttributes, typeof(IAsyncResult), parameters);
            methodBuilder.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

            parameters = GetParameterInfoArray(method.Info, InfoArrayMode.EndInvoke);
            methodBuilder = DefineMethod(delegateBuilder, "EndInvoke", methodAttributes, method.ReturnType, parameters);
            methodBuilder.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

            return delegateBuilder.CreateType();
        }

        private static void ImplementMethods(TypeBuilder typeBuilder, IEnumerable<MethodItem> methods)
        {
            foreach(MethodItem method in methods)
            {
                LightParameterInfo[] infoArray = GetParameterInfoArray(method.Info);
                MethodBuilder methodBuilder = DefineMethod(
                    typeBuilder,
                    method.Name,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final | MethodAttributes.Virtual,
                    method.ReturnType,
                    infoArray);

                ILGenerator ilGen = methodBuilder.GetILGenerator();

                ilGen.Emit(OpCodes.Ldarg_0);

                ilGen.Emit(OpCodes.Ldfld, method.FieldInfo);

                for(int i = 0; i < infoArray.Length; i++)
                {
                    LdArg(ilGen, i + 1);
                }

                ilGen.Emit(OpCodes.Callvirt, method.DelegateType.GetMethod("Invoke"));

                ilGen.Emit(OpCodes.Ret);

                typeBuilder.DefineMethodOverride(methodBuilder, method.Info);
            }
        }
        #endregion Main steps

        #region Reflection and emit helpers
        private static MethodBuilder DefineMethod(
            TypeBuilder typeBuilder,
            string name,
            MethodAttributes attributes,
            Type returnType,
            LightParameterInfo[] infoArray)
        {
            MethodBuilder methodBuilder =
                typeBuilder.DefineMethod(name, attributes, returnType, GetParameterTypeArray(infoArray));
            for(int parameterIndex = 0; parameterIndex < infoArray.Length; parameterIndex++)
            {
                _ = methodBuilder.DefineParameter(parameterIndex + 1, infoArray[parameterIndex].Attributes, infoArray[parameterIndex].Name);
            }

            return methodBuilder;
        }

        private static RuntimeDllImportAttribute GetRuntimeDllImportAttribute(MethodInfo methodInfo)
        {
            object[] attributes = methodInfo.GetCustomAttributes(typeof(RuntimeDllImportAttribute), true);
            return attributes.Length == 0
                ? throw new Exception($"RuntimeDllImportAttribute for method '{methodInfo.Name}' not found")
                : (RuntimeDllImportAttribute)attributes[0];
        }

        private static void LdArg(ILGenerator ilGen, int index)
        {
            switch(index)
            {
                case 0:
                    ilGen.Emit(OpCodes.Ldarg_0);
                    break;

                case 1:
                    ilGen.Emit(OpCodes.Ldarg_1);
                    break;

                case 2:
                    ilGen.Emit(OpCodes.Ldarg_2);
                    break;

                case 3:
                    ilGen.Emit(OpCodes.Ldarg_3);
                    break;

                default:
                    ilGen.Emit(OpCodes.Ldarg, index);
                    break;
            }
        }
        #endregion Reflection and emit helpers

        #region Method helpers
        private enum InfoArrayMode
        {
            Invoke,

            BeginInvoke,

            EndInvoke
        }

        private static LightParameterInfo[] GetParameterInfoArray(MethodInfo methodInfo, InfoArrayMode mode = InfoArrayMode.Invoke)
        {
            ParameterInfo[] parameters = methodInfo.GetParameters();
            List<LightParameterInfo> infoList = new List<LightParameterInfo>();
            for(int i = 0; i < parameters.Length; i++)
            {
                if(mode != InfoArrayMode.EndInvoke || parameters[i].ParameterType.IsByRef)
                {
                    infoList.Add(new LightParameterInfo(parameters[i]));
                }
            }

            if(mode == InfoArrayMode.BeginInvoke)
            {
                infoList.Add(new LightParameterInfo(typeof(AsyncCallback), "callback"));
                infoList.Add(new LightParameterInfo(typeof(object), "object"));
            }

            if(mode == InfoArrayMode.EndInvoke)
            {
                infoList.Add(new LightParameterInfo(typeof(IAsyncResult), "result"));
            }

            LightParameterInfo[] infoArray = new LightParameterInfo[infoList.Count];
            for(int i = 0; i < infoList.Count; i++)
            {
                infoArray[i] = infoList[i];
            }

            return infoArray;
        }

        private static Type[] GetParameterTypeArray(LightParameterInfo[] infoArray)
        {
            Type[] typeArray = new Type[infoArray.Length];
            for(int i = 0; i < infoArray.Length; i++)
            {
                typeArray[i] = infoArray[i].Type;
            }

            return typeArray;
        }

        private class LightParameterInfo
        {
            public LightParameterInfo(ParameterInfo info)
            {
                Type = info.ParameterType;
                Name = info.Name;
                Attributes = info.Attributes;
            }

            public LightParameterInfo(Type type, string name)
            {
                Type = type;
                Name = name;
                Attributes = ParameterAttributes.HasDefault;
            }

            public ParameterAttributes Attributes { get; }

            public string Name { get; }

            public Type Type { get; }
        }

        private class MethodItem
        {
            public Type DelegateType { get; set; }

            public RuntimeDllImportAttribute DllImportAttribute { get; set; }

            public FieldInfo FieldInfo { get; set; }

            public MethodInfo Info { get; set; }

            public string Name => Info.Name;

            public Type ReturnType => Info.ReturnType;
        }
        #endregion Method helpers

        #region Name helpers
        private static string GetAssemblyName(Type interfaceType) { return $"InteropRuntimeImplementer.{GetSubstantialName(interfaceType)}Instance"; }

        private static string GetDelegateName(string assemblyName, MethodInfo methodInfo) { return $"{assemblyName}.{methodInfo.Name}Delegate"; }

        private static string GetImplementationTypeName(string assemblyName, Type interfaceType)
        { return $"{assemblyName}.{GetSubstantialName(interfaceType)}Implementation"; }

        private static string GetSubstantialName(Type interfaceType)
        {
            string name = interfaceType.Name;
            if(name.StartsWith("I"))
            {
                name = name.Substring(1);
            }

            return name;
        }
        #endregion Name helpers
    }
}