using System;
using System.Collections.Generic;

namespace MigrationTool.Models
{
    public sealed class OrganizationUnit
    {
        public OrganizationUnit()
        {
            OrdersDestOrganizationUnit = new HashSet<Order>();
            OrdersSourceOrganizationUnit = new HashSet<Order>();
        }

        public long Id { get; set; }
        public int? DgppId { get; set; }
        public string SyncCode1C { get; set; }
        public Guid ReplicationCode { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public DateTime FirstEmitDate { get; set; }
        public long CountryId { get; set; }
        public long TimeZoneId { get; set; }
        public DateTime? ErmLaunchDate { get; set; }
        public DateTime? InfoRussiaLaunchDate { get; set; }
        public string ElectronicMedia { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public ICollection<Order> OrdersDestOrganizationUnit { get; set; }
        public ICollection<Order> OrdersSourceOrganizationUnit { get; set; }
    }
}
