using System;
using System.Collections.Generic;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Web;
using WebApplication1.Models.Tables;
namespace WebApplication1.Models.Maps
{
    public class tenant_profile_change_request_maps : EntityTypeConfiguration<tenant_profile_change_request>
    {
        public tenant_profile_change_request_maps()
        {
            HasKey(t => t.id);
            ToTable("tenant_profile_change_request");
        }
    }
}