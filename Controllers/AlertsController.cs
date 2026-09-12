using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AirQualityAnalysis.Filters;

namespace AirQualityAnalysis.Controllers
{
    [LoginRequired]
    public class AlertsController : Controller
    {
        private AirQualityDBEntities db = new AirQualityDBEntities();

        public ActionResult Index(int? countryId, int? cityId, string alertLevel = "", int page = 1, int pageSize = 10)
        {
            var model = new AlertsIndexViewModel
            {
                SelectedCountryId = countryId ?? 0,
                SelectedCityId = cityId ?? 0,
                SelectedAlertLevel = alertLevel ?? "",
                CurrentPage = page,
                SelectedPageSize = new[] { 10, 25, 50 }.Contains(pageSize) ? pageSize : 10,
                Countries = db.Countries.OrderBy(x => x.CountryName).ToList(),
                Cities = new List<City>(),
                AlertLevels = new List<string> { "Warning", "High", "Critical", "Emergency" },
                Alerts = new List<AlertListItemViewModel>(),
                HasSearched = true
            };

            if (model.SelectedCountryId > 0)
            {
                model.Cities = db.Cities
                    .Where(x => x.CountryID == model.SelectedCountryId)
                    .OrderBy(x => x.CityName)
                    .ToList();
            }

            if (model.HasSearched)
            {
                LoadAlerts(model);
            }

            return View(model);
        }

        private void LoadAlerts(AlertsIndexViewModel model)
        {
            var query =
                from al in db.Alerts
                join aqr in db.AirQualityRecords on al.RecordID equals aqr.RecordID
                join ci in db.Cities on aqr.CityID equals ci.CityID
                join co in db.Countries on ci.CountryID equals co.CountryID
                join p in db.Pollutants on aqr.PollutantID equals p.PollutantID
                join ac in db.AQICategories on aqr.CategoryID equals ac.CategoryID
                select new AlertListItemViewModel
                {
                    AlertID = al.AlertID,
                    RecordID = aqr.RecordID,
                    CountryID = co.CountryID,
                    CountryName = co.CountryName,
                    CityID = ci.CityID,
                    CityName = ci.CityName,
                    PollutantID = p.PollutantID,
                    PollutantName = p.PollutantName,
                    AQIValue = aqr.AQIValue,
                    AQICategoryName = ac.CategoryName,
                    AlertLevel = al.AlertLevel,
                    AlertMessage = al.AlertMessage,
                    LastUpdated = al.LastUpdated
                };

            query = query.Where(x => x.PollutantName == "Overall AQI");

            if (model.SelectedCountryId > 0)
                query = query.Where(x => x.CountryID == model.SelectedCountryId);

            if (model.SelectedCityId > 0)
                query = query.Where(x => x.CityID == model.SelectedCityId);

            if (!string.IsNullOrWhiteSpace(model.SelectedAlertLevel))
                query = query.Where(x => x.AlertLevel == model.SelectedAlertLevel);

            model.TotalRecords = query.Count();
            model.TotalPages = (int)Math.Ceiling((double)model.TotalRecords / model.SelectedPageSize);

            if (model.TotalPages == 0)
                model.CurrentPage = 1;
            else if (model.CurrentPage > model.TotalPages)
                model.CurrentPage = model.TotalPages;

            model.Alerts = query
                .OrderBy(x => x.CountryName)
                .ThenBy(x => x.CityName)
                .ThenByDescending(x => x.LastUpdated)
                .Skip((model.CurrentPage - 1) * model.SelectedPageSize)
                .Take(model.SelectedPageSize)
                .ToList();

            var cityIds = model.Alerts.Select(x => x.CityID).Distinct().ToList();

            var pollutantStatuses =
                (from r in db.AirQualityRecords
                 join p in db.Pollutants on r.PollutantID equals p.PollutantID
                 join c in db.AQICategories on r.CategoryID equals c.CategoryID
                 where cityIds.Contains(r.CityID) && p.PollutantName != "Overall AQI"
                 select new
                 {
                     r.CityID,
                     p.PollutantName,
                     r.AQIValue,
                     CategoryName = c.CategoryName
                 })
                .ToList();

            foreach (var alert in model.Alerts)
            {
                var riskyPollutants = pollutantStatuses
                    .Where(x => x.CityID == alert.CityID && GetCategorySeverity(x.CategoryName) >= 2)
                    .OrderByDescending(x => GetCategorySeverity(x.CategoryName))
                    .ThenByDescending(x => x.AQIValue)
                    .ToList();

                var maxSeverity = riskyPollutants.Any()
                    ? riskyPollutants.Max(x => GetCategorySeverity(x.CategoryName))
                    : 0;

                alert.AlertLevel = GetAlertLevelFromSeverity(maxSeverity);

                alert.CausePollutantsText = riskyPollutants.Any()
                    ? string.Join(", ", riskyPollutants.Select(x => x.PollutantName + " (AQI " + x.AQIValue + ")"))
                    : "";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
        private int GetCategorySeverity(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                return 0;

            switch (categoryName.Trim())
            {
                case "Good":
                    return 0;
                case "Moderate":
                    return 1;
                case "Unhealthy for Sensitive Groups":
                    return 2;
                case "Unhealthy":
                    return 3;
                case "Very Unhealthy":
                    return 4;
                case "Hazardous":
                    return 5;
                default:
                    return 0;
            }
        }

        private string GetAlertLevelFromSeverity(int severity)
        {
            switch (severity)
            {
                case 2: return "Warning";
                case 3: return "High";
                case 4: return "Critical";
                case 5: return "Emergency";
                default: return "";
            }
        }

        public class AlertsIndexViewModel
        {
            public int SelectedCountryId { get; set; }
            public int SelectedCityId { get; set; }
            public string SelectedAlertLevel { get; set; }

            public int CurrentPage { get; set; }
            public int SelectedPageSize { get; set; }

            public int TotalRecords { get; set; }
            public int TotalPages { get; set; }

            public bool HasSearched { get; set; }

            public List<Country> Countries { get; set; }
            public List<City> Cities { get; set; }
            public List<string> AlertLevels { get; set; }
            public List<AlertListItemViewModel> Alerts { get; set; }
        }

        public class AlertListItemViewModel
        {
            public int AlertID { get; set; }
            public int RecordID { get; set; }
            public int CountryID { get; set; }
            public string CountryName { get; set; }
            public int CityID { get; set; }
            public string CityName { get; set; }
            public int PollutantID { get; set; }
            public string PollutantName { get; set; }
            public int AQIValue { get; set; }
            public string AQICategoryName { get; set; }
            public string AlertLevel { get; set; }
            public string AlertMessage { get; set; }
            public DateTime LastUpdated { get; set; }

            public string CausePollutantsText { get; set; }
        }
    }
}