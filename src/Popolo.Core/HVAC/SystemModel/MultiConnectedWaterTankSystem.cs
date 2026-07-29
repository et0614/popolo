/* MultiConnectedWaterTankSystem.cs
 * 
 * Copyright (C) 2019 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;

using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.HVAC.HeatSource;
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.Physics;
using Popolo.Core.Numerics;
using Popolo.Core.HVAC.Storage;

namespace Popolo.Core.HVAC.SystemModel
{
  /// <summary>Multi-connected fully-mixed thermal storage tank.</summary>
  public class MultiConnectedWaterTankSystem : IHeatSourceSubSystem
  {

    #region Constant declarations


    #endregion

    #region Instance variables and properties

    /// <summary>Bypass flow rate [kg/s] for AHP inlet temperature control.</summary>
    private double ahpBypass = 0;

    /// <summary>Gets a value indicating whether a forecast calculation is in progress.</summary>
    private bool isForecasting = false;

    /// <summary>Temperatures of each tank section.</summary>
    private double[] oldTemps;

    /// <summary>Temperature rise due to chiller pump work [K].</summary>
    private double dtChgPump;

    /// <summary>Air-heat-source heat pump.</summary>
    private AirHeatSourceModularChillers ahp;

    /// <summary>Water pump.</summary>
    private CentrifugalPump chgPump, rlsPump1, rlsPump2;

    /// <summary>Plate heat exchanger.</summary>
    private PlateHeatExchanger pHex;

    /// <summary>Stratified thermal storage tank.</summary>
    private MultiConnectedWaterTank wTank;

    /// <summary>Gets the air-heat-source heat pump.</summary>
    public IReadOnlyAirHeatSourceModularChillers AHP { get { return ahp; } }

    /// <summary>Gets the thermal storage charge pump.</summary>
    public IReadOnlyCentrifugalPump ChargePump { get { return chgPump; } }

    /// <summary>Gets the primary discharge pump.</summary>
    public IReadOnlyCentrifugalPump ReleasePump1 { get { return rlsPump1; } }

    /// <summary>Gets the secondary discharge pump.</summary>
    public IReadOnlyCentrifugalPump ReleasePump2 { get { return rlsPump2; } }

    /// <summary>Gets the plate heat exchanger.</summary>
    public IReadOnlyPlateHeatExchanger PlateHeatExchanger { get { return pHex; } }

    /// <summary>Stratified thermal storage tank.</summary>
    public IReadOnlyMultiConnectedWaterTank WaterTank { get { return wTank; } }

    /// <summary>Gets or sets the state-update time step for the thermal storage tank [s].</summary>
    public double TankUpdateTimeStep { get; set; } = 600;

    /// <summary>Gets or sets the chilled water thermal storage temperature [°C].</summary>
    public double ChilledWaterStorageTemperature { get; set; } = 5;

    /// <summary>Gets or sets the hot water thermal storage temperature [°C].</summary>
    public double HotWaterStorageTemperature { get; set; } = 45;

    /// <summary>Gets or sets the chilled water inlet temperature setpoint for the heat source unit [°C].</summary>
    public double HeatSourceInletChilledWaterTemperatureSP { get; set; } = 10;

    /// <summary>Gets or sets the hot water inlet temperature setpoint for the heat source unit [°C].</summary>
    public double HeatSourceInletHotWaterTemperatureSP { get; set; } = 40;

    /// <summary>Gets the number of heat source units.</summary>
    public int HeatSourceCount { get; private set; }

    /// <summary>Gets or sets the number of operating heat source units.</summary>
    public int ActiveHeatSourceCount { get; set; }

    #endregion

    #region IHeatSourceSubSystem implementation

    /// <summary>Gets a value indicating whether the chilled water supply is overloaded.</summary>
    public bool IsOverLoad_C { get; private set; }

    /// <summary>Gets a value indicating whether the hot water supply is overloaded.</summary>
    public bool IsOverLoad_H { get; private set; }

    /// <summary>Gets the selectable operating modes.</summary>
    public HeatSourceSystemModel.OperatingMode SelectableMode
    {
      get
      {
        return ahp.IsHeatPumpModel ?
          HeatSourceSystemModel.OperatingMode.Cooling | HeatSourceSystemModel.OperatingMode.Heating :
          HeatSourceSystemModel.OperatingMode.Cooling;
      }
    }

    /// <summary>Gets or sets the current operating mode.</summary>
    public HeatSourceSystemModel.OperatingMode Mode { get; set; }

    /// <summary>Gets or sets the current date and time.</summary>
    public DateTime CurrentDateTime { get; set; }

    /// <summary>Gets or sets the simulation time step [s].</summary>
    public double TimeStep { get; set; }

    /// <summary>Gets or sets the hot water return temperature [°C].</summary>
    public double HotWaterReturnTemperature { get; set; } = 40;

    /// <summary>Gets or sets the hot water supply temperature setpoint [°C].</summary>
    public double HotWaterSupplyTemperatureSetpoint { get; set; } = 45;

    /// <summary>Gets the hot water supply temperature [°C].</summary>
    public double HotWaterSupplyTemperature { get; private set; } = 45;

    /// <summary>Gets the hot water flow rate [kg/s].</summary>
    public double HotWaterFlowRate { get; private set; }

    /// <summary>Gets the maximum hot water flow rate [kg/s].</summary>
    public double MaxHotWaterFlowRate { get { return pHex.MaxSupplyFlowRate; } }

    /// <summary>Gets the minimum hot water flow rate ratio [-].</summary>
    public double MinHotWaterFlowRatio { get { return 0; } }

    /// <summary>Gets or sets the chilled water return temperature [°C].</summary>
    public double ChilledWaterReturnTemperature { get; set; } = 12;

    /// <summary>Gets or sets the chilled water supply temperature setpoint [°C].</summary>
    public double ChilledWaterSupplyTemperatureSetpoint { get; set; } = 7;

    /// <summary>Gets the chilled water supply temperature [°C].</summary>
    public double ChilledWaterSupplyTemperature { get; private set; } = 7;

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    public double ChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the maximum chilled water flow rate [kg/s].</summary>
    public double MaxChilledWaterFlowRate { get { return pHex.MaxSupplyFlowRate; } }

    /// <summary>Gets the minimum chilled water flow rate ratio [-].</summary>
    public double MinChilledWaterFlowRatio { get { return 0; } }

    /// <summary>Gets or sets the outdoor air conditions.</summary>
    public IReadOnlyMoistAir OutdoorAir { get; set; } = new MoistAir(35, 0.0185);

    /// <summary>Shuts off this heat source sub-system.</summary>
    public void ShutOff()
    {
      IsOverLoad_C = IsOverLoad_H = false;
      ahp.ShutOff();
      chgPump.ShutOff();
      pHex.ShutOff();
      rlsPump1.ShutOff();
      rlsPump2.ShutOff();
    }

    /// <summary>Forecasts the supply water temperatures for the given flow rates.</summary>
    /// <param name="chilledWaterFlowRate">Chilled water flow rate [kg/s].</param>
    /// <param name="hotWaterFlowRate">Hot water flow rate [kg/s].</param>
    public void ForecastSupplyWaterTemperature(double chilledWaterFlowRate, double hotWaterFlowRate)
    {
      ChilledWaterFlowRate = chilledWaterFlowRate;
      HotWaterFlowRate = hotWaterFlowRate;

      //Temporarily save and restore the temperature distribution in the tank
      if (isForecasting) wTank.InitializeTemperature(oldTemps);
      else
      {
        isForecasting = true;
        wTank.GetTemperatures(ref oldTemps);
      }

      //Set the AHP operating mode
      if (Mode == HeatSourceSystemModel.OperatingMode.ShutOff) ActiveHeatSourceCount = 0;
      bool charging = (0 < ActiveHeatSourceCount);
      bool isCooling = (Mode == HeatSourceSystemModel.OperatingMode.Cooling);
      if (charging)
      {
        if (isCooling)
        {
          ahp.Mode = AirHeatSourceModularChillers.OperatingMode.Cooling;
          ahp.WaterOutletSetpointTemperature = ChilledWaterStorageTemperature;
          pHex.SupplyTemperatureSetpoint = ChilledWaterSupplyTemperatureSetpoint;
        }
        else 
        {
          ahp.Mode = AirHeatSourceModularChillers.OperatingMode.Heating;
          ahp.WaterOutletSetpointTemperature = HotWaterStorageTemperature;
          pHex.SupplyTemperatureSetpoint = HotWaterSupplyTemperatureSetpoint;
        }
      }
      else ahp.Mode = AirHeatSourceModularChillers.OperatingMode.ShutOff;

      //Keep updating the tank temperatures until the time step has elapsed
      double remTime = TimeStep;
      double aveTemp = 0;
      IsOverLoad_C = IsOverLoad_H = false;
      while (true)
      {
        bool isLastCalc = remTime <= TankUpdateTimeStep;
        wTank.TimeStep = TankUpdateTimeStep;
        if (isLastCalc) wTank.TimeStep = remTime;
        remTime -= wTank.TimeStep;

        //If the AHP is running and there is a secondary side load, iterate on the required HEX flow rate (chasing operation)
        double chFlow = Math.Max(ChilledWaterFlowRate, HotWaterFlowRate);
        if (charging && 0 < chFlow)
        {
          //Calculate the temperature rise across the chilled/hot water pump
          rlsPump1.UpdateState(rlsPump1.DesignFlowRate);
          rlsPump2.UpdateState(0.001 * chFlow);
          double dtCHP = rlsPump2.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * chFlow);

          Roots.ErrorFunction eFnc = delegate (double hexFlow)
          {
            CalcHexHeatTransfer(hexFlow, (isCooling ? ChilledWaterReturnTemperature : HotWaterReturnTemperature) + dtCHP);
            if (isCooling) return pHex.SupplyTemperature - ChilledWaterSupplyTemperatureSetpoint;
            else return HotWaterSupplyTemperatureSetpoint - pHex.SupplyTemperature;
          };

          //Check whether the load can be handled at the maximum flow rate
          if (eFnc(pHex.MaxHeatSourceFlowRate) < 0)
          {
            IsOverLoad_C = IsOverLoad_H = false;
            //Check whether control is possible at the minimum flow rate
            if (0 < eFnc(pHex.MaxHeatSourceFlowRate * 0.001))
              Roots.Bisection(eFnc, pHex.MaxHeatSourceFlowRate * 0.001,
                pHex.MaxHeatSourceFlowRate, 0.01, pHex.MaxHeatSourceFlowRate * 0.01, 20);
          }
          else
          {
            if (isCooling) IsOverLoad_C = true;
            else IsOverLoad_H = true;
          }

          //Update the temperatures in the tank
          double tTNKin;
          bool isFwdFlow = pHex.HeatSourceFlowRate < (ahp.WaterFlowRate - ahpBypass) * ActiveHeatSourceCount;
          if (isFwdFlow) tTNKin = ahp.WaterOutletTemperature;
          else tTNKin = pHex.HeatSourceOutletTemperature
              + rlsPump1.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * pHex.HeatSourceFlowRate);
          wTank.ForecastState(tTNKin, 0.001 * Math.Abs((ahp.WaterFlowRate - ahpBypass) * ActiveHeatSourceCount - pHex.HeatSourceFlowRate), isFwdFlow);
        }
        //AHP off + secondary side load present (discharge operation)
        else if (0 < chFlow)
        {
          ahp.ShutOff();
          chgPump.ShutOff();

          //Calculate the temperature rise across the discharge pump
          rlsPump2.UpdateState(0.001 * chFlow);
          double dtCHP = rlsPump2.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * chFlow);

          //Calculate the required water flow rate through the discharge plate heat exchanger
          pHex.ControlSupplyTemperature
            (WaterTank.WaterOutletTemperarture, (isCooling ? ChilledWaterReturnTemperature : HotWaterReturnTemperature) + dtCHP, chFlow);

          //Update the temperatures in the tank
          double tankInletTemp = 0;
          if (0 < pHex.HeatSourceFlowRate)
          {
            rlsPump1.UpdateState(0.001 * pHex.HeatSourceFlowRate);
            tankInletTemp = pHex.HeatSourceOutletTemperature
              + rlsPump1.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * pHex.HeatSourceFlowRate);
          }
          wTank.ForecastState(tankInletTemp, 0.001 * pHex.HeatSourceFlowRate, false);

          if (isCooling) IsOverLoad_C = pHex.IsOverLoad;
          else IsOverLoad_H = pHex.IsOverLoad;
        }
        //AHP running + no secondary side load (charge operation)
        else if (charging)
        {
          pHex.ShutOff();
          rlsPump1.ShutOff();
          rlsPump2.ShutOff();
          chgPump.UpdateState(chgPump.DesignFlowRate);

          //Control the heat source inlet temperature with the three-way valve
          double tAHPout = ahp.WaterOutletSetpointTemperature;
          double rf = GetFirstTankWaterFlowRate(0, 0, tAHPout);
          double ahpIn = rf * tAHPout + (1 - rf) * wTank.GetTemperature(wTank.TankCount - 1);
          ahp.Update(ahpIn + dtChgPump, 1000 * chgPump.DesignFlowRate, OutdoorAir.DryBulbTemperature);
          wTank.ForecastState
            (ahp.WaterOutletTemperature, chgPump.DesignFlowRate * ActiveHeatSourceCount * (1 - rf), true);
          IsOverLoad_C = IsOverLoad_H = false;
        }
        //AHP off + no secondary side load (thermal storage tank heat loss only)
        else
        {
          ShutOff();
          wTank.ForecastState(0, 0, true);
          IsOverLoad_C = IsOverLoad_H = false;
        }
        aveTemp += pHex.SupplyTemperature * wTank.TimeStep;

        wTank.FixState();
        if (isLastCalc) break;
      }

      //When overloaded, output the average supply water temperature
      if (IsOverLoad_C) ChilledWaterSupplyTemperature = aveTemp / TimeStep;
      else ChilledWaterSupplyTemperature = ChilledWaterSupplyTemperatureSetpoint;
      if (IsOverLoad_H) HotWaterSupplyTemperature = aveTemp / TimeStep;
      else HotWaterSupplyTemperature = HotWaterSupplyTemperatureSetpoint;
    }

    /// <summary>Updates the heat exchanger heat transfer.</summary>
    /// <param name="hexFlow">Heat exchanger flow rate [kg/s].</param>
    /// <param name="rtnTmp">Chilled/hot water return temperature including pump heat gain [°C].</param>
    private void CalcHexHeatTransfer(double hexFlow, double rtnTmp)
    {
      double tAHPout = ahp.WaterOutletSetpointTemperature;

      for (int i = 0; i < 2; i++)
      {
        double chFlow = Math.Max(ChilledWaterFlowRate, HotWaterFlowRate);
        double hexFlowVol = 0.001 * hexFlow;
        rlsPump1.UpdateState(hexFlowVol);
        double dtRlsPump = rlsPump1.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * hexFlow);

        //Calculate the discharge heat exchanger//assume zero bypass flow rate
        double rAH = Math.Min(1, chgPump.DesignFlowRate * ActiveHeatSourceCount / rlsPump1.DesignFlowRate);
        double tHexIn = tAHPout * rAH + wTank.FirstTankTemperature * (1 - rAH);
        pHex.Update(tHexIn + dtRlsPump, rtnTmp, hexFlow, chFlow);

        //Determine the flow direction in the tank
        double rF = GetFirstTankWaterFlowRate(hexFlowVol, pHex.HeatSourceOutletTemperature, tAHPout);
        ahpBypass = ahp.WaterFlowRate * rF;
        double ahpFlw = (1 - rF) * chgPump.DesignFlowRate * ActiveHeatSourceCount;
        double fwdFlw = ahpFlw - hexFlowVol;
        double tH = 0 < fwdFlw ?
          (fwdFlw * wTank.LastTankTemperature + hexFlowVol * pHex.HeatSourceOutletTemperature) / (fwdFlw + hexFlowVol) :
          pHex.HeatSourceOutletTemperature;

        //Calculate the AHP with an assumed inlet water temperature
        double ahpIn = rF * tAHPout + (1 - rF) * tH + dtChgPump;
        ahp.Update(ahpIn, chgPump.DesignFlowRate * 1000, OutdoorAir.DryBulbTemperature);

        if (!ahp.IsOverLoad) break; //Finish if the AHP outlet temperature can be achieved.
        tAHPout = ahp.WaterOutletTemperature; //When overloaded, recalculate once more
      }
    }

    /// <summary>Computes the leading tank water flow ratio [-] that achieves the target heat source inlet temperature.</summary>
    /// <returns>Leading tank water flow ratio [-].</returns>
    /// <param name="hexFlow">Return water flow rate from the discharge heat exchanger [m³/s].</param>
    /// <param name="tHexRtn">Return water temperature from discharge heat exchanger [°C].</param>
    /// <param name="tAHPout">AHP outlet water temperature [°C].</param>
    private double GetFirstTankWaterFlowRate(double hexFlow, double tHexRtn, double tAHPout)
    {
      double rr = hexFlow / (chgPump.DesignFlowRate * ActiveHeatSourceCount);

      if (Mode == HeatSourceSystemModel.OperatingMode.Cooling)
      {
        double tSP = HeatSourceInletChilledWaterTemperatureSP - dtChgPump;
        //Case where the outlet temperature cannot satisfy the inlet temperature in the first place//this handling is problematic
        if (tSP < tAHPout) return 0.9;
        //When the HEX return water flow is large, the ratio is obtained immediately
        if (1.0 <= rr) return Math.Max(0, (tSP - tHexRtn) / (tAHPout - tHexRtn));
        //Case where the water temperature is controlled by mixing HEX return water, last tank, and first tank water
        double th = (1 - rr) * wTank.LastTankTemperature + rr * tHexRtn;
        if (th <= tSP) return 0.0; //Case where an inlet water temperature below the setpoint is achievable even without first tank water
        return (tSP - wTank.LastTankTemperature - rr * (tHexRtn - wTank.LastTankTemperature)) / (tAHPout - wTank.LastTankTemperature);
      }
      else
      {
        double tSP = HeatSourceInletHotWaterTemperatureSP - dtChgPump;
        //Case where the outlet temperature cannot satisfy the inlet temperature in the first place//this handling is problematic
        if (tAHPout < tSP) return 0.9;
        //When the HEX return water flow is large, the ratio is obtained immediately
        if (1.0 <= rr) return Math.Max(0, (tSP - tHexRtn) / (tAHPout - tHexRtn));
        //Case where the water temperature is controlled by mixing HEX return water, last tank, and first tank water
        double th = (1 - rr) * wTank.LastTankTemperature + rr * tHexRtn;
        if (tSP <= th) return 0.0; //Case where an inlet water temperature below the setpoint is achievable even without first tank water
        return (tSP - wTank.LastTankTemperature - rr * (tHexRtn - wTank.LastTankTemperature)) / (tAHPout - wTank.LastTankTemperature);
      }
    }

    /// <summary>Fixes (commits) the forecast state as the current state.</summary>
    public void FixState() { isForecasting = false; }

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance.</summary>
    /// <param name="waterTank">Multi-connected fully-mixed thermal storage tank.</param>
    /// <param name="plateHex">Plate heat exchanger.</param>
    /// <param name="ahp">Air-heat-source heat pump.</param>
    /// <param name="chgPump">Thermal storage charge pump.</param>
    /// <param name="rlsPump1">Primary discharge pump.</param>
    /// <param name="rlsPump2">Secondary discharge pump.</param>
    /// <param name="ashpCount">Number of air-heat-source heat pump units.</param>
    public MultiConnectedWaterTankSystem
      (MultiConnectedWaterTank waterTank, PlateHeatExchanger plateHex, AirHeatSourceModularChillers ahp,
      CentrifugalPump chgPump, CentrifugalPump rlsPump1, CentrifugalPump rlsPump2, int ashpCount)
    {
      this.ahp = ahp;
      this.chgPump = chgPump;
      this.rlsPump1 = rlsPump1;
      this.rlsPump2 = rlsPump2;
      this.pHex = plateHex;
      this.wTank = waterTank;
      this.HeatSourceCount = this.ActiveHeatSourceCount = ashpCount;
      oldTemps = new double[WaterTank.TankCount];

      //Calculate and store the temperature rise across the chiller pump
      this.chgPump.UpdateState(chgPump.DesignFlowRate);
      dtChgPump = this.chgPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * 1000 * this.chgPump.DesignFlowRate);

      Mode = HeatSourceSystemModel.OperatingMode.ShutOff;
    }

    #endregion

  }
}
