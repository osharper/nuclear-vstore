using System;
using System.Collections.Generic;

namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Order
    {
        public Order()
        {
            OrderPositions = new HashSet<OrderPosition>();
        }

        public long Id { get; set; }
        public Guid ReplicationCode { get; set; }
        public string Number { get; set; }
        public DateTime BeginDistributionDate { get; set; }
        public DateTime EndDistributionDatePlan { get; set; }
        public DateTime EndDistributionDateFact { get; set; }
        public int BeginReleaseNumber { get; set; }
        public int EndReleaseNumberPlan { get; set; }
        public int EndReleaseNumberFact { get; set; }
        public short ReleaseCountPlan { get; set; }
        public short ReleaseCountFact { get; set; }
        public int WorkflowStepId { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public DateTime? RejectionDate { get; set; }
        public DateTime SignupDate { get; set; }
        public bool IsTerminated { get; set; }
        public long? DgppId { get; set; }
        public byte HasDocumentsDebt { get; set; }
        public string DocumentsComment { get; set; }
        public long? InspectorCode { get; set; }
        public string Comment { get; set; }
        public int TerminationReason { get; set; }
        public int OrderType { get; set; }
        public int PaymentMethod { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }

        public ICollection<OrderPosition> OrderPositions { get; set; }
    }
}
