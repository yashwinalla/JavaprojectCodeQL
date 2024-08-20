using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using WSR.Azure.Database.Context;
using WSR.Azure.Database.Models;
using WSR.Azure.Database.Models.M13;
using WSR.Azure.Database.Models.Policy;
using WSR.Azure.Database.Models.USDA;
using WSR.Domain.Model;
using WSR.Domain.Services;
using System.Linq.Dynamic.Core;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Net;
using WSR.Domain.Model.CIMSReports;
using WSR.Domain.Model.CIMSReports.Request;
using System.Linq;

namespace WSR.CIMS.Implementation
{
    public class CIMSService : ICIMSService
    {
        private WSRDBContext _context;
        private readonly ILogger<CIMSService> _logger;
        private readonly IMapper _mapper;
        private readonly QuotationDocumentBlobHelper _quotationDocumentBlobHelper;
        private readonly string _quotationDocumentContainer;
        private readonly IClientContext _clientContext;
        private string? LoggedInAgentEmailId => _clientContext.Email;
        private int? LoggedInAgentId => _context.AgentsDetails.FirstOrDefault(a => a.Email.ToLower() == (LoggedInAgentEmailId ?? "").ToLower())?.AgentId;
        private IConfiguration configuration;

        public CIMSService(WSRDBContext context, ILogger<CIMSService> logger, IMapper mapper, IConfiguration configuration, IClientContext clientContext)
        {
            _context = context;
            _logger = logger;
            _mapper = mapper;
            _context.Database.SetCommandTimeout(600);
            _clientContext = clientContext;
            this.configuration = configuration;

            var quotationDocumentStorageSection = configuration.GetSection("AzureStorage");
            _quotationDocumentContainer = quotationDocumentStorageSection.GetValue<string>("QuotationDocumentContainerName") ?? "gridprocims";
            _quotationDocumentBlobHelper = new QuotationDocumentBlobHelper(configuration);
        }

        public List<Customer> GetPolicyHolders(int take, int skip, string? aipInsuranceAgentKey, int? agentId = 0, bool? adminAgent = true)
        {
            if (adminAgent ?? true)
                return _context.Customers.OrderBy(c => c.Id).Skip(skip).Take(take).AsList();
            else
            {
                var agent = _context.M13P55AInsuranceAgentAgency.Where(a => a.EmailAddress.Equals(_clientContext.Email)).Select(a => a.AIPInsuranceAgentKey);
                var policyHoldersKey = _context.M13P14InsuranceInForce.Where(i => agent.Contains(i.AIPInsuranceAgentKey)).Select(i => i.AIPPolicyProducerKey).Distinct().ToList();
                List<Customer> policyHolder = new();
                foreach (var customer in _context.Customers.OrderBy(c => c.Id))
                {
                    var exterIds = customer.ExternalId.Split(",");
                    if (exterIds.Intersect(policyHoldersKey).Any())
                        policyHolder.Add(customer);
                }

                return policyHolder.Skip(skip).Take(take).ToList();
            }
        }

        public Customer? GetPolicyHolder(int id)
        {
            return _context.Customers.Where(c => c.Id == id)
                .Include(c => c.Contacts).FirstOrDefault();
        }

        public List<GridProContact> GetGridProContacts(int take, int skip, bool adminAgent)
        {
            if (adminAgent)
            {
                return _context.GridProContacts.OrderByDescending(c => c.LastUpdatedDate).Skip(skip).Take(take).ToList();
            }
            else
            {
                return _context.GridProContacts.Where(c => _clientContext.Email != null && c.CreatedBy.ToLower().Equals(_clientContext.Email.ToLower())).OrderByDescending(c => c.LastUpdatedDate).Skip(skip).Take(take).ToList();
            }
        }

        public GridProContact? GetGridProContact(int id)
        {
            return _context.GridProContacts.Where(c => c.Id == id).FirstOrDefault();
        }

        public List<ActualHistoryReportView> GetActualHistory(int year, bool? adminAgent = false)
        {
            List<ActualHistoryReportView> actualHistory = new List<ActualHistoryReportView>();
            using (SqlConnection db = new SqlConnection(configuration.GetConnectionString("SqlConnectionString")))
            {
                var dynamicParams = new DynamicParameters();
                dynamicParams.Add("@producerKey", null);
                dynamicParams.Add("@reinsuranceYear", year);
                List<ActualHistoryReportView> result = db.Query<dynamic>("CIMS_ActualHistory_Report", dynamicParams, commandTimeout: 500)
                                            .Select(item => new ActualHistoryReportView
                                            {
                                                Id = (int)item.Id,
                                                ReinsuranceYear = item.ReinsuranceYear,
                                                AIPCode = item.AIPCode == "NA" ? "NAU" : item.AIPCode,
                                                BusinessName = item.BusinessName,
                                                AgentName = item.AgentName,
                                                AgentEmail = item.AgentEmail,
                                                CommodityAbbreviation = item.CommodityAbbreviation
                                            }).ToList();

                if (result.Count > 0)
                {
                    if (adminAgent == true)
                        actualHistory = result.Where(x => (x.ReinsuranceYear == year.ToString())).GroupBy(x => x.BusinessName).Select(y => y.First()).Distinct().ToList();
                    else
                        actualHistory = result.Where(x => (x.ReinsuranceYear == year.ToString()) && (x.AgentEmail.ToLower().Trim() == LoggedInAgentEmailId.ToLower().Trim())).GroupBy(x => x.BusinessName).Select(y => y.First()).Distinct().ToList();
                }

            }
            return actualHistory;
        }

