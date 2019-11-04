namespace MasterPerform.Contracts
{
    public class ScaleAction
    {
        public ScaleAction(int capacityPlanCount)
            => this.CapacityPlanCount = capacityPlanCount;

        public int CapacityPlanCount { get; set; }
    }
}