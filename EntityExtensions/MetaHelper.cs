﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.ModelConfiguration;
using System.Reflection;

namespace EntityExtensions
{
    /// <summary>
    /// Exposes useful extension methods to read EF metadata. Most of the contained function will result in opening a DB connection since it utilizes ObjectContext
    /// </summary>
    public static class MetaHelper
    {
        /// <summary>
        /// Returns a map of key database column names, and their counterpart properties.
        /// Will result in opening a DB connection.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static Dictionary<string, PropertyInfo> GetTableKeyColumns<T>(this DbContext context)
        {
            var entityType = typeof(T);
            var octx = ((IObjectContextAdapter) context).ObjectContext;
            var storageEntityType = octx.MetadataWorkspace.GetItems(DataSpace.SSpace)
                .Where(x => x.BuiltInTypeKind == BuiltInTypeKind.EntityType).OfType<EntityType>()
                .Single(x => x.Name == entityType.Name);
            var columnNames = storageEntityType.Properties.ToDictionary(x => x.Name,
                y => y.MetadataProperties.FirstOrDefault(x => x.Name == "PreferredName")?.Value as string ?? y.Name);

            return storageEntityType.KeyMembers.Select((elm, index) => new
            { elm.Name, Property = entityType.GetProperty(columnNames[elm.Name]) })
                .ToDictionary(x => x.Name, x => x.Property);
        }

        /// <summary>
        /// Returns the database table name for a given entity
        /// Will result in opening a DB connection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string GetTableName<T>(this DbContext context)
        {
            var entityType = typeof(T);
            var octx = (context as IObjectContextAdapter).ObjectContext;
            var et = octx.MetadataWorkspace.GetItemCollection(DataSpace.SSpace)
                .GetItems<EntityContainer>()
                .Single()
                .BaseEntitySets
                .Single(x => x.Name == entityType.Name);

            return String.Concat(et.MetadataProperties["Schema"].Value, ".", et.MetadataProperties["Table"].Value);
        }

        /// <summary>
        /// Returns a map of computed column names, with a flag indicating whether it's DB generted (identity) or not.
        /// Will result in opening a DB connection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <returns></returns>
        public static Dictionary<string, bool> GetComputedColumnNames<T>(this DbContext context)
        {
            var entityType = typeof(T);
            var octx = (context as IObjectContextAdapter).ObjectContext;
            var storageEntityType = octx.MetadataWorkspace.GetItems(DataSpace.SSpace)
                .Where(x => x.BuiltInTypeKind == BuiltInTypeKind.EntityType)
                .OfType<EntityType>().Single(x => x.Name == entityType.Name);
            return storageEntityType.Members
                .Where(x => x.IsStoreGeneratedIdentity || x.IsStoreGeneratedComputed)
                .ToDictionary(x => x.Name, y => y.IsStoreGeneratedIdentity);
        }


        /// <summary>
        /// Returns a map of actual database column names and their counterpart properties.
        /// Will result in opening a DB connection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <returns></returns>
        public static Dictionary<string, PropertyInfo> GetTableColumns<T>(this DbContext context)
        {
            var entityType = typeof(T);
            var octx = (context as IObjectContextAdapter).ObjectContext;
            var storageEntityType = octx.MetadataWorkspace.GetItems(DataSpace.SSpace)
                .Where(x => x.BuiltInTypeKind == BuiltInTypeKind.EntityType).OfType<EntityType>()
                .Single(x => x.Name == entityType.Name);

            var columnNames = storageEntityType.Properties.ToDictionary(x => x.Name,
                y => y.MetadataProperties.FirstOrDefault(x => x.Name == "PreferredName")?.Value as string ?? y.Name);

            return storageEntityType.Properties.Select((elm, index) =>
                    new { elm.Name, Property = entityType.GetProperty(columnNames[elm.Name]) })
                .ToDictionary(x => x.Name, x => x.Property);
        }

