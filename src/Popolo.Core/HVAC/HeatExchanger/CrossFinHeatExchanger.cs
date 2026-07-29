/* CrossFinHeatExchanger.cs
 * 
 * Copyright (C) 2014 E.Togashi
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

using Popolo.Core.Physics;
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Cross-fin (plate-fin-and-tube) heat exchanger for air-water coils.</summary>
  public class CrossFinHeatExchanger : IReadOnlyCrossFinHeatExchanger
  {

    #region Enumeration definitions

    /// <summary>Water flow circuit type.</summary>
    public enum WaterFlowType
    {
      /// <summary>Half-flow circuit.</summary>
      HalfFlow,
      /// <summary>Single-flow circuit.</summary>
      SingleFlow,
      /// <summary>Double-flow circuit.</summary>
      DoubleFlow,
      /// <summary>Triple-flow circuit.</summary>
      TripleFlow
    }

    #endregion

    #region Instance variables

    /// <summary>True if the detailed geometric model is used.</summary>
    private readonly bool isDetailedModel;

    /// <summary>Coil specification for the detailed model.</summary>
    private readonly double airWaterSurfaceRatio, coreArea, equivalentFinRadius, equivalentDiameter,
      waterPath, finThickness, thermalConductivity, innerDiameter, outerDiameter;

    /// <summary>Coil specification for the simplified model.</summary>
    private readonly double ratedVelocity, ratedWaterSpeed;

    /// <summary>Heat transfer degradation factor [-].</summary>
    private double degradationFactor = 1.0;

    #endregion

    #region Properties

    /// <summary>Gets the relative humidity at the dry/wet boundary [%].</summary>
    public double BorderRelativeHumidity { get; private set; }

    /// <summary>Gets the maximum water flow rate [kg/s].</summary>
    public double MaxWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal water flow rate [kg/s].</summary>
    public double RatedWaterFlowRate { get; private set; }

    /// <summary>Gets the current water flow rate [kg/s].</summary>
    public double WaterFlowRate { get; private set; }

    /// <summary>Gets the current air flow rate [kg/s].</summary>
    public double AirFlowRate { get; private set; }

    /// <summary>Gets the nominal air flow rate [kg/s].</summary>
    public double RatedAirFlowRate { get; private set; }

    /// <summary>Gets the inlet air dry-bulb temperature [°C].</summary>
    public double InletAirTemperature { get; private set; }

    /// <summary>Gets the inlet air humidity ratio [kg/kg].</summary>
    public double InletAirHumidityRatio { get; private set; }

    /// <summary>Gets the outlet air dry-bulb temperature [°C].</summary>
    public double OutletAirTemperature { get; private set; }

    /// <summary>Gets the outlet air humidity ratio [kg/kg].</summary>
    public double OutletAirHumidityRatio { get; private set; }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    public double InletWaterTemperature { get; private set; }

    /// <summary>Gets the outlet water temperature [°C].</summary>
    public double OutletWaterTemperature { get; private set; }

    /// <summary>Gets the heat transfer surface area [m²].</summary>
    public double SurfaceArea { get; private set; }

    /// <summary>Gets the dry coil fraction [-].</summary>
    public double DryFraction { get; private set; }

    /// <summary>Gets the overall heat transfer coefficient for the dry coil [kW/(m²·K)].</summary>
    public double DryHeatTransferCoefficient { get; private set; }

    /// <summary>Gets the overall heat transfer coefficient for the wet coil [kW/(m²·(kJ/kg))].</summary>
    public double WetHeatTransferCoefficient { get; private set; }

    /// <summary>Gets or sets the surface area correction factor [-].</summary>
    public double CorrectionFactor { get; set; }

    /// <summary>Gets the heat transfer rate [kW] from the water-side perspective.</summary>
    /// <remarks>
    /// Computed as (OutletWaterTemperature - InletWaterTemperature) × WaterFlowRate × cp.
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <b>Cooling coil</b>: water is heated by the air → positive value.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <b>Heating coil</b>: water is cooled by releasing heat to the air → negative value.
    ///     </description>
    ///   </item>
    /// </list>
    /// To obtain the magnitude of heating delivered to the air, use
    /// <c>Math.Abs(HeatTransfer)</c>.
    /// </remarks>
    public double HeatTransfer
    {
      get
      {
        return (OutletWaterTemperature - InletWaterTemperature) * WaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      }
    }

    /// <summary>Gets or sets the heat transfer degradation factor [-].</summary>
    public double DegradationFactor
    {
      get { return degradationFactor; }
      set { degradationFactor = Math.Max(0.0001, Math.Min(1.0, value)); }
    }

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance using the detailed geometric coil model.</summary>
    /// <param name="depth">Coil depth [m].</param>
    /// <param name="width">Coil width [m].</param>
    /// <param name="height">Coil height [m].</param>
    /// <param name="rowCount">Total number of tube columns (perpendicular to air flow).</param>
    /// <param name="columnCount">Total number of tube rows in the air-flow direction.</param>
    /// <param name="finPitch">Fin pitch [m].</param>
    /// <param name="finThickness">Fin thickness [m].</param>
    /// <param name="thermalConductivity">Thermal conductivity of the fin material [W/(m·K)].</param>
    /// <param name="innerDiameter">Tube inner diameter [m].</param>
    /// <param name="outerDiameter">Tube outer diameter [m].</param>
    /// <param name="ratedAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="ratedInletAirTemperature">Nominal inlet air dry-bulb temperature [°C].</param>
    /// <param name="ratedInletAirHumidityRatio">Nominal inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="ratedWaterFlowRate">Nominal water flow rate [kg/s].</param>
    /// <param name="maxWaterFlowRate">Maximum water flow rate [kg/s].</param>
    /// <param name="ratedInletWaterTemperature">Nominal inlet water temperature [°C].</param>
    /// <param name="flowType">Water flow circuit type.</param>
    /// <param name="heatTransfer">Rated heat transfer capacity [kW].</param>
    /// <param name="useCorrectionFactor">True to apply the correction factor to the heat transfer coefficients.</param>
    public CrossFinHeatExchanger(double depth, double width, double height, int rowCount, int columnCount,
      double finPitch, double finThickness, double thermalConductivity, double innerDiameter,
      double outerDiameter, double ratedAirFlowRate, double ratedInletAirTemperature,
      double ratedInletAirHumidityRatio, double borderRelativeHumidity, double ratedWaterFlowRate,
      double maxWaterFlowRate, double ratedInletWaterTemperature, WaterFlowType flowType,
      double heatTransfer, bool useCorrectionFactor)
      : this(depth, width, height, rowCount, columnCount, finPitch, finThickness, thermalConductivity,
          innerDiameter, outerDiameter, ratedAirFlowRate, ratedInletAirTemperature,
          ratedInletAirHumidityRatio, borderRelativeHumidity, ratedWaterFlowRate, maxWaterFlowRate,
          ratedInletWaterTemperature, GetFlowFactor(flowType), heatTransfer, useCorrectionFactor)
    { }

    /// <summary>Initializes a new instance using the detailed model with automatic UA estimation.</summary>
    /// <param name="width">Coil width [m].</param>
    /// <param name="height">Coil height [m].</param>
    /// <param name="rowCount">Total number of tube columns (perpendicular to air flow).</param>
    /// <param name="columnount">Total number of tube rows in the air-flow direction.</param>
    /// <param name="ratedAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="ratedInletAirTemperature">Nominal inlet air dry-bulb temperature [°C].</param>
    /// <param name="ratedInletAirHumidityRatio">Nominal inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="ratedWaterFlowRate">Nominal water flow rate [kg/s].</param>
    /// <param name="maxWaterFlowRate">Maximum water flow rate [kg/s].</param>
    /// <param name="ratedInletWaterTemperature">Nominal inlet water temperature [°C].</param>
    /// <param name="flowType">Water flow circuit type.</param>
    /// <param name="heatTransfer">Rated heat transfer capacity [kW].</param>
    /// <param name="useCorrectionFactor">True to apply the correction factor to the heat transfer coefficients.</param>
    public CrossFinHeatExchanger(
      double width, double height, int rowCount, int columnount, double ratedAirFlowRate,
      double ratedInletAirTemperature, double ratedInletAirHumidityRatio, double borderRelativeHumidity,
      double ratedWaterFlowRate, double maxWaterFlowRate, double ratedInletWaterTemperature,
      WaterFlowType flowType, double heatTransfer, bool useCorrectionFactor)
      : this(rowCount * 0.0329, width, height, rowCount, columnount, 0.0029, 0.0002, 237, 0.0146, 0.0158,
          ratedAirFlowRate, ratedInletAirTemperature, ratedInletAirHumidityRatio, borderRelativeHumidity,
          ratedWaterFlowRate, maxWaterFlowRate, ratedInletWaterTemperature, GetFlowFactor(flowType),
          heatTransfer, useCorrectionFactor)
    { }

    /// <summary>Computes the flow factor from the water flow circuit type.</summary>
    /// <param name="wType">Water flow circuit type.</param>
    /// <returns>Flow factor [-].</returns>
    private static double GetFlowFactor(WaterFlowType wType)
    {
      if (wType == WaterFlowType.HalfFlow) return 0.5;
      else if (wType == WaterFlowType.SingleFlow) return 1.0;
      else if (wType == WaterFlowType.DoubleFlow) return 2.0;
      else return 3.0;
    }

    /// <summary>Initializes a new instance using the detailed geometric coil model.</summary>
    /// <param name="depth">Coil depth [m].</param>
    /// <param name="width">Coil width [m].</param>
    /// <param name="height">Coil height [m].</param>
    /// <param name="rowCount">Total number of tube columns (perpendicular to air flow).</param>
    /// <param name="columnCount">Total number of tube rows in the air-flow direction.</param>
    /// <param name="finPitch">Fin pitch [m].</param>
    /// <param name="finThickness">Fin thickness [m].</param>
    /// <param name="thermalConductivity">Thermal conductivity of the fin material [W/(m·K)].</param>
    /// <param name="innerDiameter">Tube inner diameter [m].</param>
    /// <param name="outerDiameter">Tube outer diameter [m].</param>
    /// <param name="ratedAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="ratedInletAirTemperature">Nominal inlet air dry-bulb temperature [°C].</param>
    /// <param name="ratedInletAirHumidityRatio">Nominal inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="ratedWaterFlowRate">Nominal water flow rate [kg/s].</param>
    /// <param name="maxWaterFlowRate">Maximum water flow rate [kg/s].</param>
    /// <param name="ratedInletWaterTemperature">Nominal inlet water temperature [°C].</param>
    /// <param name="flowFactor">Flow factor [-].</param>
    /// <param name="heatTransfer">Rated heat transfer capacity [kW].</param>
    /// <param name="useCorrectionFactor">True to apply the correction factor to the heat transfer coefficients.</param>
    public CrossFinHeatExchanger(double depth, double width, double height, int rowCount, int columnCount,
      double finPitch, double finThickness, double thermalConductivity, double innerDiameter,
      double outerDiameter, double ratedAirFlowRate, double ratedInletAirTemperature,
      double ratedInletAirHumidityRatio, double borderRelativeHumidity, double ratedWaterFlowRate,
      double maxWaterFlowRate, double ratedInletWaterTemperature, double flowFactor, double heatTransfer,
      bool useCorrectionFactor)
    {
      //Initialize with the detailed model
      isDetailedModel = true;

      //Compute the coil geometry
      double asr, car, eqr, eqd, asa;
      GetGeometricCompfigulation(depth, width, height, rowCount, columnCount, finPitch, finThickness,
        innerDiameter, outerDiameter, out asr, out car, out eqr, out eqd, out asa);

      //Store the coil specification
      this.airWaterSurfaceRatio = asr;
      this.coreArea = car;
      this.equivalentFinRadius = eqr;
      this.equivalentDiameter = eqd;
      this.waterPath = flowFactor * columnCount;
      this.finThickness = finThickness;
      this.thermalConductivity = thermalConductivity;
      this.innerDiameter = innerDiameter;
      this.outerDiameter = outerDiameter;

      //Store the other coil specifications
      this.RatedAirFlowRate = ratedAirFlowRate;
      this.RatedWaterFlowRate = ratedWaterFlowRate;
      this.MaxWaterFlowRate = maxWaterFlowRate;

      //Store the air relative humidity at the dry/wet boundary
      this.BorderRelativeHumidity = borderRelativeHumidity;

      if (useCorrectionFactor)
      {
        //Compute the overall heat transfer coefficients
        double kd, kw;
        GetHeatTransferCoefficient(airWaterSurfaceRatio, coreArea, equivalentFinRadius, equivalentDiameter,
          waterPath, finThickness, thermalConductivity, innerDiameter, outerDiameter, RatedAirFlowRate,
          ratedInletAirTemperature, ratedInletAirHumidityRatio, borderRelativeHumidity, RatedWaterFlowRate,
          ratedInletWaterTemperature, out kd, out kw);
        DryHeatTransferCoefficient = kd;
        WetHeatTransferCoefficient = kw;

        //Obtain the heat transfer surface area [m2]
        SurfaceArea = GetSurfaceArea(ratedInletAirTemperature, ratedInletAirHumidityRatio,
          borderRelativeHumidity, ratedInletWaterTemperature, ratedAirFlowRate, ratedWaterFlowRate,
          heatTransfer, kd, kw);
        CorrectionFactor = SurfaceArea / (asa * rowCount);
      }
      else
      {
        SurfaceArea = asa * rowCount;
        CorrectionFactor = 1.0d;
      }
    }

    /// <summary>Initializes a new instance using the simplified coil model.</summary>
    /// <param name="ratedAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="ratedVelocity">Nominal face velocity [m/s].</param>
    /// <param name="ratedInletAirTemperature">Nominal inlet air dry-bulb temperature [°C].</param>
    /// <param name="ratedInletAirHumidityRatio">Nominal inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Dry/wet boundary relative humidity [%].</param>
    /// <param name="ratedWaterFlowRate">Nominal water flow rate [kg/s].</param>
    /// <param name="ratedWaterSpeed">Nominal water velocity inside tubes [m/s].</param>
    /// <param name="maxWaterFlowRate">Maximum water flow rate [kg/s].</param>
    /// <param name="ratedInletWaterTemperature">Nominal inlet water temperature [°C].</param>
    /// <param name="heatTransfer">Rated heat transfer capacity [kW].</param>
    public CrossFinHeatExchanger(double ratedAirFlowRate, double ratedVelocity,
      double ratedInletAirTemperature, double ratedInletAirHumidityRatio, double borderRelativeHumidity,
      double ratedWaterFlowRate, double ratedWaterSpeed, double maxWaterFlowRate,
      double ratedInletWaterTemperature, double heatTransfer)
    {
      //Initialize with the simplified model
      isDetailedModel = false;

      //Store the coil specification for the simplified model
      this.ratedVelocity = ratedVelocity;
      this.ratedWaterSpeed = ratedWaterSpeed;

      //Store the other coil specifications
      this.RatedAirFlowRate = ratedAirFlowRate;
      this.RatedWaterFlowRate = ratedWaterFlowRate;
      this.MaxWaterFlowRate = maxWaterFlowRate;

      //Store the air relative humidity at the dry/wet boundary
      this.BorderRelativeHumidity = borderRelativeHumidity;

      //Compute the overall heat transfer coefficients
      double kd, kw;
      GetHeatTransferCoefficient(ratedWaterSpeed, ratedVelocity, out kd, out kw);
      DryHeatTransferCoefficient = kd;
      WetHeatTransferCoefficient = kw;

      //Obtain the heat transfer surface area [m2]
      SurfaceArea = GetSurfaceArea(ratedInletAirTemperature, ratedInletAirHumidityRatio,
        borderRelativeHumidity, ratedInletWaterTemperature, ratedAirFlowRate, ratedWaterFlowRate,
        heatTransfer, kd, kw);
    }

    #endregion

    #region Instance methods

    /// <summary>Computes the outlet air and water states for the given inlet conditions.</summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    public void UpdateOutletState(double inletAirTemperature, double inletAirHumidityRatio,
      double inletWaterTemperature, double airFlowRate, double waterFlowRate)
    {
      //Store the input values
      InletAirTemperature = inletAirTemperature;
      InletAirHumidityRatio = inletAirHumidityRatio;
      InletWaterTemperature = inletWaterTemperature;
      AirFlowRate = airFlowRate;
      WaterFlowRate = waterFlowRate;

      //If no fluid is flowing
      if (AirFlowRate <= 0 || WaterFlowRate <= 0)
      {
        OutletAirTemperature = InletAirTemperature;
        OutletAirHumidityRatio = InletAirHumidityRatio;
        OutletWaterTemperature = InletWaterTemperature;
        DryFraction = 1.0;
        return;
      }

      //Compute the overall heat transfer coefficients
      if (isDetailedModel)
      {
        //Detailed model
        double kd, kw;
        GetHeatTransferCoefficient(airWaterSurfaceRatio, coreArea, equivalentFinRadius, equivalentDiameter,
          waterPath, finThickness, thermalConductivity, innerDiameter, outerDiameter, AirFlowRate,
          InletAirTemperature, InletAirHumidityRatio, BorderRelativeHumidity, WaterFlowRate,
          InletWaterTemperature, out kd, out kw);
        DryHeatTransferCoefficient = kd;
        WetHeatTransferCoefficient = kw;
      }
      else
      {
        //Simplified model
        double kd, kw;
        //Compute water and air velocities//proportional to air and water flow rates
        double velocity = (AirFlowRate / RatedAirFlowRate) * ratedVelocity;
        double waterSpeed = (WaterFlowRate / RatedWaterFlowRate) * ratedWaterSpeed;
        GetHeatTransferCoefficient(waterSpeed, velocity, out kd, out kw);
        DryHeatTransferCoefficient = kd;
        WetHeatTransferCoefficient = kw;
      }

      //Compute the outlet state
      double ta, xa, tw, dr;
      GetOutletState(InletAirTemperature, InletAirHumidityRatio, BorderRelativeHumidity,
        InletWaterTemperature, AirFlowRate, WaterFlowRate, DryHeatTransferCoefficient,
        WetHeatTransferCoefficient, SurfaceArea * DegradationFactor, out ta, out xa, out tw, out dr);
      OutletAirTemperature = ta;
      OutletAirHumidityRatio = xa;
      OutletWaterTemperature = tw;
      DryFraction = dr;
    }

    /// <summary>Controls the outlet air temperature to the given setpoint by adjusting the water flow rate.</summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="outletAirTemperatureSetpoint">Outlet air dry-bulb temperature setpoint [°C].</param>
    /// <returns>True if control to the setpoint is achievable; false if overloaded.</returns>
    public bool ControlOutletAirTemperature(double inletAirTemperature, double inletAirHumidityRatio,
      double inletWaterTemperature, double airFlowRate, double outletAirTemperatureSetpoint)
    {
      //Store the input values
      InletAirTemperature = inletAirTemperature;
      InletAirHumidityRatio = inletAirHumidityRatio;
      InletWaterTemperature = inletWaterTemperature;
      AirFlowRate = airFlowRate;

      //Determine cooling or heating mode
      bool isCooling = (inletWaterTemperature < inletAirTemperature);

      //If neither cooling nor heating is needed
      if (isCooling && inletAirTemperature < outletAirTemperatureSetpoint + 1e-3 ||
        !isCooling && outletAirTemperatureSetpoint < inletAirTemperature + 1e-3)
      {
        ShutOff();
        return false;
      }

      //Compute the uncontrolled outlet temperature at the maximum water flow rate
      UpdateOutletState
        (InletAirTemperature, InletAirHumidityRatio, InletWaterTemperature, AirFlowRate, MaxWaterFlowRate);

      //If overloaded, output the uncontrolled state at maximum capacity
      if ((isCooling && (outletAirTemperatureSetpoint < OutletAirTemperature))
        || (!isCooling && (OutletAirTemperature < outletAirTemperatureSetpoint)))
        return false;

      //If the load can be handled, iterate on the water flow rate with Brent's method
      //Define the error function
      Roots.ErrorFunction eFnc = delegate (double wFlow)
      {
        UpdateOutletState
        (InletAirTemperature, InletAirHumidityRatio, InletWaterTemperature, AirFlowRate, wFlow);
        return outletAirTemperatureSetpoint - OutletAirTemperature;
      };
      double wf = Roots.Brent(0, MaxWaterFlowRate, MaxWaterFlowRate * 0.001, eFnc);
      UpdateOutletState(InletAirTemperature, InletAirHumidityRatio, InletWaterTemperature, AirFlowRate, wf);
      OutletAirTemperature = outletAirTemperatureSetpoint;
      return true;
    }

    /// <summary>Shuts off the heat exchanger.</summary>
    public void ShutOff()
    {
      OutletAirTemperature = InletAirTemperature;
      OutletAirHumidityRatio = InletAirHumidityRatio;
      OutletWaterTemperature = InletWaterTemperature;
      WaterFlowRate = 0;
      DryFraction = 1;
    }

    #endregion

    #region Static methods

    /// <summary>Computes the outlet air and water states for the given inlet conditions.</summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="dryHeatTransferCoefficient">Overall heat transfer coefficient for the dry section [kW/(m²·K)].</param>
    /// <param name="wetHeatTransferCoefficient">Overall heat transfer coefficient for the wet section [kW/(m²·(kJ/kg))].</param>
    /// <param name="surfaceArea">Surface area [m²].</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="outletWaterTemperature">Output: outlet water temperature [°C].</param>
    /// <param name="dryFraction">Output: dry coil fraction [-].</param>
    public static void GetOutletState
      (double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity,
      double inletWaterTemperature, double airFlowRate, double waterFlowRate,
      double dryHeatTransferCoefficient, double wetHeatTransferCoefficient, double surfaceArea,
      out double outletAirTemperature, out double outletAirHumidityRatio,
      out double outletWaterTemperature, out double dryFraction)
    {
      //If either fluid flow rate is zero, outlet state = inlet state
      if (airFlowRate <= 0 || waterFlowRate <= 0 ||
        inletWaterTemperature == inletAirTemperature)
      {
        outletAirTemperature = inletAirTemperature;
        outletAirHumidityRatio = inletAirHumidityRatio;
        outletWaterTemperature = inletWaterTemperature;
        dryFraction = 1;
        return;
      }

      //Compute the heat capacity flow rates of water and moist air [kW/s]
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = airFlowRate * cpma;
      double mcw = waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;

      //Heating coil case
      if (inletAirTemperature < inletWaterTemperature)
      {
        dryFraction = 1.0;

        double mcMin = Math.Min(mcw, mca);
        double mcMax = Math.Max(mcw, mca);
        double ntu = dryHeatTransferCoefficient * surfaceArea / mcMin;

        //Compute the counter-flow heat transfer effectiveness [-]
        double eff = HeatExchange.GetEffectiveness(ntu, mcMin / mcMax, HeatExchange.FlowType.CounterFlow);

        //Compute the heat exchange rate and outlet state
        double q = eff * mcMin * (inletWaterTemperature - inletAirTemperature);
        outletAirTemperature = inletAirTemperature + q / mca;
        outletWaterTemperature = inletWaterTemperature - q / mcw;
        outletAirHumidityRatio = inletAirHumidityRatio;
      }
      //Cooling coil case
      else
      {
        //Compute the air temperature at the dry/wet boundary [C]
        double ba = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndRelativeHumidity
          (inletAirHumidityRatio, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);

        //Compute the inlet air enthalpy [kJ/kg]
        double iAirEnthalpy = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
          (inletAirTemperature, inletAirHumidityRatio);

        //Compute the enthalpy approximation coefficients
        double a, b;
        GetSaturationEnthalpyCoefficients(inletWaterTemperature, out a, out b);

        double xd = 1 / mca;
        double yd = -1 / mcw;
        double xw = 1 / airFlowRate;
        double yw = -a / mcw;

        double v2, v3, v4, bWaterTemp, bAirTemp;
        v2 = v3 = v4 = bWaterTemp = bAirTemp = 0;

        Roots.ErrorFunction eFnc = delegate (double dRate)
        {
          double zd = Math.Exp(dryHeatTransferCoefficient * surfaceArea * dRate * (xd + yd));
          double wd = zd * xd + yd;
          double v1 = xd * (zd - 1) / wd;
          v2 = zd * (xd + yd) / wd;

          double zw = Math.Exp(wetHeatTransferCoefficient * surfaceArea * (1 - dRate) * (xw + yw));
          double ww = zw * xw + yw;
          v3 = (xw + yw) / ww;
          v4 = xw * (zw - 1) / ww;
          double v5 = zw * (xw + yw) / ww;
          double v6 = yw * (1 - zw) / ww / a;

          //Compute the water temperature at the dry/wet boundary [C]
          bWaterTemp = (v5 * inletWaterTemperature
          + v6 * (iAirEnthalpy - v1 * cpma * inletAirTemperature - b)) / (1 - v1 * v6 * cpma);
          //Compute the air state at the dry/wet boundary
          bAirTemp = inletAirTemperature - v1 * (inletAirTemperature - bWaterTemp);

          //Evaluate the error
          return ba - bAirTemp;
        };
        //If condensation occurs, iterate on the dry coil area fraction
        dryFraction = 1.0;
        if (0 < eFnc(dryFraction)) dryFraction = Roots.Brent(0, 1, 0.0001, eFnc);

        //Compute the outlet water temperature [C]
        outletWaterTemperature = inletAirTemperature - v2 * (inletAirTemperature - bWaterTemp);
        double bAirEnthalpy = cpma * (bAirTemp - inletAirTemperature) + iAirEnthalpy;
        //Compute the outlet air state
        double iWaterEnthalpy = a * inletWaterTemperature + b;
        double oAirEnthalpy = v3 * bAirEnthalpy + v4 * iWaterEnthalpy;
        if (dryFraction < 1.0)
          outletAirHumidityRatio = MoistAir.GetHumidityRatioFromEnthalpyAndRelativeHumidity
            (oAirEnthalpy, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        else outletAirHumidityRatio = inletAirHumidityRatio;
        outletAirTemperature = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndEnthalpy
          (outletAirHumidityRatio, oAirEnthalpy);
      }
    }

    /// <summary>Computes the required water flow rate [kg/s] to achieve the target outlet air temperature.</summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="velocity">Air face velocity [m/s].</param>
    /// <param name="ratedWaterSpeed">Nominal water velocity inside tubes [m/s].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="ratedWaterFlowRate">Nominal water flow rate [kg/s].</param>
    /// <param name="maxWaterFlowRate">Maximum water flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="outletAirTemperatureSetpoint">Outlet air dry-bulb temperature setpoint [°C].</param>
    /// <returns>Required water flow rate [kg/s].</returns>
    public static double GetWaterFlowRate(
      double inletAirTemperature, double inletAirHumidityRatio,
      double borderRelativeHumidity, double inletWaterTemperature,
      double velocity, double ratedWaterSpeed,
      double airFlowRate, double ratedWaterFlowRate, double maxWaterFlowRate,
      double surfaceArea, double outletAirTemperatureSetpoint)
    {
      //Determine cooling or heating mode
      bool isCooling = (inletWaterTemperature < inletAirTemperature);

      //If neither cooling nor heating is needed
      if (isCooling && inletAirTemperature < outletAirTemperatureSetpoint
        || !isCooling && outletAirTemperatureSetpoint < inletAirTemperature) return 0;

      double wc = ratedWaterSpeed / ratedWaterFlowRate;

      //Compute the uncontrolled outlet temperature at the maximum water flow rate
      double oat, oah, owt, dr, kd, kw;
      GetHeatTransferCoefficient(wc * maxWaterFlowRate, velocity, out kd, out kw);
      GetOutletState(inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
        inletWaterTemperature, airFlowRate, maxWaterFlowRate, kd, kw, surfaceArea,
        out oat, out oah, out owt, out dr);

      //If overloaded, return the maximum water flow rate
      if ((isCooling && (outletAirTemperatureSetpoint < oat))
        || (!isCooling && (oat < outletAirTemperatureSetpoint)))
        return maxWaterFlowRate;

      //If the load can be handled, iterate on the water flow rate with Brent's method
      //Define the error function
      Roots.ErrorFunction eFnc = delegate (double wFlow)
      {
        GetHeatTransferCoefficient(wc * wFlow, velocity, out kd, out kw);
        GetOutletState(inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
          inletWaterTemperature, airFlowRate, wFlow, kd, kw, surfaceArea,
          out oat, out oah, out owt, out dr);
        return outletAirTemperatureSetpoint - oat;
      };
      return Roots.Brent(0, maxWaterFlowRate, 0.01, eFnc);
    }

    /// <summary>Computes the required water flow rate [kg/s] to achieve the target outlet air temperature.</summary>
    /// <param name="airWaterSurfaceRatio">Air-side to water-side surface area ratio [-].</param>
    /// <param name="coreArea">Coil face area [m²].</param>
    /// <param name="equivalentFinRadius">Equivalent annular fin outer radius [m].</param>
    /// <param name="equivalentDiameter">Equivalent hydraulic diameter [m].</param>
    /// <param name="waterPath">Number of parallel water flow paths [-].</param>
    /// <param name="finThickness">Fin thickness [m].</param>
    /// <param name="thermalConductivity">Thermal conductivity of the fin material [W/(m·K)].</param>
    /// <param name="innerDiameter">Tube inner diameter [m].</param>
    /// <param name="outerDiameter">Tube outer diameter [m].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="maxWaterFlowRate">Maximum water flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="outletAirTemperatureSetpoint">Outlet air dry-bulb temperature setpoint [°C].</param>
    /// <returns>Required water flow rate [kg/s].</returns>
    public static double GetWaterFlowRate(
      double airWaterSurfaceRatio, double coreArea, double equivalentFinRadius,
      double equivalentDiameter, double waterPath, double finThickness,
      double thermalConductivity, double innerDiameter, double outerDiameter,
      double airFlowRate, double inletAirTemperature, double inletAirHumidityRatio,
      double borderRelativeHumidity, double waterFlowRate, double inletWaterTemperature,
      double maxWaterFlowRate, double surfaceArea, double outletAirTemperatureSetpoint)
    {
      //Determine cooling or heating mode
      bool isCooling = (inletWaterTemperature < inletAirTemperature);

      //If neither cooling nor heating is needed
      if (isCooling && inletAirTemperature < outletAirTemperatureSetpoint
        || !isCooling && outletAirTemperatureSetpoint < inletAirTemperature) return 0;

      //Compute the uncontrolled outlet temperature at the maximum water flow rate
      double oat, oah, owt, dr, kd, kw;
      GetHeatTransferCoefficient(airWaterSurfaceRatio, coreArea, equivalentFinRadius,
        equivalentDiameter, waterPath, finThickness, thermalConductivity,
        innerDiameter, outerDiameter, airFlowRate, inletAirHumidityRatio,
        inletAirHumidityRatio, borderRelativeHumidity, maxWaterFlowRate,
        inletWaterTemperature, out kd, out kw);
      GetOutletState(inletAirTemperature, inletAirHumidityRatio,
        borderRelativeHumidity, inletWaterTemperature,
        airFlowRate, maxWaterFlowRate, kd, kw, surfaceArea,
        out oat, out oah, out owt, out dr);

      //If overloaded, return the maximum water flow rate
      if ((isCooling && (outletAirTemperatureSetpoint < oat))
        || (!isCooling && (oat < outletAirTemperatureSetpoint)))
        return maxWaterFlowRate;

      //If the load can be handled, iterate on the water flow rate with Brent's method
      //Define the error function
      Roots.ErrorFunction eFnc = delegate (double wFlow)
      {
        GetHeatTransferCoefficient(airWaterSurfaceRatio, coreArea, equivalentFinRadius,
          equivalentDiameter, waterPath, finThickness, thermalConductivity,
          innerDiameter, outerDiameter, airFlowRate, inletAirHumidityRatio,
          inletAirHumidityRatio, borderRelativeHumidity, wFlow,
          inletWaterTemperature, out kd, out kw);
        GetOutletState(inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
          inletWaterTemperature, airFlowRate, wFlow, kd, kw, surfaceArea,
          out oat, out oah, out owt, out dr);
        return outletAirTemperatureSetpoint - oat;
      };
      return Roots.Brent(0, maxWaterFlowRate, 0.01, eFnc);
    }

    /// <summary>Computes the linearisation coefficients for saturation enthalpy as a function of dry-bulb temperature.</summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="a">Coefficient for the dry-bulb temperature term.</param>
    /// <param name="b">Intercept of the linearised saturation enthalpy equation.</param>
    private static void GetSaturationEnthalpyCoefficients
      (double dryBulbTemperature, out double a, out double b)
    {
      const double DELTA = 0.001;
      double hws1 = MoistAir.GetSaturationEnthalpyFromDryBulbTemperature
        (dryBulbTemperature, PhysicsConstants.StandardAtmosphericPressure);
      double hws2 = MoistAir.GetSaturationEnthalpyFromDryBulbTemperature
        (dryBulbTemperature + DELTA, PhysicsConstants.StandardAtmosphericPressure);
      a = (hws2 - hws1) / DELTA;
      b = hws1 - a * dryBulbTemperature;
    }

    /// <summary>Computes the air-side heat transfer surface area [m²].</summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Dry/wet boundary relative humidity [%].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="heatTransfer">Heat transfer capacity [kW].</param>
    /// <param name="dryHeatTransferCoefficient">Overall heat transfer coefficient for the dry section [kW/(m²·K)].</param>
    /// <param name="wetHeatTransferCoefficient">Overall heat transfer coefficient for the wet section [kW/(m²·(kJ/kg))].</param>
    /// <returns>Air-side heat transfer surface area [m²].</returns>
    public static double GetSurfaceArea
      (double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity,
      double inletWaterTemperature, double airFlowRate, double waterFlowRate, double heatTransfer,
      double dryHeatTransferCoefficient, double wetHeatTransferCoefficient)
    {

      //Compute the heat capacity flow rates of water and moist air [kW/s]
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = airFlowRate * cpma;
      double mcw = waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;

      //Heating coil case
      if (inletAirTemperature < inletWaterTemperature)
      {
        //Compute the NTU value
        double mcMin = Math.Min(mcw, mca);
        double mcMax = Math.Max(mcw, mca);

        //Compute the heat transfer effectiveness [-]
        double eff = heatTransfer / mcMin / (inletWaterTemperature - inletAirTemperature);
        double ntu = HeatExchange.GetNTU(eff, mcMin / mcMax, HeatExchange.FlowType.CounterFlow);

        return ntu * mcMin / dryHeatTransferCoefficient;
      }
      //Cooling coil case
      else
      {
        //Compute the chilled water outlet temperature [C]
        double oWaterTemp = inletWaterTemperature + heatTransfer / mcw;

        //Compute the inlet and outlet air enthalpies [kJ/kg]
        double iAirEnthalpy = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
          (inletAirTemperature, inletAirHumidityRatio);
        double oAirEnthalpy = iAirEnthalpy - heatTransfer / airFlowRate;

        //Compute the dry/wet boundary of the coil
        double oAirHumidRatio = MoistAir.GetHumidityRatioFromEnthalpyAndRelativeHumidity
          (oAirEnthalpy, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);

        //Fully dry coil case
        if (inletAirHumidityRatio < oAirHumidRatio)
        {
          double oAirTemp = inletAirTemperature - heatTransfer / mca;
          double d1 = inletAirTemperature - oWaterTemp;
          double d2 = oAirTemp - inletWaterTemperature;
          double lmtd = (d1 - d2) / Math.Log(d1 / d2);
          return heatTransfer / lmtd / dryHeatTransferCoefficient;
        }
        //Dry + wet coil case
        else
        {
          //Compute the moist air state at the boundary point
          double bAirTemp =
            MoistAir.GetDryBulbTemperatureFromHumidityRatioAndRelativeHumidity
            (inletAirHumidityRatio, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
          double bAirEnthalpy =
            MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(bAirTemp, inletAirHumidityRatio);

          //Compute the water temperature at the boundary point [C]
          double htWet = (bAirEnthalpy - oAirEnthalpy) * airFlowRate;
          double bWaterTemp = inletWaterTemperature + htWet / mcw;

          //Compute the saturation enthalpy of air at the water temperature [kJ/(kg)]
          double iWaterEnthalpy =
            MoistAir.GetSaturationEnthalpyFromDryBulbTemperature(inletWaterTemperature, PhysicsConstants.StandardAtmosphericPressure);
          double bWaterEnthalpy =
            MoistAir.GetSaturationEnthalpyFromDryBulbTemperature(bWaterTemp, PhysicsConstants.StandardAtmosphericPressure);

          //Compute the dry coil surface area [m2]
          double dt1 = inletAirTemperature - oWaterTemp;
          double dt2 = bAirTemp - bWaterTemp;
          double lmtd = (dt1 - dt2) / Math.Log(dt1 / dt2);
          double sd = (heatTransfer - htWet) / lmtd / dryHeatTransferCoefficient;

          double dh1 = bAirEnthalpy - bWaterEnthalpy;
          double dh2 = oAirEnthalpy - iWaterEnthalpy;
          double lmhd = (dh1 - dh2) / Math.Log(dh1 / dh2);
          double sw = htWet / lmhd / wetHeatTransferCoefficient;

          return sd + sw;
        }
      }
    }

    /// <summary>Computes the overall heat transfer coefficients (dry and wet).</summary>
    /// <param name="waterSpeed">Water velocity inside tubes [m/s].</param>
    /// <param name="velocity">Face velocity [m/s].</param>
    /// <param name="dryHeatTransferCoefficient">Overall heat transfer coefficient for the dry section [kW/(m²·K)].</param>
    /// <param name="wetHeatTransferCoefficient">Overall heat transfer coefficient for the wet section [kW/(m²·(kJ/kg))].</param>
    public static void GetHeatTransferCoefficient
      (double waterSpeed, double velocity,
      out double dryHeatTransferCoefficient, out double wetHeatTransferCoefficient)
    {
      dryHeatTransferCoefficient =
        1 / (4.72 + 4.91 * Math.Pow(waterSpeed, -0.8) + 26.7 * Math.Pow(velocity, -0.64));
      wetHeatTransferCoefficient =
        1 / (10.044 + 10.44 * Math.Pow(waterSpeed, -0.8) + 39.6 * Math.Pow(velocity, -0.64));
    }

    /// <summary>Computes the coil geometry (surface areas, diameters, fin efficiency).</summary>
    /// <param name="depth">Coil depth [m].</param>
    /// <param name="width">Coil width [m].</param>
    /// <param name="height">Coil height [m].</param>
    /// <param name="rowCount">Total number of tube columns (perpendicular to air flow).</param>
    /// <param name="columnCount">Total number of tube rows in the air-flow direction.</param>
    /// <param name="finPitch">Fin pitch [m].</param>
    /// <param name="finThickness">Fin thickness [m].</param>
    /// <param name="innerDiameter">Tube inner diameter [m].</param>
    /// <param name="outerDiameter">Tube outer diameter [m].</param>
    /// <param name="airWaterSurfaceRatio">Air-side to water-side surface area ratio [-].</param>
    /// <param name="coreArea">Coil face area [m²].</param>
    /// <param name="equivalentFinRadius">Equivalent annular fin outer radius [m].</param>
    /// <param name="equivalentDiameter">Equivalent hydraulic diameter [m].</param>
    /// <param name="surfaceArea">Air-side heat transfer surface area [m²].</param>
    public static void GetGeometricCompfigulation
      (double depth, double width, double height, int rowCount, int columnCount,
      double finPitch, double finThickness, double innerDiameter, double outerDiameter,
      out double airWaterSurfaceRatio, out double coreArea, out double equivalentFinRadius,
      out double equivalentDiameter, out double surfaceArea)
    {
      //Compute the air-side heat transfer surface area [m2]
      double sf = 2 * (height * depth / rowCount 
        - outerDiameter * outerDiameter / 4 * Math.PI * columnCount) * width / finPitch;
      double sto = outerDiameter * Math.PI * columnCount * width * (1 - finThickness / finPitch);
      surfaceArea = sf + sto;

      //Compute the water-side heat transfer surface area [m2]
      double wSurface = innerDiameter * Math.PI * columnCount * width;

      //Compute the air-side to water-side heat transfer surface area ratio [-]
      airWaterSurfaceRatio = surfaceArea / wSurface;

      //Compute the core area [m2]
      coreArea = (width * height - outerDiameter * width * columnCount) * (1 - finThickness / finPitch);

      //Compute the equivalent radius of the annular fin [m]
      equivalentFinRadius = Math.Sqrt((depth / rowCount) * (height / columnCount) / Math.PI);

      //Compute the equivalent diameter [m]
      equivalentDiameter = 4 * coreArea / (surfaceArea * rowCount / depth);
    }

    /// <summary>Computes the overall heat transfer coefficients (dry and wet).</summary>
    /// <param name="airWaterSurfaceRatio">Air-side to water-side surface area ratio [-].</param>
    /// <param name="coreArea">Coil face area [m²].</param>
    /// <param name="equivalentFinRadius">Equivalent annular fin outer radius [m].</param>
    /// <param name="equivalentDiameter">Equivalent hydraulic diameter [m].</param>
    /// <param name="waterPath">Number of parallel water flow paths [-].</param>
    /// <param name="finThickness">Fin thickness [m].</param>
    /// <param name="thermalConductivity">Thermal conductivity of the fin material [W/(m·K)].</param>
    /// <param name="innerDiameter">Tube inner diameter [m].</param>
    /// <param name="outerDiameter">Tube outer diameter [m].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="dryHeatTransferCoefficient">Overall heat transfer coefficient for the dry section [kW/(m²·K)].</param>
    /// <param name="wetHeatTransferCoefficient">Overall heat transfer coefficient for the wet section [kW/(m²·(kJ/kg))].</param>
    public static void GetHeatTransferCoefficient
      (double airWaterSurfaceRatio, double coreArea, double equivalentFinRadius,
      double equivalentDiameter, double waterPath, double finThickness,
      double thermalConductivity, double innerDiameter, double outerDiameter,
      double airFlowRate, double inletAirTemperature, double inletAirHumidityRatio,
      double borderRelativeHumidity, double waterFlowRate, double inletWaterTemperature,
      out double dryHeatTransferCoefficient, out double wetHeatTransferCoefficient)
    {
      //Compute moist air properties//specific heat [kJ/kgK], kinematic viscosity [m2/s], thermal conductivity [W/(mK)]
      //specific volume [kg/m3], diffusivity [m2/s]
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double dVis = MoistAir.GetDynamicViscosity
        (inletAirTemperature, inletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double tCond = MoistAir.GetThermalConductivity(inletAirTemperature);
      double sVol = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletAirTemperature, inletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double difc = MoistAir.GetThermalDiffusivity
        (inletAirTemperature, inletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);

      //Compute the actual air velocity [m/s]
      double coreVelocity = airFlowRate * PhysicsConstants.NominalMoistAirDensity / coreArea;

      //Compute the Reynolds number [-]
      double re = coreVelocity * equivalentDiameter / dVis;

      //Compute the water-side convective heat transfer coefficient [W/(m2K)]
      double wfCoefficient = FluidCircuit.WaterPipe.GetInsideHeatTransferCoefficient
        (inletWaterTemperature, innerDiameter, waterFlowRate / waterPath);

      //Dry section calculation////
      //Compute the air-side convective heat transfer coefficient [W/(m2K)]
      double afd = 0.129 * tCond / equivalentDiameter * Math.Pow(re, 0.64);

      //Compute the fin efficiency [-]
      double fEfficiencyD = HeatExchange.GetCircularFinEfficiency
        (outerDiameter / 2, equivalentFinRadius, finThickness, afd, thermalConductivity);

      //Compute the overall heat transfer coefficient [kW/(m2K)]
      dryHeatTransferCoefficient = 0.001 / (airWaterSurfaceRatio / wfCoefficient 
        + 1 / (afd * (fEfficiencyD + 1 / airWaterSurfaceRatio)));

      //Wet section calculation////
      //Compute the mass transfer coefficient on the fin surface [W/(m2(kJ/kg))]
      double kf = 37.2 * difc / (sVol * equivalentDiameter) * Math.Pow(re, 0.8);

      //Compute the enthalpy approximation coefficients
      double a, b;
      GetSaturationEnthalpyCoefficients(inletWaterTemperature, out a, out b);

      //Compute the fin efficiency [-]
      double lewis = 3.19 * Math.Pow(re, -0.16);
      double afw = a / (cpma * lewis) * afd;
      double fEfficiencyW = HeatExchange.GetCircularFinEfficiency
        (outerDiameter / 2, equivalentFinRadius, finThickness, afw, thermalConductivity);

      //Compute the overall heat transfer coefficient [kW/(m2(kJ/kg))]
      wetHeatTransferCoefficient = 0.001 / (a * airWaterSurfaceRatio / wfCoefficient 
        + 1 / (kf * (fEfficiencyW + 1 / airWaterSurfaceRatio)));
    }

    #endregion

  }
}
