using WSR.Azure.Database.Models;
using WSR.Azure.Database.Models.CIMS;
using WSR.Azure.Database.Models.M13;
using WSR.Azure.Database.Models.USDA;
using WSR.Domain.Model.CIMSv2;

namespace WSR.CIMS
{
    public interface ICIMSV2Service
    {
        (List<M13P10PolicyProducer>, List<M13P10APolicyProducerAddress>) GetPolicyHolders(int skip, int take, IDictionary<string, string> filters);

        (M13P10PolicyProducer?, M13P10APolicyProducerAddress?, List<M13P10BPolicyProducerOtherPerson>?) GetPolicyHolder(string aipPolicyProducerKeys, string year);

        PolicyProducerEmailAddressResponse UpsertPolicyProducerEmail(PolicyProducerEmailAddressRequest request);

        List<PolicyProducerEmailAddress>? GetPolicyProducerEmails(List<string> PolicyProducerKeys);

        List<(string, string)>? GetPolicyProducerCommodities(List<string> AIPPolicyProducerKey);

        GetPoliciesResponseViewModel GetPolicies(int skip, int take, IDictionary<string, string> fitlers);

        GetPolicyDetailsResponseViewModel GetPolicy(string[] AIPInsuranceInForceKeys, string ReinsuranceYear);

        List<USDAState>? GetStateDetails(List<string> stateCodes);

        List<USDACounty>? GetCountyDetails(List<(string stateCode, string countyCode)> countCodes);

        List<M13P55InsuranceAgent>? GetAgentDetails(List<(string aipInsuranceAgentKey, string reinsuranceYear)> agentKeys);

        List<USDACommodity>? GetCommodityDetails(List<string> commodityCode);

        public List<TypeValue> GetTypeDetails();
    }
}
