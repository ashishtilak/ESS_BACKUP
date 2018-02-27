using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Web.Http;
using AutoMapper;
using ESS.Dto;
using ESS.Models;

namespace ESS.Controllers.Api
{
    public class AutoMailController : ApiController
    {
        private ApplicationDbContext _context;

        public AutoMailController()
        {
            _context = new ApplicationDbContext();
        }

        [HttpGet]
        public IHttpActionResult SendMail(string releaseGroupCode, int id, string releaseAuth)
        {
            var leaveAppDto = _context.LeaveApplications
                .Include(e => e.Employee)
                .Include(c => c.Company)
                .Include(cat => cat.Categories)
                .Include(w => w.WorkGroup)
                .Include(d => d.Departments)
                .Include(s => s.Stations)
                .Include(u => u.Units)
                .Include(r => r.ReleaseGroup)
                .Include(rs => rs.RelStrategy)
                .Include(l => l.LeaveApplicationDetails)
                .Select(Mapper.Map<LeaveApplications, LeaveApplicationDto>)
                .FirstOrDefault(a => a.LeaveAppId == id);

            if (leaveAppDto == null)
                return BadRequest("Invalid leave application parameters.");


            var app = _context.ApplReleaseStatus
                .Where(l =>
                    l.YearMonth == leaveAppDto.YearMonth &&
                    l.ReleaseGroupCode == leaveAppDto.ReleaseGroupCode &&
                    l.ApplicationId == leaveAppDto.LeaveAppId &&
                    l.ReleaseAuth == releaseAuth                        //Get from parameter
                    )
                .ToList()
                .Select(Mapper.Map<ApplReleaseStatus, ApplReleaseStatusDto>);

            foreach (var applReleaseStatusDto in app)
            {
                leaveAppDto.ApplReleaseStatus.Add(applReleaseStatusDto);
            }

            var empCode = leaveAppDto.EmpUnqId;
            var empName = leaveAppDto.Employee.EmpName;
            var deptstat = leaveAppDto.Stations.StatName;
            var releaser = _context.Employees.FirstOrDefault(e => e.EmpUnqId == releaseAuth);

            if (releaser == null)
                return BadRequest("Invalid releaser information.");

            if (string.IsNullOrEmpty(releaser.Email))
                return BadRequest("Email not maintained for releaser " + releaser.EmpUnqId + " - " + releaser.EmpName);


            const string header = @"
                <html lang=""en"">
                    <head>    
                        <meta content=""text/html; charset=utf-8"" http-equiv=""Content-Type"">
                        <title>
                            ESS Portal - Automessage
                        </title>
                        <style type=""text/css"">
                            body { font-family: arial, sans-serif; }
                            table {
                                font-family: arial, sans-serif;
                                border-collapse: collapse;
                                width: 80%;
                            }

                            td, th {
                                border: 1px solid #dddddd;
                                text-align: left;
                                padding: 8px;
                            }

                            tr:nth-child(even) {
                                background-color: #dddddd;
                            }
                        </style>
                    </head>
                    <body>
                ";

            string body = "Dear Sir, <br /><br /> " +
                "Following leave application requires your attention: <br/> <br />" +
                "Employee Code: " + empCode + " <br/>" +
                "Employee Name: " + empName + " <br/>" +
                "Dept/Station: " + deptstat + " <br/><br/>";

            body = header + body;

            string bodyTable = "<table> " +
                               "<thead>" +
                               "<tr>" +
                               "<th>Leave Type</th>" +
                               "<th>From Date</th>" +
                               "<th>To Date</th>" +
                               "<th>Total Days</th>" +
                               "<th>Half Day</th>" +
                               "<th>Reason</th>" +
                               "</tr>" +
                               "</thead>" +
                               "<tbody>";



            foreach (var detail in leaveAppDto.LeaveApplicationDetails)
            {
                string bodyLine = "<tr>" +
                              "<td>" + detail.LeaveTypeCode + "</td>" +
                              "<td>" + detail.FromDt.ToString("dd/MM/yyyy") + "</td>" +
                              "<td>" + detail.ToDt.ToString("dd/MM/yyyy") + "</td>" +
                              "<td>" + detail.TotalDays + "</td>" +
                              "<td>" + (detail.HalfDayFlag ? "Yes" : "No") + "</td>" +
                              "<td>" + detail.Remarks + "</td>" +
                              "</tr>";

                bodyTable += bodyLine;
            }

            bodyTable += "</tbody>" +
                         "</table> <br/>";
            body += bodyTable;

            body += "<br/>Kindly review the same in <a href='http://172.16.12.44:8080'>ESS Portal</a>.";
            body += "</body></html>";

            SmtpClient smtpClient = new SmtpClient("172.16.12.47", 25)
            {
                //Credentials = new System.Net.NetworkCredential("tilaka@jindalsaw.com", "ashish123$$"),
                UseDefaultCredentials = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = false
            };

            MailMessage mail = new MailMessage
            {
                From = new MailAddress("attendance.ipu@jindalsaw.com", "ESS Portal"),
                Subject = "Notification from ESS Portal",
                BodyEncoding = System.Text.Encoding.UTF8,
                IsBodyHtml = true,
                Body = body

            };

            mail.To.Add(new MailAddress(releaser.Email));
            smtpClient.Send(mail);

            return Ok();
        }
    }
}
