using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using ESS.Dto;
using ESS.Models;

namespace ESS.Helpers
{
    public class CustomHelper
    {
        //private static readonly string RemoteServer = System.Configuration.ConfigurationManager.ConnectionStrings["RemoteConnection"].ConnectionString;
        private static readonly string ThisServer = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;


        public static string GetAttendanceServerApi(string location)
        {
            //return System.Configuration.ConfigurationManager.ConnectionStrings["AttendanceServerApi"].ConnectionString;
            ApplicationDbContext context = new ApplicationDbContext();
            var loc = context.Location.FirstOrDefault(c => c.Location == location);
            return loc != null ? loc.AttendanceServerApi : "";
        }

        public static string GetRemoteServer(string location)
        {
            ApplicationDbContext context = new ApplicationDbContext();
            var loc = context.Location.FirstOrDefault(c => c.Location == location);
            return loc != null ? loc.RemoteConnection : "";
        }

        /// <summary>
        /// Get list of Holidays
        /// </summary>
        /// <param name="fromDt"> From Date </param>
        /// <param name="toDt"> To Date</param>
        /// <param name="compCode"> Company Code</param>
        /// <param name="wrkGrp"> Work Group </param>
        /// <returns></returns>
        public static List<DateTime> GetHolidays(DateTime fromDt, DateTime toDt, string compCode, string wrkGrp, string location)
        {
            List<DateTime> holidays = new List<DateTime>();

            string strRemoteServer = GetRemoteServer(location);

            using (SqlConnection cn = new SqlConnection(strRemoteServer))
            {
                cn.Open();
                string sql = "select " +
                             "tDate from HolidayMast where " +
                             "CompCode = '" + compCode + "' " +
                             "and WrkGrp = '" + wrkGrp + "' " +
                             "and tDate between '" + fromDt.ToString("yyyy-MM-dd") + "' " +
                             "and '" + toDt.ToString("yyyy-MM-dd") + "' ";
                SqlCommand cmd = new SqlCommand(sql, cn);
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                    holidays.Add(DateTime.Parse(dr["tDate"].ToString()));

            }
            if (DateTime.Parse("2017-12-07") >= fromDt && DateTime.Parse("2017-12-07") <= toDt)
                holidays.Add(DateTime.Parse("2017-12-07"));

            return holidays;
        }


        public static List<HolidayDto> GetHolidays(string compCode, string wrkGrp, int tYear, string location)
        {
            List<HolidayDto> holidays = new List<HolidayDto>();

            string strRemoteServer = GetRemoteServer(location);

            using (SqlConnection cn = new SqlConnection(strRemoteServer))
            {
                cn.Open();
                string sql = "select tDate, HlDesc from HolidayMast where " +
                             "CompCode = '" + compCode + "' and " +
                             "WrkGrp = '" + wrkGrp + "' and " +
                             "tYear = " + tYear + "";

                SqlCommand cmd = new SqlCommand(sql, cn);
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                    holidays.Add(new HolidayDto
                    {
                        HolidayDate = DateTime.Parse(dr["tDate"].ToString()),
                        HolidayName = dr["HlDesc"].ToString()
                    });
            }
            return holidays;
        }


        public static bool GetOptionalHolidays(DateTime leaveDate, string location)
        {
            string strRemoteServer = GetRemoteServer(location);
            using (SqlConnection cn = new SqlConnection(strRemoteServer))
            {
                cn.Open();
                string sql = "select * from HolidayOptMast " +
                             "where tDate = '" + leaveDate.ToString("yyyy-MM-dd") + "'";
                SqlCommand cmd = new SqlCommand(sql, cn);
                SqlDataReader dr = cmd.ExecuteReader();

                return dr.HasRows;
            }
        }


        //Get weekly off day
        public static List<DateTime> GetWeeklyOff(DateTime fromDt, DateTime toDt, string empUnqId)
        {
            List<DateTime> weeklyOff = new List<DateTime>();

            ApplicationDbContext context = new ApplicationDbContext();
            var emp = context.Employees.Single(e => e.EmpUnqId == empUnqId);

            string strRemoteServer = GetRemoteServer(emp.Location);

            using (SqlConnection cn = new SqlConnection(strRemoteServer))
            {
                cn.Open();
                string sql = "select " +
                             "tDate " +
                             "from AttdData where EmpUnqId ='" + empUnqId + "' " +
                             "and tDate between '" + fromDt.ToString("yyyy-MM-dd") + "' " +
                             "and '" + toDt.ToString("yyyy-MM-dd") + "' " +
                             "and ScheduleShift = 'WO' ";
                SqlCommand cmd = new SqlCommand(sql, cn);
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                    weeklyOff.Add(DateTime.Parse(dr["tDate"].ToString()));

            }

            return weeklyOff;
        }

        //Get Leave Balance
        public static List<LeaveBalanceDto> GetLeaveBalance(string empUnqId, int year)
        {

            List<LeaveBalanceDto> lDtoList = new List<LeaveBalanceDto>();

            ApplicationDbContext context = new ApplicationDbContext();
            var emp = context.Employees.Single(e => e.EmpUnqId == empUnqId);

            string strRemoteServer = GetRemoteServer(emp.Location);

            using (SqlConnection cn = new SqlConnection(strRemoteServer))
            {
                cn.Open();
                string sql = "select " +
                             "EmpUnqId, " +
                             "LeaveTyp as LeaveTypeCode, " +
                             "OPN as Opening, " +
                             "AVL as Availed, " +
                             "BAL as Balance," +
                             "ENC as Encashed" +
                             " from LeaveBal where tyear=" + year + " and empUnqId = '" + empUnqId + "'";

                SqlCommand cmd = new SqlCommand(sql, cn);
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    LeaveBalanceDto lBal = new LeaveBalanceDto
                    {
                        EmpUnqId = dr["EmpUnqId"].ToString(),
                        LeaveTypeCode = dr["LeaveTypeCode"].ToString()
                    };

                    float value;
                    float.TryParse(dr["Opening"].ToString(), out value);
                    lBal.Opening = value;

                    float.TryParse(dr["Availed"].ToString(), out value);
                    lBal.Availed = value;

                    float.TryParse(dr["Balance"].ToString(), out value);
                    lBal.Balance = value;

                    float.TryParse(dr["Encashed"].ToString(), out value);
                    lBal.Encashed = value;

                    lDtoList.Add(lBal);

                }


            }



