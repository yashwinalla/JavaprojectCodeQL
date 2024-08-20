using WSR.Azure.Database.Models.M13;
using WSR.Domain.Model.CIMSv2;

namespace WSR.CIMS.Helper
{
    internal static class CIMSPolicyDetailsHelper
    {
        internal static GetPolicyDetailsResponseViewModel GetPolicyDetails(M13P10PolicyProducer producer, List<M13P14InsuranceInForce> insurance, List<M13P11Acreage> acreage)
        {
            var policyDetails = GetPolicyDetail(producer, insurance.First());
            policyDetails.PolicyCounties = GetCountyDetails(insurance, acreage);
            return policyDetails;
        }

        private static GetPolicyDetailsResponseViewModel GetPolicyDetail(M13P10PolicyProducer producer, M13P14InsuranceInForce insurance)
        {
            return new()
            {
                InsuredName = producer.BusinessName + producer.FirstName + " " + producer.LastName,
                ReinsuranceYear = producer.ReinsuranceYear,
                AIPCode = producer.AIPCode,
                AIPPolicyProducerKey = producer.AIPPolicyProducerKey,
                AIPInsuranceAgentKey = insurance.AIPInsuranceAgentKey,
                PolicyNumber = producer.PolicyNumber,
                StateCode = producer.LocationStateCode
            };
        }

        private static List<GetPolicyCountyResponseViewModel> GetCountyDetails(List<M13P14InsuranceInForce> insurances, List<M13P11Acreage> acreage)
        {
            List<GetPolicyCountyResponseViewModel> counties = new();
            foreach (var insurance in insurances)
            {
                GetPolicyCountyResponseViewModel countyDetails = new()
                {
                    AIPInsuranceInForceKey = insurance.AIPInsuranceInForceKey,
                    CommodityCode = insurance.CommodityCode,
                    CountyCode = insurance.LocationCountyCode
                };
                countyDetails.PolicyGrids = GetGridDetails(insurance, acreage.Where(a => a.AIPInsuranceInForceKey == insurance.AIPInsuranceInForceKey).ToList());
                counties.Add(countyDetails);
            }

            return counties;
        }

