namespace WSR.CIMS.Helper
{
    public static class PracticeDetails
    {
        public static (string intendedUse, string interval, string irrigationPratice, string organicPractice) GetPracticeDetails(string typeCode, string practiceCode)
        {
            if (typeCode == "030")
            {
                switch (practiceCode)
                {
                    case "625": return ("Haying", "Jan - Feb", "Unspecified", "Unspecified");
                    case "626": return ("Haying", "Feb - Mar", "Unspecified", "Unspecified");
                    case "627": return ("Haying", "Mar - Apr", "Unspecified", "Unspecified");
                    case "628": return ("Haying", "Apr - May", "Unspecified", "Unspecified");
                    case "629": return ("Haying", "May - Jun", "Unspecified", "Unspecified");
                    case "630": return ("Haying", "Jun - Jul", "Unspecified", "Unspecified");
                    case "631": return ("Haying", "Jul - Aug", "Unspecified", "Unspecified");
                    case "632": return ("Haying", "Aug - Sep", "Unspecified", "Unspecified");
                    case "633": return ("Haying", "Sep - Oct", "Unspecified", "Unspecified");
                    case "634": return ("Haying", "Oct - Nov", "Unspecified", "Unspecified");
                    case "635": return ("Haying", "Nov - Dec", "Unspecified", "Unspecified");

                    case "425": return ("Haying", "Jan - Feb", "Irrigated", "Not Organic");
                    case "426": return ("Haying", "Feb - Mar", "Irrigated", "Not Organic");
                    case "427": return ("Haying", "Mar - Apr", "Irrigated", "Not Organic");
                    case "428": return ("Haying", "Apr - May", "Irrigated", "Not Organic");
                    case "429": return ("Haying", "May - Jun", "Irrigated", "Not Organic");
                    case "430": return ("Haying", "Jun - Jul", "Irrigated", "Not Organic");
                    case "431": return ("Haying", "Jul - Aug", "Irrigated", "Not Organic");
                    case "432": return ("Haying", "Aug - Sep", "Irrigated", "Not Organic");
                    case "433": return ("Haying", "Sep - Oct", "Irrigated", "Not Organic");
                    case "434": return ("Haying", "Oct - Nov", "Irrigated", "Not Organic");
                    case "435": return ("Haying", "Nov - Dec", "Irrigated", "Not Organic");

                    case "465": return ("Haying", "Jan - Feb", "Irrigated", "Certified");
                    case "466": return ("Haying", "Feb - Mar", "Irrigated", "Certified");
                    case "467": return ("Haying", "Mar - Apr", "Irrigated", "Certified");
                    case "468": return ("Haying", "Apr - May", "Irrigated", "Certified");
                    case "469": return ("Haying", "May - Jun", "Irrigated", "Certified");
                    case "470": return ("Haying", "Jun - Jul", "Irrigated", "Certified");
                    case "471": return ("Haying", "Jul - Aug", "Irrigated", "Certified");
                    case "472": return ("Haying", "Aug - Sep", "Irrigated", "Certified");
                    case "473": return ("Haying", "Sep - Oct", "Irrigated", "Certified");
                    case "474": return ("Haying", "Oct - Nov", "Irrigated", "Certified");
                    case "475": return ("Haying", "Nov - Dec", "Irrigated", "Certified");

                    case "485": return ("Haying", "Jan - Feb", "Irrigated", "Transitional");
                    case "486": return ("Haying", "Feb - Mar", "Irrigated", "Transitional");
                    case "487": return ("Haying", "Mar - Apr", "Irrigated", "Transitional");
                    case "488": return ("Haying", "Apr - May", "Irrigated", "Transitional");
                    case "489": return ("Haying", "May - Jun", "Irrigated", "Transitional");
                    case "490": return ("Haying", "Jun - Jul", "Irrigated", "Transitional");
                    case "491": return ("Haying", "Jul - Aug", "Irrigated", "Transitional");
                    case "492": return ("Haying", "Aug - Sep", "Irrigated", "Transitional");
                    case "493": return ("Haying", "Sep - Oct", "Irrigated", "Transitional");
                    case "494": return ("Haying", "Oct - Nov", "Irrigated", "Transitional");
                    case "495": return ("Haying", "Nov - Dec", "Irrigated", "Transitional");

                    case "525": return ("Haying", "Jan - Feb", "Non - Irrigated", "Not Organic");
                    case "526": return ("Haying", "Feb - Mar", "Non - Irrigated", "Not Organic");
                    case "527": return ("Haying", "Mar - Apr", "Non - Irrigated", "Not Organic");
                    case "528": return ("Haying", "Apr - May", "Non - Irrigated", "Not Organic");
                    case "529": return ("Haying", "May - Jun", "Non - Irrigated", "Not Organic");
                    case "530": return ("Haying", "Jun - Jul", "Non - Irrigated", "Not Organic");
                    case "531": return ("Haying", "Jul - Aug", "Non - Irrigated", "Not Organic");
                    case "532": return ("Haying", "Aug - Sep", "Non - Irrigated", "Not Organic");
                    case "533": return ("Haying", "Sep - Oct", "Non - Irrigated", "Not Organic");
                    case "534": return ("Haying", "Oct - Nov", "Non - Irrigated", "Not Organic");
                    case "535": return ("Haying", "Nov - Dec", "Non - Irrigated", "Not Organic");

                    case "565": return ("Haying", "Jan - Feb", "Non - Irrigated", "Certified");
                    case "566": return ("Haying", "Feb - Mar", "Non - Irrigated", "Certified");
                    case "567": return ("Haying", "Mar - Apr", "Non - Irrigated", "Certified");
                    case "568": return ("Haying", "Apr - May", "Non - Irrigated", "Certified");
                    case "569": return ("Haying", "May - Jun", "Non - Irrigated", "Certified");
                    case "570": return ("Haying", "Jun - Jul", "Non - Irrigated", "Certified");
                    case "571": return ("Haying", "Jul - Aug", "Non - Irrigated", "Certified");
                    case "572": return ("Haying", "Aug - Sep", "Non - Irrigated", "Certified");
                    case "573": return ("Haying", "Sep - Oct", "Non - Irrigated", "Certified");
                    case "574": return ("Haying", "Oct - Nov", "Non - Irrigated", "Certified");
                    case "575": return ("Haying", "Nov - Dec", "Non - Irrigated", "Certified");

                    case "585": return ("Haying", "Jan - Feb", "Non - Irrigated", "Transitional");
                    case "586": return ("Haying", "Feb - Mar", "Non - Irrigated", "Transitional");
                    case "587": return ("Haying", "Mar - Apr", "Non - Irrigated", "Transitional");
                    case "588": return ("Haying", "Apr - May", "Non - Irrigated", "Transitional");
                    case "589": return ("Haying", "May - Jun", "Non - Irrigated", "Transitional");
                    case "590": return ("Haying", "Jun - Jul", "Non - Irrigated", "Transitional");
                    case "591": return ("Haying", "Jul - Aug", "Non - Irrigated", "Transitional");
                    case "592": return ("Haying", "Aug - Sep", "Non - Irrigated", "Transitional");
                    case "593": return ("Haying", "Sep - Oct", "Non - Irrigated", "Transitional");
                    case "594": return ("Haying", "Oct - Nov", "Non - Irrigated", "Transitional");
                    case "595": return ("Haying", "Nov - Dec", "Non - Irrigated", "Transitional");
                }

            }
            if (typeCode == "007")
            {
                switch (practiceCode)
                {
                    case "625": return ("Grazing", "Jan - Feb", "Unspecified", "Unspecified");
                    case "626": return ("Grazing", "Feb - Mar", "Unspecified", "Unspecified");
                    case "627": return ("Grazing", "Mar - Apr", "Unspecified", "Unspecified");
                    case "628": return ("Grazing", "Apr - May", "Unspecified", "Unspecified");
                    case "629": return ("Grazing", "May - Jun", "Unspecified", "Unspecified");
                    case "630": return ("Grazing", "Jun - Jul", "Unspecified", "Unspecified");
                    case "631": return ("Grazing", "Jul - Aug", "Unspecified", "Unspecified");
                    case "632": return ("Grazing", "Aug - Sep", "Unspecified", "Unspecified");
                    case "633": return ("Grazing", "Sep - Oct", "Unspecified", "Unspecified");
                    case "634": return ("Grazing", "Oct - Nov", "Unspecified", "Unspecified");
                    case "635": return ("Grazing", "Nov - Dec", "Unspecified", "Unspecified");

                }
            }

            return ("Unspecified", "Unspecified", "Unspecified", "Unspecified");
        }
    }
}