        public List<Customer> GetContacts(int take, int skip, bool? adminAgent = false)
        {
            var isAdmin = HasAdminAccess(adminAgent);

            List<string> aipInsuranceAgentKeys = !isAdmin
                                                    ? _context.AgentsAIPMapping.Where(x => x.AgentId == LoggedInAgentId)
                                                                    .Select(x => x.AIPInsuranceAgentKey.ToString())
                                                                    .ToList()
                                                    : new List<string>();
            var policies = _context.Policies.Where(p => isAdmin || aipInsuranceAgentKeys.Contains(p.AIPInsuranceAgentKey))
                                            .Select(p => p.AIPPolicyProducerKey).ToList();

            var customerTypeId = _context.TypeValues.Where(t => t.Code == "PO").Select(t => t.Id).FirstOrDefault().ToString();


            var customers = _context.Customers.Where(c => c.CustomerType == customerTypeId).ToList();

            if (adminAgent == true && aipInsuranceAgentKeys.Count == 0)
                return customers;

            if (policies != null && policies.Any())
            {

                List<Customer> subset = new List<Customer>();

                foreach (Customer customer in customers)
                {
                    if (policies.Any(p => customer.ExternalId.Contains(p)))
                        subset.Add(customer);
                }
                return subset;
            }

            return new List<Customer>();

        }

        public Customer? GetContact(int id)
        {
            var customerTypeId = _context.TypeValues.Where(t => t.Code == "PO").Select(t => t.Id).FirstOrDefault().ToString();
            return _context.Customers
                 .Where(c => c.Id == id)
                .OrderByDescending(x => x.Id)
                .Include(x => x.Contacts)
                .FirstOrDefault();
        }

        public Contact? GetContactDetails(int id)
        {
            return _context.Contacts.Include(X => X.Customer).Where(x => x.Id == id).FirstOrDefault();
        }

        public List<Policy> GetPolicies(int take, int skip, string? aipPolicyProducerKey, string? aipInsuranceAgentKey, string? aipCode, bool? adminAgent = false)
        {
            var isAdmin = HasAdminAccess(adminAgent);
            List<string> aipInsuranceAgentKeys = !isAdmin
                                                    ? _context.AgentsAIPMapping.Where(x => x.AgentId == LoggedInAgentId)
                                                                    .Select(x => x.AIPInsuranceAgentKey.ToString())
                                                                    .ToList()
                                                    : new List<string>();

            List<Policy> p = _context.Policies
                .Where(p => (isAdmin || aipInsuranceAgentKeys.Contains(p.AIPInsuranceAgentKey)) &&
                            (aipPolicyProducerKey == null || aipPolicyProducerKey.Contains(p.AIPPolicyProducerKey)) &&
                            (aipCode == null || aipCode.Contains(p.AIPCode)) &&
                            (aipInsuranceAgentKey == null || aipInsuranceAgentKey.Contains(p.AIPInsuranceAgentKey)))
                .AsNoTracking()
                //.Skip(skip)
                //.Take(take)
                .ToList();

            return p;
        }

        public (List<Policy>, int) GetFilteredPolicies(int take, int skip, string? filterBy, string? filterText, bool? adminAgent = false, string orderBy = "Id", string orderDirection = "desc")
        {
            string? policyNumberContains = filterBy == "policyNumber" ? filterText : null;
            string? aipCode = filterBy == "aipCode" ? filterText : null;
            string? stateCode = filterBy == "stateCode" ? filterText : null;

            var isAdmin = HasAdminAccess(adminAgent);
            List<string> aipInsuranceAgentKeys = !isAdmin
                                                    ? _context.AgentsAIPMapping.Where(x => x.AgentId == LoggedInAgentId)
                                                                    .Select(x => x.AIPInsuranceAgentKey.ToString())
                                                                    .ToList()
                                                    : new List<string>();

            List<Policy> p = _context.Policies
                .Where(p => (isAdmin || aipInsuranceAgentKeys.Contains(p.AIPInsuranceAgentKey))
                            && (policyNumberContains == null || p.PolicyNumber.Contains(policyNumberContains)) &&
                            (aipCode == null || aipCode.Contains(p.AIPCode)) &&
                            (stateCode == null || stateCode.Contains(p.StateCode))
                            ).AsNoTracking()
                //.Skip(skip)
                //.Take(? 20: 5000)
                .ToList();

            IQueryable<Policy> query = p.AsQueryable();
            query = query.ApplyOrder(orderBy, orderDirection);
            if (filterBy == null || filterBy == "policyNumber" || filterBy == "aipCode" || filterBy == "stateCode")
            {
                query = query.Skip(skip);
                query = query.Take(take);
            }

            return (query.ToList(), p.Count());
        }

