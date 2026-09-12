using System;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using AirQualityAnalysis;
using AirQualityAnalysis.Filters;

namespace AirQualityAnalysis.Controllers
{
    [LoginRequired]
    public class CountriesController : Controller
    {
        private AirQualityDBEntities db = new AirQualityDBEntities();

        public ActionResult Index(string searchText = "", string sortOption = "nameAsc", int page = 1, int pageSize = 10)
        {
            var query = db.Countries.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                string search = searchText.Trim().ToLower();
                query = query.Where(x => x.CountryName.ToLower().Contains(search));
            }

            switch (sortOption)
            {
                case "nameDesc":
                    query = query.OrderByDescending(x => x.CountryName);
                    break;

                case "idAsc":
                    query = query.OrderBy(x => x.CountryID);
                    break;

                case "idDesc":
                    query = query.OrderByDescending(x => x.CountryID);
                    break;

                case "dateAsc":
                    query = query.OrderBy(x => x.LastUpdated);
                    break;

                case "dateDesc":
                    query = query.OrderByDescending(x => x.LastUpdated);
                    break;

                case "nameAsc":
                default:
                    query = query.OrderBy(x => x.CountryName);
                    break;
            }

            int totalRecords = query.Count();
            int totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

            if (totalPages == 0)
            {
                page = 1;
            }
            else if (page > totalPages)
            {
                page = totalPages;
            }

            var countries = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.SearchText = searchText;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.TotalPages = totalPages;
            ViewBag.SortOption = sortOption;

            return View(countries);
        }
        [AdminOnly]
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Country country = db.Countries.Find(id);

            if (country == null)
                return HttpNotFound();

            return View(country);
        }
        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Country country)
        {
            if (ModelState.IsValid)
            {
                var existing = db.Countries.Find(country.CountryID);

                if (existing == null)
                    return HttpNotFound();

                existing.CountryName = country.CountryName;
                existing.LastUpdated = DateTime.Now;

                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(country);
        }
        [AdminOnly]
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Country country = db.Countries.Find(id);

            if (country == null)
                return HttpNotFound();

            return View(country);
        }
        [AdminOnly]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Country country = db.Countries.Find(id);

            if (country == null)
                return HttpNotFound();

            try
            {
                db.Countries.Remove(country);
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = ex.InnerException != null
                    ? ex.InnerException.Message
                    : ex.Message;

                return View(country);
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
        [AdminOnly]
        public ActionResult Create()
        {
            return View();
        }
        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Country country)
        {
            if (ModelState.IsValid)
            {
                country.LastUpdated = DateTime.Now;

                db.Countries.Add(country);
                db.SaveChanges();

                return RedirectToAction("Index");
            }

            return View(country);
        }
    }
}