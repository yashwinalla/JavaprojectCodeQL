using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using WSR.Azure.Database.Context;
using WSR.Azure.Database.Models;
using WSR.Azure.Database.Models.CIMS;
using WSR.Azure.Database.Models.M13;
using WSR.Domain.Services;
using WSR.Domain.Model.CIMSv2;
using WSR.Azure.Database.Models.USDA;
using System.Reflection;
using WSR.CIMS.Helper;

namespace WSR.CIMS.Implementation
{
    public class CIMSV2Service : ICIMSV2Service
    {
        private readonly IClientContext _clientContext;
        private WSRDBContext _databaseContext;
        private readonly ILogger<CIMSV2Service> _logger;

        public CIMSV2Service(IClientContext clientContext, WSRDBContext databaseContext, ILogger<CIMSV2Service> logger)
        {
            _clientContext = clientContext;
            _databaseContext = databaseContext;
            _logger = logger;
        }

        public (List<M13P10PolicyProducer>, List<M13P10APolicyProducerAddress>) GetPolicyHolders(int skip, int take, IDictionary<string, string> filters)
        {
            string? agentEmail = string.Empty;
            filters.TryGetValue("AgentEmail", out agentEmail);
            var producerKeys = GetAuthorizedAIPPolicyProducerKeys(agentEmail).ToList();

            var policyProducers = _databaseContext.M13P10PolicyProducer
                     .Where(producer => producerKeys.Contains(producer.AIPPolicyProducerKey))
                     .Distinct()
                     .OrderByDescending(producer => producer.ReinsuranceYear).ThenByDescending(p => p.PolicyNumber)
                     .Skip(skip).Take(take).ToList();

            List<M13P10APolicyProducerAddress> policyProducerAddress = _databaseContext.M13P10APolicyProducerAddress
                    .Where(address => policyProducers.Select(producer => producer.AIPPolicyProducerKey).Any(producerkey => producerkey == address.AIPPolicyProducerKey)).ToList();

            return (policyProducers, policyProducerAddress);
        }

        public (M13P10PolicyProducer?, M13P10APolicyProducerAddress?, List<M13P10BPolicyProducerOtherPerson>?) GetPolicyHolder(string aipPolicyProducerKeys, string year)
        {
            var producers = aipPolicyProducerKeys.Split(",");
            var policyProducer = _databaseContext.M13P10PolicyProducer.Where(producer => producers.Contains(producer.AIPPolicyProducerKey) && producer.ReinsuranceYear == year).FirstOrDefault() ?? new();
            var policyProducerAddress = _databaseContext.M13P10APolicyProducerAddress.Where(producer => policyProducer.AIPPolicyProducerKey == producer.AIPPolicyProducerKey && producer.ReinsuranceYear == year).FirstOrDefault() ?? new();
            var policyProducerOtherPerson = _databaseContext.M13P10BPolicyProducerOtherPerson.Where(producer => policyProducer.AIPPolicyProducerKey == producer.AIPPolicyProducerKey && producer.ReinsuranceYear == year).ToList();

            return (policyProducer, policyProducerAddress, policyProducerOtherPerson);
        }

        public PolicyProducerEmailAddressResponse UpsertPolicyProducerEmail(PolicyProducerEmailAddressRequest request)
        {
            var producerEmails = _databaseContext.PolicyProducerEmailAddresses.Where(producer => producer.PolicyProducerKey == request.PolicyProducerKey).AsNoTracking().ToList();

            var newEmail = request.EmailAddresses.Where(e => e.Id == 0);
            if(newEmail.Any())
            {
                foreach (var email in newEmail)
                {
                    _databaseContext.PolicyProducerEmailAddresses.Add(new()
                    {
                        PolicyProducerKey = request.PolicyProducerKey,
                        EmailAddress = email.EmailAddress.ToUpper(),
                        CreatedBy = _clientContext.Email,
                        CreatedDate = DateTime.Now
                    });
                    _databaseContext.SaveChanges();
                }
            }

            var updateEmail = request.EmailAddresses.Where(e => e.Id != 0);
            if(updateEmail.Any())
            {
                foreach (var email in updateEmail)
                {
                    var existingEntry = producerEmails.Where(e => e.Id == email.Id).FirstOrDefault();
                    if(existingEntry!= null)
                    {
                        existingEntry.LastUpdatedBy = _clientContext.Email;
                        existingEntry.LastUpdatedDate = DateTime.Now;
                        existingEntry.EmailAddress = email.EmailAddress.ToUpper();
                        _databaseContext.PolicyProducerEmailAddresses.Update(existingEntry);
                        _databaseContext.SaveChanges();
                    }
                }
            }

            var deleteEmail = producerEmails.Where(e => !updateEmail.Any(u => u.Id == e.Id));
            if(deleteEmail.Any())
            {
                foreach (var email in deleteEmail)
                {
                    _databaseContext.PolicyProducerEmailAddresses.Remove(email);
                    _databaseContext.SaveChanges();
                }
            }

            producerEmails = _databaseContext.PolicyProducerEmailAddresses.Where(producer => producer.PolicyProducerKey == request.PolicyProducerKey).AsNoTracking().ToList();
            List<PolicyProducerEmailWithIdResponse> respose = producerEmails.Select(e =>  new PolicyProducerEmailWithIdResponse() { Id = e.Id, EmailAddress = e.EmailAddress }).ToList();
            return new() { PolicyProducerKey = request.PolicyProducerKey, Email = respose };
        }

