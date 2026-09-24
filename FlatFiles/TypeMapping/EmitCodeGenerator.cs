using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using FlatFiles.Properties;

namespace FlatFiles.TypeMapping
{
    internal sealed class EmitCodeGenerator : ICodeGenerator
    {
        private readonly ConcurrentDictionary<string, int> nameLookup = new();
        private readonly ModuleBuilder moduleBuilder;

        public EmitCodeGenerator()
        {
            var assemblyName = new AssemblyName( "FlatFiles.DynamicAssembly" );
            var flatFilesAssembly = typeof( ICodeGenerator ).GetTypeInfo().Assembly;
            var flatFilesAssemblyName = flatFilesAssembly.GetName();
            var publicKey = flatFilesAssemblyName.GetPublicKey();
            assemblyName.SetPublicKey( publicKey );
            var publicKeyToken = flatFilesAssemblyName.GetPublicKeyToken();
            assemblyName.SetPublicKeyToken( publicKeyToken );
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.RunAndCollect );
            moduleBuilder = assemblyBuilder.DefineDynamicModule( "FlatFiles_DynamicModule" );
        }

        /// <summary>
        ///     How many times a generator has been asked to prepare something: a factory, a constructor, a
        ///     deserialiser or a serialiser. Each one defines a type in the dynamic module, so what a read asks
        ///     for once is cheap and what it asks for per record is not - 8.1.0 shipped a factory built per
        ///     record, which every behavioural test passed. The tests read this either side of a read and hold
        ///     the count to what a read prepares rather than to what it reads.
        /// </summary>
        internal static int Preparations => preparations;

        private static int preparations;

        private string GetUniqueTypeName( string name )
        {
            var id = nameLookup.AddOrUpdate( name, 0, ( _, old ) => old + 1 );
            return $"{name}_{id}";
        }

        public Func<TEntity> GetFactory<TEntity>()
        {
            Interlocked.Increment( ref preparations );
            var entityType = typeof( TEntity );
            // The caller has already established there is one: a type without a parameterless constructor is
            // built through the constructor it does have, and never arrives here.
            var constructorInfo = MemberAccessorBuilder.GetConstructor<TEntity>( Type.EmptyTypes )!;
            if (!constructorInfo.IsPublic)
            {
                // Emitted code is not the type's friend, so a constructor it keeps to itself has to be reached
                // reflectively. Slower per entity, and only for a type that asked for it by hiding its
                // constructor.
                return () => (TEntity) constructorInfo.Invoke( null );
            }
            var typeName = GetUniqueTypeName( $"{entityType.Name}Factory" );
            var typeBuilder = moduleBuilder.DefineType( typeName, TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed );
            var methodBuilder = typeBuilder.DefineMethod( "Create", MethodAttributes.Public | MethodAttributes.Static, entityType, Type.EmptyTypes );
            var generator = methodBuilder.GetILGenerator();
            generator.Emit( OpCodes.Newobj, constructorInfo );
            generator.Emit( OpCodes.Ret );
            var typeInfo = typeBuilder.CreateTypeInfo();
            var createInfo = typeInfo.GetMethod( methodBuilder.Name )!;
            return createInfo.CreateDelegate<Func<TEntity>>();
        }

        /// <summary>
        ///     Emits a method that reads the parameters out of the parsed values and calls the constructor with
        ///     them, so a type that must be built complete costs one call rather than a reflective invoke and an
        ///     array of arguments for every record.
        /// </summary>
        public Func<object?[], TEntity> GetConstructor<TEntity>( ConstructorMapping mapping )
        {
            Interlocked.Increment( ref preparations );
            var entityType = typeof( TEntity );
            var typeName = GetUniqueTypeName( $"{entityType.Name}Constructor" );
            var typeBuilder = moduleBuilder.DefineType( typeName, TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed );
            var methodBuilder = typeBuilder.DefineMethod( "Create", MethodAttributes.Public | MethodAttributes.Static, entityType, [typeof( object?[] )] );
            var generator = methodBuilder.GetILGenerator();

            var parameters = mapping.Constructor.GetParameters();
            for (var index = 0; index != parameters.Length; ++index)
            {
                generator.Emit( OpCodes.Ldarg_0 );
                generator.Emit( OpCodes.Ldc_I4, mapping.LogicalIndexes[index] );
                generator.Emit( OpCodes.Ldelem_Ref );
                var parameterType = parameters[index].ParameterType;
                // A value type arrives boxed and has to be taken out of its box; a reference type only has to be
                // proved to be what the constructor was promised.
                generator.Emit( parameterType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, parameterType );
            }
            generator.Emit( OpCodes.Newobj, mapping.Constructor );
            generator.Emit( OpCodes.Ret );

            var typeInfo = typeBuilder.CreateTypeInfo();
            var createInfo = typeInfo.GetMethod( methodBuilder.Name )!;
            return createInfo.CreateDelegate<Func<object?[], TEntity>>();
        }