        private static List<GetPolicySubCountyResponseViewModel> GetGridDetails(M13P14InsuranceInForce insurance, List<M13P11Acreage> acreage)
        {
            List<GetPolicySubCountyResponseViewModel> grids = new();
            var insuranceGrids = acreage.Where(a => a.AIPInsuranceInForceKey == insurance.AIPInsuranceInForceKey);
            var gridIds = insuranceGrids.Where(g => !string.IsNullOrEmpty(g.SubCountyCode)).Select(i => i.SubCountyCode).Distinct().ToList();

            string intendedUse = string.IsNullOrWhiteSpace(insurance.TypeCode) ? insurance.IntendedUseCode : insurance.TypeCode;
            string irrigationPractice = string.Empty;
            string organicPractice = string.Empty;

            var practiceCode = insuranceGrids.Where(i => !string.IsNullOrWhiteSpace(i.PracticeCode)).Select(i => i.PracticeCode).FirstOrDefault();

            foreach (var gridId in gridIds)
            {
                var sharePercent = insuranceGrids.Where(i => i.SubCountyCode == gridId && !string.IsNullOrWhiteSpace(i.InsuredSharePercent)).Select(i => i.InsuredSharePercent).FirstOrDefault();
                var insurableAcres = insuranceGrids.Where(i => i.NonPremiumAcreageCode == "I" && !string.IsNullOrWhiteSpace(i.ReportedAcreage)).Select(i => i.ReportedAcreage).FirstOrDefault();
                var insurableColonies = insuranceGrids.Where(i => i.NonPremiumAcreageCode == "I" && !string.IsNullOrWhiteSpace(i.ReportedColonies)).Select(i => i.ReportedColonies).FirstOrDefault();
                var insuredAcres = insuranceGrids.Where(i => i.SubCountyCode == gridId && i.NonPremiumAcreageCode == "" && !string.IsNullOrWhiteSpace(i.TotalInsuredAcreage)).Select(i => i.TotalInsuredAcreage).FirstOrDefault();
                var insuredeColonies = insuranceGrids.Where(i => i.SubCountyCode == gridId && i.NonPremiumAcreageCode == "" && !string.IsNullOrWhiteSpace(i.TotalInsuredColonies)).Select(i => i.TotalInsuredColonies).FirstOrDefault();
                var gridIntendedUse = intendedUse;
                var gridIrrigationPractice = irrigationPractice;
                var gridOrganicPractice = organicPractice;
                if(intendedUse.Equals("007"))
                {
                    gridIntendedUse = "Grazing";
                    gridIrrigationPractice = "Unspecified";
                    gridOrganicPractice = "Unspecified";
                }
                if(intendedUse.Equals("030"))
                {
                    gridIntendedUse = "Haying";
                    gridIrrigationPractice = "Unspecified";
                    gridOrganicPractice = "Unspecified";
                    var acreagePractice = insuranceGrids.Where(i => i.SubCountyCode == gridId).Where(i => i.OrganicPracticeCode != "").Select(i => new { i.OrganicPracticeCode, i.IrrigationPracticeCode }).FirstOrDefault();
                    if(acreagePractice!= null)
                    {
                        if(!string.IsNullOrEmpty(acreagePractice.OrganicPracticeCode))
                        {
                            if(acreagePractice.OrganicPracticeCode == "997")
                                gridOrganicPractice = "Not Organic";
                            if (acreagePractice.OrganicPracticeCode == "001")
                                gridOrganicPractice = "Certified";
                            if (acreagePractice.OrganicPracticeCode == "002")
                                gridOrganicPractice = "Transitional";
                        }
                        if (!string.IsNullOrEmpty(acreagePractice.IrrigationPracticeCode) && acreagePractice.IrrigationPracticeCode != "997")
                        {
                            if (acreagePractice.IrrigationPracticeCode == "002")
                                gridIrrigationPractice = "Irrigated";
                            if (acreagePractice.IrrigationPracticeCode == "003")
                                gridIrrigationPractice = "Non-Irrigated";
                        }
                    }
                    else
                    {
                        var acreagePracticeCode = insuranceGrids.Where(i => i.SubCountyCode == gridId).Where(i => i.PracticeCode != "").Select(i => i.PracticeCode).FirstOrDefault();
                        if(!string.IsNullOrEmpty(acreagePracticeCode))
                        {
                            var acreagePracticeDetails = PracticeDetails.GetPracticeDetails(intendedUse, acreagePracticeCode);
                            gridIntendedUse = acreagePracticeDetails.intendedUse;
                            irrigationPractice = acreagePracticeDetails.irrigationPratice;
                            organicPractice = acreagePracticeDetails.organicPractice;
                        }
                    }
                }

                GetPolicySubCountyResponseViewModel grid = new()
                {
                    SubCountyCode = gridId,
                    IntendedUse = gridIntendedUse,
                    IrrigationPractice = gridIrrigationPractice,
                    OrganicPractice = gridOrganicPractice,
                    SharePercent = sharePercent,
                    TotalInsurableAcreage = insurableAcres,
                    TotalInsurableColonies = insurableColonies,
                    TotalInsuredAcreage = insuredAcres,
                    TotalInsuredColonies = insuredeColonies,
                    CoverageLevelPercent = insurance.CoverageLevelPercent,
                    ProductivityFactor = insurance.PriceElectionPercent
                };

                grid.PolicyIntervals = GetIntervalDetails(insurance, insuranceGrids.Where(i => i.SubCountyCode == gridId).ToList());
                grids.Add(grid);
            }
            return grids;
        }

        private static List<GetPolicyIntervalResponseViewModel> GetIntervalDetails(M13P14InsuranceInForce insurance, List<M13P11Acreage> acreage)
        {
            acreage = acreage.Where(a => a.NonPremiumAcreageCode == "").ToList();
            List<GetPolicyIntervalResponseViewModel> intervals = new();
            foreach(var interval in acreage)
            {
                intervals.Add(new()
                {
                    AIPAcreageKey = interval.AIPAcreageKey,
                    IntervalCode = string.IsNullOrEmpty(interval.PracticeCode) ? interval.IntervalCode : interval.PracticeCode,
                    PercentOfInterval = interval.PercentofValue,
                    TotalCoverage = string.IsNullOrEmpty(interval.TotalInsuredColonies) ? interval.TotalInsuredAcreage : interval.TotalInsuredColonies,
                    TotalPremiumAmount = interval.AIPTotalPremiumAmount,
                    SubsidyAmount = interval.AIPSubsidyAmount,
                    ProducerPremiumAmount = float.TryParse(interval.AIPTotalPremiumAmount, out float premium) && float.TryParse(interval.AIPSubsidyAmount, out float subsidy) ?(premium - subsidy).ToString() : string.Empty
                });
            }
            return intervals;
        }
    }
}