        /// <summary>
        /// Returns the actual database column name for a given property.
        /// Will result in opening a DB connection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static string GetColumnName<T>(this DbContext context, string propertyName)
        {
            var entityType = typeof(T);
            var octx = (context as IObjectContextAdapter).ObjectContext;
            var storageEntityType = octx.MetadataWorkspace.GetItems(DataSpace.SSpace)
                .Where(x => x.BuiltInTypeKind == BuiltInTypeKind.EntityType).OfType<EntityType>()
                .Single(x => x.Name == entityType.Name);

            return storageEntityType.Properties.FirstOrDefault(y =>
                y.MetadataProperties.Any(x => x.Name == "PreferredName" && x.Value as string == propertyName))?.Name;
        }

        /// <summary>
        /// Returns a map of object property name to actual database column name
        /// Will result in opening a DB connection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Dictionary<string, string> GetPropertyColumnNames<T>(this DbContext context)
        {
            var columns = GetTableColumns<T>(context);
            return columns.ToDictionary(x => x.Value.Name, y => y.Key);
        }

        private static IEnumerable<NavigationProperty> GetTypeNavigationProperties(DbContext context, Type type)
        {
            var octx = (context as IObjectContextAdapter).ObjectContext;
            ////For some reason, below doesn't populate the dependent properties???
            //var result =objectEntityType.NavigationProperties.Where(x => x.FromEndMember.Name.StartsWith(typeof(T).Name))
            //    .ToDictionary(x=>GetPropertyType(typeof(T).GetProperty(x.Name)), x=>x.GetDependentProperties());

            //Can't dynamically call a generic method, hence have to use below and dynamic!
            var method = octx.GetType().GetMethods().First(x => x.Name == nameof(octx.CreateObjectSet) && x.DeclaringType == typeof(ObjectContext));
            var generic = method.MakeGenericMethod(type);
            dynamic value = generic.Invoke(octx, null);
            EntitySet set = value.EntitySet;
            //Dependents are all navigation properties that has a One multiplicity from the type's end.
            return set.ElementType.NavigationProperties;

        }

        /// <summary>
        /// Represents a single side of a relationship between entities.
        /// </summary>
        public class EntityKey
        {
            public readonly Type Type;
            public readonly List<PropertyInfo> Keys;
            public EntityKey(Type type, List<PropertyInfo> keys)
            {
                Type = type;
                Keys = keys;
            }
        }

        public static List<EntityKey> GetDependentTypes(this DbContext ctx, Type entityType)
        {

            /*
             1. Get all dependent navigation properties (muliplicity = 1 from this type end)
             2. for each dependent, get the dependent properties
             */

            var result = new List<EntityKey>();

            var dependents = GetTypeNavigationProperties(ctx, entityType)
                //.Where(x => !x.GetDependentProperties().Any())
                .ToList();

            foreach (var property in dependents)
            {
                var targeType = GetPropertyType(entityType.GetProperty(property.Name));
                var props = GetTypeNavigationProperties(
                    ctx, targeType
                ).Where(x => x.RelationshipType == property.RelationshipType
                && x.GetDependentProperties().Any()).ToList();
                var navigationProp = props.SingleOrDefault();
                if (navigationProp == null)
                {
                    throw new ModelValidationException(
                        $"Navigation property {entityType.Name}.{property.Name} doesn't have a counter property on {targeType.Name}.");
                }
                var depedentProps = navigationProp.GetDependentProperties().ToList();

                result.Add(new EntityKey(targeType,
                    depedentProps.Select(x => targeType.GetProperty(x.Name)).ToList()));
            }

            return result;
        }

        private static Type GetPropertyType(PropertyInfo property)
        {
            var propType = property.PropertyType;
            if (propType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propType))
            {
                //This is a collection, so get the actual type.
                propType =
                    propType.IsArray
                        ? propType.GetElementType()
                        : propType.GetGenericArguments()[0];
            }
            return propType;
        }
    }
}