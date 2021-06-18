using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DAL.Helpers
{
    /// <summary>
    /// Helpers to automate some EF configuring processes
    /// </summary>
    internal static class EFCoreHelpers
    {
        private static readonly ConcurrentDictionary<Type, Action<ModelBuilder>> _cachedActions = 
            new ConcurrentDictionary<Type, Action<ModelBuilder>>();

        private static Type[] GetDbContextSetTypes(Type type)
        {
            return type.GetProperties()
                .Where
                (
                    e =>
                        e.CanRead && e.CanWrite
                            &&
                        e.PropertyType.IsGenericType
                            &&
                        e.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>)
                )
                .Select(e => e.PropertyType.GetGenericArguments().First())
                .ToArray();
        }

        private static MethodInfo GetMethodInfo_ApplyConfiguration()
        {
            var methods = typeof(ModelBuilder).GetMethods();

            int methodsLength = methods.Length;
            for (int methodsIndex = 0; methodsIndex < methodsLength; ++methodsIndex)
            {
                var method = methods[methodsIndex];

                if (method.Name != nameof(ModelBuilder.ApplyConfiguration) || !method.IsGenericMethod)
                    continue;

                var genericArguments = method.GetGenericArguments();

                if (genericArguments.Length != 1)
                    continue;

                var parameters = method.GetParameters();

                if (parameters.Length != 1)
                    continue;

                var dynamicEntityType = genericArguments[0];
                var expectedParameterType = typeof(IEntityTypeConfiguration<>).MakeGenericType(dynamicEntityType);

                var actualParameterType = parameters[0].ParameterType;

                if (expectedParameterType != actualParameterType)
                    continue;

                return method;
            }

            return null;
        }

        private static Action<ModelBuilder> CreateDelegate_OnModelCreating(Type type)
        {
            var parameter = Expression.Parameter(typeof(ModelBuilder));

            //Getting method info of ModelBuilder.ApplyConfiguration<TEntity>
            var applyConfiguration = GetMethodInfo_ApplyConfiguration();

            //Getting all model types associated with current context
            var modelTypes = GetDbContextSetTypes(type);

            //Getting all types that implement IEntityTypeConfiguration
            //Checking if all of them have default public constructor
            //Creating expressions for each of type: modelBuilder.ApplyConfiguration(new Configuration());
            var expressions = type.Assembly.GetTypes()
                .Where(type => !type.IsAbstract)
                .Select
                (
                    type => new
                    {
                        ImplementedEntityTypesConfiguration = type.GetInterfaces()
                            .Where
                            (
                                interfaceType =>
                                    interfaceType.IsGenericType
                                        &&
                                    interfaceType.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)
                            )
                            .Select(interfaceType => interfaceType.GetGenericArguments().First())
                            .Where(modelType => modelTypes.Contains(modelType)),

                        Constructor = type.GetConstructor(Type.EmptyTypes)
                    }
                )
                .Where
                (
                    typeConfig =>
                        typeConfig.ImplementedEntityTypesConfiguration.Any()
                            &&
                        //Should we throw this one? Or just skip the types which doesn't have public default constructor?
                        (typeConfig.Constructor != null ? true : throw new InvalidOperationException($"Entity type configuration doesn't have default public constructor! {typeConfig.Constructor.DeclaringType.FullName}"))
                )
                .SelectMany
                (
                    typeConfig => typeConfig.ImplementedEntityTypesConfiguration
                        .Select
                        (
                            entityType =>
                            {
                                var injectedMembers = typeConfig.Constructor.DeclaringType
                                    .GetMembers()
                                    .Select
                                    (
                                        e => e switch
                                        {
                                            FieldInfo field =>
                                            (
                                                field.IsPublic && !field.IsInitOnly
                                                    &&
                                                //Currently only ModelBuilder is supported for injection
                                                field.FieldType == typeof(ModelBuilder)
                                            )
                                            ? e : null,
                                            PropertyInfo property =>
                                            (
                                                property.CanWrite && property.CanRead
                                                    &&
                                                property.GetMethod.IsPublic && property.SetMethod.IsPublic
                                                    &&
                                                //Currently only ModelBuilder is supported for injection
                                                property.PropertyType == typeof(ModelBuilder)
                                            )
                                            ? e : null,
                                            _ => null
                                        }
                                    )
                                    .Where(e => e != null)
                                    .ToArray();

                                Expression configCreator = Expression.New(typeConfig.Constructor);

                                //If there are some injected properties
                                if (injectedMembers.Any())
                                {
                                    configCreator = Expression.MemberInit
                                    (
                                        configCreator as NewExpression,
                                        injectedMembers.Select(e => Expression.Bind(e, parameter))
                                    );
                                }

                                return Expression.Call
                                (
                                    parameter,
                                    applyConfiguration.MakeGenericMethod(entityType),
                                    configCreator
                                );
                            }
                        )
                )
                .ToArray();

            //Combining those expressions into a block, and receiving lambda
            var lambda = Expression.Lambda<Action<ModelBuilder>>(Expression.Block(expressions), parameter);

            return lambda.Compile();
        }

        /// <summary>
        /// This method is scanning for <see cref="IEntityTypeConfiguration{TEntity}"/>
        /// and applying those to the <see cref="ModelBuilder"/> for specific context type.
        /// This one should be called at <see cref="DbContext.OnModelCreating(ModelBuilder)"/>
        /// </summary>
        /// <remarks>
        /// Under the hood its retreiving all <see cref="IEntityTypeConfiguration{TEntity}"/> for all
        /// registered models (those which are defined with <see cref="DbSet{TEntity}"/>, others are ignored) 
        /// at specified context type, and applying those configs to the <see cref="ModelBuilder"/>
        /// </remarks>
        /// <typeparam name="TContext"></typeparam>
        /// <returns></returns>
        public static void ApplyEntityTypeConfigurations<TContext>(ModelBuilder modelBuilder)
            where TContext : DbContext
        {
            var applyConfigurations = _cachedActions.GetOrAdd(typeof(TContext), CreateDelegate_OnModelCreating);

            applyConfigurations(modelBuilder);
        }

        /// <summary>
        /// Configures "table splitting" feature for subset and superset entity types.
        /// (for example it could be Order and OrderDetailed, with some shared columns)
        /// (<seealso cref="https://docs.microsoft.com/en-us/ef/core/modeling/table-splitting"/>)
        /// </summary>
        /// <typeparam name="TSuperSetModel"></typeparam>
        /// <typeparam name="TSubSetModel"></typeparam>
        /// <param name="subSetModelBuilder"></param>
        public static void ConfigureTableSplitting<TSuperSetModel, TSubSetModel>
        (
            EntityTypeBuilder<TSuperSetModel> superSetModelBuilder, 
            EntityTypeBuilder<TSubSetModel> subSetModelBuilder
        )
            where TSubSetModel : class
            where TSuperSetModel : class
        {
            var superSetModelType = superSetModelBuilder.Metadata.Model.FindEntityType(typeof(TSuperSetModel));
            var subSetModelType = subSetModelBuilder.Metadata.Model.FindEntityType(typeof(TSubSetModel));

            var tableName = superSetModelType.GetTableName();
            
            subSetModelBuilder.ToTable(tableName);

            var subSetModelProperties = subSetModelBuilder.Metadata.GetProperties();

            var superSetModelPrimaryKey = superSetModelType.FindPrimaryKey();
            var subSetModelPrimaryKey = subSetModelType.FindPrimaryKey();

            if (superSetModelPrimaryKey is null || subSetModelPrimaryKey is null)
                throw new InvalidOperationException($"To apply Table Splitting feature the entity types MUST have PK [{typeof(TSuperSetModel).FullName}]<=>[{typeof(TSubSetModel).FullName}]");

            var superSetModelPrimaryKeyProperties = superSetModelPrimaryKey.Properties;
            var subSetModelPrimaryKeyProperties = subSetModelPrimaryKey.Properties;

            var superSetModelScalarProperties = superSetModelType.GetProperties().Where(e => !superSetModelPrimaryKeyProperties.Contains(e));

            //Mapping all shared properties to the same columns
            foreach (var subSetModelProperty in subSetModelProperties)
            {
                var subSetModelPropertyName = subSetModelProperty.Name;
                var subSetModelPropertyColumnName = subSetModelProperty.GetColumnName();

                //Searching by property names or columns matches
                var superSetModelProperty = superSetModelScalarProperties
                    .FirstOrDefault
                    (
                        e =>
                        {
                            var superSetModelPropertyColumnName = e.GetColumnName();

                            return
                            (
                                !string.IsNullOrWhiteSpace(superSetModelPropertyColumnName)
                                    &&
                                (
                                    superSetModelPropertyColumnName == subSetModelPropertyColumnName
                                        ||
                                    superSetModelPropertyColumnName == subSetModelPropertyName
                                )
                            );
                        }
                    );

                if (superSetModelProperty == null)
                    continue;

                //Copied as much configs as possible to match shared properties configurations
                var columnName = superSetModelProperty.GetColumnName();
                var defaultValue = superSetModelProperty.GetDefaultValue();
                var columnType = superSetModelProperty.GetColumnType();
                var isUnicode = superSetModelProperty.IsUnicode();
                var computedValue = superSetModelProperty.GetComputedColumnSql();
                var maxLength = superSetModelProperty.GetMaxLength();
                var isRequired = !superSetModelProperty.IsColumnNullable();
                var defaultValueSql = superSetModelProperty.GetDefaultValueSql();
                var isFixedLength = superSetModelProperty.IsFixedLength();

                //TODO:
                //Here might be its more reliable to use expressions overload,
                //but its a bit more complicated (dynamic expressions building / caching)
                var superSetModelPropertyBuilder = superSetModelBuilder.Property(superSetModelProperty.Name);

                superSetModelPropertyBuilder.HasColumnName(columnName);

                //TODO:
                //Here might be its more reliable to use expressions overload,
                //but its a bit more complicated (dynamic expressions building / caching)
                var subSetModelPropertyBuilder = subSetModelBuilder.Property(subSetModelProperty.Name);

                subSetModelPropertyBuilder.HasColumnName(columnName);
                subSetModelPropertyBuilder.IsRequired(isRequired);
                subSetModelPropertyBuilder.IsFixedLength(isFixedLength);

                if (!(columnType is null))
                    subSetModelPropertyBuilder.HasColumnType(columnType);

                if (!(maxLength is null))
                    subSetModelPropertyBuilder.HasMaxLength(maxLength.Value);

                if (!(isUnicode is null))
                    subSetModelPropertyBuilder.IsUnicode(isUnicode.Value);

                if(!(defaultValue is null))
                    subSetModelPropertyBuilder.HasDefaultValue(defaultValue);

                if(!(defaultValueSql is null))
                    subSetModelPropertyBuilder.HasDefaultValueSql(defaultValueSql);

                if (!(computedValue is null))
                    subSetModelPropertyBuilder.HasComputedColumnSql(computedValue);
            }

            //Searching for navigation property
            var navigationToSuperSet = subSetModelType.GetNavigations()
                .Where(e => (e?.FieldInfo?.FieldType ?? e?.PropertyInfo?.PropertyType) == typeof(TSuperSetModel))
                .FirstOrDefault();

            //TODO:
            //Here might be its more reliable to use expressions overload,
            //but its a bit more complicated (dynamic expressions building / caching)
            if (subSetModelPrimaryKeyProperties.Any() && !(superSetModelPrimaryKey is null))
            {
                //Set the same primary key constraint name
                subSetModelBuilder
                    .HasKey(subSetModelPrimaryKeyProperties.Select(e => e.Name).ToArray())
                    .HasName(superSetModelPrimaryKey.GetName());
            }

            //TODO:
            //Here might be its more reliable to use expressions overload,
            //but its a bit more complicated (dynamic expressions building / caching)
            if (!(navigationToSuperSet is null) && superSetModelPrimaryKeyProperties.Any())
            {
                //Configure one-to-one relationship, to specify table-splitting feature
                subSetModelBuilder
                    .HasOne(navigationToSuperSet.Name)
                    .WithOne()
                    .HasForeignKey(typeof(TSuperSetModel), superSetModelPrimaryKeyProperties.Select(e => e.Name).ToArray());
            }
        }
    }
}
