/* WaterPipe.cs
 * 
 * Copyright (C) 2014 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General
 * Public License as published by the Free Software Foundation; either version 3 of the License, or (at your
 * option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even 
 * the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public 
 * License for more details.
 * 
 * You should have received a copy of the GNU General Public License along with this program; if not, write
 * to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using Popolo.Core.Exceptions;
using Popolo.Core.Numerics;
using Popolo.Core.Physics;
using System;

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a water pipe with heat loss calculation.</summary>
  public class WaterPipe : IReadOnlyWaterPipe, ICircuitBranch
  {

    #region Enumeration definitions

    /// <summary>Pipe material.</summary>
    public enum Material
    {
      /// <summary>Carbon steel pipe.</summary>
      CarbonSteel = 0,
      /// <summary>PVC pipe.</summary>
      Plastic = 1,
      /// <summary>Stainless steel.</summary>
      StainlessSteel = 2
    }

    /// <summary>Insulator material type.</summary>
    public enum Insulator
    {
      /// <summary>No insulation.</summary>
      None = 0,
      /// <summary>Rock wool insulation.</summary>
      RockWool = 1,
      /// <summary>Glass wool insulation.</summary>
      GlassWool = 2,
      /// <summary>Polystyrene insulation.</summary>
      Polystyrene = 3
    }

    #endregion

    #region Instance variables

    /// <summary>Linear thermal transmittance excluding convective resistances [W/(m·K)].</summary>
    private double linearThermalTransmittance = 0;

    /// <summary>Pipe length [m].</summary>
    private double length = 1;

    /// <summary>Inner surface roughness [m].</summary>
    private double roughness;

    #endregion

    #region Properties

    /// <summary>Gets the inner diameter [m].</summary>
    public double InnerDiameter { get; private set; }

    /// <summary>Gets the outer diameter [m].</summary>
    public double OuterDiameter { get; private set; }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    public double InletWaterTemperature { get; private set; }

    /// <summary>Gets the ambient dry-bulb temperature [°C].</summary>
    public double AmbientTemperature { get; private set; }

    /// <summary>Gets the ambient humidity ratio [kg/kg].</summary>
    public double AmbientHumidityRatio { get; private set; }

    /// <summary>Gets the linear thermal transmittance (excluding convective resistances) [W/(m·K)].</summary>
    public double LinearThermalTransmittance { get; private set; }

    /// <summary>Gets or sets the pipe length [m].</summary>
    public double Length
    {
      get { return length; }
      set { if (0 < value) length = value; }
    }

    /// <summary>Gets or sets the inner surface roughness [m].</summary>
    public double Roughness
    {
      get { return roughness; }
      set { if (0 < value) roughness = value; }
    }

    /// <summary>Gets the heat loss [kW].</summary>
    public double HeatLoss { get; private set; }

    /// <summary>Gets the outlet water temperature [°C].</summary>
    public double OutletWaterTemperauture { get; private set; }

    /// <summary>Gets the pipe thermal conductivity [W/(m·K)].</summary>
    public double PipeThermalConductivity { get; private set; }

    /// <summary>Gets the insulator thermal conductivity [W/(m·K)].</summary>
    public double InsulatorThermalConductivity { get; private set; }

    /// <summary>Gets the insulator thickness [m].</summary>
    public double InsulatorThickness { get; private set; }

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance.</summary>
    /// <param name="length">Length [m].</param>
    /// <param name="innerDiameter">Inner diameter [m].</param>
    /// <param name="roughness">Pipe roughness [m].</param>
    /// <param name="thickness">Pipe wall thickness [m].</param>
    public WaterPipe(double length, double innerDiameter, double roughness, double thickness)
    {
      InnerDiameter = innerDiameter;
      Length = length;
      Roughness = roughness;
      OuterDiameter = InnerDiameter + 2 * thickness;

      //Assume no insulation
      RemoveInsulator();
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="length">Length [m].</param>
    /// <param name="innerDiameter">Inner diameter [m].</param>
    /// <param name="material">Pipe material.</param>
    public WaterPipe(double length, double innerDiameter, Material material)
    {
      InnerDiameter = innerDiameter;
      Length = length;
      double dth;
      switch (material)
      {
        case Material.CarbonSteel:
          Roughness = 0.03e-3;
          dth = 1.0796e-3 * Math.Pow(1000 * innerDiameter, 0.3181);
          break;
        case Material.Plastic:
          Roughness = 0.03e-3;
          dth = 0.5196e-3 * Math.Pow(1000 * innerDiameter, 0.5746);
          break;
        default://SUS
          Roughness = 0.015e-3;
          dth = 0.7250e-3 * Math.Pow(1000 * innerDiameter, 0.4581);
          break;
      }
      OuterDiameter = InnerDiameter + 2 * dth;

      //Assume no insulation
      RemoveInsulator();
    }

    /// <summary>Initializes internal state.</summary>
    private void Initialize()
    {
      //Initialize the linear thermal transmittance [W/(mK)]
      linearThermalTransmittance = GetPipeLinearThermalTransmittance
        (InnerDiameter, InsulatorThermalConductivity, InsulatorThickness, PipeThermalConductivity,
        0.5 * (OuterDiameter - InnerDiameter));

      //Initialize the heat flow//Water flow rate equivalent to a velocity of 2.0 m/s
      UpdateHeatFlow(7, InnerDiameter * InnerDiameter / 4d * Math.PI * 2 * 1000, 25, 0.02);
    }

    #endregion

    #region Instance methods for heat flow

    /// <summary>Updates the heat flow.</summary>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="waterFlowRate">Water flow rate [m³/s].</param>
    /// <param name="ambientTemperature">Ambient dry-bulb temperature [°C].</param>
    /// <param name="ambientHumidityRatio">Ambient humidity ratio [kg/kg].</param>
    public void UpdateHeatFlow(double inletWaterTemperature,
      double waterFlowRate, double ambientTemperature, double ambientHumidityRatio)
    {
      VolumetricFlowRate = waterFlowRate;
      InletWaterTemperature = inletWaterTemperature;
      AmbientTemperature = ambientTemperature;
      AmbientHumidityRatio = ambientHumidityRatio;

      //Convert volumetric flow rate [m3/s] to mass flow rate [kg/s]
      double mw = waterFlowRate / Water.GetLiquidDensity(inletWaterTemperature);

      //Compute the water-side convective heat transfer coefficient [W/(m2K)]
      double aw = GetInsideHeatTransferCoefficient(InletWaterTemperature, InnerDiameter, mw);

      //Assume the pipe surface temperature is the average of the water and air temperatures
      double ts = 0.5 * (InletWaterTemperature + AmbientTemperature);

      double err = 1;
      int iterNum = 0;
      while (0.01 < err)
      {
        if (20 < iterNum) throw new PopoloNumericalException(
          "WaterPipe.UpdateHeatFlow",
          $"Surface temperature iteration did not converge within {iterNum} iterations.");

        //Compute the air-side convective heat transfer coefficient [W/(m2K)]
        double aa = GetOutSideHeatTransferCoefficient
          (AmbientTemperature, AmbientHumidityRatio, OuterDiameter, ts);

        //Update the linear thermal transmittance [W/(mK)]
        LinearThermalTransmittance = GetAirToWaterLinearThermalTransmittance
          (aw, aa, InnerDiameter, OuterDiameter, linearThermalTransmittance);

        //Update the pipe surface temperature [C] and evaluate the error
        double tsOld = ts;
        ts = AmbientTemperature - LinearThermalTransmittance
          * (AmbientTemperature - InletWaterTemperature) / aa;
        err = Math.Abs(ts - tsOld);
        iterNum++;
      }

      //Update the heat loss and outlet water temperature
      HeatLoss = LinearThermalTransmittance * Length
        * (AmbientTemperature - InletWaterTemperature) / 1000d;
      double cpw = Water.GetLiquidIsobaricSpecificHeat(InletWaterTemperature);
      OutletWaterTemperauture = HeatLoss / (mw * cpw) + InletWaterTemperature;
    }

    /// <summary>Sets the insulation.</summary>
    /// <param name="thickness">Insulator thickness [m].</param>
    /// <param name="insulator">Insulator type.</param>
    public void SetInsulator(double thickness, Insulator insulator)
    {
      InsulatorThickness = thickness;
      switch (insulator)
      {
        case Insulator.RockWool:
          SetInsulator(thickness, 0.044);
          break;
        case Insulator.GlassWool:
          SetInsulator(thickness, 0.043);
          break;
        case Insulator.Polystyrene:
          SetInsulator(thickness, 0.043);
          break;
        default:
          throw new PopoloArgumentException(
            $"Unsupported insulator type: {insulator}.", nameof(insulator));
      }
    }

    /// <summary>Sets the insulation.</summary>
    /// <param name="thickness">Insulator thickness [m].</param>
    /// <param name="thermalConductivity">Insulator thermal conductivity [W/(m·K)].</param>
    public void SetInsulator(double thickness, double thermalConductivity)
    {
      InsulatorThickness = thickness;
      InsulatorThermalConductivity = thermalConductivity;
      Initialize();
    }

    /// <summary>Removes the insulation.</summary>
    public void RemoveInsulator()
    {
      InsulatorThickness = 0.0d;
      InsulatorThermalConductivity = 0.04d;
      Initialize();
    }

    /// <summary>Changes the pipe size.</summary>
    /// <param name="innerDiameter">Inner diameter [m].</param>
    /// <param name="thickness">Thickness [m].</param>
    public void ChangeSize(double innerDiameter, double thickness)
    {
      if ((0 < innerDiameter) && (0 < thickness))
      {
        InnerDiameter = innerDiameter;
        OuterDiameter = InnerDiameter + 2 * thickness;
        Initialize();
      }
    }

    /// <summary>Sets the pipe thermal conductivity.</summary>
    /// <param name="thermalConductivity">Thermal conductivity [W/(m·K)].</param>
    public void SetPipeThermalConductivity(double thermalConductivity)
    {
      if (0 < thermalConductivity) PipeThermalConductivity = thermalConductivity;
      Initialize();
    }

    #endregion

    #region ICircuitBranch implementation

    /// <summary>Gets or sets the flow rate [m³/s].</summary>
    public double VolumetricFlowRate { get; set; }

    /// <summary>Gets or sets the upstream node.</summary>
    public CircuitNode? UpStreamNode { get; set; }

    /// <summary>Gets or sets the downstream node.</summary>
    public CircuitNode? DownStreamNode { get; set; }

    /// <summary>Computes the volumetric flow rate [m³/s] from the differential pressure.</summary>
    /// <returns>Volumetric flow rate [m³/s].</returns>
    public void UpdateFlowRateFromNodePressureDifference()
    {
      if (UpStreamNode == null || DownStreamNode == null)
        throw new PopoloInvalidOperationException(
            nameof(WaterPipe),
            nameof(UpStreamNode));

      //Compute water properties (kinematic viscosity [m2/s], thermal diffusivity [m2/s])
      double v = Water.GetLiquidDynamicViscosity(InletWaterTemperature);
      double rho = Water.GetLiquidDensity(InletWaterTemperature);

      //Flow area [m2] and pressure difference across the pipe [kPa]
      double fArea = InnerDiameter * InnerDiameter / 4d * Math.PI;
      double dp = (UpStreamNode.Pressure - DownStreamNode.Pressure) * 1000;

      //Iteratively solve for the flow velocity [m/s]
      Roots.ErrorFunction eFnc = delegate (double vel)
      {
        double reNumber = vel * InnerDiameter / v;
        double ff = Conduit.GetFrictionFactor(reNumber, Roughness / InnerDiameter);
        return Conduit.GetVelocity(ff, rho, Length, InnerDiameter, dp) - vel;
      };
      //Convergence works well when the initial guess is at a large Reynolds number (high velocity)
      VolumetricFlowRate = Roots.Newton(eFnc, 5, 0.0001, 1e-10, 1e-9, 30) * fArea;
    }

    #endregion

    /// <summary>Gets the pressure loss [kPa].</summary>
    /// <param name="flowRate">Volumetric flow rate [m³/s].</param>
    /// <returns>Pressure loss [kPa].</returns>
    public double GetPressureDrop(double flowRate)
    {
      double v = Water.GetLiquidDynamicViscosity(InletWaterTemperature);
      double rho = Water.GetLiquidDensity(InletWaterTemperature);
      double fArea = InnerDiameter * InnerDiameter / 4d * Math.PI;
      double vel = flowRate / fArea;
      double reNumber = vel * InnerDiameter / v;
      double ff = Conduit.GetFrictionFactor(reNumber, Roughness / InnerDiameter);
      return 0.001 * Conduit.GetPressureDrop(ff, rho, Length, InnerDiameter, vel);
    }

    #region Class methods

    /// <summary>Computes the convective heat transfer coefficient at the inner pipe surface [W/(m²·K)].</summary>
    /// <param name="waterTemperature">Water temperature [°C].</param>
    /// <param name="diameter">Diameter [m].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <returns>Convective heat transfer coefficient [W/(m²·K)].</returns>
    /// <remarks>Applicable to turbulent, fully developed flow.</remarks>
    public static double GetInsideHeatTransferCoefficient
      (double waterTemperature, double diameter, double waterFlowRate)
    {
      //Compute water properties
      //Kinematic viscosity [m2/s], thermal diffusivity [m2/s], thermal conductivity [W/(m·K)], density [kg/m3]
      double v = Water.GetLiquidDynamicViscosity(waterTemperature);
      double a = Water.GetLiquidThermalDiffusivity(waterTemperature);
      double lam = Water.GetLiquidThermalConductivity(waterTemperature);
      double rho = Water.GetLiquidDensity(waterTemperature);

      //Compute the Nusselt number from the in-pipe flow velocity [m/s]
      double u = waterFlowRate / (rho * diameter * diameter / 4 * Math.PI);
      double reNumber = u * diameter / v;
      double prNumber = v / a;
      double nuNumber = 0.023 * Math.Pow(reNumber, 0.8) * Math.Pow(prNumber, 0.4);

      //Compute the convective heat transfer coefficient [W/m2K] from the Nusselt number
      return nuNumber * lam / diameter;
    }

    /// <summary>Computes the natural convection heat transfer coefficient at the outer pipe surface [W/(m²·K)].</summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg].</param>
    /// <param name="diameter">Diameter [m].</param>
    /// <param name="surfaceTemperature">Pipe surface temperature [°C].</param>
    /// <returns>Natural convection heat transfer coefficient at the outer pipe surface [W/(m²·K)].</returns>
    public static double GetOutSideHeatTransferCoefficient
      (double dryBulbTemperature, double humidityRatio, double diameter, double surfaceTemperature)
    {
      //Compute moist air properties
      //Kinematic viscosity [m2/s], thermal diffusivity [m2/s], expansion coefficient [1/K], thermal conductivity [W/(m·K)]
      double v = MoistAir.GetDynamicViscosity(dryBulbTemperature, humidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double a = MoistAir.GetThermalDiffusivity(dryBulbTemperature, humidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double beta = MoistAir.GetExpansionCoefficient(dryBulbTemperature);
      double lam = MoistAir.GetThermalConductivity(dryBulbTemperature);

      //Compute the Grashof number
      double grNumber = 9.8 * Math.Pow(diameter, 3) * beta *
        Math.Abs(surfaceTemperature - dryBulbTemperature) / (v * v);

      //Compute the Prandtl number
      double prNumber = v / a;

      //Compute the Nusselt number
      double nuNumber;
      double grpr = grNumber * prNumber;
      if (grpr < 1e10) nuNumber = 0.53 * Math.Pow(grpr, 0.25);
      else nuNumber = 0.13 * Math.Pow(grpr, 0.333);

      //Compute the convective heat transfer coefficient [W/(m2K)] from the Nusselt number
      return nuNumber * lam / diameter;
    }

    /// <summary>Computes the linear thermal transmittance from water to air [W/(m·K)].</summary>
    /// <param name="insideHeatTransferCoef">Water-side convective heat transfer coefficient [W/(m²·K)].</param>
    /// <param name="outsideHeatTransferCoef">Air-side convective heat transfer coefficient [W/(m²·K)].</param>
    /// <param name="innerDiameter">Inner diameter [m].</param>
    /// <param name="outerDiameter">Outer diameter [m].</param>
    /// <param name="thermalConductance">Linear thermal transmittance from inner to outer surface [W/(m·K)].</param>
    /// <returns>Linear thermal transmittance from water to air [W/(m·K)].</returns>
    public static double GetAirToWaterLinearThermalTransmittance
      (double insideHeatTransferCoef, double outsideHeatTransferCoef,
      double innerDiameter, double outerDiameter, double thermalConductance)
    {
      double p2 = 2 * Math.PI;
      double kl = 1 / (insideHeatTransferCoef * innerDiameter) +
        1 / (outsideHeatTransferCoef * outerDiameter) + p2 / thermalConductance;
      return p2 / kl;
    }

    /// <summary>Computes the linear thermal transmittance between the inner and outer surfaces of the pipe [W/(m·K)].</summary>
    /// <param name="innerDiameter">Pipe inner diameter [m].</param>
    /// <param name="insulatorThermalConductivity">Insulator thermal conductivity [W/(m·K)].</param>
    /// <param name="insulatorThickness">Insulator thickness [m].</param>
    /// <param name="pipeThermalConductivity">Pipe thermal conductivity [W/(m·K)].</param>
    /// <param name="pipeThickness">Pipe wall thickness [m].</param>
    /// <returns>Linear thermal transmittance between inner and outer pipe surfaces [W/(m·K)].</returns>
    public static double GetPipeLinearThermalTransmittance
      (double innerDiameter, double insulatorThermalConductivity, double insulatorThickness,
      double pipeThermalConductivity, double pipeThickness)
    {
      double d_b = innerDiameter + pipeThickness;
      double d_o = d_b + insulatorThickness;
      double klc = 1 / pipeThermalConductivity * Math.Log(d_b / innerDiameter)
        + 1 / insulatorThermalConductivity * Math.Log(d_o / d_b);
      return Math.PI / klc;
    }

    #endregion

  }
}
