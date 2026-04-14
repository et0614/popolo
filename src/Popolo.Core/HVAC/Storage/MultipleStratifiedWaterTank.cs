/* MultipleStratifiedWaterTank.cs
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

using System;

using Popolo.Core.Physics;
using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.HVAC.Storage
{
  /// <summary>Thermally stratified water storage tank.</summary>
  public class MultipleStratifiedWaterTank : IReadOnlyMultipleStratifiedWaterTank
  {

    #region 定数宣言

    /// <summary>Gravitational acceleration [m/s²].</summary>
    private const double G_FORCES = 9.8;

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Tridiagonal coefficient matrix (3×layers).</summary>
    private IMatrix wMat;
    private IVector wVec1, wVec2;

    /// <summary>Temperature distribution in the tank [°C].</summary>
    private IVector temperatures;

    /// <summary>Time step [s].</summary>
    private double timeStep = 60;

    /// <summary>Gets or sets the time step [s].</summary>
    public double TimeStep
    {
      get { return timeStep; }
      set { if (0 < value) timeStep = value; }
    }

    /// <summary>Gets the water depth [m].</summary>
    public double WaterDepth { get; private set; }

    /// <summary>Gets the horizontal cross-sectional area [m²].</summary>
    public double SectionalArea { get; private set; }

    /// <summary>Gets the tank volume [m³].</summary>
    public double WaterVolume { get { return WaterDepth * SectionalArea; } }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    public double WaterInletTemperature { get; private set; }

    /// <summary>Gets the outlet water temperature at the top port [°C].</summary>
    public double UpperOutletTemperarture
    { get { return temperatures[temperatures.Length - PipeInstallationLayer - 1]; } }

    /// <summary>Gets the outlet water temperature at the bottom port [°C].</summary>
    public double LowerOutletTemperarture
    { get { return temperatures[PipeInstallationLayer]; } }

    /// <summary>Gets the volumetric flow rate [m³/s].</summary>
    public double WaterFlowRate { get; private set; }

    /// <summary>Gets the port diameter [m].</summary>
    public double PipeDiameter { get; private set; }

    /// <summary>Gets the layer index of the inlet/outlet port.</summary>
    public int PipeInstallationLayer { get; private set; }

    /// <summary>Gets or sets the overall heat loss coefficient [kW/K].</summary>
    public double HeatLossCoefficient { get; set; }

    /// <summary>Gets or sets the ambient temperature [°C].</summary>
    public double AmbientTemperature { get; set; } = 20;

    /// <summary>True if the flow is directed downward.</summary>
    public bool IsDownFlow { get; private set; }

    /// <summary>Gets the number of layers.</summary>
    public int LayerNumber { get { return temperatures.Length; } }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="waterDepth">Water depth [m].</param>
    /// <param name="sectionalArea">Horizontal cross-sectional area [m²].</param>
    /// <param name="pipeDiameter">Port diameter [m].</param>
    /// <param name="pipeInstallationHeight">Installation height of the upper port [m].</param>
    /// <param name="layerNumber">Number of layers.</param>
    public MultipleStratifiedWaterTank
      (double waterDepth, double sectionalArea, double pipeDiameter, double pipeInstallationHeight, int layerNumber)
    {
      this.WaterDepth = waterDepth;
      this.SectionalArea = sectionalArea;
      this.PipeDiameter = pipeDiameter;
      wMat = new Matrix(3, layerNumber);
      wVec1 = new Vector(layerNumber);
      wVec2 = new Vector(layerNumber);
      temperatures = new Vector(layerNumber);
      temperatures.Initialize(20);

      double dz = WaterDepth / LayerNumber;
      PipeInstallationLayer = (int)Math.Floor(pipeInstallationHeight / dz);
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Initializes the temperature distribution [°C].</summary>
    /// <param name="temperature">Temperature to initialize [°C].</param>
    public void InitializeTemperature(double temperature)
    { this.temperatures.Initialize(temperature); }

    /// <summary>Initializes the temperature distribution [°C].</summary>
    /// <param name="temperature">Temperature to initialize [°C].</param>
    /// <param name="layerNumber">Zero-based layer index.</param>
    public void InitializeTemperature(int layerNumber, double temperature)
    { this.temperatures[layerNumber] = temperature; }

    /// <summary>Sets the temperature distribution from an array.</summary>
    /// <param name="temperatures">Array of layer temperatures [°C].</param>
    public void InitializeTemperature(double[] temperatures)
    { for (int i = 0; i < temperatures.Length; i++) this.temperatures[i] = temperatures[i]; }

    /// <summary>Gets the temperature of the specified layer [°C].</summary>
    /// <param name="layerNumber">Zero-based layer index.</param>
    /// <returns>Layer temperature [°C].</returns>
    public double GetTemperature(int layerNumber) { return temperatures[layerNumber]; }

    /// <summary>Copies the temperature distribution into the provided array.</summary>
    /// <param name="temperatures">Output array to receive the layer temperatures.</param>
    public void GetTemperatures(ref double[] temperatures)
    { for (int i = 0; i < temperatures.Length; i++) temperatures[i] = this.temperatures[i]; }

    /// <summary>Advances the temperature distribution by one time step.</summary>
    /// <param name="waterInletTemperature">Inlet water temperature [°C].</param>
    /// <param name="waterFlowRate">Inlet volumetric flow rate [m³/s].</param>
    /// <param name="isDownFlow">True if the flow is directed downward; false for upward flow.</param>
    public void ForecastState(double waterInletTemperature, double waterFlowRate, bool isDownFlow)
    {
      this.WaterInletTemperature = waterInletTemperature;
      this.WaterFlowRate = waterFlowRate;
      this.IsDownFlow = isDownFlow;

      UpdateTemperature(ref temperatures, ref wMat, ref wVec1, ref wVec2, timeStep,
        WaterInletTemperature, WaterFlowRate, HeatLossCoefficient, AmbientTemperature, WaterDepth,
        PipeDiameter, SectionalArea, PipeInstallationLayer, IsDownFlow);
    }

    #endregion

    #region 蓄熱量、蓄放熱流の計算処理

    /// <summary>Computes the stored heat [MJ] relative to a reference temperature (positive for hot, negative for cold storage).</summary>
    /// <param name="referenceTemperature">Reference temperature [°C].</param>
    /// <returns>Stored heat [MJ].</returns>
    public double GetHeatStorage(double referenceTemperature)
    {
      double dz = WaterDepth / temperatures.Length;  //分割幅
      double sum = 0;
      for (int i = 0; i < temperatures.Length; i++) sum += (temperatures[i] - referenceTemperature);
      double rho = Water.GetLiquidDensity(referenceTemperature);
      return 0.001 * sum * rho * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * dz * SectionalArea;
    }

    /// <summary>Computes the heat storage rate [kW].</summary>
    /// <returns>Heat storage rate [kW].</returns>
    public double GetHeatStorageFlow()
    {
      double two;
      if (IsDownFlow) two = LowerOutletTemperarture;
      else two = UpperOutletTemperarture;
      double aveTemp = 0.5 * (WaterInletTemperature + two);
      return (WaterInletTemperature - two) * WaterFlowRate * Water.GetLiquidDensity(aveTemp) * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
    }

    #endregion

    #region staticメソッド

    /// <summary>Updates the layer temperatures by solving the tridiagonal system for one time step.</summary>
    /// <param name="temperature">Layer temperature distribution [°C].</param>
    /// <param name="wMat">Tridiagonal coefficient matrix (3×layers).</param>
    /// <param name="wVec1">Working vector (length = number of layers).</param>
    /// <param name="wVec2">Working vector (length = number of layers).</param>
    /// <param name="timeStep">Time step [s].</param>
    /// <param name="waterInletTemperature">Inlet water temperature [°C].</param>
    /// <param name="waterFlowRate">Volumetric flow rate [m³/s].</param>
    /// <param name="heatLossCoefficient">Overall heat loss coefficient [kW/K].</param>
    /// <param name="ambientTemperature">Ambient temperature [°C].</param>
    /// <param name="waterDepth">Water depth [m].</param>
    /// <param name="pipeDiameter">Port diameter [m].</param>
    /// <param name="sectionalArea">Cross-sectional area [m²].</param>
    /// <param name="pipeInstallationLayer">Layer index of the inlet/outlet port.</param>
    /// <param name="isDownFlow">True for downward flow.</param>
    public static void UpdateTemperature
      (ref IVector temperature, ref IMatrix wMat, ref IVector wVec1, ref IVector wVec2, double timeStep,
      double waterInletTemperature, double waterFlowRate, double heatLossCoefficient, double ambientTemperature,
      double waterDepth, double pipeDiameter, double sectionalArea, int pipeInstallationLayer, bool isDownFlow)
    {
      int layerNum = temperature.Length;  //分割数
      double dz = waterDepth / layerNum;  //分割幅

      //平均水温を計算
      double aveTemp = 0;
      for (int i = 0; i < layerNum; i++) aveTemp += temperature[i];
      aveTemp /= layerNum;

      //混合域の噴流配分の計算
      IVector phiN = wVec1;
      IVector uN = wVec2;
      if (waterFlowRate == 0)
      {
        phiN.Initialize(0);
        uN.Initialize(0);
      }
      //温度逆転の場合は混合すると温度が逆転する層まで完全混合とし、噴流は等分割
      else if ((isDownFlow && waterInletTemperature <= temperature[0])
        || (!isDownFlow && temperature[layerNum - 1] <= waterInletTemperature))
      {
        //温度逆転範囲を求めて平均温度を計算
        double mixTemp, mixedAve;
        int mixedNum;
        if (isDownFlow)
        {
          mixedAve = temperature[0];
          mixTemp = temperature[0]
            + timeStep / (sectionalArea * dz) * (waterInletTemperature - temperature[0]) * waterFlowRate;
        }
        else
        {
          mixedAve = temperature[temperature.Length - 1];
          mixTemp = temperature[temperature.Length - 1]
            + timeStep * (waterInletTemperature - temperature[temperature.Length - 1]) / (sectionalArea * dz);
        }

        for (mixedNum = 1; mixedNum < layerNum; mixedNum++)
        {
          if (isDownFlow)
          {
            if (temperature[mixedNum] < mixTemp) break;
            mixTemp = (mixTemp * mixedNum + temperature[mixedNum]) / (mixedNum + 1);
            mixedAve += temperature[mixedNum];
          }
          else
          {
            int tgtLayer = layerNum - (mixedNum + 1);
            if (mixTemp < temperature[tgtLayer]) break;
            mixTemp = 0.5 * (mixTemp + temperature[tgtLayer]);
            mixedAve += temperature[tgtLayer];
          }
        }
        mixedAve /= mixedNum;
        double bf = waterFlowRate / (mixedNum * dz);

        //平均温度と噴流配分を設定
        for (int i = 0; i < layerNum; i++)
        {
          int tgtLayer = i;
          if (!isDownFlow) tgtLayer = layerNum - (i + 1);

          if (i < mixedNum)
          {
            phiN[tgtLayer] = bf;
            temperature[tgtLayer] = mixedAve;
            uN[tgtLayer] = dz / sectionalArea * phiN[tgtLayer];
          }
          else phiN[tgtLayer] = uN[tgtLayer] = 0;
          if (i != 0)
          {
            if (isDownFlow) uN[tgtLayer] += uN[tgtLayer - 1];
            else uN[tgtLayer] += uN[tgtLayer + 1];
          }
        }
      }
      else
      {
        double rhowi = Water.GetLiquidDensity(waterInletTemperature);
        double pipeSA = Math.Pow(pipeDiameter / 2, 2) * Math.PI; //流入口断面積[m2]
        double uwi2 = Math.Pow(waterFlowRate / pipeSA, 2);//流入速度2乗[(m/s)^2]

        //噴流が到達する層を求める
        int lmax;
        double tgt = rhowi * uwi2 / (G_FORCES * dz);
        double rr = 0;
        for (lmax = 0; lmax < layerNum - 1; lmax++)
        {
          if (isDownFlow) rr += (Water.GetLiquidDensity(temperature[lmax]) - rhowi);
          else rr += (rhowi - Water.GetLiquidDensity(temperature[layerNum - lmax - 1]));
          if (tgt < rr) break;
        }
        if (!isDownFlow) lmax = layerNum - lmax - 1;

        //混合域深さの計算************
        double tlMax = temperature[lmax];
        double lm;
        //等温の層まで到達する場合には混合域100%とする
        if (tlMax == waterInletTemperature) lm = waterDepth;
        else
        {
          double rho0 = Water.GetLiquidDensity(tlMax);
          double ari = pipeDiameter * G_FORCES * Math.Abs(rho0 - rhowi) / (rho0 * uwi2);  //アルキメデス数
          double ndt = 0;
          if (isDownFlow)
            for (int i = 0; i < lmax; i++) ndt += (temperature[i] - tlMax);
          else for (int i = lmax; i < layerNum; i++) ndt += (temperature[i] - tlMax);
          ndt = (ndt * dz) / (waterDepth * (waterInletTemperature - tlMax));  //無次元時間
          lm = waterDepth * 0.8 * Math.Pow(ari, -0.5) * pipeDiameter / waterDepth + 0.5 * ndt;
        }

        double z1 = 0;
        double bf = waterFlowRate / (2 * lm * lm * lm);
        for (int i = 0; i < layerNum; i++)
        {
          int ln = i;
          if (!isDownFlow) ln = layerNum - (i + 1);
          double z2 = z1 + dz;
          if (z2 < lm) phiN[ln] = bf * (3 * lm * lm - dz * dz * (3 * i * (i + 1) + 1));
          else if (lm < z1) phiN[ln] = 0;
          else phiN[ln] = bf * Math.Pow(i * dz - lm, 2) * (i + 2 * lm / dz);

          if (i == 0) uN[ln] = dz / sectionalArea * phiN[ln];
          else if (isDownFlow) uN[ln] = uN[ln - 1] + dz / sectionalArea * phiN[ln];
          else uN[ln] = uN[ln + 1] + dz / sectionalArea * phiN[ln];
          z1 = z2;
        }
      }

      //3重対角行列を解く
      wMat.Initialize(0);
      int outletLayer = pipeInstallationLayer;
      if (!isDownFlow) outletLayer = layerNum - pipeInstallationLayer - 1;
      double s = timeStep * Water.GetLiquidThermalDiffusivity(aveTemp) / (dz * dz);
      double p = heatLossCoefficient * timeStep /
        (Water.GetLiquidDensity(aveTemp) * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * waterDepth * sectionalArea);
      for (int i = 0; i < layerNum; i++)
      {

        double r1 = 0;
        double r2 = 0;
        if (isDownFlow && i != 0 && i <= outletLayer) r1 = uN[i - 1] * timeStep / dz;
        if (!isDownFlow && i != layerNum - 1 && outletLayer <= i) r2 = uN[i + 1] * timeStep / dz;

        if (i == layerNum - 1) wMat[0, i] = -(2 * s + r1);
        else if (i != 0) wMat[0, i] = -(s + r1);

        if (i == 0) wMat[2, i] = -(2 * s + r2);
        else if (i != layerNum - 1) wMat[2, i] = -(s + r2);

        wMat[1, i] = 2 * s + r1 + r2 + 1 + p;
        temperature[i] += p * ambientTemperature;
        if (phiN[i] != 0)
        {
          double q = timeStep * phiN[i] / sectionalArea;
          wMat[1, i] += q;
          temperature[i] += q * waterInletTemperature;
        }
      }
      LinearAlgebraOperations.SolveTridiagonalMatrix(wMat, temperature);
    }

    #endregion

  }
}
