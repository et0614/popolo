/* MultiConnectedWaterTank.cs
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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using Popolo.Core.Exceptions;
using Popolo.Core.Numerics.LinearAlgebra;
using Popolo.Core.Physics;
using System;

namespace Popolo.Core.HVAC.Storage
{
  /// <summary>Multi-tank series-connected fully-mixed thermal storage.</summary>
  public class MultiConnectedWaterTank : IReadOnlyMultiConnectedWaterTank
  {

    #region インスタンス変数・プロパティ

    /// <summary>Tridiagonal coefficient matrix (3×n).</summary>
    private IMatrix wMat;

    /// <summary>Tank temperatures [°C].</summary>
    private IVector temperatures, temperatures_Back;

    /// <summary>True while a forecast calculation is in progress.</summary>
    private bool isForecasting = false;

    /// <summary>Tank volumes [m³].</summary>
    private double[] volumes;

    /// <summary>Heat loss coefficients [kW/K].</summary>
    private double[] heatLossCoefficients;

    /// <summary>Time step [s].</summary>
    private double timeStep = 60;

    /// <summary>Gets or sets the time step [s].</summary>
    public double TimeStep
    {
      get { return timeStep; }
      set { if (0 < value) timeStep = value; }
    }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    public double WaterInletTemperature { get; private set; }

    /// <summary>Gets the outlet water temperature [°C].</summary>
    public double WaterOutletTemperarture
    {
      get
      {
        if (IsForwardFlow) return temperatures[temperatures.Length - 1];
        else return temperatures[0];
      }
    }

    /// <summary>Gets the water flow rate [m³/s].</summary>
    public double WaterFlowRate { get; private set; }

    /// <summary>Gets or sets the ambient temperature [°C].</summary>
    public double AmbientTemperature { get; set; } = 20;

    /// <summary>Gets a value indicating whether flow is in the forward direction.</summary>
    public bool IsForwardFlow { get; private set; }

    /// <summary>Gets the number of tanks.</summary>
    public int TankNumber { get { return temperatures.Length; } }

    /// <summary>Gets the temperature of the first tank [°C].</summary>
    public double FirstTankTemperature { get { return temperatures[0]; } }

    /// <summary>Gets the temperature of the last tank [°C].</summary>
    public double LastTankTemperature { get { return temperatures[temperatures.Length - 1]; } }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="volumes">Tank volumes [m³].</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="volumes"/> contains fewer than 2 elements.
    /// The tridiagonal solver requires at least 2 tanks.
    /// </exception>
    public MultiConnectedWaterTank(double[] volumes)
    {
      if (volumes.Length < 2)
        throw new PopoloArgumentException(
          "At least 2 tanks are required. The tridiagonal solver cannot operate on a single tank.",
          nameof(volumes));
      int num = volumes.Length;

      wMat = new Matrix(3, num);
      temperatures = new Vector(num);
      temperatures_Back = new Vector(num);
      temperatures.Initialize(20);
      temperatures_Back.Initialize(20);

      this.volumes = new double[num];
      volumes.CopyTo(this.volumes, 0);
      this.heatLossCoefficients = new double[num];
    }

    #endregion

    #region 水温設定関連の処理

    /// <summary>Initializes the tank temperature [°C].</summary>
    /// <param name="temperature">Temperature to initialize [°C].</param>
    public void InitializeTemperature(double temperature)
    {
      RestoreState();
      this.temperatures.Initialize(temperature);
    }

    /// <summary>Initializes the tank temperature [°C].</summary>
    /// <param name="temperature">Temperature to initialize [°C].</param>
    /// <param name="tankIndex">Zero-based tank index.</param>
    public void InitializeTemperature(int tankIndex, double temperature)
    {
      RestoreState();
      this.temperatures[tankIndex] = temperature;
    }

    /// <summary>Initializes all tank temperatures.</summary>
    /// <param name="temperatures">Array of tank temperatures [°C].</param>
    public void InitializeTemperature(double[] temperatures)
    { for (int i = 0; i < temperatures.Length; i++) this.temperatures[i] = temperatures[i]; }

    /// <summary>Gets the tank temperature [°C].</summary>
    /// <param name="tankIndex">Zero-based tank index.</param>
    /// <returns>Tank temperature [°C].</returns>
    public double GetTemperature(int tankIndex) { return temperatures[tankIndex]; }

    /// <summary>Copies tank temperatures into the provided array.</summary>
    /// <param name="temperatures">Output array to receive tank temperatures.</param>
    public void GetTemperatures(ref double[] temperatures)
    { for (int i = 0; i < temperatures.Length; i++) temperatures[i] = this.temperatures[i]; }

    #endregion

    #region 水温更新関連の処理

    /// <summary>Advances the state by one time step (forecast mode).</summary>
    /// <param name="waterInletTemperature">Inlet water temperature [°C].</param>
    /// <param name="waterFlowRate">Inlet volumetric flow rate [m³/s].</param>
    /// <param name="isForwardFlow">True for forward flow; false for reverse flow.</param>
    public void ForecastState(double waterInletTemperature, double waterFlowRate, bool isForwardFlow)
    {
      this.WaterInletTemperature = waterInletTemperature;
      this.WaterFlowRate = waterFlowRate;
      this.IsForwardFlow = isForwardFlow;

      //既に予測計算中の場合には状態を復元
      if (isForecasting) RestoreState();
      else
      {
        //現在の温度を保存
        isForecasting = true;
        for (int i = 0; i < temperatures.Length; i++) temperatures_Back[i] = temperatures[i];
      }

      //水温更新
      UpdateTemperature(ref temperatures, ref wMat, timeStep, WaterInletTemperature, WaterFlowRate,
        heatLossCoefficients, AmbientTemperature, volumes, IsForwardFlow);
    }

    /// <summary>Restores the tank temperatures to the pre-forecast state.</summary>
    public void RestoreState()
    {
      if (isForecasting)
        for (int i = 0; i < temperatures.Length; i++)
          temperatures[i] = temperatures_Back[i];
    }

    /// <summary>Commits the forecasted state as the current state.</summary>
    public void FixState() { isForecasting = false; }

    #endregion

    #region 蓄熱量、蓄放熱流の計算処理

    /// <summary>Computes the stored heat [MJ] relative to a reference temperature (positive for hot, negative for cold storage).</summary>
    /// <param name="referenceTemperature">Reference temperature [°C].</param>
    /// <returns>Stored heat [MJ].</returns>
    public double GetHeatStorage(double referenceTemperature)
    {
      double sum = 0;
      for (int i = 0; i < volumes.Length; i++) sum += volumes[i] * (temperatures[i] - referenceTemperature);
      double rho = Water.GetLiquidDensity(referenceTemperature);
      return 0.001 * sum * rho * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
    }

    /// <summary>Computes the heat storage rate [kW].</summary>
    /// <returns>Heat storage rate [kW].</returns>
    public double GetHeatStorageFlow()
    {
      double aveTemp = 0.5 * (WaterInletTemperature + WaterOutletTemperarture);
      return (WaterInletTemperature - WaterOutletTemperarture)
        * WaterFlowRate * Water.GetLiquidDensity(aveTemp) * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
    }

    #endregion

    #region 熱損失係数関連の処理

    /// <summary>Sets the heat loss coefficient [kW/K] for the specified tank.</summary>
    /// <param name="tankIndex">Zero-based tank index.</param>
    /// <param name="heatLossCoefficient">Heat loss coefficient [kW/K].</param>
    public void SetHeatLossCoefficient(int tankIndex, double heatLossCoefficient)
    { heatLossCoefficients[tankIndex] = heatLossCoefficient; }

    /// <summary>Gets the heat loss coefficient [kW/K] for the specified tank.</summary>
    /// <param name="tankIndex">Zero-based tank index.</param>
    /// <returns>Heat loss coefficient [kW/K].</returns>
    public double GetHeatLossCoefficient(int tankIndex)
    { return heatLossCoefficients[tankIndex]; }

    #endregion

    #region staticメソッド

    /// <summary>Updates tank temperatures by solving the tridiagonal system for one time step.</summary>
    /// <param name="temperatures">Tank temperatures [°C].</param>
    /// <param name="wMat">Tridiagonal coefficient matrix (3×n).</param>
    /// <param name="timeStep">Time step [s].</param>
    /// <param name="waterInletTemperature">Inlet water temperature [°C].</param>
    /// <param name="waterFlowRate">Volumetric flow rate [m³/s].</param>
    /// <param name="heatLossCoefficients">Heat loss coefficients [kW/K].</param>
    /// <param name="ambientTemperature">Ambient temperature [°C].</param>
    /// <param name="volumes">Tank volumes [m³].</param>
    /// <param name="isForwardFlow">True for forward flow.</param>
    public static void UpdateTemperature
      (ref IVector temperatures, ref IMatrix wMat, double timeStep, double waterInletTemperature,
      double waterFlowRate, double[] heatLossCoefficients, double ambientTemperature, double[] volumes,
      bool isForwardFlow)
    {
      double tRhoc = timeStep / (PhysicsConstants.NominalWaterIsobaricSpecificHeat);
      int num = temperatures.Length;
      double wft = waterFlowRate * timeStep;
      wMat.Initialize(0);

      //対角行列を作成
      for (int i = 0; i < num; i++)
      {
        double s = wft / volumes[i];
        double r = heatLossCoefficients[i] / volumes[i] * tRhoc;

        wMat[1, i] = 1 + s + r;
        temperatures[i] += ambientTemperature * r;
        if (isForwardFlow)
        {
          if (i == 0) temperatures[i] += s * waterInletTemperature;
          else wMat[0, i] = -s;
          wMat[2, i] = 0;
        }
        else
        {
          if (i == num - 1) temperatures[i] += s * waterInletTemperature;
          else wMat[2, i] = -s;
          wMat[0, i] = 0;
        }
      }
      LinearAlgebraOperations.SolveTridiagonalMatrix(wMat, temperatures);
    }

    #endregion

  }
}
