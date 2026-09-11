using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Infrastructure.Context.AccountContext.Mapping;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AccessControlSystem.Infrastructure.Data {
    public class AppDbContext : DbContext {

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {

        }

        public DbSet<Operator> Operators { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            modelBuilder.ApplyConfiguration(new OperatorMap());
        }
    }
}
