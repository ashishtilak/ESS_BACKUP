    using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;
using Antlr.Runtime.Misc;
using AutoMapper;
using ESS.Dto;
using ESS.Models;

namespace ESS.Controllers.Api
{
    public class ShiftScheduleUpdateController : ApiController
    {
        private readonly ApplicationDbContext _context;

        public ShiftScheduleUpdateController()
        {
            _context = new ApplicationDbContext();
        }

        public IHttpActionResult GetSchedule(string empUnqId)
        {
            //algorythm is simple:
            //For releaser passed, find the employees for which ss is already uploaded & released
            //From today's date to this month's end date, create a template with existing uploaded ss

            try
            {
                var relCode = _context.ReleaseAuth
                    .Where(e => e.EmpUnqId == empUnqId)
                    .Select(e => e.ReleaseCode)
                    .ToArray();

                if (relCode.Length == 0) return BadRequest("Employee is not a releaser.");

                var relStrLvl = _context.ReleaseStrategyLevels
                    .Where(r => relCode.Contains(r.ReleaseCode) && r.ReleaseGroupCode == ReleaseGroups.ShiftSchedule)
                    .Select(r => r.ReleaseStrategy).ToArray();

                var vRelStr = _context.ReleaseStrategy
                    .Where(r => relStrLvl.Contains(r.ReleaseStrategy) &&
                                r.Active &&
                                r.ReleaseGroupCode == ReleaseGroups.ShiftSchedule)
                    .ToList();

                var allRelStr = vRelStr.Select(v => v.ReleaseStrategy).ToArray();

                //don't get openmonth from db, as after 25th, the same will be changed to next month
                //for uploading of next month's ss
                int openMonth = Convert.ToInt32(DateTime.Now.Year + DateTime.Now.Month.ToString("00"));


                DateTime fromDt = DateTime.Now.Date;
                DateTime toDt = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1).AddDays(-1);


                // Get all schedules from master whether released or not except rejected...
                var schedules = _context.ShiftSchedules
                    .Where(s => s.YearMonth == openMonth &&
                                allRelStr.Contains(s.ReleaseStrategy) &&
                                s.ReleaseStatusCode != ReleaseStatus.ReleaseRejected
                    ).ToList();

                if (schedules.Count == 0)
                    return BadRequest("No shift schedule found uploaded for this employee.");


                var outputTable = new DataTable("ShiftSchedule");
                outputTable.Columns.Add("EmpUnqId");

                // not require these columns:

                // outputTable.Columns.Add("EmpName");
                // outputTable.Columns.Add("DeptName");
                // outputTable.Columns.Add("StatName");
                // outputTable.Columns.Add("Designation");
                // outputTable.Columns.Add("CatName");

                DateTime loopDate = fromDt;
                for (int dt = fromDt.Day; dt <= toDt.Day; dt++)
                {
                    string dayStr = dt.ToString("00") + "_" + loopDate.DayOfWeek.ToString().Substring(0, 2);
                    outputTable.Columns.Add(dayStr);
                    loopDate = loopDate.AddDays(1);
                }

                // Loop for each employee under this releaser
                foreach (ReleaseStrategies relStr in vRelStr)
                {
                    // loop for each day of month
                    DataRow dr = outputTable.NewRow();
                    dr["EmpUnqId"] = relStr.ReleaseStrategy;

                    EmployeeDto employeeDto = _context.Employees
                        .Select(e => new EmployeeDto
                        {
                            EmpUnqId = e.EmpUnqId,
                            EmpName = e.EmpName,
                            FatherName = e.FatherName,
                            Active = e.Active,
                            Pass = e.Pass,

                            CompCode = e.CatCode,
                            WrkGrp = e.WrkGrp,
                            UnitCode = e.UnitCode,
                            DeptCode = e.DeptCode,
                            StatCode = e.StatCode,
                            CatCode = e.CatCode,
                            EmpTypeCode = e.EmpTypeCode,
                            GradeCode = e.GradeCode,
                            DesgCode = e.DesgCode,
                            IsHod = e.IsHod,

                            CompName = e.Company.CompName,
                            WrkGrpDesc = e.WorkGroup.WrkGrpDesc,
                            UnitName = e.Units.UnitName,
                            DeptName = e.Departments.DeptName,
                            StatName = e.Stations.StatName,
                            CatName = e.Categories.CatName,
                            EmpTypeName = e.EmpTypes.EmpTypeName,
                            GradeName = e.Grades.GradeName,
                            DesgName = e.Designations.DesgName,

                            Location = e.Location
                        })
                        .Single(e => e.EmpUnqId == relStr.ReleaseStrategy);

                    // don't need these columns:

                    // dr["EmpName"] = employeeDto.EmpName;
                    // dr["DeptName"] = employeeDto.DeptName;
                    // dr["StatName"] = employeeDto.StatName;
                    // dr["Designation"] = employeeDto.DesgName;
                    // dr["CatName"] = employeeDto.CatName;

                    List<ShiftSchedules> tmpSchedule;

                    tmpSchedule = schedules
                        .Where(s =>
                            s.ReleaseStrategy == relStr.ReleaseStrategy &&
                            s.YearMonth == openMonth &&
                            s.ReleaseStatusCode == ReleaseStatus.FullyReleased)
                        .OrderByDescending(s => s.ScheduleId)
                        .ToList();

                    if (!tmpSchedule.Any())
                        continue;


                    ShiftSchedules sch = tmpSchedule.First();

                    var schDtl = _context.ShiftScheduleDetails
                        .Where(s =>
                            s.YearMonth == sch.YearMonth &&
                            s.ScheduleId == sch.ScheduleId &&
                            s.EmpUnqId == sch.EmpUnqId).ToList();

                    for (DateTime dt = fromDt; dt <= toDt;)
                    {
                        string dayStr = dt.Day.ToString("00") + "_" + dt.DayOfWeek.ToString().Substring(0, 2);
                        dr[dayStr] = schDtl.First(s => s.ShiftDay == dt.Day).ShiftCode ?? "";
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

        [HttpPost]
        public IHttpActionResult UpldateSchedule(string empUnqId)
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
            }
            catch (Exception ex)
            {
                return BadRequest(ex.ToString());
            }

            DateTime fromDt = DateTime.Now.Date;
            DateTime todt = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1).AddDays(-1);

            for (int i = 0; i < httpContext.Request.Files.Count;)
            {
                HttpPostedFile postedFile = httpContext.Request.Files[i];

                if (postedFile.ContentLength <= 0) return BadRequest("Passed file is null!");

                try
                {
                    string fileExt = Path.GetExtension(postedFile.FileName);
                    if (fileExt != ".csv") return BadRequest("Invalid file extension.");

                    //don't get openmonth from db, as after 25th, the same will be changed to next month
                    //for uploading of next month's ss
                    int openMonth = Convert.ToInt32(DateTime.Now.Year + DateTime.Now.Month.ToString("00"));

                    using (var reader = new StreamReader(postedFile.InputStream))
                    {
                        // This will just rip off the first Header line off the excel file
                        var header = reader.ReadLine()?.Split(',');
                        // Add check for column header here...
                        //first column is empunqid. Second column is date
                        if (header != null && header[1].Substring(0, 2) != fromDt.Day.ToString("00"))
                            return BadRequest("check date column. Download template and upload it.");

                        if (header != null && (header.Length - 1) != ((todt - fromDt).Days + 1))
                        {
                            return BadRequest("File format invalid.");
                        }

                        var schedules = new List<ShiftScheduleDto>();

                        while (!reader.EndOfStream)
                        {
                            var row = reader.ReadLine()?.Split(',');
                            if (row == null) continue;

                            var tmpEmpId = row[0].ToString();

                            var sch = new ShiftScheduleDto
                            {
                                YearMonth = openMonth,
                                EmpUnqId = tmpEmpId,
                                ReleaseGroupCode = ReleaseGroups.ShiftSchedule,
                                ReleaseStrategy = tmpEmpId,
                                ReleaseStatusCode = ReleaseStatus.PartiallyReleased,
                                AddDt = DateTime.Now,
                                AddUser = empUnqId,
                                ShiftScheduleDetails = new List<ShiftScheduleDetailDto>()
                            };

                            //first get the schedule for upto FromDate 
                            //and add it to details

                            var extScheduleId = _context.ShiftSchedules
                                .Where(s => s.EmpUnqId == tmpEmpId &&
                                            s.ReleaseStatusCode == ReleaseStatus.FullyReleased)
                                .Max(s => s.ScheduleId);

                            var existingSch = _context.ShiftScheduleDetails
                                .Where(s => s.YearMonth == openMonth && 
                                            s.ScheduleId == extScheduleId &&
                                            s.EmpUnqId == tmpEmpId
                                            )
                                .ToList();

                            for (int rowIndex = 1; rowIndex < fromDt.Day; rowIndex++)
                            {
                                ShiftScheduleDetails existingLine =
                                    existingSch.FirstOrDefault(s => s.ShiftDay == rowIndex);

                                if (existingLine == null) continue;

                                var schLine = new ShiftScheduleDetailDto
                                {
                                    YearMonth = existingLine.YearMonth,
                                    ShiftDay = existingLine.ShiftDay,
                                    ShiftCode = existingLine.ShiftCode
                                };
                                sch.ShiftScheduleDetails.Add(schLine);
                            }


                            for (int rowIndex = fromDt.Day; rowIndex <= todt.Day; rowIndex++)
                            {
                                var schLine = new ShiftScheduleDetailDto
                                {
                                    YearMonth = sch.YearMonth,
                                    ShiftDay = rowIndex,
                                    ShiftCode = row[rowIndex - fromDt.Day + 1]
                                };

                                sch.ShiftScheduleDetails.Add(schLine);
                            }

                            schedules.Add(sch);
                        }

                        var errors = ShiftScheduleController.SchValidate(schedules, empUnqId);

                        // Check if any unreleased schedule exist for any of the employee...

                        var schEmps = schedules.Select(s => s.EmpUnqId).ToArray();
                        var unrelSch = _context.ShiftSchedules
                            .Where(s => s.YearMonth == openMonth &&
                                        schEmps.Contains(s.EmpUnqId) &&
                                        (s.ReleaseStatusCode != ReleaseStatus.FullyReleased &&
                                         s.ReleaseStatusCode != ReleaseStatus.ReleaseRejected))
                            .Select(e => e.EmpUnqId)
                            .ToArray();

                        if (unrelSch.Length > 0)
                            errors.Add("A shift schedule already uploaded for " + unrelSch.Length +
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
                                    var appRelStat = new ApplReleaseStatus
                                    {
                                        YearMonth = sch.YearMonth,
                                        ReleaseGroupCode = sch.ReleaseGroupCode,
                                        ApplicationId = sch.ScheduleId,
                                        ReleaseStrategy = relStratReleaseStrategyLevel.ReleaseStrategy,
                                        ReleaseStrategyLevel = relStratReleaseStrategyLevel.ReleaseStrategyLevel,
                                        ReleaseCode = relStratReleaseStrategyLevel.ReleaseCode,
                                        ReleaseStatusCode = ReleaseStatus.NotReleased,
                                        ReleaseDate = null,
                                        ReleaseAuth = empUnqId,
                                        IsFinalRelease = relStratReleaseStrategyLevel.IsFinalRelease
                                    };


                                    if (relStratReleaseStrategyLevel.ReleaseStrategyLevel <= 1)
                                    {
                                        appRelStat.ReleaseStatusCode = ReleaseStatus.FullyReleased;
                                        appRelStat.ReleaseDate = DateTime.Now;
                                    }
                                    else if (relStratReleaseStrategyLevel.ReleaseStrategyLevel == 2)
                                    {
                                        appRelStat.ReleaseStatusCode = ReleaseStatus.InRelease;
                                        appRelStat.ReleaseDate = null;
                                    }
                                    else
                                    {
                                        appRelStat.ReleaseStatusCode = ReleaseStatus.NotReleased;
                                        appRelStat.ReleaseDate = null;
                                    }

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
                    return BadRequest(ex.ToString());
                }
            }

            return Ok();
        }
    }
}