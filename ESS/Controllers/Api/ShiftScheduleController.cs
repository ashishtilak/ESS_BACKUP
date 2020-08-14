using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Configuration;
using System.Web.Hosting;
using System.Web.Http;
using Antlr.Runtime.Misc;
using AutoMapper;
using ESS.Dto;
using ESS.Models;

namespace ESS.Controllers.Api
{
    public class ShiftScheduleController : ApiController
    {
        private readonly ApplicationDbContext _context;

        public ShiftScheduleController()
        {
            _context = new ApplicationDbContext();
        }

        public enum ReportModes
        {
            ExcelDownload,
            CurrentMonthAll,
            CurrentMonthReleased,
            PreviousMonthAll,
            PreviousMonthReleased
        }

        /// <summary>
        /// Get schedule to be used to get blank excel template for all employees under release
        /// </summary>
        /// <param name="empUnqId">SAP code of Releaser</param>
        /// <param name="mode">Report type selection
        /// 0- ExcelDownload, 1-CurrentMonthAll, 2-CurrentMonthReleased,
        /// 3- PreviousMonthAll, 4- PreviousMonthReleased,
        /// </param>
        /// <returns>json data in template format</returns>
        public IHttpActionResult GetSchedule(string empUnqId, ReportModes mode)
        {
            // LIST OF MODES:
            // 1: EXCEL DOWNLOAD FORMAT
            // 2: CURRENT OPENMONTH - ALL SCHEDULES (RELEASED/UNRELEASED ALL)
            // 3: CURRENT OPENMONTH - ONLY RELEASED ONE
            // 4: PREVIOUS MONTH RELEASED

            // Check if employee passed is a releaser...
            string relCode = _context.ReleaseAuth.FirstOrDefault(e => e.EmpUnqId == empUnqId)?.ReleaseCode;

            if (relCode == null) return BadRequest("Employee is not a releaser.");

            var relStrLvl = _context.ReleaseStrategyLevels
                .Where(r => relCode.Contains(r.ReleaseCode) && r.ReleaseGroupCode == ReleaseGroups.ShiftSchedule)
                .Select(r => r.ReleaseStrategy).ToArray();

            var vRelStr = _context.ReleaseStrategy
                .Where(r => relStrLvl.Contains(r.ReleaseStrategy) &&
                            r.Active &&
                            r.ReleaseGroupCode == ReleaseGroups.ShiftSchedule)
                .ToList();

            var allRelStr = vRelStr.Select(v => v.ReleaseStrategy).ToArray();

            var openMonth = _context.SsOpenMonth.FirstOrDefault()?.YearMonth;
            if (openMonth == null) return BadRequest("Open month not configured.");

            //carry on the good work...

            DateTime fromDt = DateTime.Parse("01/" + openMonth.ToString().Substring(4, 2) + "/" +
                                             openMonth.ToString().Substring(0, 4));
            DateTime toDt = fromDt.AddMonths(1).AddDays(-1);

            // If data required for previous month,
            if (mode == ReportModes.PreviousMonthAll || mode == ReportModes.PreviousMonthReleased)
            {
                // then change from date, to date and openMonth to
                fromDt = fromDt.AddMonths(-1);
                toDt = fromDt.AddMonths(1).AddDays(-1);
                openMonth = Convert.ToInt32(fromDt.Year.ToString("0000") + fromDt.Month.ToString("00"));
            }

            // Get all schedules from master whether released or not except rejected...

            var shedules = _context.ShiftSchedules
                .Where(s => s.YearMonth == openMonth &&
                            allRelStr.Contains(s.ReleaseStrategy) &&
                            s.ReleaseStatusCode != ReleaseStatus.ReleaseRejected
                ).ToList();


            //return bad request if we've asked for blank format and any employee is found already uploaded
            if (mode == ReportModes.ExcelDownload && shedules.Count > 0)
                return Content(HttpStatusCode.BadRequest, shedules);


            var outputTable = new DataTable("ShiftSchedule");
            outputTable.Columns.Add("EmpUnqId");
            //TODO: "COLUMN ORDER" remember to change below in for loop if adding/deleting any columns

            for (DateTime dt = fromDt; dt <= toDt;)
            {
                outputTable.Columns.Add("D" + dt.Day.ToString("00"));

                //TODO: "COLUMN ORDER" Remember to change here if adding/deleting columns from excel
                outputTable.Columns["D" + dt.Day.ToString("00")].SetOrdinal(dt.Day);
                dt = dt.AddDays(1);
            }


            // Loop for each employee under this releaser
            foreach (ReleaseStrategies relStr in vRelStr)
            {
                // loop for each day of month
                DataRow dr = outputTable.NewRow();
                dr["EmpUnqId"] = relStr.ReleaseStrategy;

                if (mode == ReportModes.ExcelDownload)
                {
                    outputTable.Rows.Add(dr);
                    continue;
                }
                
                // If there are multiple schedules uploaded for this employee, 
                // show only last one

                List<ShiftSchedules> schedule;
                
                if (mode == ReportModes.CurrentMonthReleased || mode == ReportModes.PreviousMonthReleased)
                {
                    schedule = _context.ShiftSchedules
                        .Where(s =>
                            s.ReleaseStrategy == relStr.ReleaseStrategy &&
                            s.YearMonth == openMonth &&
                            s.ReleaseStatusCode == ReleaseStatus.FullyReleased)
                        .OrderByDescending(s => s.ScheduleId)
                        .ToList();
                }
                else
                {
                    schedule = _context.ShiftSchedules
                        .Where(s =>
                            s.ReleaseStrategy == relStr.ReleaseStrategy &&
                            s.YearMonth == openMonth)
                        .OrderByDescending(s => s.ScheduleId)
                        .ToList();
                }

                if (!schedule.Any())
                    continue;
                
                ShiftSchedules sch = schedule.First();

                var schDtl = _context.ShiftScheduleDetails
                    .Where(s =>
                        s.YearMonth == sch.YearMonth &&
                        s.ScheduleId == sch.ScheduleId &&
                        s.EmpUnqId == sch.EmpUnqId).ToList();

                for (DateTime dt = fromDt; dt <= toDt;)
                {
                    dr["D" + dt.Day.ToString("00")] = schDtl.First(s => s.ShiftDay == dt.Day).ShiftCode ?? "";
                    dt = dt.AddDays(1);
                }


                outputTable.Rows.Add(dr);
            }

            return Ok(outputTable);
        }

