using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using LookupImportPlus.Data;
using Microsoft.Xrm.Sdk.Query;

namespace LookupImportPlus.Services
{
    public sealed class SavedView
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string FetchXml { get; set; }

        /// <summary>Column logical names parsed from the view's FetchXML.</summary>
        public List<string> Columns { get; set; } = new List<string>();
    }

    /// <summary>
    /// Lists saved main views for an entity so a configuration can be based on an
    /// existing view (port of ViewService.ts). Degrades to an empty list if
    /// savedqueries is not accessible.
    /// </summary>
    public sealed class ViewService
    {
        private readonly DataverseContext _ctx;

        public ViewService(DataverseContext ctx)
        {
            _ctx = ctx;
        }

        public IReadOnlyList<SavedView> ListViews(string entityLogicalName)
        {
            try
            {
                var query = new QueryExpression("savedquery")
                {
                    ColumnSet = new ColumnSet("savedqueryid", "name", "fetchxml", "returnedtypecode", "querytype"),
                    TopCount = 100,
                    Orders = { new OrderExpression("name", OrderType.Ascending) }
                };
                query.Criteria.AddCondition("returnedtypecode", ConditionOperator.Equal, entityLogicalName);
                query.Criteria.AddCondition("querytype", ConditionOperator.Equal, 0);

                var res = _ctx.RetrieveMultiple(query);
                return res.Entities.Select(r =>
                {
                    var fetchXml = r.GetAttributeValue<string>("fetchxml") ?? "";
                    return new SavedView
                    {
                        Id = r.Id.ToString(),
                        Name = r.GetAttributeValue<string>("name") ?? "",
                        FetchXml = fetchXml,
                        Columns = ParseFetchXmlColumns(fetchXml)
                    };
                }).ToList();
            }
            catch
            {
                return new List<SavedView>();
            }
        }

        /// <summary>Attribute logical names from a FetchXML string (fetchxml.ts).</summary>
        public static List<string> ParseFetchXmlColumns(string fetchXml)
        {
            var cols = new List<string>();
            if (string.IsNullOrWhiteSpace(fetchXml)) return cols;
            try
            {
                var doc = XDocument.Parse(fetchXml);
                foreach (var attr in doc.Descendants("attribute"))
                {
                    var name = (string)attr.Attribute("name");
                    if (!string.IsNullOrEmpty(name) && !cols.Contains(name)) cols.Add(name);
                }
            }
            catch
            {
                // Malformed FetchXML — return whatever parsed.
            }
            return cols;
        }
    }
}
