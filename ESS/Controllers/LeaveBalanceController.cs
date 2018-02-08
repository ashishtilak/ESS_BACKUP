using System;
using System.Data.Entity;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ESS.Models;

namespace ESS.Controllers
{
    public class LeaveBalanceController : Controller
    {
        private ApplicationDbContext _context;

        public LeaveBalanceController()
        {
            _context = new ApplicationDbContext();
        }

        protected override void Dispose(bool disposing)
        {
            _context.Dispose();
            base.Dispose(disposing);
        }

        // GET: LeaveBalance
        public ActionResult Index(string empUnqId)
        {
            
            var leaveBalance = empUnqId == null ? 
                _context.LeaveBalance.ToList() : 
                _context.LeaveBalance.Where(e => e.EmpUnqId == empUnqId).ToList();

            return View(leaveBalance);
        }
    }
}