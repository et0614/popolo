using Popolo.Core.HVAC.HeatSource;

namespace Popolo.Core.Samples
{
  internal class Program
  {
    static void Main(string[] args)
    {
      var c = new AdsorptionChiller(
                chilledWaterInletTemperature: 14.0,
                chilledWaterOutletTemperature: 9.0,
                chilledWaterFlowRate: 2.0,
                coolingWaterInletTemperature: 30.0,
                coolingWaterOutletTemperature: 35.0,
                coolingWaterFlowRate: 3.0,
                hotWaterInletTemperature: 85.0,
                hotWaterOutletTemperature: 75.0,
                hotWaterFlowRate: 1.0);
      c.ChilledWaterOutletSetPointTemperature = 9.0;

      c.Update(14.0, 2.0, 30.0, 3.0, 85.0, 1.0);
    }
  }
}
