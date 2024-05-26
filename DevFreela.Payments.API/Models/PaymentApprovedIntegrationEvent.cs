namespace DevFreela.Payments.API.Models
{
    public class PaymentApprovedIntegrationEvent
    {
        public PaymentApprovedIntegrationEvent(int projectId)
        {
            ProjectId = projectId;
        }

        public int ProjectId { get; set; }
    }
}
