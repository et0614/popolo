/* IceOnCoilThermalStorage.cs
 *
 * Copyright (C) 2016 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;

using Popolo.Core.Exceptions;
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.Storage
{
  /// <summary>Internal-melt ice-on-coil thermal storage tank.</summary>
  /// <remarks>
  /// Based on:
  /// 1) Aotake, Sagara, et al., "A study on optimal operation of a thermal storage system
  ///    with an inverter-driven refrigerator," Proc. SHASEJ Annual Meeting, pp.157–160, 2007.
  ///
  /// The original formulation has been extended to allow sensible heat flow after the ice
  /// has fully solidified (i.e., temperature changes of the frozen ice itself).
  /// </remarks>
  public class IceOnCoilThermalStorage : IReadOnlyIceOnCoilThermalStorage
  {

    #region Constant declarations

    /// <summary>Specific heat of liquid water [kJ/(kg·K)].</summary>
    public const double WaterSpecificHeat = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;

    /// <summary>Specific heat of ice [kJ/(kg·K)].</summary>
    public const double IceSpecificHeat = 2.1;

    /// <summary>Latent heat of fusion of ice [kJ/kg].</summary>
    public const double IceLatentHeat = 334;

    /// <summary>Density of ice [kg/m³].</summary>
    public const double IceDensity = 917d;

    /// <summary>Thermal conductivity of the coil pipe [W/(m·K)] (assuming copper).</summary>
    public const double PipeThermalConductivity = 370;

    /// <summary>Thermal conductivity of ice [W/(m·K)].</summary>
    public const double IceThermalConductivity = 2.2;

    /// <summary>Number of segments used to discretize each coil branch along its length.</summary>
    public const int SegmentsCount = 10;

    /// <summary>Natural convection heat transfer coefficient on the outer surface
    /// (pipe or ice) in still water [W/(m²·K)].</summary>
    /// <remarks>Reference 1).</remarks>
    public const double NaturalConvectionCoefficient = 170;

    /// <summary>Natural convection heat transfer coefficient inside the annular water layer
    /// during melting [W/(m²·K)].</summary>
    /// <remarks>Reference 1).</remarks>
    public const double InsideNaturalConvectionCoefficient = 250;

    /// <summary>Forced convection heat transfer coefficient on the outer surface
    /// (pipe or ice) with air bubbling [W/(m²·K)].</summary>
    /// <remarks>Reference 1).</remarks>
    public const double ForcedConvectionCoefficient = 300;

    /// <summary>Critical ice thickness below which the ice shell is considered to be
    /// broken (fragmented) [m].</summary>
    /// <remarks>Reference 1).</remarks>
    public const double CriticalIceThickness = 0.0155;

    #endregion

    #region Enumeration definitions

    /// <summary>State of ice around a coil segment.</summary>
    public enum IceState
    {
      /// <summary>No ice is present (pipe exposed to water).</summary>
      NoIce,
      /// <summary>Ice is solidly attached to the pipe (during freezing).</summary>
      Frozen,
      /// <summary>Water layer has formed between the pipe and the ice (melting from inside).</summary>
      Melting
    }

    #endregion

    #region Instance variables and properties

    /// <summary>Time step [s].</summary>
    private double timeStep = 60;

    /// <summary>Outer diameter of ice at each segment [m].</summary>
    private readonly double[] iceOuterDiameters = new double[SegmentsCount];

    /// <summary>Inner diameter of ice at each segment [m].</summary>
    private readonly double[] iceInnerDiameters = new double[SegmentsCount];

    /// <summary>Water/ice temperature at each segment [°C].</summary>
    private readonly double[] waterIceTemperatures = new double[SegmentsCount];

    /// <summary>Water volume per unit pipe length [m³/m].</summary>
    private readonly double watPerUnit;

    /// <summary>Maximum ice volume per unit pipe length (when all water has frozen) [m³/m].</summary>
    private readonly double icePerUnit;

    /// <summary>Maximum ice outer diameter reachable before all water freezes [m].</summary>
    private readonly double maxIceDiameter;

    /// <summary>Heat loss coefficient per unit pipe length [W/(m·K)].</summary>
    private double heatLossPerUnit = 1;

    /// <summary>Gets or sets the time step [s]. Only positive values are accepted.</summary>
    public double TimeStep
    {
      get { return timeStep; }
      set { if (0 < value) timeStep = value; }
    }

    /// <summary>Gets the current ice state of the tank (evaluated at segment 0).</summary>
    public IceState CurrentState { get; private set; }

    /// <summary>Gets the total water volume of the tank [m³].</summary>
    public double WaterVolume { get; private set; }

    /// <summary>Gets the number of parallel coil branches.</summary>
    public double NumberOfBranches { get; private set; }

    /// <summary>Gets the length of a single coil branch [m].</summary>
    public double BranchLength { get; private set; }

    /// <summary>Gets the pipe inner diameter [m].</summary>
    public double PipeInnerDiameter { get; private set; }

    /// <summary>Gets the pipe wall thickness [m].</summary>
    public double PipeThickness { get { return 0.5 * (PipeOuterDiameter - PipeInnerDiameter); } }

    /// <summary>Gets the pipe outer diameter [m].</summary>
    public double PipeOuterDiameter { get; private set; }

    /// <summary>Gets or sets a value indicating whether air bubbling (forced convection) is active.</summary>
    public bool IsBubbling { get; set; } = false;

    /// <summary>Gets the brine inlet temperature [°C].</summary>
    public double InletBrineTemperature { get; private set; }

    /// <summary>Gets the brine outlet temperature [°C].</summary>
    public double OutletBrineTemperature { get; private set; }

    /// <summary>Gets the brine mass flow rate [kg/s].</summary>
    public double BrineFlowRate { get; private set; }

    /// <summary>Gets or sets the specific heat of brine [kJ/(kg·K)].</summary>
    public double BrineSpecificHeat { get; set; } = 3.5;

    /// <summary>Gets the heat transfer from brine to the coil [kW].</summary>
    /// <remarks>Positive: heat rejected from tank to brine (melting);
    /// negative: heat extracted from brine (ice making).</remarks>
    public double HeatTransferToCoil { get; private set; }

    /// <summary>Gets or sets the overall heat loss coefficient of the tank [W/K].</summary>
    public double HeatLossCoefficient
    {
      get { return heatLossPerUnit * (NumberOfBranches * BranchLength); }
      set { heatLossPerUnit = value / (NumberOfBranches * BranchLength); }
    }

    /// <summary>Gets or sets the ambient temperature surrounding the tank [°C].</summary>
    public double AmbientTemperature { get; set; } = 20;

    /// <summary>Gets the heat loss from the tank to the ambient [kW].</summary>
    public double HeatLoss { get; private set; }

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance of the <see cref="IceOnCoilThermalStorage"/> class.</summary>
    /// <param name="waterVolume">Total water volume of the tank [m³].</param>
    /// <param name="branchCount">Number of parallel coil branches.</param>
    /// <param name="branchLength">Length of a single coil branch [m].</param>
    /// <param name="pipeInnerDiameter">Pipe inner diameter [m].</param>
    /// <param name="pipeOuterDiameter">Pipe outer diameter [m].</param>
    public IceOnCoilThermalStorage(double waterVolume, int branchCount, double branchLength,
      double pipeInnerDiameter, double pipeOuterDiameter)
    {
      WaterVolume = waterVolume;
      NumberOfBranches = branchCount;
      BranchLength = branchLength;
      PipeInnerDiameter = pipeInnerDiameter;
      PipeOuterDiameter = pipeOuterDiameter;

      watPerUnit = WaterVolume / (NumberOfBranches * branchLength); //Water volume per unit pipe length [m3/m]
      icePerUnit = watPerUnit * PhysicsConstants.NominalWaterDensity / IceDensity; //Ice volume per unit pipe length [m3/m]
      maxIceDiameter = getOuterDiameterFromAnnulusArea(icePerUnit, PipeOuterDiameter); //Maximum ice diameter [m]

      //Initialize the water temperature
      Initialize(10);
    }

    /// <summary>Initializes the tank to a uniform water temperature with no ice.</summary>
    /// <param name="waterTemperature">Water temperature to initialize [°C]. Negative values are clamped to 0.</param>
    public void Initialize(double waterTemperature)
    {
      waterTemperature = Math.Max(0, waterTemperature);
      for (int i = 0; i < SegmentsCount; i++)
      {
        iceOuterDiameters[i] = iceInnerDiameters[i] = PipeOuterDiameter;
        waterIceTemperatures[i] = waterTemperature;
      }
      CurrentState = IceState.NoIce;
    }

    #endregion

    #region State update methods

    /// <summary>Advances the tank state by one time step.</summary>
    /// <param name="inletBrineTemperature">Brine inlet temperature [°C].</param>
    /// <param name="brineFlowRate">Brine mass flow rate [kg/s].</param>
    public void Update(double inletBrineTemperature, double brineFlowRate)
    {
      InletBrineTemperature = inletBrineTemperature;
      BrineFlowRate = brineFlowRate;
      double branchFlow = BrineFlowRate / NumberOfBranches;

      //Compute the thermal resistance of the pipe itself depending on the flow rate
      //The inside convective heat transfer coefficient assumes water at 0°C
      double alpha_i = WaterPipe.GetInsideHeatTransferCoefficient(0, PipeInnerDiameter, branchFlow);
      if (alpha_i == 0) alpha_i = 0.001; //Even at near-zero flow, use a very small value instead of 0 to account for convection.
      double pR = 1d / (PipeInnerDiameter * alpha_i) + //Convective heat transfer at the pipe inner surface
        0.5d / PipeThermalConductivity * Math.Log(PipeOuterDiameter / PipeInnerDiameter);  //Heat conduction through the pipe

      //Update the state of each segment
      HeatLoss = 0;
      for (int i = 0; i < SegmentsCount; i++)
      {
        updateSegment(i, inletBrineTemperature, branchFlow, pR, out double hl, out inletBrineTemperature);
        HeatLoss += hl; //Sum the heat losses
      }
      HeatLoss *= 0.001 / SegmentsCount * BranchLength * NumberOfBranches;
      OutletBrineTemperature = inletBrineTemperature;

      //Compute the heat flow from the brine to the coil
      HeatTransferToCoil = (InletBrineTemperature - OutletBrineTemperature) * BrineFlowRate * BrineSpecificHeat;

      //Update the current ice state (based on segment 0)
      CurrentState = getIceState(PipeOuterDiameter, iceOuterDiameters[0], iceInnerDiameters[0]);
    }

    /// <summary>Updates a single segment.</summary>
    /// <param name="segmentIndex">Segment index.</param>
    /// <param name="inletBrineTemperature">Segment inlet brine temperature [°C].</param>
    /// <param name="brineFlowRate">Brine mass flow rate per branch [kg/s].</param>
    /// <param name="pipeResistance">Pipe thermal resistance [m²·K/W].</param>
    /// <param name="heatLoss">Heat loss from the segment to the ambient [W/m].</param>
    /// <param name="outletBrineTemperature">Segment outlet brine temperature [°C].</param>
    private void updateSegment(
      int segmentIndex, double inletBrineTemperature, double brineFlowRate, double pipeResistance,
      out double heatLoss, out double outletBrineTemperature)
    {
      double dIceO = this.iceOuterDiameters[segmentIndex];
      double dIceI = this.iceInnerDiameters[segmentIndex];
      double tIceWater = this.waterIceTemperatures[segmentIndex];

      //Compute the heat flows
      heatLoss = (AmbientTemperature - tIceWater) * heatLossPerUnit;
      double heatFlowToCoil = (inletBrineTemperature - tIceWater) * getPipeLinearThermalTransmittance(segmentIndex, pipeResistance) + heatLoss;

      //No heat flow
      if (heatFlowToCoil == 0)
      {
        outletBrineTemperature = inletBrineTemperature;
        return;
      }

      //Ice present
      if (PipeOuterDiameter < dIceO)
      {
        //Ice making
        if (heatFlowToCoil < 0)
        {
          //Special situation: ice making resumes in the middle of melting
          if (PipeOuterDiameter < dIceI)
          {
            //Attach the same volume of ice directly to the pipe
            dIceO = getOuterDiameterFromAnnulusArea(getAnnulusSurfaceArea(dIceO, dIceI), PipeOuterDiameter);
            iceOuterDiameters[segmentIndex] = dIceO;
            iceInnerDiameters[segmentIndex] = PipeOuterDiameter;
          }

          //If no water is left to freeze, the ice temperature drops
          if (maxIceDiameter <= dIceO)
          {
            double dTice = -heatFlowToCoil * timeStep / (icePerUnit * IceDensity * IceSpecificHeat * 1000);
            waterIceTemperatures[segmentIndex] -= dTice;
          }
          //If water remains, the ice grows thicker
          else
          {
            double dAreaIce = -heatFlowToCoil * timeStep / (IceDensity * IceLatentHeat * 1000); //Ice volume increase [m3/m]
            dIceO = getOuterDiameterFromAnnulusArea(dAreaIce, dIceO);  //Update the ice diameter

            //Ice outer diameter exceeded the maximum
            if (maxIceDiameter <= dIceO)
            {
              //Convert the excess latent heat into a temperature drop
              double dTice = getAnnulusSurfaceArea(dIceO, maxIceDiameter) * (IceLatentHeat / (IceSpecificHeat * icePerUnit));
              waterIceTemperatures[segmentIndex] = -dTice;
              iceOuterDiameters[segmentIndex] = maxIceDiameter;
            }
            else iceOuterDiameters[segmentIndex] = dIceO;
          }
        }
        //Ice melting
        else
        {
          //Ice temperature change
          if (tIceWater < 0)
          {
            double dTice = heatFlowToCoil * timeStep / (icePerUnit * IceDensity * IceSpecificHeat * 1000);
            tIceWater += dTice;
            //Turns into water when the temperature exceeds 0°C
            if (0 < tIceWater)
            {
              //Convert the excess sensible heat into an ice melting volume
              double dAreaIce = tIceWater * (icePerUnit * IceSpecificHeat) / IceLatentHeat;
              double areaIce = getAnnulusSurfaceArea(dIceO, dIceI);
              //All of the ice would melt
              if (areaIce <= dAreaIce)
                throw new PopoloNumericalException(nameof(IceOnCoilThermalStorage),
                  "100% of the ice was melted in a single time step. The time step is too large.");
              //Update the ice inner diameter
              else
              {
                dIceI = getInnerDiameterFromAnnulusArea(areaIce - dAreaIce, dIceO);
                iceInnerDiameters[segmentIndex] = dIceI;
                tIceWater = 0;
              }
            }
            waterIceTemperatures[segmentIndex] = tIceWater;
          }
          //Ice melting
          else
          {
            double dAreaIce = heatFlowToCoil * timeStep / (IceDensity * IceLatentHeat * 1000); //Ice volume decrease [m3/m]
            double areaIce = getAnnulusSurfaceArea(dIceO, dIceI);
            //All of the ice melts
            if (areaIce <= dAreaIce)
            {
              //Convert the excess heat into a water temperature rise
              double dTwat = (dAreaIce - areaIce) * (IceDensity * IceLatentHeat) / (watPerUnit * PhysicsConstants.NominalWaterDensity * WaterSpecificHeat);
              tIceWater += dTwat;
              waterIceTemperatures[segmentIndex] = tIceWater;
              iceOuterDiameters[segmentIndex] = iceInnerDiameters[segmentIndex] = PipeOuterDiameter;
            }
            //Update the ice inner diameter
            else
            {
              dIceI = getInnerDiameterFromAnnulusArea(areaIce - dAreaIce, dIceO);
              iceInnerDiameters[segmentIndex] = dIceI;
            }
          }
        }
      }
      //No ice
      else
      {
        tIceWater += heatFlowToCoil * timeStep / (watPerUnit * PhysicsConstants.NominalWaterDensity * WaterSpecificHeat * 1000);

        //Ice making has started
        if (tIceWater < 0)
        {
          //Convert the excess heat into ice making
          double dAreaIce = -tIceWater * (watPerUnit * PhysicsConstants.NominalWaterDensity * WaterSpecificHeat) / (IceDensity * IceLatentHeat);
          iceOuterDiameters[segmentIndex] = getOuterDiameterFromAnnulusArea(dAreaIce, PipeOuterDiameter);
          if (maxIceDiameter <= iceOuterDiameters[segmentIndex])
            throw new PopoloNumericalException(nameof(IceOnCoilThermalStorage),
              "100% of the ice was produced in a single time step. The time step is too large.");
          waterIceTemperatures[segmentIndex] = 0;
        }
        else waterIceTemperatures[segmentIndex] = tIceWater;
      }

      //Update the brine temperature
      if (brineFlowRate == 0) outletBrineTemperature = inletBrineTemperature;
      else outletBrineTemperature = inletBrineTemperature - (heatFlowToCoil * BranchLength / SegmentsCount)
        / (brineFlowRate * BrineSpecificHeat * 1000);
    }

    #endregion

    #region Other instance methods

    /// <summary>Computes the linear thermal transmittance of pipe + ice/water [W/(m·K)] at the given segment.</summary>
    /// <param name="segmentIndex">Segment index.</param>
    /// <param name="pipeResistance">Pipe thermal resistance [m²·K/W].</param>
    /// <returns>Linear thermal transmittance [W/(m·K)].</returns>
    private double getPipeLinearThermalTransmittance(int segmentIndex, double pipeResistance)
    {
      return getPipeLinearThermalTransmittance(
        PipeInnerDiameter, PipeOuterDiameter,
        iceOuterDiameters[segmentIndex], iceInnerDiameters[segmentIndex], pipeResistance, IsBubbling);
    }

    /// <summary>Computes the ice packing factor (IPF) [-].</summary>
    /// <returns>Ice packing factor (ratio of total ice mass to total tank water mass) [-].</returns>
    public double GetIcePackingFactor()
    {
      double iceVolume = 0d;
      for (int i = 0; i < SegmentsCount; i++)
        iceVolume += getAnnulusSurfaceArea(iceOuterDiameters[i], iceInnerDiameters[i]);
      double iceMass = iceVolume / SegmentsCount * BranchLength * NumberOfBranches * IceDensity;
      return iceMass / (WaterVolume * PhysicsConstants.NominalWaterDensity);
    }

    /// <summary>Computes the spatially averaged water/ice temperature across all coil segments [°C].</summary>
    /// <returns>Average water/ice temperature [°C].</returns>
    public double GetAverageWaterIceTemperature()
    {
      double sum = 0d;
      for (int i = 0; i < SegmentsCount; i++)
        sum += waterIceTemperatures[i];
      return sum / SegmentsCount;
    }

    #endregion

    #region Static methods

    /// <summary>Computes the linear thermal transmittance of pipe + ice/water [W/(m·K)].</summary>
    /// <param name="pipeInnerDiameter">Pipe inner diameter [m].</param>
    /// <param name="pipeOuterDiameter">Pipe outer diameter [m].</param>
    /// <param name="iceOuterDiameter">Ice outer diameter [m].</param>
    /// <param name="iceInnerDiameter">Ice inner diameter [m].</param>
    /// <param name="pipeResistance">Pipe thermal resistance [m²·K/W].</param>
    /// <param name="isBubbling">Whether air bubbling is active.</param>
    /// <returns>Linear thermal transmittance [W/(m·K)].</returns>
    private static double getPipeLinearThermalTransmittance(
      double pipeInnerDiameter, double pipeOuterDiameter, double iceOuterDiameter, double iceInnerDiameter,
      double pipeResistance, bool isBubbling)
    {
      double buff = pipeResistance;

      IceState iState = getIceState(pipeOuterDiameter, iceOuterDiameter, iceInnerDiameter);
      bool isBroken =
        ((pipeOuterDiameter < iceInnerDiameter) &&
        (0.5 * (iceOuterDiameter - iceInnerDiameter) < CriticalIceThickness)); //Ice is considered broken below a certain thickness (to be adjusted later)

      //No ice
      if (iState == IceState.NoIce)
        buff += 1d / (pipeOuterDiameter * (isBubbling ? ForcedConvectionCoefficient : NaturalConvectionCoefficient)); //Convective heat transfer at the pipe outer surface
      //Frozen (ice attached)
      else if (iState == IceState.Frozen)
      {
        buff += 0.5d / IceThermalConductivity * Math.Log(iceOuterDiameter / pipeOuterDiameter); //Heat conduction through the ice
        buff += 1d / (iceOuterDiameter * (isBubbling ? ForcedConvectionCoefficient : NaturalConvectionCoefficient)); //Convective heat transfer outside the ice
      }
      //Melting
      else
      {
        buff += 1d / (pipeOuterDiameter * ((isBubbling && isBroken) ? ForcedConvectionCoefficient : InsideNaturalConvectionCoefficient)); //Convective heat transfer at the pipe outer surface
        buff += 1d / (iceInnerDiameter * ((isBubbling && isBroken) ? ForcedConvectionCoefficient : InsideNaturalConvectionCoefficient)); //Convective heat transfer at the ice surface
      }

      return Math.PI / buff;
    }

    /// <summary>Determines the ice state from the geometry.</summary>
    /// <param name="pipeDiameter">Pipe outer diameter [m].</param>
    /// <param name="iceOuterDiameter">Ice outer diameter [m].</param>
    /// <param name="iceInnerDiameter">Ice inner diameter [m].</param>
    /// <returns>Ice state.</returns>
    private static IceState getIceState(double pipeDiameter, double iceOuterDiameter, double iceInnerDiameter)
    {
      if (pipeDiameter == iceOuterDiameter && pipeDiameter == iceInnerDiameter) return IceState.NoIce;
      else if (pipeDiameter < iceOuterDiameter && pipeDiameter < iceInnerDiameter) return IceState.Melting;
      else return IceState.Frozen;
    }

    /// <summary>Solves for the outer diameter of an annulus given its cross-sectional area and inner diameter.</summary>
    /// <param name="surfaceArea">Annulus cross-sectional area [m²].</param>
    /// <param name="innerDiameter">Inner diameter [m].</param>
    /// <returns>Outer diameter [m].</returns>
    private static double getOuterDiameterFromAnnulusArea(double surfaceArea, double innerDiameter)
    {
      return Math.Sqrt(Math.Pow(innerDiameter, 2) + 4 * surfaceArea / Math.PI);
    }

    /// <summary>Solves for the inner diameter of an annulus given its cross-sectional area and outer diameter.</summary>
    /// <param name="surfaceArea">Annulus cross-sectional area [m²].</param>
    /// <param name="outerDiameter">Outer diameter [m].</param>
    /// <returns>Inner diameter [m].</returns>
    private static double getInnerDiameterFromAnnulusArea(double surfaceArea, double outerDiameter)
    {
      return Math.Sqrt(Math.Pow(outerDiameter, 2) - 4 * surfaceArea / Math.PI);
    }

    /// <summary>Computes the cross-sectional area of an annulus from its inner and outer diameters.</summary>
    /// <param name="outerDiameter">Outer diameter [m].</param>
    /// <param name="innerDiameter">Inner diameter [m].</param>
    /// <returns>Annulus cross-sectional area [m²].</returns>
    private static double getAnnulusSurfaceArea(double outerDiameter, double innerDiameter)
    {
      return Math.PI * (Math.Pow(outerDiameter, 2) - Math.Pow(innerDiameter, 2)) / 4d;
    }

    #endregion

  }
}
