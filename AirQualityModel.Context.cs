

namespace AirQualityAnalysis
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class AirQualityDBEntities : DbContext
    {
        public AirQualityDBEntities()
            : base("name=AirQualityDBEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<AirQualityRecord> AirQualityRecords { get; set; }
        public virtual DbSet<Alert> Alerts { get; set; }
        public virtual DbSet<AlertNote> AlertNotes { get; set; }
        public virtual DbSet<AQICategory> AQICategories { get; set; }
        public virtual DbSet<City> Cities { get; set; }
        public virtual DbSet<Country> Countries { get; set; }
        public virtual DbSet<GasPollutant> GasPollutants { get; set; }
        public virtual DbSet<ParticulatePollutant> ParticulatePollutants { get; set; }
        public virtual DbSet<Pollutant> Pollutants { get; set; }
        public virtual DbSet<RawAirQualityImport> RawAirQualityImports { get; set; }
        public virtual DbSet<vw_AirQualityRecordDetails> vw_AirQualityRecordDetails { get; set; }
    }
}