        public Policy? GetPolicy(int id)
        {
            return _context.Policies
                .Where(p => p.Id == id)
                .Include(p => p.PolicyCounties)
                .ThenInclude(p => p.PolicyGrids)
                .ThenInclude(p => p.PolicyIntervals)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();
        }

        public string? GetCountyNameByPolicyId(int policyId, string stateCode)
        {
            return (from pc in _context.PolicyCounties
                    join c in _context.USDACounties on pc.CountyCode equals c.Code
                    where pc.PolicyId == policyId && c.StateCode == stateCode
                    select c.Name).ToList().Distinct().FirstOrDefault();
        }

        public List<PolicyCounties> GetCountyAndPolicyId()
        {
            List<PolicyCounties> Pc = (from pc in _context.PolicyCounties
                                       join c in _context.USDACounties on pc.CountyCode equals c.Code
                                       select new PolicyCounties { Name = c.Name, PolicyId = pc.PolicyId, StateCode = c.StateCode }).ToList();

            return Pc;

        }


        public IEnumerable<USDACounty> GetCountiesByStateCode(string stateCode)
        {
            return (from c in _context.USDACounties
                    where c.StateCode == stateCode
                    select c).ToList();
        }

        public string? GetCropNameByPolicyId(int policyId)
        {
            return (from pc in _context.PolicyCounties
                    join cm in _context.USDACommodity on pc.CommodityCode equals cm.CommodityCode
                    where pc.PolicyId == policyId
                    select cm.CommodityName).AsNoTracking().ToList().Distinct().FirstOrDefault();
        }

        public List<PolicyAndCommodity> GetCropNameAndPolicyId()
        {
            List<PolicyAndCommodity> PAC = (from pc in _context.PolicyCounties
                                            join cm in _context.USDACommodity on pc.CommodityCode equals cm.CommodityCode
                                            select new PolicyAndCommodity { CommodityName = cm.CommodityName, PolicyId = pc.PolicyId }).ToList();
            return PAC;
        }

        public IEnumerable<LandUseCodeMaster> GetLandUseCodes()
            => _context.LandUseCodeMaster.ToList();

        public List<ApprovedInsuranceProvider> GetAIPs(bool? adminAgent = false)
        {
            var isAdmin = HasAdminAccess(adminAgent);
            List<int> aipIds = !isAdmin
                                    ? _context.AgentsAIPMapping.Where(x => x.AgentId == LoggedInAgentId)
                                        .Select(x => x.AIPId)
                                        .ToList()
                                    : new List<int>();

            return _context.ApprovedInsuranceProviders.Where(api => isAdmin || aipIds.Contains(api.Id)).ToList();
        }

        public ApprovedInsuranceProvider? GetAIP(int id)
        {
            return _context.ApprovedInsuranceProviders
                .Where(a => a.Id == id)
                .FirstOrDefault();
        }

        public USDAState? GetStateDetails(string code)
        {
            return _context.USDAStates.Where(s => s.Code == code).FirstOrDefault();
        }

        public List<Agent> GetAgents()
        {

            return _context.Agents
                .Select(a => new Agent
                {
                    Id = a.Id,
                    FirstName = a.FirstName,
                    LastName = a.LastName,
                    MiddleName = a.MiddleName,
                    PhoneNumber = a.PhoneNumber,
                    PhoneExtensionNumber = a.PhoneExtensionNumber,
                    Email = a.Email,
                    IsActive = a.IsActive
                })
                .ToList();
        }
        public List<AgentsDetails> GetAgentsDetails(bool? adminAgent = false)
        {
            var isAdmin = HasAdminAccess(adminAgent);
            return _context.AgentsDetails.Where(a => (isAdmin || a.Email == LoggedInAgentEmailId)).ToList();
        }

        public AgentsDetails GetAgentDetail(int agentId)
        {
            AgentsDetails agentsDetails = _context.AgentsDetails.Where(x => x.AgentId == agentId).First();

            return agentsDetails;
        }

        public List<AgentsAddressDetails> GetAgentAddressDetails(int agentId)
        {
            return _context.AgentsAddressDetails.Where(x => x.AgentId == agentId).ToList();
        }

        public List<AgentsPhoneDetails> GetAgentsPhoneDetail(int agentId)
        {
            return _context.AgentsPhoneDetails.Where(x => x.AgentId == agentId).ToList();
        }

        public List<AgentsAIPMapping> GetAgentsAIPDetail(int agentId)
        {
            return _context.AgentsAIPMapping.Include(x => x.ApprovedInsuranceProviders).Where(x => x.AgentId == agentId).ToList();
        }

        public List<AgentsLicenseDetails> GetAgentsLicenseDetail(int agentId)
        {
            return _context.AgentsLicenseDetails.Where(x => x.AgentId == agentId).ToList();
        }

