using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace LookupImportPlus.Services
{
    /// <summary>
    /// Metadata access over the Dataverse SDK. Replaces the Code App's
    /// getEntityMetadata / getEntitySet (src/services/MetadataService.ts):
    ///   RetrieveAllEntitiesRequest for the entity picker (Tab 1),
    ///   RetrieveEntityRequest for attributes + relationships of the chosen table.
    /// Polymorphic targets come from LookupAttributeMetadata.Targets.
    /// </summary>
    public sealed class MetadataService
    {
        private readonly IOrganizationService _service;

        public MetadataService(IOrganizationService service)
        {
            _service = service;
        }

        /// <summary>
        /// Lightweight list of entities for the target-table picker. Filtered to
        /// entities that can actually be imported into (customizable, not virtual).
        /// </summary>
        public IReadOnlyList<EntityMetadata> RetrieveEntityList()
        {
            var response = (RetrieveAllEntitiesResponse)_service.Execute(
                new RetrieveAllEntitiesRequest
                {
                    EntityFilters = EntityFilters.Entity,
                    RetrieveAsIfPublished = true
                });

            return response.EntityMetadata
                .Where(e => e.IsCustomizable?.Value == true && e.IsIntersect != true)
                .OrderBy(e => e.LogicalName)
                .ToList();
        }

        /// <summary>
        /// Full attribute + relationship metadata for one entity (Tab 1 selection).
        /// </summary>
        public EntityMetadata RetrieveEntity(string logicalName)
        {
            var response = (RetrieveEntityResponse)_service.Execute(
                new RetrieveEntityRequest
                {
                    LogicalName = logicalName,
                    EntityFilters = EntityFilters.Attributes | EntityFilters.Relationships,
                    RetrieveAsIfPublished = true
                });

            return response.EntityMetadata;
        }

        /// <summary>Target logical names of a (possibly polymorphic) lookup attribute.</summary>
        public IReadOnlyList<string> GetLookupTargets(EntityMetadata entity, string lookupAttribute)
        {
            var attr = entity.Attributes?
                .OfType<LookupAttributeMetadata>()
                .FirstOrDefault(a => a.LogicalName == lookupAttribute);

            return attr?.Targets?.ToList() ?? new List<string>();
        }
    }
}
