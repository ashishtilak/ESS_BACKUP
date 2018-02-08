using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ESS.Models;

namespace ESS.Controllers
{
    public class StationsController : Controller
    {
        private ApplicationDbContext _context;

        public StationsController()
        {
            _context = new ApplicationDbContext();
        }

        protected override void Dispose(bool disposing)
        {
            _context.Dispose();
            base.Dispose(disposing);
        }

        // GET: Stations
        public ActionResult Index()
        {
            var stations = _context.Stations.ToList();
            return View(stations);
        }
    }
}