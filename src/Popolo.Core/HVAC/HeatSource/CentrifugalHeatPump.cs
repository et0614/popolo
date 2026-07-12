/* CentrifugalHeatPump.cs
 *
 * Comprehensive centrifugal (turbo) heat-pump model: calibration (parameter
 * estimation from catalog operating points) and the forward energy-prediction
 * model (paper Figure 6) covering the cooling, heating, and heat-recovery
 * modes, overloaded operation, and operation below the capacity-control range.
 *
 * Characteristic equation (paper Eq.11 / Eq.12):
 *   η = 1 / (a_cmp·ϕ² + b_cmp·ϕ + c_cmp·(ϕ⁻¹ − 1) + d_cmp·Rw² + e_cmp·Rw + f_cmp)
 *   E = W_cmp,is / η   (linear in the six parameters → single-pass least squares)
 * where Rv = vR/vR,N, Rw = w1,is/w1,is,N and ϕ = Rv/√Rw.
 *
 * Below the continuous capacity-control range (ϕ < ϕ_min: on-off cycling or hot-gas
 * bypass) the power follows paper Eq.13:
 *   E = E_min·[1 + θ·(ϕ/ϕ_min − 1)],  0 ≤ θ ≤ 1
 * where E_min is the power at the minimum continuously controllable load Q_min
 * (the load at which ϕ = ϕ_min under the present water-side conditions).
 *
 * Copyright (C) 2026 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 */

using System;
using System.Collections.Generic;

