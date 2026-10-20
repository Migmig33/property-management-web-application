using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication1.Models.Tables
{
    public class tenant_profile_change_request
    {
        public int id { get; set; }
        public int Tid { get; set; }
        public string requestedEmail { get; set; }
        public string requestedPhone { get; set; }
        public string requestedOccupation { get; set; }
        public string requestedAddress { get; set; }
        public string reason { get; set; }
        public string status { get; set; }
        public DateTime requestedAt { get; set; }
        public DateTime? reviewedAt { get; set; }
        public string reviewNotes { get; set; }

    }
}