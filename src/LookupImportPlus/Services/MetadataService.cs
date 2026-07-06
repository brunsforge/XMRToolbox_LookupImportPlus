using System;
using System.Collections.Generic;
using System.Linq;
using LookupImportPlus.Data;
using LookupImportPlus.Domain;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Sdk = Microsoft.Xrm.Sdk.Metadata;
// Resolve the name clash: bare metadata names mean our domain types; SDK
// metadata types are reached via the Sdk. alias above.
using EntityMetadata = LookupImportPlus.Domain.EntityMetadata;
using AttributeMetadata = LookupImportPlus.Domain.AttributeMetadata;
using LookupMetadata = LookupImportPlus.Domain.LookupMetadata;

namespace LookupImportPlus.Services
{
    /// <summary>
    /// Turns raw Dataverse metadata into the app's normalized domain shapes and
    /// caches them (port of src/services/MetadataService.ts). Lookup targets and
    /// navigation properties are derived from ManyToOne relationships, never
    /// guessed — this is what makes polymorphic lookups work correctly.
    /// </summary>
    public sealed class MetadataService
    {
        private readonly DataverseContext _ctx;
        private readonly Dictionary<string, EntityMetadata> _entityCache =
            new Dictionary<string, EntityMetadata>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EntitySummary> _summaryCache =
            new Dictionary<string, EntitySummary>(StringComparer.OrdinalIgnoreCase);

