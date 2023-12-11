using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Consumer
{
    internal class ApplicationContext : DbContext
    {
        private readonly string dbFilePath = @"..\..\..\statuses.db";
        internal DbSet<ModuleInfo> ModuleInfos { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={dbFilePath}");
        }
    }
}