        public List<AgentsAIPContactDetails> GetAgentsAIPContactsDetail(int agentId)
        {
            return _context.AgentsAIPContactDetails.Where(x => x.AgentId == agentId).ToList();
        }

        public Agent? GetAgent(int id)
        {
            return _context.Agents
                .Where(a => a.Id == id)
                .FirstOrDefault();
        }

        public AgentsDetails? GetAgentByEmail(string emailId)
        {
            return _context.AgentsDetails
                .Where(a => a.Email == emailId)
                .FirstOrDefault();
        }

        public List<Quotation> GetQuotations(int take, int skip, string? taxId, string? entityType, string? aipCode, string? agentEmailAddress, bool? adminAgent)
        {
            var isAdmin = HasAdminAccess(adminAgent);
            Expression<Func<Quotation, bool>> filter = q => ((taxId == null || q.EntityTaxId == taxId) &&
                                                            (entityType == null || q.EntityType == entityType) &&
                                                            (aipCode == null || q.AIPCode == aipCode) &&
                                                            (isAdmin || q.AgentEmail == LoggedInAgentEmailId) &&
                                                            q.SourceTypeCode.ToLower() == "gp");

            if (agentEmailAddress != null)
            {
                var allowedToAccessAgentData = isAdmin || agentEmailAddress == LoggedInAgentEmailId;
                filter = q => ((taxId == null || q.EntityTaxId == taxId) &&
                                (entityType == null || q.EntityType == entityType) &&
                                (aipCode == null || q.AIPCode == aipCode) &&
                                allowedToAccessAgentData && q.AgentEmail == agentEmailAddress &&
                                q.SourceTypeCode.ToLower() == "gp");
            }

            var quotations = _context.Quotations
                              .Where(filter)
                              .Select(x => new Quotation
                              {
                                  Id = x.Id,
                                  QuotationId = x.QuotationId,
                                  FileName = x.FileName,
                                  QuotationCreatedDate = x.QuotationCreatedDate,
                                  EntityName = x.EntityName,
                                  Commodity = x.Commodity,
                                  CropYear = x.CropYear,
                                  QuotationLastUpdatedDate = x.QuotationLastUpdatedDate,
                                  QuotationStatus = x.QuotationStatus,
                                  CreatedDate = x.CreatedDate,
                                  AgentName = x.AgentName
                              })
                              .OrderByDescending(q => q.Id)
                              //.Skip(skip)
                              //.Take(take)
                              .ToList();

            return quotations;

        }

        public Quotation? GetQuotation(int id)
        {
            return _context.Quotations
                   .Where(q => q.Id == id)
                   .Select(x => new Quotation
                   {
                       Id = x.Id,
                       QuotationId = x.QuotationId,
                       QuotationJSON = x.QuotationJSON,
                       FileName = x.FileName,
                       QuotationCreatedDate = x.QuotationCreatedDate,
                       EntityName = x.EntityName,
                       Commodity = x.Commodity,
                       CropYear = x.CropYear,
                       QuotationLastUpdatedDate = x.QuotationLastUpdatedDate,
                       QuotationStatus = x.QuotationStatus,
                       CreatedDate = x.CreatedDate,
                       AgentName = x.AgentName
                   })
                   .OrderByDescending(q => q.Id)
                   .FirstOrDefault();
        }

        public string? GetM13PolicyProducerName(string policyProducerKey)
        {
            return _context.M13P10PolicyProducer
                .Where(x => x.AIPPolicyProducerKey == policyProducerKey)
                .Select(m => m.BusinessName + m.FirstName + " " + m.LastName).AsNoTracking()
                .FirstOrDefault();
        }

        public List<M13P10PolicyProducer> GetAllM13PolicyProducerName()
        {
            return _context.M13P10PolicyProducer.AsNoTracking().ToList();
        }

        public string? GetM13AgentName(string agentKey)
        {
            return _context.M13P55InsuranceAgent
                .Where(x => x.AIPInsuranceAgentKey == agentKey)
                .Select(m => m.FirstName + " " + m.LastName).AsNoTracking()
                .FirstOrDefault();
        }

        public List<M13P55InsuranceAgent> GetAllM13Agents()
        {
            return _context.M13P55InsuranceAgent.AsNoTracking().ToList();
        }

