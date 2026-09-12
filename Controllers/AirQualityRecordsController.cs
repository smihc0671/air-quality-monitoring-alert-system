using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using AirQualityAnalysis.Filters;

namespace AirQualityAnalysis.Controllers
{
    [LoginRequired]
    public class AirQualityRecordsController : Controller
    {
        private AirQualityDBEntities db = new AirQualityDBEntities();

        public ActionResult Index(int? countryId, int? cityId, int? pollutantId, string sortOption = "dateDesc", int page = 1, int pageSize = 10)
        {
            var model = new AirQualityRecordsIndexViewModel
            {
                SelectedCountryId = countryId ?? 0,
                SelectedCityId = cityId ?? 0,
                SelectedPollutantId = pollutantId ?? 0,
                SelectedSortOption = sortOption ?? "dateDesc",
                CurrentPage = page,
                SelectedPageSize = new[] { 10, 25, 50 }.Contains(pageSize) ? pageSize : 10,
                Countries = db.Countries.OrderBy(x => x.CountryName).ToList(),
                Pollutants = db.Pollutants.OrderBy(x => x.PollutantName).ToList(),
                Cities = new List<City>(),
                Records = new List<AirQualityRecordListItemVM>(),
                GroupedRecords = new List<AirQualityDisplayRowVM>(),
                HasSearched = countryId.HasValue
            };

            if (model.SelectedCountryId > 0)
            {
                model.Cities = db.Cities
                    .Where(x => x.CountryID == model.SelectedCountryId)
                    .OrderBy(x => x.CityName)
                    .ToList();
            }
            else
            {
                model.Cities = new List<City>();
            }

            if (countryId.HasValue)
            {
                LoadIndexData(model);
            }

            return View(model);
        }

       
        [AdminOnly]
        [HttpGet]
        public ActionResult Create()
        {
            if (Session["UserRole"] == null || Session["UserRole"].ToString() != "Admin")
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new CreateAirQualityRecordSetViewModel
            {
                Countries = db.Countries.OrderBy(x => x.CountryName).ToList(),
                Cities = new List<City>()
            };

            return View(model);
        }

        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateAirQualityRecordSetViewModel model)
        {
            if (Session["UserRole"] == null || Session["UserRole"].ToString() != "Admin")
            {
                return RedirectToAction("Login", "Account");
            }

            model.Countries = db.Countries.OrderBy(x => x.CountryName).ToList();
            model.Cities = model.SelectedCountryId > 0
                ? db.Cities.Where(x => x.CountryID == model.SelectedCountryId).OrderBy(x => x.CityName).ToList()
                : new List<City>();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.SelectedCountryId == 0)
                ModelState.AddModelError("", "Please select a country.");

            if (model.SelectedCityId == 0)
                ModelState.AddModelError("", "Please select a city.");

            if (model.COAQIValue < 0 || model.OzoneAQIValue < 0 || model.NO2AQIValue < 0 || model.PM25AQIValue < 0)
                ModelState.AddModelError("", "AQI values cannot be negative.");

            if (!ModelState.IsValid)
                return View(model);

            var selectedCountry = db.Countries.FirstOrDefault(x => x.CountryID == model.SelectedCountryId);
            if (selectedCountry == null)
            {
                ModelState.AddModelError("", "Selected country was not found.");
                return View(model);
            }

            var selectedCity = db.Cities.FirstOrDefault(x =>
                x.CityID == model.SelectedCityId &&
                x.CountryID == model.SelectedCountryId);

            if (selectedCity == null)
            {
                ModelState.AddModelError("", "Selected city was not found for the chosen country.");
                return View(model);
            }

            var requiredPollutants = new[] { "Overall AQI", "CO", "Ozone", "NO2", "PM2.5" };

            var pollutantMap = db.Pollutants
                .Where(x => requiredPollutants.Contains(x.PollutantName))
                .ToList()
                .ToDictionary(x => x.PollutantName, x => x.PollutantID);

            foreach (var pollutantName in requiredPollutants)
            {
                if (!pollutantMap.ContainsKey(pollutantName))
                {
                    ModelState.AddModelError("", "Missing pollutant in database: " + pollutantName);
                    return View(model);
                }
            }

            var fourPollutantIds = new[]
            {
                pollutantMap["CO"],
                pollutantMap["Ozone"],
                pollutantMap["NO2"],
                pollutantMap["PM2.5"]
            };

