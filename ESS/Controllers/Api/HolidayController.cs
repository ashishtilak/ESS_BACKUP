using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using ESS.Dto;

namespace ESS.Controllers.Api
{
    public class HolidayController : ApiController
    {
        public IHttpActionResult GetHolidays(string compCode, string wrkGrp, int tYear)
        {
            try
            {
                List<HolidayDto> holidays = Helpers.CustomHelper.GetHolidays(compCode, wrkGrp, tYear);
                return Ok(holidays);
            }
            catch (Exception e)
            {
                return BadRequest(e.ToString());
            }
        }
    }
}