        public IHttpActionResult GetSchedule(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var openMonth = _context.SsOpenMonth.FirstOrDefault()?.YearMonth;
                if (openMonth == null) return BadRequest("Open month not configured.");

                //carry on the good work...

                DateTime fromDt = DateTime.Parse("01/" + openMonth.ToString().Substring(4, 2) + "/" +
                                                 openMonth.ToString().Substring(0, 4));
                DateTime toDt = fromDt.AddMonths(1).AddDays(-1);

                var shedules = _context.ShiftSchedules
                    .Where(s => (s.ReleaseDt>= fromDate && s.AddDt <= toDate) &&
                                s.YearMonth == openMonth &&
                                s.ReleaseStatusCode == ReleaseStatus.FullyReleased
                    ).ToList();

                var outputTable = new DataTable("ShiftSchedule");
                outputTable.Columns.Add("EmpUnqId");

                for (DateTime dt = fromDt; dt <= toDt;)
                {
                    outputTable.Columns.Add("D" + dt.Day.ToString("00"));
                    dt = dt.AddDays(1);
                }


                // Loop for each employee under this releaser
                foreach (ShiftSchedules sch in shedules)
                {
                    // loop for each day of month
                    DataRow dr = outputTable.NewRow();
                    dr["EmpUnqId"] = sch.ReleaseStrategy;

                    var schDtl = _context.ShiftScheduleDetails
                        .Where(s =>
                            s.YearMonth == sch.YearMonth &&
                            s.ScheduleId == sch.ScheduleId &&
                            s.EmpUnqId == sch.EmpUnqId).ToList();

                    for (DateTime dt = fromDt; dt <= toDt;)
                    {
                        dr["D" + dt.Day.ToString("00")] = schDtl.First(s => s.ShiftDay == dt.Day).ShiftCode ?? "";
                        dt = dt.AddDays(1);
                    }


                    outputTable.Rows.Add(dr);
                }

                return Ok(outputTable);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.ToString());
            }
        }

        /// <summary>
        /// Shift schedule upload for employees under the releaser
        /// </summary>
        /// <param name="empUnqId">SAP code of releaser and csv file in filedata</param>
        /// <returns>Ok with uploaded schedule data</returns>
        [HttpPost]
        public IHttpActionResult UploadSchedule(string empUnqId)
        {
            HttpContext httpContext = HttpContext.Current;

            // Check for any uploaded file  
            if (httpContext.Request.Files.Count <= 0) return BadRequest("NO FILES???");

            // Create new folder if does not exist.

            try
            {
                string folder = HostingEnvironment.MapPath(@"~/App_Data/tmp/");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder ?? throw new InvalidOperationException("Folder not found"));

                //Loop through uploaded files 
                for (int i = 0; i < httpContext.Request.Files.Count; i++)
                {
                    HttpPostedFile postedFile = httpContext.Request.Files[i];

                    if (postedFile.ContentLength <= 0) return BadRequest("Passed file is null!");

                    try
                    {
                        string fileExt = Path.GetExtension(postedFile.FileName);
                        if (fileExt != ".csv") return BadRequest("Invalid file extension.");

                        SsOpenMonth ssOpenMonth = _context.SsOpenMonth.FirstOrDefault();
                        int openMonth = 0;

                        if (ssOpenMonth != null)
                            openMonth = ssOpenMonth.YearMonth;
                        else
                            return BadRequest("SS Open month not configured.");


                        using (var reader = new StreamReader(postedFile.InputStream))
                        {
                            // This will just rip off the first Header line off the excel file
                            var header = reader.ReadLine()?.Split(',');

                            var schedules = new List<ShiftScheduleDto>();

                            while (!reader.EndOfStream)
                            {
                                var row = reader.ReadLine()?.Split(',');
                                if (row == null) continue;

                                var sch = new ShiftScheduleDto
                                {
                                    YearMonth = openMonth,
                                    EmpUnqId = row[0],
                                    ReleaseGroupCode = ReleaseGroups.ShiftSchedule,
                                    ReleaseStrategy = row[0],
                                    ReleaseStatusCode = ReleaseStatus.InRelease,
                                    AddDt = DateTime.Now,
                                    AddUser = empUnqId,
                                    ShiftScheduleDetails = new List<ShiftScheduleDetailDto>()
                                };
                                
                                for (int rowIndex = 1; rowIndex < row.Length; rowIndex++)
                                {
                                    var schLine = new ShiftScheduleDetailDto
                                    {
                                        YearMonth = sch.YearMonth,
                                        ShiftDay = rowIndex,
                                        ShiftCode = row[rowIndex]
                                    };

                                    sch.ShiftScheduleDetails.Add(schLine);
                                }

                                schedules.Add(sch);
                            }

                            var errors = SchValidate(schedules, empUnqId);

                            // Check if any unreleased schedule exist for any of the employee...

                            var schEmps = schedules.Select(s => s.EmpUnqId).ToArray();
                            var existingSch = _context.ShiftSchedules
                                .Where(s => s.YearMonth == openMonth &&
                                            schEmps.Contains(s.EmpUnqId) &&
                                            s.ReleaseStatusCode != ReleaseStatus.FullyReleased)
                                .Select(e => e.EmpUnqId)
                                .ToArray();

                            if (existingSch.Length > 0)
                                errors.Add("A shift schedule already uploaded for " + existingSch.Length +
                                           " employees, still pending for release.");

                            if (errors.Count > 0)
                                return Content(HttpStatusCode.BadRequest, errors);


                            // OUR SHIFT SCHEDULE IS OK TO UPLOAD...

                            int maxId = _context.ShiftSchedules
                                            .Select(s => (int?) s.ScheduleId)
                                            .Max() ?? 0;

                            maxId++;

                            using (DbContextTransaction transaction = _context.Database.BeginTransaction())
                            {
                                foreach (ShiftScheduleDto sch in schedules)
                                {
                                    Employees emp = _context.Employees.FirstOrDefault(e => e.EmpUnqId == sch.EmpUnqId);

                                    if (emp == null) return BadRequest("Employee is null!!!");

                                    sch.ScheduleId = maxId;
                                    sch.CompCode = emp.CompCode;
                                    sch.WrkGrp = emp.WrkGrp;
                                    sch.UnitCode = emp.UnitCode;
                                    sch.DeptCode = emp.DeptCode;
                                    sch.StatCode = emp.StatCode;

                                    foreach (ShiftScheduleDetailDto detail in sch.ShiftScheduleDetails)
                                        detail.ScheduleId = maxId;

                                    // get the release strategy
                                    ReleaseStrategies relStrat = _context.ReleaseStrategy
                                        .FirstOrDefault(
                                            r => r.ReleaseStrategy == sch.EmpUnqId &&
                                                 r.ReleaseGroupCode == ReleaseGroups.ShiftSchedule &&
                                                 r.Active
                                        );

                                    if (relStrat == null)
                                    {
                                        errors.Add("Release strategy not found for emp: " + sch.EmpUnqId);
                                        continue;
                                    }

                                    relStrat.ReleaseStrategyLevels = new ListStack<ReleaseStrategyLevels>();
                                    var relStratLvl = _context.ReleaseStrategyLevels
                                        .Where(r => r.ReleaseGroupCode == ReleaseGroups.ShiftSchedule &&
                                                    r.ReleaseStrategy == relStrat.ReleaseStrategy)
                                        .ToList();

                                    relStrat.ReleaseStrategyLevels.AddRange(relStratLvl);

                                    var apps = new List<ApplReleaseStatusDto>();

                                    foreach (ReleaseStrategyLevels relStratReleaseStrategyLevel in relStrat
                                        .ReleaseStrategyLevels)
                                    {
                                        ApplReleaseStatus appRelStat = new ApplReleaseStatus
                                        {
                                            YearMonth = sch.YearMonth,
                                            ReleaseGroupCode = sch.ReleaseGroupCode,
                                            ApplicationId = sch.ScheduleId,
                                            ReleaseStrategy = relStratReleaseStrategyLevel.ReleaseStrategy,
                                            ReleaseStrategyLevel = relStratReleaseStrategyLevel.ReleaseStrategyLevel,
                                            ReleaseCode = relStratReleaseStrategyLevel.ReleaseCode,
                                            ReleaseStatusCode =
                                                relStratReleaseStrategyLevel.ReleaseStrategyLevel == 1
                                                    ? ReleaseStatus.InRelease
                                                    : ReleaseStatus.NotReleased,
                                            ReleaseDate = null,
                                            ReleaseAuth = empUnqId,
                                            IsFinalRelease = relStratReleaseStrategyLevel.IsFinalRelease
                                        };

                                        //add to collection
                                        apps.Add(Mapper.Map<ApplReleaseStatus, ApplReleaseStatusDto>(appRelStat));

                                        _context.ApplReleaseStatus.Add(appRelStat);
                                    }

                                    _context.ShiftSchedules.Add(Mapper.Map<ShiftScheduleDto, ShiftSchedules>(sch));

                                    _context.SaveChanges();

                                    //transaction.Commit();
                                    sch.ApplReleaseStatus = new List<ApplReleaseStatusDto>();
                                    sch.ApplReleaseStatus.AddRange(apps);
                                }


                                if (errors.Count > 0)
                                {
                                    transaction.Rollback();
                                    return Content(HttpStatusCode.BadRequest, errors);
                                }

                                transaction.Commit();
                            }

                            return Ok(schedules);
                        }
                    }
                    catch (Exception ex)
                    {
                        return BadRequest("Error:" + ex);
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.ToString());
            }

            return Ok();
        }

        /// <summary>
        /// Validate given shift schedule
        /// </summary>
        /// <param name="schedule">Shift schedule object</param>
        /// <param name="empUnqId">Employee id of releaser</param>
        /// <returns></returns>
        private List<string> SchValidate(List<ShiftScheduleDto> schedule, string empUnqId)
        {
            //get all shifts in a list
            var shifts = _context.Shifts.Select(s => s.ShiftCode).ToList();

            //REMEMBER TO ADD WO manually AFTER SHIFT SYNC SO THAT IT APPEARS ABOVE...

            var errors = new List<string>();

            // check for duplicate rows..
            {
                var duplicateEntries = schedule.GroupBy(e => e.EmpUnqId).Where(g => g.Count() > 1).ToList();
                if (duplicateEntries.Count > 0)
                    errors.Add("Duplicated records found in file.");
            }

            foreach (ShiftScheduleDto dto in schedule)
            {
                // Check if employee exist
                Employees emp = _context.Employees.FirstOrDefault(e => e.EmpUnqId == dto.EmpUnqId);

                if (emp == null)
                {
                    errors.Add("Emp: " + dto.EmpUnqId + " does not exist.");
                }
                else
                {
                    // see if employee is under his release...
                    if (!CheckReleaser(dto.EmpUnqId, empUnqId))
                        errors.Add("Emp:" + dto.EmpUnqId + ": Not under release of " + empUnqId);
                }

                // Now check that each Shift of this is not blank and is valid

                //    foreach (ShiftScheduleDetails detail in dto.ShiftScheduleDetails)
                //    if (!shifts.Contains(detail.ShiftCode))
                //        errors.Add("Emp:" + dto.EmpUnqId + ": Day " + detail.ShiftDay + " shift codes is invalid (" +
                //                   detail.ShiftCode + ")");
                //   ABOVE CODE CONVERTED TO LINQ BELOW :-
                //
                //    errors.AddRange(
                //    from detail in dto.ShiftScheduleDetails
                //    where !shifts.Contains(detail.ShiftCode)
                //    select "Emp:" + dto.EmpUnqId + ": Day " + detail.ShiftDay + " shift codes is invalid (" +
                //           detail.ShiftCode + ")");
                //
                //  WHICH IS THEN CONVERTED TO BELOW LAMBDA EXPRESSION:

                errors.AddRange(
                    dto.ShiftScheduleDetails.Where(d => !shifts.Contains(d.ShiftCode))
                        .Select(detail => "Emp:" + dto.EmpUnqId + ": Day " + detail.ShiftDay +
                                          " shift codes is invalid (" + detail.ShiftCode + ")")
                );

                // Count number of week offs -- should not be more than 5

                int countWo = dto.ShiftScheduleDetails.Count(e => e.ShiftCode == "WO");
                if (countWo > 5)
                    errors.Add("Emp:" + dto.EmpUnqId + ": Week off more than 5");
            }

            return errors;
        }

        /// <summary>
        /// Validates whether the employee is under release of releaser
        /// </summary>
        /// <param name="empUnqId">Employee to check</param>
        /// <param name="releaser">SAP id of releaser</param>
        /// <returns></returns>
        private bool CheckReleaser(string empUnqId, string releaser)
        {
            //Get release strategy of the employee

            ReleaseStrategies releaseStr = _context.ReleaseStrategy.FirstOrDefault(e =>
                e.ReleaseStrategy == empUnqId && e.Active && e.ReleaseGroupCode == ReleaseGroups.ShiftSchedule);

            if (releaseStr == null)
                return false;

            //get release codes of release strategy levels of above 
            var relCodes = _context.ReleaseStrategyLevels.Where(
                    r => r.ReleaseGroupCode == ReleaseGroups.ShiftSchedule &&
                         r.ReleaseStrategy == releaseStr.ReleaseStrategy)
                .Select(c => c.ReleaseCode)
                .ToArray();

            // Get release auth and compare it to the releaser...
            ReleaseAuth relCode =
                _context.ReleaseAuth.FirstOrDefault(e => e.EmpUnqId == releaser && relCodes.Contains(e.ReleaseCode));

            // if release code is null, return false, else return tru
            return relCode != null;
        }
    }
}