            var hasAnyExistingCoreRecord = db.AirQualityRecords
                .Any(x => x.CityID == selectedCity.CityID && fourPollutantIds.Contains(x.PollutantID));

            if (hasAnyExistingCoreRecord)
            {
                TempData["ErrorMessage"] = "A record already exists for this city. Use Manage/Edit.";
                return RedirectToAction("Index", new
                {
                    countryId = model.SelectedCountryId,
                    cityId = model.SelectedCityId
                });
            }

            int overallAQIValue = new[] { model.COAQIValue, model.OzoneAQIValue, model.NO2AQIValue, model.PM25AQIValue }.Max();

            var categories = db.AQICategories.OrderBy(x => x.MinValue).ToList();

            AQICategory FindCategory(int value)
            {
                return categories.FirstOrDefault(x => value >= x.MinValue && value <= x.MaxValue);
            }

            var overallCategory = FindCategory(overallAQIValue);
            var coCategory = FindCategory(model.COAQIValue);
            var ozoneCategory = FindCategory(model.OzoneAQIValue);
            var no2Category = FindCategory(model.NO2AQIValue);
            var pm25Category = FindCategory(model.PM25AQIValue);

            if (overallCategory == null || coCategory == null || ozoneCategory == null || no2Category == null || pm25Category == null)
            {
                ModelState.AddModelError("", "One or more AQI values do not match any AQI category.");
                return View(model);
            }

            var now = DateTime.Now;

            var newRecords = new List<AirQualityRecord>
            {
                new AirQualityRecord
                {
                    CityID = selectedCity.CityID,
                    PollutantID = pollutantMap["Overall AQI"],
                    CategoryID = overallCategory.CategoryID,
                    AQIValue = overallAQIValue,
                    LastUpdated = now
                },
                new AirQualityRecord
                {
                    CityID = selectedCity.CityID,
                    PollutantID = pollutantMap["CO"],
                    CategoryID = coCategory.CategoryID,
                    AQIValue = model.COAQIValue,
                    LastUpdated = now
                },
                new AirQualityRecord
                {
                    CityID = selectedCity.CityID,
                    PollutantID = pollutantMap["Ozone"],
                    CategoryID = ozoneCategory.CategoryID,
                    AQIValue = model.OzoneAQIValue,
                    LastUpdated = now
                },
                new AirQualityRecord
                {
                    CityID = selectedCity.CityID,
                    PollutantID = pollutantMap["NO2"],
                    CategoryID = no2Category.CategoryID,
                    AQIValue = model.NO2AQIValue,
                    LastUpdated = now
                },
                new AirQualityRecord
                {
                    CityID = selectedCity.CityID,
                    PollutantID = pollutantMap["PM2.5"],
                    CategoryID = pm25Category.CategoryID,
                    AQIValue = model.PM25AQIValue,
                    LastUpdated = now
                }
            };

            foreach (var item in newRecords)
                db.AirQualityRecords.Add(item);

            db.SaveChanges();
            SyncAlertsForCity(selectedCity.CityID);