            return lDtoList;
        }

        //Sync Data from Remote server to this server
        public static void SyncData(string location)
        {
            List<DateTime> holidays = new List<DateTime>();

            string strRemoteServer = GetRemoteServer(location);

            SyncCompany(strRemoteServer, location);
            SyncWrkGrp(strRemoteServer, location);
            SyncUnit(strRemoteServer, location);
            SyncDept(strRemoteServer, location);
            SyncStat(strRemoteServer, location);
            //SyncSec();
            SyncCatg(strRemoteServer, location);
            SyncDesg(strRemoteServer, location);
            SyncGrade(strRemoteServer, location);
            SyncEmpType(strRemoteServer, location);
            SyncEmp(strRemoteServer, location);
        }

        public static void SyncCompany(string strRemoteServer, string location)
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(strRemoteServer))
                {
                    cnRemote.Open();
                    //first get all masters:

                    SqlConnection cnLocal;

                    //create a temp table
                    using (cnLocal = new SqlConnection(ThisServer))
                    {
                        string sql = "select top 0 * into #tmpCompanies from Companies";
                        cnLocal.Open();
                        SqlCommand cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        //get data from attendance server
                        sql = "select CompCode, CompName, " +
                              "'" + location + "' as location " +
                              " from MastComp";

                        SqlDataAdapter da = new SqlDataAdapter(sql, cnRemote);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        using (SqlBulkCopy bulk = new SqlBulkCopy(cnLocal))
                        {
                            bulk.DestinationTableName = "#tmpCompanies";

                            bulk.ColumnMappings.Add("CompCode", "CompCode");
                            bulk.ColumnMappings.Add("CompName", "CompName");
                            bulk.ColumnMappings.Add("Location", "Location");

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Companies as target " +
                              "using #tmpCompanies as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.Location = Source.Location " +
                              "when matched then " +
                              "update set Target.CompName = Source.CompName " +
                              "when not matched then " +
                              "insert (compcode, compname, location) values (source.compcode, source.compname," +
                              "'" + location + "' ); ";

                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        sql = "drop table #tmpCompanies";
                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            #endregion
        }

        public static void SyncWrkGrp(string strRemoteServer, string location)
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(strRemoteServer))
                {
                    cnRemote.Open();
                    //first get all masters:

                    SqlConnection cnLocal;

                    //create a temp table
                    using (cnLocal = new SqlConnection(ThisServer))
                    {
                        string sql = "select top 0 * into #tmpWrkGrp from WorkGroups";
                        cnLocal.Open();
                        SqlCommand cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        //get data from attendance server
                        sql = "select CompCode, WrkGrp, WrkGrpDesc, " +
                              "'" + location + "' as location " +
                              " from MastWorkGrp";
                        SqlDataAdapter da = new SqlDataAdapter(sql, cnRemote);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        using (SqlBulkCopy bulk = new SqlBulkCopy(cnLocal))
                        {
                            bulk.DestinationTableName = "#tmpWrkGrp";

                            bulk.ColumnMappings.Add("CompCode", "CompCode");
                            bulk.ColumnMappings.Add("WrkGrp", "WrkGrp");
                            bulk.ColumnMappings.Add("WrkGrpDesc", "WrkGrpDesc");
                            bulk.ColumnMappings.Add("Location", "Location");

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into WorkGroups as target " +
                              "using #tmpWrkGrp as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.Location = Source.Location " +
                              "when matched then " +
                              "update set Target.WrkGrpDesc = Source.WrkGrpDesc " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, WrkGrpDesc, location) " +
                              "values (source.compcode, source.wrkgrp, source.wrkgrpdesc," +
                              "'" + location + "' ); ";

                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        sql = "drop table #tmpWrkGrp";
                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            #endregion
        }

        public static void SyncUnit(string strRemoteServer, string location)
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(strRemoteServer))
                {
                    cnRemote.Open();
                    //first get all masters:

                    SqlConnection cnLocal;

                    //create a temp table
                    using (cnLocal = new SqlConnection(ThisServer))
                    {
                        string sql = "select top 0 * into #tmpUnits from Units";
                        cnLocal.Open();
                        SqlCommand cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        //get data from attendance server
                        sql = "select CompCode, WrkGrp, UnitCode, UnitName, " +
                              "'" + location + "' as location " +
                              " from MastUnit";
                        SqlDataAdapter da = new SqlDataAdapter(sql, cnRemote);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        using (SqlBulkCopy bulk = new SqlBulkCopy(cnLocal))
                        {
                            bulk.DestinationTableName = "#tmpUnits";

                            bulk.ColumnMappings.Add("CompCode", "CompCode");
                            bulk.ColumnMappings.Add("WrkGrp", "WrkGrp");
                            bulk.ColumnMappings.Add("UnitCode", "UnitCode");
                            bulk.ColumnMappings.Add("UnitName", "UnitName");
                            bulk.ColumnMappings.Add("Location", "Location");

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Units as target " +
                              "using #tmpUnits as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.UnitCode = Source.UnitCode and " +
                              "Target.Location = Source.Location " +
                              "when matched then " +
                              "update set Target.UnitName = Source.UnitName " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, unitcode, unitname, location) " +
                              "values (source.compcode, source.wrkgrp, source.unitcode, source.unitname, " +
                              "'" + location + "' ); ";

                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        sql = "drop table #tmpUnits";
                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            #endregion
        }

        public static void SyncDept(string strRemoteServer, string location)
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(strRemoteServer))
                {
                    cnRemote.Open();
                    //first get all masters:

                    SqlConnection cnLocal;

                    //create a temp table
                    using (cnLocal = new SqlConnection(ThisServer))
                    {
                        string sql = "select top 0 * into #tmpDepts from Departments";
                        cnLocal.Open();
                        SqlCommand cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        //get data from attendance server
                        sql = "select CompCode, WrkGrp, UnitCode, DeptCode, DeptDesc, " +
                              "'" + location + "' as location " +
                              " from MastDept";
                        SqlDataAdapter da = new SqlDataAdapter(sql, cnRemote);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        using (SqlBulkCopy bulk = new SqlBulkCopy(cnLocal))
                        {
                            bulk.DestinationTableName = "#tmpDepts";

                            bulk.ColumnMappings.Add("CompCode", "CompCode");
                            bulk.ColumnMappings.Add("WrkGrp", "WrkGrp");
                            bulk.ColumnMappings.Add("UnitCode", "UnitCode");
                            bulk.ColumnMappings.Add("DeptCode", "DeptCode");
                            bulk.ColumnMappings.Add("DeptDesc", "DeptName");
                            bulk.ColumnMappings.Add("Location", "Location");

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Departments as target " +
                              "using #tmpDepts as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.UnitCode = Source.UnitCode and " +
                              "Target.DeptCode = Source.DeptCode and " +
                              "Target.Location = Source.Location " +
                              "when matched then " +
                              "update set Target.DeptName = Source.Deptname " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, unitcode, deptcode, deptname, location ) " +
                              "values (source.compcode, source.wrkgrp, source.unitcode, source.deptcode, source.deptname, " +
                              "'" + location + "' ); ";

                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        sql = "drop table #tmpDepts";
                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            #endregion
        }

        public static void SyncStat(string strRemoteServer, string location)
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(strRemoteServer))
                {
                    cnRemote.Open();
                    //first get all masters:

                    SqlConnection cnLocal;

                    //create a temp table
                    using (cnLocal = new SqlConnection(ThisServer))
                    {
                        string sql = "select top 0 * into #tmpStat from Stations";
                        cnLocal.Open();
                        SqlCommand cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        //get data from attendance server
                        sql = "select CompCode, WrkGrp, UnitCode, DeptCode, StatCode, StatDesc, " +
                              "'" + location + "' as location " +
                              " from MastStat";
                        SqlDataAdapter da = new SqlDataAdapter(sql, cnRemote);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        using (SqlBulkCopy bulk = new SqlBulkCopy(cnLocal))
                        {
                            bulk.DestinationTableName = "#tmpStat";

                            bulk.ColumnMappings.Add("CompCode", "CompCode");
                            bulk.ColumnMappings.Add("WrkGrp", "WrkGrp");
                            bulk.ColumnMappings.Add("UnitCode", "UnitCode");
                            bulk.ColumnMappings.Add("DeptCode", "DeptCode");
                            bulk.ColumnMappings.Add("StatCode", "StatCode");
                            bulk.ColumnMappings.Add("StatDesc", "StatName");
                            bulk.ColumnMappings.Add("Location", "Location");

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Stations as target " +
                              "using #tmpStat as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.UnitCode = Source.UnitCode and " +
                              "Target.DeptCode = Source.DeptCode and " +
                              "Target.StatCode = Source.StatCode and " +
                              "Target.Location = Source.Location " +
                              "when matched then " +
                              "update set Target.StatName = Source.StatName " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, unitcode, deptcode, statcode, statname, location ) " +
                              "values (source.compcode, source.wrkgrp, source.unitcode, source.deptcode, " +
                              "        source.statcode, source.statname, '" + location + "'  ); ";

                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        sql = "drop table #tmpStat";
                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            #endregion
        }

        public static void SyncSec(string strRemoteServer, string location)
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(strRemoteServer))
                {
                    cnRemote.Open();
                    //first get all masters:

                    SqlConnection cnLocal;

                    //create a temp table
                    using (cnLocal = new SqlConnection(ThisServer))
                    {
                        string sql = "select top 0 * into #tmpSec from Sections";
                        cnLocal.Open();
                        SqlCommand cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        //get data from attendance server
                        sql = "select CompCode, WrkGrp, UnitCode, DeptCode, StatCode, SecCode, SecDesc from MastStatSec";
                        SqlDataAdapter da = new SqlDataAdapter(sql, cnRemote);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        using (SqlBulkCopy bulk = new SqlBulkCopy(cnLocal))
                        {
                            bulk.DestinationTableName = "#tmpSec";

                            bulk.ColumnMappings.Add("CompCode", "CompCode");
                            bulk.ColumnMappings.Add("WrkGrp", "WrkGrp");
                            bulk.ColumnMappings.Add("UnitCode", "UnitCode");
                            bulk.ColumnMappings.Add("DeptCode", "DeptCode");
                            bulk.ColumnMappings.Add("StatCode", "StatCode");
                            bulk.ColumnMappings.Add("SecCode", "SecCode");
                            bulk.ColumnMappings.Add("SecDesc", "SecName");

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Sections as target " +
                              "using #tmpSec as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.UnitCode = Source.UnitCode and " +
                              "Target.DeptCode = Source.DeptCode and " +
                              "Target.StatCode = Source.StatCode and " +
                              "Target.SecCode = Source.SecCode " +
                              "when matched then " +
                              "update set Target.SecName = Source.SecName " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, unitcode, deptcode, statcode, seccode, secname ) " +
                              "values (source.compcode, source.wrkgrp, source.unitcode, source.deptcode, " +
                              "        source.statcode, source.seccode, source.secname); ";

                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        sql = "drop table #tmpSec";
                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            #endregion
        }

        public static void SyncCatg(string strRemoteServer, string location)
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(strRemoteServer))
                {
                    cnRemote.Open();
                    //first get all masters:

                    SqlConnection cnLocal;

                    //create a temp table
                    using (cnLocal = new SqlConnection(ThisServer))
                    {
                        string sql = "select top 0 * into #tmpCat from Categories";
                        cnLocal.Open();
                        SqlCommand cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        //get data from attendance server
                        sql = "select CompCode, WrkGrp, CatCode, CatDesc, " +
                              "'" + location + "' as location " +
                              " from MastCat";
                        SqlDataAdapter da = new SqlDataAdapter(sql, cnRemote);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        using (SqlBulkCopy bulk = new SqlBulkCopy(cnLocal))
                        {
                            bulk.DestinationTableName = "#tmpCat";

                            bulk.ColumnMappings.Add("CompCode", "CompCode");
                            bulk.ColumnMappings.Add("WrkGrp", "WrkGrp");
                            bulk.ColumnMappings.Add("CatCode", "CatCode");
                            bulk.ColumnMappings.Add("CatDesc", "CatName");
                            bulk.ColumnMappings.Add("Location", "Location");

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Categories as target " +
                              "using #tmpCat as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.CatCode = Source.CatCode and " +
                              "Target.Location = Source.Location " +
                              "when matched then " +
                              "update set Target.CatName = Source.CatName " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, catcode, catname, location) " +
                              "values (source.compcode, source.wrkgrp, source.catcode, source.catname, " +
                              "'" + location + "' ); ";

                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        sql = "drop table #tmpCat";
                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            #endregion
        }

        public static void SyncDesg(string strRemoteServer, string location)
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(strRemoteServer))
                {
                    cnRemote.Open();
                    //first get all masters:

                    SqlConnection cnLocal;

                    //create a temp table
                    using (cnLocal = new SqlConnection(ThisServer))
                    {
                        string sql = "select top 0 * into #tmpDesg from Designations";
                        cnLocal.Open();
                        SqlCommand cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        //get data from attendance server
                        sql = "select CompCode, WrkGrp, DesgCode, DesgDesc, " +
                              "'" + location + "' as location " +
                              " from MastDesg";
                        SqlDataAdapter da = new SqlDataAdapter(sql, cnRemote);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        using (SqlBulkCopy bulk = new SqlBulkCopy(cnLocal))
                        {
                            bulk.DestinationTableName = "#tmpDesg";

                            bulk.ColumnMappings.Add("CompCode", "CompCode");
                            bulk.ColumnMappings.Add("WrkGrp", "WrkGrp");
                            bulk.ColumnMappings.Add("DesgCode", "DesgCode");
                            bulk.ColumnMappings.Add("DesgDesc", "DesgName");
                            bulk.ColumnMappings.Add("Location", "Location");

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Designations as target " +
                              "using #tmpDesg as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.DesgCode = Source.DesgCode and " +
                              "Target.Location = Source.Location " +
                              "when matched then " +
                              "update set Target.DesgName = Source.DesgName " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, desgcode, desgname, location) " +
                              "values (source.compcode, source.wrkgrp, source.desgcode, source.desgname, " +
                              "'" + location + "' ); ";

                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        sql = "drop table #tmpDesg";
                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            #endregion
        }

        public static void SyncGrade(string strRemoteServer, string location)
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(strRemoteServer))
                {
                    cnRemote.Open();
                    //first get all masters:

                    SqlConnection cnLocal;

                    //create a temp table
                    using (cnLocal = new SqlConnection(ThisServer))
                    {
                        string sql = "select top 0 * into #tmpGrade from Grades";
                        cnLocal.Open();
                        SqlCommand cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        //get data from attendance server
                        sql = "select CompCode, WrkGrp, GradeCode, GradeDesc,  " +
                              "'" + location + "' as location " +
                              "from MastGrade";
                        SqlDataAdapter da = new SqlDataAdapter(sql, cnRemote);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        using (SqlBulkCopy bulk = new SqlBulkCopy(cnLocal))
                        {
                            bulk.DestinationTableName = "#tmpGrade";

                            bulk.ColumnMappings.Add("CompCode", "CompCode");
                            bulk.ColumnMappings.Add("WrkGrp", "WrkGrp");
                            bulk.ColumnMappings.Add("GradeCode", "GradeCode");
                            bulk.ColumnMappings.Add("GradeDesc", "GradeName");
                            bulk.ColumnMappings.Add("Location", "Location");

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Grades as target " +
                              "using #tmpGrade as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.GradeCode = Source.GradeCode and " +
                              "Target.location = Source.location " +
                              "when matched then " +
                              "update set Target.GradeName = Source.GradeName " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, gradecode, gradename, location) " +
                              "values (source.compcode, source.wrkgrp, source.gradecode, source.gradename, " +
                              "'" + location + "' ); ";

                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        sql = "drop table #tmpGrade";
                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            #endregion
        }

        public static void SyncEmpType(string strRemoteServer, string location)
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(strRemoteServer))
                {
                    cnRemote.Open();
                    //first get all masters:

                    SqlConnection cnLocal;

                    //create a temp table
                    using (cnLocal = new SqlConnection(ThisServer))
                    {
                        string sql = "select top 0 * into #tmpEmpTyp from EmpTypes";
                        cnLocal.Open();
                        SqlCommand cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        //get data from attendance server
                        sql = "select CompCode, WrkGrp, EmpTypeCode, EmpTypeDesc, " +
                              "'" + location + "' as location " +
                              " from MastEmpType";
                        SqlDataAdapter da = new SqlDataAdapter(sql, cnRemote);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        using (SqlBulkCopy bulk = new SqlBulkCopy(cnLocal))
                        {
                            bulk.DestinationTableName = "#tmpEmpTyp";

                            bulk.ColumnMappings.Add("CompCode", "CompCode");
                            bulk.ColumnMappings.Add("WrkGrp", "WrkGrp");
                            bulk.ColumnMappings.Add("EmpTypeCode", "EmpTypeCode");
                            bulk.ColumnMappings.Add("EmpTypeDesc", "EmpTypeName");
                            bulk.ColumnMappings.Add("Location", "Location");

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into EmpTypes as target " +
                              "using #tmpEmpTyp as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.EmpTypeCode = Source.EmpTypeCode and " +
                              "Target.Location = Source.Location " +
                              "when matched then " +
                              "update set Target.EmpTypeName = Source.EmpTypeName " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, EmpTypeCode, EmpTypeName, Location ) " +
                              "values (source.compcode, source.wrkgrp, source.EmpTypeCode, source.EmpTypeName, " +
                              "'" + location + "' ); ";

                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        sql = "drop table #tmpEmpTyp";
                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            #endregion
        }

        public static void SyncEmp(string strRemoteServer, string location)
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(strRemoteServer))
                {
                    cnRemote.Open();
                    //first get all masters:

                    SqlConnection cnLocal;

                    //create a temp table
                    using (cnLocal = new SqlConnection(ThisServer))
                    {
                        string sql = "select top 0 * into #tmpEmp from Employees";
                        cnLocal.Open();
                        SqlCommand cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();

                        if (location == Locations.Kjqtl || location == Locations.Kjsaw)
                        {
                            sql = "select CompCode, EmpUnqId, WrkGrp, EmpName, FatherName, " +
                                  "Active, EmpTypeCode, UnitCode, DeptCode, StatCode, CatCode, " +
                                  "DesgCode, GradCode, 0 as IsHod, 0 as IsReleaser, 0 as IsHrUser, OtFlg as OtFlag, " +
                                  "0 as IsAdmin, 0 as IsGpReleaser, 0 as IsSecUser, " +
                                  "'" + location + "' as location " +
                                  "from MastEmp where active = 1 ";

                        }
                        else
                        {
                            //get data from attendance server
                            sql = "select CompCode, EmpUnqId, WrkGrp, EmpName, FatherName, " +
                                  "Active, EmpTypeCode, UnitCode, DeptCode, StatCode, CatCode, " +
                                  "DesgCode, GradCode, 0 as IsHod, 0 as IsReleaser, 0 as IsHrUser, OtFlg as OtFlag, " +
                                  "0 as IsAdmin, 0 as IsGpReleaser, 0 as IsSecUser, " +
                                  "'" + location + "' as location " +
                                  "from MastEmp ";

                        }


                        SqlDataAdapter da = new SqlDataAdapter(sql, cnRemote);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        using (SqlBulkCopy bulk = new SqlBulkCopy(cnLocal))
                        {
                            bulk.DestinationTableName = "#tmpEmp";

                            bulk.ColumnMappings.Add("CompCode", "CompCode");
                            bulk.ColumnMappings.Add("EmpUnqId", "EmpUnqId");
                            bulk.ColumnMappings.Add("WrkGrp", "WrkGrp");
                            bulk.ColumnMappings.Add("EmpTypeCode", "EmpTypeCode");
                            bulk.ColumnMappings.Add("UnitCode", "UnitCode");
                            bulk.ColumnMappings.Add("DeptCode", "DeptCode");
                            bulk.ColumnMappings.Add("StatCode", "StatCode");
                            //bulk.ColumnMappings.Add("SecCode", "SecCode");
                            bulk.ColumnMappings.Add("CatCode", "CatCode");
                            bulk.ColumnMappings.Add("DesgCode", "DesgCode");
                            bulk.ColumnMappings.Add("GradCode", "GradeCode");
                            bulk.ColumnMappings.Add("EmpName", "EmpName");
                            bulk.ColumnMappings.Add("FatherName", "FatherName");
                            bulk.ColumnMappings.Add("Active", "Active");
                            bulk.ColumnMappings.Add("IsHod", "IsHod");
                            bulk.ColumnMappings.Add("IsReleaser", "IsReleaser");
                            bulk.ColumnMappings.Add("IsHrUser", "IsHrUser");
                            bulk.ColumnMappings.Add("OtFlag", "OtFlag");
                            bulk.ColumnMappings.Add("IsAdmin", "IsAdmin");
                            bulk.ColumnMappings.Add("IsGpReleaser", "IsGpReleaser");
                            bulk.ColumnMappings.Add("IsSecUser", "IsSecUser");
                            bulk.ColumnMappings.Add("Location", "Location");

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Employees as target " +
                              "using #tmpEmp as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.EmpUnqId = Source.EmpUnqId " +
                              "when matched then " +
                              "update set " +
                              "Target.WrkGrp = Source.WrkGrp, " +
                              "Target.EmpTypeCode = Source.EmpTypeCode, " +
                              "Target.UnitCode = Source.UnitCode, " +
                              "Target.DeptCode = Source.DeptCode, " +
                              "Target.StatCode = Source.StatCode, " +
                            //"Target.SecCode = Source.SecCode, " +
                              "Target.CatCode = Source.CatCode, " +
                              "Target.DesgCode = Source.DesgCode, " +
                              "Target.GradeCode = Source.GradeCode, " +
                              "Target.EmpName = Source.EmpName, " +
                              "Target.FatherName = Source.FatherName, " +
                              "Target.OtFlag = Source.OtFlag," +
                              "Target.Active = Source.Active, " +
                              "Target.Location = Source.Location " +
                            //"Target.IsHod = Source.IsHod " +
                              "when not matched then " +
                              "insert (empunqid, compcode, wrkgrp, emptypecode, " +
                              "unitcode, deptcode, statcode, " +
                            //"seccode, " +
                              "catcode, " +
                              "desgcode, gradecode, empname, fathername, " +
                              "active, OtFlag, ishod, isreleaser, ishruser, pass, " +
                              "isadmin, isgpreleaser, issecuser, location ) " +
                              "values (source.empunqid, source.compcode, source.wrkgrp, source.emptypecode, " +
                              "source.unitcode, source.deptcode, source.statcode, " +
                            //"source.seccode, " +
                              "source.catcode, " +
                              "source.desgcode, source.gradecode, source.empname, source.fathername, " +
                              "source.active, source.OtFlag, 0, 0, 0, source.empunqid, 0, 0, 0, " +
                              "'" + location + "'); ";

                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();


                        sql = "drop table #tmpEmp";
                        cmd = new SqlCommand(sql, cnLocal);
                        cmd.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            #endregion
        }

        public static void UpdateOpenMonth()
        {

        }

        /// <summary>
        /// Get Leaves from Attendance Server
        /// </summary>
        /// <param name="empUnqId">Employee unique Id</param>
        /// <param name="year">Year</param>
        /// <returns>List of LeaveEntryDto</returns>
        public static List<LeaveEntryDto> GetLeaveEntries(string empUnqId)
        {
            List<LeaveEntryDto> leaves = new List<LeaveEntryDto>();

            ApplicationDbContext context = new ApplicationDbContext();
            var emp = context.Employees.Single(e => e.EmpUnqId == empUnqId);

            string strRemoteServer = GetRemoteServer(emp.Location);


            using (SqlConnection cn = new SqlConnection(strRemoteServer))
            {
                cn.Open();
                string sql = "select top 20 " +
                             "tYear, EmpUnqId, CompCode, WrkGrp, FromDt, ToDt, " +
                             "LeaveTyp as LeaveTypeCode, TotDay, LeaveDed, LeaveHalf " +
                             "from LeaveEntry where empUnqId = '" + empUnqId + "' " +
                             "order by todt desc";

                SqlCommand cmd = new SqlCommand(sql, cn);
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    LeaveEntryDto leave = new LeaveEntryDto
                    {
                        YearMonth = Convert.ToInt32(dr["tYear"].ToString()),
                        EmpUnqId = dr["EmpUnqId"].ToString(),
                        CompCode = dr["CompCode"].ToString(),
                        WrkGrp = dr["WrkGrp"].ToString(),
                        LeaveTypeCode = dr["LeaveTypeCode"].ToString(),
                        FromDt = Convert.ToDateTime(dr["FromDt"].ToString()),
                        ToDt = Convert.ToDateTime(dr["ToDt"].ToString()),
                        HalfDayFlag = Convert.ToBoolean(dr["LeaveHalf"].ToString()),
                        TotalDays = float.Parse(dr["TotDay"].ToString()),
                        LeaveDed = float.Parse(dr["LeaveDed"].ToString())
                    };

                    leaves.Add(leave);
                }


            }
            return leaves;
        }

        public static List<PerfAttdDto> GetPerfAttd(string empUnqId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            List<PerfAttdDto> result = new List<PerfAttdDto>();

            ApplicationDbContext context = new ApplicationDbContext();

            var emp = context.Employees.Single(e => e.EmpUnqId == empUnqId);

            string strRemoteServer = GetRemoteServer(emp.Location);


            using (SqlConnection cn = new SqlConnection(strRemoteServer))
            {
                cn.Open();

                DateTime fromDt = fromDate ?? DateTime.Now.AddDays(-30);
                DateTime toDt = toDate ?? DateTime.Now;


                string sql = "SELECT [tYear],[tDate],[EmpUnqID],[ScheDuleShift],[ConsShift],[ConsIN],[ConsOut]," +
                             "[ConsWrkHrs],[ConsOverTime],[Status],[HalfDay],[LeaveTyp],[LeaveHalf],[Earlycome]," +
                             "[EarlyGoing],[LateCome] " +
                             "FROM [ATTENDANCE].[dbo].[AttdData] " +
                             "where tyear in (" + fromDt.ToString("yyyy") + ", " + toDt.ToString("yyyy") + ") " +
                    //"and compcode = '01' " +
                             "and wrkgrp  = '" + emp.WrkGrp + "' " +
                             "and empunqid = '" + empUnqId + "' " +
                             "and tdate between '" + fromDt.ToString("yyyy-MM-dd") + "' and '" +
                             toDt.ToString("yyyy-MM-dd 23:59:59") + "'";

                SqlCommand cmd = new SqlCommand(sql, cn);
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    var res = new PerfAttdDto
                    {
                        AttdDate = Convert.ToDateTime(dr["tDate"]),
                        EmpUnqId = empUnqId,
                        ScheDuleShift = dr["ScheDuleShift"].ToString(),
                        ConsShift = dr["ConsShift"].ToString(),
                        ConsIn = dr.IsDBNull(dr.GetOrdinal("ConsIn"))
                            ? (DateTime?)null
                            : Convert.ToDateTime(dr["ConsIn"]),
                        ConsOut = dr.IsDBNull(dr.GetOrdinal("ConsOut"))
                            ? (DateTime?)null
                            : Convert.ToDateTime(dr["ConsOut"]),
                        ConsWrkHrs = float.Parse(dr["ConsWrkHrs"].ToString()),
                        ConsOverTime = float.Parse(dr["ConsOverTime"].ToString()),
                        Status = dr["Status"].ToString(),
                        HalfDay = Convert.ToBoolean(dr["HalfDay"]),
                        LeaveType = dr["LeaveTyp"].ToString(),
                        LeaveHalf = Convert.ToBoolean(dr["LeaveHalf"]),
                        Earlycome = dr["Earlycome"].ToString(),
                        EarlyGoing = dr["EarlyGoing"].ToString(),
                        LateCome = dr["LateCome"].ToString()
                    };



                    result.Add(res);
                }

            }

            return result;
        }

        public static List<PerfPunchDto> GetPerfPunch(string empUnqId)
        {
            List<PerfPunchDto> result = new List<PerfPunchDto>();

            ApplicationDbContext context = new ApplicationDbContext();

            var emp = context.Employees.Single(e => e.EmpUnqId == empUnqId);
            string strRemoteServer = GetRemoteServer(emp.Location);

            using (SqlConnection cn = new SqlConnection(strRemoteServer))
            {
                cn.Open();

                DateTime fromDt = DateTime.Now.AddDays(-30);
                DateTime toDt = DateTime.Now;



                string sql = "SELECT [PunchDate],[EmpUnqId],a.[IOFlg],a.[MachineIP],b.[MachineDesc] " +
                             "FROM [ATTENDANCE].[dbo].[AttdLunchGate] a " +
                             "LEFT JOIN [ATTENDANCE].[dbo].[ReaderConFig] b on a.[MachineIP] = b.MachineIP " +
                             "where tyear in (" + fromDt.ToString("yyyy") + ", " + toDt.ToString("yyyy") + ") " +
                             "and empunqid = '" + empUnqId + "' " +
                             "and PunchDate between '" + fromDt.ToString("yyyy-MM-dd") + "' and '" +
                             toDt.ToString("yyyy-MM-dd 23:59:59") + "'";

                SqlCommand cmd = new SqlCommand(sql, cn);
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    var res = new PerfPunchDto
                    {
                        PunchDate = dr.IsDBNull(dr.GetOrdinal("PunchDate"))
                            ? (DateTime?)null
                            : Convert.ToDateTime(dr["PunchDate"]),
                        IoFlag = dr["IOFlg"].ToString(),
                        MachineIp = dr["MachineIP"].ToString(),
                        MachineDesc = dr["MachineDesc"].ToString()
                    };

                    result.Add(res);
                }

            }

            return result;
        }

        public static List<EmpDetailsDto> GetEmpDetails(string empUnqId)
        {

            ApplicationDbContext context = new ApplicationDbContext();

            string sql = "SELECT [EmpUnqID],[BirthDT],[BirthPlace],[ContactNo],[BLDGRP]" +
                         ",[PERADD1],[PERADd2],[PERADD3],[PERADD4],[PERDistrict],[PERCITY],[PERSTATE],[PERPIN],[PERPHONE],[PERPOLICEST]" +
                         ",[PREADD1],[PREADd2],[PREADD3],[PREADD4],[PREDistrict],[PRECITY],[PRESTATE],[PREPIN],[PREPHONE],[PREPOLICEST]" +
                         ",[IDPRF1],[IDPRF1NO],[IDPRF1EXPON],[IDPRF2],[IDPRF2NO],[IDPRF2EXPON],[IDPRF3],[IDPRF3NO],[IDPRF3EXPON]" +
                         ",[RELIGION],[CATAGORY],[SAPID],[JoinDT],[BankAcNo],[BankName],[BankIFSCCode]" +
                         ",[AdharNo]" +
                         " FROM [ATTENDANCE].[dbo].[MastEmp] where EmpUnqId = '" + empUnqId + "'";

            List<EmpDetailsDto> result = new List<EmpDetailsDto>();
            var emp = context.Employees.Single(e => e.EmpUnqId == empUnqId);
            string strRemoteServer = GetRemoteServer(emp.Location);

            using (SqlConnection cn = new SqlConnection(strRemoteServer))
            {
                cn.Open();

                SqlCommand cmd = new SqlCommand(sql, cn);
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {

                    var res = new EmpDetailsDto
                    {
                        EmpUnqId = dr["EmpUnqId"].ToString(),
                        BirthDate = dr.IsDBNull(dr.GetOrdinal("BirthDt"))
                       ? (DateTime?)null
                       : Convert.ToDateTime(dr["BirthDt"]),
                        BirthPlace = dr["BirthPlace"].ToString(),
                        ContactNo = dr["ContactNo"].ToString(),
                        BloodGroup = dr["BLDGRP"].ToString(),

                        PerAdd1 = dr["PERADD1"].ToString(),
                        PerAdd2 = dr["PERADD2"].ToString(),
                        PerAdd3 = dr["PERADD3"].ToString(),
                        PerAdd4 = dr["PERADD4"].ToString(),
                        PerDistrict = dr["PERDistrict"].ToString(),
                        PerCity = dr["PERCITY"].ToString(),
                        PerState = dr["PERSTATE"].ToString(),
                        PerPin = dr["PERPIN"].ToString(),
                        PerPhone = dr["PERPHONE"].ToString(),
                        PerPoliceSt = dr["PERPOLICEST"].ToString(),

                        PreAdd1 = dr["PreADD1"].ToString(),
                        PreAdd2 = dr["PreADD2"].ToString(),
                        PreAdd3 = dr["PreADD3"].ToString(),
                        PreAdd4 = dr["PreADD4"].ToString(),
                        PreDistrict = dr["PreDistrict"].ToString(),
                        PreCity = dr["PreCITY"].ToString(),
                        PreState = dr["PreSTATE"].ToString(),
                        PrePin = dr["PrePIN"].ToString(),
                        PrePhone = dr["PrePHONE"].ToString(),
                        PrePoliceSt = dr["PrePOLICEST"].ToString(),


                        IdPrf1 = dr["IDPRF1"].ToString(),
                        IdPrf1No = dr["IDPRF1No"].ToString(),
                        IdPrf1ExpOn = dr.IsDBNull(dr.GetOrdinal("IDPRF1EXPON"))
                           ? (DateTime?)null
                           : Convert.ToDateTime(dr["IDPRF1EXPON"]),

                        IdPrf2 = dr["IDPRF2"].ToString(),
                        IdPrf2No = dr["IDPRF2No"].ToString(),
                        IdPrf2ExpOn = dr.IsDBNull(dr.GetOrdinal("IDPRF2EXPON"))
                            ? (DateTime?)null
                            : Convert.ToDateTime(dr["IDPRF2EXPON"]),

                        IdPrf3 = dr["IDPRF3"].ToString(),
                        IdPrf3No = dr["IDPRF3No"].ToString(),
                        IdPrf3ExpOn = dr.IsDBNull(dr.GetOrdinal("IDPRF3EXPON"))
                            ? (DateTime?)null
                            : Convert.ToDateTime(dr["IDPRF3EXPON"]),

                        Religion = dr["RELIGION"].ToString(),
                        Category = dr["CATAGORY"].ToString(),

                        SapId = dr["SAPID"].ToString(),
                        JoinDt = dr.IsDBNull(dr.GetOrdinal("JoinDt"))
                           ? (DateTime?)null
                           : Convert.ToDateTime(dr["JoinDt"]),
                        BankAcNo = dr["BankAcNo"].ToString(),
                        BankName = dr["BankName"].ToString(),
                        BankIfsc = dr["BankIFSCCode"].ToString(),
                        AadharNo = dr["AdharNo"].ToString(),
                    };

                    var empAdd = context.EmpAddress
                        .OrderByDescending(e => e.Counter)
                        .FirstOrDefault(e => e.EmpUnqId == res.EmpUnqId);
                    if (empAdd != null)
                    {
                        res.PreAdd1 = empAdd.PreAdd1;
                        res.PreAdd2 = empAdd.PreAdd2;
                        res.PreAdd3 = empAdd.PreAdd3;
                        res.PreAdd4 = empAdd.PreAdd4;
                        res.PreDistrict = empAdd.PreDistrict;
                        res.PreCity = empAdd.PreCity;
                        res.PreState = empAdd.PreState;
                        res.PrePin = empAdd.PrePin;
                        res.PrePhone = empAdd.PrePhone;
                        res.PreResPhone = empAdd.PreResPhone;
                        res.PreEmail = empAdd.PreEmail;
                    }

                    result.Add(res);
                }

            }

            return result;
        }

        public static List<EmpDetailsDto> GetEmpPerAddress(string location)
        {
            ApplicationDbContext context = new ApplicationDbContext();

            string sql = "SELECT [EmpUnqID]" +
                         ",[PERADD1],[PERADd2],[PERADD3],[PERADD4],[PERDistrict],[PERCITY],[PERSTATE],[PERPIN],[PERPHONE],[PERPOLICEST]" +
                         " FROM [ATTENDANCE].[dbo].[MastEmp] where WRKGRP = 'COMP' and Active= 1 ";

            string strRemoteServer = GetRemoteServer(location);

            List<EmpDetailsDto> result = new List<EmpDetailsDto>();

            using (SqlConnection cn = new SqlConnection(strRemoteServer))
            {
                cn.Open();

                SqlCommand cmd = new SqlCommand(sql, cn);
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {

                    var res = new EmpDetailsDto
                    {
                        EmpUnqId = dr["EmpUnqId"].ToString(),
                        PerAdd1 = dr["PERADD1"].ToString(),
                        PerAdd2 = dr["PERADD2"].ToString(),
                        PerAdd3 = dr["PERADD3"].ToString(),
                        PerAdd4 = dr["PERADD4"].ToString(),
                        PerDistrict = dr["PERDistrict"].ToString(),
                        PerCity = dr["PERCITY"].ToString(),
                        PerState = dr["PERSTATE"].ToString(),
                        PerPin = dr["PERPIN"].ToString(),
                        PerPhone = dr["PERPHONE"].ToString(),
                        PerPoliceSt = dr["PERPOLICEST"].ToString(),

                    };

                    result.Add(res);
                }

            }

            return result;
        }

        public static List<EmpEduDto> GetEmpEduDetails(string empUnqId)
        {
            string sql =
                "SELECT [EmpUnqID],[Sr],[PassingYear],[EduName],[Subject],[University],[Per],[OtherInfo] " +
                "FROM [ATTENDANCE].[dbo].[MastEmpEDU] where EmpUnqId = '" + empUnqId + "'";


            List<EmpEduDto> result = new List<EmpEduDto>();

            ApplicationDbContext context = new ApplicationDbContext();
            var emp = context.Employees.Single(e => e.EmpUnqId == empUnqId);
            string strRemoteServer = GetRemoteServer(emp.Location);

            using (SqlConnection cn = new SqlConnection(strRemoteServer))
            {
                cn.Open();

                SqlCommand cmd = new SqlCommand(sql, cn);
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    var res = new EmpEduDto
                    {
                        EmpUnqId = dr["EmpUnqId"].ToString(),
                        PassingYear = Int32.Parse(dr["PassingYear"].ToString()),
                        EduName = dr["EduName"].ToString(),
                        Subject = dr["Subject"].ToString(),
                        University = dr["University"].ToString(),
                        Percentage = Int32.Parse(dr["Per"].ToString()),
                        Remarks = dr["OtherInfo"].ToString()
                    };

                    result.Add(res);
                }

            }

            return result;
        }

    }
}
