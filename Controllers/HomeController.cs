using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AirQualityAnalysis.Filters;

namespace AirQualityAnalysis.Controllers
{
    [LoginRequired]
    public class HomeController : Controller
    {
        private AirQualityDBEntities db = new AirQualityDBEntities();

        public ActionResult Index()
        {
            if (Session["UserRole"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            string username = Session["Username"] != null ? Session["Username"].ToString() : "User";
            string userRole = Session["UserRole"] != null ? Session["UserRole"].ToString() : "User";

            var overallPollutant = db.Pollutants.FirstOrDefault(p => p.PollutantName == "Overall AQI");
            int overallPollutantId = overallPollutant != null ? overallPollutant.PollutantID : 0;

            var mostRiskyRecord = db.AirQualityRecords
                .Where(x => x.PollutantID == overallPollutantId)
                .OrderByDescending(x => x.AQIValue)
                .FirstOrDefault();

            var topCountries = db.AirQualityRecords
                .Where(x => x.PollutantID == overallPollutantId)
                .GroupBy(x => x.City.Country.CountryName)
                .Select(g => new
                {
                    CountryName = g.Key,
                    AverageAQI = g.Average(x => x.AQIValue)
                })
                .OrderByDescending(x => x.AverageAQI)
                .Take(10)
                .ToList();

            var alertGroups = db.Alerts
                .GroupBy(x => x.AlertLevel)
                .Select(g => new
                {
                    AlertLevel = g.Key,
                    Count = g.Count()
                })
                .ToList();

            var model = new HomeDashboardViewModel
            {
                Username = username,
                UserRole = userRole,
                TotalCountries = db.Countries.Count(),
                TotalCities = db.Cities.Count(),
                TotalAlerts = db.Alerts.Count(),

                MostRiskyCityName = mostRiskyRecord != null
                    ? mostRiskyRecord.City.CityName + " (" + mostRiskyRecord.City.Country.CountryName + ")"
                    : "N/A",

                MostRiskyCityAQI = mostRiskyRecord != null
                    ? mostRiskyRecord.AQIValue
                    : 0,

                TopCountryLabels = topCountries.Select(x => x.CountryName).ToList(),
                TopCountryAverageAQIValues = topCountries.Select(x => Math.Round(x.AverageAQI, 2)).ToList(),

                WarningCount = alertGroups.Where(x => x.AlertLevel == "Warning").Select(x => x.Count).FirstOrDefault(),
                HighCount = alertGroups.Where(x => x.AlertLevel == "High").Select(x => x.Count).FirstOrDefault(),
                CriticalCount = alertGroups.Where(x => x.AlertLevel == "Critical").Select(x => x.Count).FirstOrDefault(),
                EmergencyCount = alertGroups.Where(x => x.AlertLevel == "Emergency").Select(x => x.Count).FirstOrDefault()
            };

            return View(model);
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";
            ViewBag.UserRole = Session["UserRole"] as string;
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";
            ViewBag.UserRole = Session["UserRole"] as string;
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        public class HomeDashboardViewModel
        {
            public string Username { get; set; }
            public string UserRole { get; set; }

            public int TotalCountries { get; set; }
            public int TotalCities { get; set; }
            public int TotalAlerts { get; set; }

            public string MostRiskyCityName { get; set; }
            public int MostRiskyCityAQI { get; set; }

            public List<string> TopCountryLabels { get; set; }
            public List<double> TopCountryAverageAQIValues { get; set; }

            public int WarningCount { get; set; }
            public int HighCount { get; set; }
            public int CriticalCount { get; set; }
            public int EmergencyCount { get; set; }
        }

        public class CityAQIItemVM
        {
            public string CityName { get; set; }
            public string CountryName { get; set; }
            public int AQIValue { get; set; }
            public string CategoryName { get; set; }
            public DateTime LastUpdated { get; set; }
        }
    }
}