            return RedirectToAction("Index", new
            {
                countryId = model.SelectedCountryId,
                cityId = model.SelectedCityId
            });
        }

        [AdminOnly]
        [HttpGet]
        public ActionResult Edit(int? id, int? countryId, int? cityId, int? pollutantId, int? page)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var record = db.AirQualityRecords.FirstOrDefault(x => x.RecordID == id.Value);
            if (record == null)
                return HttpNotFound();

            var details = (
                from r in db.AirQualityRecords
                join city in db.Cities on r.CityID equals city.CityID
                join country in db.Countries on city.CountryID equals country.CountryID
                join pollutant in db.Pollutants on r.PollutantID equals pollutant.PollutantID
                join category in db.AQICategories on r.CategoryID equals category.CategoryID
                where r.RecordID == id.Value
                select new EditAirQualityRecordViewModel
                {
                    RecordID = r.RecordID,
                    CountryID = country.CountryID,
                    CountryName = country.CountryName,
                    CityID = city.CityID,
                    CityName = city.CityName,
                    PollutantID = pollutant.PollutantID,
                    PollutantName = pollutant.PollutantName,
                    CategoryID = r.CategoryID,
                    CurrentCategoryName = category.CategoryName,
                    AQIValue = r.AQIValue,
                    LastUpdated = r.LastUpdated,
                    ReturnCountryId = countryId,
                    ReturnCityId = cityId,
                    ReturnPollutantId = pollutantId,
                    ReturnPage = page
                })
                .FirstOrDefault();

            if (details == null)
                return HttpNotFound();

            details.IsOverallAQI = details.PollutantName == "Overall AQI";
            details.CalculatedCategoryName = GetCalculatedCategoryName(details.AQIValue);

            return View(details);
        }
        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(EditAirQualityRecordViewModel model)
        {
            var record = db.AirQualityRecords.FirstOrDefault(x => x.RecordID == model.RecordID);
            if (record == null)
                return HttpNotFound();

            model.CountryName = db.Countries.Where(x => x.CountryID == model.CountryID).Select(x => x.CountryName).FirstOrDefault();
            model.CityName = db.Cities.Where(x => x.CityID == model.CityID).Select(x => x.CityName).FirstOrDefault();
            model.PollutantName = db.Pollutants.Where(x => x.PollutantID == model.PollutantID).Select(x => x.PollutantName).FirstOrDefault();
            model.CurrentCategoryName = db.AQICategories.Where(x => x.CategoryID == record.CategoryID).Select(x => x.CategoryName).FirstOrDefault();
            model.IsOverallAQI = model.PollutantName == "Overall AQI";
            model.LastUpdated = record.LastUpdated;
            model.CalculatedCategoryName = GetCalculatedCategoryName(model.AQIValue);

            if (model.IsOverallAQI)
            {
                ModelState.AddModelError("", "Overall AQI cannot be edited manually.");
                return View(model);
            }

            if (model.AQIValue < 0)
            {
                ModelState.AddModelError("", "AQI value cannot be negative.");
                return View(model);
            }

            var calculatedCategory = FindCategory(model.AQIValue);
            if (calculatedCategory == null)
            {
                ModelState.AddModelError("", "No AQI category matches this AQI value.");
                return View(model);
            }

            record.AQIValue = model.AQIValue;
            record.CategoryID = calculatedCategory.CategoryID;
            record.LastUpdated = DateTime.Now;

            db.SaveChanges();

            UpdateOverallAQI(model.CityID);
            SyncAlertsForCity(model.CityID);

            return RedirectToAction("CityRecords", new
            {
                cityId = model.CityID,
                countryId = model.ReturnCountryId,
                selectedCityId = model.CityID,
                pollutantId = model.ReturnPollutantId,
                page = model.ReturnPage
            });
        }
        [AdminOnly]
        [HttpGet]
        public ActionResult Delete(int? id, int? countryId, int? cityId, int? pollutantId, int? page)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var details = (
                from r in db.AirQualityRecords
                join city in db.Cities on r.CityID equals city.CityID
                join country in db.Countries on city.CountryID equals country.CountryID
                join pollutant in db.Pollutants on r.PollutantID equals pollutant.PollutantID
                join category in db.AQICategories on r.CategoryID equals category.CategoryID
                where r.RecordID == id.Value
                select new DeleteAirQualityRecordViewModel
                {
                    RecordID = r.RecordID,
                    CountryName = country.CountryName,
                    CityName = city.CityName,
                    PollutantName = pollutant.PollutantName,
                    CategoryName = category.CategoryName,
                    AQIValue = r.AQIValue,
                    LastUpdated = r.LastUpdated,
                    CityID = r.CityID,
                    ReturnCountryId = countryId,
                    ReturnCityId = cityId,
                    ReturnPollutantId = pollutantId,
                    ReturnPage = page
                }).FirstOrDefault();

            if (details == null)
                return HttpNotFound();

            return View(details);
        }

        [AdminOnly]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(DeleteAirQualityRecordViewModel model)
        {
            var record = db.AirQualityRecords.FirstOrDefault(x => x.RecordID == model.RecordID);
            if (record == null)
                return HttpNotFound();

            int deletedCityId = record.CityID;

            db.AirQualityRecords.Remove(record);
            db.SaveChanges();

            RecalculateOverallAQI(deletedCityId);
            SyncAlertsForCity(deletedCityId);

            return RedirectToAction("CityRecords", new
            {
                cityId = deletedCityId,
                countryId = model.ReturnCountryId,
                selectedCityId = deletedCityId,
                pollutantId = model.ReturnPollutantId,
                page = model.ReturnPage
            });
        }

        private void LoadIndexData(AirQualityRecordsIndexViewModel model)
        {

            if (model.SelectedPollutantId == 0)
            {
                var query =
                    from record in db.AirQualityRecords
                    join city in db.Cities on record.CityID equals city.CityID
                    join country in db.Countries on city.CountryID equals country.CountryID
                    join pollutant in db.Pollutants on record.PollutantID equals pollutant.PollutantID
                    join category in db.AQICategories on record.CategoryID equals category.CategoryID
                    select new
                    {
                        country.CountryID,
                        country.CountryName,
                        city.CityID,
                        city.CityName,
                        pollutant.PollutantName,
                        category.CategoryName,
                        record.AQIValue,
                        record.LastUpdated
                    };
                if (model.SelectedCountryId > 0)
                    query = query.Where(x => x.CountryID == model.SelectedCountryId);

                if (model.SelectedCityId > 0)
                    query = query.Where(x => x.CityID == model.SelectedCityId);

                var allRows = query.ToList();

                var groupedQuery = allRows
             .GroupBy(x => new { x.CountryID, x.CountryName, x.CityID, x.CityName })
             .Select(g =>
             {
                 int? GetValue(string pollutantName)
                 {
                     return g.Where(x => x.PollutantName == pollutantName)
                             .Select(x => (int?)x.AQIValue)
                             .FirstOrDefault();
                 }

                 string GetCategory(string pollutantName)
                 {
                     return g.Where(x => x.PollutantName == pollutantName)
                             .Select(x => x.CategoryName)
                             .FirstOrDefault();
                 }

                 return new AirQualityDisplayRowVM
                 {
                     CityID = g.Key.CityID,
                     Country = g.Key.CountryName,
                     City = g.Key.CityName,
                     AQIValue = GetValue("Overall AQI"),
                     AQICategory = GetCategory("Overall AQI"),
                     COAQIValue = GetValue("CO"),
                     COAQICategory = GetCategory("CO"),
                     OzoneAQIValue = GetValue("Ozone"),
                     OzoneAQICategory = GetCategory("Ozone"),
                     NO2AQIValue = GetValue("NO2"),
                     NO2AQICategory = GetCategory("NO2"),
                     PM25AQIValue = GetValue("PM2.5"),
                     PM25AQICategory = GetCategory("PM2.5"),
                     LastUpdated = g.Max(x => x.LastUpdated)
                 };
             });

                switch (model.SelectedSortOption)
                {
                    case "aqiAsc":
                        groupedQuery = groupedQuery.OrderBy(x => x.AQIValue ?? int.MaxValue);
                        break;
                    case "aqiDesc":
                        groupedQuery = groupedQuery.OrderByDescending(x => x.AQIValue ?? int.MinValue);
                        break;
                    case "dateAsc":
                        groupedQuery = groupedQuery.OrderBy(x => x.LastUpdated ?? DateTime.MaxValue);
                        break;
                    case "dateDesc":
                        groupedQuery = groupedQuery.OrderByDescending(x => x.LastUpdated ?? DateTime.MinValue);
                        break;
                    case "countryAsc":
                        groupedQuery = groupedQuery.OrderBy(x => x.Country).ThenBy(x => x.City);
                        break;
                    case "countryDesc":
                        groupedQuery = groupedQuery.OrderByDescending(x => x.Country).ThenBy(x => x.City);
                        break;
                    case "cityAsc":
                        groupedQuery = groupedQuery.OrderBy(x => x.City);
                        break;
                    case "cityDesc":
                        groupedQuery = groupedQuery.OrderByDescending(x => x.City);
                        break;
                    default:
                        groupedQuery = groupedQuery.OrderByDescending(x => x.LastUpdated ?? DateTime.MinValue);
                        break;
                }

                var grouped = groupedQuery.ToList();

                model.TotalRecords = grouped.Count;
                model.TotalPages = (int)Math.Ceiling((double)model.TotalRecords / model.SelectedPageSize);
                if (model.TotalPages == 0) model.CurrentPage = 1;
                else if (model.CurrentPage > model.TotalPages) model.CurrentPage = model.TotalPages;

                model.GroupedRecords = grouped
                    .Skip((model.CurrentPage - 1) * model.SelectedPageSize)
                    .Take(model.SelectedPageSize)
                    .ToList();

                model.Records = new List<AirQualityRecordListItemVM>();
            }
            else
            {
                var query =
                    from record in db.AirQualityRecords
                    join city in db.Cities on record.CityID equals city.CityID
                    join country in db.Countries on city.CountryID equals country.CountryID
                    join pollutant in db.Pollutants on record.PollutantID equals pollutant.PollutantID
                    join category in db.AQICategories on record.CategoryID equals category.CategoryID
                    select new AirQualityRecordListItemVM
                    {
                        RecordID = record.RecordID,
                        CountryID = country.CountryID,
                        CountryName = country.CountryName,
                        CityID = city.CityID,
                        CityName = city.CityName,
                        PollutantID = pollutant.PollutantID,
                        PollutantName = pollutant.PollutantName,
                        CategoryName = category.CategoryName,
                        AQIValue = record.AQIValue,
                        LastUpdated = record.LastUpdated
                    };

                if (model.SelectedCountryId > 0)
                    query = query.Where(x => x.CountryID == model.SelectedCountryId);

                if (model.SelectedCityId > 0)
                    query = query.Where(x => x.CityID == model.SelectedCityId);

                query = query.Where(x => x.PollutantID == model.SelectedPollutantId);

                switch (model.SelectedSortOption)
                {
                    case "aqiAsc":
                        query = query.OrderBy(x => x.AQIValue);
                        break;
                    case "aqiDesc":
                        query = query.OrderByDescending(x => x.AQIValue);
                        break;
                    case "dateAsc":
                        query = query.OrderBy(x => x.LastUpdated);
                        break;
                    case "dateDesc":
                        query = query.OrderByDescending(x => x.LastUpdated);
                        break;
                    case "countryAsc":
                        query = query.OrderBy(x => x.CountryName).ThenBy(x => x.CityName);
                        break;
                    case "countryDesc":
                        query = query.OrderByDescending(x => x.CountryName).ThenBy(x => x.CityName);
                        break;
                    case "cityAsc":
                        query = query.OrderBy(x => x.CityName);
                        break;
                    case "cityDesc":
                        query = query.OrderByDescending(x => x.CityName);
                        break;
                    default:
                        query = query.OrderByDescending(x => x.LastUpdated);
                        break;
                }

                model.TotalRecords = query.Count();
                model.TotalPages = (int)Math.Ceiling((double)model.TotalRecords / model.SelectedPageSize);
                if (model.TotalPages == 0) model.CurrentPage = 1;
                else if (model.CurrentPage > model.TotalPages) model.CurrentPage = model.TotalPages;

                model.Records = query
                    .Skip((model.CurrentPage - 1) * model.SelectedPageSize)
                    .Take(model.SelectedPageSize)
                    .ToList();

                model.GroupedRecords = new List<AirQualityDisplayRowVM>();
            }
        }

        private AQICategory FindCategory(int value)
        {
            return db.AQICategories
                .OrderBy(x => x.MinValue)
                .FirstOrDefault(x => value >= x.MinValue && value <= x.MaxValue);
        }

        private string GetCalculatedCategoryName(int value)
        {
            var category = FindCategory(value);
            return category != null ? category.CategoryName : "No matching category";
        }

        private void UpdateOverallAQI(int cityId)
        {
            var pollutantRecords = (
                from r in db.AirQualityRecords
                join p in db.Pollutants on r.PollutantID equals p.PollutantID
                where r.CityID == cityId && p.PollutantName != "Overall AQI"
                select r
            ).ToList();

            if (pollutantRecords.Count == 0)
                return;

            var maxValue = pollutantRecords.Max(x => x.AQIValue);

            var overallPollutant = db.Pollutants.FirstOrDefault(x => x.PollutantName == "Overall AQI");
            if (overallPollutant == null)
                return;

            var overallRecord = db.AirQualityRecords.FirstOrDefault(x =>
                x.CityID == cityId && x.PollutantID == overallPollutant.PollutantID);

            if (overallRecord == null)
                return;

            var overallCategory = FindCategory(maxValue);
            if (overallCategory == null)
                return;

            overallRecord.AQIValue = maxValue;
            overallRecord.CategoryID = overallCategory.CategoryID;
            overallRecord.LastUpdated = DateTime.Now;

            db.SaveChanges();
        }

        [AdminOnly]
        [HttpGet]
        public ActionResult Add(int returnCityId, int pollutantToAddId, int? countryId, int? cityId, int? pollutantId, int? page)
        {
            var city = db.Cities.FirstOrDefault(x => x.CityID == returnCityId);
            if (city == null)
                return HttpNotFound();

            var country = db.Countries.FirstOrDefault(x => x.CountryID == city.CountryID);
            var pollutant = db.Pollutants.FirstOrDefault(x => x.PollutantID == pollutantToAddId);

            if (country == null || pollutant == null)
                return HttpNotFound();

            var model = new AddAirQualityRecordViewModel
            {
                ReturnCityId = returnCityId,
                PollutantToAddId = pollutantToAddId,
                CountryId = countryId,
                CityId = cityId,
                PollutantId = pollutantId,
                Page = page,
                CountryName = country.CountryName,
                CityName = city.CityName,
                PollutantName = pollutant.PollutantName
            };

            return View(model);
        }

        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Add(AddAirQualityRecordViewModel model)
        {
            var city = db.Cities.FirstOrDefault(x => x.CityID == model.ReturnCityId);
            if (city == null)
                return HttpNotFound();

            var country = db.Countries.FirstOrDefault(x => x.CountryID == city.CountryID);
            var pollutant = db.Pollutants.FirstOrDefault(x => x.PollutantID == model.PollutantToAddId);

            model.CountryName = country != null ? country.CountryName : "";
            model.CityName = city.CityName;
            model.PollutantName = pollutant != null ? pollutant.PollutantName : "";

            if (model.AQIValue < 0)
                ModelState.AddModelError("", "AQI value cannot be negative.");

            var existingRecord = db.AirQualityRecords.FirstOrDefault(x =>
                x.CityID == model.ReturnCityId &&
                x.PollutantID == model.PollutantToAddId);

            if (existingRecord != null)
                ModelState.AddModelError("", "This pollutant already has a record for this city.");

            var category = FindCategory(model.AQIValue);
            if (category == null)
                ModelState.AddModelError("", "No AQI category matches this AQI value.");

            if (!ModelState.IsValid)
                return View(model);

            var newRecord = new AirQualityRecord
            {
                CityID = model.ReturnCityId,
                PollutantID = model.PollutantToAddId,
                CategoryID = category.CategoryID,
                AQIValue = model.AQIValue,
                LastUpdated = DateTime.Now
            };

            db.AirQualityRecords.Add(newRecord);
            db.SaveChanges();

            RecalculateOverallAQI(model.ReturnCityId);
            SyncAlertsForCity(model.ReturnCityId);

            return RedirectToAction("CityRecords", new
            {
                cityId = model.ReturnCityId,
                countryId = model.CountryId,
                selectedCityId = model.CityId,
                pollutantId = model.PollutantId,
                page = model.Page
            });
        }
        public ActionResult CityRecords(int cityId, int? countryId, int? selectedCityId, int? pollutantId, int? page)
        {
            var cityInfo = (
                from city in db.Cities
                join country in db.Countries on city.CountryID equals country.CountryID
                where city.CityID == cityId
                select new CityRecordsViewModel.CityHeaderInfo
                {
                    CityID = city.CityID,
                    CityName = city.CityName,
                    CountryID = country.CountryID,
                    CountryName = country.CountryName
                }
            ).FirstOrDefault();

            if (cityInfo == null)
                return HttpNotFound();

            var pollutantOrder = new List<string> { "Overall AQI", "CO", "Ozone", "NO2", "PM2.5" };

            var pollutants = db.Pollutants.ToList();

            var existingRecords = db.Database.SqlQuery<AirQualityRecordDetailViewRow>(
                @"SELECT
                RecordID,
                CityID,
                PollutantID,
                PollutantName,
                CategoryName,
                AQIValue,
                RecordLastUpdated AS LastUpdated
                FROM dbo.vw_AirQualityRecordDetails
                WHERE CityID = @p0",
                cityId
            ).ToList();

            var rows = pollutantOrder
                .Select(name =>
                {
                    var pollutant = db.Pollutants.FirstOrDefault(p => p.PollutantName == name);
                    var existing = existingRecords.FirstOrDefault(x => x.PollutantName == name);

                    return new CityRecordsViewModel.CityRecordRow
                    {
                        RecordID = existing != null ? (int?)existing.RecordID : null,
                        CityID = cityId,
                        PollutantID = pollutant != null ? pollutant.PollutantID : -1,
                        PollutantName = name,
                        CategoryName = existing != null ? existing.CategoryName : null,
                        AQIValue = existing != null ? (int?)existing.AQIValue : null,
                        LastUpdated = existing != null ? (DateTime?)existing.LastUpdated : null
                    };
                })
                .ToList();

            var model = new CityRecordsViewModel
            {
                CityInfo = cityInfo,
                Rows = rows,
                CountryId = countryId,
                SelectedCityId = selectedCityId,
                PollutantId = pollutantId,
                Page = page
            };

            return View(model);
        }

        private void RecalculateOverallAQI(int cityId)
        {
            var categories = db.AQICategories.OrderBy(x => x.MinValue).ToList();

            AQICategory FindCategoryLocal(int value)
            {
                return categories.FirstOrDefault(x => value >= x.MinValue && value <= x.MaxValue);
            }

            var overallPollutant = db.Pollutants.FirstOrDefault(p => p.PollutantName == "Overall AQI");
            if (overallPollutant == null)
                return;

            var overallRecord = db.AirQualityRecords.FirstOrDefault(x =>
                x.CityID == cityId &&
                x.PollutantID == overallPollutant.PollutantID);

            var nonOverallRecords = (
                from r in db.AirQualityRecords
                join p in db.Pollutants on r.PollutantID equals p.PollutantID
                where r.CityID == cityId && p.PollutantName != "Overall AQI"
                select r
            ).ToList();

            if (nonOverallRecords.Count == 0)
            {
                if (overallRecord != null)
                {
                    db.AirQualityRecords.Remove(overallRecord);
                    db.SaveChanges();
                }
                return;
            }

            var maxValue = nonOverallRecords.Max(x => x.AQIValue);
            var overallCategory = FindCategoryLocal(maxValue);
            if (overallCategory == null)
                return;

            if (overallRecord == null)
            {
                overallRecord = new AirQualityRecord
                {
                    CityID = cityId,
                    PollutantID = overallPollutant.PollutantID,
                    CategoryID = overallCategory.CategoryID,
                    AQIValue = maxValue,
                    LastUpdated = DateTime.Now
                };

                db.AirQualityRecords.Add(overallRecord);
            }
            else
            {
                overallRecord.AQIValue = maxValue;
                overallRecord.CategoryID = overallCategory.CategoryID;
                overallRecord.LastUpdated = DateTime.Now;
            }

            db.SaveChanges();
        }
        private void SyncAlertsForCity(int cityId)
        {
            var records = (
                from r in db.AirQualityRecords
                join p in db.Pollutants on r.PollutantID equals p.PollutantID
                join c in db.AQICategories on r.CategoryID equals c.CategoryID
                where r.CityID == cityId
                select new
                {
                    r.RecordID,
                    r.AQIValue,
                    PollutantName = p.PollutantName,
                    CategoryName = c.CategoryName
                }
            ).ToList();

            foreach (var item in records)
            {
                string alertLevel = null;
                // string alertMessage = null;

                switch (item.CategoryName)
                {
                    case "Unhealthy for Sensitive Groups":
                        alertLevel = "Warning";
                        break;

                    case "Unhealthy":
                        alertLevel = "High";
                        break;

                    case "Very Unhealthy":
                        alertLevel = "Critical";
                        break;

                    case "Hazardous":
                        alertLevel = "Emergency";
                        break;
                }

                var existing = db.Alerts.FirstOrDefault(x => x.RecordID == item.RecordID);

                if (alertLevel == null)
                {
                    if (existing != null)
                        db.Alerts.Remove(existing);
                }
                else
                {
                    string message = $"{item.PollutantName} level is {item.CategoryName} (AQI {item.AQIValue})";

                    if (existing == null)
                    {
                        db.Alerts.Add(new Alert
                        {
                            RecordID = item.RecordID,
                            AlertLevel = alertLevel,
                            AlertMessage = message,
                            LastUpdated = DateTime.Now
                        });
                    }
                    else
                    {
                        existing.AlertLevel = alertLevel;
                        existing.AlertMessage = message;
                        existing.LastUpdated = DateTime.Now;
                    }
                }
            }

            db.SaveChanges();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }


        public class AirQualityRecordsIndexViewModel
        {
            public List<Country> Countries { get; set; }
            public List<City> Cities { get; set; }
            public List<Pollutant> Pollutants { get; set; }
            public List<AirQualityRecordListItemVM> Records { get; set; }
            public List<AirQualityDisplayRowVM> GroupedRecords { get; set; }

            public int SelectedCountryId { get; set; }
            public int SelectedCityId { get; set; }
            public int SelectedPollutantId { get; set; }
            public string SelectedSortOption { get; set; }
            public int CurrentPage { get; set; }
            public int SelectedPageSize { get; set; }

            public int TotalRecords { get; set; }
            public int TotalPages { get; set; }
            public bool HasSearched { get; set; }
        }
        public class CityRecordsViewModel
        {
            public CityHeaderInfo CityInfo { get; set; }
            public List<CityRecordRow> Rows { get; set; }

            public int? CountryId { get; set; }
            public int? SelectedCityId { get; set; }
            public int? PollutantId { get; set; }
            public int? Page { get; set; }

            public class CityHeaderInfo
            {
                public int CityID { get; set; }
                public string CityName { get; set; }
                public int CountryID { get; set; }
                public string CountryName { get; set; }
            }

            public class CityRecordRow
            {
                public int? RecordID { get; set; }
                public int CityID { get; set; }
                public int PollutantID { get; set; }
                public string PollutantName { get; set; }
                public string CategoryName { get; set; }
                public int? AQIValue { get; set; }
                public DateTime? LastUpdated { get; set; }
            }
        }

        public class AddAirQualityRecordViewModel
        {
            public int ReturnCityId { get; set; }
            public int PollutantToAddId { get; set; }
            public int AQIValue { get; set; }

            public string CountryName { get; set; }
            public string CityName { get; set; }
            public string PollutantName { get; set; }

            public int? CountryId { get; set; }
            public int? CityId { get; set; }
            public int? PollutantId { get; set; }
            public int? Page { get; set; }
        }
        public class AirQualityRecordListItemVM
        {
            public int RecordID { get; set; }
            public int CountryID { get; set; }
            public string CountryName { get; set; }
            public int CityID { get; set; }
            public string CityName { get; set; }
            public int PollutantID { get; set; }
            public string PollutantName { get; set; }
            public string CategoryName { get; set; }
            public int AQIValue { get; set; }
            public DateTime LastUpdated { get; set; }
        }

        public class AirQualityDisplayRowVM
        {
            public int CityID { get; set; }
            public string Country { get; set; }
            public string City { get; set; }

            public int? AQIValue { get; set; }
            public string AQICategory { get; set; }

            public int? COAQIValue { get; set; }
            public string COAQICategory { get; set; }

            public int? OzoneAQIValue { get; set; }
            public string OzoneAQICategory { get; set; }

            public int? NO2AQIValue { get; set; }
            public string NO2AQICategory { get; set; }

            public int? PM25AQIValue { get; set; }
            public string PM25AQICategory { get; set; }
            public DateTime? LastUpdated { get; set; }
        }

        public class CreateAirQualityRecordSetViewModel
        {
            public List<Country> Countries { get; set; }
            public List<City> Cities { get; set; }

            public int SelectedCountryId { get; set; }
            public int SelectedCityId { get; set; }

            public int COAQIValue { get; set; }
            public int OzoneAQIValue { get; set; }
            public int NO2AQIValue { get; set; }
            public int PM25AQIValue { get; set; }
        }

        public class EditAirQualityRecordViewModel
        {
            public int RecordID { get; set; }
            public int CountryID { get; set; }
            public string CountryName { get; set; }

            public int CityID { get; set; }
            public string CityName { get; set; }
            public int PollutantID { get; set; }
            public string PollutantName { get; set; }
            public int CategoryID { get; set; }
            public string CurrentCategoryName { get; set; }
            public int AQIValue { get; set; }
            public DateTime LastUpdated { get; set; }

            public bool IsOverallAQI { get; set; }
            public string CalculatedCategoryName { get; set; }

            public int? ReturnCountryId { get; set; }
            public int? ReturnCityId { get; set; }
            public int? ReturnPollutantId { get; set; }
            public int? ReturnPage { get; set; }
        }

        public class DeleteAirQualityRecordViewModel
        {
            public int RecordID { get; set; }
            public string CountryName { get; set; }
            public string CityName { get; set; }
            public string PollutantName { get; set; }
            public string CategoryName { get; set; }
            public int AQIValue { get; set; }
            public DateTime LastUpdated { get; set; }
            public int CityID { get; set; }

            public int? ReturnCountryId { get; set; }
            public int? ReturnCityId { get; set; }
            public int? ReturnPollutantId { get; set; }
            public int? ReturnPage { get; set; }
        }
        public class AirQualityRecordDetailViewRow
        {
            public int RecordID { get; set; }
            public int CityID { get; set; }
            public int PollutantID { get; set; }
            public string PollutantName { get; set; }
            public string CategoryName { get; set; }
            public int AQIValue { get; set; }
            public DateTime LastUpdated { get; set; }
        }
    }
}
