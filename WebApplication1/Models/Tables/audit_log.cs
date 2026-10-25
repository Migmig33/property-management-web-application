using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication1.Models.Tables
{
    public class audit_log
    {
        public int logId { get; set; }
        public string type { get; set; }       // login | settings | security | user | backup
        public string severity { get; set; }   // info | warning | critical
        public string message { get; set; }
        public string actor { get; set; }
        public DateTime createdAt { get; set; }
        public int is_archived { get; set; }
    }
}