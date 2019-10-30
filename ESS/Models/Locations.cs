﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ESS.Models
{
    public class Locations
    {
        [Key]
        [StringLength(5)]
        public string Location { get; set; }

        public string RemoteConnection { get; set; }
        public string AttendanceServerApi { get; set; }
        public string MailAddress { get; set; }
        public string SmtpClient { get; set; }
        public string PortalAddress { get; set; }


        public static readonly string Ipu = "IPU";
        public static readonly string Nkp = "NKP";
        public static readonly string Kjsaw = "KJSAW";
        public static readonly string Kjqtl = "KJQTL";
        public static readonly string Bellary = "BEL";
    }
}