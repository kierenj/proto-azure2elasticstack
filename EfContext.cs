using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace azure2elasticstack
{
    public class EfContext : DbContext
    {
        public DbSet<MetricLastDate> MetricLastDates { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=azure2elasticstack_v3.db");
        }
    }

    public class MetricLastDate
    {
        [Key]
        [MaxLength(500)]
        public string Key { get; set; }

        public DateTime LastDate { get; set; }
    }
}