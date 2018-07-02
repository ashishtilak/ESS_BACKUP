using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using ESS.Dto;
using ESS.Models;
using Newtonsoft.Json;

namespace ESS.Controllers.Api
{
    public class LeaveValidateController : ApiController
    {
        private ApplicationDbContext _context;

        public LeaveValidateController()
        {
            _context = new ApplicationDbContext();
        }

        [HttpPost]
        public IHttpActionResult IsValid([FromBody] object requestData)
        {
            List<string> error = new List<string>();

            LeaveApplicationDto lDto;
            try
            {
                lDto = JsonConvert.DeserializeObject<LeaveApplicationDto>(requestData.ToString());
            }
            catch (Exception e)
            {
                return BadRequest(e.ToString());
            }

            //get leave balance each leave wise for current year

            int yearMonth = _context.OpenMonth.Select(t => t.YearMonth).Single();
            int year = _context.OpenMonth.Select(t => t.OpenYear).Single();

            var monthFirst = DateTime.ParseExact(
                String.Format("{0}-{1}-{2}", yearMonth.ToString().Substring(0, 4), yearMonth.ToString().Substring(4, 2), "01"),
                "yyyy-MM-dd", null).AddMonths(-1);

            var monthLast = DateTime.ParseExact(
                String.Format("{0}-{1}-{2}", yearMonth.ToString().Substring(0, 4), yearMonth.ToString().Substring(4, 2), "01"),
                "yyyy-MM-dd", null).AddMonths(4).AddDays(-1);

            // Reject if any leave is pending for release

            var pendingLeave = _context.LeaveApplications
                .Where(l => l.EmpUnqId == lDto.EmpUnqId &&
                            (l.ReleaseStatusCode == ReleaseStatus.PartiallyReleased ||
                             l.ReleaseStatusCode == ReleaseStatus.NotReleased))
                .ToList();

            if (pendingLeave.Count > 0)
            {
                error.Add("Cannot take leave if any application is pending for release.");
                return Content(HttpStatusCode.BadRequest, error);
            }


            // Use helper class to get leavebalance form attendance server
            var leaveBalDto = ESS.Helpers.CustomHelper.GetLeaveBalance(lDto.EmpUnqId, year);


            //
            // Raag Bhoopali:
            // Aaroh: Sa Re Ga Pa Dha Sa.  Avroh: Sa. Dha Pa Ga Re Sa
            // Pakad: Ga Re Sa .Dh, Sa Re Ga, Pa Ga, Dha Pa Ga, Re Sa
            //

            //store weekly off and holidays in this variable
            //then deduct it in date continuation check

            float offDays = 0;

            #region ForEach_on_leaveAppDetails

            foreach (var details in lDto.LeaveApplicationDetails)
            {
                //check if leave type is there in balance table
                bool leaveExist = leaveBalDto.Any(l => l.LeaveTypeCode == details.LeaveTypeCode);

                if (!leaveExist &&
                    (details.LeaveTypeCode != LeaveTypes.LeaveWithoutPay && details.LeaveTypeCode != LeaveTypes.CompOff))
                {
                    error.Add("There is no balance available for leave type: " + details.LeaveTypeCode);
                    continue;
                }

                //Check if dates are within open month
                if (details.FromDt < monthFirst || details.ToDt > monthLast)
                    error.Add(details.LeaveTypeCode + " leave date must be within open month: "
                              + monthFirst.ToShortDateString() + " - " + monthLast.ToShortDateString());


                //check that the date should not overlap with existing leave taken
                var existingLeave = _context.LeaveApplicationDetails
                    .Where(l =>
                        l.LeaveApplication.EmpUnqId == lDto.EmpUnqId &&
                        ((l.FromDt <= details.ToDt && l.ToDt >= details.FromDt) ||
                        (l.ToDt <= details.FromDt && l.FromDt >= details.ToDt)) &&
                        l.LeaveApplication.ReleaseStatusCode == ReleaseStatus.FullyReleased &&
                        l.Cancelled == false
                    )
                    .ToList();

                if (existingLeave.Count > 0)
                    error.Add(details.LeaveTypeCode + " leave date must not overlap with leave already taken. ");

                //Check that previous leave should not be CL the day before this leave app's date
                //this check is for leaves other than LWP

                if ((details.LeaveTypeCode != LeaveTypes.LeaveWithoutPay) &&
                    (details.LeaveTypeCode != LeaveTypes.OptionalLeave) &&
                    (details.LeaveTypeCode != LeaveTypes.CasualLeave) &&
                    (details.LeaveTypeCode != LeaveTypes.CompOff)
                   )
                {
                    var detailsFromDt = details.FromDt.AddDays(-1);
                    existingLeave = _context.LeaveApplicationDetails
                        .Where(l =>
                            l.LeaveApplication.EmpUnqId == lDto.EmpUnqId &&
                            l.Cancelled == false &&
                            l.ToDt == detailsFromDt
                        )
                        .ToList();
                    if (existingLeave.Count > 0)
                    {
                        if (existingLeave.Any(x => x.LeaveTypeCode == LeaveTypes.CasualLeave))
                            error.Add("You have taken CL on previous day! Can't take this leave.");
                    }
                }


                // Take days == 1 only, as 
                // start date will be Week Off date,
                // End date will be date of CO.

                if (details.LeaveTypeCode == LeaveTypes.CompOff)
                    details.TotalDays = 1;


                //break out of loop in case of LWP
                if (details.LeaveTypeCode == LeaveTypes.LeaveWithoutPay ||
                    details.LeaveTypeCode == LeaveTypes.CompOff)
                    continue;


                //now get holidays between from/to date and add them to off days
                List<DateTime> holidays =
                    ESS.Helpers.CustomHelper.GetHolidays(details.FromDt, details.ToDt, lDto.CompCode, lDto.WrkGrp);

                details.TotalDays -= holidays.Count;

                if (details.TotalDays == 0)
                    error.Add("You cannot take " + details.LeaveTypeCode + " on a holiday.");

                offDays += holidays.Count;


                // Get weekly offs between the selected range
                List<DateTime> weekOffs =
                    ESS.Helpers.CustomHelper.GetWeeklyOff(details.FromDt, details.ToDt, lDto.EmpUnqId);


                //Check if weekoff is on holiday. If it is, then remove it.
                if (holidays.Count > 0)
                {
                    weekOffs.RemoveAll(w => holidays.Any(h => h == w));
                }

                details.TotalDays -= weekOffs.Count;

                if (details.TotalDays == 0)
                    error.Add("You cannot take " + details.LeaveTypeCode + " on weekly off day.");

                offDays += weekOffs.Count;





                // CL can't be more than 3 days
                if (details.LeaveTypeCode == LeaveTypes.CasualLeave &&
                    details.TotalDays > 3)
                    error.Add("CL cannot be more than 3 days");

                // EL Must be at least 3 days
                if (details.LeaveTypeCode == LeaveTypes.PaidLeave &&
                    details.TotalDays < 3)
                    error.Add("EL cannot be less than 3 days");



                //check leave balance
                LeaveBalanceDto lb = leaveBalDto.Single(l => l.LeaveTypeCode == details.LeaveTypeCode);
                float bal = lb.Opening - lb.Availed - lb.Encashed;

                if (bal < details.TotalDays)
                    error.Add("Insufficient balance of " + details.LeaveTypeCode + ". Current Balance is: " + bal);

                if (details.LeaveTypeCode == LeaveTypes.OptionalLeave)
                {
                    if (details.TotalDays > 1)
                        error.Add("Only one OH is allowed.");

                    if (!Helpers.CustomHelper.GetOptionalHolidays(details.FromDt))
                        error.Add("Invalid Optional holiday. Pl verify date.");

                }


                //Checks involving previous days leaves.
                //Get previous 5 days data in dictionary

                Dictionary<DateTime, string> prevLeave = new Dictionary<DateTime, string>();
                DateTime d = details.FromDt.AddDays(-1);
                string strLeave = "";
                while (d >= details.FromDt.AddDays(-5))
                {
                    prevLeave.Add(d, GetLeaveOnDate(d, lDto.EmpUnqId));


                    if (!string.IsNullOrEmpty(prevLeave[d]))
                        strLeave += prevLeave[d] + ",";
                    else
                    {
                        if (ESS.Helpers.CustomHelper.GetWeeklyOff(d, d, lDto.EmpUnqId).Count > 0)
                            strLeave += "WO,";
                        else
                            strLeave += prevLeave[d] + ",";
                    }
                    d = d.AddDays(-1);
                }
                //got data in array along with WO


                // 1st simple check... if current leave is CL
                var leaveRules = _context.LeaveRules
                    .FirstOrDefault(l => strLeave.StartsWith(l.LeaveRule));

                if (details.LeaveTypeCode == LeaveTypes.CasualLeave)
                {
                    if (leaveRules != null)
                        if (details.TotalDays > leaveRules.AllowedCl)
                            error.Add("Cannot take this CL. Check previous leaves.");
                }
                else
                {
                    //For other types of leaves.
                }

            }
            #endregion

            //if there are multiple leaves and one of them is CL, throw error
            if (lDto.LeaveApplicationDetails.Any(x => x.LeaveTypeCode == LeaveTypes.CasualLeave)
                && lDto.LeaveApplicationDetails.Count > 1)
            {
                var leaves = lDto.LeaveApplicationDetails.Where(x => x.LeaveTypeCode != LeaveTypes.CasualLeave);

                //find if there's a leave type other than LWP
                bool found = leaves.Any(d => d.LeaveTypeCode != LeaveTypes.LeaveWithoutPay);

                if (found)
                    error.Add("CL cannot be clubbed with any other leaves.");
            }


            //throw error if multiple CL are applied in the same application:
            if (lDto.LeaveApplicationDetails.Count(x => x.LeaveTypeCode == LeaveTypes.CasualLeave) > 1)
            {
                error.Add("Cannot take multiple CLs in single Leave Application.");
            }

            //Date range check
            DateTime start = lDto.LeaveApplicationDetails.Select(x => x.FromDt).Min();
            DateTime end = lDto.LeaveApplicationDetails.Select(x => x.ToDt).Max();
            double total = lDto.LeaveApplicationDetails.Select(x => (x.ToDt.Subtract(x.FromDt).TotalDays + 1)).Sum();

            //subtract off days (w/offs + holidays) from above total
            total -= offDays;

            if (Math.Abs(total - ((end.Subtract(start).TotalDays + 1) - offDays)) > 0)
                error.Add("Date ranges must be continuous. No gaps allowed");


            //Check if ranges overlap

            var overlaps = lDto.LeaveApplicationDetails
                .SelectMany(
                    x1 => lDto.LeaveApplicationDetails,
                    (x1, x2) => new { x1, x2 })
                    .Where(t => !(Equals(t.x1, t.x2)))
                    .Where(t => (t.x1.FromDt <= t.x2.ToDt) && (t.x1.ToDt >= t.x2.FromDt))
                    .Select(t => t.x2);

            if (overlaps.Any())
                error.Add("Date ranges must be consicutive, should not overlap.");


            // CHECKS FOR COMP OFF ( CO )

            // check if start date is a week off
            if (lDto.LeaveApplicationDetails.Any(x => x.LeaveTypeCode == LeaveTypes.CompOff))
            {
                if (ESS.Helpers.CustomHelper.GetWeeklyOff(start, start, lDto.EmpUnqId).Count != 1)
                {
                    error.Add("Date selected is not week off as per shift schedule.");
                }
                else
                {
                    // Check if CO date is <= 3 days from WO day
                    if (end.Subtract(start).TotalDays > 3)
                        error.Add("Comp. Off can be taken within 3 days of Week Off.");
                }

            }


            // DONE. If there's no error, return success
            if (error.Count == 0)
                return Ok(lDto);


            return Content(HttpStatusCode.BadRequest, error);

        }

        private string GetLeaveOnDate(DateTime dt, string empUnqId)
        {
            var leave = _context.LeaveApplicationDetails
                .FirstOrDefault(l => l.LeaveApplication.EmpUnqId == empUnqId &&
                    (dt >= l.FromDt && dt <= l.ToDt) &&
                    l.LeaveApplication.ReleaseStatusCode != ReleaseStatus.ReleaseRejected);

            return leave != null ? leave.LeaveTypeCode : "";
        }
    }
}
