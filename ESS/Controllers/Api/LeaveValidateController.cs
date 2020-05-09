using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using ESS.Dto;
using ESS.Models;
using Newtonsoft.Json;

namespace ESS.Controllers.Api
{
    public class LeaveValidateController : ApiController
    {
        private readonly ApplicationDbContext _context;

        public LeaveValidateController()
        {
            _context = new ApplicationDbContext();
        }

        [HttpPost]
        public IHttpActionResult IsValid([FromBody] object requestData)
        {
            var error = new List<string>();

            LeaveApplicationDto leaveApplicationDto;
            try
            {
                leaveApplicationDto = JsonConvert.DeserializeObject<LeaveApplicationDto>(requestData.ToString());
            }
            catch (Exception e)
            {
                return BadRequest(e.ToString());
            }

            //get leave balance each leave wise for current year

            Employees emp = _context.Employees.Single(e => e.EmpUnqId == leaveApplicationDto.EmpUnqId);

            int yearMonth = _context.OpenMonth.Select(t => t.YearMonth).Single();
            int year = _context.OpenMonth.Select(t => t.OpenYear).Single();

            DateTime monthFirst = DateTime.ParseExact(
                $"{yearMonth.ToString().Substring(0, 4)}-{yearMonth.ToString().Substring(4, 2)}-01",
                "yyyy-MM-dd", null).AddMonths(-1);

            DateTime monthLast = DateTime.ParseExact(
                $"{yearMonth.ToString().Substring(0, 4)}-{yearMonth.ToString().Substring(4, 2)}-{"01"}",
                "yyyy-MM-dd", null).AddMonths(4).AddDays(-1);


            // CHECK 1 Reject if any leave is pending for release
            var pendingLeave = _context.LeaveApplications
                .Where(l => l.EmpUnqId == leaveApplicationDto.EmpUnqId &&
                            (l.ReleaseStatusCode == ReleaseStatus.PartiallyReleased ||
                             l.ReleaseStatusCode == ReleaseStatus.NotReleased))
                .ToList();

            if (pendingLeave.Count > 0)
            {
                error.Add("Cannot take leave if any application is pending for release.");
                return Content(HttpStatusCode.BadRequest, error);
            }
            // CHECK 1 OVER


            // Use helper class to get leavebalance form attendance server
            var leaveBalDto = Helpers.CustomHelper.GetLeaveBalance(leaveApplicationDto.EmpUnqId, year);

            //
            // Raag Bhoopali:
            // Aaroh: Sa Re Ga Pa Dha Sa.  Avroh: Sa. Dha Pa Ga Re Sa
            // Pakad: Ga Re Sa .Dh, Sa Re Ga, Pa Ga, Dha Pa Ga, Re Sa
            //

            //store weekly off and holidays in this variable
            //then deduct it in date continuation check

            float offDays = 0;

            #region ForEach_on_leaveAppDetails

            foreach (LeaveApplicationDetailDto details in leaveApplicationDto.LeaveApplicationDetails)
            {
                if (details.TotalDays <= 0)
                {
                    error.Add("Leave days less then zero????");
                    continue;
                }

                //check2: check if leave type is there in balance table
                bool leaveExist = leaveBalDto.Any(l => l.LeaveTypeCode == details.LeaveTypeCode);

                if (!leaveExist &&
                    (details.LeaveTypeCode != LeaveTypes.LeaveWithoutPay &&
                     details.LeaveTypeCode != LeaveTypes.CompOff &&
                     details.LeaveTypeCode != LeaveTypes.OutdoorDuty && 
                     details.LeaveTypeCode != LeaveTypes.WeekOff))
                {
                    error.Add("There is no balance available for leave type: " + details.LeaveTypeCode);
                    continue;
                }
                //check2 over

                //Check3 if dates are within open month
                if (details.FromDt < monthFirst || details.ToDt > monthLast)
                    error.Add(details.LeaveTypeCode + " leave date must be within open month: "
                                                    + monthFirst.ToShortDateString() + " - " +
                                                    monthLast.ToShortDateString());
                //check3 over

                
                if (details.LeaveTypeCode == LeaveTypes.OutdoorDuty ||
                    details.LeaveTypeCode == LeaveTypes.WeekOff)
                    continue;
                
                // Re calculate days because, days passed from client are from grid
                // which are actual days (total days - holidays - weekoffs)
                // which are again deducted

                details.TotalDays = details.HalfDayFlag ? 0.5f : (details.ToDt - details.FromDt).Days + 1;

                // for CO, Take days == 1 only, as 
                // start date will be Week Off date,
                // End date will be date of CO.

                if (details.LeaveTypeCode == LeaveTypes.CompOff)
                    details.TotalDays = 1;

                // allow half CO in case of JFL tembhurni
                if (emp.Location == Locations.Jfl &&
                    details.LeaveTypeCode == LeaveTypes.CompOff && details.HalfDayFlag == true)
                    details.TotalDays = 0.5f;
                

                //check4 that the date should not overlap with existing leave taken
                var existingLeave = _context.LeaveApplicationDetails
                    .Where(l =>
                        l.LeaveApplication.EmpUnqId == leaveApplicationDto.EmpUnqId &&
                        ((l.FromDt <= details.ToDt && l.ToDt >= details.FromDt) ||
                         (l.ToDt <= details.FromDt && l.FromDt >= details.ToDt)) &&
                        l.LeaveApplication.ReleaseStatusCode == ReleaseStatus.FullyReleased &&
                        l.Cancelled == false
                    )
                    .ToList();

                if (existingLeave.Count > 0)
                {
                    error.Add(details.LeaveTypeCode + " leave date must not overlap with leave already taken. ");
                    continue;
                }

                //check4 over

                //break out of loop in case of LWP                                                                                            
                if (details.LeaveTypeCode == LeaveTypes.LeaveWithoutPay)
                    continue;


                // Get holidays between from/to date and add them to off days
                var holidays =
                    Helpers.CustomHelper.GetHolidays(details.FromDt, details.ToDt, leaveApplicationDto.CompCode,
                        leaveApplicationDto.WrkGrp, emp.Location);

                details.TotalDays -= holidays.Count;

                if (details.TotalDays == 0)
                    error.Add("You cannot take " + details.LeaveTypeCode + " on a holiday.");

                offDays += holidays.Count;

                // Get weekly offs between the selected range
                var weekOffs =
                    Helpers.CustomHelper.GetWeeklyOff(details.FromDt, details.ToDt, leaveApplicationDto.EmpUnqId);

                //Check if weekoff is on holiday. If it is, then remove it.
                if (holidays.Count > 0)
                {
                    weekOffs.RemoveAll(w => holidays.Any(h => h == w));
                }

                details.TotalDays -= weekOffs.Count;

                if (details.TotalDays == 0)
                    error.Add("You cannot take " + details.LeaveTypeCode + " on weekly off day.");

                offDays += weekOffs.Count;


                //CHECK6: check leave balance other than CO

                if (details.LeaveTypeCode != LeaveTypes.CompOff)
                {
                    LeaveBalanceDto lb = leaveBalDto.Single(l => l.LeaveTypeCode == details.LeaveTypeCode);
                    float bal = lb.Opening - lb.Availed - lb.Encashed;

                    if (bal < details.TotalDays)
                        error.Add("Insufficient balance of " + details.LeaveTypeCode + ". Current Balance is: " + bal);
                }
                //CHECK6 OVER


                //Checks involving previous and next days leaves
                //Get previous 6 days data in dictionary

                var prevLeave = new Dictionary<DateTime, string>();
                DateTime d = details.FromDt.AddDays(-1);
                string strPrevLeave = "";
                while (d >= details.FromDt.AddDays(-6))
                {
                    prevLeave.Add(d, GetLeaveOnDate(d, leaveApplicationDto.EmpUnqId));

                    if (!string.IsNullOrEmpty(prevLeave[d]))
                        strPrevLeave += prevLeave[d] + ",";
                    else
                    {
                        if (Helpers.CustomHelper.GetWeeklyOff(d, d, leaveApplicationDto.EmpUnqId).Count > 0)
                            strPrevLeave += "WO,";
                        else if (Helpers.CustomHelper.GetHolidays(d, d, leaveApplicationDto.CompCode,
                                     leaveApplicationDto.WrkGrp, emp.Location).Count > 0)
                            strPrevLeave += "HL,";
                        else
                            strPrevLeave += prevLeave[d] + ",";
                    }

                    d = d.AddDays(-1);
                }
                //got data in array along with WO


                //Get next 6 days data in dictionary
                var nextLeave = new Dictionary<DateTime, string>();
                d = details.ToDt.AddDays(1);
                string strNextLeave = "";
                while (d <= details.ToDt.AddDays(6))
                {
                    nextLeave.Add(d, GetLeaveOnDate(d, leaveApplicationDto.EmpUnqId));


                    if (!string.IsNullOrEmpty(nextLeave[d]))
                        strNextLeave += nextLeave[d] + ",";
                    else
                    {
                        if (Helpers.CustomHelper.GetWeeklyOff(d, d, leaveApplicationDto.EmpUnqId).Count > 0)
                            strNextLeave += "WO,";
                        else if (Helpers.CustomHelper.GetHolidays(d, d, leaveApplicationDto.CompCode,
                                     leaveApplicationDto.WrkGrp, emp.Location).Count > 0)
                            strNextLeave += "HL,";
                        else
                            strNextLeave += nextLeave[d] + ",";
                    }

                    d = d.AddDays(1);
                }
                //got data in array along with WO


                //Get leave rules for both prev & next
                LeaveRules prevLeaveRules = _context.LeaveRules
                    .FirstOrDefault(l =>
                        strPrevLeave.StartsWith(l.LeaveRule) && details.LeaveTypeCode == l.LeaveTypeCode);

                LeaveRules nextLeaveRules = _context.LeaveRules
                    .FirstOrDefault(l =>
                        strNextLeave.StartsWith(l.LeaveRule) && details.LeaveTypeCode == l.LeaveTypeCode);


                // *****************************************
                //  LEAVE TYPE WISE CHECKS................
                // *****************************************

                //CHECKS FOR CL
                if (details.LeaveTypeCode == LeaveTypes.CasualLeave)
                {
                    // CL can't be more than 3 days
                    if (details.TotalDays > 3)
                        error.Add("CL cannot be more than 3 days");

                    // If some leave rule is found for prev/next leaves, check it here...

                    if (prevLeaveRules != null && prevLeaveRules.Active)
                        if (details.TotalDays > prevLeaveRules.DaysAllowed)
                            error.Add("Cannot take this CL. Check leaves taken on previous days.");

                    if (nextLeaveRules != null && nextLeaveRules.Active)
                        if (details.TotalDays > nextLeaveRules.DaysAllowed)
                            error.Add("Cannot take this CL. Check leaves taken on next days.");
                }

                //CHECKS FOR EL/PL
                if (details.LeaveTypeCode == LeaveTypes.PaidLeave)
                {
                    // EL Must be at least 3 days
                    if (details.TotalDays < 3)
                        error.Add("EL cannot be less than 3 days");

                    // If some leave rule is found for prev/next leaves, check it here...

                    if (prevLeaveRules != null && prevLeaveRules.Active)
                        if (details.TotalDays > prevLeaveRules.DaysAllowed || !prevLeaveRules.LeaveAllowed)
                            error.Add("Cannot take this EL. Check leaves taken on previous days.");

                    if (nextLeaveRules != null && nextLeaveRules.Active)
                        if (details.TotalDays > nextLeaveRules.DaysAllowed || !nextLeaveRules.LeaveAllowed)
                            error.Add("Cannot take this EL. Check leaves taken on next days.");
                }

                //CHECKS FOR SL
                if (details.LeaveTypeCode == LeaveTypes.SickLeave)
                {
                    // If some leave rule is found for prev/next leaves, check it here...
                    if (prevLeaveRules != null && prevLeaveRules.Active)
                        if (details.TotalDays > prevLeaveRules.DaysAllowed || !prevLeaveRules.LeaveAllowed)
                            error.Add("Cannot take this SL. Check leaves taken on previous days.");

                    if (nextLeaveRules != null && nextLeaveRules.Active)
                        if (details.TotalDays > nextLeaveRules.DaysAllowed || !nextLeaveRules.LeaveAllowed)
                            error.Add("Cannot take this SL. Check leaves taken on next days.");
                }


                if (details.LeaveTypeCode == LeaveTypes.OptionalLeave)
                {
                    if (details.TotalDays > 1)
                        error.Add("Only one OH is allowed.");

                    if (details.HalfDayFlag)
                        error.Add("Half day OH is no allowed.");

                    if (!Helpers.CustomHelper.GetOptionalHolidays(details.FromDt, emp.Location))
                        error.Add("Invalid Optional holiday. Pl verify date.");


                    // If some leave rule is found for prev/next leaves, check it here...
                    if (prevLeaveRules != null && prevLeaveRules.Active)
                        if (details.TotalDays > prevLeaveRules.DaysAllowed || !prevLeaveRules.LeaveAllowed)
                            error.Add("Cannot take this OL. Check leaves taken on previous days.");

                    if (nextLeaveRules != null && nextLeaveRules.Active)
                        if (details.TotalDays > nextLeaveRules.DaysAllowed || !nextLeaveRules.LeaveAllowed)
                            error.Add("Cannot take this OL. Check leaves taken on next days.");

                }
            }

            #endregion

            //CHECKS FOR WHOLE LEAVE OBJECTS COMBINING LEAVE DETAIL DATA

            //if there are multiple leaves and one of them is CL, throw error
            if (leaveApplicationDto.LeaveApplicationDetails.Any(x => x.LeaveTypeCode == LeaveTypes.CasualLeave)
                && leaveApplicationDto.LeaveApplicationDetails.Count > 1)
            {
                var leaves =
                    leaveApplicationDto.LeaveApplicationDetails.Where(x => x.LeaveTypeCode != LeaveTypes.CasualLeave);

                //find if there's a leave type other than LWP
                bool found = leaves.Any(d => d.LeaveTypeCode != LeaveTypes.LeaveWithoutPay &&
                                             d.LeaveTypeCode != LeaveTypes.OptionalLeave);

                if (found)
                    error.Add("CL cannot be clubbed with any other leaves.");
            }


            foreach (var leaveType in leaveApplicationDto.LeaveApplicationDetails.Select(l => l.LeaveTypeCode)
                .Distinct())
            {
                if (leaveType != LeaveTypes.CasualLeave && leaveType != LeaveTypes.SickLeave &&
                    leaveType != LeaveTypes.PaidLeave)
                    continue;

                float leaveCount = leaveApplicationDto.LeaveApplicationDetails
                    .Where(detail => detail.LeaveTypeCode == leaveType).Sum(detail => detail.TotalDays);

                if (leaveCount > 3 && leaveType == LeaveTypes.CasualLeave)
                    error.Add("CL cannot be more than 3 days");

                LeaveBalanceDto lb = leaveBalDto.FirstOrDefault(l => l.LeaveTypeCode == leaveType);
                if (lb == null)
                {
                    error.Add("Invalid leave type: " + leaveType + "");
                    continue;
                }

                float bal = lb.Opening - lb.Availed - lb.Encashed;

                if (bal < leaveCount)
                    error.Add("Insufficient balance of " + leaveType + ". Current Balance is: " + bal);
            }


            //throw error if multiple CL are applied in the same application:
            if (leaveApplicationDto.LeaveApplicationDetails.Count(x => x.LeaveTypeCode == LeaveTypes.CasualLeave) > 1)
            {
                //check for half CL...
                bool found = leaveApplicationDto.LeaveApplicationDetails.Any(detail => detail.HalfDayFlag);

                bool findOptional =
                    leaveApplicationDto.LeaveApplicationDetails.Any(detail =>
                        detail.LeaveTypeCode == LeaveTypes.OptionalLeave);

                if (!found)
                {
                    if (!findOptional)
                        error.Add("Cannot take multiple CLs in single Leave Application.");
                }
            }

            //throw error if multiple OH are applied in the same application:
            if (leaveApplicationDto.LeaveApplicationDetails.Count(x => x.LeaveTypeCode == LeaveTypes.OptionalLeave) > 1)
            {
                error.Add("Cannot take multiple Optional Leaves in single Leave Application.");
            }

            //Date range check
            DateTime start = leaveApplicationDto.LeaveApplicationDetails.Select(x => x.FromDt).Min();
            DateTime end = leaveApplicationDto.LeaveApplicationDetails.Select(x => x.ToDt).Max();
            double total = leaveApplicationDto.LeaveApplicationDetails
                .Select(x => (x.ToDt.Subtract(x.FromDt).TotalDays + 1)).Sum();

            //subtract off days (w/offs + holidays) from above total
            total -= offDays;

            if (Math.Abs(total - ((end.Subtract(start).TotalDays + 1) - offDays)) > 0)
                error.Add("Date ranges must be continuous. No gaps allowed");


            //Check if ranges overlap skip in case of CO
            if (leaveApplicationDto.LeaveApplicationDetails.All(x => x.LeaveTypeCode != LeaveTypes.CompOff))
            {
                var overlaps = leaveApplicationDto.LeaveApplicationDetails.SelectMany(
                        x1 => leaveApplicationDto.LeaveApplicationDetails,
                        (x1, x2) => new {x1, x2})
                    .Where(t => !(Equals(t.x1, t.x2)))
                    .Where(t => (t.x1.FromDt <= t.x2.ToDt) && (t.x1.ToDt >= t.x2.FromDt))
                    .Select(t => t.x2);

                if (overlaps.Any())
                    error.Add("Date ranges must be consicutive, should not overlap.");
            }


            // CHECKS FOR COMP OFF ( CO )

            // check if start date is a Holiday
            if (leaveApplicationDto.LeaveApplicationDetails.Any(x => x.LeaveTypeCode == LeaveTypes.CompOff))
            {
                if (start != end)
                {
                    if (Helpers.CustomHelper.GetHolidays(start, start, leaveApplicationDto.CompCode,
                            leaveApplicationDto.WrkGrp, emp.Location).Count != 1)
                    {
                        error.Add("Date selected is not Holiday.");
                    }
                    else
                    {
                        // Check if CO date is <= 7 days from WO day
                        if (emp.Location == Locations.Ipu)
                        {
                            if (end.Subtract(start).TotalDays > 7)
                                error.Add("Comp. Off can be taken within 7 days.");
                        }
                        else if (emp.Location == Locations.Jfl)
                        {
                            if (end.Subtract(start).TotalDays > 30)
                                error.Add("Comp. Off can be taken within 30 days.");
                        }
                        else
                        {
                            if (end.Subtract(start).TotalDays > 90)
                                error.Add("Comp. Off can be taken within 90 days.");
                        }
                    }

                    if (Helpers.CustomHelper.GetWeeklyOff(end, end, leaveApplicationDto.EmpUnqId).Count == 1)
                    {
                        error.Add("Comp. Off cannot be taken on Week Off day.");
                    }
                }
            }

            // DONE. If there's no error, return success
            if (error.Count == 0)
                return Ok(leaveApplicationDto);


            return Content(HttpStatusCode.BadRequest, error);
        }

        private string GetLeaveOnDate(DateTime dt, string empUnqId)
        {
            if (Helpers.CustomHelper.GetWeeklyOff(dt, dt, empUnqId).Count > 0)
                return "WO";

            LeaveApplicationDetails leave = _context.LeaveApplicationDetails
                .FirstOrDefault(l => l.LeaveApplication.EmpUnqId == empUnqId &&
                                     (dt >= l.FromDt && dt <= l.ToDt) &&
                                     l.Cancelled == false &&
                                     l.LeaveApplication.ReleaseStatusCode != ReleaseStatus.ReleaseRejected);

            return leave != null ? leave.LeaveTypeCode : "";
        }
    }
}