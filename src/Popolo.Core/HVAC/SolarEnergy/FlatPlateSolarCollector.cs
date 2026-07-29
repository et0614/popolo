/* FlatPlateSolarCollector.cs
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

using Popolo.Core.Exceptions;
using Popolo.Core.Physics;
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.SolarEnergy
{
  /// <summary>Provides static methods for flat-plate solar collector heat transfer calculations.</summary>
  public static class FlatPlateSolarCollector
  {

    #region Constant declarations

    /// <summary>Angle-of-incidence correction coefficients for glass transmittance.</summary>
    private static readonly double[] MI_TAU = new double[] { 2.552, 1.364, -11.388, 13.617, -5.146 };

    /// <summary>Angle-of-incidence correction coefficients for glass reflectance.</summary>
    private static readonly double[] MI_RHO = new double[] { 5.189, -12.392, 16.593, -11.851, 3.461 };

    /// <summary>Effective transmittance coefficient for diffuse sky radiation.</summary>
    private static readonly double DIFFUSE_TRANSMITTANCE_COEF;

    /// <summary>Effective reflectance coefficient for diffuse sky radiation.</summary>
    private static readonly double DIFFUSE_REFLECTANCE_COEF;

    #endregion

    #region Static constructor

    /// <summary>Static constructor.</summary>
    static FlatPlateSolarCollector()
    {
      for (int i = 0; i < 5; i++)
      {
        DIFFUSE_TRANSMITTANCE_COEF += MI_TAU[i] / (i + 3);
        DIFFUSE_REFLECTANCE_COEF += MI_RHO[i] / (i + 3);
      }
      DIFFUSE_TRANSMITTANCE_COEF *= 2;
      DIFFUSE_REFLECTANCE_COEF *= 2;
    }

    #endregion

    #region Static methods

    /// <summary>Computes the heat collected by a flat-plate solar thermal collector.</summary>
    /// <param name="skyTemperature">Effective sky temperature [°C].</param>
    /// <param name="airTemperature">Ambient air temperature [°C].</param>
    /// <param name="directNormalRadiation">Direct normal irradiance [W/m²].</param>
    /// <param name="diffuseRadiation">Diffuse horizontal irradiance [W/m²].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="waterInletTemperature">Inlet water temperature [°C].</param>
    /// <param name="airThickness">Air gap thickness [m].</param>
    /// <param name="glassEmissivity">Glass emissivity [-].</param>
    /// <param name="panelEmissivity">Absorber panel emissivity [-].</param>
    /// <param name="windSpeed">External wind speed [m/s].</param>
    /// <param name="insulatorThickness">Insulator thickness [m].</param>
    /// <param name="insulatorThermalConductivity">Insulator thermal conductivity [W/(m·K)].</param>
    /// <param name="surfaceArea">Panel surface area [m²].</param>
    /// <param name="tubePitch">Tube pitch (centre-to-centre spacing) [m].</param>
    /// <param name="innerDiameter">Inner diameter of the collector tube [m].</param>
    /// <param name="outerDiameter">Outer diameter of the collector tube [m].</param>
    /// <param name="panelThickness">Absorber panel thickness [m].</param>
    /// <param name="panelThermalConductivity">Absorber panel thermal conductivity [W/(m·K)].</param>
    /// <param name="cosTheta">Cosine of the angle of incidence.</param>
    /// <param name="glassTransmittance">Glass solar transmittance [-].</param>
    /// <param name="glassReflectance">Glass solar reflectance [-].</param>
    /// <param name="panelAbsorptance">Absorber panel solar absorptance [-].</param>
    /// <param name="panelTemperature">Output: absorber panel temperature [°C].</param>
    /// <param name="glassTemperature">Output: glass cover temperature [°C].</param>
    /// <param name="waterOutletTemperature">Output: outlet water temperature [°C].</param>
    /// <param name="meanWaterTemperature">Output: mean water temperature [°C].</param>
    /// <param name="efficiency">Output: collection efficiency [-].</param>
    /// <returns>Collected heat [W].</returns>
    public static double GetHeatTransfer
      (double skyTemperature, double airTemperature, double directNormalRadiation, double diffuseRadiation, 
      double waterFlowRate, double waterInletTemperature, double airThickness, double glassEmissivity,
      double panelEmissivity, double windSpeed, double insulatorThickness, double insulatorThermalConductivity,
      double surfaceArea, double tubePitch, double innerDiameter, double outerDiameter, double panelThickness,
      double panelThermalConductivity, double cosTheta, double glassTransmittance, double glassReflectance, 
      double panelAbsorptance, out double panelTemperature, out double glassTemperature,
      out double waterOutletTemperature, out double meanWaterTemperature, out double efficiency)
    {
      double heatTransfer = 0; //Collected heat [W]
      glassTemperature = 0;
      meanWaterTemperature = 0;

      //Precompute values independent of the iterative calculation//////////////////////////////
      //Compute the solar absorption of the glass and the panel [W/m2]
      double sagDN, sapDN, sagDF, sapDF;
      GetSolarAbsorptanceOfGlassAndPanel
        (cosTheta, glassTransmittance, glassReflectance, panelAbsorptance, out sagDN, out sapDN, out sagDF, out sapDF);
      double glassSolarAbsorption = sagDN * directNormalRadiation + sagDF * diffuseRadiation;
      double panelSolarAbsorption = sapDN * directNormalRadiation + sapDF * diffuseRadiation;

      //Compute the convective heat transfer coefficient between the glass and the outdoor air [W/m2K]
      double convectiveHeatLoss_ga = windSpeed * 7.6 + 4.7;

      //Compute the heat transmission coefficient between the glass and the panel [W/m2K]
      double airThermalConductivity = 0.0241 + 0.000077 * airTemperature;
      double convectiveHeatLoss_pg = airThermalConductivity / airThickness;   //Assume Nu=1.0
      //Compute the effective radiation constant between the glass and the panel
      double effectiveEmissivity_pg = PhysicsConstants.StefanBoltzmannConstant / (1 / panelEmissivity + 1 / glassEmissivity - 1);

      //Compute the heat transmission coefficient between the panel and the back side [W/m2K]
      double heatLoss_pb = insulatorThermalConductivity / insulatorThickness;

      //Set the initial panel temperature
      panelTemperature = airTemperature + 5;
      //Panel temperature at the previous time step
      double ptL = panelTemperature + 1;
      //Iterative calculation of the panel temperature
      int iterNumP = 0;
      while (0.001 < Math.Abs(panelTemperature - ptL))
      {
        if (100 < iterNumP) throw new PopoloNumericalException(
          "FlatPlateSolarCollector.GetHeatTransfer",
          $"Panel temperature iteration did not converge within {iterNumP} iterations.");

        double heatLoss_pg = 0; //Heat transmission coefficient between the glass and the panel [W/m2K]
        double radiativeHeatLoss_ga = 0; //Radiative heat transfer coefficient between the glass and the sky [W/m2K]

        //Save the panel temperature from the previous step
        ptL = panelTemperature;

        //Define the error function
        Roots.ErrorFunction efncG = delegate (double gTemp)
        {
          //Compute the radiative heat transfer coefficient between the glass and the sky [W/m2K]
          radiativeHeatLoss_ga = 4 * PhysicsConstants.StefanBoltzmannConstant * glassEmissivity 
          * Math.Pow(PhysicsConstants.ToKelvin((gTemp + skyTemperature) / 2), 3);

          //Compute the heat transmission coefficient between the glass and the panel [W/m2K]
          double radiativeHeatLoss_pg = 4 * effectiveEmissivity_pg * Math.Pow(PhysicsConstants.ToKelvin((ptL + gTemp) / 2), 3);
          heatLoss_pg = convectiveHeatLoss_pg + radiativeHeatLoss_pg;

          //Error between the updated glass temperature and the assumed value
          return gTemp - (glassSolarAbsorption + heatLoss_pg * ptL + radiativeHeatLoss_ga * skyTemperature 
          + convectiveHeatLoss_ga * airTemperature) / (heatLoss_pg + radiativeHeatLoss_ga + convectiveHeatLoss_ga);
        };

        //Solve for the glass temperature with the Newton-Raphson method
        glassTemperature = Roots.Newton(efncG, 0.5 * (panelTemperature + airTemperature), 0.0001, 0.01, 0.01, 10);

        //Compute the heat loss coefficient UL [W/m2K]
        double heatLoss_ga = convectiveHeatLoss_ga 
          + radiativeHeatLoss_ga * (glassTemperature - skyTemperature) / (glassTemperature - airTemperature);
        double heatLossCoefficient = (heatLoss_pg * heatLoss_ga) / (heatLoss_pg + heatLoss_ga) + heatLoss_pb;

        //Compute the fin efficiency of the rectangular fin [-]
        double wd2 = Math.Sqrt(heatLossCoefficient / (panelThermalConductivity * panelThickness)) 
          * (tubePitch - innerDiameter) / 2;
        double rectFinEfficiency = Math.Tanh(wd2) / wd2;

        //Compute the fin efficiency of the solar collector [-]
        double tubeHeatTransfer = 
          GetConvectiveHeatTransferCoefficientOfTube(waterInletTemperature, innerDiameter, waterFlowRate);
        double fe1 = 1 / (heatLossCoefficient * (outerDiameter + (tubePitch - outerDiameter) * rectFinEfficiency));
        double fe2 = 1 / (Math.PI * innerDiameter * tubeHeatTransfer);
        double finEfficiency = (1 / heatLossCoefficient) / (tubePitch * (fe1 + fe2));

        //Compute the collector heat removal factor FR [-]
        double bf = waterFlowRate * Water.GetLiquidIsobaricSpecificHeat(waterInletTemperature) 
          * 1000 / (surfaceArea * heatLossCoefficient);
        double heatRemovalFactor = bf * (1 - Math.Exp(-finEfficiency / bf));

        //Compute the heat transfer [W]
        heatTransfer = panelSolarAbsorption + glassSolarAbsorption * heatLoss_pg / (heatLoss_pg + heatLoss_ga);
        heatTransfer -= heatLossCoefficient * (waterInletTemperature - airTemperature);
        heatTransfer *= surfaceArea * heatRemovalFactor;

        //Update the panel and mean heat medium temperatures
        double buff = (heatTransfer / surfaceArea) / (heatLossCoefficient * heatRemovalFactor);
        panelTemperature = waterInletTemperature + buff * (1 - heatRemovalFactor);
        meanWaterTemperature = waterInletTemperature + buff * (1 - heatRemovalFactor / finEfficiency);

        iterNumP++;
      }
      //Compute the outlet water temperature
      waterOutletTemperature = 0.001 * waterInletTemperature + heatTransfer
        / (waterFlowRate * Water.GetLiquidIsobaricSpecificHeat(waterInletTemperature));
      //Compute the collection efficiency
      efficiency = heatTransfer / (cosTheta * directNormalRadiation + diffuseRadiation) / surfaceArea;

      //Return the collected heat
      return heatTransfer;
    }

    /// <summary>Computes the combined solar absorptance of the glass cover and the absorber panel.</summary>
    /// <param name="cosineTheta">Cosine of the solar angle of incidence (cos θ).</param>
    /// <param name="glassTransmittance">Glass solar transmittance [-].</param>
    /// <param name="glassReflectance">Glass solar reflectance [-].</param>
    /// <param name="panelAbsorptance">Absorber panel solar absorptance [-].</param>
    /// <param name="glassDNSolarAbsorptance">Output: combined glass absorptance for direct normal radiation [-].</param>
    /// <param name="panelDNSolarAbsorptance">Output: combined panel absorptance for direct normal radiation [-].</param>
    /// <param name="glassDFSolarAbsorptance">Output: combined glass absorptance for diffuse sky radiation [-].</param>
    /// <param name="panelDFSolarAbsorptance">Output: combined panel absorptance for diffuse sky radiation [-].</param>
    private static void GetSolarAbsorptanceOfGlassAndPanel
      (double cosineTheta, double glassTransmittance, double glassReflectance, double panelAbsorptance, 
      out double glassDNSolarAbsorptance, out double panelDNSolarAbsorptance,
      out double glassDFSolarAbsorptance, out double panelDFSolarAbsorptance)
    {
      //Compute the normalized transmittance and normalized reflectance of the glass
      double tn = 0;
      double rn = 0;
      for (int i = 4; 0 <= i; i--)
      {
        tn = cosineTheta * (tn + MI_TAU[i]);
        rn = cosineTheta * (rn + MI_RHO[i]);
      }

      //Compute the transmittance, reflectance, and absorptance of the glass alone for direct normal radiation
      double tauThetaG_DN = glassTransmittance * tn;
      double rhoThetaG_DN = 1 - (1 - glassReflectance) * rn;
      double alphaThetaG_DN = 1 - (tauThetaG_DN + rhoThetaG_DN);

      //Compute the transmittance, reflectance, and absorptance of the glass alone for diffuse sky radiation
      double tauThetaG_DF = glassTransmittance * DIFFUSE_TRANSMITTANCE_COEF;
      double rhoThetaG_DF = glassReflectance * DIFFUSE_REFLECTANCE_COEF;
      double alphaThetaG_DF = 1 - (tauThetaG_DF + rhoThetaG_DF);

      //Compute the absorptance and reflectance of the panel alone
      double alphaThetaP = cosineTheta * panelAbsorptance;
      double rhoThetaP = 1 - alphaThetaP;

      //Compute the combined absorptance of the glass
      glassDNSolarAbsorptance = alphaThetaG_DN * (1 + tauThetaG_DN * rhoThetaP / (1 - rhoThetaG_DN * rhoThetaP));
      glassDFSolarAbsorptance = alphaThetaG_DF * (1 + tauThetaG_DF * rhoThetaP / (1 - rhoThetaG_DF * rhoThetaP));

      //Compute the combined absorptance of the panel
      panelDNSolarAbsorptance = tauThetaG_DN * alphaThetaP / (1 - rhoThetaG_DN * rhoThetaP);
      panelDFSolarAbsorptance = tauThetaG_DF * alphaThetaP / (1 - rhoThetaG_DF * rhoThetaP);
    }

    /// <summary>Computes the convective heat transfer coefficient inside the collector tube.</summary>
    /// <param name="waterTemperature">Water temperature [°C].</param>
    /// <param name="diameter">Tube inner diameter [m].</param>
    /// <param name="waterFlowRate">Water flow rate [L/min].</param>
    /// <returns>Convective heat transfer coefficient [W/(m²·K)].</returns>
    private static double GetConvectiveHeatTransferCoefficientOfTube
      (double waterTemperature, double diameter, double waterFlowRate)
    {
      //Compute the kinematic viscosity [m2/s], thermal diffusivity [m2/s], and thermal conductivity [W/(m·K)]
      double v = Water.GetLiquidDynamicViscosity(waterTemperature);
      double a = Water.GetLiquidThermalDiffusivity(waterTemperature);
      double lambda = Water.GetLiquidThermalConductivity(waterTemperature);

      //Compute the flow velocity in the tube [m/s]
      double u = (waterFlowRate / 60 / 1000) / (Math.Pow(diameter / 2, 2) * Math.PI);

      //Compute the Nusselt number
      double reNumber = u * diameter / v;
      double prNumber = v / a;
      double nuNumber = 0.023 * Math.Pow(reNumber, 0.8) * Math.Pow(prNumber, 0.4);

      //Compute the convective heat transfer coefficient from the Nusselt number
      return nuNumber * lambda / diameter;
    }

    #endregion

  }
}
