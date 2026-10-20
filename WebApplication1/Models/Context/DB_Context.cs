using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using WebApplication1.Models.Tables;
using WebApplication1.Models.Maps;
namespace WebApplication1.Models.Context
{
    public class DB_Context : DbContext
    { 
       static DB_Context() {
            Database.SetInitializer<DB_Context>(null);
        }
        public DB_Context() : base("name=db_greenresidences") { }
        public virtual DbSet<unit_image> unit_image { get; set; }
        public virtual DbSet<unit_amenity> unit_amenity { get; set; }
        public virtual DbSet<unit> unit { get; set; }
        public virtual DbSet<tenant_document> tenant_document { get; set; }
        public virtual DbSet<tenant> tenant { get; set; }
        public virtual DbSet<payment> payment { get; set; }
        public virtual DbSet<maintenance_request> maintenance_request { get; set; }
        public virtual DbSet<co_occupant> co_occupant { get; set; }
        public virtual DbSet<booking> booking { get; set; }
        public virtual DbSet<amenity> amenity { get; set; }
        public virtual DbSet<admin> admin { get; set; }
        public virtual DbSet<sms_log> sms_log { get; set; }
        public virtual DbSet<busy_schedule> busy_schedule { get; set; }
        public virtual DbSet<audit_log> audit_log { get; set; }
        public virtual DbSet<tenant_profile_change_request> tenant_profile_change_request { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Configurations.Add(new Maps.unit_image_maps());
            modelBuilder.Configurations.Add(new Maps.unit_amenity_maps());
            modelBuilder.Configurations.Add(new Maps.unit_maps());
            modelBuilder.Configurations.Add(new Maps.tenant_document_maps());
            modelBuilder.Configurations.Add(new Maps.tenant_maps());
            modelBuilder.Configurations.Add(new Maps.payment_maps());
            modelBuilder.Configurations.Add(new Maps.maintenance_request_maps());
            modelBuilder.Configurations.Add(new Maps.co_occupant_maps());
            modelBuilder.Configurations.Add(new Maps.booking_maps());
            modelBuilder.Configurations.Add(new Maps.amenity_maps());
            modelBuilder.Configurations.Add(new Maps.admin_maps());
            modelBuilder.Configurations.Add(new Maps.busy_schedule_maps());
            modelBuilder.Configurations.Add(new Maps.audit_log_maps());
            modelBuilder.Configurations.Add(new Maps.tenant_profile_change_request_maps());
        }

    }
}