        public Action<IRecordContext, TEntity, object?[]> GetReader<TEntity>( IMemberMapping[] mappings )
        {
            Interlocked.Increment( ref preparations );
            var entityType = typeof( TEntity );
            var typeName = GetUniqueTypeName( $"{entityType.Name}Reader" );
            var typeBuilder = moduleBuilder.DefineType( typeName, TypeAttributes.Public | TypeAttributes.Sealed );
            var fieldBuilder = typeBuilder.DefineField( "mappings", typeof( IMemberMapping[] ), FieldAttributes.Private );
            var ctorBuilder = typeBuilder.DefineConstructor( MethodAttributes.Public, CallingConventions.Standard, [typeof( IMemberMapping[] )] );
            var ctorGenerator = ctorBuilder.GetILGenerator();
            ctorGenerator.Emit( OpCodes.Ldarg_0 );
            ctorGenerator.Emit( OpCodes.Ldarg_1 );
            ctorGenerator.Emit( OpCodes.Stfld, fieldBuilder );
            ctorGenerator.Emit( OpCodes.Ret );

            var methodBuilder = typeBuilder.DefineMethod( "Read", MethodAttributes.Public, null, [typeof( IRecordContext ), entityType, typeof( object[] )] );
            var methodGenerator = methodBuilder.GetILGenerator();
            for (var index = 0; index != mappings.Length; ++index)
            {
                var mapping = mappings[index];
                if (mapping.Member is not null)
                {
                    EmitMemberRead( methodGenerator, mapping.Member, mapping.LogicalIndex );
                }
                else if (mapping.Reader is not null)
                {
                    EmitCustomRead( methodGenerator, fieldBuilder, index );
                }
            }
            methodGenerator.Emit( OpCodes.Ret );

            var typeInfo = typeBuilder.CreateTypeInfo();
            var instance = Activator.CreateInstance( typeInfo.AsType(), (object) mappings );
            var readInfo = typeInfo.GetMethod( methodBuilder.Name )!;
            return readInfo.CreateDelegate<Action<IRecordContext, TEntity, object?[]>>( instance );
        }

        private static void EmitMemberRead( ILGenerator generator, IMemberAccessor member, int logicalIndex )
        {
            generator.Emit( OpCodes.Ldarg_2 );
            generator.Emit( OpCodes.Ldarg_3 );
            generator.Emit( OpCodes.Ldc_I4, logicalIndex );
            generator.Emit( OpCodes.Ldelem_Ref );

            switch (member.MemberInfo)
            {
                case FieldInfo fieldInfo:
                    generator.Emit( OpCodes.Unbox_Any, fieldInfo.FieldType );
                    generator.Emit( OpCodes.Stfld, fieldInfo );
                    break;
                case PropertyInfo propertyInfo:
                    var setter = propertyInfo.GetSetMethod( true );
                    if (setter is null)
                    {
                        var message = string.Format( CultureInfo.CurrentCulture, Resources.ReadOnlyProperty, propertyInfo.Name );
                        throw new FlatFileException( message );
                    }
                    generator.Emit( OpCodes.Unbox_Any, propertyInfo.PropertyType );
                    generator.Emit( OpCodes.Callvirt, setter );
                    break;
            }
        }

