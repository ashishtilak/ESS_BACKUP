using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;

using System.Web;
using System.Web.Mvc;
using ESS.Models;

namespace ESS.Controllers
{
    public class DepartmentsController : Controller
    {
        private ApplicationDbContext _context;              // will be used everywhere in this class, just need in constructor

        public DepartmentsController()
        {
            _context = new ApplicationDbContext();
        }

        protected override void Dispose(bool disposing)
        {
            _context.Dispose();
            base.Dispose(disposing);
        }

        // GET: Departments
        public ActionResult Index()
        {
            var departments = _context.Departments.ToList();
            return View(departments);
        }
    }
}