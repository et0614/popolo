/* Wall.cs
 * 
 * Copyright (C) 2015 E.Togashi
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
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using Popolo.Core.Numerics.LinearAlgebra;
using Popolo.Core.Physics;

using System;
using System.Collections.Generic;

namespace Popolo.Core.Building.Envelope
{
  /// <inheritdoc cref="IReadOnlyWall"/>
  /// <remarks>
  /// <para>
  /// This is the mutable implementation of <see cref="IReadOnlyWall"/>. Configure
  /// the wall by passing a <see cref="WallLayer"/> array to the constructor —
  /// the wall <b>clones</b> each layer, so later mutations of the original
  /// layer objects do not propagate. After construction, use
  /// <see cref="Initialize(double)"/> or <see cref="Initialize(double,double)"/>
  /// to set the initial temperature (and humidity) distribution, and install
  /// radiant pipes with <c>AddPipe</c>.
  /// </para>
  /// <para>
  /// The coefficient and inverse matrices of the response-factor model are
  /// cached and recomputed lazily: internal flags indicate when layer
  /// properties or pipe flow conditions have changed, and the matrices are
  /// rebuilt on the next solver step. This keeps the per-step cost low when
  /// properties are steady.
  /// </para>
  /// <para>
  /// Boundary attachment (outdoor, ground, adjacent space, or another zone)
  /// is owned by the enclosing <see cref="MultiRoom"/>, not by the wall
  /// itself; a wall therefore has no notion of "which side is outside" until
  /// it is placed. See <see cref="OutsideWallReference"/>,
  /// <see cref="GroundWallReference"/>, and
  /// <see cref="AdjacentSpaceWallReference"/> for the boundary kinds exposed
  /// to callers.
  /// </para>
  /// </remarks>
  public class Wall : IReadOnlyWall
  {

    #region 定数宣言

    /// <summary>Latent heat of vaporization of water at the triple point [J/kg].
    /// Converted from Water.VaporizationHeatAtTriplePoint [kJ/kg].</summary>
    private static readonly double VAPORIZATION_LATENT_HEAT = Water.VaporizationHeatAtTriplePoint * 1000.0;

    #endregion

    #region インスタンス変数・publicプロパティ

    /// <summary>Flag indicating whether the inverse matrix needs to be updated.</summary>
    private bool needToUpdateUINVMatrix = true;

    /// <summary>Flag indicating whether the coefficient matrix needs to be updated.</summary>
    internal bool needToUpdateUMatrix = false;

    /// <summary>Array of wall layers.</summary>
    private WallLayer[] layers;

    /// <summary>Indices of layers whose thermophysical properties may change with temperature.</summary>
    private int[] variableLayers = null!;

    /// <summary>Dictionary of buried pipes keyed by node index.</summary>
    private Dictionary<int, BuriedPipe> bPipes = new Dictionary<int, BuriedPipe>();

    /// <summary>Vector holding the temperature and humidity ratio distribution at each node.</summary>
    private IVector tempAndHumid = null!;

    /// <summary>Sensible heat resistance [m²·K/W] and heat capacity [J/(m²·K)] arrays.</summary>
    /// <remarks>Pre-allocated to avoid repeated heap allocation during simulation.</remarks>
    private double[] resS = null!, capS = null!;

    /// <summary>Moisture resistance [(kg/kg)·m²/(kg/s)] and moisture capacity [kg/m²] arrays (used only when ComputeMoistureTransfer is true).</summary>
    /// <remarks>Pre-allocated to avoid repeated heap allocation during simulation.</remarks>
    private double[] resL = null!, capL = null!, cNu = null!, cKappa = null!;

    /// <summary>Working matrices for the response factor calculation.</summary>
    /// <remarks>Pre-allocated to avoid repeated heap allocation during simulation.</remarks>
    private IMatrix uMatrix = null!, umWithTubeEffect = null!, uxMatrix = null!;

    /// <summary>Working arrays for sensible heat balance and radiant floor/ceiling calculation.</summary>
    /// <remarks>Pre-allocated to avoid repeated heap allocation during simulation.</remarks>
    private double[] uSF = null!, uSB = null!, uP = null!, uPF = null!, uPM = null!, uPB = null!;

    /// <summary>Working arrays for coupled heat and moisture transfer analysis (used only when ComputeMoistureTransfer is true).</summary>
    /// <remarks>Pre-allocated to avoid repeated heap allocation during simulation.</remarks>
    private double[] uSF2 = null!, uSB2 = null!, uLF2 = null!, uLB2 = null!, uSF3 = null!, uSB3 = null!, uLF3 = null!, uLB3 = null!;


    /// <summary>Gets or sets the wall ID.</summary>
    public int ID { get; set; }

    /// <summary>Gets a value indicating whether moisture transfer is solved.</summary>
    public bool ComputeMoistureTransfer { get; private set; }

    /// <summary>Gets the number of nodes in the finite difference model.</summary>
    public int NodeCount { get { return layers.Length + 1; } }

    /// <summary>Gets the temperature distribution vector [°C].</summary>
    public IVector Temperatures
    { get { return new VectorView(tempAndHumid, 0, NodeCount); } }

    /// <summary>Gets the humidity ratio distribution vector [kg/kg].</summary>
    public IVector Humidities
    { get { return new VectorView(tempAndHumid, NodeCount, NodeCount); } }

    /// <summary>Gets or sets the wall surface area [m²].</summary>
    public double Area { get; set; } = 1.0d;

    /// <summary>Calculation time step [s].</summary>
    private double timeStep = 3600;

    /// <summary>Gets or sets the calculation time step [s].</summary>
    public double TimeStep
    {
      get { return timeStep; }
      set
      {
        if (value <= 0 || timeStep == value) return;
        timeStep = value;
        needToUpdateUMatrix = true;
      }
    }

    /// <summary>Convective and radiative heat transfer coefficients on the F and B sides [W/(m²·K)].</summary>
    private double cCoefF, rCoefF, cCoefB, rCoefB;

    /// <summary>Gets the combined heat transfer coefficient on the F side [W/(m²·K)].</summary>
    public double FilmCoefficientF { get { return cCoefF + rCoefF; } }

    /// <summary>Gets or sets the convective heat transfer coefficient on the F side [W/(m²·K)].</summary>
    public double ConvectiveCoefficientF
    {
      get { return cCoefF; }
      set
      {
        if (cCoefF == value) return;
        needToUpdateUMatrix = true;
        cCoefF = value;
      }
    }

    /// <summary>Gets or sets the radiative heat transfer coefficient on the F side [W/(m²·K)].</summary>
    public double RadiativeCoefficientF
    {
      get { return rCoefF; }
      set
      {
        if (rCoefF == value) return;
        needToUpdateUMatrix = true;
        rCoefF = value;
      }
    }

    /// <summary>Gets the moisture transfer coefficient on the F side [(kg/s)/((kg/kg)·m²)].</summary>
    public double MoistureCoefficientF { get { return 1.0d / resL[0]; } }

    /// <summary>Gets or sets the short-wave (solar) absorptance on the F side [-].</summary>
    public double ShortWaveAbsorptanceF { get; set; } = 0.7;

    /// <summary>Gets or sets the long-wave (thermal) emissivity on the F side [-].</summary>
    public double LongWaveEmissivityF { get; set; } = 0.9;

    /// <summary>Gets or sets the sol-air temperature on the F side [°C].</summary>
    public double SolAirTemperatureF { get; set; }

    /// <summary>Gets or sets the humidity ratio on the F side [kg/kg].</summary>
    public double HumidityRatioF { get; set; }

    /// <summary>Gets the combined heat transfer coefficient on the B side [W/(m²·K)].</summary>
    public double FilmCoefficientB { get { return cCoefB + rCoefB; } }

    /// <summary>Gets or sets the convective heat transfer coefficient on the B side [W/(m²·K)].</summary>
    public double ConvectiveCoefficientB
    {
      get { return cCoefB; }
      set
      {
        if (cCoefB == value) return;
        needToUpdateUMatrix = true;
        cCoefB = value;
      }
    }

    /// <summary>Gets or sets the radiative heat transfer coefficient on the B side [W/(m²·K)].</summary>
    public double RadiativeCoefficientB
    {
      get { return rCoefB; }
      set
      {
        if (rCoefB == value) return;
        needToUpdateUMatrix = true;
        rCoefB = value;
      }
    }

    /// <summary>Gets the moisture transfer coefficient on the B side [(kg/s)/((kg/kg)·m²)].</summary>
    public double MoistureCoefficientB { get { return 1.0d / resL[resL.Length - 1]; } }

    /// <summary>Gets or sets the short-wave (solar) absorptance on the B side [-].</summary>
    public double ShortWaveAbsorptanceB { get; set; } = 0.7;

    /// <summary>Gets or sets the long-wave (thermal) emissivity on the B side [-].</summary>
    public double LongWaveEmissivityB { get; set; } = 0.9;

    /// <summary>Gets or sets the sol-air temperature on the B side [°C].</summary>
    public double SolAirTemperatureB { get; set; }

    /// <summary>Gets or sets the humidity ratio on the B side [kg/kg].</summary>
    public double HumidityRatioB { get; set; }

    /// <summary>Gets the wall layer array.</summary>
    public IReadOnlyWallLayer[] Layers { get { return layers; } }

    /// <summary>Gets the surface temperature on the F side [°C].</summary>
    public double SurfaceTemperatureF { get { return SurfaceF.SurfaceTemperature; } }

    /// <summary>Gets the surface temperature on the B side [°C].</summary>
    public double SurfaceTemperatureB { get { return SurfaceB.SurfaceTemperature; } }

    #endregion

    #region internalプロパティ（多数室計算用）

    /// <summary>Gets the boundary surface element on the F side.</summary>
    internal BoundarySurface SurfaceF { get; private set; } = null!;

    /// <summary>Gets the boundary surface element on the B side.</summary>
    internal BoundarySurface SurfaceB { get; private set; } = null!;

    /// <summary>Response factor coefficient: boundary condition term (F side × temperature).</summary>
    internal double IF2_F { get; private set; }

    /// <summary>Response factor coefficient: boundary condition term (B side × temperature).</summary>
    internal double IF2_B { get; private set; }

    /// <summary>Response factor coefficient: boundary condition term (F side × humidity).</summary>
    internal double IF3_F { get; private set; }

    /// <summary>Response factor coefficient: boundary condition term (B side × humidity).</summary>
    internal double IF3_B { get; private set; }

    /// <summary>Response factor: F-side sol-air temperature → F-side sensible heat (temperature term).</summary>
    internal double FFS2_F { get; private set; }

    /// <summary>Response factor: F-side sol-air temperature → B-side sensible heat (temperature term).</summary>
    internal double FFS2_B { get; private set; }

    /// <summary>Response factor: F-side sol-air temperature → F-side sensible heat (humidity term).</summary>
    internal double FFS3_F { get; private set; }

    /// <summary>Response factor: F-side sol-air temperature → B-side sensible heat (humidity term).</summary>
    internal double FFS3_B { get; private set; }

    /// <summary>Response factor: B-side sol-air temperature → F-side sensible heat (temperature term).</summary>
    internal double BFS2_F { get; private set; }

    /// <summary>Response factor: B-side sol-air temperature → B-side sensible heat (temperature term).</summary>
    internal double BFS2_B { get; private set; }

    /// <summary>Response factor: B-side sol-air temperature → F-side sensible heat (humidity term).</summary>
    internal double BFS3_F { get; private set; }

    /// <summary>Response factor: B-side sol-air temperature → B-side sensible heat (humidity term).</summary>
    internal double BFS3_B { get; private set; }

    /// <summary>Response factor: F-side humidity ratio → F-side sensible heat (temperature term).</summary>
    internal double FFL2_F { get; private set; }

    /// <summary>Response factor: F-side humidity ratio → B-side sensible heat (temperature term).</summary>
    internal double FFL2_B { get; private set; }

    /// <summary>Response factor: F-side humidity ratio → F-side sensible heat (humidity term).</summary>
    internal double FFL3_F { get; private set; }

    /// <summary>Response factor: F-side humidity ratio → B-side sensible heat (humidity term).</summary>
    internal double FFL3_B { get; private set; }

    /// <summary>Response factor: B-side humidity ratio → F-side sensible heat (temperature term).</summary>
    internal double BFL2_F { get; private set; }

    /// <summary>Response factor: B-side humidity ratio → B-side sensible heat (temperature term).</summary>
    internal double BFL2_B { get; private set; }

    /// <summary>Response factor: B-side humidity ratio → F-side sensible heat (humidity term).</summary>
    internal double BFL3_F { get; private set; }

    /// <summary>Response factor: B-side humidity ratio → B-side sensible heat (humidity term).</summary>
    internal double BFL3_B { get; private set; }

    /// <summary>Gets or sets a value indicating whether the inverse matrix has been updated.</summary>
    internal bool invMatrixUpdated { get; set; } = true;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="area">Surface area [m²].</param>
    /// <param name="layers">Wall layer composition.</param>
    public Wall(double area, WallLayer[] layers) : this(area, layers, false) { }

    /// <summary>Initializes a new instance, optionally enabling coupled moisture transfer.</summary>
    /// <param name="area">Surface area [m²].</param>
    /// <param name="layers">Wall layer composition (ordered from F side to B side). Each layer is cloned.</param>
    /// <param name="computeMoistureTransfer">
    /// True to solve coupled heat and moisture transport through the layer stack;
    /// false (default) to solve heat only.
    /// </param>
    /// <remarks>
    /// Enabling moisture transfer roughly doubles the per-step cost: the
    /// number of unknowns per node becomes two (temperature + humidity ratio),
    /// and a second set of response-factor coefficients is maintained. Each
    /// layer's <see cref="IReadOnlyWallLayer.MoistureConductivity"/>,
    /// <see cref="IReadOnlyWallLayer.WaterCapacity"/>, <see cref="IReadOnlyWallLayer.KappaC"/>,
    /// and <see cref="IReadOnlyWallLayer.NuC"/> must be populated for the
    /// moisture balance to be meaningful; otherwise the extra cost buys
    /// nothing. Enable only when hygric behavior (e.g., condensation risk
    /// inside an envelope) is of interest.
    /// </remarks>
    public Wall(double area, WallLayer[] layers, bool computeMoistureTransfer)
    {
      Area = area;
      this.layers = new WallLayer[layers.Length];
      for (int i = 0; i < layers.Length; i++) this.layers[i] = (WallLayer)layers[i].Clone();
      ComputeMoistureTransfer = computeMoistureTransfer;
      SurfaceF = new BoundarySurface(this, true);
      SurfaceB = new BoundarySurface(this, false);

      //計算領域を確保

      int mNum = this.layers.Length + 1; //質点数
      capS = new double[mNum];
      resS = new double[mNum + 1];
      int ssNum = mNum; //未知変数の数
      if (ComputeMoistureTransfer)
      {
        ssNum *= 2; //絶対湿度も未知のため倍
        uSF2 = new double[mNum];
        uSB2 = new double[mNum];
        uSF3 = new double[mNum];
        uSB3 = new double[mNum];
        uLF2 = new double[mNum];
        uLB2 = new double[mNum];
        uLF3 = new double[mNum];
        uLB3 = new double[mNum];
        cNu = new double[mNum];
        cKappa = new double[mNum];
        capL = new double[mNum];
        resL = new double[mNum + 1];
      }
      else
      {
        uSF = new double[ssNum];
        uSB = new double[ssNum];
      }
      tempAndHumid = new Vector(ssNum);
      uMatrix = new Matrix(ssNum, ssNum);
      umWithTubeEffect = new Matrix(ssNum, ssNum);
      uxMatrix = new Matrix(ssNum, ssNum);
      uP = new double[mNum];
      uPF = new double[mNum];
      uPM = new double[mNum];
      uPB = new double[mNum];

      //物性変化があり得る層の番号を保存
      List<int> tal = new List<int>();
      for (int i = 0; i < layers.Length; i++) if (layers[i].IsVariableProperties) tal.Add(i);
      variableLayers = tal.ToArray();

      ConvectiveCoefficientF = ConvectiveCoefficientB = 5.3;
      RadiativeCoefficientF = RadiativeCoefficientB = 4.5;
      needToUpdateUMatrix = true;
    }

    #endregion

    #region 温湿度計算関連の処理

    /// <summary>Advances the wall's internal state by one time step using the current sol-air conditions.</summary>
    /// <remarks>
    /// Per call, <see cref="Update"/>:
    /// <list type="bullet">
    ///   <item><description>rebuilds the inverse response-factor matrix if an input has changed (layer properties, pipe flow, film coefficients, time step);</description></item>
    ///   <item><description>applies <see cref="SolAirTemperatureF"/> / <see cref="SolAirTemperatureB"/> (and the two humidity ratios in moisture mode) to obtain the new node temperatures and humidities;</description></item>
    ///   <item><description>calls <c>UpdateState</c> on variable-property layers (e.g., <see cref="PCMWallLayer"/>, <see cref="HorizontalAirChamber"/>) and, if any property changed, schedules a matrix rebuild for the next call;</description></item>
    ///   <item><description>refreshes the surface-delay response factors (IF2 / IF3) used by the zone solver.</description></item>
    /// </list>
    /// Called internally by <see cref="MultiRoom"/> during each forecast /
    /// fix cycle; external code normally does not call <see cref="Update"/>
    /// directly.
    /// </remarks>
    public void Update()
    {
      //逆行列を更新
      UpdateInverseMatrix();

      //相当温度と絶対湿度で係数ベクトルを作成
      int mNum = layers.Length + 1;
      int last = mNum - 1;
      Vector tempAndHumid2 = new Vector(tempAndHumid.Length);
      tempAndHumid2.Initialize(0);
      if (ComputeMoistureTransfer)
      {
        tempAndHumid2[0] = uSF2[0] * SolAirTemperatureF + uLF2[0] * HumidityRatioF;
        tempAndHumid2[last] = uSB2[last] * SolAirTemperatureB + uLB2[last] * HumidityRatioB;
        tempAndHumid2[mNum] = uSF3[0] * SolAirTemperatureF + uLF3[0] * HumidityRatioF;
        tempAndHumid2[last + mNum] = uSB3[last] * SolAirTemperatureB + uLB3[last] * HumidityRatioB;
        for (int i = 0; i < mNum; i++)
        {
          if (capS[i] != 0 || cNu[i] != 0) tempAndHumid2[i] += tempAndHumid[i];
          if (capL[i] != 0 || cKappa[i] != 0) tempAndHumid2[i + mNum] += tempAndHumid[i + mNum];
        }
      }
      else
      {
        tempAndHumid2[0] = uSF[0] * SolAirTemperatureF;
        tempAndHumid2[last] = uSB[last] * SolAirTemperatureB;
        for (int i = 0; i < tempAndHumid2.Length; i++)
          if (capS[i] != 0) tempAndHumid2[i] += tempAndHumid[i];
      }
      //埋設配管の影響を加える
      foreach (int key in bPipes.Keys)
      {
        if (key == 0) tempAndHumid2[0] += uPF[0] * SolAirTemperatureF;
        if (key == last) tempAndHumid2[last] += uPB[last] * SolAirTemperatureB;
        tempAndHumid2[key] += uP[key] * bPipes[key].InletWaterTemperature;
      }
      //逆行列で解を求める
      LinearAlgebraOperations.Multiply(uxMatrix, tempAndHumid2, tempAndHumid, 1, 0);

      //物性変化があり得る層の状態を計算して行列更新の要否を確認
      for (int i = 0; i < variableLayers.Length; i++)
      {
        int lnum = variableLayers[i];
        bool flg;
        if (ComputeMoistureTransfer)
          flg = layers[lnum].UpdateState
            (tempAndHumid[lnum], tempAndHumid[lnum + 1], tempAndHumid[lnum + mNum], tempAndHumid[lnum + mNum] + 1);
        else flg = layers[lnum].UpdateState(tempAndHumid[lnum], tempAndHumid[lnum + 1]);
        if (flg) needToUpdateUMatrix = true;

        //PCMの場合には温度を調整
        const PCMWallLayer.State sOrE = PCMWallLayer.State.Solid | PCMWallLayer.State.Equilibrium;
        PCMWallLayer? pwl = layers[lnum] as PCMWallLayer;
        if (flg && (pwl != null))
        {
          if (pwl.CurrentState_F != pwl.LastState_F)
          {
            double temp;
            if ((pwl.CurrentState_F | pwl.LastState_F) == sOrE) temp = pwl.FreezingTemperature;
            else temp = pwl.MeltingTemperature;
            double cap1 = pwl.GetHeatCapacity(pwl.LastState_F);
            double cap2 = pwl.GetHeatCapacity(pwl.CurrentState_F);
            if (lnum == 0) tempAndHumid[0] = cap1 / cap2 * (tempAndHumid[0] - temp) + temp;
            else
            {
              double cap3 = layers[lnum - 1].HeatCapacity_F;
              tempAndHumid[lnum] = (tempAndHumid[lnum] * (cap1 + cap3) + temp * (cap2 - cap1)) / (cap2 + cap3);
            }
          }
          if (pwl.CurrentState_B != pwl.LastState_B)
          {
            double temp;
            if ((pwl.CurrentState_B | pwl.LastState_B) == sOrE) temp = pwl.FreezingTemperature;
            else temp = pwl.MeltingTemperature;
            double cap1 = pwl.GetHeatCapacity(pwl.LastState_B);
            double cap2 = pwl.GetHeatCapacity(pwl.CurrentState_B);
            if (lnum == layers.Length - 1)
              tempAndHumid[layers.Length - 1] = cap1 / cap2 * (tempAndHumid[layers.Length - 1] - temp) + temp;
            else
            {
              double cap3 = layers[lnum].HeatCapacity_B;
              tempAndHumid[lnum + 1] =
                (tempAndHumid[lnum + 1] * (cap1 + cap3) + temp * (cap2 - cap1)) / (cap2 + cap3);
            }
          }
        }
      }

      //係数IFを更新
      if (ComputeMoistureTransfer)
      {
        int nm = layers.Length;
        int nm1 = nm + 1;
        int nm2 = 2 * nm + 1;
        IF2_F = IF2_B = IF3_F = IF3_B = 0;
        for (int i = 0; i <= nm; i++)
        {
          double bf = tempAndHumid[i];
          int ipn = i + nm1;
          if (bPipes.ContainsKey(i)) bf += uP[i] * bPipes[i].InletWaterTemperature;
          IF2_F += uxMatrix[0, i] * bf + uxMatrix[0, ipn] * tempAndHumid[ipn];
          IF2_B += uxMatrix[nm, i] * bf + uxMatrix[nm, ipn] * tempAndHumid[ipn];
          IF3_F += uxMatrix[nm1, i] * bf + uxMatrix[nm1, ipn] * tempAndHumid[ipn];
          IF3_B += uxMatrix[nm2, i] * bf + uxMatrix[nm2, ipn] * tempAndHumid[ipn];
        }
      }
      else
      {
        int num = uxMatrix.Rows - 1;
        IF2_F = IF2_B = 0;
        for (int i = 0; i <= num; i++)
        {
          double bf = tempAndHumid[i];
          if (bPipes.ContainsKey(i)) bf += uP[i] * bPipes[i].InletWaterTemperature;
          IF2_F += uxMatrix[0, i] * bf;
          IF2_B += uxMatrix[num, i] * bf;
        }
      }
    }

    /// <summary>Resets the temperature distribution to a uniform value.</summary>
    /// <param name="temperature">Temperature [°C].</param>
    /// <remarks>
    /// Sets every node temperature and both sol-air temperatures to the
    /// given value, re-evaluates variable-property layers at that
    /// temperature, marks the coefficient matrix for rebuild, and runs one
    /// <see cref="Update"/> to establish a consistent initial state. Leaves
    /// humidity untouched; use
    /// <see cref="Initialize(double,double)"/> to reset both at once when
    /// moisture transfer is enabled.
    /// </remarks>
    public void Initialize(double temperature)
    {
      VectorView temp = new VectorView(tempAndHumid, 0, layers.Length + 1);
      temp.Initialize(temperature);
      for (int i = 0; i < variableLayers.Length; i++)
        layers[variableLayers[i]].UpdateState(temperature, temperature);
      SolAirTemperatureF = SolAirTemperatureB = temperature;
      needToUpdateUMatrix = true;
      Update();
    }

    /// <summary>Resets both the temperature and humidity ratio distributions to uniform values.</summary>
    /// <param name="temperature">Temperature [°C].</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg].</param>
    /// <remarks>
    /// No-op when <see cref="ComputeMoistureTransfer"/> is false (moisture
    /// state is not tracked in that mode); use
    /// <see cref="Initialize(double)"/> instead for a heat-only wall.
    /// </remarks>
    public void Initialize(double temperature, double humidityRatio)
    {
      if (!ComputeMoistureTransfer) return;
      int mnum = layers.Length + 1;
      new VectorView(tempAndHumid, 0, mnum).Initialize(temperature);
      new VectorView(tempAndHumid, mnum, mnum).Initialize(humidityRatio);
      for (int i = 0; i < variableLayers.Length; i++)
        layers[variableLayers[i]].UpdateState(temperature, temperature, humidityRatio, humidityRatio);
      SolAirTemperatureF = SolAirTemperatureB = temperature;
      HumidityRatioF = HumidityRatioB = humidityRatio;
      needToUpdateUMatrix = true;
      Update();
    }

    /// <summary>Gets the surface heat flux [W/m²]. Positive values indicate heat absorption.</summary>
    /// <param name="isSideF">True for the F (front) side; false for the B (back) side.</param>
    /// <returns>Surface heat flux [W/m²].</returns>
    public double GetSurfaceHeatTransfer(bool isSideF)
    {
      // (sol-air − surface) × h: positive when the surface absorbs heat from the sol-air.
      if (isSideF) return (SolAirTemperatureF - SurfaceF.SurfaceTemperature) * FilmCoefficientF;
      else return (SolAirTemperatureB - SurfaceB.SurfaceTemperature) * FilmCoefficientB;
    }

    #endregion

    #region 埋設配管関連の処理

    /// <summary>Embeds a buried radiant pipe at the specified node.</summary>
    /// <param name="node">
    /// Node index where the pipe is embedded. Valid range is [0, NodeCount-1];
    /// 0 is the F-side surface node and <c>NodeCount-1</c> is the B-side surface
    /// node. Interior nodes correspond to boundaries between consecutive layers.
    /// </param>
    /// <param name="pitch">Spacing between parallel pipe runs [m].</param>
    /// <param name="length">Total length of pipe installed [m].</param>
    /// <param name="branchCount">Number of parallel branches.</param>
    /// <param name="iDiameter">Pipe inner diameter [m].</param>
    /// <param name="oDiameter">Pipe outer diameter [m].</param>
    /// <param name="tubeConductivity">Thermal conductivity of the pipe material [W/(m·K)].</param>
    /// <remarks>
    /// Fin thicknesses and conductivities are derived automatically from the
    /// two adjacent layers in the stack (clamped by the pipe outer diameter).
    /// At most one pipe can occupy a given node; calling this again at the
    /// same node replaces the previous pipe. The wall's response-factor
    /// matrix is invalidated and rebuilt on the next <see cref="Update"/>
    /// call. Water flow is off by default; set it with
    /// <see cref="SetInletWater(int,double,double)"/> before expecting any
    /// heat transfer.
    /// </remarks>
    public void AddPipe(int node, double pitch, double length, int branchCount,
      double iDiameter, double oDiameter, double tubeConductivity)
    {
      UpdateUMatrix();  //resSを設定
      needToUpdateUINVMatrix = true;
      double lambdaUF, lambdaLF, thkUF, thkLF;
      if (node == 0)
      {
        lambdaUF = layers[0].ThermalConductivity;
        thkUF = Math.Min(oDiameter, 0.5 * layers[0].Thickness);
      }
      else
      {
        lambdaUF = layers[node - 1].ThermalConductivity;
        thkUF = Math.Min(oDiameter, layers[node - 1].Thickness);
      }
      if (node == layers.Length)
      {
        lambdaLF = layers[node - 1].ThermalConductivity;
        thkLF = Math.Min(oDiameter, 0.5 * layers[node - 1].Thickness);
      }
      else
      {
        lambdaLF = layers[node].ThermalConductivity;
        thkLF = Math.Min(oDiameter, layers[node].Thickness);
      }
      BuriedPipe bp = new BuriedPipe(pitch, length, branchCount, iDiameter, oDiameter,
        tubeConductivity, lambdaUF, lambdaLF, resS[node], resS[node + 1], thkUF, thkLF);
      bPipes[node] = bp;
    }

    /// <summary>Gets the buried pipe at the specified node.</summary>
    /// <param name="node">Index of the node where the pipe is buried.</param>
    /// <returns>The buried pipe at the node.</returns>
    public IReadOnlyBuriedPipe GetPipe(int node) { return bPipes[node]; }

    /// <summary>Sets the water flow rate and inlet temperature for a buried pipe.</summary>
    /// <param name="mIndex">Node index of the pipe (must match one previously passed to <see cref="AddPipe"/>).</param>
    /// <param name="flowRate">Water mass flow rate [kg/s]. Set to 0 to turn the pipe off (heat transfer becomes 0).</param>
    /// <param name="temperature">Inlet water temperature [°C].</param>
    /// <remarks>
    /// Re-computes the pipe's ε-NTU effectiveness and invalidates the
    /// inverse response-factor matrix so the next <see cref="Update"/> picks
    /// up the change. If both arguments match the current state, the call is
    /// a no-op (no matrix rebuild is triggered), so it is safe to call every
    /// step from an HVAC loop.
    /// </remarks>
    public void SetInletWater(int mIndex, double flowRate, double temperature)
    {
      BuriedPipe bp = bPipes[mIndex];
      if (bp.WaterFlowRate != flowRate || bp.InletWaterTemperature != temperature)
      {
        needToUpdateUINVMatrix = true;
        bp.SetFlowRate(flowRate);
        bp.SetWaterTemperature(temperature);
      }
    }

    /// <summary>Gets the heat transfer rate from the buried pipe at the specified node [W].</summary>
    /// <param name="mIndex">Node index.</param>
    /// <returns>
    /// Heat released from the water into the surrounding wall mass [W].
    /// Positive = the pipe is heating the slab (water warmer than the node);
    /// negative = the pipe is cooling it. Returns 0 when the flow rate is 0.
    /// </returns>
    /// <remarks>
    /// Derived from the current node temperatures obtained in the last
    /// <see cref="Update"/> call, so the value reflects the most recent
    /// solver step.
    /// </remarks>
    public double GetHeatTransferFromPipe(int mIndex)
    {
      BuriedPipe bp = bPipes[mIndex];
      if (bp.WaterFlowRate == 0) return 0;
      double tm1, tp1;
      if (mIndex == 0) tm1 = SolAirTemperatureF;
      else tm1 = tempAndHumid[mIndex - 1];
      if (mIndex == layers.Length + 1) tp1 = SolAirTemperatureB;
      else tp1 = tempAndHumid[mIndex + 1];
      double bf = uP[mIndex] * bp.InletWaterTemperature + uPF[mIndex] * tm1
        - uPM[mIndex] * tempAndHumid[mIndex] + uPB[mIndex] * tp1;
      return bf * capS[mIndex] * Area / timeStep;
    }

    /// <summary>Gets the outlet water temperature of the buried pipe at the specified node [°C].</summary>
    /// <param name="mIndex">Node index.</param>
    /// <returns>
    /// Outlet water temperature [°C], computed from the inlet temperature
    /// and the heat rejected to (or absorbed from) the wall mass. When the
    /// flow rate is 0 the inlet temperature is returned unchanged.
    /// </returns>
    public double GetOutletWaterTemperature(int mIndex)
    {
      BuriedPipe bp = bPipes[mIndex];
      if (bp.WaterFlowRate == 0) return bp.InletWaterTemperature; //2017.12.15 E.Togashi
      double hs = GetHeatTransferFromPipe(mIndex);
      return bp.InletWaterTemperature - hs / (PhysicsConstants.NominalWaterIsobaricSpecificHeat * bp.WaterFlowRate);
    }

    #endregion

    #region privateメソッド

    /// <summary>Updates the coefficient matrix from the current layer properties and boundary conditions.</summary>
    private void UpdateUMatrix()
    {
      if (!needToUpdateUMatrix) return;
      needToUpdateUINVMatrix = true;

      int mNum = layers.Length + 1; //質点数
      //熱容量,熱抵抗,水分容量,透湿抵抗の配列を作成
      for (int i = 0; i < mNum; i++)
      {
        capS[i] = 0;
        if (i != 0) capS[i] += layers[i - 1].HeatCapacity_B;
        if (i != mNum - 1) capS[i] += layers[i].HeatCapacity_F;
        if (i != 0) resS[i] = 1 / layers[i - 1].HeatConductance;
        if (ComputeMoistureTransfer)
        {
          capL[i] = cKappa[i] = cNu[i] = 0;
          if (i != 0)
          {
            capL[i] += layers[i - 1].WaterCapacity;
            cKappa[i] += layers[i - 1].KappaC;
            cNu[i] += layers[i - 1].NuC;
          }
          if (i != mNum - 1)
          {
            capL[i] += layers[i].WaterCapacity;
            cKappa[i] += layers[i].KappaC;
            cNu[i] += layers[i].NuC;
          }
          if (i != 0) resL[i] = 1 / layers[i - 1].MoistureConductivity;
        }
      }
      resS[0] = 1 / FilmCoefficientF;
      resS[resS.Length - 1] = 1 / FilmCoefficientB;
      if (ComputeMoistureTransfer)
      {
        resL[0] = PhysicsConstants.NominalMoistAirIsobaricSpecificHeat / cCoefF;
        resL[resL.Length - 1] = PhysicsConstants.NominalMoistAirIsobaricSpecificHeat / cCoefB;
      }

      //係数行列[U]を作成
      uMatrix.Initialize(0);
      if (ComputeMoistureTransfer)
      {
        //熱水分同時移動
        for (int i = 0; i < mNum; i++)
        {
          if (capS[i] == 0 && capL[i] == 0)
            throw new Exceptions.PopoloArgumentException("Vacuum wall layer is not supported.", "layers");
          double cS = capS[i];
          double cL = capL[i];
          double dtS = timeStep;
          double dtL = timeStep;
          if (cS == 0 && cNu[i] == 0) cS = dtS = 1;
          if (cL == 0 && cKappa[i] == 0) cL = dtL = 1;
          double cSL = cS * (cL + cKappa[i]) + VAPORIZATION_LATENT_HEAT * cNu[i] * cL;
          double cSLS = dtS / cSL;
          double cSLL = dtL / cSL;
          uSF2[i] = cSLS * (cL + cKappa[i]) / resS[i];
          uSB2[i] = cSLS * (cL + cKappa[i]) / resS[i + 1];
          uLF2[i] = cSLS * VAPORIZATION_LATENT_HEAT * cKappa[i] / resL[i];
          uLB2[i] = cSLS * VAPORIZATION_LATENT_HEAT * cKappa[i] / resL[i + 1];
          uSF3[i] = cSLL * cNu[i] / resS[i];
          uSB3[i] = cSLL * cNu[i] / resS[i + 1];
          uLF3[i] = cSLL * (cS + VAPORIZATION_LATENT_HEAT * cNu[i]) / resL[i];
          uLB3[i] = cSLL * (cS + VAPORIZATION_LATENT_HEAT * cNu[i]) / resL[i + 1];

          if (i != 0)
          {
            uMatrix[i, i - 1] = -uSF2[i];
            uMatrix[i, i - 1 + mNum] = -uLF2[i];
            uMatrix[i + mNum, i - 1] = -uSF3[i];
            uMatrix[i + mNum, i - 1 + mNum] = -uLF3[i];
          }
          if (i != mNum - 1)
          {
            uMatrix[i, i + 1] = -uSB2[i];
            uMatrix[i, i + 1 + mNum] = -uLB2[i];
            uMatrix[i + mNum, i + 1] = -uSB3[i];
            uMatrix[i + mNum, i + 1 + mNum] = -uLB3[i];
          }
          if (capS[i] == 0 && cNu[i] == 0) uMatrix[i, i] = uSF2[i] + uSB2[i];
          else uMatrix[i, i] = 1d + uSF2[i] + uSB2[i];
          if (capL[i] == 0 && cKappa[i] == 0) uMatrix[i + mNum, i + mNum] = uLF3[i] + uLB3[i];
          else uMatrix[i + mNum, i + mNum] = 1d + uLF3[i] + uLB3[i];
          uMatrix[i, i + mNum] = uLF2[i] + uLB2[i];
          uMatrix[i + mNum, i] = uSF3[i] + uSB3[i];
        }
      }
      else
      {
        //顕熱流のみ
        for (int i = 0; i < mNum; i++)
        {
          if (capS[i] == 0)
          {
            uSF[i] = 1 / resS[i];
            uSB[i] = 1 / resS[i + 1];
          }
          else
          {
            uSF[i] = timeStep / (capS[i] * resS[i]);
            uSB[i] = timeStep / (capS[i] * resS[i + 1]);
          }
          if (i != 0) uMatrix[i, i - 1] = -uSF[i];
          if (i != mNum - 1) uMatrix[i, i + 1] = -uSB[i];
          if (capS[i] == 0) uMatrix[i, i] = uSF[i] + uSB[i];
          else uMatrix[i, i] = 1d + uSF[i] + uSB[i];
        }
      }
      needToUpdateUMatrix = false;  //BugFix 2017.09.10
    }

    /// <summary>Updates the inverse matrix used in the response factor calculation.</summary>
    private void UpdateInverseMatrix()
    {
      //係数行列（配管の影響を除く）を更新
      UpdateUMatrix();

      //逆行列を更新
      if (needToUpdateUINVMatrix)
      {
        needToUpdateUINVMatrix = false;

        //埋設配管の影響を行列に反映
        umWithTubeEffect.Initialize(0);
        for (int i = 0; i < uMatrix.Rows; i++)
          for (int j = 0; j < uMatrix.Columns; j++)
            umWithTubeEffect[i, j] = uMatrix[i, j];
        foreach (int key in bPipes.Keys)
        {
          BuriedPipe bp = bPipes[key];
          double cS = capS[key];
          if (ComputeMoistureTransfer)
          {
            double cSL = capS[key] * (capL[key] + cKappa[key]) + VAPORIZATION_LATENT_HEAT * cNu[key] * capL[key];
            cS = cSL / (capL[key] + cKappa[key]);
          }
          uP[key] = PhysicsConstants.NominalWaterIsobaricSpecificHeat * bp.WaterFlowRate * bp.Effectiveness * TimeStep / (cS * Area);
          double bf = uP[key] / (resS[key + 1] * bp.UpperFinEfficiency + resS[key] * bp.LowerFinEfficiency);
          uPF[key] = bf * resS[key + 1] * (1 - bp.UpperFinEfficiency);
          uPM[key] = bf * (resS[key] + resS[key + 1]);
          uPB[key] = bf * resS[key] * (1 - bp.LowerFinEfficiency);

          if (key != 0) umWithTubeEffect[key, key - 1] -= uPF[key];
          if (key != layers.Length) umWithTubeEffect[key, key + 1] -= uPB[key];
          umWithTubeEffect[key, key] += uPM[key];
        }
        //逆行列を計算
        LinearAlgebraOperations.GetInverse(umWithTubeEffect, uxMatrix);

        //係数FFとBFを更新
        if (ComputeMoistureTransfer)
        {
          IMatrix ux = uxMatrix;
          int n0 = layers.Length;
          int n1 = n0 + 1;
          int n2 = n0 * 2 + 1;
          FFS2_F = ux[0, 0] * (uSF2[0] + uPF[0]) + ux[0, n1] * uSF3[0];
          FFS2_B = ux[n0, 0] * (uSF2[0] + uPF[0]) + ux[n0, n1] * uSF3[0];
          FFS3_F = ux[n1, 0] * (uSF2[0] + uPF[0]) + ux[n1, n1] * uSF3[0];
          FFS3_B = ux[n2, 0] * (uSF2[0] + uPF[0]) + ux[n2, n1] * uSF3[0];
          BFS2_F = ux[0, n0] * (uSB2[n0] + uPB[n0]) + ux[0, n2] * uSB3[n0];
          BFS2_B = ux[n0, n0] * (uSB2[n0] + uPB[n0]) + ux[n0, n2] * uSB3[n0];
          BFS3_F = ux[n1, n0] * (uSB2[n0] + uPB[n0]) + ux[n1, n2] * uSB3[n0];
          BFS3_B = ux[n2, n0] * (uSB2[n0] + uPB[n0]) + ux[n2, n2] * uSB3[n0];
          FFL2_F = ux[0, 0] * uLF2[0] + ux[0, n1] * uLF3[0];
          FFL2_B = ux[n0, 0] * uLF2[0] + ux[n0, n1] * uLF3[0];
          FFL3_F = ux[n1, 0] * uLF2[0] + ux[n1, n1] * uLF3[0];
          FFL3_B = ux[n2, 0] * uLF2[0] + ux[n2, n1] * uLF3[0];
          BFL2_F = ux[0, n0] * uLB2[n0] + ux[0, n2] * uLB3[n0];
          BFL2_B = ux[n0, n0] * uLB2[n0] + ux[n0, n2] * uLB3[n0];
          BFL3_F = ux[n1, n0] * uLB2[n0] + ux[n1, n2] * uLB3[n0];
          BFL3_B = ux[n2, n0] * uLB2[n0] + ux[n2, n2] * uLB3[n0];
        }
        else
        {
          int num = uxMatrix.Rows - 1;
          FFS2_F = uxMatrix[0, 0] * (uSF[0] + uPF[0]);
          FFS2_B = uxMatrix[num, 0] * (uSF[0] + uPF[0]);
          BFS2_F = uxMatrix[0, num] * (uSB[num] + uPB[num]);
          BFS2_B = uxMatrix[num, num] * (uSB[num] + uPB[num]);
        }

        //逆行列変更フラグON
        invMatrixUpdated = true;
      }
    }

    /// <summary>Post-deserialization callback (legacy; no longer used).</summary>
    /// <param name="sender"></param>
    public void OnDeserialization(object sender)
    {
      needToUpdateUMatrix = true; //逆行列再計算フラグをONに

      int mNum = this.layers.Length + 1; //質点数
      capS = new double[mNum];
      resS = new double[mNum + 1];
      int ssNum = mNum; //未知変数の数
      if (ComputeMoistureTransfer)
      {
        ssNum *= 2; //絶対湿度も未知のため倍
        uSF2 = new double[mNum];
        uSB2 = new double[mNum];
        uSF3 = new double[mNum];
        uSB3 = new double[mNum];
        uLF2 = new double[mNum];
        uLB2 = new double[mNum];
        uLF3 = new double[mNum];
        uLB3 = new double[mNum];
        cNu = new double[mNum];
        cKappa = new double[mNum];
        capL = new double[mNum];
        resL = new double[mNum + 1];
      }
      else
      {
        uSF = new double[ssNum];
        uSB = new double[ssNum];
      }
      //tempAndHumid = new Vector(ssNum);
      uMatrix = new Matrix(ssNum, ssNum);
      umWithTubeEffect = new Matrix(ssNum, ssNum);
      uxMatrix = new Matrix(ssNum, ssNum);
      uP = new double[mNum];
      uPF = new double[mNum];
      uPM = new double[mNum];
      uPB = new double[mNum];
    }

    #endregion

  }
}