        public List<Customer> GetCustomers(CustomerRequestModel request, int take, int skip)
        {
            return _context.Customers
                .Where(c =>
                (request.Id == null || request.Id == c.Id) &&
                (request.Code == null || request.Code == c.Code) &&
                (request.BusinessName == null || EF.Functions.Like(c.BusinessName, "%" + request.BusinessName + '%')) &&
                (request.FirstName == null || EF.Functions.Like(c.FirstName, "%" + request.FirstName + '%')) &&
                (request.LastName == null || EF.Functions.Like(c.LastName, "%" + request.LastName + '%')) &&
                (request.MiddleName == null || request.MiddleName == c.MiddleName) &&
                (request.Suffix == null || request.Suffix == c.Suffix) &&
                (request.Title == null || request.Title == c.Title) &&
                (request.TaxId == null || request.TaxId == c.TaxId) &&
                (request.TaxIdType == null || request.TaxIdType == c.TaxIdType) &&
                (request.AddressLine1 == null || request.AddressLine1 == c.AddressLine1) &&
                (request.AddressLine2 == null || request.AddressLine2 == c.AddressLine2) &&
                (request.CityName == null || request.CityName == c.CityName) &&
                (request.StateAbbreviation == null || request.StateAbbreviation == c.StateAbbreviation) &&
                (request.ZipCode == null || request.ZipCode == c.ZipCode) &&
                (request.ZipExtensionCode == null || request.ZipExtensionCode == c.ZipExtensionCode) &&
                (request.PhoneNumber == null || request.PhoneNumber == c.PhoneNumber) &&
                (request.PhoneExtensionNumber == null || request.PhoneExtensionNumber == c.PhoneExtensionNumber) &&
                (request.EntityTypeCode == null || request.EntityTypeCode == c.EntityTypeCode) &&
                (request.ExternalId == null || request.ExternalId == c.ExternalId) &&
                (request.SourceTypeId == null || request.SourceTypeId == c.SourceTypeId))
                .OrderByDescending(x => x.Id)
                //.Skip(skip).Take(take)
                .Include(x => x.Contacts).AsNoTracking()
                .ToList();
        }

        public List<Contact> GetContacts(ContactRequestModel request, int take, int skip, bool? adminAgent = false)
        {
            return _context.Contacts
                .Where(c =>
                (request.Id == null || request.Id == c.Id) &&
                (request.EntityType == null || request.EntityType == c.EntityType) &&
                (request.TaxIdType == null || request.TaxIdType == c.TaxIdType) &&
                (request.TaxId == null || request.TaxId == c.TaxId) &&
                (request.BusinessName == null || request.BusinessName == c.BusinessName) &&
                (request.FirstName == null || request.FirstName == c.FirstName) &&
                (request.LastName == null || request.LastName == c.LastName) &&
                (request.MiddleName == null || request.MiddleName == c.MiddleName) &&
                (request.Suffix == null || request.Suffix == c.Suffix) &&
                (request.Title == null || request.Title == c.Title) &&
                (request.AddressLine1 == null || request.AddressLine1 == c.AddressLine1) &&
                (request.AddressLine2 == null || request.AddressLine2 == c.AddressLine2) &&
                (request.CityName == null || EF.Functions.Like(c.CityName, "%" + request.CityName + '%')) &&
                (request.StateAbbreviation == null || request.StateAbbreviation == c.StateAbbreviation) &&
                (request.ZipCode == null || request.ZipCode == c.ZipCode) &&
                (request.ZipExtensionCode == null || request.ZipExtensionCode == c.ZipExtensionCode) &&
                (request.PhoneNumber == null || request.PhoneNumber == c.PhoneNumber) &&
                (request.PhoneExtensionNumber == null || request.PhoneExtensionNumber == c.PhoneExtensionNumber))
                .OrderByDescending(x => x.Id)
                //.Skip(skip)
                //.Take(take)
                .Include(x => x.Customer).AsNoTracking()
                .ToList();
        }

        public List<Policy> GetPolicies(PolicyRequestModel request, int take, int skip)
        {
            return _context.Policies
                .Where(p =>
                (request.ReinsuranceYear == null || request.ReinsuranceYear == p.ReinsuranceYear) &&
                (request.AIPCode == null || request.AIPCode == p.AIPCode) &&
                (request.AIPPolicyProducerKey == null || request.AIPPolicyProducerKey == p.AIPPolicyProducerKey) &&
                (request.AIPInsuranceAgentKey == null || request.AIPInsuranceAgentKey == p.AIPInsuranceAgentKey) &&
                (request.PolicyNumber == null || request.PolicyNumber == p.PolicyNumber) &&
                (request.StateCode == null || request.StateCode == p.StateCode)
                )
                .OrderByDescending(x => x.Id)
                .Include(x => x.PolicyCounties)
                .ThenInclude(pc => pc.PolicyGrids)
                .ThenInclude(pg => pg.PolicyIntervals)
                //.Skip(skip)
                //.Take(take)
                .ToList();
        }

        public List<Quotation> GetQuotations(GetQuotationRequestModel request, int take, int skip)
        {
            return _context.Quotations
                .Where(q =>
                 (request.Id == null || request.Id == q.Id) &&
                 (request.QuotationId == null || request.QuotationId == q.QuotationId) &&
                 (request.FileName == null || request.FileName == q.FileName) &&
                 (request.CropYear == null || request.CropYear == q.CropYear) &&
                 (request.EntityName == null || request.EntityName == q.EntityName) &&
                 (request.EntityType == null || request.EntityType == q.EntityType) &&
                 (request.EntityTaxId == null || request.EntityTaxId == q.EntityTaxId) &&
                 (request.EntityTaxIdTypeCode == null || request.EntityTaxIdTypeCode == q.EntityTaxIdTypeCode) &&
                 (request.AgentEmail == null || request.AgentEmail == q.AgentEmail) &&
                 (request.Commodity == null || request.Commodity == q.Commodity) &&
                 (request.AIPCode == null || request.AIPCode == q.AIPCode)
                 )
                 .OrderByDescending(q => q.Id).AsNoTracking()
                 //.Skip(skip)
                 //.Take(take)
                 .ToList();
        }