        private static void EmitCustomRead( ILGenerator generator, FieldInfo fieldInfo, int mappingIndex )
        {
            var contextBuilder = generator.DeclareLocal( typeof( ColumnContext ) );
            var mappingBuilder = generator.DeclareLocal( typeof( IMemberMapping ) );
            var indexBuilder = generator.DeclareLocal( typeof( int ) );

            // Gets the mapping being processed
            generator.Emit( OpCodes.Ldarg_0 );
            generator.Emit( OpCodes.Ldfld, fieldInfo );
            generator.Emit( OpCodes.Ldc_I4, mappingIndex );
            generator.Emit( OpCodes.Ldelem_Ref );
            generator.Emit( OpCodes.Stloc, mappingBuilder );

            // Create the Context, passing in RecordContext, physical index and logical index
            generator.Emit( OpCodes.Ldarg_1 );

            generator.Emit( OpCodes.Ldloc, mappingBuilder );
            var physicalIndexGetInfo = MemberAccessorBuilder.GetProperty<IMemberMapping, int>( x => x.PhysicalIndex )!;
            var physicalIndexGetter = physicalIndexGetInfo.GetGetMethod()!;
            generator.Emit( OpCodes.Callvirt, physicalIndexGetter );

            // Set ColumnContext.LogicalIndex
            generator.Emit( OpCodes.Ldloc, mappingBuilder );
            var logicalIndexGetInfo = MemberAccessorBuilder.GetProperty<IMemberMapping, int>( x => x.LogicalIndex )!;
            var logicalIndexGetter = logicalIndexGetInfo.GetGetMethod()!;
            generator.Emit( OpCodes.Callvirt, logicalIndexGetter );
            generator.Emit( OpCodes.Dup );
            generator.Emit( OpCodes.Stloc, indexBuilder );

            var contextCtorInfo = MemberAccessorBuilder.GetConstructor<ColumnContext>( typeof( IRecordContext ), typeof( int ), typeof( int ) )!;
            generator.Emit( OpCodes.Newobj, contextCtorInfo );
            generator.Emit( OpCodes.Stloc, contextBuilder );

            // Get the reader
            generator.Emit( OpCodes.Ldloc, mappingBuilder );
            var readerGetInfo = MemberAccessorBuilder.GetProperty<IMemberMapping, Action<IColumnContext?, object?, object?>?>( x => x.Reader )!;
            var readerGetter = readerGetInfo.GetGetMethod()!;
            generator.Emit( OpCodes.Callvirt, readerGetter );

            // Load the parameters
            generator.Emit( OpCodes.Ldloc, contextBuilder );
            generator.Emit( OpCodes.Ldarg_2 );
            generator.Emit( OpCodes.Ldarg_3 );
            generator.Emit( OpCodes.Ldloc, indexBuilder );
            generator.Emit( OpCodes.Ldelem_Ref );

            // Invoke the reader
            var invokeInfo = MemberAccessorBuilder.GetMethod<Action<IColumnContext?, object?, object?>>( x => x.Invoke( null, null, null ) )!;
            generator.Emit( OpCodes.Callvirt, invokeInfo );
        }

        public Action<IRecordContext, TEntity, object?[]> GetWriter<TEntity>( IMemberMapping[] mappings )
        {
            Interlocked.Increment( ref preparations );
            var entityType = typeof( TEntity );
            var typeName = GetUniqueTypeName( $"{entityType.Name}Writer" );
            var typeBuilder = moduleBuilder.DefineType( typeName, TypeAttributes.Public | TypeAttributes.Sealed );
            var fieldBuilder = typeBuilder.DefineField( "mappings", typeof( IMemberMapping[] ), FieldAttributes.Private );
            var ctorBuilder = typeBuilder.DefineConstructor( MethodAttributes.Public, CallingConventions.Standard, [typeof( IMemberMapping[] )] );
            var ctorGenerator = ctorBuilder.GetILGenerator();
            ctorGenerator.Emit( OpCodes.Ldarg_0 );
            ctorGenerator.Emit( OpCodes.Ldarg_1 );
            ctorGenerator.Emit( OpCodes.Stfld, fieldBuilder );
            ctorGenerator.Emit( OpCodes.Ret );

            var methodBuilder = typeBuilder.DefineMethod( "Write", MethodAttributes.Public, null, [typeof( IRecordContext ), entityType, typeof( object[] )] );
            var methodGenerator = methodBuilder.GetILGenerator();
            for (var index = 0; index != mappings.Length; ++index)
            {
                var mapping = mappings[index];
                if (mapping.Member is not null)
                {
                    EmitMemberWrite( methodGenerator, mapping.Member, mapping.LogicalIndex );
                }
                else if (mapping.Writer is not null)
                {
                    EmitCustomWrite( methodGenerator, fieldBuilder, index );
                }
            }
            methodGenerator.Emit( OpCodes.Ret );

            var typeInfo = typeBuilder.CreateTypeInfo();
            var instance = Activator.CreateInstance( typeInfo.AsType(), (object) mappings );
            var writeMethodInfo = typeInfo.GetMethod( methodBuilder.Name )!;
            return writeMethodInfo.CreateDelegate<Action<IRecordContext, TEntity, object?[]>>( instance );
        }

