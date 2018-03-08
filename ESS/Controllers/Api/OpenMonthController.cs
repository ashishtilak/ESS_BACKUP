using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using ESS.Models;

namespace ESS.Controllers.Api
{
    public class OpenMonthController : ApiController
    {
        private ApplicationDbContext _context;

        public OpenMonthController()
        {
            _context = new ApplicationDbContext();
        }

        [HttpPost]
        public IHttpActionResult ChangeOpenMonth(int yearMonth)
        {
            var currentMonth = _context.OpenMonth.First();

            if (yearMonth < currentMonth.YearMonth)
                return BadRequest("New open month cannot be less than current open month.");

            currentMonth.OpenYear = int.Parse(yearMonth.ToString().Substring(0, 4));
            DateTime prevMonth = DateTime.Parse("01/" + yearMonth.ToString().Substring(4, 2) + "/" +
                                                yearMonth.ToString().Substring(0, 4)).AddMonths(-1);

            currentMonth.PrevMonth = int.Parse(prevMonth.Year.ToString() + prevMonth.Month.ToString("00"));
            currentMonth.YearMonth = yearMonth;
            //_context.SaveChanges();

            string sql = "UPDATE OpenMonths " +
                         "Set YearMonth = " + currentMonth.YearMonth + ", " +
                         "OpenYear = " + currentMonth.OpenYear + ", " +
                         "PrevMonth = " + currentMonth.PrevMonth;

            _context.Database.ExecuteSqlCommand(sql);

            return Ok();
        }
    }
}