        public List<PolicyProducerEmailAddress>? GetPolicyProducerEmails(List<string> PolicyProducerKeys)
        {
            return _databaseContext.PolicyProducerEmailAddresses.Where(producer => PolicyProducerKeys.Any(p => p == producer.PolicyProducerKey)).ToList();
        }

        public List<(string, string)> GetPolicyProducerCommodities(List<string> aipPolicyProducerKeys)
        {
            var producerCommodity = _databaseContext.M13P14InsuranceInForce.Where(i => aipPolicyProducerKeys.Any(c => c == i.AIPPolicyProducerKey)).Select(i => new { i.CommodityCode, i.AIPPolicyProducerKey }).Distinct();
            var commodities = _databaseContext.USDACommodity.Where(c => c.ReinsuranceYear == "2023");

            var result = (from producer in producerCommodity
                          join commodity in commodities on producer.CommodityCode equals commodity.CommodityCode
                          select new { producer.AIPPolicyProducerKey, commodity.CommodityAbbreviation }).ToList();

            return result.Select(r => (r.AIPPolicyProducerKey, r.CommodityAbbreviation)).ToList();
            //return _databaseContext.USDACommodity.Where(c => commodityCodes.Any(code => code == c.CommodityCode) && c.ReinsuranceYear == "2023").Select(c => c.CommodityAbbreviation).ToList();
        }

        public GetPoliciesResponseViewModel GetPolicies(int skip, int take, IDictionary<string, string> filters)
        {
            var policyProduceKeys = GetAuthorizedAIPPolicyProducerKeys();
            var filteredPolicies = _databaseContext.M13P10PolicyProducer.Where(producer => policyProduceKeys.Any(keys => keys == producer.AIPPolicyProducerKey));
            filteredPolicies = ApplyFilters(filteredPolicies, filters);
            var filteredInsurances = _databaseContext.M13P14InsuranceInForce.Where(insurance => policyProduceKeys.Any(key => key == insurance.AIPPolicyProducerKey));
            filteredInsurances = ApplyFilters(filteredInsurances, filters);
            var query = (from policy in filteredPolicies
                         join insurance in filteredInsurances on new { key = policy.AIPPolicyProducerKey, year = policy.ReinsuranceYear } equals new { key = insurance.AIPPolicyProducerKey, year = insurance.ReinsuranceYear }
                         where new[] { "0088", "1191", "0332" }.Contains(insurance.CommodityCode)
                         orderby policy.ReinsuranceYear descending, policy.PolicyNumber descending
                         select new { policy.ReinsuranceYear, policy.PolicyNumber, policy.AIPCode, policy.BusinessName, policy.FirstName, policy.LastName, policy.LocationStateCode, insurance.AIPInsuranceAgentKey, insurance.CommodityCode, insurance.LocationCountyCode, insurance.AIPInsuranceInForceKey });

            var allPolicies = query.GroupBy(g => new { g.ReinsuranceYear, g.PolicyNumber, g.AIPCode, g.BusinessName, g.FirstName, g.LastName, g.LocationStateCode, g.AIPInsuranceAgentKey, g.CommodityCode })
                        .Select(i => new PoliciesGridResponseViewModelcs()
                        {
                            AIPCode = i.Key.AIPCode,
                            PolicyNumber = i.Key.PolicyNumber,
                            ReinsuranceYear = i.Key.ReinsuranceYear,
                            InsuredName = i.Key.BusinessName + i.Key.FirstName + " " + i.Key.LastName,
                            LocationStateCode = i.Key.LocationStateCode,
                            AIPInsuranceAgentKey = i.Key.AIPInsuranceAgentKey,
                            CommodityCode = i.Key.CommodityCode,
                            LocationCountyCode = string.Join(',', i.Select(ag => ag.LocationCountyCode).Distinct().ToArray()),
                            AIPInsuranceInForceKeys = string.Join(',', i.Select(ag => ag.AIPInsuranceInForceKey).Distinct().ToArray())
                        });

            var RangedPolicies = allPolicies.OrderByDescending(p=>p.ReinsuranceYear).Skip(skip).Take(take).AsSplitQuery().ToList();
            var count = allPolicies.AsSplitQuery().ToList().Count();

            return new() { count = count, Policies = RangedPolicies.ToList() };
        }