        private static void EmitMemberWrite( ILGenerator generator, IMemberAccessor member, int logicalIndex )
        {
            generator.Emit( OpCodes.Ldarg_3 );
            generator.Emit( OpCodes.Ldc_I4, logicalIndex );
            generator.Emit( OpCodes.Ldarg_2 );

            switch (member.MemberInfo)
            {
                case FieldInfo fieldInfo:
                    generator.Emit( OpCodes.Ldfld, fieldInfo );
                    var fieldType = fieldInfo.FieldType;
                    if (!fieldType.GetTypeInfo().IsClass)
                    {
                        generator.Emit( OpCodes.Box, fieldType );
                    }
                    break;
                case PropertyInfo propertyInfo:
                    var getter = propertyInfo.GetGetMethod( true );
                    if (getter is null)
                    {
                        var message = string.Format( CultureInfo.CurrentCulture, Resources.WriteOnlyProperty, propertyInfo.Name );
                        throw new FlatFileException( message );
                    }
                    generator.Emit( OpCodes.Callvirt, getter );
                    var propertyType = propertyInfo.PropertyType;
                    if (!propertyType.GetTypeInfo().IsClass)
                    {
                        generator.Emit( OpCodes.Box, propertyType );
                    }
                    break;
            }
            generator.Emit( OpCodes.Stelem_Ref );
        }

        private static void EmitCustomWrite( ILGenerator generator, FieldInfo fieldInfo, int mappingIndex )
        {
            var contextBuilder = generator.DeclareLocal( typeof( ColumnContext ) );
            var mappingBuilder = generator.DeclareLocal( typeof( IMemberMapping ) );

            // Gets the mapping being processed
            generator.Emit( OpCodes.Ldarg_0 );
            generator.Emit( OpCodes.Ldfld, fieldInfo );
            generator.Emit( OpCodes.Ldc_I4, mappingIndex );
            generator.Emit( OpCodes.Ldelem_Ref );
            generator.Emit( OpCodes.Stloc, mappingBuilder );

            // Create the ColumnContext, passing RecordContext, physical index and logical index
            generator.Emit( OpCodes.Ldarg_1 );

            // Set ColumnContext.PhysicalIndex
            generator.Emit( OpCodes.Ldloc, mappingBuilder );
            var physicalIndexGetInfo = MemberAccessorBuilder.GetProperty<IMemberMapping, int>( x => x.PhysicalIndex )!;
            var physicalIndexGetter = physicalIndexGetInfo.GetGetMethod()!;
            generator.Emit( OpCodes.Callvirt, physicalIndexGetter );

            // Set ColumnContext.LogicalIndex
            generator.Emit( OpCodes.Ldloc, mappingBuilder );
            var logicalIndexGetInfo = MemberAccessorBuilder.GetProperty<IMemberMapping, int>( x => x.LogicalIndex )!;
            var logicalIndexGetter = logicalIndexGetInfo.GetGetMethod()!;
            generator.Emit( OpCodes.Callvirt, logicalIndexGetter );

            var contextCtorInfo = MemberAccessorBuilder.GetConstructor<ColumnContext>( typeof( IRecordContext ), typeof( int ), typeof( int ) )!;
            generator.Emit( OpCodes.Newobj, contextCtorInfo );
            generator.Emit( OpCodes.Stloc, contextBuilder );

            // Get the writer
            generator.Emit( OpCodes.Ldloc, mappingBuilder );
            var writerInfo = MemberAccessorBuilder.GetProperty<IMemberMapping, Action<IColumnContext?, object?, object?[]>?>( x => x.Writer )!;
            var writerGetter = writerInfo.GetGetMethod()!;
            generator.Emit( OpCodes.Callvirt, writerGetter );

            // Load the parameters
            generator.Emit( OpCodes.Ldloc, contextBuilder );
            generator.Emit( OpCodes.Ldarg_2 );
            generator.Emit( OpCodes.Ldarg_3 );

            // Invoke the writer
            var invokeInfo = MemberAccessorBuilder.GetMethod<Action<IColumnContext?, object?, object?>>( x => x.Invoke( null, null, null ) )!;
            generator.Emit( OpCodes.Callvirt, invokeInfo );
        }
    }
}
