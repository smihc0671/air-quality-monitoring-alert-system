using System;
using System.Web.Mvc;

namespace AirQualityAnalysis.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string role, string username, string password)
        {
            if (role == "User")
            {
                Session["UserRole"] = "User";
                Session["Username"] = "User";
                return RedirectToAction("Index", "Home");
            }

            if (role == "Admin")
            {
                var adminUsername =
                    Environment.GetEnvironmentVariable("AIRQUALITY_ADMIN_USERNAME") ?? "admin";

                var adminPassword =
                    Environment.GetEnvironmentVariable("AIRQUALITY_ADMIN_PASSWORD");

                if (username == adminUsername &&
                    !string.IsNullOrEmpty(adminPassword) &&
                    password == adminPassword)
                {
                    Session["UserRole"] = "Admin";
                    Session["Username"] = adminUsername;
                    return RedirectToAction("Index", "Home");
                }

                ViewBag.ErrorMessage = "Admin username or password is incorrect.";
                return View();
            }

            ViewBag.ErrorMessage = "Please select a role.";
            return View();
        }

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login", "Account");
        }
    }
}