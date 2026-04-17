using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.HVAC.HeatSource;
using Popolo.Core.HVAC.SystemModel;
using Popolo.Core.Physics;

namespace Popolo.Core.Samples
{
  internal class Program
  {
    static void Main(string[] args)
    {
      double Cp = 4.186;
      var (hs, chiller, _, _, _) = MakeCentrifugalSystem();
      double load = 500.0 * 0.6;
      hs.ForecastSupplyWaterTemperature(load / (Cp * 5), 12, 0, 40);
    }


    /// <summary>
    /// Test3 (HeatSourceSubsystemTest3) の前半に対応。
    /// ターボ冷凍機 + 冷却塔 で 60%負荷の冷却運転。
    /// </summary>
    private static (HeatSourceSystemModel, SimpleCentrifugalChiller, CentrifugalPump, CentrifugalPump, CoolingTower)
        MakeCentrifugalSystem()
    {
      const double Cp = 4.186;
      const double NCH_FLOW = 500.0 / (12 - 7) / Cp;
      const double NCD_FLOW = 1670.0 / 60;

      var chiller = new SimpleCentrifugalChiller(
          500.0 / 6.0, 0.2, 12, 7, 37, NCH_FLOW, false);
      var chPmp = new CentrifugalPump(
          150, 1e-3 * NCH_FLOW, 140, 1e-3 * NCH_FLOW,
          CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
      var cdPmp = new CentrifugalPump(
          150, 1e-3 * NCD_FLOW, 140, 1e-3 * NCD_FLOW,
          CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
      var cTower = new CoolingTower(
          37, 32, 27, NCD_FLOW,
          CoolingTower.AirFlowDirection.CrossFlow, false);

      var crSystem = new CentrifugalChillerSystem(chiller, chPmp, cdPmp, cTower, 1, 1);

      var hsSystem = new HeatSourceSystemModel(
          new IHeatSourceSubSystem[] { crSystem });
      hsSystem.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
      hsSystem.SetChillingOperationSequence(0, 1);
      hsSystem.ChilledWaterSupplyTemperatureSetpoint = 7.0;
      hsSystem.OutdoorAir = new MoistAir(35, 0.0195);
      hsSystem.TimeStep = 3600;

      return (hsSystem, chiller, chPmp, cdPmp, cTower);
    }


  }
}
