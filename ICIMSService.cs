using Microsoft.AspNetCore.Http;
using WSR.Azure.Database.Models;
using WSR.Azure.Database.Models.M13;
using WSR.Azure.Database.Models.Policy;
using WSR.Azure.Database.Models.USDA;
using WSR.Domain.Model;
using WSR.Domain.Model.CIMSReports;
using WSR.Domain.Model.CIMSReports.Request;

namespace WSR.CIMS
{
    public interface ICIMSService
    {
        List<Customer> GetPolicyHolders(int take, int skip, string? aipInsuranceAgentKey, int? agentId = 0, bool? adminAgent = true);

        Customer? GetPolicyHolder(int id);

        List<GridProContact> GetGridProContacts(int take, int skip, bool adminAgent);

        GridProContact? GetGridProContact(int id);

        List<Customer> GetContacts(int take, int skip, bool? adminAgent = false);

        Customer? GetContact(int id);

        List<Policy> GetPolicies(int take, int skip, string? aipPolicyProducerKey, string? aipInsuranceAgentKey, string? aipCodev, bool? adminAgent = false);

        Policy? GetPolicy(int id);

        string? GetCountyNameByPolicyId(int policyId, string stateCode);

        IEnumerable<USDACounty> GetCountiesByStateCode(string stateCode);

        string? GetCropNameByPolicyId(int policyId);

        IEnumerable<LandUseCodeMaster> GetLandUseCodes();

        List<Quotation> GetQuotations(int take, int skip, string? taxId, string? entityType, string? aipCode, string? agentEmailAddress, bool? adminAgent);

        Quotation? GetQuotation(int id);

        string? GetM13PolicyProducerName(string policyProducerKey);

        string? GetM13AgentName(string agentKey);

        USDAState? GetStateDetails(string code);

        List<ApprovedInsuranceProvider> GetAIPs(bool? adminAgent = false);

        ApprovedInsuranceProvider? GetAIP(int id);

        List<Agent> GetAgents();

        Agent? GetAgent(int id);

        AgentsDetails? GetAgentByEmail(string emailId);

        List<Customer> GetCustomers(CustomerRequestModel request, int take, int skip);

        List<Contact> GetContacts(ContactRequestModel request, int take, int skip, bool? adminAgent = false);

        List<Policy> GetPolicies(PolicyRequestModel request, int take, int skip);

        List<Quotation> GetQuotations(GetQuotationRequestModel request, int take, int skip);
        List<USDACommodity> GetCommodity();
        List<IntervalCode> GetIntervalCodes();
        List<USDAState> GetStateCodes();

        List<TypeValue> GetTypes();

        int PostAIP(AIPRequestModel request);

        ApprovedInsuranceProvider? PutAIP(AIPRequestModel aip);

        void DeleteAIP(int Id);
        List<AgentsDetails> GetAgentsDetails(bool? adminAgent = false);
        AgentsDetails GetAgentDetail(int agentId);
        List<AgentsAddressDetails> GetAgentAddressDetails(int agentId);
        List<AgentsPhoneDetails> GetAgentsPhoneDetail(int agentId);
        List<AgentsAIPMapping> GetAgentsAIPDetail(int agentId);

        Task<IEnumerable<QuotationsDocumentsMapping>?> GetQuotationsDocumentsAsync(int quotationId, int take, int skip);

        Task<IEnumerable<QuotationsDocumentsMapping>?> UploadQuotationDocumentAsync(List<IFormFile> files, int quotationId, int apiInsuranceAgentKey);

        Task<(byte[] fileData, string fileName, string fileType)> DownloadQuotationDocumentAsync(int quotationId, int docId);

        Task DeleteQuotationDocumentAsync(int quotationId, int docId);

        Contact? GetContactDetails(int id);

        bool HasAdminAccess(bool? requestAdminAccess);
        List<AgentsLicenseDetails> GetAgentsLicenseDetail(int agentId);
        List<AgentsAIPContactDetails> GetAgentsAIPContactsDetail(int agentId);

        Task<bool> AddAgentLicensesAndAIPContacts(List<AgentsLicenseDetails> agentsLicenseDetails, List<AgentsAIPContactDetails> aIPContactsMappings, int agentId, string stateCode, string LicenseDate);

        Task<bool> GetActualHistoryMemoryStream(string Baseurl, int year, string ids, bool allInOne = true, bool wsrCompleted = false, bool currentInterval = false, bool nextInterval = false);

        void SetAgentLoginType(int agentId, bool isAdmin);
        List<M13P10PolicyProducer> GetAllM13PolicyProducerName();
        List<M13P55InsuranceAgent> GetAllM13Agents();

        List<PolicyCounties> GetCountyAndPolicyId();
        List<PolicyAndCommodity> GetCropNameAndPolicyId();
        List<ActualHistoryReportView> GetActualHistory(int year, bool? adminAgent = false);

        (List<Policy>, int) GetFilteredPolicies(int take, int skip, string? filterBy, string? filterText, bool? adminAgent = false, string orderBy = "PolicyNumber", string orderDirection = "desc");

        Task<AIPLossRegistryReportFilterResponse> GetAIPLossRegistryReportFilters(bool adminAgent);
        Task<bool> GetAIPLossRegistryMemoryStream(string Baseurl, AIPLossRegistryGridFilter aipLossRegistryReportFilter);
        List<SelectionModel> GetCountyByStateCode(string stateCode);
    }
}