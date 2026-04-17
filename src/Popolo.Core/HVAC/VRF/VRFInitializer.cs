/* VRFInitializer.cs
 * 
 * Copyright (C) 2020 E.Togashi
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

using Popolo.Core.Exceptions;
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.VRF
{
  /// <summary>Provides factory methods for initialising VRF system components from manufacturer catalogue data.</summary>
  public static class VRFInitializer
  {

    #region 列挙型定義

    /// <summary>Outdoor unit model identifier.</summary>
    public enum OutdoorUnitModel
    {
      /// <summary>Daikin VRV-X series.</summary>
      Daikin_VRVX,
      /// <summary>Daikin VRV-A series.</summary>
      Daikin_VRVA,
      /// <summary>Hitachi Set-Free SS series.</summary>
      Hitachi_SS,
      /// <summary>Toshiba SMMS-i (MMY) series.</summary>
      Toshiba_MMY
    }

    /// <summary>Indoor unit type identifier.</summary>
    public enum IndoorUnitType
    {
      /// <summary>Ceiling-embedded cassette, four-way blow.</summary>
      CeilingFourWay,
      /// <summary>Ceiling-embedded cassette, S-round flow.</summary>
      CeilingRoundFlow_S,
      /// <summary>Ceiling-embedded cassette, round flow.</summary>
      CeilingRoundFlow,
      /// <summary>Ceiling-embedded cassette, eco double-flow (two-way blow).</summary>
      CeilingDoubleFlow_Eco,
      /// <summary>Ceiling-embedded cassette, double-flow (two-way blow).</summary>
      CeilingDoubleFlow,
      /// <summary>Ceiling-embedded cassette, single-flow (one-way blow).</summary>
      CeilingSingleFlow,
      /// <summary>Ceiling built-in type.</summary>
      CeilingMounted,
      /// <summary>Ceiling-embedded slim duct type.</summary>
      CeilingConcealedSlimDuct,
      /// <summary>Ceiling-embedded duct type.</summary>
      CeilingConcealedDuct,
      /// <summary>Ceiling-suspended type.</summary>
      CeilingSuspended,
      /// <summary>Wall-mounted type.</summary>
      WallMounted,
      /// <summary>Floor-standing low-boy type.</summary>
      FloorStandingLowboy,
      /// <summary>Floor-embedded low-boy type.</summary>
      ConcealedLowboy,
      /// <summary>Floor-standing duct type.</summary>
      FloorMount
    }

    /// <summary>Cooling capacity class.</summary>
    public enum CoolingCapacity
    {
      /// <summary>2.2kW</summary>
      C2_2,
      /// <summary>2.8kW</summary>
      C2_8,
      /// <summary>3.6kW</summary>
      C3_6,
      /// <summary>4.0kW</summary>
      C4_0,
      /// <summary>4.5kW</summary>
      C4_5,
      /// <summary>5.0kW</summary>
      C5_0,
      /// <summary>5.6kW</summary>
      C5_6,
      /// <summary>6.3kW</summary>
      C6_3,
      /// <summary>7.1kW</summary>
      C7_1,
      /// <summary>8.0kW</summary>
      C8_0,
      /// <summary>9.0kW</summary>
      C9_0,
      /// <summary>11.2kW</summary>
      C11_2,
      /// <summary>14.0kW</summary>
      C14_0,
      /// <summary>16.0kW</summary>
      C16_0,
      /// <summary>22.4kW</summary>
      C22_4,
      /// <summary>28.0kW</summary>
      C28_0,
      /// <summary>33.5kW</summary>
      C33_5,
      /// <summary>40.0kW</summary>
      C40_0,
      /// <summary>45.0kW</summary>
      C45_0,
      /// <summary>50.0kW</summary>
      C50_0,
      /// <summary>56.0kW</summary>
      C56_0,
      /// <summary>61.5kW</summary>
      C61_5,
      /// <summary>67.0kW</summary>
      C67_0,
      /// <summary>73.0kW</summary>
      C73_0,
      /// <summary>77.5kW</summary>
      C77_5,
      /// <summary>85.0kW</summary>
      C85_0,
      /// <summary>90.0kW</summary>
      C90_0,
      /// <summary>95.0kW</summary>
      C95_0,
      /// <summary>100.0kW</summary>
      C100_0,
      /// <summary>106.0kW</summary>
      C106_0,
      /// <summary>112.0kW</summary>
      C112_0,
      /// <summary>118.0kW</summary>
      C118_0,
      /// <summary>122.0kW</summary>
      C122_0,
      /// <summary>128.0kW</summary>
      C128_0,
      /// <summary>136.0kW</summary>
      C136_0,
      /// <summary>140.0kW</summary>
      C140_0,
      /// <summary>145.0kW</summary>
      C145_0,
      /// <summary>150.0kW</summary>
      C150_0,
      /// <summary>22.4 kW model from Miyata (research paper).</summary>
      Miyata22_4
    }

    #endregion

    #region public static method

    /// <summary>Creates an outdoor unit from catalogue data for the specified model and capacity.</summary>
    /// <param name="model">Outdoor unit model.</param>
    /// <param name="coolingCapacity">Cooling capacity class.</param>
    /// <param name="indoorUnitHeight">Installation height of indoor units relative to the outdoor unit [m].</param>
    /// <param name="useWaterSpray">True to enable water spray on the outdoor unit condenser.</param>
    /// <returns>Initialized VRF outdoor unit system.</returns>
    public static VRFSystem MakeOutdoorUnit
      (OutdoorUnitModel model,
      CoolingCapacity coolingCapacity, double indoorUnitHeight, bool useWaterSpray)
    {
      switch (model)
      {
        case OutdoorUnitModel.Daikin_VRVX:
          return MakeOutdoorUnit_DaikinVRVX(coolingCapacity, indoorUnitHeight, useWaterSpray);
        case OutdoorUnitModel.Daikin_VRVA:
          return MakeOutdoorUnit_DaikinVRVA(coolingCapacity, indoorUnitHeight, useWaterSpray);
        case OutdoorUnitModel.Hitachi_SS:
          return MakeOutdoorUnit_HitachiSS(coolingCapacity, indoorUnitHeight, useWaterSpray);
        case OutdoorUnitModel.Toshiba_MMY:
          return MakeOutdoorUnit_ToshibaMMY(coolingCapacity, indoorUnitHeight, useWaterSpray);
        default:
          throw new PopoloArgumentException(
              $"Unsupported outdoor unit model: {model}.", nameof(model));
      }
    }

    #endregion

    #region ダイキン初期化

    /// <summary>Creates a Daikin indoor unit from catalogue data.</summary>
    /// <param name="iType">Indoor unit type.</param>
    /// <param name="coolingCapacity">Cooling capacity class.</param>
    /// <returns>Initialized Daikin indoor unit.</returns>
    /// <remarks>Data source: 2020 catalogue.</remarks>
    public static VRFUnit MakeIndoorUnit_Daikin
      (IndoorUnitType iType, CoolingCapacity coolingCapacity)
    {
      switch (iType)
      {
        case IndoorUnitType.CeilingRoundFlow_S: //FXYFP-D
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(12.5 * 1.2 / 60d, 0.028, -2.8, 0.024, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(12.5 * 1.2 / 60d, 0.028, -3.6, 0.024, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.037, -4.5, 0.033, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(15.5 * 1.2 / 60d, 0.043, -5.6, 0.038, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(22.0 * 1.2 / 60d, 0.072, -7.1, 0.068, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(23.5 * 1.2 / 60d, 0.086, -8.0, 0.081, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(30.0 * 1.2 / 60d, 0.128, -9.0, 0.110, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(33.0 * 1.2 / 60d, 0.175, -11.2, 0.162, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(34.5 * 1.2 / 60d, 0.197, -14.0, 0.179, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(35.5 * 1.2 / 60d, 0.217, -16.0, 0.207, 18.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingRoundFlow:  //FXYFP-M
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(12.5 * 1.2 / 60d, 0.033, -2.8, 0.027, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(12.5 * 1.2 / 60d, 0.033, -3.6, 0.027, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.047, -4.5, 0.034, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(15.5 * 1.2 / 60d, 0.052, -5.6, 0.038, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(20.0 * 1.2 / 60d, 0.072, -7.1, 0.068, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(24.5 * 1.2 / 60d, 0.086, -8.0, 0.081, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(24.5 * 1.2 / 60d, 0.128, -9.0, 0.110, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(31.5 * 1.2 / 60d, 0.187, -11.2, 0.174, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(34.5 * 1.2 / 60d, 0.209, -14.0, 0.200, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(35.5 * 1.2 / 60d, 0.217, -16.0, 0.207, 18.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingDoubleFlow_Eco:
          if (coolingCapacity == CoolingCapacity.C2_2) return VRFSystem.MakeIndoorUnit(10.5 * 1.2 / 60d, 0.031, -2.2, 0.028, 2.5);
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(11.5 * 1.2 / 60d, 0.039, -2.8, 0.035, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(11.5 * 1.2 / 60d, 0.039, -3.6, 0.035, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(12.0 * 1.2 / 60d, 0.037, -4.5, 0.037, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(15.0 * 1.2 / 60d, 0.056, -5.6, 0.056, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(16.0 * 1.2 / 60d, 0.060, -7.1, 0.060, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(17.5 * 1.2 / 60d, 0.068, -8.0, 0.068, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(26.0 * 1.2 / 60d, 0.086, -9.0, 0.086, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(28.0 * 1.2 / 60d, 0.093, -11.2, 0.093, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(32.0 * 1.2 / 60d, 0.146, -14.0, 0.146, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(36.0 * 1.2 / 60d, 0.197, -16.0, 0.197, 18.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingDoubleFlow:
          if (coolingCapacity == CoolingCapacity.C2_2) return VRFSystem.MakeIndoorUnit(7.0 * 1.2 / 60d, 0.078, -2.2, 0.045, 2.5);
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(9.0 * 1.2 / 60d, 0.083, -2.8, 0.050, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(9.0 * 1.2 / 60d, 0.083, -3.6, 0.050, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(12.0 * 1.2 / 60d, 0.118, -4.5, 0.085, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(12.0 * 1.2 / 60d, 0.118, -5.6, 0.085, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(16.5 * 1.2 / 60d, 0.131, -7.1, 0.098, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(18.0 * 1.2 / 60d, 0.151, -8.0, 0.118, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(26.0 * 1.2 / 60d, 0.165, -9.0, 0.132, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(26.0 * 1.2 / 60d, 0.165, -11.2, 0.132, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(32.0 * 1.2 / 60d, 0.206, -14.0, 0.173, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(36.0 * 1.2 / 60d, 0.238, -16.0, 0.205, 18.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingSingleFlow:
          if (coolingCapacity == CoolingCapacity.C2_2) return VRFSystem.MakeIndoorUnit(6.5 * 1.2 / 60d, 0.036, -2.2, 0.036, 2.5);
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(7.5 * 1.2 / 60d, 0.051, -2.8, 0.051, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(9.5 * 1.2 / 60d, 0.056, -3.6, 0.056, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(11.5 * 1.2 / 60d, 0.075, -4.5, 0.069, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.103, -5.6, 0.097, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(18.0 * 1.2 / 60d, 0.100, -7.1, 0.096, 8.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingMounted:
          if (coolingCapacity == CoolingCapacity.C2_2) return VRFSystem.MakeIndoorUnit(9.0 * 1.2 / 60d, 0.110, -2.2, 0.090, 2.5);
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(9.0 * 1.2 / 60d, 0.110, -2.8, 0.090, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(9.5 * 1.2 / 60d, 0.110, -3.6, 0.090, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(11.5 * 1.2 / 60d, 0.127, -4.5, 0.107, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(15.0 * 1.2 / 60d, 0.163, -5.6, 0.143, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(21.0 * 1.2 / 60d, 0.206, -7.1, 0.186, 8.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(27.0 * 1.2 / 60d, 0.216, -9.0, 0.196, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(28.0 * 1.2 / 60d, 0.250, -11.2, 0.220, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(38.0 * 1.2 / 60d, 0.320, -14.0, 0.300, 16.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingConcealedSlimDuct:
          if (coolingCapacity == CoolingCapacity.C2_2) return VRFSystem.MakeIndoorUnit(8.0 * 1.2 / 60d, 0.067, -2.2, 0.058, 2.5);
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(8.0 * 1.2 / 60d, 0.067, -2.8, 0.058, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(8.0 * 1.2 / 60d, 0.067, -3.6, 0.058, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(10.5 * 1.2 / 60d, 0.074, -4.5, 0.066, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(12.5 * 1.2 / 60d, 0.082, -5.6, 0.075, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(16.5 * 1.2 / 60d, 0.099, -7.1, 0.106, 8.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingConcealedDuct:
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(16.0 * 1.2 / 60d, 0.146, -4.5, 0.134, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(16.0 * 1.2 / 60d, 0.146, -5.6, 0.134, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(19.5 * 1.2 / 60d, 0.134, -7.1, 0.122, 8.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(29.0 * 1.2 / 60d, 0.184, -9.0, 0.172, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(32.0 * 1.2 / 60d, 0.210, -11.2, 0.198, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(39.0 * 1.2 / 60d, 0.279, -14.0, 0.267, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(46.0 * 1.2 / 60d, 0.400, -16.0, 0.375, 18.0);
          if (coolingCapacity == CoolingCapacity.C22_4) return VRFSystem.MakeIndoorUnit(58.0 * 1.2 / 60d, 1.340, -22.4, 1.340, 25.0);
          if (coolingCapacity == CoolingCapacity.C28_0) return VRFSystem.MakeIndoorUnit(72.0 * 1.2 / 60d, 1.340, -28.0, 1.340, 31.5);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingSuspended:
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(14.0 * 1.2 / 60d, 0.107, -3.6, 0.107, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(14.0 * 1.2 / 60d, 0.107, -4.5, 0.107, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.108, -5.6, 0.108, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(20.0 * 1.2 / 60d, 0.111, -7.1, 0.111, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(20.0 * 1.2 / 60d, 0.112, -8.0, 0.112, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(27.5 * 1.2 / 60d, 0.218, -9.0, 0.218, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(29.5 * 1.2 / 60d, 0.237, -11.2, 0.237, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(34.0 * 1.2 / 60d, 0.253, -14.0, 0.253, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(36.0 * 1.2 / 60d, 0.343, -16.0, 0.343, 18.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.WallMounted:
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(8.0 * 1.2 / 60d, 0.022, -2.8, 0.027, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(9.0 * 1.2 / 60d, 0.027, -3.6, 0.032, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(12.0 * 1.2 / 60d, 0.020, -4.5, 0.020, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(15.0 * 1.2 / 60d, 0.027, -5.6, 0.032, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(19.0 * 1.2 / 60d, 0.050, -7.1, 0.060, 8.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.FloorStandingLowboy:
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(7.0 * 1.2 / 60d, 0.039, -2.8, 0.039, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(8.0 * 1.2 / 60d, 0.067, -3.6, 0.067, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(11.0 * 1.2 / 60d, 0.069, -4.5, 0.069, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(14.0 * 1.2 / 60d, 0.086, -5.6, 0.086, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(16.0 * 1.2 / 60d, 0.087, -7.1, 0.087, 8.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.ConcealedLowboy:
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(7.0 * 1.2 / 60d, 0.039, -2.8, 0.039, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(8.0 * 1.2 / 60d, 0.067, -3.6, 0.067, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(11.0 * 1.2 / 60d, 0.069, -4.5, 0.069, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(14.0 * 1.2 / 60d, 0.086, -5.6, 0.086, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(16.0 * 1.2 / 60d, 0.087, -7.1, 0.087, 8.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.FloorMount:
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(42.0 * 1.2 / 60d, 0.340, -14.0, 0.340, 16.0);
          if (coolingCapacity == CoolingCapacity.C22_4) return VRFSystem.MakeIndoorUnit(63.0 * 1.2 / 60d, 0.490, -22.4, 0.490, 25.0);
          if (coolingCapacity == CoolingCapacity.C28_0) return VRFSystem.MakeIndoorUnit(80.0 * 1.2 / 60d, 0.640, -28.0, 0.640, 31.5);
          if (coolingCapacity == CoolingCapacity.C45_0) return VRFSystem.MakeIndoorUnit(120.0 * 1.2 / 60d, 2.020, -45.0, 2.020, 50.0);
          if (coolingCapacity == CoolingCapacity.C56_0) return VRFSystem.MakeIndoorUnit(165.0 * 1.2 / 60d, 2.180, -56.0, 2.180, 63.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        default:
          throw new PopoloArgumentException(
              $"Unsupported indoor unit type: {iType}.", nameof(iType));
      }
    }

    #region 室外機VRVX

    /// <summary>Creates a Daikin VRV-X outdoor unit from catalogue data.</summary>
    /// <param name="coolingCapacity">Cooling capacity class.</param>
    /// <param name="indoorUnitHeight">Installation height of indoor units relative to the outdoor unit [m].</param>
    /// <param name="useWaterSpray">True to enable water spray on the outdoor unit condenser.</param>
    /// <returns>Initialized Daikin VRV-X outdoor unit system.</returns>
    /// <remarks>Data source: 2016 technical data sheet.</remarks>
    private static VRFSystem MakeOutdoorUnit_DaikinVRVX
      (CoolingCapacity coolingCapacity, double indoorUnitHeight, bool useWaterSpray)
    {
      //冷媒はR410a
      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);

      VRFSystem vrfSystem;
      VRFUnit iHex;
      switch (coolingCapacity)
      {
        case CoolingCapacity.C22_4:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            218 * 1.2 / 60d, 0.26 * 2, -22.4, 6.07, -10.1, 1.89, -10.6, 1.55,
            218 * 1.2 / 60d, 0.26 * 2, 25.0, 6.32, 11.3, 2.06,
            7.5, 100, 0.88, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.18;
          break;
        case CoolingCapacity.C28_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C14_0);
          vrfSystem = new VRFSystem(r410a,
            187 * 1.2 / 60d, 0.21 * 2, -28.0, 8.93, -12.6, 2.35, -13.2, 1.94,
            187 * 1.2 / 60d, 0.21 * 2, 31.5, 8.68, 14.2, 2.54,
            7.5, 100, 0.89, 100, 0.83, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.13;
          break;
        case CoolingCapacity.C33_5:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C8_0);
          vrfSystem = new VRFSystem(r410a,
            187 * 1.2 / 60d, 0.21 * 2, -33.5, 9.74, -15.1, 2.59, -15.7, 2.12,
            187 * 1.2 / 60d, 0.21 * 2, 37.5, 10.0, 16.9, 2.94,
            7.5, 100, 0.895, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.13;
          break;
        case CoolingCapacity.C40_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            210 * 1.2 / 60d, 0.29 * 2, -40.0, 12.5, -18.0, 3.50, -18.8, 2.74,
            210 * 1.2 / 60d, 0.29 * 2, 45.0, 13.1, 20.3, 3.89,
            7.5, 100, 0.885, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.11;
          break;
        case CoolingCapacity.C45_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (218 + 218) * 1.2 / 60d, 0.26 * 2 * 2, -45.0, 12.3, -20.3, 3.88, -21.3, 3.17,
            (218 + 218) * 1.2 / 60d, 0.26 * 2 * 2, 50.0, 12.6, 22.5, 4.22,
            7.5, 100, 0.89, 100, 0.95, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.09;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C50_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (187 + 218) * 1.2 / 60d, 0.21 * 2 + 0.26 * 2, -50.0, 14.8, -22.5, 4.29, -23.7, 3.53,
            (187 + 218) * 1.2 / 60d, 0.21 * 2 + 0.26 * 2, 56.0, 14.8, 25.2, 4.65,
            7.5, 100, 0.905, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.08;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C56_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
             (187 + 218) * 1.2 / 60d, 0.21 * 2 + 0.26 * 2, -56.0, 15.9, -25.2, 4.66, -26.4, 3.81,
             (187 + 218) * 1.2 / 60d, 0.21 * 2 + 0.26 * 2, 63.0, 16.6, 28.4, 5.19,
            10.0, 100, 0.885, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.07;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C61_5:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (210 + 218) * 1.2 / 60d, 0.29 * 2 + 0.26 * 2, -61.5, 18.2, -27.7, 5.43, -29.0, 4.32,
            (210 + 218) * 1.2 / 60d, 0.29 * 2 + 0.26 * 2, 69.0, 19.0, 31.1, 5.99,
            10.0, 100, 0.890, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.07;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C67_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (187 + 187) * 1.2 / 60d, 0.26 * 2 * 2, -67.0, 19.6, -30.2, 5.33, -31.5, 4.36,
            (187 + 187) * 1.2 / 60d, 0.26 * 2 * 2, 77.5, 21.4, 34.9, 6.04,
            10.0, 100, 0.855, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.06;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C73_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (187 + 218) * 1.2 / 60d, 0.29 * 2 + 0.21 * 2, -73.0, 22.0, -32.9, 6.23, -34.4, 4.97,
            (187 + 218) * 1.2 / 60d, 0.29 * 2 + 0.21 * 2, 82.5, 23.2, 37.2, 6.99,
            10.0, 100, 0.880, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.06;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C77_5:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (210 + 210) * 1.2 / 60d, 0.29 * 2 * 2, -77.5, 23.6, -34.9, 6.86, -36.4, 5.37,
            (210 + 210) * 1.2 / 60d, 0.29 * 2 * 2, 90.0, 26.3, 40.5, 7.62,
            10.0, 100, 0.885, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.06;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C85_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (210 + 218 + 218) * 1.2 / 60d, 0.29 * 2 + 0.26 * 2 * 2, -85.0, 24.8, -38.3, 7.58, -40.2, 6.08,
            (210 + 218 + 218) * 1.2 / 60d, 0.29 * 2 + 0.26 * 2 * 2, 95.0, 25.8, 42.8, 8.33,
            10.0, 100, 0.880, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.05;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C90_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (187 + 187 + 210) * 1.2 / 60d, 0.21 * 2 * 2 + 0.26 * 2, -90.0, 25.9, -40.5, 7.41, -42.4, 6.06,
            (187 + 187 + 210) * 1.2 / 60d, 0.21 * 2 * 2 + 0.26 * 2, 100.0, 26.4, 45.0, 8.31,
            10.0, 100, 0.885, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.05;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C95_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (210 + 187 + 218) * 1.2 / 60d, 0.29 * 2 + 0.21 * 2 + 0.26 * 2, -95.0, 27.9, -42.8, 8.09, -44.8, 6.49,
            (210 + 187 + 218) * 1.2 / 60d, 0.29 * 2 + 0.21 * 2 + 0.26 * 2, 106.0, 28.8, 47.7, 9.00,
            10.0, 100, 0.890, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.04;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C100_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (187 + 187 + 187) * 1.2 / 60d, 0.21 * 2 * 3, -100.0, 29.0, -45.0, 7.93, -47.0, 6.48,
            (187 + 187 + 187) * 1.2 / 60d, 0.21 * 2 * 3, 112.0, 29.9, 50.4, 8.99,
            10.0, 100, 0.890, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.04;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C106_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (210 + 187 + 187) * 1.2 / 60d, 0.29 * 2 + 0.21 * 2 * 2, -106.0, 33.8, -47.7, 8.79, -49.8, 7.07,
            (210 + 187 + 187) * 1.2 / 60d, 0.29 * 2 + 0.21 * 2 * 2, 118.0, 34.8, 53.1, 9.90,
            10.0, 180, 0.820, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.04;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C112_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (210 + 210 + 187) * 1.2 / 60d, 0.29 * 2 * 2 + 0.21 * 2, -112.0, 33.9, -50.4, 9.80, -52.6, 7.77,
            (210 + 210 + 187) * 1.2 / 60d, 0.29 * 2 * 2 + 0.21 * 2, 125.0, 35.0, 56.3, 11.0,
            10.0, 160, 0.850, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.04;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C118_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (210 + 210 + 210) * 1.2 / 60d, 0.29 * 2 * 3, -118.0, 36.4, -53.1, 10.6, -55.4, 8.28,
            (210 + 210 + 210) * 1.2 / 60d, 0.29 * 2 * 3, 132.0, 37.8, 59.4, 11.8,
            10.0, 170, 0.85, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.04;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        default:
          throw new PopoloArgumentException(
              $"No catalogue data for cooling capacity: {coolingCapacity}.",
              nameof(coolingCapacity));
      }

      vrfSystem.MaxEvaporatingTemperature = VRFSystem.NOMINAL_EVPORATING_TEMPERATURE;
      vrfSystem.MinCondensingTemperature = VRFSystem.NOMINAL_CONDENSING_TEMPERATURE;
      vrfSystem.IndoorUnitHeight = indoorUnitHeight;
      vrfSystem.UseWaterSpray = useWaterSpray;

      return vrfSystem;
    }

    #endregion

    #region 室外機VRVA

    /// <summary>Creates a Daikin VRV-A outdoor unit from catalogue data.</summary>
    /// <param name="coolingCapacity">Cooling capacity class.</param>
    /// <param name="indoorUnitHeight">Installation height of indoor units relative to the outdoor unit [m].</param>
    /// <param name="useWaterSpray">True to enable water spray on the outdoor unit condenser.</param>
    /// <returns>Initialized Daikin VRV-X outdoor unit system.</returns>
    /// <remarks>Data source: 2016 technical data sheet.</remarks>
    private static VRFSystem MakeOutdoorUnit_DaikinVRVA
      (CoolingCapacity coolingCapacity, double indoorUnitHeight, bool useWaterSpray)
    {
      //冷媒はR410a
      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);

      VRFSystem vrfSystem;
      VRFUnit iHex;
      switch (coolingCapacity)
      {
        case CoolingCapacity.C14_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            104 * 1.2 / 60d, 0.22 * 1, -14.0, 3.78, -7.4, 1.51, -7.8, 1.21,
            104 * 1.2 / 60d, 0.22 * 1, 16.0, 3.81, 7.2, 1.27,
            7.5, 100, 0.75, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.26;
          break;
        case CoolingCapacity.C16_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            119 * 1.2 / 60d, 0.32 * 1, -16.0, 4.64, -7.2, 1.51, -7.8, 1.24,
            119 * 1.2 / 60d, 0.32 * 1, 18.0, 4.48, 8.1, 1.42,
            7.5, 100, 0.83, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.25;
          break;
        case CoolingCapacity.C22_4:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            160 * 1.2 / 60d, 0.39 * 1, -22.4, 6.36, -10.1, 2.21, -10.6, 1.74,
            160 * 1.2 / 60d, 0.39 * 1, 25.0, 6.85, 11.3, 2.23,
            7.5, 100, 0.84, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.19;
          break;
        case CoolingCapacity.C28_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            164 * 1.2 / 60d, 0.41 * 1, -28.0, 10.1, -12.6, 2.66, -14.4, 2.18,
            164 * 1.2 / 60d, 0.41 * 1, 31.5, 9.09, 14.2, 2.76,
            7.5, 100, 0.89, 100, 0.82, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.12;
          break;
        case CoolingCapacity.C33_5:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            191 * 1.2 / 60d, 0.63 * 1, -33.5, 10.8, -15.1, 2.82, -15.8, 2.32,
            191 * 1.2 / 60d, 0.63 * 1, 37.5, 10.8, 16.9, 3.06,
            7.5, 100, 0.895, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.12;
          break;
        case CoolingCapacity.C40_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            243 * 1.2 / 60d, 0.34 * 2, -40.0, 13.9, -18.0, 3.79, -18.7, 3.27,
            243 * 1.2 / 60d, 0.34 * 2, 45.0, 13.9, 20.3, 3.94,
            7.5, 100, 0.830, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.11;
          break;
        case CoolingCapacity.C45_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            281 * 1.2 / 60d, 0.47 * 2, -45.0, 16.9, -20.3, 4.34, -21.2, 3.71,
            281 * 1.2 / 60d, 0.47 * 2, 50.0, 16.0, 22.5, 4.30,
            7.5, 100, 0.89, 100, 0.95, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.10;
          break;
        case CoolingCapacity.C50_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            254 * 1.2 / 60d, 0.49 * 2, -50.0, 19.6, -22.5, 5.08, -23.9, 4.35,
            254 * 1.2 / 60d, 0.49 * 2, 56.0, 17.4, 25.2, 5.22,
            7.5, 100, 0.830, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.09;
          break;
        case CoolingCapacity.C56_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
             (191 + 160) * 1.2 / 60d, 0.63 * 1 + 0.39 * 1, -56.0, 17.3, -25.2, 4.97, -26.4, 4.02,
             (191 + 160) * 1.2 / 60d, 0.63 * 1 + 0.39 * 1, 63.0, 18.0, 28.4, 5.23,
            10.0, 100, 0.840, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.07;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C61_5:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (191 + 164) * 1.2 / 60d, 0.63 * 1 + 0.41 * 1, -61.5, 20.9, -27.7, 5.49, -30.2, 4.51,
            (191 + 164) * 1.2 / 60d, 0.63 * 1 + 0.41 * 1, 69.0, 19.9, 31.1, 5.84,
            10.0, 100, 0.880, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.06;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C67_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (191 + 191) * 1.2 / 60d, 0.63 * 1 * 2, -67.0, 21.7, -30.2, 5.80, -31.6, 4.78,
            (191 + 191) * 1.2 / 60d, 0.63 * 1 * 2, 77.5, 23.0, 34.9, 6.30,
            10.0, 100, 0.840, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.06;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C73_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (243 + 191) * 1.2 / 60d, 0.63 * 1 + 0.34 * 2, -73.0, 24.4, -32.9, 6.66, -34.3, 5.64,
            (243 + 191) * 1.2 / 60d, 0.63 * 1 + 0.34 * 2, 82.5, 24.7, 37.2, 7.06,
            10.0, 100, 0.890, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.06;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C77_5:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (281 + 191) * 1.2 / 60d, 0.63 * 1 + 0.47 * 2, -77.5, 27.1, -34.9, 7.16, -36.6, 6.04,
            (281 + 191) * 1.2 / 60d, 0.63 * 1 + 0.47 * 2, 90.0, 28.3, 40.5, 7.36,
            10.0, 100, 0.850, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.05;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C85_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (254 + 191) * 1.2 / 60d, 0.63 * 1 + 0.49 * 2, -85.0, 31.6, -38.3, 8.19, -40.5, 6.91,
            (254 + 191) * 1.2 / 60d, 0.63 * 1 + 0.49 * 2, 95.0, 29.1, 42.8, 8.58,
            10.0, 100, 0.840, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.05;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C90_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (254 + 243) * 1.2 / 60d, 0.49 * 2 + 0.34 * 2, -90.0, 33.6, -40.5, 9.10, -42.7, 7.81,
            (254 + 243) * 1.2 / 60d, 0.49 * 2 + 0.34 * 2, 100.0, 30.8, 45.0, 9.40,
            10.0, 100, 0.880, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.05;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C95_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (254 + 281) * 1.2 / 60d, 0.49 * 2 + 0.47 * 2, -95.0, 36.5, -42.8, 9.76, -45.2, 8.35,
            (254 + 281) * 1.2 / 60d, 0.49 * 2 + 0.47 * 2, 106.0, 33.4, 47.7, 9.87,
            10.0, 100, 0.860, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.04;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C100_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (254 + 254) * 1.2 / 60d, 0.49 * 2 * 2, -100.0, 39.3, -45.0, 10.4, -47.8, 8.91,
            (254 + 254) * 1.2 / 60d, 0.49 * 2 * 2, 112.0, 34.8, 50.4, 10.7,
            10.0, 100, 0.865, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.05;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C106_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (243 + 191 + 191) * 1.2 / 60d, 0.34 * 2 + 0.63 * 1 * 2, -106.0, 35.0, -47.7, 9.46, -49.8, 7.95,
            (243 + 191 + 191) * 1.2 / 60d, 0.34 * 2 + 0.63 * 1 * 2, 118.0, 34.4, 53.1, 10.1,
            10.0, 165, 0.800, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.04;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C112_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (281 + 191 + 191) * 1.2 / 60d, 0.47 * 2 + 0.63 * 1 * 2, -112.0, 38.6, -50.4, 10.3, -52.8, 8.64,
            (281 + 191 + 191) * 1.2 / 60d, 0.47 * 2 + 0.63 * 1 * 2, 125.0, 37.6, 56.3, 10.8,
            10.0, 170, 0.800, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.04;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C118_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (254 + 191 + 191) * 1.2 / 60d, 0.49 * 2 + 0.63 * 1 * 2, -118.0, 42.0, -53.1, 11.2, -55.9, 9.41,
            (254 + 191 + 191) * 1.2 / 60d, 0.49 * 2 + 0.63 * 1 * 2, 132.0, 39.6, 59.4, 12.0,
            10.0, 170, 0.82, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C122_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (254 + 243 + 191) * 1.2 / 60d, 0.49 * 2 + 0.34 * 2 + 0.63 * 1, -122.0, 43.4, -54.9, 11.6, -57.7, 9.90,
            (254 + 243 + 191) * 1.2 / 60d, 0.49 * 2 + 0.34 * 2 + 0.63 * 1, 140.0, 43.0, 63.0, 12.2,
            10.0, 160, 0.80, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C128_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (254 + 281 + 191) * 1.2 / 60d, 0.49 * 2 + 0.47 * 2 + 0.63 * 1, -128.0, 47.1, -57.6, 12.4, -60.7, 10.5,
            (254 + 281 + 191) * 1.2 / 60d, 0.49 * 2 + 0.47 * 2 + 0.63 * 1, 145.0, 45.1, 65.3, 12.7,
            10.0, 180, 0.80, 100, 0.965, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C136_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (254 + 254 + 191) * 1.2 / 60d, 0.49 * 2 * 2 + 0.63 * 1, -136.0, 52.0, -61.2, 13.6, -64.8, 11.5,
            (210 + 210 + 210) * 1.2 / 60d, 0.49 * 2 * 2 + 0.63 * 1, 150.0, 45.9, 67.5, 14.2,
            10.0, 150, 0.82, 100, 0.92, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C140_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (254 + 254 + 243) * 1.2 / 60d, 0.49 * 2 * 2 + 0.34 * 2, -140.0, 53.2, -63.0, 14.3, -66.6, 12.3,
            (254 + 254 + 243) * 1.2 / 60d, 0.49 * 2 * 2 + 0.34 * 2, 155.0, 47.6, 69.8, 14.7,
            10.0, 180, 0.80, 100, 1.00, iHex); //暖房配管長補正を有効にするとパラメータ推定に失敗する
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C145_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (254 + 254 + 281) * 1.2 / 60d, 0.49 * 2 * 2 + 0.47 * 2, -145.0, 56.2, -65.3, 14.7, -69.1, 12.6,
            (254 + 254 + 281) * 1.2 / 60d, 0.49 * 2 * 2 + 0.47 * 2, 160.0, 49.6, 72.0, 15.0,
            10.0, 150, 0.80, 50, 1.00, iHex); //暖房配管長補正を有効にするとパラメータ推定に失敗する
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C150_0:
          iHex = MakeIndoorUnit_Daikin(IndoorUnitType.CeilingRoundFlow_S, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            (254 + 254 + 254) * 1.2 / 60d, 0.49 * 2 * 3, -150.0, 58.9, -67.5, 15.6, -71.8, 13.3,
            (254 + 254 + 254) * 1.2 / 60d, 0.49 * 2 * 3, 165.0, 50.5, 74.3, 16.1,
            10.0, 170, 0.78, 50, 1.00, iHex); //暖房配管長補正を有効にするとパラメータ推定に失敗する
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        default:
          throw new PopoloArgumentException(
              $"No catalogue data for cooling capacity: {coolingCapacity}.",
              nameof(coolingCapacity));
      }

      vrfSystem.MaxEvaporatingTemperature = VRFSystem.NOMINAL_EVPORATING_TEMPERATURE;
      vrfSystem.MinCondensingTemperature = VRFSystem.NOMINAL_CONDENSING_TEMPERATURE;
      vrfSystem.IndoorUnitHeight = indoorUnitHeight;
      vrfSystem.UseWaterSpray = useWaterSpray;

      return vrfSystem;
    }

    #endregion

    #endregion

    #region 日立初期化

    /// <summary>Creates a Hitachi indoor unit from catalogue data.</summary>
    /// <param name="iType">Indoor unit type.</param>
    /// <param name="coolingCapacity">Cooling capacity class.</param>
    /// <returns>Initialized Hitachi indoor unit.</returns>
    /// <remarks>Data source: 2020 catalogue.</remarks>
    public static VRFUnit MakeIndoorUnit_Hitachi
      (IndoorUnitType iType, CoolingCapacity coolingCapacity)
    {
      switch (iType)
      {
        case IndoorUnitType.CeilingFourWay:
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(15.0 * 1.2 / 60d, 0.057, -2.8, 0.057, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(17.0 * 1.2 / 60d, 0.057, -3.6, 0.057, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_0) return VRFSystem.MakeIndoorUnit(20.0 * 1.2 / 60d, 0.057, -4.0, 0.057, 4.8);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(20.0 * 1.2 / 60d, 0.057, -4.5, 0.057, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_0) return VRFSystem.MakeIndoorUnit(22.0 * 1.2 / 60d, 0.057, -5.0, 0.057, 5.6);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(22.0 * 1.2 / 60d, 0.057, -5.6, 0.057, 6.3);
          if (coolingCapacity == CoolingCapacity.C6_3) return VRFSystem.MakeIndoorUnit(26.0 * 1.2 / 60d, 0.057, -6.3, 0.057, 7.5);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(27.0 * 1.2 / 60d, 0.057, -7.1, 0.057, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(27.5 * 1.2 / 60d, 0.057, -8.0, 0.057, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(29.0 * 1.2 / 60d, 0.057, -9.0, 0.057, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(36.0 * 1.2 / 60d, 0.094, -11.2, 0.094, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(37.0 * 1.2 / 60d, 0.094, -14.0, 0.094, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(37.0 * 1.2 / 60d, 0.094, -16.0, 0.094, 18.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingDoubleFlow:
          if (coolingCapacity == CoolingCapacity.C2_2) return VRFSystem.MakeIndoorUnit(10.0 * 1.2 / 60d, 0.057, -2.2, 0.057, 2.5);
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(11.0 * 1.2 / 60d, 0.057, -2.8, 0.057, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(12.0 * 1.2 / 60d, 0.057, -3.6, 0.057, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_0) return VRFSystem.MakeIndoorUnit(15.0 * 1.2 / 60d, 0.057, -4.0, 0.057, 4.8);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(15.0 * 1.2 / 60d, 0.057, -4.5, 0.057, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_0) return VRFSystem.MakeIndoorUnit(16.5 * 1.2 / 60d, 0.057, -5.0, 0.057, 5.6);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(16.5 * 1.2 / 60d, 0.057, -5.6, 0.057, 6.3);
          if (coolingCapacity == CoolingCapacity.C6_3) return VRFSystem.MakeIndoorUnit(17.0 * 1.2 / 60d, 0.057, -6.3, 0.057, 7.5);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(18.5 * 1.2 / 60d, 0.057, -7.1, 0.057, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(21.0 * 1.2 / 60d, 0.057, -8.0, 0.057, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(22.0 * 1.2 / 60d, 0.057, -9.0, 0.057, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(30.0 * 1.2 / 60d, 0.057 * 2, -11.2, 0.057 * 2, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(35.0 * 1.2 / 60d, 0.057 * 2, -14.0, 0.057 * 2, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(37.0 * 1.2 / 60d, 0.057 * 2, -16.0, 0.057 * 2, 18.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingSingleFlow:
          if (coolingCapacity == CoolingCapacity.C2_2) return VRFSystem.MakeIndoorUnit(8.5 * 1.2 / 60d, 0.050, -2.2, 0.050, 2.5);
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(9.5 * 1.2 / 60d, 0.050, -2.8, 0.050, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(10.5 * 1.2 / 60d, 0.050, -3.6, 0.050, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_0) return VRFSystem.MakeIndoorUnit(13.0 * 1.2 / 60d, 0.050, -4.0, 0.050, 4.8);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(13.0 * 1.2 / 60d, 0.050, -4.5, 0.050, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_0) return VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.050, -5.0, 0.050, 5.6);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.050, -5.6, 0.050, 6.3);
          if (coolingCapacity == CoolingCapacity.C6_3) return VRFSystem.MakeIndoorUnit(17.0 * 1.2 / 60d, 0.080, -6.3, 0.080, 7.5);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(18.5 * 1.2 / 60d, 0.080, -7.1, 0.080, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(20.0 * 1.2 / 60d, 0.080, -8.0, 0.080, 9.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingMounted:
          if (coolingCapacity == CoolingCapacity.C2_2) return VRFSystem.MakeIndoorUnit(8.5 * 1.2 / 60d, 0.157, -2.2, 0.157, 2.5);
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(9.5 * 1.2 / 60d, 0.157, -2.8, 0.157, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(10.5 * 1.2 / 60d, 0.157, -3.6, 0.157, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_0) return VRFSystem.MakeIndoorUnit(13.0 * 1.2 / 60d, 0.157, -4.0, 0.157, 4.8);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(13.0 * 1.2 / 60d, 0.157, -4.5, 0.157, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_0) return VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.157, -5.0, 0.157, 5.6);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(17.0 * 1.2 / 60d, 0.157, -5.6, 0.157, 6.3);
          if (coolingCapacity == CoolingCapacity.C6_3) return VRFSystem.MakeIndoorUnit(18.5 * 1.2 / 60d, 0.190, -6.3, 0.190, 7.5);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(20.0 * 1.2 / 60d, 0.190, -7.1, 0.190, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(21.5 * 1.2 / 60d, 0.190, -8.0, 0.190, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(30.0 * 1.2 / 60d, 0.190, -9.0, 0.190, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(30.0 * 1.2 / 60d, 0.259, -11.2, 0.259, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(33.5 * 1.2 / 60d, 0.259, -14.0, 0.259, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(36.0 * 1.2 / 60d, 0.259, -16.0, 0.259, 18.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingConcealedDuct:
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(13.0 * 1.2 / 60d, 0.157, -4.5, 0.157, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_0) return VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.157, -5.0, 0.157, 5.6);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.157, -5.6, 0.157, 6.3);
          if (coolingCapacity == CoolingCapacity.C6_3) return VRFSystem.MakeIndoorUnit(17.0 * 1.2 / 60d, 0.190, -6.3, 0.190, 7.5);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(18.5 * 1.2 / 60d, 0.190, -7.1, 0.190, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(20.0 * 1.2 / 60d, 0.190, -8.0, 0.190, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(21.5 * 1.2 / 60d, 0.190, -9.0, 0.190, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(30.0 * 1.2 / 60d, 0.259, -11.2, 0.259, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(33.5 * 1.2 / 60d, 0.259, -14.0, 0.259, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(36.0 * 1.2 / 60d, 0.259, -16.0, 0.259, 18.0);
          if (coolingCapacity == CoolingCapacity.C22_4) return VRFSystem.MakeIndoorUnit(63.0 * 1.2 / 60d, 0.840, -22.4, 0.840, 25.0);
          if (coolingCapacity == CoolingCapacity.C28_0) return VRFSystem.MakeIndoorUnit(80.0 * 1.2 / 60d, 0.840, -28.0, 0.840, 31.5);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingSuspended:
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(13.0 * 1.2 / 60d, 0.030, -3.6, 0.030, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_0) return VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.040, -4.0, 0.040, 4.8);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.040, -4.5, 0.040, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_0) return VRFSystem.MakeIndoorUnit(15.0 * 1.2 / 60d, 0.050, -5.0, 0.050, 5.6);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(15.0 * 1.2 / 60d, 0.050, -5.6, 0.050, 6.3);
          if (coolingCapacity == CoolingCapacity.C6_3) return VRFSystem.MakeIndoorUnit(18.0 * 1.2 / 60d, 0.050, -6.3, 0.050, 7.5);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(19.0 * 1.2 / 60d, 0.050, -7.1, 0.050, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(21.0 * 1.2 / 60d, 0.060, -8.0, 0.060, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(23.5 * 1.2 / 60d, 0.090, -9.0, 0.090, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(30.0 * 1.2 / 60d, 0.100, -11.2, 0.100, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(35.0 * 1.2 / 60d, 0.160, -14.0, 0.160, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(37.0 * 1.2 / 60d, 0.160, -16.0, 0.160, 18.0);
          if (coolingCapacity == CoolingCapacity.C22_4) return VRFSystem.MakeIndoorUnit(58.0 * 1.2 / 60d, 0.200 * 2, -22.4, 0.200 * 2, 25.0);
          if (coolingCapacity == CoolingCapacity.C28_0) return VRFSystem.MakeIndoorUnit(66.0 * 1.2 / 60d, 0.200 * 2, -28.0, 0.200 * 2, 31.5);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.WallMounted:
          if (coolingCapacity == CoolingCapacity.C2_2) return VRFSystem.MakeIndoorUnit(9.0 * 1.2 / 60d, 0.030, -2.2, 0.030, 2.5);
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(9.0 * 1.2 / 60d, 0.030, -2.8, 0.030, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(9.0 * 1.2 / 60d, 0.030, -3.6, 0.030, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_0) return VRFSystem.MakeIndoorUnit(14.0 * 1.2 / 60d, 0.060, -4.0, 0.060, 4.8);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(14.0 * 1.2 / 60d, 0.060, -4.5, 0.060, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_0) return VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.040, -5.0, 0.040, 5.6);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.040, -5.6, 0.040, 6.3);
          if (coolingCapacity == CoolingCapacity.C6_3) return VRFSystem.MakeIndoorUnit(17.5 * 1.2 / 60d, 0.060, -6.3, 0.060, 7.5);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(18.5 * 1.2 / 60d, 0.070, -7.1, 0.070, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(20.0 * 1.2 / 60d, 0.080, -8.0, 0.080, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(21.5 * 1.2 / 60d, 0.080, -9.0, 0.080, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(23.0 * 1.2 / 60d, 0.090, -11.2, 0.090, 12.5); //伝熱面積初期化エラー：蒸発器凝縮器、両方ダメ
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.FloorStandingLowboy:
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(6.5 * 1.2 / 60d, 0.020, -2.8, 0.020, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(9.0 * 1.2 / 60d, 0.030, -3.6, 0.030, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(11.0 * 1.2 / 60d, 0.035, -4.5, 0.035, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(14.0 * 1.2 / 60d, 0.040, -5.6, 0.040, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(15.5 * 1.2 / 60d, 0.045, -7.1, 0.045, 8.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.ConcealedLowboy:
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(6.5 * 1.2 / 60d, 0.020, -2.8, 0.020, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(9.0 * 1.2 / 60d, 0.030, -3.6, 0.030, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(11.0 * 1.2 / 60d, 0.035, -4.5, 0.035, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(14.0 * 1.2 / 60d, 0.040, -5.6, 0.040, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(15.5 * 1.2 / 60d, 0.045, -7.1, 0.045, 8.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.FloorMount:
          if (coolingCapacity == CoolingCapacity.C5_0) return VRFSystem.MakeIndoorUnit(16.0 * 1.2 / 60d, 0.040, -5.0, 0.040, 5.6);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(16.0 * 1.2 / 60d, 0.040, -5.6, 0.040, 6.3);
          if (coolingCapacity == CoolingCapacity.C6_3) return VRFSystem.MakeIndoorUnit(17.0 * 1.2 / 60d, 0.040, -6.3, 0.040, 7.5);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(18.5 * 1.2 / 60d, 0.050, -7.1, 0.050, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(18.5 * 1.2 / 60d, 0.050, -8.0, 0.050, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(22.0 * 1.2 / 60d, 0.080, -9.0, 0.080, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(24.0 * 1.2 / 60d, 0.090, -11.2, 0.090, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(29.0 * 1.2 / 60d, 0.130, -14.0, 0.130, 16.0); //伝熱面積初期化エラー：蒸発器凝縮器、両方ダメ
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(31.0 * 1.2 / 60d, 0.150, -16.0, 0.150, 18.0); //伝熱面積初期化エラー：凝縮器がダメ
          if (coolingCapacity == CoolingCapacity.C22_4) return VRFSystem.MakeIndoorUnit(49.0 * 1.2 / 60d, 0.330, -22.4, 0.330, 25.0);
          if (coolingCapacity == CoolingCapacity.C28_0) return VRFSystem.MakeIndoorUnit(69.0 * 1.2 / 60d, 0.390, -28.0, 0.390, 31.5);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        default:
          throw new PopoloArgumentException(
              $"Unsupported indoor unit type: {iType}.", nameof(iType));
      }
    }

    #region 室外機SS

    /// <summary>Creates a Hitachi SS outdoor unit from catalogue data.</summary>
    /// <param name="coolingCapacity">Cooling capacity class.</param>
    /// <param name="indoorUnitHeight">Installation height of indoor units relative to the outdoor unit [m].</param>
    /// <param name="useWaterSpray">True to enable water spray on the outdoor unit condenser.</param>
    /// <returns>Initialized Hitachi SS outdoor unit system.</returns>
    /// <remarks>Data source: 2018 product guidebook.</remarks>
    private static VRFSystem MakeOutdoorUnit_HitachiSS
      (CoolingCapacity coolingCapacity, double indoorUnitHeight, bool useWaterSpray)
    {
      //冷媒はR410a
      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);

      VRFSystem vrfSystem;
      VRFUnit iHex = MakeIndoorUnit_Hitachi(IndoorUnitType.CeilingFourWay, CoolingCapacity.C11_2);
      switch (coolingCapacity)
      {
        case CoolingCapacity.C22_4:
          vrfSystem = new VRFSystem(r410a,
            165 * 1.2 / 60d, 0.26 * 1, -22.4, 6.50, -10.1, 1.84, -10.6, 1.53,
            165 * 1.2 / 60d, 0.26 * 1, 25.0, 6.22, 11.3, 1.99,
            7.5, 100, 0.90, 100, 0.92, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.15;
          break;
        case CoolingCapacity.C28_0:
          vrfSystem = new VRFSystem(r410a,
           170 * 1.2 / 60d, 0.28 * 1, -28.0, 10.1, -12.6, 2.37, -13.2, 1.94,
           170 * 1.2 / 60d, 0.28 * 1, 31.5, 8.93, 14.2, 2.55,
           7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.13;
          break;
        case CoolingCapacity.C33_5:
          vrfSystem = new VRFSystem(r410a,
            190 * 1.2 / 60d, 0.42 * 1, -33.5, 10.8, -15.1, 2.89, -15.9, 2.29,
            190 * 1.2 / 60d, 0.42 * 1, 37.5, 11.7, 17.0, 3.24,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.10;
          break;
        case CoolingCapacity.C40_0:
          vrfSystem = new VRFSystem(r410a,
            239 * 1.2 / 60d, 0.33 * 2, -40.0, 14.7, -18.0, 3.54, -18.7, 2.89,
            239 * 1.2 / 60d, 0.33 * 2, 45.0, 15.2, 20.3, 3.43,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.09;
          break;
        case CoolingCapacity.C45_0:
          vrfSystem = new VRFSystem(r410a,
            256 * 1.2 / 60d, 0.39 * 2, -45.0, 15.7, -20.3, 4.10, -21.3, 3.37,
            256 * 1.2 / 60d, 0.39 * 2, 50.0, 17.4, 22.7, 3.96,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.07;
          break;
        case CoolingCapacity.C50_0:
          vrfSystem = new VRFSystem(r410a,
            256 * 1.2 / 60d, 0.39 * 2, -50.0, 19.0, -22.7, 4.69, -23.6, 3.85,
            256 * 1.2 / 60d, 0.39 * 2, 56.0, 20.2, 25.6, 4.54,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.06;
          break;
        case CoolingCapacity.C56_0:
          vrfSystem = new VRFSystem(r410a,
            329 * 1.2 / 60d, 0.48 * 2, -56.0, 23.6, -25.2, 5.27, -26.3, 4.21,
            329 * 1.2 / 60d, 0.48 * 2, 63.0, 23.5, 28.4, 5.14,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.06;
          break;
        case CoolingCapacity.C61_5:
          vrfSystem = new VRFSystem(r410a,
            329 * 1.2 / 60d, 0.48 * 2, -61.5, 22.6, -27.7, 6.06, -29.1, 4.74,
            329 * 1.2 / 60d, 0.48 * 2, 69.0, 22.9, 31.1, 5.65,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.05;
          break;
        case CoolingCapacity.C67_0:
          vrfSystem = new VRFSystem(r410a,
            348 * 1.2 / 60d, 0.56 * 2, -67.0, 24.5, -30.2, 6.44, -31.7, 5.04,
            348 * 1.2 / 60d, 0.56 * 2, 77.5, 29.0, 34.9, 6.37,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.05;
          break;
        case CoolingCapacity.C73_0:
          vrfSystem = new VRFSystem(r410a,
            (239 + 190) * 1.2 / 60d, 0.33 * 2 + 0.42 * 1, -73.0, 25.2, -33.1, 6.43, -34.6, 5.18,
            (239 + 190) * 1.2 / 60d, 0.33 * 2 + 0.42 * 1, 82.5, 26.9, 37.3, 6.67,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.05;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C77_5:
          vrfSystem = new VRFSystem(r410a,
            (256 + 190) * 1.2 / 60d, 0.39 * 2 + 0.42 * 1, -77.5, 25.8, -35.4, 6.99, -37.2, 5.66,
            (256 + 190) * 1.2 / 60d, 0.39 * 2 + 0.42 * 1, 90.0, 30.6, 40.5, 7.39,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.04;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C85_0:
          vrfSystem = new VRFSystem(r410a,
            (256 + 190) * 1.2 / 60d, 0.39 * 2 + 0.42 * 1, -85.0, 30.8, -38.4, 7.73, -40.1, 6.25,
            (256 + 190) * 1.2 / 60d, 0.39 * 2 + 0.42 * 1, 95.0, 32.9, 42.8, 7.83,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.04;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C90_0:
          vrfSystem = new VRFSystem(r410a,
            (256 + 239) * 1.2 / 60d, 0.39 * 2 + 0.33 * 2, -90.0, 33.8, -40.7, 8.23, -42.3, 6.74,
            (256 + 239) * 1.2 / 60d, 0.39 * 2 + 0.33 * 2, 100.0, 34.7, 45.9, 7.97,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.04;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C95_0:
          vrfSystem = new VRFSystem(r410a,
            256 * 2 * 1.2 / 60d, 0.39 * 2 * 2, -95.0, 34.7, -43.0, 8.79, -44.9, 7.22,
            256 * 2 * 1.2 / 60d, 0.39 * 2 * 2, 106.0, 37.6, 48.3, 8.50,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C100_0:
          vrfSystem = new VRFSystem(r410a,
            256 * 2 * 1.2 / 60d, 0.39 * 2 * 2, -100.0, 38.1, -45.4, 9.38, -47.2, 7.70,
            256 * 2 * 1.2 / 60d, 0.39 * 2 * 2, 112.0, 40.4, 51.2, 9.08,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C106_0:
          vrfSystem = new VRFSystem(r410a,
            (348 + 239) * 1.2 / 60d, 0.56 * 2 + 0.33 * 2, -106.0, 38.5, -48.2, 9.98, -50.4, 7.93,
            (348 + 239) * 1.2 / 60d, 0.56 * 2 + 0.33 * 2, 118.0, 40.9, 55.2, 9.80,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C112_0:
          vrfSystem = new VRFSystem(r410a,
            (329 + 256) * 1.2 / 60d, 0.48 * 2 + 0.39 * 2, -112.0, 42.0, -50.4, 10.8, -52.7, 8.59,
            (329 + 256) * 1.2 / 60d, 0.48 * 2 + 0.39 * 2, 125.0, 43.1, 56.7, 10.2,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C118_0:
          vrfSystem = new VRFSystem(r410a,
            (348 + 256) * 1.2 / 60d, 0.56 * 2 + 0.39 * 2, -118.0, 44.2, -53.1, 11.2, -55.5, 8.93,
            (348 + 256) * 1.2 / 60d, 0.56 * 2 + 0.39 * 2, 132.0, 48.1, 60.5, 10.9,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C122_0:
          vrfSystem = new VRFSystem(r410a,
            329 * 2 * 1.2 / 60d, 0.48 * 2 * 2, -122.0, 44.5, -55.4, 12.1, -58.2, 9.48,
            329 * 2 * 1.2 / 60d, 0.48 * 2 * 2, 140.0, 47.2, 63.0, 11.5,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C128_0:
          vrfSystem = new VRFSystem(r410a,
            (348 + 329) * 1.2 / 60d, 0.56 * 2 + 0.48 * 2, -128.0, 46.7, -57.9, 12.5, -60.8, 9.78,
            (348 + 329) * 1.2 / 60d, 0.56 * 2 + 0.48 * 2, 145.0, 50.9, 66.0, 12.0,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C136_0:
          vrfSystem = new VRFSystem(r410a,
            348 * 2 * 1.2 / 60d, 0.56 * 2 * 2, -136.0, 49.6, -61.4, 13.2, -64.4, 10.3,
            348 * 2 * 1.2 / 60d, 0.56 * 2 * 2, 150.0, 56.0, 69.8, 12.7,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 2;
          break;
        case CoolingCapacity.C140_0:
          vrfSystem = new VRFSystem(r410a,
            (256 * 2 + 239) * 1.2 / 60d, 0.39 * 2 * 2 + 0.33 * 2, -140.0, 52.8, -63.4, 12.9, -65.9, 10.6,
            (256 * 2 + 239) * 1.2 / 60d, 0.39 * 2 * 2 + 0.33 * 2, 155.0, 54.2, 71.5, 12.5,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C145_0:
          vrfSystem = new VRFSystem(r410a,
            256 * 3 * 1.2 / 60d, 0.39 * 2 * 3, -145.0, 53.8, -66.2, 13.6, -69.0, 11.2,
            256 * 3 * 1.2 / 60d, 0.39 * 2 * 3, 160.0, 56.4, 73.9, 13.0,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        case CoolingCapacity.C150_0:
          vrfSystem = new VRFSystem(r410a,
            256 * 3 * 1.2 / 60d, 0.39 * 2 * 3, -150.0, 57.1, -68.1, 14.1, -70.8, 11.6,
            256 * 3 * 1.2 / 60d, 0.39 * 2 * 3, 165.0, 58.5, 76.8, 13.6,
            7.5, 100, 0.90, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.03;
          vrfSystem.NumberOfOutdoorUnitDivisions = 3;
          break;
        default:
          throw new PopoloArgumentException(
              $"No catalogue data for cooling capacity: {coolingCapacity}.",
              nameof(coolingCapacity));
      }

      vrfSystem.MaxEvaporatingTemperature = VRFSystem.NOMINAL_EVPORATING_TEMPERATURE;
      vrfSystem.MinCondensingTemperature = VRFSystem.NOMINAL_CONDENSING_TEMPERATURE;
      vrfSystem.IndoorUnitHeight = indoorUnitHeight;
      vrfSystem.UseWaterSpray = useWaterSpray;

      return vrfSystem;
    }

    #endregion

    #endregion

    #region 東芝初期化

    /// <summary>Creates a Toshiba indoor unit from catalogue data.</summary>
    /// <param name="iType">Indoor unit type.</param>
    /// <param name="coolingCapacity">Cooling capacity class.</param>
    /// <returns>Initialized Toshiba indoor unit.</returns>
    /// <remarks>Data source: 2020 catalogue.</remarks>
    public static VRFUnit MakeIndoorUnit_Toshiba
      (IndoorUnitType iType, CoolingCapacity coolingCapacity)
    {
      switch (iType)
      {
        case IndoorUnitType.CeilingFourWay:
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(14.1 * 1.2 / 60d, 0.020, -2.8, 0.020, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(14.1 * 1.2 / 60d, 0.020, -3.6, 0.020, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(16.0 * 1.2 / 60d, 0.022, -4.5, 0.022, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(17.5 * 1.2 / 60d, 0.026, -5.6, 0.026, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(20.8 * 1.2 / 60d, 0.045, -7.1, 0.045, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(22.9 * 1.2 / 60d, 0.048, -8.0, 0.048, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(22.9 * 1.2 / 60d, 0.048, -9.0, 0.048, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(36.4 * 1.2 / 60d, 0.125, -11.2, 0.125, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(37.7 * 1.2 / 60d, 0.135, -14.0, 0.135, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(37.7 * 1.2 / 60d, 0.137, -16.0, 0.137, 18.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingDoubleFlow:
          if (coolingCapacity == CoolingCapacity.C2_2) return VRFSystem.MakeIndoorUnit(9.3 * 1.2 / 60d, 0.024, -2.2, 0.024, 2.5);
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(9.3 * 1.2 / 60d, 0.024, -2.8, 0.024, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(9.3 * 1.2 / 60d, 0.024, -3.6, 0.024, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(10.0 * 1.2 / 60d, 0.026, -4.5, 0.026, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(15.0 * 1.2 / 60d, 0.034, -5.6, 0.034, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(17.5 * 1.2 / 60d, 0.045, -7.1, 0.045, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(17.5 * 1.2 / 60d, 0.045, -8.0, 0.045, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(21.0 * 1.2 / 60d, 0.055, -9.0, 0.055, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(29.0 * 1.2 / 60d, 0.081, -11.2, 0.081, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(30.0 * 1.2 / 60d, 0.091, -14.0, 0.091, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(34.0 * 1.2 / 60d, 0.131, -16.0, 0.131, 18.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingSingleFlow:
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(12.5 * 1.2 / 60d, 0.039, -4.5, 0.039, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(13.0 * 1.2 / 60d, 0.042, -5.6, 0.042, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(19.0 * 1.2 / 60d, 0.064, -7.1, 0.064, 8.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingMounted:
          if (coolingCapacity == CoolingCapacity.C2_2) return VRFSystem.MakeIndoorUnit(8.0 * 1.2 / 60d, 0.033, -2.2, 0.033, 2.5);
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(8.0 * 1.2 / 60d, 0.033, -2.8, 0.033, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(9.5 * 1.2 / 60d, 0.039, -3.6, 0.039, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(10.8 * 1.2 / 60d, 0.039, -4.5, 0.039, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(13.0 * 1.2 / 60d, 0.050, -5.6, 0.050, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(19.0 * 1.2 / 60d, 0.060, -7.1, 0.060, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(19.0 * 1.2 / 60d, 0.060, -8.0, 0.060, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(21.0 * 1.2 / 60d, 0.071, -9.0, 0.071, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(27.0 * 1.2 / 60d, 0.107, -11.2, 0.107, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(27.0 * 1.2 / 60d, 0.128, -14.0, 0.128, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(33.0 * 1.2 / 60d, 0.128, -16.0, 0.128, 18.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingConcealedDuct:
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(13.3 * 1.2 / 60d, 0.058, -5.6, 0.058, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(20.0 * 1.2 / 60d, 0.094, -7.1, 0.094, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(20.0 * 1.2 / 60d, 0.094, -8.0, 0.094, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(20.0 * 1.2 / 60d, 0.094, -9.0, 0.094, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(32.0 * 1.2 / 60d, 0.160, -11.2, 0.160, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(35.0 * 1.2 / 60d, 0.188, -14.0, 0.188, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(40.0 * 1.2 / 60d, 0.245, -16.0, 0.245, 18.0);
          if (coolingCapacity == CoolingCapacity.C22_4) return VRFSystem.MakeIndoorUnit(63.3 * 1.2 / 60d, 0.360, -22.4, 0.360, 25.0);
          if (coolingCapacity == CoolingCapacity.C28_0) return VRFSystem.MakeIndoorUnit(80.0 * 1.2 / 60d, 0.560, -28.0, 0.560, 31.5);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.CeilingSuspended:
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(14.0 * 1.2 / 60d, 0.033, -4.5, 0.033, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(15.0 * 1.2 / 60d, 0.034, -5.6, 0.034, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(23.5 * 1.2 / 60d, 0.067, -7.1, 0.067, 8.0);
          if (coolingCapacity == CoolingCapacity.C8_0) return VRFSystem.MakeIndoorUnit(23.5 * 1.2 / 60d, 0.067, -8.0, 0.067, 9.0);
          if (coolingCapacity == CoolingCapacity.C9_0) return VRFSystem.MakeIndoorUnit(24.5 * 1.2 / 60d, 0.074, -9.0, 0.074, 10.0);
          if (coolingCapacity == CoolingCapacity.C11_2) return VRFSystem.MakeIndoorUnit(31.0 * 1.2 / 60d, 0.083, -11.2, 0.083, 12.5);
          if (coolingCapacity == CoolingCapacity.C14_0) return VRFSystem.MakeIndoorUnit(31.0 * 1.2 / 60d, 0.083, -14.0, 0.083, 16.0);
          if (coolingCapacity == CoolingCapacity.C16_0) return VRFSystem.MakeIndoorUnit(34.0 * 1.2 / 60d, 0.111, -16.0, 0.111, 18.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.WallMounted:
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(10.0 * 1.2 / 60d, 0.019, -2.8, 0.019, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(10.0 * 1.2 / 60d, 0.019, -3.6, 0.019, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(14.0 * 1.2 / 60d, 0.028, -4.5, 0.028, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(14.0 * 1.2 / 60d, 0.028, -5.6, 0.028, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(17.0 * 1.2 / 60d, 0.044, -7.1, 0.044, 8.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.ConcealedLowboy:
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(7.7 * 1.2 / 60d, 0.049, -2.8, 0.049, 3.2);
          if (coolingCapacity == CoolingCapacity.C3_6) return VRFSystem.MakeIndoorUnit(7.7 * 1.2 / 60d, 0.049, -3.6, 0.049, 4.0);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(12.3 * 1.2 / 60d, 0.078, -4.5, 0.078, 5.0);
          if (coolingCapacity == CoolingCapacity.C5_6) return VRFSystem.MakeIndoorUnit(12.3 * 1.2 / 60d, 0.078, -5.6, 0.078, 6.3);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(15.8 * 1.2 / 60d, 0.083, -7.1, 0.083, 8.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        case IndoorUnitType.FloorStandingLowboy:
          if (coolingCapacity == CoolingCapacity.C2_8) return VRFSystem.MakeIndoorUnit(8.0 * 1.2 / 60d, 0.053, -2.8, 0.053, 3.2);
          if (coolingCapacity == CoolingCapacity.C4_5) return VRFSystem.MakeIndoorUnit(15.0 * 1.2 / 60d, 0.090, -4.5, 0.090, 5.0);
          if (coolingCapacity == CoolingCapacity.C7_1) return VRFSystem.MakeIndoorUnit(18.0 * 1.2 / 60d, 0.100, -7.1, 0.100, 8.0);
          throw new PopoloArgumentException(
            $"No catalogue data for {iType} with cooling capacity {coolingCapacity}.",
            nameof(coolingCapacity));
        default:
          throw new PopoloArgumentException(
              $"Unsupported indoor unit type: {iType}.", nameof(iType));
      }
    }

    #region 室外機MMY(スーパーモジュールマルチ)

    /// <summary>Creates a Toshiba MMY outdoor unit from catalogue data.</summary>
    /// <param name="coolingCapacity">Cooling capacity class.</param>
    /// <param name="indoorUnitHeight">Installation height of indoor units relative to the outdoor unit [m].</param>
    /// <param name="useWaterSpray">True to enable water spray on the outdoor unit condenser.</param>
    /// <returns>Initialized Toshiba MMY outdoor unit system.</returns>
    /// <remarks>Data source: manufacturer technical data sheet.</remarks>
    private static VRFSystem MakeOutdoorUnit_ToshibaMMY
      (CoolingCapacity coolingCapacity, double indoorUnitHeight, bool useWaterSpray)
    {
      //冷媒はR410a
      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);

      VRFSystem vrfSystem;
      VRFUnit iHex;
      switch (coolingCapacity)
      {
        case CoolingCapacity.C22_4:
          iHex = MakeIndoorUnit_Toshiba(IndoorUnitType.CeilingFourWay, CoolingCapacity.C11_2);
          vrfSystem = new VRFSystem(r410a,
            165 * 1.2 / 60d, 1.00, -22.4, 6.65, -10.2, 1.90, -10.4, 1.48,
            165 * 1.2 / 60d, 1.00, 22.4, 5.53, 10.1, 1.89,
            7.5, 100, 0.88, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 2.2 / 22.4; //技術資料の「能力可変範囲」。連続的に能力変化できる範囲なのかは不明
          break;
        case CoolingCapacity.Miyata22_4:
          iHex = MakeIndoorUnit_Toshiba(IndoorUnitType.CeilingFourWay, CoolingCapacity.C5_6);
          vrfSystem = new VRFSystem(r410a,
            //165 * 1.2 / 60d, 1.00, -20.83, 4.43, -12.15, 1.90, -10.4, 1.48, //中負荷中温条件をカタログ情報で補完した初期化
            165 * 1.2 / 60d, 1.00, -20.83, 4.43, -12.15, 1.90, //中負荷中温条件を削除した初期化
            165 * 1.2 / 60d, 1.00, 22.81, 4.62, 11.00, 1.98,
            7.5, 100, 0.88, 100, 1.00, iHex);
          vrfSystem.MinimumPartialLoadRate = 0.25; //宮田さんの論文によれば、L条件では発停になったとのこと。
          break;

        default:
          throw new PopoloArgumentException(
              $"No catalogue data for cooling capacity: {coolingCapacity}.",
              nameof(coolingCapacity));
      }
      vrfSystem.IndoorUnitHeight = indoorUnitHeight;
      vrfSystem.UseWaterSpray = useWaterSpray;

      return vrfSystem;
    }

    #endregion

    #endregion

  }
}