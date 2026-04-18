/* FluidMachinery.cs
 * 
 * Copyright (C) 2015 E.Togashi
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
using Popolo.Core.Numerics;
using System;

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Abstract base class for fluid machinery (pumps and fans).</summary>
  public abstract class FluidMachinery : IReadOnlyFluidMachinery, ICircuitBranch
  {

    #region インスタンス変数・プロパティ

    /// <summary>Gets the correction factor [-].</summary>
    public double CorrectionFactor { get; protected set; } = 1.0;

    /// <summary>Efficiency characteristic coefficients.</summary>
    protected double[] efficiencyCoefficient;

    /// <summary>Pressure characteristic coefficients (concave-down quadratic: a[0]x²+a[1]x+a[2], a[0] negative).</summary>
    /// <remarks>This constraint is required for applying the quadratic formula and computing derivatives.</remarks>
    protected double[] pressureCoefficient = new double[3];

    /// <summary>Resistance coefficient [kPa/(m³/s)²].</summary>
    protected double resistanceCoefficient;

    /// <summary>Minimum rotation speed ratio [-].</summary>
    private double minRotationRatio = 0.4;

    /// <summary>Gets or sets the volumetric flow rate [m³/s].</summary>
    public double VolumetricFlowRate { get; set; }

    /// <summary>Gets or sets the rotation speed ratio [-].</summary>
    public double RotationRatio { get; set; } = 1.0;

    /// <summary>Gets or sets the minimum rotation speed ratio [-].</summary>
    public double MinRotationRatio
    {
      get { return minRotationRatio; }
      set { minRotationRatio = Math.Max(Math.Min(value, 1.0), 0.05); }
    }

    /// <summary>Gets the total pressure or pump head [kPa].</summary>
    public double Pressure { get; protected set; }

    /// <summary>Gets the actual head [kPa].</summary>
    public double ActualHead { get; private set; }

    /// <summary>Gets a value indicating whether the machine has an inverter.</summary>
    public bool HasInverter { get; private set; }

    /// <summary>Gets the design flow rate [m³/s].</summary>
    public double DesignFlowRate { get; private set; }

    /// <summary>Gets the nominal shaft power [kW].</summary>
    public double NominalShaftPower { get; private set; }

    /// <summary>Gets the motor efficiency [-].</summary>
    public double MotorEfficiency { get; private set; }

    /// <summary>Gets or sets a value indicating whether the machine is shut off.</summary>
    public bool IsShutOff { get; set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="nominalPressure">Nominal total pressure or head [kPa].</param>
    /// <param name="nominalFlowRate">Nominal flow rate [m³/s].</param>
    /// <param name="designPressure">Design total pressure or head [kPa].</param>
    /// <param name="designFlowRate">Design flow rate [m³/s].</param>
    /// <param name="actualHead">Actual head [kPa].</param>
    /// <param name="hasInverter">True if the machine has an inverter.</param>
    protected FluidMachinery
      (double nominalPressure, double nominalFlowRate, double designPressure,
      double designFlowRate, double actualHead, bool hasInverter)
    {
      DesignFlowRate = designFlowRate;
      ActualHead = actualHead;
      HasInverter = hasInverter;
      efficiencyCoefficient = new double[] { 0, 0, 0, 0, 1d };

      //定格軸動力[kW]を計算
      NominalShaftPower = nominalPressure * nominalFlowRate;
      MotorEfficiency = GetMotorEfficiency(NominalShaftPower);

      //抵抗係数[kPa/(m3/s)^2]を計算
      resistanceCoefficient = (designPressure - actualHead) / (designFlowRate * designFlowRate);
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Gets the fluid machinery efficiency [-].</summary>
    public virtual double GetFluidMachineryEfficiency()
    { return Math.Max(GetFluidMachineryEfficiency(VolumetricFlowRate, efficiencyCoefficient), 0.05); }

    /// <summary>Gets the inverter efficiency [-].</summary>
    public virtual double GetInverterEfficiency()
    {
      if (HasInverter)
        return GetInverterEfficiency
          (VolumetricFlowRate * Pressure / NominalShaftPower, RotationRatio);
      else return 1.0;
    }

    /// <summary>Gets the overall efficiency [-].</summary>
    public virtual double GetTotalEfficiency()
    { return MotorEfficiency * GetFluidMachineryEfficiency() * GetInverterEfficiency(); }

    /// <summary>Gets the power consumption [kW].</summary>
    public virtual double GetElectricConsumption()
    { return VolumetricFlowRate * Pressure / GetTotalEfficiency() * CorrectionFactor; }

    /// <summary>Computes the rotation ratio from volumetric flow rate and pressure.</summary>
    /// <param name="flowRate">Volumetric flow rate [m³/s].</param>
    /// <param name="pressure">Pressure [kPa].</param>
    internal void updateWithFlowRateAndPressure(double flowRate, double pressure)
    {
      IsShutOff = false;
      VolumetricFlowRate = flowRate;
      Pressure = pressure;
      RotationRatio = GetRotationRatio(flowRate, pressure, pressureCoefficient);
    }

    /// <summary>Computes the volumetric flow rate at the intersection of the setpoint pressure and PQ characteristic.</summary>
    /// <param name="rotationRatio">Rotation speed ratio [-].</param>
    /// <param name="pressure">Pressure setpoint [kPa].</param>
    internal void updateWithRotationRatioAndPressure(double rotationRatio, double pressure)
    {
      IsShutOff = false;
      RotationRatio = rotationRatio;
      Pressure = pressure;
      VolumetricFlowRate = GetFlowRate(rotationRatio, pressureCoefficient, pressure, DesignFlowRate);
    }

    /// <summary>Computes the volumetric flow rate at the intersection of the resistance curve and PQ characteristic.</summary>
    /// <param name="rotationRatio">Rotation speed ratio [-].</param>
    /// <param name="resistanceCoefficient">Resistance coefficient [kPa/(m³/s)²].</param>
    /// <param name="actualHead">Actual head [kPa].</param>
    /// <param name="activeUnitCount">Number of operating units.</param>
    /// <param name="totalFlowRate">Total flow rate [m³/s].</param>
    internal void updateWithResistanceAndRotationRatio
      (double rotationRatio, double resistanceCoefficient,
      double actualHead, int activeUnitCount, out double totalFlowRate)
    {
      IsShutOff = false;
      RotationRatio = rotationRatio;
      totalFlowRate = GetFlowRate(RotationRatio, pressureCoefficient, resistanceCoefficient,
        actualHead, activeUnitCount, DesignFlowRate * activeUnitCount);
      VolumetricFlowRate = totalFlowRate / activeUnitCount;
      Pressure = resistanceCoefficient * totalFlowRate * totalFlowRate + actualHead;
    }

    /// <summary>Computes the volumetric flow rate at the intersection of the resistance curve and PQ characteristic.</summary>
    /// <param name="rotationRatio">Rotation speed ratio [-].</param>
    internal void updateWithResistanceAndRotationRatio(double rotationRatio)
    {
      IsShutOff = false;
      RotationRatio = rotationRatio;
      VolumetricFlowRate = GetFlowRate(RotationRatio, pressureCoefficient,
        resistanceCoefficient, ActualHead, 1, DesignFlowRate);
      Pressure = resistanceCoefficient * Math.Pow(VolumetricFlowRate, 2) + ActualHead;
    }

    /// <summary>Computes pressure from volumetric flow rate and rotation ratio.</summary>
    /// <param name="flowRate">Volumetric flow rate [m³/s].</param>
    /// <param name="rotationRatio">Rotation speed ratio [-].</param>
    public void UpdateWithFlowRateAndRotationRatio(double flowRate, double rotationRatio)
    {
      IsShutOff = false;
      VolumetricFlowRate = flowRate;
      RotationRatio = rotationRatio;
      Pressure = GetPressure(flowRate, rotationRatio, pressureCoefficient);
    }

    /// <summary>Shuts off the machine.</summary>
    public virtual void ShutOff()
    {
      IsShutOff = true;
      VolumetricFlowRate = 0;
      Pressure = 0;
    }

    #endregion

    #region 特性式設定処理

    /// <summary>Sets the efficiency characteristic coefficients.</summary>
    /// <param name="eCoef0">Efficiency coefficient 0 (eCoef[0]+eCoef[1]x+…+eCoef[4]x⁴).</param>
    /// <param name="eCoef1">Efficiency coefficient 1.</param>
    /// <param name="eCoef2">Efficiency coefficient 2.</param>
    /// <param name="eCoef3">Efficiency coefficient 3.</param>
    /// <param name="eCoef4">Efficiency coefficient 4.</param>
    public void SetEfficiencyCoefficient(
      double eCoef0, double eCoef1, double eCoef2, double eCoef3, double eCoef4)
    {
      efficiencyCoefficient = new double[] { eCoef4, eCoef3, eCoef2, eCoef1, eCoef0 };
    }

    /// <summary>Sets the pressure characteristic coefficients.</summary>
    /// <param name="pCoef0">Pressure coefficient 0 (concave-down quadratic, pCoef[0] negative).</param>
    /// <param name="pCoef1">Pressure coefficient 1.</param>
    /// <param name="pCoef2">Pressure coefficient 2.</param>
    public void SetPressureCoefficient(
      double pCoef0, double pCoef1, double pCoef2)
    {
      pressureCoefficient[0] = pCoef2; //逆な点に注意（SetEfficiencyCoefficientの引数との整合のため）
      pressureCoefficient[1] = pCoef1;
      pressureCoefficient[2] = pCoef0;
    }

    #endregion

    #region ICircuitBranch実装

    /// <summary>Gets or sets the upstream node.</summary>
    public CircuitNode? UpStreamNode { get; set; }

    /// <summary>Gets or sets the downstream node.</summary>
    public CircuitNode? DownStreamNode { get; set; }

    /// <summary>Computes the volumetric flow rate [m³/s] from the differential pressure.</summary>
    /// <returns>Volumetric flow rate [m³/s].</returns>
    /// <remarks>The PQ characteristic is monotonically decreasing throughout to support iterative convergence.</remarks>
    public void UpdateFlowRateFromNodePressureDifference()
    {
      if (UpStreamNode == null || DownStreamNode == null)
        throw new PopoloInvalidOperationException(
            nameof(FluidMachinery),
            nameof(UpStreamNode));

      double dp = DownStreamNode.Pressure - UpStreamNode.Pressure;

      if (IsShutOff || RotationRatio <= 0)
      {
        VolumetricFlowRate = 0;
        return;
      }
      double r2 = RotationRatio * RotationRatio;
      double[] pc = pressureCoefficient;

      //揚程0での流量を計算:解の公式
      double maxF = (-pc[1] - Math.Sqrt(pc[1] * pc[1] - 4 * pc[0] * pc[2])) / (2 * pc[0]);
      if (dp <= 0)
      {
        VolumetricFlowRate = maxF + Math.Sqrt(-dp * r2);
        Pressure = 0;
        return;
      }

      //PQ特性極大値を計算//揚程不足の場合には流量0を出力
      double pMaxF = -0.5 * pc[1] / pc[0];
      double pMax;
      if (0 < pMaxF) pMax = r2 * GetPolynomial(pMaxF, pc);
      else pMax = r2 * pc[2];  //切片圧力
      if (pMax < dp)
      {
        VolumetricFlowRate = pMaxF - (dp - pMax);
        Pressure = pMax;
        return;
      }

      //解の公式で求解
      double aa = r2 * pc[0];
      double bb = r2 * pc[1];
      double cc = r2 * pc[2] - dp;
      VolumetricFlowRate = (-bb - Math.Sqrt(bb * bb - 4 * aa * cc)) / (2 * aa);
      Pressure = dp;
    }

    #endregion

    #region public staticメソッド

    /// <summary>Computes the fluid machinery efficiency [-].</summary>
    /// <param name="flowRate">Volumetric flow rate [m³/s].</param>
    /// <param name="efficiencyCoef">Mechanical efficiency [-].</param>
    /// <returns>Fluid machinery efficiency [-].</returns>
    public static double GetFluidMachineryEfficiency(double flowRate, double[] efficiencyCoef)
    { return GetPolynomial(flowRate, efficiencyCoef); }

    /// <summary>Computes the motor efficiency [-].</summary>
    /// <param name="shaftPower">Shaft power [kW].</param>
    /// <returns>Motor efficiency [-].</returns>
    public static double GetMotorEfficiency(double shaftPower)
    {
      double lnWT = Math.Log(shaftPower);
      return 8.015e-1 + (6.567e-2 + -8.724e-3 * lnWT) * lnWT;
    }

    /// <summary>Computes the inverter efficiency [-].</summary>
    /// <param name="loadRatio">Load ratio [-].</param>
    /// <param name="rotationRatio">Rotation speed ratio [-].</param>
    /// <returns>Inverter efficiency [-].</returns>
    public static double GetInverterEfficiency(double loadRatio, double rotationRatio)
    {
      double lRatio = Math.Max(0, Math.Min(1, loadRatio));
      double rRatio = Math.Max(0, Math.Min(1, rotationRatio));

      double r2 = rRatio * rRatio;
      double a1 = -0.2287 * r2 + 0.3425 * rRatio - 0.4923;
      double a2 = 0.4690 * r2 - 0.8248 * rRatio + 0.9172;
      double a3 = -0.3356 * r2 + 0.6319 * rRatio + 0.4756;
      return lRatio * (lRatio * a1 + a2) + a3;
    }

    /// <summary>Computes the rotation ratio from volumetric flow rate and pressure.</summary>
    /// <param name="flowRate">Volumetric flow rate [m³/s].</param>
    /// <param name="pressure">Pressure [kPa].</param>
    /// <param name="pressureCoef">Pressure characteristic coefficients.</param>
    /// <returns>Rotation speed ratio [-].</returns>
    public static double GetRotationRatio(double flowRate, double pressure, double[] pressureCoef)
    {
      Roots.ErrorFunction eFnc = delegate (double rRate)
      { return GetPressure(flowRate, rRate, pressureCoef) - pressure; };
      return Roots.Newton(eFnc, 0.8, 1e-5, 1e-4, 1e-4, 20);
    }

    /// <summary>Computes the volumetric flow rate at the intersection of the setpoint pressure and PQ characteristic.</summary>
    /// <param name="rotationRatio">Rotation speed ratio [-].</param>
    /// <param name="pressureCoef">Pressure characteristic coefficients.</param>
    /// <param name="pressure">Pressure setpoint [kPa].</param>
    /// <param name="initialFlowRate">Initial volumetric flow rate [m³/s].</param>
    /// <returns>Volumetric flow rate [m³/s].</returns>
    public static double GetFlowRate
      (double rotationRatio, double[] pressureCoef, double pressure, double initialFlowRate)
    {
      if (rotationRatio <= 0) return 0;
      double r2 = rotationRatio * rotationRatio;

      //揚程0での流量を計算:解の公式
      double maxF = (-pressureCoef[1] - Math.Sqrt(pressureCoef[1] * pressureCoef[1] - 4 * pressureCoef[0] * pressureCoef[2])) / (2 * pressureCoef[0]);
      if (pressure <= 0) return maxF;

      //PQ特性極大値を計算//揚程不足の場合には流量0を出力
      double pMaxF = -0.5 * pressureCoef[1] / pressureCoef[0];
      double pMax;
      if (0 < pMaxF) pMax = r2 * GetPolynomial(pMaxF, pressureCoef);
      else pMax = r2 * pressureCoef[2];  //切片圧力
      if (pMax < pressure) return 0; //2022.01.06 BugFix

      //二分法で求解
      Roots.ErrorFunction eFnc = delegate (double vf)
      { return r2 * GetPolynomial(vf / rotationRatio, pressureCoef) - pressure; };
      return Roots.Bisection(eFnc, Math.Max(0, pMaxF), maxF, 1e-4, 1e-4, 20);
    }

    /// <summary>Computes the volumetric flow rate at the intersection of the resistance curve and PQ characteristic.</summary>
    /// <param name="rotationRatio">Rotation speed ratio [-].</param>
    /// <param name="pressureCoef">Pressure characteristic coefficients.</param>
    /// <param name="resistanceCoefficient">Resistance coefficient [kPa/(m³/s)²].</param>
    /// <param name="actualHead">Actual head [kPa].</param>
    /// <param name="activeUnitCount">Number of operating units.</param>
    /// <param name="initialFlowRate">Initial volumetric flow rate [m³/s].</param>
    /// <returns>Volumetric flow rate [m³/s].</returns>
    public static double GetFlowRate
      (double rotationRatio, double[] pressureCoef, double resistanceCoefficient,
      double actualHead, int activeUnitCount, double initialFlowRate)
    {
      double r2 = rotationRatio * rotationRatio;
      Roots.ErrorFunction eFnc = delegate (double vf)
      {
        return r2 * GetPolynomial
        (vf / (activeUnitCount * rotationRatio), pressureCoef) - (vf * vf * resistanceCoefficient + actualHead);
      };
      Roots.ErrorFunction eFncD = delegate (double vf)
      {
        return r2 * GetPolynomialD(vf, 1 / (activeUnitCount * rotationRatio), pressureCoef)
          - 2 * vf * resistanceCoefficient;
      };
      return Roots.Newton(eFnc, eFncD, initialFlowRate, 1e-4, 1e-4, 20);
    }

    /// <summary>Computes pressure from volumetric flow rate and rotation ratio.</summary>
    /// <param name="flowRate">Volumetric flow rate [m³/s].</param>
    /// <param name="rotationRatio">Rotation speed ratio [-].</param>
    /// <param name="pressureCoef">Pressure characteristic coefficients.</param>
    /// <returns>Pressure [kPa].</returns>
    public static double GetPressure(double flowRate, double rotationRatio, double[] pressureCoef)
    { return rotationRatio * rotationRatio * GetPolynomial(flowRate / rotationRatio, pressureCoef); }

    #endregion

    #region private staticメソッド

    /// <summary>Evaluates the polynomial y = Σa[n]·x^(N−n).</summary>
    /// <param name="x">Input variable.</param>
    /// <param name="a">Coefficient array.</param>
    /// <returns>y=Σa[n]*x^n</returns>
    private static double GetPolynomial(double x, double[] a)
    {
      double y = a[0];
      for (int i = 1; i < a.Length; i++) y = y * x + a[i];
      return y;
    }

    /// <summary>Computes the derivative of the polynomial y = Σa[n]·x^(N−n)·b^(N−n).</summary>
    /// <param name="x">Input variable.</param>
    /// <param name="b">Coefficient b.</param>
    /// <param name="a">Coefficient array.</param>
    /// <returns>Derivative of the polynomial y = Σa[n]·x^(N−n)·b^(N−n).</returns>
    private static double GetPolynomialD(double x, double b, double[] a)
    {
      int pow = a.Length - 1;
      double y = b * pow * a[0];
      for (int i = 1; i < a.Length - 1; i++) y = b * (x * y + (pow - i) * a[i]);
      return y;
    }

    #endregion

  }

  #region 読み取り専用インターフェース

  #endregion

}
