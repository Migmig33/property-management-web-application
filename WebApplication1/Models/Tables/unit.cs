using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication1.Models.Tables
{
    public class unit
    {
        public int Uid { get; set; }
        public string unitName { get; set; }
        public decimal price { get; set; }
        public string beds { get; set; }
        public int sqm { get; set; }
        public string floor { get; set; }
        public string description { get; set; }
        public string videoUrl { get; set; }
        public string status { get; set; }
        public string address { get; set; }
        public int maxOccupants { get; set; }


    }
}