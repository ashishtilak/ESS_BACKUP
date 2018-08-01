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
        private const string RemoteServer = "Data Source=172.16.12.47;Initial Catalog=ATTENDANCE;Integrated Security=False; User Id=sa; Password=testomonials";

        //private const string ThisServer = "Data Source=172.16.12.14;Initial Catalog=ESS;Integrated Security=False; User Id=sa; Password=testomonials@123";
        private static readonly string ThisServer = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        //public static readonly string AttendanceServerApi = "http://172.16.12.44:8082";
        public static readonly string AttendanceServerApi = "http://172.16.12.48:9002";



        /// <summary>
        /// Get list of Holidays
        /// </summary>
        /// <param name="fromDt"> From Date </param>
        /// <param name="toDt"> To Date</param>
        /// <param name="compCode"> Company Code</param>
        /// <param name="wrkGrp"> Work Group </param>
        /// <returns></returns>
        public static List<DateTime> GetHolidays(DateTime fromDt, DateTime toDt, string compCode, string wrkGrp)
        {
            List<DateTime> holidays = new List<DateTime>();

            using (SqlConnection cn = new SqlConnection(RemoteServer))
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


        public static List<HolidayDto> GetHolidays(string compCode, string wrkGrp, int tYear)
        {
            List<HolidayDto> holidays = new List<HolidayDto>();

            using (SqlConnection cn = new SqlConnection(RemoteServer))
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


        public static bool GetOptionalHolidays(DateTime leaveDate)
        {
            using (SqlConnection cn = new SqlConnection(RemoteServer))
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

            using (SqlConnection cn = new SqlConnection(RemoteServer))
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

            using (SqlConnection cn = new SqlConnection(RemoteServer))
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
        public static void SyncData()
        {
            SyncCompany();
            SyncWrkGrp();
            SyncUnit();
            SyncDept();
            SyncStat();
            //SyncSec();
            SyncCatg();
            SyncDesg();
            SyncGrade();
            SyncEmpType();
            SyncEmp();
        }

        public static void SyncCompany()
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(RemoteServer))
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
                        sql = "select CompCode, CompName from MastComp";
                        SqlDataAdapter da = new SqlDataAdapter(sql, cnRemote);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        using (SqlBulkCopy bulk = new SqlBulkCopy(cnLocal))
                        {
                            bulk.DestinationTableName = "#tmpCompanies";

                            bulk.ColumnMappings.Add("CompCode", "CompCode");
                            bulk.ColumnMappings.Add("CompName", "CompName");

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Companies as target " +
                              "using #tmpCompanies as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode " +
                              "when matched then " +
                              "update set Target.CompName = Source.CompName " +
                              "when not matched then " +
                              "insert (compcode, compname) values (source.compcode, source.compname); ";

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

        public static void SyncWrkGrp()
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(RemoteServer))
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
                        sql = "select CompCode, WrkGrp, WrkGrpDesc from MastWorkGrp";
                        SqlDataAdapter da = new SqlDataAdapter(sql, cnRemote);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        using (SqlBulkCopy bulk = new SqlBulkCopy(cnLocal))
                        {
                            bulk.DestinationTableName = "#tmpWrkGrp";

                            bulk.ColumnMappings.Add("CompCode", "CompCode");
                            bulk.ColumnMappings.Add("WrkGrp", "WrkGrp");
                            bulk.ColumnMappings.Add("WrkGrpDesc", "WrkGrpDesc");

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into WorkGroups as target " +
                              "using #tmpWrkGrp as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and Target.WrkGrp = Source.WrkGrp " +
                              "when matched then " +
                              "update set Target.WrkGrpDesc = Source.WrkGrpDesc " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, WrkGrpDesc) " +
                              "values (source.compcode, source.wrkgrp, source.wrkgrpdesc); ";

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

        public static void SyncUnit()
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(RemoteServer))
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
                        sql = "select CompCode, WrkGrp, UnitCode, UnitName from MastUnit";
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

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Units as target " +
                              "using #tmpUnits as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.UnitCode = Source.UnitCode " +
                              "when matched then " +
                              "update set Target.UnitName = Source.UnitName " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, unitcode, unitname) " +
                              "values (source.compcode, source.wrkgrp, source.unitcode, source.unitname); ";

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

        public static void SyncDept()
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(RemoteServer))
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
                        sql = "select CompCode, WrkGrp, UnitCode, DeptCode, DeptDesc from MastDept";
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

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Departments as target " +
                              "using #tmpDepts as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.UnitCode = Source.UnitCode and " +
                              "Target.DeptCode = Source.DeptCode " +
                              "when matched then " +
                              "update set Target.DeptName = Source.Deptname " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, unitcode, deptcode, deptname ) " +
                              "values (source.compcode, source.wrkgrp, source.unitcode, source.deptcode, source.deptname); ";

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

        public static void SyncStat()
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(RemoteServer))
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
                        sql = "select CompCode, WrkGrp, UnitCode, DeptCode, StatCode, StatDesc from MastStat";
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
                              "Target.StatCode = Source.StatCode " +
                              "when matched then " +
                              "update set Target.StatName = Source.StatName " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, unitcode, deptcode, statcode, statname ) " +
                              "values (source.compcode, source.wrkgrp, source.unitcode, source.deptcode, " +
                              "        source.statcode, source.statname); ";

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

        public static void SyncSec()
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(RemoteServer))
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

        public static void SyncCatg()
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(RemoteServer))
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
                        sql = "select CompCode, WrkGrp, CatCode, CatDesc from MastCat";
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

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Categories as target " +
                              "using #tmpCat as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.CatCode = Source.CatCode " +
                              "when matched then " +
                              "update set Target.CatName = Source.CatName " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, catcode, catname) " +
                              "values (source.compcode, source.wrkgrp, source.catcode, source.catname); ";

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

        public static void SyncDesg()
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(RemoteServer))
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
                        sql = "select CompCode, WrkGrp, DesgCode, DesgDesc from MastDesg";
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

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Designations as target " +
                              "using #tmpDesg as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.DesgCode = Source.DesgCode " +
                              "when matched then " +
                              "update set Target.DesgName = Source.DesgName " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, desgcode, desgname) " +
                              "values (source.compcode, source.wrkgrp, source.desgcode, source.desgname); ";

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

        public static void SyncGrade()
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(RemoteServer))
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
                        sql = "select CompCode, WrkGrp, GradeCode, GradeDesc from MastGrade";
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

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into Grades as target " +
                              "using #tmpGrade as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.GradeCode = Source.GradeCode " +
                              "when matched then " +
                              "update set Target.GradeName = Source.GradeName " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, gradecode, gradename) " +
                              "values (source.compcode, source.wrkgrp, source.gradecode, source.gradename); ";

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

        public static void SyncEmpType()
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(RemoteServer))
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
                        sql = "select CompCode, WrkGrp, EmpTypeCode, EmpTypeDesc from MastEmpType";
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

                            bulk.WriteToServer(dt);
                        }

                        //bulk copy done, now use MERGE

                        sql = "merge into EmpTypes as target " +
                              "using #tmpEmpTyp as Source " +
                              "on " +
                              "Target.CompCode = Source.CompCode and " +
                              "Target.WrkGrp = Source.WrkGrp and " +
                              "Target.EmpTypeCode = Source.EmpTypeCode " +
                              "when matched then " +
                              "update set Target.EmpTypeName = Source.EmpTypeName " +
                              "when not matched then " +
                              "insert (compcode, wrkgrp, EmpTypeCode, EmpTypeName) " +
                              "values (source.compcode, source.wrkgrp, source.EmpTypeCode, source.EmpTypeName); ";

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

        public static void SyncEmp()
        {
            #region tryblock

            try
            {
                using (SqlConnection cnRemote = new SqlConnection(RemoteServer))
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


                        //get data from attendance server
                        sql = "select CompCode, EmpUnqId, WrkGrp, EmpName, FatherName, " +
                              "Active, EmpTypeCode, UnitCode, DeptCode, StatCode, CatCode, " +
                              "DesgCode, GradCode, 0 as IsHod, 0 as IsReleaser, 0 as IsHrUser from MastEmp ";
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
                              "Target.Active = Source.Active " +
                            //"Target.IsHod = Source.IsHod " +
                              "when not matched then " +
                              "insert (empunqid, compcode, wrkgrp, emptypecode, " +
                              "unitcode, deptcode, statcode, " +
                            //"seccode, " +
                              "catcode, " +
                              "desgcode, gradecode, empname, fathername, " +
                              "active, ishod, isreleaser, ishruser, pass) " +
                              "values (source.empunqid, source.compcode, source.wrkgrp, source.emptypecode, " +
                              "source.unitcode, source.deptcode, source.statcode, " +
                            //"source.seccode, " +
                              "source.catcode, " +
                              "source.desgcode, source.gradecode, source.empname, source.fathername, " +
                              "source.active, 0, 0, 0, empunqid); ";

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
        public static List<LeaveEntryDto> GetLeaveEntries(string empUnqId, int year)
        {
            List<LeaveEntryDto> leaves = new List<LeaveEntryDto>();

            using (SqlConnection cn = new SqlConnection(RemoteServer))
            {
                cn.Open();
                string sql = "select " +
                             "tYear, EmpUnqId, CompCode, WrkGrp, FromDt, ToDt, " +
                             "LeaveTyp as LeaveTypeCode, TotDay, LeaveDed, LeaveHalf " +
                             "from LeaveEntry where tyear=" + year + " and empUnqId = '" + empUnqId + "'";

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

            using (SqlConnection cn = new SqlConnection(RemoteServer))
            {
                cn.Open();

                DateTime fromDt = fromDate ?? DateTime.Now.AddDays(-30);
                DateTime toDt = toDate ?? DateTime.Now;


                string sql = "SELECT [tYear],[tDate],[EmpUnqID],[ScheDuleShift],[ConsShift],[ConsIN],[ConsOut]," +
                             "[ConsWrkHrs],[ConsOverTime],[Status],[HalfDay],[LeaveTyp],[LeaveHalf],[Earlycome]," +
                             "[EarlyGoing],[LateCome] " +
                             "FROM [ATTENDANCE].[dbo].[AttdData] " +
                             "where tyear in (" + fromDt.ToString("yyyy") + ", " + toDt.ToString("yyyy") + ") " +
                             "and compcode = '01' " +
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

            using (SqlConnection cn = new SqlConnection(RemoteServer))
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
            string sql = "SELECT [EmpUnqID],[BirthDT],[BirthPlace],[ContactNo],[BLDGRP]" +
                         ",[PERADD1],[PERADd2],[PERADD3],[PERADD4],[PERDistrict],[PERCITY],[PERSTATE],[PERPIN],[PERPHONE],[PERPOLICEST]" +
                         ",[PREADD1],[PREADd2],[PREADD3],[PREADD4],[PREDistrict],[PRECITY],[PRESTATE],[PREPIN],[PREPHONE],[PREPOLICEST]" +
                         ",[IDPRF1],[IDPRF1NO],[IDPRF1EXPON],[IDPRF2],[IDPRF2NO],[IDPRF2EXPON],[IDPRF3],[IDPRF3NO],[IDPRF3EXPON]" +
                         ",[RELIGION],[CATAGORY],[SAPID],[JoinDT],[BankAcNo],[BankName],[BankIFSCCode]" +
                         ",[AdharNo]" +
                         " FROM [ATTENDANCE].[dbo].[MastEmp] where EmpUnqId = '" + empUnqId + "'";


            List<EmpDetailsDto> result = new List<EmpDetailsDto>();

            using (SqlConnection cn = new SqlConnection(RemoteServer))
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

            using (SqlConnection cn = new SqlConnection(RemoteServer))
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
