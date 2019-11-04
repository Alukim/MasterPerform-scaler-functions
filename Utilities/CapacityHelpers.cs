namespace MasterPerform.Utilities
{
    public static class CapacityHelpers
    {
        public static int CalculateCapacity(double generatedData)
        {
            if (generatedData < 36000)
                return 1;
            
            if (generatedData < 57600)
                return 2;
            
            if (generatedData < 64800)
                return 3;
            
            if(generatedData < 75600)
                return 4;
            
            return 5;
        }

        public static double CalculateCostOfCapacity(int capacity)
            => capacity * MasterPerformMetricsCollector.CostOfOneMachine;
    }
}