using System;

namespace MasterPerform.Utilities
{
    public static class EnvironmentHelpers
    {
        public static int GetIntegerEnvironmentParameter(string parameterName)
        {
            var param = Environment.GetEnvironmentVariable(parameterName);

            if(int.TryParse(param, out var parsed))
                return parsed;

            throw new Exception($"Cannot parse parameter: {parameterName} to integer.");
        }
    }
}