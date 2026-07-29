/* MultiZoneCO2Model.cs
 *
 * Copyright (C) 2026 E.Togashi
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
using System.Collections.Generic;

using Popolo.Core.Exceptions;
using Popolo.Core.Numerics.LinearAlgebra;
using Popolo.Core.Physics;

namespace Popolo.Core.Building.AirQuality
{
  /// <summary>
  /// Multi-zone indoor CO2 concentration model.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Each zone is treated as well-mixed and only the zone air stores CO2.
  /// The coupled mass balances are advanced with the implicit (backward)
  /// Euler scheme, which is unconditionally stable for any time step. For a
  /// single zone with constant inputs the result converges to Seidel's
  /// analytic solution (<see cref="CO2Balance.GetConcentration"/>) as the
  /// time step is refined.
  /// </para>
  /// <para>
  /// When the model holds an <see cref="IReadOnlyBuildingThermalModel"/> and
  /// a zone is bound to an <see cref="IReadOnlyZone"/>, the outdoor air
  /// ventilation rate (<see cref="IReadOnlyZone.VentilationRate"/>) and the
  /// directed inter-zone air flows
  /// (<see cref="IReadOnlyBuildingThermalModel.GetAirFlow"/>) are read from
  /// the thermal model at every update, so HVAC-schedule-dependent air flow
  /// changes are followed automatically. Inter-zone advection is considered
  /// only between pairs of zones that are both registered in this model and
  /// bound to the thermal model; each inflow displaces an equal amount of
  /// zone air whose destination is not tracked. Whether the overall air mass
  /// balance is appropriate depends on the thermal model configuration and
  /// is the responsibility of the user (the same convention as
  /// <c>BuildingThermalModel.SetAirFlow</c>).
  /// </para>
  /// <para>
  /// Air flows of the thermal model are given as mass flow rates [kg/s] and
  /// are converted to volumetric flow rates with
  /// <see cref="PhysicsConstants.NominalMoistAirDensity"/>.
  /// </para>
  /// </remarks>
  public class MultiZoneCO2Model : IReadOnlyMultiZoneCO2Model
  {

    #region インスタンス変数・プロパティ

    /// <summary>Zones of the model.</summary>
    private readonly CO2ModelZone[] zones;

    /// <summary>Thermal model used to read air flows. Null when not linked.</summary>
    private readonly IReadOnlyBuildingThermalModel? bModel;

    /// <summary>MultiRooms indices of the bound zones (-1 for unbound zones).</summary>
    private readonly int[] rmIndices;

    /// <summary>Zone indices of the bound zones (-1 for unbound zones).</summary>
    private readonly int[] znIndices;

    /// <summary>Coefficient matrix of the implicit scheme.</summary>
    private readonly Matrix matA;

    /// <summary>Right-hand side / solution vector of the implicit scheme.</summary>
    private readonly Vector vecB;

    /// <inheritdoc />
    public IReadOnlyList<IReadOnlyCO2ModelZone> Zones { get { return zones; } }

    /// <summary>Gets or sets the outdoor CO2 concentration [m³/m³].</summary>
    public double OutdoorCO2Level { get; set; } = 400e-6;

    /// <summary>Gets or sets the outdoor CO2 concentration [ppm].</summary>
    public double OutdoorCO2Level_PPM
    {
      get { return OutdoorCO2Level * 1e6; }
      set { OutdoorCO2Level = value * 1e-6; }
    }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance without a thermal model link.</summary>
    /// <param name="zones">Zones of the model.</param>
    public MultiZoneCO2Model(CO2ModelZone[] zones) : this(zones, null) { }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="zones">Zones of the model.</param>
    /// <param name="buildingModel">
    /// Thermal model used to read the ventilation rates and the inter-zone
    /// air flows of the bound zones, or null.
    /// </param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="zones"/> is null or empty, when a zone is
    /// bound but <paramref name="buildingModel"/> is null, or when a bound
    /// zone is not found in <paramref name="buildingModel"/>.
    /// </exception>
    public MultiZoneCO2Model(CO2ModelZone[] zones, IReadOnlyBuildingThermalModel? buildingModel)
    {
      if (zones == null || zones.Length == 0)
        throw new PopoloArgumentException(
          "zones must contain at least one zone.", nameof(zones));

      this.zones = zones;
      this.bModel = buildingModel;

      //紐づけゾーンの(MultiRoom, Zone)インデックスを解決
      rmIndices = new int[zones.Length];
      znIndices = new int[zones.Length];
      for (int i = 0; i < zones.Length; i++)
      {
        rmIndices[i] = znIndices[i] = -1;
        IReadOnlyZone? bz = zones[i].BoundZone;
        if (bz == null) continue;

        if (buildingModel == null)
          throw new PopoloArgumentException(
            $"Zone '{zones[i].Name}' is bound to a thermal model zone but "
            + "buildingModel is null.", nameof(buildingModel));

        for (int r = 0; r < buildingModel.MultiRoom.Length && rmIndices[i] < 0; r++)
        {
          IReadOnlyList<IReadOnlyZone> zns = buildingModel.MultiRoom[r].Zones;
          for (int z = 0; z < zns.Count; z++)
          {
            if (ReferenceEquals(zns[z], bz))
            {
              rmIndices[i] = r;
              znIndices[i] = z;
              break;
            }
          }
        }
        if (rmIndices[i] < 0)
          throw new PopoloArgumentException(
            $"The zone bound to '{zones[i].Name}' was not found in buildingModel.",
            nameof(zones));
      }

      matA = new Matrix(zones.Length, zones.Length);
      vecB = new Vector(zones.Length);
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>
    /// Advances the CO2 concentrations of all zones by one time step.
    /// </summary>
    /// <param name="timeStep">Time step [s].</param>
    /// <remarks>
    /// Call after updating the thermal model so that the latest ventilation
    /// rates and inter-zone air flows are used.
    /// </remarks>
    public void Update(double timeStep)
    {
      if (timeStep <= 0)
        throw new PopoloArgumentException("timeStep must be positive.", nameof(timeStep));

      int n = zones.Length;

      //後退Euler法の連立一次方程式を構築
      //V_i/Δt (C_i' - C_i) = Σ_j q_ji (C_j' - C_i') + qA_i (C_OA - C_i') + qB_i (C_B,i - C_i') + G_i
      for (int i = 0; i < n; i++)
      {
        CO2ModelZone zn = zones[i];

        //A系統: 紐づけゾーンはVentilationRate[kg/s]を体積流量に換算して外気濃度で流入
        double qA = 0.0;
        if (zn.BoundZone != null)
          qA = Math.Max(0, zn.BoundZone.VentilationRate) / PhysicsConstants.NominalMoistAirDensity;

        //B系統: 任意濃度の追加換気
        double qB = Math.Max(0, zn.AuxiliaryVentilationRate);

        double diag = zn.Volume / timeStep + qA + qB;
        vecB[i] = zn.Volume / timeStep * zn.CO2Level
          + qA * OutdoorCO2Level
          + qB * zn.AuxiliaryVentilationCO2Level
          + zn.CO2Generation;

        //ゾーン間移流（紐づけ済みペアのみ）
        for (int j = 0; j < n; j++)
        {
          if (i == j || rmIndices[i] < 0 || rmIndices[j] < 0)
          {
            if (i != j) matA[i, j] = 0.0;
            continue;
          }
          double qJI = Math.Max(0, bModel!.GetAirFlow(rmIndices[j], znIndices[j], rmIndices[i], znIndices[i]))
            / PhysicsConstants.NominalMoistAirDensity;
          matA[i, j] = -qJI;
          diag += qJI;
        }
        matA[i, i] = diag;
      }

      //求解して濃度を更新
      LinearAlgebraOperations.SolveLinearEquations(matA, vecB);
      for (int i = 0; i < n; i++) zones[i].CO2Level = vecB[i];
    }

    #endregion

  }
}