        public GetPolicyDetailsResponseViewModel GetPolicy(string[] aipInsuranceInForceKeys, string reinsuranceYear)
        {
            var insurance = _databaseContext.M13P14InsuranceInForce.Where(insurance => insurance.ReinsuranceYear == reinsuranceYear && aipInsuranceInForceKeys.Contains(insurance.AIPInsuranceInForceKey)).ToList();
            var acreage = _databaseContext.M13P11Acreage.Where(acreage => acreage.ReinsuranceYear == reinsuranceYear && aipInsuranceInForceKeys.Contains(acreage.AIPInsuranceInForceKey)).ToList();
            var producer = _databaseContext.M13P10PolicyProducer.Where(producer => producer.ReinsuranceYear == reinsuranceYear && insurance.First().AIPPolicyProducerKey == producer.AIPPolicyProducerKey).FirstOrDefault() ?? new();

            var result = CIMSPolicyDetailsHelper.GetPolicyDetails(producer, insurance, acreage);
            return UpdatePolicyDetails(result);
        }

        public List<USDAState>? GetStateDetails(List<string> stateCodes)
        {
            return _databaseContext.USDAStates.Where(state => stateCodes.Any(s => s == state.Code)).ToList();
        }

        public List<USDACounty>? GetCountyDetails(List<(string stateCode, string countyCode)> stateCodes)
        {
            return _databaseContext.USDACounties.AsEnumerable().Where(county => stateCodes.Any(s => s.stateCode == county.StateCode && s.countyCode.Split(',').Contains(county.Code))).ToList();
        }

        public List<M13P55InsuranceAgent>? GetAgentDetails(List<(string aipInsuranceAgentKey, string reinsuranceYear)> agetnKeys)
        {
            return _databaseContext.M13P55InsuranceAgent.AsEnumerable().Where(agent => agetnKeys.Any(a => a.aipInsuranceAgentKey == agent.AIPInsuranceAgentKey && a.reinsuranceYear == agent.ReinsuranceYear)).ToList();
        }

        public List<USDACommodity>? GetCommodityDetails(List<string> commodityCodes)
        {
            return _databaseContext.USDACommodity.AsEnumerable().Where(commodity => commodityCodes.Any(c => c == commodity.CommodityCode && commodity.ReinsuranceYear == "2023")).ToList();
        }

        public List<TypeValue> GetTypeDetails()
        {
            return _databaseContext.TypeValues.ToList();
        }

        #region Helpers

        private IQueryable<string> GetAuthorizedAIPPolicyProducerKeys(string? agentEmail = "")
        {
            if (_clientContext.IsAdmin)
            {
                if(string.IsNullOrWhiteSpace(agentEmail))
                {
                    return (from producer in _databaseContext.M13P10PolicyProducer
                            join insurance in _databaseContext.M13P14InsuranceInForce on producer.AIPPolicyProducerKey equals insurance.AIPPolicyProducerKey
                            where new[] { "0088", "1191", "0332" }.Contains(insurance.CommodityCode)
                            select producer.AIPPolicyProducerKey).Distinct();
                }
                else
                {
                    return (from insurance in _databaseContext.M13P14InsuranceInForce
                            join agent in _databaseContext.M13P55AInsuranceAgentAgency on insurance.AIPInsuranceAgentKey equals agent.AIPInsuranceAgentKey
                            where agent.EmailAddress == agentEmail
                            && new[] { "0088", "1191", "0332" }.Contains(insurance.CommodityCode)
                            select insurance.AIPPolicyProducerKey).Distinct();
                }
            }
            else
            {
                return (from insurance in _databaseContext.M13P14InsuranceInForce
                        join agent in _databaseContext.M13P55AInsuranceAgentAgency on insurance.AIPInsuranceAgentKey equals agent.AIPInsuranceAgentKey
                        where agent.EmailAddress == _clientContext.Email
                        && (agentEmail == null || agentEmail == "" || agent.EmailAddress == agentEmail)
                        && new[] { "0088", "1191", "0332" }.Contains(insurance.CommodityCode)
                        select insurance.AIPPolicyProducerKey).Distinct();
            }
        }