        public List<TypeValue> GetTypes()
        {
            return _context.TypeValues.ToList();
        }

        public int PostAIP(AIPRequestModel request)
        {
            ApprovedInsuranceProvider aip = _mapper.Map<ApprovedInsuranceProvider>(request);
            aip.CreatedDate = DateTime.Now;
            aip.LastModifiedDate = DateTime.Now;
            _context.ApprovedInsuranceProviders.Add(aip);
            _context.SaveChanges();
            return aip.Id;
        }

        public ApprovedInsuranceProvider? PutAIP(AIPRequestModel aip)
        {
            var entity = _context.ApprovedInsuranceProviders.FirstOrDefault(a => a.Id == aip.Id);
            if (entity != null)
            {
                entity.Name = aip.Name;
                entity.Abbreviation = aip.Abbreviation;
                entity.PhoneNumber = aip.PhoneNumber;
                entity.Email = aip.Email;
                entity.Address = aip.Address;
                entity.Website = aip.Website;
                entity.IsActive = aip.IsActive;
                entity.LastModifiedDate = DateTime.Now;
            }
            _context.SaveChanges();
            return _context.ApprovedInsuranceProviders.FirstOrDefault(a => a.Id == aip.Id);
        }

        public void DeleteAIP(int Id)
        {
            var entity = _context.ApprovedInsuranceProviders.FirstOrDefault(a => a.Id == Id);
            if (entity != null)
            {
                _context.ApprovedInsuranceProviders.Remove(entity);
                _context.SaveChanges();
            }
        }

        public List<USDACommodity>? GetCommodity()
        {
            return _context.USDACommodity.OrderBy(q => q.Id).ToList();
        }

        public List<IntervalCode>? GetIntervalCodes()
        {
            return _context.IntervalCodes.OrderBy(q => q.Id).ToList();
        }

        public List<USDAState>? GetStateCodes()
        {
            return _context.USDAStates.OrderBy(q => q.Id).ToList();
        }

        #region Quotation Documents

        public async Task<IEnumerable<QuotationsDocumentsMapping>?> GetQuotationsDocumentsAsync(int quotationId, int take, int skip)
        {
            return await _context.QuotationsDocumentsMapping
                        .Include(qd => qd.Document)
                        .Where(qd => qd.QuoteId == quotationId && qd.Document.IsActive)
                        //.Skip(skip)
                        //.Take(take)
                        .ToListAsync();
        }

        public async Task<IEnumerable<QuotationsDocumentsMapping>?> UploadQuotationDocumentAsync(List<IFormFile> files, int quotationId, int apiInsuranceAgentKey)
        {
            if (!files.Any())
            {
                return null;
            }

            var result = new List<QuotationsDocumentsMapping>();
            foreach (var file in files)
            {
                if (file == null) continue;

                var fileBytes = await GetBytesAsync(file);
                var azureFilename = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filepath = $"quotationdocument/{quotationId}/{azureFilename}";
                filepath = await _quotationDocumentBlobHelper.UploadBlobAsync(_quotationDocumentContainer, filepath, fileBytes);

                var docResult = await _context.Documents.AddAsync(new Documents
                {
                    AzureFilename = azureFilename,
                    DocName = file.FileName,
                    filepath = filepath,
                    filetype = Path.GetExtension(file.FileName),
                    IsActive = true
                });
                await _context.SaveChangesAsync();

                var docId = docResult.Entity.DocId;
                var quotationDocResult = await _context.QuotationsDocumentsMapping.AddAsync(new QuotationsDocumentsMapping
                {
                    DocId = docId,
                    CreatedDate = DateTime.Now,
                    LastUpdatedDate = DateTime.Now,
                    QuoteId = quotationId,
                    AIPInsuranceAgentKey = apiInsuranceAgentKey
                });
                await _context.SaveChangesAsync();

                result.Add(quotationDocResult.Entity);
            }

            return result;
        }

        public async Task<(byte[] fileData, string fileName, string fileType)> DownloadQuotationDocumentAsync(int quotationId, int docId)
        {
            var quoteDoc = await _context.QuotationsDocumentsMapping
                                .Include(qd => qd.Document)
                                .FirstOrDefaultAsync(qd => qd.QuoteId == quotationId && qd.DocId == docId) ?? throw new KeyNotFoundException();

            var entity = quoteDoc.Document;

            var fileBytes = await _quotationDocumentBlobHelper.GetBlobAsByteArrayAsync(_quotationDocumentContainer, entity.filepath);
            return (fileBytes, entity.DocName, entity.filetype);
        }

