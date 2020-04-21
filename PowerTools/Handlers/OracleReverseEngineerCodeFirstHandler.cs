// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.
namespace Microsoft.DbContextPackage.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity.Design;
    using System.Data.Entity.Design.PluralizationServices;
    using System.Data.Metadata.Edm;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using Microsoft.DbContextPackage.Extensions;
    using Microsoft.DbContextPackage.Resources;
    using Microsoft.DbContextPackage.Utilities;

    public class OracleReverseEngineerCodeFirstHandler: ICodeFirstHandler
    {
        private static readonly IEnumerable<EntityStoreSchemaFilterEntry> _storeMetadataFilters = new[]
            {
                new EntityStoreSchemaFilterEntry(null, null, "EdmMetadata", EntityStoreSchemaFilterObjectTypes.Table, EntityStoreSchemaFilterEffect.Exclude),
                new EntityStoreSchemaFilterEntry(null, null, "__MigrationHistory", EntityStoreSchemaFilterObjectTypes.Table, EntityStoreSchemaFilterEffect.Exclude)
            };
        /// <summary>
        /// 生成C#代码
        /// </summary>
        public void ReverseEngineerCodeFirst(string strCon, List<string> tableNames = null, string savePath = null)
        {
            // Load store schema
            var storeGenerator = new EntityStoreSchemaGenerator("Oracle.ManagedDataAccess.Client", strCon, "dbo");
            storeGenerator.GenerateForeignKeyProperties = true;
            var errors = storeGenerator.GenerateStoreMetadata(_storeMetadataFilters).Where(e => e.Severity == EdmSchemaErrorSeverity.Error);
            errors.HandleErrors(Strings.ReverseEngineer_SchemaError);

            // Generate default mapping
            var contextName = "CustomDbContext";
            var modelGenerator = new EntityModelSchemaGenerator(storeGenerator.EntityContainer, "DefaultNamespace", contextName);
            modelGenerator.PluralizationService = PluralizationService.CreateService(new CultureInfo("en"));
            modelGenerator.GenerateForeignKeyProperties = true;
            modelGenerator.GenerateMetadata();

            // Pull out info about types to be generated
            var entityTypes = modelGenerator.EdmItemCollection.OfType<EntityType>().ToArray();
            var mappings = new EdmMapping(modelGenerator, storeGenerator.StoreItemCollection);

            // Generate Entity Classes and Mappings
            var templateProcessor = new TemplateProcessor(null);
            var modelsNamespace = "EF.Models";
            var modelsDirectory = savePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Codes");
            if (!Directory.Exists(modelsDirectory))
            {
                Directory.CreateDirectory(modelsDirectory);
            }
            var mappingNamespace = modelsNamespace + ".Mapping";
            var mappingDirectory = Path.Combine(modelsDirectory, "Mapping");
            if (!Directory.Exists(mappingDirectory))
            {
                Directory.CreateDirectory(mappingDirectory);
            }
            var entityFrameworkVersion = new Version("6.0.0.0");

            var matchs = (tableNames ?? new List<string>()).Where(q => q.EndsWith("*")).Select(q => q.TrimEnd('*')).ToList();
            foreach (var entityType in entityTypes)
            {
                var enable = tableNames == null;
                if (tableNames != null && tableNames.Count > 0 && tableNames.Any(q => q.ToLower() == entityType.Name.ToLower()))
                {
                    enable = true;
                }
                if (!enable && matchs != null && matchs.Count > 0 && matchs.Any(q => entityType.Name.ToLower().StartsWith(q.ToLower())))
                {
                    enable = true;
                }
                if (!enable) continue;

                // Generate the code file
                var entityHost = new EfTextTemplateHost
                {
                    EntityType = entityType,
                    EntityContainer = modelGenerator.EntityContainer,
                    Namespace = modelsNamespace,
                    ModelsNamespace = modelsNamespace,
                    MappingNamespace = mappingNamespace,
                    EntityFrameworkVersion = entityFrameworkVersion,
                    TableSet = mappings.EntityMappings[entityType].Item1,
                    PropertyToColumnMappings = mappings.EntityMappings[entityType].Item2,
                    ManyToManyMappings = mappings.ManyToManyMappings
                };
                var entityContents = templateProcessor.Process(Templates.EntityTemplate, entityHost);
                var filePath = Path.Combine(modelsDirectory, ToPascal(entityType.Name) + entityHost.FileExtension);
                File.WriteAllText(filePath, entityContents);

                var mappingHost = new EfTextTemplateHost
                {
                    EntityType = entityType,
                    EntityContainer = modelGenerator.EntityContainer,
                    Namespace = mappingNamespace,
                    ModelsNamespace = modelsNamespace,
                    MappingNamespace = mappingNamespace,
                    EntityFrameworkVersion = entityFrameworkVersion,
                    TableSet = mappings.EntityMappings[entityType].Item1,
                    PropertyToColumnMappings = mappings.EntityMappings[entityType].Item2,
                    ManyToManyMappings = mappings.ManyToManyMappings
                };
                var mappingContents = templateProcessor.Process(Templates.MappingTemplate, mappingHost);
                var mappingFilePath = Path.Combine(mappingDirectory, ToPascal(entityType.Name) + "Map" + mappingHost.FileExtension);
                File.WriteAllText(mappingFilePath, mappingContents);
            }

            if (tableNames == null || tableNames.Count == 0)
            {
                // Generate Context
                var contextHost = new EfTextTemplateHost
                {
                    EntityContainer = modelGenerator.EntityContainer,
                    Namespace = modelsNamespace,
                    ModelsNamespace = modelsNamespace,
                    MappingNamespace = mappingNamespace,
                    EntityFrameworkVersion = entityFrameworkVersion
                };
                var contextContents = templateProcessor.Process(Templates.ContextTemplate, contextHost);
                var contextFilePath = Path.Combine(modelsDirectory, ToPascal(modelGenerator.EntityContainer.Name) + contextHost.FileExtension);
                File.WriteAllText(contextFilePath, contextContents);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        private string ToPascal(string raw)
        {
            var value = new StringBuilder("");
            var names = raw.ToLower().Split("_".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
            foreach (var name in names)
            {
                value.Append(name[0].ToString().ToUpper());
                value.Append(name.Skip(1).ToArray());
            }
            return value.ToString();
        }
        /// <summary>
        /// 
        /// </summary>
        private class EdmMapping
        {
            public EdmMapping(EntityModelSchemaGenerator mcGenerator, StoreItemCollection store)
            {
                DebugCheck.NotNull(mcGenerator);
                DebugCheck.NotNull(store);

                // Pull mapping xml out
                var mappingDoc = new XmlDocument();
                var mappingXml = new StringBuilder();

                using (var textWriter = new StringWriter(mappingXml))
                {
                    mcGenerator.WriteStorageMapping(new XmlTextWriter(textWriter));
                }

                mappingDoc.LoadXml(mappingXml.ToString());

                var entitySets = mcGenerator.EntityContainer.BaseEntitySets.OfType<EntitySet>();
                var associationSets = mcGenerator.EntityContainer.BaseEntitySets.OfType<AssociationSet>();
                var tableSets = store.GetItems<EntityContainer>().Single().BaseEntitySets.OfType<EntitySet>();

                this.EntityMappings = BuildEntityMappings(mappingDoc, entitySets, tableSets);
                this.ManyToManyMappings = BuildManyToManyMappings(mappingDoc, associationSets, tableSets);
            }

            public Dictionary<EntityType, Tuple<EntitySet, Dictionary<EdmProperty, EdmProperty>>> EntityMappings { get; set; }

            public Dictionary<AssociationType, Tuple<EntitySet, Dictionary<RelationshipEndMember, Dictionary<EdmMember, string>>>> ManyToManyMappings { get; set; }

            private static Dictionary<AssociationType, Tuple<EntitySet, Dictionary<RelationshipEndMember, Dictionary<EdmMember, string>>>> BuildManyToManyMappings(XmlDocument mappingDoc, IEnumerable<AssociationSet> associationSets, IEnumerable<EntitySet> tableSets)
            {
                DebugCheck.NotNull(mappingDoc);
                DebugCheck.NotNull(associationSets);
                DebugCheck.NotNull(tableSets);

                // Build mapping for each association
                var mappings = new Dictionary<AssociationType, Tuple<EntitySet, Dictionary<RelationshipEndMember, Dictionary<EdmMember, string>>>>();
                var namespaceManager = new XmlNamespaceManager(mappingDoc.NameTable);
                namespaceManager.AddNamespace("ef", mappingDoc.ChildNodes[0].NamespaceURI);
                foreach (var associationSet in associationSets.Where(a => !a.ElementType.AssociationEndMembers.Where(e => e.RelationshipMultiplicity != RelationshipMultiplicity.Many).Any()))
                {
                    var setMapping = mappingDoc.SelectSingleNode(string.Format("//ef:AssociationSetMapping[@Name=\"{0}\"]", associationSet.Name), namespaceManager);
                    var tableName = setMapping.Attributes["StoreEntitySet"].Value;
                    var tableSet = tableSets.Single(s => s.Name == tableName);

                    var endMappings = new Dictionary<RelationshipEndMember, Dictionary<EdmMember, string>>();
                    foreach (var end in associationSet.AssociationSetEnds)
                    {
                        var propertyToColumnMappings = new Dictionary<EdmMember, string>();
                        var endMapping = setMapping.SelectSingleNode(string.Format("./ef:EndProperty[@Name=\"{0}\"]", end.Name), namespaceManager);
                        foreach (XmlNode fk in endMapping.ChildNodes)
                        {
                            var propertyName = fk.Attributes["Name"].Value;
                            var property = end.EntitySet.ElementType.Properties[propertyName];
                            var columnName = fk.Attributes["ColumnName"].Value;
                            propertyToColumnMappings.Add(property, columnName);
                        }

                        endMappings.Add(end.CorrespondingAssociationEndMember, propertyToColumnMappings);
                    }

                    mappings.Add(associationSet.ElementType, Tuple.Create(tableSet, endMappings));
                }

                return mappings;
            }

            private static Dictionary<EntityType, Tuple<EntitySet, Dictionary<EdmProperty, EdmProperty>>> BuildEntityMappings(XmlDocument mappingDoc, IEnumerable<EntitySet> entitySets, IEnumerable<EntitySet> tableSets)
            {
                DebugCheck.NotNull(mappingDoc);
                DebugCheck.NotNull(entitySets);
                DebugCheck.NotNull(tableSets);

                // Build mapping for each type
                var mappings = new Dictionary<EntityType, Tuple<EntitySet, Dictionary<EdmProperty, EdmProperty>>>();
                var namespaceManager = new XmlNamespaceManager(mappingDoc.NameTable);
                namespaceManager.AddNamespace("ef", mappingDoc.ChildNodes[0].NamespaceURI);
                foreach (var entitySet in entitySets)
                {
                    // Post VS2010 builds use a different structure for mapping
                    var setMapping = mappingDoc.ChildNodes[0].NamespaceURI == "http://schemas.microsoft.com/ado/2009/11/mapping/cs"
                        ? mappingDoc.SelectSingleNode(string.Format("//ef:EntitySetMapping[@Name=\"{0}\"]/ef:EntityTypeMapping/ef:MappingFragment", entitySet.Name), namespaceManager)
                        : mappingDoc.SelectSingleNode(string.Format("//ef:EntitySetMapping[@Name=\"{0}\"]", entitySet.Name), namespaceManager);

                    var tableName = setMapping.Attributes["StoreEntitySet"].Value;
                    var tableSet = tableSets.Single(s => s.Name == tableName);

                    var propertyMappings = new Dictionary<EdmProperty, EdmProperty>();
                    foreach (var prop in entitySet.ElementType.Properties)
                    {
                        var propMapping = setMapping.SelectSingleNode(string.Format("./ef:ScalarProperty[@Name=\"{0}\"]", prop.Name), namespaceManager);
                        var columnName = propMapping.Attributes["ColumnName"].Value;
                        var columnProp = tableSet.ElementType.Properties[columnName];

                        propertyMappings.Add(prop, columnProp);
                    }

                    mappings.Add(entitySet.ElementType, Tuple.Create(tableSet, propertyMappings));
                }

                return mappings;
            }
        }
    }
}