        public MetadataService(DataverseContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>Full entity metadata with attributes and enriched lookup targets. Cached.</summary>
        public EntityMetadata GetEntity(string logicalName)
        {
            if (_entityCache.TryGetValue(logicalName, out var cached)) return cached;

            var resp = (RetrieveEntityResponse)_ctx.Execute(new RetrieveEntityRequest
            {
                LogicalName = logicalName,
                EntityFilters = EntityFilters.Attributes | EntityFilters.Relationships,
                RetrieveAsIfPublished = true
            });

            var entity = Normalize(resp.EntityMetadata);
            EnrichLookupTargets(entity);
            _entityCache[logicalName] = entity;
            return entity;
        }

        /// <summary>Lightweight summary (no attributes/relationships). Cached.</summary>
        public EntitySummary GetEntitySummary(string logicalName)
        {
            if (_summaryCache.TryGetValue(logicalName, out var cached)) return cached;

            var resp = (RetrieveEntityResponse)_ctx.Execute(new RetrieveEntityRequest
            {
                LogicalName = logicalName,
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            });

            var m = resp.EntityMetadata;
            var summary = new EntitySummary
            {
                LogicalName = m.LogicalName,
                DisplayName = Label(m.DisplayName) ?? m.LogicalName,
                DisplayCollectionName = Label(m.DisplayCollectionName) ?? m.LogicalName,
                EntitySetName = m.EntitySetName,
                PrimaryIdAttribute = m.PrimaryIdAttribute,
                PrimaryNameAttribute = m.PrimaryNameAttribute
            };
            _summaryCache[logicalName] = summary;
            return summary;
        }

        /// <summary>Entity list for the target-table picker (customizable, non-intersect).</summary>
        public IReadOnlyList<EntitySummary> RetrieveEntityList()
        {
            var resp = (RetrieveAllEntitiesResponse)_ctx.Execute(new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            });

            return resp.EntityMetadata
                .Where(e => e.IsCustomizable?.Value == true && e.IsIntersect != true && !string.IsNullOrEmpty(e.EntitySetName))
                .Select(e => new EntitySummary
                {
                    LogicalName = e.LogicalName,
                    DisplayName = Label(e.DisplayName) ?? e.LogicalName,
                    DisplayCollectionName = Label(e.DisplayCollectionName) ?? e.LogicalName,
                    EntitySetName = e.EntitySetName,
                    PrimaryIdAttribute = e.PrimaryIdAttribute,
                    PrimaryNameAttribute = e.PrimaryNameAttribute
                })
                .OrderBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>Only the lookup attributes of an entity — powers the "Lookups only" filter.</summary>
        public IReadOnlyList<AttributeMetadata> GetLookupAttributes(string logicalName)
        {
            return GetEntity(logicalName).Attributes.Where(a => a.Kind == AttributeKind.Lookup).ToList();
        }

        public void ClearCache()
        {
            _entityCache.Clear();
            _summaryCache.Clear();
        }

        // ── normalization ────────────────────────────────────────────────

        private EntityMetadata Normalize(Sdk.EntityMetadata raw)
        {
            var manyToOne = raw.ManyToOneRelationships ?? new OneToManyRelationshipMetadata[0];

            var attributes = (raw.Attributes ?? new Sdk.AttributeMetadata[0])
                .Where(a => !string.IsNullOrEmpty(a.LogicalName) && a.AttributeType != null)
                .Where(a => a.AttributeOf == null) // skip child/virtual sub-attributes
                .Select(a => NormalizeAttribute(a, manyToOne))
                .ToList();

            return new EntityMetadata
            {
                LogicalName = raw.LogicalName,
                DisplayName = Label(raw.DisplayName) ?? raw.LogicalName,
                DisplayCollectionName = Label(raw.DisplayCollectionName) ?? raw.LogicalName,
                EntitySetName = raw.EntitySetName,
                PrimaryIdAttribute = raw.PrimaryIdAttribute,
                PrimaryNameAttribute = raw.PrimaryNameAttribute,
                IsActivity = raw.IsActivity == true,
                Attributes = attributes
            };
        }

        private static AttributeMetadata NormalizeAttribute(
            Sdk.AttributeMetadata a,
            OneToManyRelationshipMetadata[] manyToOne)
        {
            var kind = MapKind(a);
            var required = a.RequiredLevel?.Value ?? AttributeRequiredLevel.None;

            var result = new AttributeMetadata
            {
                LogicalName = a.LogicalName,
                DisplayName = Label(a.DisplayName) ?? a.LogicalName,
                Kind = kind,
                AttributeType = a.AttributeTypeName?.Value ?? a.AttributeType?.ToString(),
                IsWritable = (a.IsValidForCreate == true || a.IsValidForUpdate == true)
                             && a.IsLogical != true,
                IsRequired = required == AttributeRequiredLevel.ApplicationRequired
                             || required == AttributeRequiredLevel.SystemRequired,
                IsPrimaryId = a.IsPrimaryId == true,
                IsPrimaryName = a.IsPrimaryName == true,
                MaxLength = (a as StringAttributeMetadata)?.MaxLength
                            ?? (a as MemoAttributeMetadata)?.MaxLength
            };

            if (kind == AttributeKind.Lookup)
            {
                var rels = manyToOne
                    .Where(r => string.Equals(r.ReferencingAttribute, a.LogicalName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var targets = rels.Select(r => new LookupTarget
                {
                    LogicalName = r.ReferencedEntity,
                    NavigationProperty = r.ReferencingEntityNavigationPropertyName,
                    // Enriched later from the target entity's own metadata:
                    EntitySetName = "",
                    DisplayName = r.ReferencedEntity,
                    PrimaryIdAttribute = "",
                    PrimaryNameAttribute = ""
                }).ToList();

                result.Lookup = new LookupMetadata
                {
                    Kind = targets.Count > 1 ? LookupKind.Polymorphic : LookupKind.Simple,
                    Targets = targets
                };
            }

            return result;
        }

        private void EnrichLookupTargets(EntityMetadata entity)
        {
            var targets = entity.Attributes
                .Where(a => a.Lookup != null)
                .SelectMany(a => a.Lookup.Targets)
                .ToList();

            foreach (var logicalName in targets.Select(t => t.LogicalName).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                EntitySummary s;
                try { s = GetEntitySummary(logicalName); }
                catch { continue; } // target not accessible — leave un-enriched

                foreach (var t in targets.Where(x => string.Equals(x.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase)))
                {
                    t.EntitySetName = s.EntitySetName;
                    t.DisplayName = s.DisplayName;
                    t.PrimaryIdAttribute = s.PrimaryIdAttribute;
                    t.PrimaryNameAttribute = s.PrimaryNameAttribute;
                }
            }
        }

        /// <summary>Map the SDK attribute type to our normalized kind.</summary>
        private static AttributeKind MapKind(Sdk.AttributeMetadata a)
        {
            var typeName = a.AttributeTypeName?.Value;
            switch (typeName)
            {
                case "StringType": return AttributeKind.String;
                case "MemoType": return AttributeKind.Memo;
                case "IntegerType": return AttributeKind.Integer;
                case "BigIntType": return AttributeKind.BigInt;
                case "DecimalType": return AttributeKind.Decimal;
                case "DoubleType": return AttributeKind.Double;
                case "MoneyType": return AttributeKind.Money;
                case "BooleanType": return AttributeKind.Boolean;
                case "DateTimeType": return AttributeKind.DateTime;
                case "PicklistType": return AttributeKind.Choice;
                case "MultiSelectPicklistType": return AttributeKind.MultiChoice;
                case "LookupType":
                case "CustomerType":
                case "OwnerType":
                case "PartyListType": return AttributeKind.Lookup;
                case "UniqueidentifierType": return AttributeKind.UniqueIdentifier;
                case "StateType": return AttributeKind.State;
                case "StatusType": return AttributeKind.Status;
            }

            // Fall back to the numeric type code when the type-name string is absent.
            switch (a.AttributeType)
            {
                case AttributeTypeCode.String: return AttributeKind.String;
                case AttributeTypeCode.Memo: return AttributeKind.Memo;
                case AttributeTypeCode.Integer: return AttributeKind.Integer;
                case AttributeTypeCode.BigInt: return AttributeKind.BigInt;
                case AttributeTypeCode.Decimal: return AttributeKind.Decimal;
                case AttributeTypeCode.Double: return AttributeKind.Double;
                case AttributeTypeCode.Money: return AttributeKind.Money;
                case AttributeTypeCode.Boolean: return AttributeKind.Boolean;
                case AttributeTypeCode.DateTime: return AttributeKind.DateTime;
                case AttributeTypeCode.Picklist: return AttributeKind.Choice;
                case AttributeTypeCode.Lookup:
                case AttributeTypeCode.Customer:
                case AttributeTypeCode.Owner:
                case AttributeTypeCode.PartyList: return AttributeKind.Lookup;
                case AttributeTypeCode.Uniqueidentifier: return AttributeKind.UniqueIdentifier;
                case AttributeTypeCode.State: return AttributeKind.State;
                case AttributeTypeCode.Status: return AttributeKind.Status;
                default: return AttributeKind.Unknown;
            }
        }

        private static string Label(Microsoft.Xrm.Sdk.Label label) => label?.UserLocalizedLabel?.Label;
    }
}
