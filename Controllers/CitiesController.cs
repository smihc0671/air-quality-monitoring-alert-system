using System;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using AirQualityAnalysis.Filters;

namespace AirQualityAnalysis.Controllers
{
    [LoginRequired]
    public class CitiesController : Controller
    {
        private AirQualityDBEntities db = new AirQualityDBEntities();

        public ActionResult Index(string searchCity = "", string sortOption = "cityNameAsc", int page = 1, int pageSize = 10)
        {
            var query =
                from city in db.Cities
                join country in db.Countries
                    on city.CountryID equals country.CountryID
                select new CityListItemViewModel
                {
                    CityID = city.CityID,
                    CityName = city.CityName,
                    CountryName = country.CountryName,
                    LastUpdated = city.LastUpdated
                };

            if (!string.IsNullOrWhiteSpace(searchCity))
            {
                query = query.Where(x => x.CityName.Contains(searchCity));
            }

            switch (sortOption)
            {
                case "cityIdAsc":
                    query = query.OrderBy(x => x.CityID);
                    break;

                case "cityIdDesc":
                    query = query.OrderByDescending(x => x.CityID);
                    break;

                case "cityNameDesc":
                    query = query.OrderByDescending(x => x.CityName);
                    break;

                case "countryNameAsc":
                    query = query.OrderBy(x => x.CountryName);
                    break;

                case "countryNameDesc":
                    query = query.OrderByDescending(x => x.CountryName);
                    break;

                case "dateAsc":
                    query = query.OrderBy(x => x.LastUpdated);
                    break;

                case "dateDesc":
                    query = query.OrderByDescending(x => x.LastUpdated);
                    break;

                case "cityNameAsc":
                default:
                    query = query.OrderBy(x => x.CityName);
                    break;
            }

            int totalRecords = query.Count();
            int totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

            if (totalPages == 0)
                page = 1;
            else if (page > totalPages)
                page = totalPages;

            var cities = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.SearchCity = searchCity;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSizes = new[] { 10, 25, 50 };
            ViewBag.SortOption = sortOption;

            return View(cities);
        }
        [AdminOnly]
        public ActionResult Create()
        {
            ViewBag.CountryID = new SelectList(
                db.Countries.OrderBy(x => x.CountryName).ToList(),
                "CountryID",
                "CountryName"
            );

            return View();
        }
        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(City city)
        {
            if (city.CountryID == 0 || string.IsNullOrWhiteSpace(city.CityName))
            {
                ModelState.AddModelError("", "Country and City Name are required.");
            }

            if (ModelState.IsValid)
            {
                city.LastUpdated = DateTime.Now;
                db.Cities.Add(city);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.CountryID = new SelectList(
                db.Countries.OrderBy(x => x.CountryName).ToList(),
                "CountryID",
                "CountryName",
                city.CountryID
            );

            return View(city);
        }
        [AdminOnly]
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            City city = db.Cities.Find(id);

            if (city == null)
                return HttpNotFound();

            var country = db.Countries.Find(city.CountryID);
            ViewBag.CountryName = country != null ? country.CountryName : "";

            return View(city);
        }
        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(City city)
        {
            if (ModelState.IsValid)
            {
                var existing = db.Cities.Find(city.CityID);

                if (existing == null)
                    return HttpNotFound();

                existing.CityName = city.CityName;
                existing.LastUpdated = DateTime.Now;

                db.SaveChanges();
                return RedirectToAction("Index");
            }

            var country = db.Countries.Find(city.CountryID);
            ViewBag.CountryName = country != null ? country.CountryName : "";

            return View(city);
        }
        [AdminOnly]
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            City city = db.Cities.Find(id);

            if (city == null)
                return HttpNotFound();

            var country = db.Countries.Find(city.CountryID);
            ViewBag.CountryName = country != null ? country.CountryName : "";

            return View(city);
        }
        [AdminOnly]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            City city = db.Cities.Find(id);

            if (city == null)
                return HttpNotFound();

            try
            {
                db.Cities.Remove(city);
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                var country = db.Countries.Find(city.CountryID);
                ViewBag.CountryName = country != null ? country.CountryName : "";
                ViewBag.ErrorMessage = ex.InnerException != null
                    ? ex.InnerException.Message
                    : ex.Message;

                return View("Delete", city);
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

        public class CityListItemViewModel
        {
            public int CityID { get; set; }
            public string CityName { get; set; }
            public string CountryName { get; set; }
            public DateTime LastUpdated { get; set; }
        }

        public JsonResult GetCitiesByCountry(int countryId)
        {
            var cities = db.Cities
                .Where(x => x.CountryID == countryId)
                .OrderBy(x => x.CityName)
                .Select(x => new
                {
                    x.CityID,
                    x.CityName
                })
                .ToList();

            return Json(cities, JsonRequestBehavior.AllowGet);
        }
    }
}