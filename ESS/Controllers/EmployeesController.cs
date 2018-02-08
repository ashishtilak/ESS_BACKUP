using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ESS.Models;
using PagedList;
using PagedList.Mvc;



namespace ESS.Controllers
{
    public class EmployeesController : Controller
    {
        private ApplicationDbContext _context;

        public EmployeesController()
        {
            _context = new ApplicationDbContext();
        }

        protected override void Dispose(bool disposing)
        {
            _context.Dispose();
            base.Dispose(disposing);
        }

        // GET: Employees
        public ActionResult Index(int? pageNumber)
        {
            var employees = _context.Employees.ToList().ToPagedList(pageNumber ?? 1, 20);
            return View(employees);
        }
    }
}