using Popolo.Core.Physics;
using Popolo.Core.Numerics;
using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>
  /// Comprehensive centrifugal heat-pump model. Static calibration methods identify the
  /// model parameters from manufacturer catalog data (EstimateMode,
  /// <see cref="EstimateHeatingMode"/>, <see cref="EstimateMinimumFlowCoefficient"/>,
  /// <see cref="EstimateCyclingPowerWeight"/>), and <see cref="Solve"/> predicts the
  /// operating state (power consumption, heats, temperatures) for given water-side
  /// boundary conditions in the cooling, heating, and heat-recovery modes.
  /// </summary>
  public class CentrifugalHeatPump
  {

    #region 定数

    /// <summary>Default condenser approach temperature at rated conditions Δt_cnd,app,N [K].</summary>
    public const double DefaultCondenserApproach = 1.0;

    /// <summary>Default evaporator approach temperature at rated conditions Δt_evp,app,N [K].</summary>
    public const double DefaultEvaporatorApproach = 2.0;

    /// <summary>Minimum number of operating points required to fit the six-parameter characteristic.</summary>
    private const int MinCurvePoints = 7;

    #endregion

    #region 列挙型

    /// <summary>Base operation mode of the heat pump.</summary>
    public enum OperationMode
    {
      /// <summary>Cooling mode (evaporator = chilled water, condenser = cooling water).</summary>
      Cooling,
      /// <summary>Heating mode (evaporator = heat source water, condenser = hot water).</summary>
      Heating
    }

    #endregion

    #region インスタンス（機器固有情報）

    private readonly Refrigerant refrigerant;
    private readonly ModeCalibration? coolingCalibration;
    private readonly ModeCalibration? heatingCalibration;
    private readonly double maximumPower;
    private readonly int stageCount;

    /// <summary>Maximum electric power input E_max [kW] (capacity ceiling).</summary>
    public double MaximumPower => maximumPower;

    /// <summary>Number of compressor impeller stages J.</summary>
    public int StageCount => stageCount;

    /// <summary>Cooling-mode calibration (null when the machine has no cooling mode).</summary>
    public ModeCalibration? CoolingCalibration => coolingCalibration;

    /// <summary>Heating-mode calibration (null when the machine has no heating mode).</summary>
    public ModeCalibration? HeatingCalibration => heatingCalibration;

    /// <summary>
    /// Initializes the heat pump with its machine-specific information. The refrigerant
    /// property evaluator is created once here and reused by every <see cref="Solve"/> call,
    /// which matters in annual simulations with thousands of calls. Instances are not
    /// thread-safe; use one instance per thread.
    /// </summary>
    /// <param name="refrigerant">Refrigerant fluid.</param>
    /// <param name="maximumPower">Maximum electric power input E_max [kW]
    /// (see <see cref="ResolveMaximumPower"/>).</param>
    /// <param name="coolingCalibration">Cooling-mode calibration (null if unavailable).</param>
    /// <param name="heatingCalibration">Heating-mode calibration (null if unavailable).</param>
    /// <param name="stageCount">Number of compressor impeller stages J.</param>
    public CentrifugalHeatPump(
      Refrigerant.Fluid refrigerant, double maximumPower,
      ModeCalibration? coolingCalibration, ModeCalibration? heatingCalibration = null,
      int stageCount = 2)
    {
      if (!(0.0 < maximumPower) || double.IsInfinity(maximumPower))
        throw new ArgumentOutOfRangeException(nameof(maximumPower), maximumPower,
          "The maximum power input must be positive and finite.");
      if (stageCount < 1) throw new ArgumentOutOfRangeException(nameof(stageCount));
      if (coolingCalibration == null && heatingCalibration == null)
        throw new ArgumentException("At least one mode calibration is required.",
          nameof(coolingCalibration));

      this.refrigerant = new Refrigerant(refrigerant);
      this.maximumPower = maximumPower;
      this.coolingCalibration = coolingCalibration;
      this.heatingCalibration = heatingCalibration;
      this.stageCount = stageCount;
    }

    #endregion

    #region パラメータ推定

    /// <summary>
    /// Estimates the model parameters for a single operation mode from catalog data,
    /// using the paper's default approach temperatures (Δt_cnd,app,N = 1 K, Δt_evp,app,N = 2 K).
    /// </summary>
    public static ModeCalibration EstimateMode(
      OperationMode mode, Refrigerant.Fluid refrigerant,
      CatalogPoint rated, IReadOnlyList<CatalogPoint> points, int stageCount = 2,
      CatalogPoint? recoveryRated = null)
      => EstimateMode(mode, refrigerant, rated, points,
        DefaultCondenserApproach, DefaultEvaporatorApproach, stageCount, recoveryRated);

    /// <summary>
    /// Estimates the model parameters for a single operation mode from catalog data,
    /// with explicit approach temperatures. The heat-transfer coefficients (KA) and the
    /// normalization references (w1,N, vR,N) are derived from the rated point; the six
    /// characteristic coefficients are then identified by ordinary least squares on
    /// E = W_cmp,is·(a_cmp·ϕ² + b_cmp·ϕ + c_cmp·(ϕ⁻¹−1) + d_cmp·Rw² + e_cmp·Rw + f_cmp).
    /// When the heat-recovery rated point is given, the recovery-tube heat-transfer
    /// coefficient KA_cnd,rcv is also estimated (condenser side = hot water).
    /// </summary>
    /// <param name="mode">Operation mode.</param>
    /// <param name="refrigerant">Refrigerant fluid.</param>
    /// <param name="rated">Rated operating point of the mode (normalization anchor).</param>
    /// <param name="points">Catalog operating points.</param>
    /// <param name="condenserApproach">Condenser approach temperature [K].</param>
    /// <param name="evaporatorApproach">Evaporator approach temperature [K].</param>
    /// <param name="stageCount">Number of compressor impeller stages J.</param>
    /// <param name="recoveryRated">Heat-recovery rated point (null if unavailable).</param>
    public static ModeCalibration EstimateMode(
      OperationMode mode, Refrigerant.Fluid refrigerant,
      CatalogPoint rated, IReadOnlyList<CatalogPoint> points,
      double condenserApproach, double evaporatorApproach, int stageCount = 2,
      CatalogPoint? recoveryRated = null)
    {
      if (stageCount < 1) throw new ArgumentOutOfRangeException(nameof(stageCount));
      if (points == null) throw new ArgumentNullException(nameof(points));

      // 定格点は正規化の基準であると同時に回帰点でもある（論文の「定格点＋6点以上」）。
      // points に含まれていなければ自動的に追加する。
      var pts = new List<CatalogPoint>(points);
      if (!pts.Contains(rated)) pts.Add(rated);
      if (pts.Count < MinCurvePoints)
        throw new ArgumentException(
          $"At least {MinCurvePoints} operating points (including the rated point) are required " +
          $"to fit the characteristic; got {pts.Count}.", nameof(points));

      CycleSolution sol = SolveCyclePoints(
        mode, refrigerant, rated, pts, condenserApproach, evaporatorApproach, stageCount);

      // E = W_is·(a·ϕ² + b·ϕ + c·(ϕ⁻¹−1) + d·Rw² + e·Rw + f)：6パラメータに対して線形
      int n = pts.Count;
      double[] y = new double[n];
      double[][] x = new double[n][];
      for (int i = 0; i < n; i++)
      {
        CyclePoint c = sol.Points[i];
        double rv = c.VolumetricFlow / sol.NominalFlowVolume;
        double rw = c.StageOneHead / sol.NominalHead;
        double phi = rv / Math.Sqrt(rw);
        double wis = c.IsentropicPower;
        y[i] = pts[i].PowerInput;
        x[i] = new double[]
        {
          wis * phi * phi,
          wis * phi,
          wis * (1.0 / phi - 1.0),
          wis * rw * rw,
          wis * rw,
          wis
        };
      }

      // 制約付き最小二乗（効率極大条件：a_cmp ≥ 0, d_cmp ≥ 0）。
      // 凸二次計画なので、制約に対応する有効集合（a=0 / d=0 / 両方）を列挙し、
      // 該当列を除いた通常のOLSを解いて実行可能解のうちRSS最小を採れば厳密解になる。
      // {a=0, d=0} は構成上必ず実行可能なため、解は常に存在する。
      double[] cf = FitOls(y, x, [true, true, true, true, true, true], out double rss);
      bool constrained = false;
      if (cf[0] < 0.0 || cf[3] < 0.0)
      {
        constrained = true;
        cf = null!;
        rss = double.PositiveInfinity;
        foreach (bool[] mask in new[]
        {
          new[] { false, true, true, true, true, true },   // a=0
          new[] { true, true, true, false, true, true },   // d=0
          new[] { false, true, true, false, true, true },  // a=0, d=0
        })
        {
          double[] c = FitOls(y, x, mask, out double r);
          if (c[0] < 0.0 || c[3] < 0.0) continue;   // 自由に残した側が違反 → 棄却
          if (r < rss) { rss = r; cf = c; }
        }
      }

      // 熱回収定格点があれば、回収チューブの熱通過率 KA_cnd,rcv も推定する
      double kaRcv = double.NaN;
      if (recoveryRated.HasValue)
      {
        CatalogPoint rr = recoveryRated.Value;
        SplitDuty(mode, rr.Capacity, rr.PowerInput, out _, out double qCndRcv);
        kaRcv = EstimateCondenserHeatTransfer(
          rr.CondenserInletTemperature, Mc(rr.CondenserFlowRate), qCndRcv, condenserApproach);
      }

      return new ModeCalibration
      {
        Parameters = new Parameters(cf[0], cf[1], cf[2], cf[3], cf[4], cf[5]),
        EvaporatorHeatTransferCoefficient = sol.EvaporatorHeatTransferCoefficient,
        CondenserHeatTransferCoefficient = sol.CondenserHeatTransferCoefficient,
        RecoveryHeatTransferCoefficient = kaRcv,
        NominalHead = sol.NominalHead,
        NominalFlowVolume = sol.NominalFlowVolume,
        RSquared = RSquared(y, rss),
        ConstraintActivated = constrained
      };
    }

    /// <summary>
    /// Solves an ordinary least-squares problem using only the columns flagged in
    /// <paramref name="use"/>, and returns the coefficient vector expanded to full
    /// length (excluded columns get a zero coefficient).
    /// </summary>
    private static double[] FitOls(double[] y, double[][] xFull, bool[] use, out double rss)
    {
      var idx = new List<int>();
      for (int k = 0; k < use.Length; k++) if (use[k]) idx.Add(k);
      double[][] x = new double[y.Length][];
      for (int i = 0; i < y.Length; i++)
      {
        x[i] = new double[idx.Count];
        for (int k = 0; k < idx.Count; k++) x[i][k] = xFull[i][idx[k]];
      }
      double[] c = LinearAlgebraOperations.EstimateMultipleRegressionCoefficients(
        y, x, out _, out _, out rss);
      double[] full = new double[use.Length];
      for (int k = 0; k < idx.Count; k++) full[idx[k]] = c[k];
      return full;
    }

    /// <summary>
    /// Builds the heating-mode calibration from the heating rated point by borrowing the
    /// cooling-mode characteristic (paper Eq.39: η_ht = α·η_ch). The heat-transfer
    /// coefficients are re-estimated from the heating rated point, while the normalization
    /// references (w1,N, vR,N) are shared with the cooling mode so that both modes live on
    /// the same (ϕ, Rw) map; α absorbs the level difference and is determined in closed form
    /// so that the rated power is reproduced exactly. The scaling 1/η_ht = (1/α)·(1/η_ch) is
    /// folded into the returned coefficients, so <see cref="Parameters.PredictPower"/> can be
    /// used unchanged.
    /// </summary>
    /// <param name="coolingCalibration">Cooling-mode calibration (characteristic source).</param>
    /// <param name="refrigerant">Refrigerant fluid.</param>
    /// <param name="rated">Heating rated point (evaporator side = heat source water,
    /// condenser side = hot water, capacity = condenser heat).</param>
    /// <param name="condenserApproach">Condenser approach temperature [K].</param>
    /// <param name="evaporatorApproach">Evaporator approach temperature [K].</param>
    /// <param name="stageCount">Number of compressor impeller stages J.</param>
    /// <param name="recoveryRated">Heating-priority heat-recovery rated point (evaporator
    /// side = recovered chilled water); null if unavailable.</param>
    public static ModeCalibration EstimateHeatingMode(
      ModeCalibration coolingCalibration, Refrigerant.Fluid refrigerant, CatalogPoint rated,
      double condenserApproach = DefaultCondenserApproach,
      double evaporatorApproach = DefaultEvaporatorApproach,
      int stageCount = 2, CatalogPoint? recoveryRated = null)
    {
      // 加熱定格点の理論サイクル（KAは加熱定格から取り直す）
      CycleSolution sol = SolveCyclePoints(
        OperationMode.Heating, refrigerant, rated, [rated],
        condenserApproach, evaporatorApproach, stageCount);
      CyclePoint cyc = sol.Points[0];

      // 冷却モードの正規化座標上での運転点と、実効率から α を閉形式で決める
      double rv = cyc.VolumetricFlow / coolingCalibration.NominalFlowVolume;
      double rw = cyc.StageOneHead / coolingCalibration.NominalHead;
      double phi = rv / Math.Sqrt(rw);
      double etaActual = cyc.IsentropicPower / rated.PowerInput;
      double alpha = etaActual / coolingCalibration.Parameters.Efficiency(phi, rw);

      // 1/η_ht = (1/α)·(1/η_ch) → 6係数を一律 1/α 倍
      Parameters pc = coolingCalibration.Parameters;
      Parameters heating = new Parameters(
        pc.a_cmp / alpha, pc.b_cmp / alpha, pc.c_cmp / alpha,
        pc.d_cmp / alpha, pc.e_cmp / alpha, pc.f_cmp / alpha);

      // 加熱優先熱回収の定格点があれば、蒸発器側の回収チューブ KA も推定する
      double kaRcv = double.NaN;
      if (recoveryRated.HasValue)
      {
        CatalogPoint rr = recoveryRated.Value;
        SplitDuty(OperationMode.Heating, rr.Capacity, rr.PowerInput, out double qEvpRcv, out _);
        kaRcv = EstimateEvaporatorHeatTransfer(
          rr.EvaporatorInletTemperature, Mc(rr.EvaporatorFlowRate), qEvpRcv, evaporatorApproach);
      }

      return new ModeCalibration
      {
        Parameters = heating,
        EvaporatorHeatTransferCoefficient = sol.EvaporatorHeatTransferCoefficient,
        CondenserHeatTransferCoefficient = sol.CondenserHeatTransferCoefficient,
        RecoveryHeatTransferCoefficient = kaRcv,
        NominalHead = coolingCalibration.NominalHead,
        NominalFlowVolume = coolingCalibration.NominalFlowVolume,
        Alpha = alpha,
        RSquared = double.NaN
      };
    }

    /// <summary>
    /// Estimates the minimum flow coefficient ϕ_min — the lower boundary of the continuous
    /// capacity-control range — from a single catalog point at that boundary. Surge (and the
    /// hand-off to hot-gas bypass or on-off cycling) occurs at an approximately constant flow
    /// coefficient, and under the similarity transform this boundary is the speed-independent
    /// vertical line ϕ = ϕ_min in the (ϕ, Rw) plane; one point therefore suffices. The value
    /// is stored in <see cref="ModeCalibration.MinimumFlowCoefficient"/> and returned.
    /// </summary>
    /// <param name="mode">Operation mode.</param>
    /// <param name="refrigerant">Refrigerant fluid.</param>
    /// <param name="calibration">Calibration of the mode (KA and normalization references are used).</param>
    /// <param name="controlRangeLowerLimit">Catalog point at the lower end of the continuous
    /// capacity-control range (e.g. the minimum continuous load at the rated temperatures).</param>
    /// <param name="stageCount">Number of compressor impeller stages J.</param>
    public static double EstimateMinimumFlowCoefficient(
      OperationMode mode, Refrigerant.Fluid refrigerant, ModeCalibration calibration,
      CatalogPoint controlRangeLowerLimit, int stageCount = 2)
    {
      Refrigerant r = new Refrigerant(refrigerant);
      CyclePoint cyc = SolveCycle(
        mode, r, stageCount,
        calibration.EvaporatorHeatTransferCoefficient,
        calibration.CondenserHeatTransferCoefficient,
        controlRangeLowerLimit.EvaporatorInletTemperature, Mc(controlRangeLowerLimit.EvaporatorFlowRate),
        controlRangeLowerLimit.CondenserInletTemperature, Mc(controlRangeLowerLimit.CondenserFlowRate),
        controlRangeLowerLimit.Capacity, controlRangeLowerLimit.PowerInput);
      double phi = (cyc.VolumetricFlow / calibration.NominalFlowVolume)
        / Math.Sqrt(cyc.StageOneHead / calibration.NominalHead);
      calibration.MinimumFlowCoefficient = phi;
      return phi;
    }

    /// <summary>
    /// Identifies the cycling power weight θ of paper Eq.13 by least squares from catalog
    /// points below the continuous capacity-control range. For each point the forward model
    /// gives the minimum continuous power E_min and the flow coefficient ϕ at the point's
    /// water-side conditions, so Eq.13 becomes a regression through the origin:
    /// y = θ·x with y = E_cat/E_min − 1 and x = ϕ/ϕ_min − 1. The estimate is clipped to
    /// [0, 1] (the range that guarantees monotonically decreasing power), stored in
    /// <see cref="ModeCalibration.CyclingPowerWeight"/> and returned. Points that do not lie
    /// below the range (ϕ ≥ ϕ_min) are ignored.
    /// </summary>
    /// <param name="mode">Operation mode.</param>
    /// <param name="refrigerant">Refrigerant fluid.</param>
    /// <param name="calibration">Calibration of the mode (ϕ_min must be estimated beforehand).</param>
    /// <param name="maximumPower">Maximum electric power input E_max [kW].</param>
    /// <param name="subRangePoints">Catalog points below the capacity-control range.</param>
    /// <param name="stageCount">Number of compressor impeller stages J.</param>
    public static double EstimateCyclingPowerWeight(
      OperationMode mode, Refrigerant.Fluid refrigerant, ModeCalibration calibration,
      double maximumPower, IReadOnlyList<CatalogPoint> subRangePoints, int stageCount = 2)
    {
      if (double.IsNaN(calibration.MinimumFlowCoefficient))
        throw new InvalidOperationException(
          "The minimum flow coefficient has not been calibrated. " +
          "Call EstimateMinimumFlowCoefficient first.");
      if (subRangePoints == null || subRangePoints.Count == 0)
        throw new ArgumentException("At least one sub-range point is required.", nameof(subRangePoints));

      double phiMin = calibration.MinimumFlowCoefficient;

      // θ=0 なら Solve の消費電力がそのまま E_min になる。呼び出し元の calibration を
      // 一時的に書き換えるとスレッド安全でないため、θ=0 のクローンで順モデルを解く。
      ModeCalibration zeroTheta = CloneWithZeroTheta(calibration);
      CentrifugalHeatPump hp = (mode == OperationMode.Cooling)
        ? new CentrifugalHeatPump(refrigerant, maximumPower, zeroTheta, null, stageCount)
        : new CentrifugalHeatPump(refrigerant, maximumPower, null, zeroTheta, stageCount);

      double sxx = 0.0, sxy = 0.0;
      int used = 0;
      foreach (CatalogPoint pt in subRangePoints)
      {
        double setpoint = (mode == OperationMode.Cooling)
          ? pt.EvaporatorInletTemperature - pt.Capacity / Mc(pt.EvaporatorFlowRate)
          : pt.CondenserInletTemperature + pt.Capacity / Mc(pt.CondenserFlowRate);
        Operation op = hp.Solve(mode,
          pt.EvaporatorInletTemperature, pt.EvaporatorFlowRate,
          pt.CondenserInletTemperature, pt.CondenserFlowRate, setpoint);
        if (!op.IsBelowControlRange) continue;
        double x = op.FlowCoefficient / phiMin - 1.0;
        double y = pt.PowerInput / op.PowerConsumption - 1.0;
        sxx += x * x;
        sxy += x * y;
        used++;
      }
      if (used == 0)
        throw new ArgumentException(
          "None of the given points lies below the capacity-control range (ϕ < ϕ_min).",
          nameof(subRangePoints));

      double theta = Math.Clamp(sxy / sxx, 0.0, 1.0);
      calibration.CyclingPowerWeight = theta;
      return theta;
    }

    /// <summary>Shallow copy of a calibration with θ = 0 (used internally to obtain E_min
    /// without temporarily mutating the caller's calibration).</summary>
    private static ModeCalibration CloneWithZeroTheta(ModeCalibration c)
      => new ModeCalibration
      {
        Parameters = c.Parameters,
        EvaporatorHeatTransferCoefficient = c.EvaporatorHeatTransferCoefficient,
        CondenserHeatTransferCoefficient = c.CondenserHeatTransferCoefficient,
        RecoveryHeatTransferCoefficient = c.RecoveryHeatTransferCoefficient,
        Alpha = c.Alpha,
        NominalHead = c.NominalHead,
        NominalFlowVolume = c.NominalFlowVolume,
        RSquared = c.RSquared,
        ConstraintActivated = c.ConstraintActivated,
        MinimumFlowCoefficient = c.MinimumFlowCoefficient,
        CyclingPowerWeight = 0.0
      };

    /// <summary>Coefficient of determination R² from the response vector and residual sum of squares.</summary>
    private static double RSquared(double[] y, double rss)
    {
      double mean = 0.0;
      for (int i = 0; i < y.Length; i++) mean += y[i];
      mean /= y.Length;
      double tss = 0.0;
      for (int i = 0; i < y.Length; i++) tss += (y[i] - mean) * (y[i] - mean);
      return (tss <= 0.0) ? 1.0 : 1.0 - rss / tss;
    }

    #endregion

    #region 理論サイクルの一括求解

    /// <summary>
    /// Derives the normalization references (w1,N, vR,N) and the heat-transfer coefficients
    /// from the rated point, then solves the theoretical cycle at every catalog point.
    /// Returns the per-point cycle quantities needed to build the ϕ/Rw characteristic
    /// (internal diagnostics; not part of the public API).
    /// </summary>
    internal static CycleSolution SolveCyclePoints(
      OperationMode mode, Refrigerant.Fluid refrigerant,
      CatalogPoint rated, IReadOnlyList<CatalogPoint> pts,
      double condenserApproach, double evaporatorApproach, int stageCount = 2)
    {
      Refrigerant r = new Refrigerant(refrigerant);
      EstimateHeatTransferAndNominal(
        mode, r, rated, condenserApproach, evaporatorApproach, stageCount,
        out double kaEvp, out double kaCnd, out double headN, out double flowN);

      var rows = new List<CyclePoint>(pts.Count);
      foreach (CatalogPoint pt in pts)
      {
        rows.Add(SolveCycle(
          mode, r, stageCount, kaEvp, kaCnd,
          pt.EvaporatorInletTemperature, Mc(pt.EvaporatorFlowRate),
          pt.CondenserInletTemperature, Mc(pt.CondenserFlowRate),
          pt.Capacity, pt.PowerInput));
      }
      return new CycleSolution
      {
        NominalHead = headN,
        NominalFlowVolume = flowN,
        EvaporatorHeatTransferCoefficient = kaEvp,
        CondenserHeatTransferCoefficient = kaCnd,
        Points = rows
      };
    }

    #endregion

    #region 熱通過率・正規化基準の推定

    /// <summary>
    /// Estimates the evaporator/condenser overall heat-transfer coefficients [kW/K] from the
    /// rated point (using the approach temperatures), and the normalization references
    /// (nominal stage-1 head w1,N [kJ/kg] and nominal volume flow vR,N [m³/s]) by solving the
    /// theoretical cycle at the rated point.
    /// </summary>
    private static void EstimateHeatTransferAndNominal(
      OperationMode mode, Refrigerant r, CatalogPoint rated,
      double condenserApproach, double evaporatorApproach, int stageCount,
      out double evaporatorKA, out double condenserKA, out double nominalHead, out double nominalFlow)
    {
      double evapMc = Mc(rated.EvaporatorFlowRate);
      double condMc = Mc(rated.CondenserFlowRate);
      SplitDuty(mode, rated.Capacity, rated.PowerInput, out double qEvp, out double qCnd);

      evaporatorKA = EstimateEvaporatorHeatTransfer(
        rated.EvaporatorInletTemperature, evapMc, qEvp, evaporatorApproach);
      condenserKA = EstimateCondenserHeatTransfer(
        rated.CondenserInletTemperature, condMc, qCnd, condenserApproach);

      CyclePoint cyc = SolveCycle(
        mode, r, stageCount, evaporatorKA, condenserKA,
        rated.EvaporatorInletTemperature, evapMc,
        rated.CondenserInletTemperature, condMc,
        rated.Capacity, rated.PowerInput);
      nominalHead = cyc.StageOneHead;
      nominalFlow = cyc.VolumetricFlow;
    }

    /// <summary>
    /// Estimates a condenser-side overall heat-transfer coefficient [kW/K] from the water-side
    /// conditions and the approach temperature (LMTD-based; the water is heated from the inlet
    /// temperature by the duty, and the condensing temperature sits above the outlet by the
    /// approach). Used for the heat-rejection tube (KA_cnd) and the recovery tube (KA_cnd,rcv).
    /// </summary>
    private static double EstimateCondenserHeatTransfer(
      double inletTemperature, double mc, double duty, double approach)
    {
      double waterOut = inletTemperature + duty / mc;
      double tCnd = waterOut + approach;
      double c1 = tCnd - inletTemperature;
      double c2 = tCnd - waterOut;
      return duty / ((c1 - c2) / Math.Log(c1 / c2));
    }

    /// <summary>
    /// Estimates an evaporator-side overall heat-transfer coefficient [kW/K] from the water-side
    /// conditions and the approach temperature (LMTD-based; the water is cooled from the inlet
    /// temperature by the duty, and the evaporating temperature sits below the outlet by the
    /// approach). Used for the main evaporator (KA_evp) and the heating-mode recovery tube.
    /// </summary>
    private static double EstimateEvaporatorHeatTransfer(
      double inletTemperature, double mc, double duty, double approach)
    {
      double waterOut = inletTemperature - duty / mc;
      double tEvp = waterOut - approach;
      double e1 = inletTemperature - tEvp;
      double e2 = waterOut - tEvp;
      return duty / ((e1 - e2) / Math.Log(e1 / e2));
    }

    #endregion

    #region 理論冷凍サイクル

    /// <summary>
    /// Solves the steady J-stage theoretical refrigeration cycle for one operating point and
    /// returns the stage-1 isentropic head [kJ/kg], the total isentropic power [kW], and the
    /// stage-1 suction volumetric flow rate [m³/s].
    /// </summary>
    private static CyclePoint SolveCycle(
      OperationMode mode, Refrigerant r, int stages,
      double evaporatorKA, double condenserKA,
      double evaporatorInletT, double evaporatorMc,
      double condenserInletT, double condenserMc,
      double capacity, double power)
    {
      SplitDuty(mode, capacity, power, out double qEvp, out double qCnd);

      // 蒸発温度（Eq.25）と凝縮温度（Eq.18）を水側の熱収支から求める
      double epsEvp = 1.0 - Math.Exp(-evaporatorKA / evaporatorMc);
      double tEvp = evaporatorInletT - qEvp / (epsEvp * evaporatorMc);
      double epsCnd = 1.0 - Math.Exp(-condenserKA / condenserMc);
      double tCnd = condenserInletT + qCnd / (epsCnd * condenserMc);

      return SolveCycleCore(r, stages, tEvp, tCnd, qEvp);
    }

    /// <summary>
    /// Solves the steady J-stage theoretical refrigeration cycle for known evaporating and
    /// condensing temperatures and a given evaporator heat [kW].
    /// </summary>
    private static CyclePoint SolveCycleCore(
      Refrigerant r, int stages, double tEvp, double tCnd, double qEvp)
    {
      // 蒸発圧力（飽和）と1段目吸込（飽和蒸気）
      r.GetSaturatedPropertyFromTemperature(PhysicsConstants.ToKelvin(tEvp),
        out _, out double dvEvp, out double pEvp);
      double hVapEvp = r.GetEnthalpyFromTemperatureAndDensity(PhysicsConstants.ToKelvin(tEvp), dvEvp);

      // 凝縮圧力（飽和）と凝縮器出口（飽和液）
      r.GetSaturatedPropertyFromTemperature(PhysicsConstants.ToKelvin(tCnd),
        out double dlCnd, out _, out double pCnd);
      double hLiqCnd = r.GetEnthalpyFromTemperatureAndDensity(PhysicsConstants.ToKelvin(tCnd), dlCnd);

      // 各段の出口圧力（等圧縮比, Eq.32–33）。pOut[0]=pEvp ... pOut[stages]=pCnd
      double rp = Math.Pow(pCnd / pEvp, 1.0 / stages);
      double[] pOut = new double[stages + 1];
      pOut[0] = pEvp;
      for (int j = 1; j < stages; j++) pOut[j] = pEvp * Math.Pow(rp, j);
      pOut[stages] = pCnd;

      // 各中間圧の飽和液・飽和蒸気の比エンタルピー（hsl[j], hsv[j] @ pOut[j]）
      double[] hsl = new double[stages + 1];
      double[] hsv = new double[stages + 1];
      for (int j = 1; j <= stages; j++)
      {
        r.GetSaturatedPropertyFromPressure(pOut[j], out double dlj, out double dvj, out double tj);
        hsl[j] = r.GetEnthalpyFromTemperatureAndDensity(tj, dlj);
        hsv[j] = r.GetEnthalpyFromTemperatureAndDensity(tj, dvj);
      }
      hsl[stages] = hLiqCnd;

      // 液のカスケード：flowStage[j] = j段目を流れる冷媒流量 = mR,cmp(j)（Eq.34–36）
      // flowStage[1]=mR,evp、flowStage[j+1]=flowStage[j]/(1−f[j])、eco[j]=flowStage[j+1]−flowStage[j]
      double[] flowStage = new double[stages + 1];
      double[] eco = new double[stages];               // eco[1..stages-1]
      flowStage[1] = qEvp / (hVapEvp - hsl[1]);         // mR,evp（蒸発器入口は最下位中間圧の飽和液）
      for (int j = 1; j <= stages - 1; j++)
      {
        double f = (hsl[j + 1] - hsl[j]) / (hsv[j] - hsl[j]);
        flowStage[j + 1] = flowStage[j] / (1.0 - f);
        eco[j] = flowStage[j + 1] - flowStage[j];
      }

      // 段ごとの断熱ヘッドと動力（Eq.14, 38）
      double hIn = hVapEvp;
      double rhoIn = dvEvp;
      double kappaIn = r.GetSpecificHeatRatioFromTemperatureAndDensity(PhysicsConstants.ToKelvin(tEvp), dvEvp);

      double stageOneHead = 0.0;
      double isentropicPower = 0.0;
      for (int j = 1; j <= stages; j++)
      {
        double pin = pOut[j - 1];
        double pou = pOut[j];
        double k = (kappaIn - 1.0) / kappaIn;
        double w = (pin / rhoIn) / k * (Math.Pow(pou / pin, k) - 1.0);   // [kJ/kg]
        if (j == 1) stageOneHead = w;
        isentropicPower += flowStage[j] * w;                            // Σ mR,cmp(j)·w_j [kW]

        if (j < stages)
        {
          double hOut = hIn + w;   // 等エントロピー吐出比エンタルピー
          // 次段入口＝前段吐出とエコノマイザ蒸気の混合（Eq.37）
          double hMix = (hOut * flowStage[j] + hsv[j] * eco[j]) / flowStage[j + 1];
          r.GetStateFromPressureAndEnthalpy(pou, hMix,
            out double tMix, out double dMix, out _, out _);   // tMix は [K]
          hIn = hMix;
          rhoIn = dMix;
          kappaIn = r.GetSpecificHeatRatioFromTemperatureAndDensity(tMix, dMix);
        }
      }

      return new CyclePoint
      {
        StageOneHead = stageOneHead,
        IsentropicPower = isentropicPower,
        VolumetricFlow = flowStage[1] / dvEvp,
        EvaporatingTemperature = tEvp,
        CondensingTemperature = tCnd
      };
    }

    /// <summary>Splits the useful capacity and electric power into evaporator and condenser duties [kW].</summary>
    private static void SplitDuty(OperationMode mode, double capacity, double power,
      out double evaporatorDuty, out double condenserDuty)
    {
      if (mode == OperationMode.Cooling)
      {
        evaporatorDuty = capacity;            // Q_evp（有効能力）
        condenserDuty = capacity + power;     // Q_cnd = Q_evp + E
      }
      else
      {
        condenserDuty = capacity;             // Q_cnd（有効能力）
        evaporatorDuty = capacity - power;    // Q_evp = Q_cnd − E
      }
    }

    /// <summary>Heat-capacity rate [kW/K] from water mass flow rate [kg/s].</summary>
    private static double Mc(double waterFlowRate)
      => waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;

    #endregion

    #region 順モデル（消費電力の予測、Figure 6 の計算フロー）

    /// <summary>Convergence tolerance on the power residual [kW] for the root-finding solves.</summary>
    private const double PowerTolerance = 1.0e-6;

    /// <summary>Upper limit of E_max relative to the largest rated power. Explicit values at or
    /// above this ratio are rejected as implausible for a centrifugal machine.</summary>
    public const double MaximumPowerRatioLimit = 1.5;

    /// <summary>
    /// Resolves the maximum electric power input E_max [kW] used as the capacity ceiling of the
    /// forward model. When <paramref name="explicitMaximumPower"/> is null, the largest rated
    /// power among the given modes is used conservatively (motors are sized for the worst-case
    /// mode, so the machine can draw this much in any mode). An explicit value must lie between
    /// the largest rated power (inclusive) and <see cref="MaximumPowerRatioLimit"/> times it
    /// (exclusive); values outside this range throw.
    /// </summary>
    /// <param name="explicitMaximumPower">User-specified E_max [kW], or null to use the default.</param>
    /// <param name="ratedPowers">Rated power of each available operation mode [kW].</param>
    public static double ResolveMaximumPower(double? explicitMaximumPower, params double[] ratedPowers)
    {
      if (ratedPowers == null || ratedPowers.Length == 0)
        throw new ArgumentException("At least one rated power is required.", nameof(ratedPowers));
      double largestRated = ratedPowers[0];
      for (int i = 1; i < ratedPowers.Length; i++)
        largestRated = Math.Max(largestRated, ratedPowers[i]);
      if (largestRated <= 0.0)
        throw new ArgumentOutOfRangeException(nameof(ratedPowers), "Rated powers must be positive.");

      if (!explicitMaximumPower.HasValue) return largestRated;

      double eMax = explicitMaximumPower.Value;
      if (eMax < largestRated)
        throw new ArgumentOutOfRangeException(nameof(explicitMaximumPower), eMax,
          $"E_max must not be smaller than the largest rated power ({largestRated:F1} kW); " +
          "otherwise the rated operating point itself would be unreachable.");
      if (MaximumPowerRatioLimit * largestRated <= eMax)
        throw new ArgumentOutOfRangeException(nameof(explicitMaximumPower), eMax,
          $"E_max must be smaller than {MaximumPowerRatioLimit:F1} times the largest rated power " +
          $"({MaximumPowerRatioLimit * largestRated:F1} kW).");
      return eMax;
    }

    /// <summary>
    /// Solves the operating state for given water-side inputs (paper Figure 6).
    /// First checks whether the machine can meet the useful-side demand within the maximum
    /// power (overload → capacity is curtailed at E = E_max); when heat recovery is demanded,
    /// the recovery amount (none / partial / full) is then determined. Every convergence
    /// calculation is a bracketed one-dimensional root-finding problem (Brent's method).
    /// In cooling mode the useful side is the evaporator (chilled water) and recovery raises
    /// the condensing temperature (the larger of Eq.18 and Eq.22 governs); in heating mode the
    /// useful side is the condenser (hot water) and recovery of chilled water lowers the
    /// evaporating temperature (the smaller of the mirrored requirements governs). The
    /// recoverable heat is capped at the total heat of the recovery-side exchanger (Eq.42).
    /// When the calibration carries a minimum flow coefficient ϕ_min and the solved state
    /// (with E &lt; E_max) falls below it, the demand lies outside the continuous
    /// capacity-control range and the power is recomputed by the cycling rule (Eq.13) from
    /// the minimum continuous load Q_min.
    /// </summary>
    /// <param name="mode">Operation mode (its calibration must have been given to the constructor).</param>
    /// <param name="evaporatorInletTemperature">Evaporator-side water inlet temperature [°C]
    /// (cooling: chilled water; heating: heat source water).</param>
    /// <param name="evaporatorFlowRate">Evaporator-side water mass flow rate [kg/s].</param>
    /// <param name="condenserInletTemperature">Condenser-side water inlet temperature [°C]
    /// (cooling: cooling water; heating: hot water).</param>
    /// <param name="condenserFlowRate">Condenser-side water mass flow rate [kg/s].</param>
    /// <param name="outletTemperatureSetpoint">Outlet temperature setpoint of the useful side [°C]
    /// (cooling: chilled water outlet; heating: hot water outlet).</param>
    /// <param name="recoveryDemand">Heat-recovery demand (null or zero flow = no recovery).</param>
    public Operation Solve(
      OperationMode mode,
      double evaporatorInletTemperature, double evaporatorFlowRate,
      double condenserInletTemperature, double condenserFlowRate,
      double outletTemperatureSetpoint,
      HeatRecoveryDemand? recoveryDemand = null)
    {
      bool isCooling = mode == OperationMode.Cooling;
      ModeCalibration? calibrationOrNull = isCooling ? coolingCalibration : heatingCalibration;
      if (calibrationOrNull == null)
        throw new InvalidOperationException(
          $"The {mode} mode is not available: no calibration for it was given to the constructor.");
      ModeCalibration calibration = calibrationOrNull;
      if (!(0.0 < evaporatorFlowRate))
        throw new ArgumentOutOfRangeException(nameof(evaporatorFlowRate), evaporatorFlowRate,
          "Water mass flow rates must be positive.");
      if (!(0.0 < condenserFlowRate))
        throw new ArgumentOutOfRangeException(nameof(condenserFlowRate), condenserFlowRate,
          "Water mass flow rates must be positive.");

      double maximumPower = this.maximumPower;
      int stageCount = this.stageCount;
      Refrigerant r = refrigerant;
      double mcEvp = Mc(evaporatorFlowRate);
      double mcCnd = Mc(condenserFlowRate);
      double epsEvp = 1.0 - Math.Exp(-calibration.EvaporatorHeatTransferCoefficient / mcEvp);
      double epsCnd = 1.0 - Math.Exp(-calibration.CondenserHeatTransferCoefficient / mcCnd);

      // 有効側の需要（Eq.40/41）：冷却=蒸発器（冷水）、加熱=凝縮器（温水）。需要が無ければ停止。
      double qDmd = isCooling
        ? mcEvp * (evaporatorInletTemperature - outletTemperatureSetpoint)
        : mcCnd * (outletTemperatureSetpoint - condenserInletTemperature);
      if (qDmd <= 0.0)
        return new Operation
        {
          EvaporatorOutletTemperature = evaporatorInletTemperature,
          CondenserOutletTemperature = condenserInletTemperature,
          EvaporatingTemperature = evaporatorInletTemperature,
          CondensingTemperature = condenserInletTemperature
        };

      // 回収需要（Eq.41）：冷却時の回収水=温水（加熱される）、加熱時の回収水=冷水（冷却される）
      HeatRecoveryDemand rcv = recoveryDemand ?? default;
      double dtRcv = isCooling
        ? rcv.OutletTemperature - rcv.InletTemperature
        : rcv.InletTemperature - rcv.OutletTemperature;
      bool recoveryDemanded = recoveryDemand.HasValue && rcv.FlowDemand > 0.0 && dtRcv > 0.0;
      double qRcvDmd = recoveryDemanded ? Mc(rcv.FlowDemand) * dtRcv : 0.0;
      if (recoveryDemanded && double.IsNaN(calibration.RecoveryHeatTransferCoefficient))
        throw new InvalidOperationException(
          "Heat recovery was demanded, but the recovery-tube heat-transfer coefficient " +
          "has not been calibrated. Pass the heat-recovery rated point to the calibration.");

      // E*計算：仮定した (有効側熱量, E, 回収熱) から消費電力を計算し直す。
      // 冷却：凝縮温度は放熱側要求（Eq.18）と回収側要求（Eq.22）の大きい方。
      // 加熱：蒸発温度は熱源側要求と回収側要求の小さい方（鏡像）。回収熱はEq.42で制限。
      (double eStar, double tEvp, double tCnd, double qRcv, double phi) EStar(
        double qUseful, double eAssumed, double qRcvTarget)
      {
        double qEvp, qCnd, qRcv, tEvp, tCnd;
        if (isCooling)
        {
          qEvp = qUseful;
          qCnd = qEvp + eAssumed;
          qRcv = Math.Min(qRcvTarget, qCnd);                                        // Eq.42
          tEvp = evaporatorInletTemperature - qEvp / (epsEvp * mcEvp);              // Eq.25
          tCnd = condenserInletTemperature + (qCnd - qRcv) / (epsCnd * mcCnd);      // Eq.18
          if (qRcv > 0.0)
          {
            double mcR = qRcv / dtRcv;   // 出口温度を設定値とする回収水の熱容量流量 [kW/K]
            double epsR = 1.0 - Math.Exp(-calibration.RecoveryHeatTransferCoefficient / mcR);
            tCnd = Math.Max(tCnd, rcv.InletTemperature + qRcv / (epsR * mcR));      // Eq.22
          }
        }
        else
        {
          qCnd = qUseful;
          qEvp = qCnd - eAssumed;
          qRcv = Math.Min(qRcvTarget, qEvp);                                        // Eq.42（加熱版）
          tCnd = condenserInletTemperature + qCnd / (epsCnd * mcCnd);
          tEvp = evaporatorInletTemperature - (qEvp - qRcv) / (epsEvp * mcEvp);
          if (qRcv > 0.0)
          {
            double mcR = qRcv / dtRcv;
            double epsR = 1.0 - Math.Exp(-calibration.RecoveryHeatTransferCoefficient / mcR);
            tEvp = Math.Min(tEvp, rcv.InletTemperature - qRcv / (epsR * mcR));
          }
        }
        CyclePoint cyc = SolveCycleCore(r, stageCount, tEvp, tCnd, qEvp);
        double rv = cyc.VolumetricFlow / calibration.NominalFlowVolume;
        double rw = cyc.StageOneHead / calibration.NominalHead;
        double phi = rv / Math.Sqrt(rw);
        return (calibration.Parameters.PredictPower(cyc.IsentropicPower, phi, rw), tEvp, tCnd, qRcv, phi);
      }

      // E*計算（回収側の冷媒温度を直接指定する版）：回収熱→0の極限では熱通過有効度が1に
      // 漸近し、回収側の要求温度はちょうど回収水の出口温度になるため、極限の消費電力は
      // これで厳密に計算できる。
      double EStarAtLimit(double qUseful, double eAssumed, double tRefrigerant)
      {
        double qEvp, tEvp, tCnd;
        if (isCooling)
        {
          qEvp = qUseful;
          tEvp = evaporatorInletTemperature - qEvp / (epsEvp * mcEvp);
          tCnd = tRefrigerant;
        }
        else
        {
          qEvp = qUseful - eAssumed;
          tCnd = condenserInletTemperature + qUseful / (epsCnd * mcCnd);
          tEvp = tRefrigerant;
        }
        CyclePoint cyc = SolveCycleCore(r, stageCount, tEvp, tCnd, qEvp);
        double rv = cyc.VolumetricFlow / calibration.NominalFlowVolume;
        double rw = cyc.StageOneHead / calibration.NominalHead;
        double phi = rv / Math.Sqrt(rw);
        return calibration.Parameters.PredictPower(cyc.IsentropicPower, phi, rw);
      }

      // 1) 熱回収を0と仮定して容量を判定する。加熱では E < Q_cnd（COP > 1）が
      //    成立するため、消費電力の探索上限を需要未満に制限する。
      double eUpper = isCooling ? maximumPower : Math.Min(maximumPower, qDmd * (1.0 - 1.0e-9));
      double eStar0 = EStar(qDmd, eUpper, 0.0).eStar;
      double e, qUse = qDmd, qRcv = 0.0;
      bool overloaded = false;
      HeatRecoveryLevel level = HeatRecoveryLevel.None;

      if (eUpper < eStar0)
      {
        // 過負荷：E = E_max に固定し、E* = E_max となる有効側熱量を求める
        overloaded = true;
        e = maximumPower;
        double qLower = isCooling ? qDmd * 1.0e-6 : maximumPower * (1.0 + 1.0e-6);
        qUse = Roots.Brent(qLower, qDmd, PowerTolerance,
          q => EStar(q, maximumPower, 0.0).eStar - maximumPower);
      }
      else
      {
        // 軽負荷：有効側熱量を需要に固定し、E* = E となる消費電力を求める
        e = Roots.Brent(eUpper * 1.0e-6, eUpper, PowerTolerance,
          x => EStar(qDmd, x, 0.0).eStar - x);

        if (recoveryDemanded)
        {
          // 回収可否は消費電力で判定する。回収に必要な冷媒温度が現状より不利（冷却:高い、
          // 加熱:低い）でも、冷凍機は水量を内部で絞ることで冷媒温度を動かせるため、温度の
          // 大小自体は制約にならず、最大消費電力（E_max）に収まるかだけが問題になる。
          // 回収水の目標出口温度が現状の冷媒温度で既に満たせる場合は、最小限の回収が現状の
          // 消費電力のまま可能なことが自明なので、E* 計算をスキップする。
          (_, double tEvpWst, double tCndWst, _, _) = EStar(qDmd, e, 0.0);
          bool minimalRecoveryFeasible = isCooling
            ? rcv.OutletTemperature < tCndWst
            : rcv.OutletTemperature > tEvpWst;
          if (!minimalRecoveryFeasible)
            minimalRecoveryFeasible =
              EStarAtLimit(qDmd, eUpper, rcv.OutletTemperature) < eUpper;

          if (!minimalRecoveryFeasible)
          {
            // 回収なし（わずかな回収でも E_max を超える）
          }
          else if (EStar(qDmd, eUpper, qRcvDmd).eStar <= eUpper)
          {
            // 全量回収：回収を反映した熱分担のもとで E* = E を解き直す
            e = Roots.Brent(eUpper * 1.0e-6, eUpper, PowerTolerance,
              x => EStar(qDmd, x, qRcvDmd).eStar - x);
            qRcv = Math.Min(qRcvDmd, isCooling ? qDmd + e : qDmd - e);
            // Eq.42 で制限された場合は需要全量には満たない（回収飽和）
            level = (qRcv < qRcvDmd) ? HeatRecoveryLevel.Partial : HeatRecoveryLevel.Full;
          }
          else
          {
            // 部分回収：E = E_max に固定し、E* = E_max となる回収熱を求める。
            // 下限側は最小回収チェックにより E* < E_max が保証されているため、根は必ず括り込める。
            e = eUpper;
            qRcv = Roots.Brent(qRcvDmd * 1.0e-9, qRcvDmd, PowerTolerance,
              q => EStar(qDmd, eUpper, q).eStar - eUpper);
            level = HeatRecoveryLevel.Partial;
          }
        }
      }

      // 確定した運転点の温度と出力を計算する
      (_, double tEvpFin, double tCndFin, double qRcvFin, double phiFin) = EStar(qUse, e, qRcv);

      // 容量制御範囲外（発停／ホットガスバイパス領域）の判定と補正（Eq.13）。
      // E < E_max で解けた運転点の ϕ が校正済みの ϕ_min を下回る場合、現在の水側条件で
      // ϕ = ϕ_min となる容量制御下限の負荷 Q_min を収束計算で求め（ϕ は負荷に対して
      // 単調増加）、そのときの消費電力 E_min を基準に重み θ で消費電力を補正する。
      // 冷媒温度は連続運転を仮想した値のまま報告する。
      bool belowRange = false;
      double qMinLoad = double.NaN;
      if (!overloaded && e < maximumPower
        && !double.IsNaN(calibration.MinimumFlowCoefficient)
        && phiFin < calibration.MinimumFlowCoefficient)
      {
        belowRange = true;
        double phiMin = calibration.MinimumFlowCoefficient;

        // 内側（E）の求解：残差 E* − E は縮小写像（dE*/dE = ∂E*/∂t_cnd / (ε·mc) ≪ 1）
        // なので、逐次代入（Picard反復）が数回で収束する。縮小が確認できない場合のみ
        // 有界区間のBrent法にフォールバックし、大域収束の保証を維持する。
        double eWarm = e;
        (double e, double phi) AtLoad(double q)
        {
          double eu = isCooling ? maximumPower : Math.Min(maximumPower, q * (1.0 - 1.0e-9));
          double ec = Math.Min(eWarm, eu);
          double prev = double.PositiveInfinity;
          for (int i = 0; i < 20; i++)
          {
            (double eStar, _, _, _, double phi) = EStar(q, ec, qRcv);
            double eNew = Math.Min(eStar, eu);
            double delta = Math.Abs(eNew - ec);
            ec = eNew;
            if (delta < PowerTolerance) { eWarm = ec; return (ec, phi); }
            if (prev <= delta) break;   // 縮小していない → フォールバック
            prev = delta;
          }
          double eb = Roots.Brent(eu * 1.0e-6, eu, PowerTolerance,
            x => EStar(q, x, qRcv).eStar - x);
          eWarm = eb;
          return (eb, EStar(q, eb, qRcv).phi);
        }

        // 外側（Q_min）の求解：ϕ は負荷にほぼ比例するため、相似則にもとづく推定値 q0 の
        // 近傍から括り込みを始める。ϕ(q) の単調性を利用して符号を確認し、必要なら拡張する。
        // ϕ(qUse) < ϕ_min は判定済みなので、下端は常に qUse まで戻せば括り込める。
        double q0 = qUse * (phiMin / phiFin);
        double qLo = Math.Max(qUse, 0.8 * q0);
        double qHi = 1.25 * q0;
        while (qUse < qLo && phiMin <= AtLoad(qLo).phi)
        {
          qHi = qLo;
          qLo = Math.Max(qUse, 0.8 * qLo);
        }
        for (int i = 0; AtLoad(qHi).phi < phiMin; i++)
        {
          if (60 <= i) throw new InvalidOperationException(
            "Failed to bracket the minimum continuously controllable load Q_min.");
          qLo = qHi;
          qHi *= 1.25;
        }
        // 残差はϕの次元。1e-4·ϕ_min は Q_min 換算で 0.1 kW 未満に相当する。
        qMinLoad = Roots.Brent(qLo, qHi, 1.0e-4 * phiMin, q => AtLoad(q).phi - phiMin);
        double eMin = AtLoad(qMinLoad).e;
        e = eMin * (1.0 + calibration.CyclingPowerWeight * (phiFin / phiMin - 1.0));   // Eq.13
      }

      double qEvpFin = isCooling ? qUse : Math.Max(0.0, qUse - e);
      double qCndFin = isCooling ? qUse + e : qUse;
      return new Operation
      {
        PowerConsumption = e,
        EvaporatorHeat = qEvpFin,
        CondenserHeat = qCndFin,
        HeatRecovery = qRcvFin,
        RecoveryFlowRate = (qRcvFin <= 0.0) ? 0.0
          : qRcvFin / dtRcv / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat),
        EvaporatorOutletTemperature = evaporatorInletTemperature
          - (qEvpFin - (isCooling ? 0.0 : qRcvFin)) / mcEvp,
        CondenserOutletTemperature = condenserInletTemperature
          + (qCndFin - (isCooling ? qRcvFin : 0.0)) / mcCnd,
        EvaporatingTemperature = tEvpFin,
        CondensingTemperature = tCndFin,
        FlowCoefficient = phiFin,
        IsOverloaded = overloaded,
        RecoveryLevel = level,
        IsBelowControlRange = belowRange,
        MinimumContinuousLoad = qMinLoad
      };
    }

    #endregion

    #region 公開データ構造

    /// <summary>
    /// One catalog operating point (the rated point is also expressed with this type).
    /// Water-side conditions use evaporator/condenser-side naming: in cooling mode the
    /// evaporator side is chilled water and the condenser side is cooling water; in heating
    /// mode the evaporator side is heat-source water and the condenser side is hot water.
    /// At the heat-recovery rated point the condenser side is the recovered hot water.
    /// </summary>
    /// <param name="EvaporatorInletTemperature">Evaporator-side water inlet temperature [°C].</param>
    /// <param name="EvaporatorFlowRate">Evaporator-side water mass flow rate [kg/s].</param>
    /// <param name="CondenserInletTemperature">Condenser-side water inlet temperature [°C].</param>
    /// <param name="CondenserFlowRate">Condenser-side water mass flow rate [kg/s].</param>
    /// <param name="Capacity">Useful capacity [kW] (cooling: evaporator heat; heating: condenser heat).</param>
    /// <param name="PowerInput">Electric power input E [kW].</param>
    public readonly record struct CatalogPoint(
      double EvaporatorInletTemperature,
      double EvaporatorFlowRate,
      double CondenserInletTemperature,
      double CondenserFlowRate,
      double Capacity,
      double PowerInput);

    /// <summary>Extent to which the heat-recovery demand is met.</summary>
    public enum HeatRecoveryLevel
    {
      /// <summary>No heat recovery.</summary>
      None,
      /// <summary>Part of the demand is met (limited by the rated power or by Eq.42).</summary>
      Partial,
      /// <summary>The full demand is met.</summary>
      Full
    }

    /// <summary>
    /// Heat-recovery demand (underlined inputs of paper Figure 5). The recovery water is
    /// hot water in cooling mode (heated: outlet &gt; inlet) and chilled water in heating mode
    /// (cooled: outlet &lt; inlet). The recovery-tube heat-transfer coefficient is a machine
    /// parameter and lives in <see cref="ModeCalibration.RecoveryHeatTransferCoefficient"/>,
    /// not here.
    /// </summary>
    /// <param name="InletTemperature">Recovery water inlet temperature [°C].</param>
    /// <param name="FlowDemand">Demanded recovery water mass flow rate [kg/s].</param>
    /// <param name="OutletTemperature">Recovery water outlet temperature [°C].</param>
    public readonly record struct HeatRecoveryDemand(
      double InletTemperature,
      double FlowDemand,
      double OutletTemperature);

    /// <summary>Operating state solved by <see cref="Solve"/> (outputs of paper Figure 5).</summary>
    public class Operation
    {
      /// <summary>Electric power input E [kW].</summary>
      public double PowerConsumption { get; init; }
      /// <summary>Evaporator heat Q_evp [kW] (cooling: useful capacity, smaller than the
      /// demand when overloaded; heating: Q_cnd − E, including the recovered chilled water).</summary>
      public double EvaporatorHeat { get; init; }
      /// <summary>Condenser heat Q_cnd [kW] (cooling: Q_evp + E, including the recovered hot
      /// water; heating: useful capacity, smaller than the demand when overloaded).</summary>
      public double CondenserHeat { get; init; }
      /// <summary>Recovered heat [kW] (cooling: hot-water side; heating: chilled-water side).</summary>
      public double HeatRecovery { get; init; }
      /// <summary>Produced recovery water mass flow rate [kg/s] (0 without recovery;
      /// cooling: hot water m_ht, heating: chilled water m_ch).</summary>
      public double RecoveryFlowRate { get; init; }
      /// <summary>Outlet temperature of the primary evaporator-side water [°C]
      /// (cooling: chilled water, above the setpoint when overloaded; heating: heat source water).</summary>
      public double EvaporatorOutletTemperature { get; init; }
      /// <summary>Outlet temperature of the primary condenser-side water [°C]
      /// (cooling: cooling water, lowered by recovery; heating: hot water).</summary>
      public double CondenserOutletTemperature { get; init; }
      /// <summary>Evaporating (saturation) temperature [°C].</summary>
      public double EvaporatingTemperature { get; init; }
      /// <summary>Condensing (saturation) temperature [°C].</summary>
      public double CondensingTemperature { get; init; }
      /// <summary>Flow coefficient ϕ = Rv/√Rw of the solved operating state (below the
      /// capacity-control range this is the value of the fictitious continuous operation
      /// at the demanded load). NaN when the machine is stopped (no demand).</summary>
      public double FlowCoefficient { get; init; } = double.NaN;
      /// <summary>True if the useful-side demand exceeds the capacity at the maximum power.</summary>
      public bool IsOverloaded { get; init; }
      /// <summary>Extent to which the heat-recovery demand is met.</summary>
      public HeatRecoveryLevel RecoveryLevel { get; init; }
      /// <summary>True when the demand lies below the continuous capacity-control range
      /// (ϕ &lt; ϕ_min) and the power was computed by the cycling rule (Eq.13).</summary>
      public bool IsBelowControlRange { get; init; }
      /// <summary>Minimum continuously controllable load Q_min [kW] under the given
      /// water-side conditions (solved only when the demand is below the control range;
      /// NaN otherwise).</summary>
      public double MinimumContinuousLoad { get; init; } = double.NaN;
    }

    /// <summary>Theoretical-cycle quantities at one operating point (internal diagnostics).</summary>
    internal readonly struct CyclePoint
    {
      /// <summary>Stage-1 suction volumetric flow rate vR [m³/s].</summary>
      public double VolumetricFlow { get; init; }
      /// <summary>Stage-1 isentropic head w1,is [kJ/kg].</summary>
      public double StageOneHead { get; init; }
      /// <summary>Total isentropic compression power W_cmp,is [kW].</summary>
      public double IsentropicPower { get; init; }
      /// <summary>Evaporating (saturation) temperature [°C].</summary>
      public double EvaporatingTemperature { get; init; }
      /// <summary>Condensing (saturation) temperature [°C].</summary>
      public double CondensingTemperature { get; init; }
    }

    /// <summary>Result of <see cref="SolveCyclePoints"/>: normalization references plus
    /// per-point cycle quantities (internal diagnostics).</summary>
    internal class CycleSolution
    {
      /// <summary>Nominal stage-1 isentropic head w1,N [kJ/kg].</summary>
      public double NominalHead { get; init; }
      /// <summary>Nominal stage-1 suction volumetric flow vR,N [m³/s].</summary>
      public double NominalFlowVolume { get; init; }
      /// <summary>Evaporator overall heat-transfer coefficient KA_evp [kW/K].</summary>
      public double EvaporatorHeatTransferCoefficient { get; init; }
      /// <summary>Condenser overall heat-transfer coefficient KA_cnd [kW/K].</summary>
      public double CondenserHeatTransferCoefficient { get; init; }
      /// <summary>Per-point cycle quantities, in input order.</summary>
      public IReadOnlyList<CyclePoint> Points { get; init; } = null!;
    }

    /// <summary>
    /// Characteristic-equation coefficients (paper Eq.11):
    /// η = 1/(a_cmp·ϕ² + b_cmp·ϕ + c_cmp·(ϕ⁻¹−1) + d_cmp·Rw² + e_cmp·Rw + f_cmp).
    /// </summary>
    public class Parameters
    {
      /// <summary>Coefficient a_cmp (ϕ² term).</summary>
      public double a_cmp { get; }
      /// <summary>Coefficient b_cmp (ϕ term).</summary>
      public double b_cmp { get; }
      /// <summary>Coefficient c_cmp (asymmetric (ϕ⁻¹−1) term).</summary>
      public double c_cmp { get; }
      /// <summary>Coefficient d_cmp (Rw² term).</summary>
      public double d_cmp { get; }
      /// <summary>Coefficient e_cmp (Rw term).</summary>
      public double e_cmp { get; }
      /// <summary>Coefficient f_cmp (constant term).</summary>
      public double f_cmp { get; }

      /// <summary>Initializes a new instance with the given coefficients.</summary>
      public Parameters(double a_cmp, double b_cmp, double c_cmp,
        double d_cmp, double e_cmp, double f_cmp)
      {
        this.a_cmp = a_cmp;
        this.b_cmp = b_cmp;
        this.c_cmp = c_cmp;
        this.d_cmp = d_cmp;
        this.e_cmp = e_cmp;
        this.f_cmp = f_cmp;
      }

      /// <summary>Evaluates 1/η at the given (ϕ, Rw).</summary>
      public double InverseEfficiency(double phi, double rw)
        => a_cmp * phi * phi + b_cmp * phi + c_cmp * (1.0 / phi - 1.0)
         + d_cmp * rw * rw + e_cmp * rw + f_cmp;

      /// <summary>Evaluates η at the given (ϕ, Rw).</summary>
      public double Efficiency(double phi, double rw)
        => 1.0 / InverseEfficiency(phi, rw);

      /// <summary>Predicts the electric power input E [kW] from W_cmp,is [kW] and (ϕ, Rw).</summary>
      public double PredictPower(double isentropicPower, double phi, double rw)
        => isentropicPower * InverseEfficiency(phi, rw);
    }

    /// <summary>Calibration result for one operation mode.</summary>
    public class ModeCalibration
    {
      /// <summary>Estimated characteristic coefficients.</summary>
      public Parameters Parameters { get; init; } = null!;
      /// <summary>Evaporator overall heat-transfer coefficient KA_evp [kW/K].</summary>
      public double EvaporatorHeatTransferCoefficient { get; init; }
      /// <summary>Condenser overall heat-transfer coefficient KA_cnd [kW/K].</summary>
      public double CondenserHeatTransferCoefficient { get; init; }
      /// <summary>Recovery-tube overall heat-transfer coefficient [kW/K]
      /// (cooling: condenser side KA_cnd,rcv; heating: evaporator side.
      /// NaN when no heat-recovery rated point was given to the calibration).</summary>
      public double RecoveryHeatTransferCoefficient { get; init; } = double.NaN;
      /// <summary>Efficiency conversion coefficient α (Eq.39); 1 for a directly fitted mode,
      /// and the estimated level correction when the characteristic is borrowed
      /// (the returned coefficients already include the 1/α scaling).</summary>
      public double Alpha { get; init; } = 1.0;
      /// <summary>Nominal stage-1 isentropic head w1,N [kJ/kg] (normalization reference).</summary>
      public double NominalHead { get; init; }
      /// <summary>Nominal stage-1 suction volumetric flow vR,N [m³/s] (normalization reference).</summary>
      public double NominalFlowVolume { get; init; }
      /// <summary>R² of the characteristic fit on the electric power E.</summary>
      public double RSquared { get; init; }
      /// <summary>True if the efficiency-peak constraint (a_cmp ≥ 0, d_cmp ≥ 0) was activated
      /// (the unconstrained least-squares solution violated it).</summary>
      public bool ConstraintActivated { get; init; }

      /// <summary>Minimum flow coefficient ϕ_min: the lower boundary of the continuous
      /// capacity-control range (surge line under the similarity transform). NaN (default)
      /// disables the sub-range handling; set via
      /// <see cref="EstimateMinimumFlowCoefficient"/> or directly.</summary>
      public double MinimumFlowCoefficient { get; set; } = double.NaN;

      private double cyclingPowerWeight = 0.0;
      /// <summary>Cycling power weight θ of paper Eq.13, in [0, 1]. 0 (default) is the
      /// conservative characteristic where the power stays at E_min below the control range
      /// (hot-gas bypass); 1 represents the smallest cycling loss. Set via
      /// <see cref="EstimateCyclingPowerWeight"/> or directly (system-dependent).</summary>
      public double CyclingPowerWeight
      {
        get => cyclingPowerWeight;
        set
        {
          if (value < 0.0 || 1.0 < value)
            throw new ArgumentOutOfRangeException(nameof(value),
              "The cycling power weight must lie in [0, 1] to keep the power monotonic.");
          cyclingPowerWeight = value;
        }
      }
    }

    #endregion

  }
}