        public async Task DeleteQuotationDocumentAsync(int quotationId, int docId)
        {
            var quoteDoc = await _context.QuotationsDocumentsMapping
                                .Include(qd => qd.Document)
                                .FirstOrDefaultAsync(qd => qd.QuoteId == quotationId && qd.DocId == docId) ?? throw new KeyNotFoundException(); ;

            var entity = quoteDoc.Document;
            entity.IsActive = false;
            _context.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> AddAgentLicensesAndAIPContacts(List<AgentsLicenseDetails> agentsLicenseDetails, List<AgentsAIPContactDetails> aIPContactsMappings, int agentId, string? stateCode, string LicenseDate)
        {
            if (stateCode is not null && LicenseDate is not null)
            {
                var agentdetails = _context.AgentsDetails.Where(x => x.AgentId == agentId).FirstOrDefault();
                agentdetails.StateCode = stateCode;
                agentdetails.LicenseTillDate = LicenseDate == "" ? DateTime.UtcNow : DateTime.Parse(LicenseDate);
                _context.Update(agentdetails);
                await _context.SaveChangesAsync();
            }
            if (!agentsLicenseDetails.Any() && !aIPContactsMappings.Any())
            {
                return true;
            }
            var removeLicense = _context.AgentsLicenseDetails.Where(a => a.AgentId == agentId).ToList();
            var removeAipContacts = _context.AgentsAIPContactDetails.Where(a => a.AgentId == agentId).ToList();
            if (removeLicense.Any())
            {
                _context.AgentsLicenseDetails.RemoveRange(removeLicense);
            }
            if (removeAipContacts.Any())
            {
                _context.AgentsAIPContactDetails.RemoveRange(removeAipContacts);
            }
            await _context.AgentsLicenseDetails.AddRangeAsync(agentsLicenseDetails);
            await _context.AgentsAIPContactDetails.AddRangeAsync(aIPContactsMappings);
            //_context.SaveChanges();
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> GetActualHistoryMemoryStream(string Baseurl, int year, string ids, bool allInOne = true, bool wsrCompleted = false, bool currentInterval = false, bool nextInterval = false)
        {
            MemoryStream pdf = new MemoryStream();
            using (var client = new HttpClient())
            {
                ActualHistoryReportRequest request = new()
                {
                    Year = year,
                    Ids = ids,
                    AllInOne = allInOne,
                    WsrCompleted = wsrCompleted,
                    CurrentInterval = currentInterval,
                    NextInterval =  nextInterval,
                    AgentEmail = _clientContext.Email
                };
                //Passing service base url  
                client.BaseAddress = new Uri(Baseurl);
                client.Timeout = TimeSpan.FromMinutes(10);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var apiKey = configuration.GetValue<string>("CIMSReports:APIKey");
                client.DefaultRequestHeaders.Add("ApiKey", apiKey);
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                //Sending request to find web api REST service resource GetDepartments using HttpClient  
                //HttpResponseMessage Res = await client.GetAsync("Queue/GetActualHistoryReportStream?year=" + year + "&ids=" + ids + "&allInOne=" + allInOne + "&wsrCompleted=" + wsrCompleted + "&currentInterval=" + currentInterval + "&nextInterval=" + nextInterval + "&agentEmail=" + _clientContext.Email);
                HttpResponseMessage response = await client.PostAsync(Baseurl + "ActualHistoryReport/Queue", content);
                
                if (response.IsSuccessStatusCode)
                {
                    //Res.Content.CopyToAsync(pdf).Wait();
                    return true;
                }
                return false;
            }
        }

        public async Task<bool> GetAIPLossRegistryMemoryStream(string Baseurl, AIPLossRegistryGridFilter aipLossRegistryFilter)
        {
            aipLossRegistryFilter.AgentEmail = _clientContext.Email;
            MemoryStream pdf = new();
            using var client = new HttpClient();
            client.BaseAddress = new Uri(Baseurl);
            client.DefaultRequestHeaders.Clear();
            client.Timeout = TimeSpan.FromSeconds(600);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var apiKey = configuration.GetValue<string>("CIMSReports:APIKey");
            client.DefaultRequestHeaders.Add("ApiKey", apiKey);
            var json = JsonConvert.SerializeObject(aipLossRegistryFilter);

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(Baseurl + "LossRegistryReport/Queue", content);

            return response.IsSuccessStatusCode;
        }

        public async Task<AIPLossRegistryReportFilterResponse> GetAIPLossRegistryReportFilters(bool adminAgent)
        {
            AIPLossRegistryReportFilterResponse aIPLossRegistryReportFilter = new AIPLossRegistryReportFilterResponse();
            //getting commodity of year 2023 only
            var commodityList = _context.USDACommodity.Where(c => c.ReinsuranceYear == "2023"
                    && (c.CommodityCode.Equals("1191") | c.CommodityCode.Equals("0088") | c.CommodityCode.Equals("0332")));

            List<int> reinsuranceYearList = new();
            var policyYears = _context.M13P14InsuranceInForce.AsEnumerable().GroupBy(p => p.ReinsuranceYear);
            foreach (var item in policyYears)
            {
                reinsuranceYearList.Add(Convert.ToInt32(item.Key));
            }
            reinsuranceYearList = reinsuranceYearList.OrderByDescending(y => y).ToList();
            var policyHolderList = _context.M13P10PolicyProducer;
            //var countyList = _context.USDACounties.ToList();
            var AIPList = _context.ApprovedInsuranceProviders.ToList();
            var agentsList = GetAgentsDetails(adminAgent);//_cimsService.GetAgentsDetails(adminAgent)
            var states = GetStateCodes();

            foreach (var item in commodityList)
            {
                aIPLossRegistryReportFilter?.CommodityList?.Add(new SelectionModel { Id = item.CommodityCode, Value = item.CommodityCode, Description = item.CommodityName });
            }

            foreach (var item in reinsuranceYearList)
            {
                aIPLossRegistryReportFilter?.PolicyYears?.Add(new SelectionModel { Id = item.ToString(), Value = item.ToString(), Description = item.ToString() });
            }

            foreach (var item in AIPList)
            {
                aIPLossRegistryReportFilter?.AIPsList?.Add(new SelectionModel { Id = item.Abbreviation, Value = item.Abbreviation, Description = item.Name });
            }

            foreach (var item in agentsList)
            {
                aIPLossRegistryReportFilter?.AgentsList?.Add(new SelectionModel { Id = item.AgentId.ToString(), Value = string.Concat(item.FirstName, " ", item.LastName), Description = string.Empty });
            }
            foreach (var item in states)
            {
                aIPLossRegistryReportFilter?.StatesList?.Add(new SelectionModel { Id = item.Code, Value = item.Name, Description = item.Abbreviation });
            }

            return await Task.Run(() => aIPLossRegistryReportFilter);
        }

        public List<SelectionModel> GetCountyByStateCode(string stateCode)
        {
            List<SelectionModel> countyList = new();
            var counties = this.GetCountiesByStateCode(stateCode);
            foreach (var item in counties)
            {
                countyList.Add(new SelectionModel { Id = item.Code, Value = item.Name, Description = item.Code });
            }
            return countyList;
        }

        #endregion

        public bool HasAdminAccess(bool? requestAdminAccess)
        {
            var isRequestedForAdminAccess = requestAdminAccess != null && requestAdminAccess.Value;

            var isAdminAgent = _context.AgentsDetails.Any(x => x.Email == LoggedInAgentEmailId && x.IsAdmin == true);

            if (!isAdminAgent && isRequestedForAdminAccess)
            {
                // Don't allow agent to access as admin if agent doesn't have admin rights
                return false;
            }

            return isRequestedForAdminAccess;
        }

        public void SetAgentLoginType(int agentId, bool isAdmin)
        {
            var agent = _context.AgentsDetails.FirstOrDefault(a => a.AgentId == agentId);
            if (agent == null)
            {
                return;
            }
            agent.IsAdmin = isAdmin;

            _context.Update(agent);
            _context.SaveChanges();
        }

        #region Helpers

        private List<Customer> GetAgentPolicyHolder(int take, int skip, string customerTypeId, int? agentId, bool? adminAgent = true)
        {
            var isAdmin = HasAdminAccess(adminAgent);
            if (adminAgent == false)
                agentId = LoggedInAgentId;


            var agentAllowedToView = isAdmin || LoggedInAgentId == agentId;

            if (agentId == null || agentId == 0)
            {
                agentId = !isAdmin ? LoggedInAgentId : null;
                agentAllowedToView = true;
            }

            List<string> aipInsuranceAgentKeys = agentId != null && agentId > 0
                                                    ? _context.AgentsAIPMapping.Where(x => x.AgentId == agentId)
                                                                    .Select(x => x.AIPInsuranceAgentKey.ToString())
                                                                    .ToList()
                                                    : new List<string>();

            if (!isAdmin && aipInsuranceAgentKeys.Count == 0)
            {
                return new List<Customer>();
            }
            var customers = _context.Customers.Where(c => c.CustomerType == customerTypeId).ToList();

            if (adminAgent == true && aipInsuranceAgentKeys.Count == 0)
                return customers;


            var policies = _context.Policies.Where(p => agentAllowedToView && (!aipInsuranceAgentKeys.Any()
                                                       || aipInsuranceAgentKeys.Contains(p.AIPInsuranceAgentKey)))
                                            .Select(p => p.AIPPolicyProducerKey).ToList();

            if (policies != null && policies.Any())
            {

                List<Customer> subset = new List<Customer>();

                foreach (Customer customer in customers)
                {
                    if (policies.Any(p => customer.ExternalId.Contains(p)))
                        subset.Add(customer);
                }

                return subset;
            }

            return new List<Customer>();
        }

        private async Task<byte[]> GetBytesAsync(IFormFile file)
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return stream.ToArray();
        }


        #endregion
    }

    public static class IQueryableExtension
    {
        public static IQueryable<T> ApplyOrder<T>(this IQueryable<T> source, string column, string direction)
        {
            if (string.IsNullOrWhiteSpace(column))
                return source;

            var pinfo = typeof(T).GetProperty(column);
            if (pinfo == null)
                return source;

            var order = (direction == "desc") ? $"{column} desc" : column;

            return source.OrderBy(order);
        }
    }
}
