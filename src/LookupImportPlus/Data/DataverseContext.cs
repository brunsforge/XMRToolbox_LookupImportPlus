using System;
using System.ServiceModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace LookupImportPlus.Data
{
    /// <summary>
    /// Thin wrapper around the host-provided <see cref="IOrganizationService"/>.
    /// This is the SDK replacement for the Code App's DataverseClient: a single
    /// seam the services use for reads/writes, WhoAmI and record deep-links.
    /// </summary>
    public sealed class DataverseContext
    {
        private readonly IOrganizationService _service;
        private Guid? _userId;

        public DataverseContext(IOrganizationService service, string webApplicationUrl)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            WebApplicationUrl = (webApplicationUrl ?? string.Empty).TrimEnd('/');
        }

        public IOrganizationService Service => _service;

        /// <summary>Base URL of the connected org, for record deep-links.</summary>
        public string WebApplicationUrl { get; }

        /// <summary>Id of the running user (cached). Mirrors client.whoAmI().</summary>
        public Guid WhoAmI()
        {
            if (_userId == null)
            {
                var resp = (WhoAmIResponse)_service.Execute(new WhoAmIRequest());
                _userId = resp.UserId;
            }
            return _userId.Value;
        }

        /// <summary>Retrieve one record, or null when it does not exist / is inaccessible.</summary>
        public Entity Retrieve(string logicalName, Guid id, ColumnSet columns)
        {
            try
            {
                return _service.Retrieve(logicalName, id, columns);
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                return null;
            }
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            return _service.RetrieveMultiple(query);
        }

        public Guid Create(Entity entity) => _service.Create(entity);

        public void Update(Entity entity) => _service.Update(entity);

        public OrganizationResponse Execute(OrganizationRequest request) => _service.Execute(request);

        /// <summary>Deep link to open the record in the model-driven UI.</summary>
        public string RecordUrl(string logicalName, Guid id)
        {
            if (string.IsNullOrEmpty(WebApplicationUrl)) return null;
            return $"{WebApplicationUrl}/main.aspx?pagetype=entityrecord&etn={logicalName}&id={id}";
        }
    }
}
