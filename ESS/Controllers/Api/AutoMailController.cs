using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Web.Http;
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
        public IHttpActionResult SendMail(string type, int id, string releaseAuth)
        {
            SmtpClient smtpClient = new SmtpClient("172.16.12.47", 25)
            {
                //Credentials = new System.Net.NetworkCredential("tilaka@jindalsaw.com", "ashish123$$"),
                UseDefaultCredentials = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = false
            };

            MailMessage mail = new MailMessage
            {
                From = new MailAddress("attendance.ipu@jindalsaw.com", "Attendance IPU"),
                Subject = "Leave release",
                BodyEncoding = System.Text.Encoding.UTF8,
                IsBodyHtml = true,
                Body = "Please release following"

            };

            mail.To.Add(new MailAddress("ashish.tilak@jindalsaw.com"));
            smtpClient.Send(mail);

            return Ok();
        }
    }
}
