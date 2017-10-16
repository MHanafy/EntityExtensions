using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
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
            Type entityType = typeof(T);
            ObjectContext octx = ((IObjectContextAdapter) context).ObjectContext;
            EntityType storageEntityType = octx.MetadataWorkspace.GetItems(DataSpace.SSpace)
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
            Type entityType = typeof(T);
            ObjectContext octx = (context as IObjectContextAdapter).ObjectContext;
            EntitySetBase et = octx.MetadataWorkspace.GetItemCollection(DataSpace.SSpace)
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
            Type entityType = typeof(T);
            ObjectContext octx = (context as IObjectContextAdapter).ObjectContext;
            EntityType storageEntityType = octx.MetadataWorkspace.GetItems(DataSpace.SSpace)
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
            Type entityType = typeof(T);
            ObjectContext octx = (context as IObjectContextAdapter).ObjectContext;
            EntityType storageEntityType = octx.MetadataWorkspace.GetItems(DataSpace.SSpace)
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
            Type entityType = typeof(T);
            ObjectContext octx = (context as IObjectContextAdapter).ObjectContext;
            EntityType storageEntityType = octx.MetadataWorkspace.GetItems(DataSpace.SSpace)
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
    }
}