        private IQueryable<T> ApplyFilters<T>(IQueryable<T> query, IDictionary<string, string> filters) where T : class
        {
            var ucFilters = filters.Select(val => new KeyValuePair<string, string>(val.Key.ToUpper(), val.Value)).ToDictionary(key => key.Key, val => val.Value);
            query = ApplyCustomFilters(query, ucFilters);
            foreach (var property in typeof(T).GetProperties())
            {
                if (ucFilters.TryGetValue(property.Name.ToUpper(), out string? value))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        query = query.Where(CreateContainsExpression<T>(property.Name, value.Split(',').ToList()));
                    }
                }
            }
            return query;
        }

        private IQueryable<T> ApplyCustomFilters<T>(IQueryable<T> query, IDictionary<string, string> filters) where T : class
        {
            if (typeof(T).Name.Equals(typeof(M13P10PolicyProducer).Name) && filters.TryGetValue(nameof(PoliciesGridResponseViewModelcs.InsuredName).ToUpper(), out string? value))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    query = query.Where(CreateStartsWithExpression<T>(nameof(M13P10PolicyProducer.BusinessName), nameof(M13P10PolicyProducer.FirstName), value));
                }
            }

            if (typeof(T).Name.Equals(typeof(M13P10PolicyProducer).Name) && filters.TryGetValue(nameof(PoliciesGridResponseViewModelcs.StateName).ToUpper(), out string? stateName))
            {
                if (!string.IsNullOrWhiteSpace(stateName))
                {
                    var stateCodes = _databaseContext.USDAStates.Where(state => state.Name.StartsWith(stateName)).Select(state => state.Code);
                    query = query.Where(CreateContainsExpression<T>(nameof(M13P10PolicyProducer.LocationStateCode), stateCodes.ToList()));
                }
            }

            if (typeof(T).Name.Equals(typeof(M13P10PolicyProducer).Name) && filters.TryGetValue(nameof(PoliciesGridResponseViewModelcs.CountyName).ToUpper(), out string? stateCountyFilter))
            {
                if (!string.IsNullOrWhiteSpace(stateCountyFilter))
                {
                    var stateCountyCodes = _databaseContext.USDACounties.Where(county => county.Name.StartsWith(stateCountyFilter)).Select(county => county.StateCode).Distinct();
                    query = query.Where(CreateContainsExpression<T>(nameof(M13P10PolicyProducer.LocationStateCode), stateCountyCodes.ToList()));
                }
            }

            if (typeof(T).Name.Equals(typeof(M13P14InsuranceInForce).Name) && filters.TryGetValue(nameof(PoliciesGridResponseViewModelcs.CountyName).ToUpper(), out string? countyName))
            {
                if (!string.IsNullOrWhiteSpace(countyName))
                {
                    var countyCodes = _databaseContext.USDACounties.Where(county => county.Name.StartsWith(countyName)).Select(county => county.Code);
                    query = query.Where(CreateContainsExpression<T>(nameof(M13P14InsuranceInForce.LocationCountyCode), countyCodes.ToList()));
                }
            }

            if (typeof(T).Name.Equals(typeof(M13P14InsuranceInForce).Name) && filters.TryGetValue(nameof(PoliciesGridResponseViewModelcs.AgentName).ToUpper(), out string? agentName))
            {
                if (!string.IsNullOrWhiteSpace(agentName))
                {
                    var aipAgentKeys = _databaseContext.M13P55InsuranceAgent.Where(agent => agent.FirstName.StartsWith(agentName) || agent.LastName.StartsWith(agentName) || agent.MiddleName.StartsWith(agentName)).Select(agent => agent.AIPInsuranceAgentKey).Distinct();
                    query = query.Where(CreateContainsExpression<T>(nameof(M13P14InsuranceInForce.AIPInsuranceAgentKey), aipAgentKeys.ToList()));
                }
            }

            return query;
        }

        private static Expression<Func<T, bool>> CreateEqualExpression<T>(string propertyName, object value) where T : class
        {
            var param = Expression.Parameter(typeof(T), "p");
            var member = Expression.Property(param, propertyName);
            var constant = Expression.Constant(value);
            var body = Expression.Equal(member, constant);
            return Expression.Lambda<Func<T, bool>>(body, param);
        }

        private static Expression<Func<T, bool>> CreateStartsWithExpression<T>(string propertyName1, string propertyName2, object value) where T : class
        {
            var param = Expression.Parameter(typeof(T), "p");
            var conValue = Expression.Constant(value, typeof(string));
            MethodInfo? method = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });

            var member1 = Expression.Property(param, propertyName1);
            var body1 = Expression.Call(member1, method!, conValue);

            if (!string.IsNullOrEmpty(propertyName2))
            {
                var member2 = Expression.Property(param, propertyName2);
                var body2 = Expression.Call(member2, method!, conValue);

                return Expression.Lambda<Func<T, bool>>(Expression.OrElse(body1, body2), param);
            }

            return Expression.Lambda<Func<T, bool>>(body1, param);
        }

        private static Expression<Func<T, bool>> CreateContainsExpression<T>(string propertyName, List<string> value) where T : class
        {
            var param = Expression.Parameter(typeof(T), "p");
            var conValue = Expression.Constant(value, typeof(List<string>));
            MethodInfo? method = typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public).Where(m => m.Name == "Contains").Where(mtd => mtd.GetParameters().Count() == 2).FirstOrDefault();

            var member = Expression.Property(param, propertyName);
            method = method!.MakeGenericMethod(member.Type);
            var body = Expression.Call(method!, new Expression[] { Expression.Constant(value), member });

            return Expression.Lambda<Func<T, bool>>(body, param);
        }

        private GetPolicyDetailsResponseViewModel UpdatePolicyDetails(GetPolicyDetailsResponseViewModel policy)
        {
            int policyId = 1;
            int policyCountyId = 1;
            int policyGridId = 1;
            int policyIntervalId = 1;
            policy.StateAbbreviation = _databaseContext.USDAStates.Where(s => s.Code == policy.StateCode).Select(s => s.Abbreviation).FirstOrDefault();
            if (policy.PolicyCounties != null)
            {
                policy.Id = policyId;
                policyId++;
                foreach (var county in policy.PolicyCounties)
                {
                    county.Id = policyCountyId;
                    policyCountyId++;
                    county.CommodityName = string.IsNullOrEmpty(county.CommodityCode) ? string.Empty : _databaseContext.USDACommodity.Where(c => c.ReinsuranceYear == "2023" && c.CommodityCode == county.CommodityCode).Select(c => c.CommodityName).FirstOrDefault();
                    county.CountyName = string.IsNullOrEmpty(county.CountyCode) ? string.Empty : _databaseContext.USDACounties.Where(c => c.Code == county.CountyCode && c.StateCode == policy.StateCode).Select(c => c.Name).FirstOrDefault();
                    if (county.PolicyGrids != null)
                    {
                        foreach (var grid in county.PolicyGrids)
                        {
                            grid.Id = policyGridId;
                            policyGridId++;
                            float totalProducerPremium = 0;
                            if (grid.PolicyIntervals != null)
                            {
                                foreach (var interval in grid.PolicyIntervals)
                                {
                                    interval.Id = policyIntervalId;
                                    policyIntervalId++;
                                    if (!string.IsNullOrEmpty(interval.IntervalCode))
                                        interval.IntervalName = (PracticeDetails.GetPracticeDetails("030", interval.IntervalCode)).interval;
                                    if (float.TryParse(interval.ProducerPremiumAmount, out float producerPremium))
                                        totalProducerPremium = totalProducerPremium + producerPremium;
                                }
                            }
                            grid.TotalProducerPremiumAmount = totalProducerPremium.ToString();
                        }
                    }
                }
            }
            return policy;
        }

        #endregion
    }
}
