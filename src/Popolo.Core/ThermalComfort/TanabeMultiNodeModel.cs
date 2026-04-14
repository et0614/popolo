/* TanabeMultiNodeModel.cs
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
using System.Collections.Generic;

using Popolo.Core.Physics;
using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.ThermalComfort
{
  /// <summary>Implements the Tanabe 65-node multi-segment human thermoregulatory model.</summary>
  /// <remarks>
  /// Based on: Tanabe, S. et al. (2002). Evaluation of thermal comfort using combined
  /// multi-node thermoregulation (65MN) and radiation models and computational fluid dynamics.
  /// Energy and Buildings, 34(6), 637–646.
  /// </remarks>
  public partial class TanabeMultiNodeModel : IReadOnlyTanabeMultiNodeModel
  {

    #region 定数宣言

    /// <summary>Volumetric specific heat of blood [J/(mL·K)].</summary>
    public const double BLD_SPECIFICHEAT = 3.842;

    /// <summary>Conversion factor from metabolic rate [met] to heat flux [W/m²].</summary>
    private const double CONVERT_MET_TO_W = 58.2;

    /// <summary>Body surface area of the reference standard body [m²].</summary>
    private const double STANDARD_SURFACE_AREA = 1.87;

    /// <summary>Blood flow rate of the reference standard body [mL/s].</summary>
    private const double STANDARD_BLOOD_FLOW = 290.004 / 3.6;

    /// <summary>Weight of the reference standard body [kg].</summary>
    private const double STANDARD_WEIGHT = 74.43;

    /// <summary>Standard atmospheric pressure [kPa].</summary>
    private const double ATMOSPHERIC_PRESSURE = 101.325;

    /// <summary>Isobaric specific heat of bone [J/(kg·K)].</summary>
    private const double SPECIFIC_HEAT_BORN = 2092;

    /// <summary>Isobaric specific heat of fat tissue [J/(kg·K)].</summary>
    private const double SPECIFIC_HEAT_FAT = 2510;

    /// <summary>Isobaric specific heat of other body tissues [J/(kg·K)].</summary>
    private const double SPECIFIC_HEAT_ELSE = 3766;

    /// <summary>Stefan–Boltzmann constant [W/(m²·K⁴)].</summary>
    private const double BLACK_CONSTANT = 5.67e-8;

    /// <summary>Offset for converting Celsius to Kelvin [K].</summary>
    private const double CONVERT_C_TO_K = 273.15;

    /// <summary>Clothing moisture permeability index [K/kPa].</summary>
    private const double I_CLS = 0.45;

    /// <summary>True if this segment is a distal limb extremity (hand or foot).</summary>
    private const Node TERMINAL_NODE =
      Node.LeftHand | Node.RightHand | Node.LeftFoot | Node.RightFoot;

    /// <summary>True if this segment is a limb (arm or leg).</summary>
    private const Node LIMBS =
      TERMINAL_NODE | Node.LeftShoulder | Node.RightShoulder | Node.LeftArm | Node.RightArm |
      Node.LeftThigh | Node.RightThigh | Node.LeftLeg | Node.RightLeg;

    #endregion

    #region 列挙型定義

    /// <summary>Body segment node identifiers for the Tanabe 65-node model.</summary>
    [Flags]
    public enum Node
    {
      /// <summary>Head.</summary>
      Head = 1,
      /// <summary>Neck.</summary>
      Neck = 2,
      /// <summary>Chest.</summary>
      Chest = 4,
      /// <summary>Back.</summary>
      Back = 8,
      /// <summary>Pelvis.</summary>
      Pelvis = 16,
      /// <summary>Left shoulder.</summary>
      LeftShoulder = 32,
      /// <summary>Left upper arm.</summary>
      LeftArm = 64,
      /// <summary>Left hand.</summary>
      LeftHand = 128,
      /// <summary>Right shoulder.</summary>
      RightShoulder = 256,
      /// <summary>Right upper arm.</summary>
      RightArm = 512,
      /// <summary>Right hand.</summary>
      RightHand = 1024,
      /// <summary>Left lower leg.</summary>
      LeftThigh = 2048,
      /// <summary>Left foot.</summary>
      LeftLeg = 4096,
      /// <summary>Left thigh (upper leg).</summary>
      LeftFoot = 8192,
      /// <summary>Right lower leg.</summary>
      RightThigh = 16384,
      /// <summary>Right foot.</summary>
      RightLeg = 32768,
      /// <summary>Right thigh (upper leg).</summary>
      RightFoot = 65536,
    }

    /// <summary>Tissue layer types within each body segment.</summary>
    public enum Layer
    {
      /// <summary>Core (inner) layer.</summary>
      Core = 0,
      /// <summary>Muscle layer.</summary>
      Muscle = 1,
      /// <summary>Fat layer.</summary>
      Fat = 2,
      /// <summary>Skin layer.</summary>
      Skin = 4,
      /// <summary>Artery (central blood pool).</summary>
      Artery = 8,
      /// <summary>Superficial vein.</summary>
      SuperficialVein = 16,
      /// <summary>Deep artery.</summary>
      DeepVein = 32,
      /// <summary>AVA</summary>
      AVA = 64
    }

    #endregion

    #region インスタンス変数

    /// <summary>Body segment node identifiers for the Tanabe 65-node model.</summary>
    private Dictionary<Node, bodyPart> parts = new Dictionary<Node, bodyPart>();

    /// <summary>Muscle weight ratio for this body segment.</summary>
    private Dictionary<Node, double> rMuscle = new Dictionary<Node, double>();

    /// <summary>Temperature state vector.</summary>
    private IVector tVector = new Vector(115);

    /// <summary>Mean setpoint temperature of the core layer [°C].</summary>
    private double averageCoreSetPoint;

    /// <summary>Mean setpoint temperature of the skin layer [°C].</summary>
    private double averageSkinSetPoint;

    #endregion

    #region プロパティ

    /// <summary>Gets the weight [kg].</summary>
    public double Weight { get; private set; }

    /// <summary>Gets the height [m].</summary>
    public double Height { get; private set; }

    /// <summary>Gets the age [years].</summary>
    public double Age { get; private set; }

    /// <summary>Gets a value indicating whether the occupant is male.</summary>
    public bool IsMale { get; private set; }

    /// <summary>Gets or sets a value indicating whether the occupant is standing.</summary>
    public bool IsStanding { get; private set; }

    /// <summary>Gets the body fat percentage [%].</summary>
    public double FatPercentage { get; private set; }

    /// <summary>Gets the total body surface area (Du Bois) [m²].</summary>
    public double SurfaceArea { get; private set; }

    /// <summary>Gets the central blood pool temperature [°C].</summary>
    public double CentralBloodTemperature { get; private set; }

    /// <summary>Gets the respiratory heat loss [W].</summary>
    public double HeatLossByBreathing { get; private set; }

    /// <summary>Gets the whole-body basal blood flow rate [mL/s].</summary>
    public double BasalBloodFlow { get; private set; }

    /// <summary>Gets the total whole-body blood flow rate [mL/s].</summary>
    public double BloodFlow { get; private set; }

    /// <summary>Gets the whole-body basal metabolic rate [W].</summary>
    public double BasalMetabolicRate { get; private set; }

    /// <summary>Gets the total whole-body metabolic rate [W].</summary>
    public double MetabolicRate { get; private set; }

    /// <summary>Gets the calculation time step [s].</summary>
    public double TimeStep { get; private set; }

    #endregion

    #region コンストラクタ・初期化処理

    /// <summary>Initializes a new instance of the Tanabe multi-node model.</summary>
    /// <remarks>When no arguments are provided, the reference standard body dimensions are used.</remarks>
    public TanabeMultiNodeModel() : this(74.43, 1.72, 25, true, 0.15, true) { }

    /// <summary>Initializes a new instance of the Tanabe multi-node model.</summary>
    /// <param name="weight">Weight [kg].</param>
    /// <param name="height">Height [m].</param>
    /// <param name="age">Age [years].</param>
    /// <param name="isMale">True for male; false for female.</param>
    /// <param name="fatPercentage">Body fat percentage [%].</param>
    /// <param name="isStanding">True for standing posture; false for seated.</param>
    public TanabeMultiNodeModel
      (double weight, double height, double age, bool isMale, double fatPercentage, bool isStanding)
    {
      //情報保存
      Weight = weight;
      Height = height;
      Age = age;
      IsMale = isMale;
      IsStanding = isStanding;
      FatPercentage = fatPercentage;

      //体表面積[m2]を計算
      SurfaceArea = 0.202 * Math.Pow(Weight, 0.425) * Math.Pow(Height, 0.725);
      //心係数[(mL/s)/m2]を計算
      double ci = 115 + Age * (-2.4 + Age * (0.0167 + Age * (3.56e-4 - 4.29e-6 * Age)));
      //基礎血流[mL/s]を計算
      BasalBloodFlow = ci * SurfaceArea;

      //基礎代謝[W]を計算
      BasalMetabolicRate = (0.1238 + 2.34 * height + 0.0481 * weight - 0.0138 * age);
      if (isMale) BasalMetabolicRate = (BasalMetabolicRate - 0.5473) / 0.0864;
      else BasalMetabolicRate = (BasalMetabolicRate - 0.5473 * 2) / 0.0864;

      //部位を作成
      double muscleSum = 0;
      Node[] nds = (Node[])Enum.GetValues(typeof(Node));
      foreach (Node nd in nds)
      {
        bodyPart bp = new bodyPart
          (this, nd, Weight, Height, SurfaceArea, fatPercentage, BasalMetabolicRate, BasalBloodFlow);
        parts.Add(nd, bp);
        muscleSum += bp.muscleWeight;
      }
      //筋肉の重量比を計算
      foreach (Node nd in parts.Keys) rMuscle[nd] = parts[nd].muscleWeight / muscleSum;

      //部位を接続
      parts[Node.Neck].connect(parts[Node.Head]);
      parts[Node.Pelvis].connect(parts[Node.LeftThigh]);
      parts[Node.Pelvis].connect(parts[Node.RightThigh]);
      parts[Node.LeftThigh].connect(parts[Node.LeftLeg]);
      parts[Node.RightThigh].connect(parts[Node.RightLeg]);
      parts[Node.LeftLeg].connect(parts[Node.LeftFoot]);
      parts[Node.RightLeg].connect(parts[Node.RightFoot]);
      parts[Node.LeftShoulder].connect(parts[Node.LeftArm]);
      parts[Node.RightShoulder].connect(parts[Node.RightArm]);
      parts[Node.LeftArm].connect(parts[Node.LeftHand]);
      parts[Node.RightArm].connect(parts[Node.RightHand]);

      //体温を初期化
      InitializeTemperature(36);

      //セットポイント初期化
      InitializeSetPoint();
    }

    /// <summary>Initializes thermoregulatory setpoint temperatures.</summary>
    private void InitializeSetPoint()
    {
      //制御をOFF
      foreach (Node nd in parts.Keys)
      {
        parts[nd].initializing = true;
        parts[nd].setClothingIndex(0);
      }

      //PMV=0となる境界条件を設定
      UpdateBoundary(0, 28.8, 28.8, 50);
      SetMetabolicRate(1);

      //定常状態まで計算（24時間）
      for (int i = 0; i < 24; i++) Update(3600);

      //セットポイント設定
      foreach (Node nd in parts.Keys)
      {
        parts[nd].initializing = false;
        parts[nd].setPoint_Skin = parts[nd].temperatures[Layer.Skin];
        parts[nd].setPoint_Core = parts[nd].temperatures[Layer.Core];
      }

      //平均セットポイントを作成
      //体中心の核
      Layer core = Layer.Core;
      double capSum = parts[Node.Chest].heatCapacity[core]
        + parts[Node.Pelvis].heatCapacity[core] + parts[Node.Back].heatCapacity[core];
      averageCoreSetPoint =
        (parts[Node.Chest].setPoint_Core * parts[Node.Chest].heatCapacity[core]
        + parts[Node.Pelvis].setPoint_Core * parts[Node.Pelvis].heatCapacity[core]
        + parts[Node.Back].setPoint_Core * parts[Node.Back].heatCapacity[core]) / capSum;

      //全身の皮膚
      averageSkinSetPoint = 0;
      foreach (Node bp in parts.Keys) averageSkinSetPoint += parts[bp].setPoint_Skin * parts[bp].surfaceArea;
      averageSkinSetPoint /= SurfaceArea;
    }

    /// <summary>Initializes body segment temperatures to the specified value.</summary>
    /// <param name="temperature">Initial temperature [°C].</param>
    public void InitializeTemperature(double temperature)
    {
      //行列に設定
      for (int i = 0; i < tVector.Length; i++) tVector[i] = temperature;
      //各部位への設定処理
      CentralBloodTemperature = temperature;
      foreach (Node nd in parts.Keys) parts[nd].updateTemperature(tVector);
    }

    #endregion

    #region staticメソッド

    /// <summary>Gets the upstream body segment node.</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Upstream body segment node.</returns>
    public static Node GetUpstreamNode(Node node)
    {
      switch (node)
      {
        case Node.Head:
          return Node.Neck;
        case Node.LeftHand:
          return Node.LeftArm;
        case Node.LeftArm:
          return Node.LeftShoulder;
        case Node.RightHand:
          return Node.RightArm;
        case Node.RightArm:
          return Node.RightShoulder;
        case Node.LeftFoot:
          return Node.LeftLeg;
        case Node.LeftLeg:
          return Node.LeftThigh;
        case Node.LeftThigh:
          return Node.Pelvis;
        case Node.RightFoot:
          return Node.RightLeg;
        case Node.RightLeg:
          return Node.RightThigh;
        case Node.RightThigh:
          return Node.Pelvis;
        default:
          return 0;
      }
    }

    /// <summary>Gets the downstream body segment node.</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Downstream body segment node.</returns>
    public static Node[] GetDownStreamNode(Node node)
    {
      switch (node)
      {
        case Node.Neck:
          return new Node[] { Node.Head };
        case Node.LeftArm:
          return new Node[] { Node.LeftHand };
        case Node.LeftShoulder:
          return new Node[] { Node.LeftArm };
        case Node.RightArm:
          return new Node[] { Node.RightHand };
        case Node.RightShoulder:
          return new Node[] { Node.RightArm };
        case Node.LeftLeg:
          return new Node[] { Node.LeftFoot };
        case Node.LeftThigh:
          return new Node[] { Node.LeftLeg };
        case Node.RightLeg:
          return new Node[] { Node.RightFoot };
        case Node.RightThigh:
          return new Node[] { Node.RightLeg };
        case Node.Pelvis:
          return new Node[] { Node.LeftThigh, Node.RightThigh };
        default:
          return new Node[] { };
      }
    }

    #endregion

    #region 更新処理

    /// <summary>Updates the thermoregulatory state for one time step.</summary>
    /// <param name="timeStep">Time step [s].</param>
    public void Update(double timeStep)
    {
      this.TimeStep = timeStep;
      IVector zVector = new Vector(115);

      //制御を更新
      UpdateControl();

      //計算用疎行列を用意
      SparseMatrix bMatrix = new SparseMatrix(115, 115);

      double mr = 0;
      foreach (Node nd in parts.Keys)
      {
        //行列に各部位の項を設定
        parts[nd].makeMatrix(bMatrix, zVector);
        //代謝量を積算
        mr += parts[nd].shiveringLoad + parts[nd].externalWork
          + parts[nd].basalMetabolicRate[Layer.Core] + parts[nd].basalMetabolicRate[Layer.Muscle]
          + parts[nd].basalMetabolicRate[Layer.Fat] + parts[nd].basalMetabolicRate[Layer.Skin];
      }

      //胸部の呼吸の項を追加
      bodyPart head = parts[Node.Head];
      HeatLossByBreathing = mr * (0.0014 * (34 - head.drybulbTemperature)
        + 0.017251 * (5.8662 - head.waterVaporPressure));
      zVector[13] -= HeatLossByBreathing;

      //中央血液溜まりの項を追加
      bMatrix[0, 0] = parts[Node.Chest].centralBloodHeatCapacity / timeStep;
      bodyPart[] bps = new bodyPart[] { parts[Node.Neck], parts[Node.LeftShoulder],
        parts[Node.RightShoulder], parts[Node.Pelvis], parts[Node.Chest], parts[Node.Back]};
      for (int i = 0; i < bps.Length; i++)
      {
        double dvf = BLD_SPECIFICHEAT * bps[i].bloodFlow[Layer.DeepVein];
        double svf = BLD_SPECIFICHEAT * bps[i].bloodFlow[Layer.SuperficialVein];
        bMatrix[0, 0] += (dvf + svf);
        bMatrix[0, bodyPart.mOffset[bps[i].node] + 5] = -dvf;
        bMatrix[0, bodyPart.mOffset[bps[i].node] + 6] = -svf;
      }
      zVector[0] = parts[Node.Chest].centralBloodHeatCapacity / timeStep * CentralBloodTemperature;

      //連立代数方程式を解く
      bMatrix.SolveLinearEquation(zVector, ref tVector);

      //設定
      foreach (Node nd in parts.Keys) parts[nd].updateTemperature(tVector);
      CentralBloodTemperature = tVector[0];
    }

    /// <summary>Computes thermoregulatory control signals from temperature deviations.</summary>
    private void UpdateControl()
    {
      //全身の温冷感信号の計算
      double cldSignal, wrmSignal;
      cldSignal = wrmSignal = 0;
      foreach (Node bp in parts.Keys)
      {
        double sig = parts[bp].getSignal();
        if (0 < sig) wrmSignal += sig;
        else cldSignal += sig;
      }
      double signal = wrmSignal + cldSignal;

      //頭部の核の温冷感信号
      Layer core = Layer.Core;
      double sHead = parts[Node.Head].temperatures[core] - parts[Node.Head].setPoint_Core;

      //発汗・ふるえ・血管収縮・血管拡張の信号を計算
      double sfRate = SurfaceArea / STANDARD_SURFACE_AREA;
      double sweatSignal = Math.Max(0, (371.2 * sHead + 33.64 * signal)) * sfRate;
      double shiveringSignal = (-24.36 * Math.Max(0, -sHead) * cldSignal) * sfRate;
      double vasoconstrictionSignal = Math.Max(0, -10.8 * sHead - 10.8 * signal);
      double vasodilatationSignal = Math.Max(0, 32.5 * sHead + 2.08 * signal);
      vasodilatationSignal *= BasalBloodFlow / STANDARD_BLOOD_FLOW;

      //AVA血流開度信号を計算
      //体中心（腰・胸・背中）の平均温度を計算
      double capSum = parts[Node.Chest].heatCapacity[core]
          + parts[Node.Pelvis].heatCapacity[core] + parts[Node.Back].heatCapacity[core];
      double aveCore = 0;
      aveCore += parts[Node.Chest].temperatures[core] * parts[Node.Chest].heatCapacity[core];
      aveCore += parts[Node.Pelvis].temperatures[core] * parts[Node.Pelvis].heatCapacity[core];
      aveCore += parts[Node.Back].temperatures[core] * parts[Node.Back].heatCapacity[core];
      aveCore /= capSum;
      //全身の平均皮膚温を計算
      double atSkin = GetAverageSkinTemperature();
      //AVA開度の計算
      double ovaHand = 0.265 * (atSkin - (averageSkinSetPoint - 0.43))
          + 0.953 * (aveCore - (averageCoreSetPoint - 0.1905)) + 0.9126;
      double ovaFoot = 0.265 * (atSkin - (averageSkinSetPoint - 0.97))
          + 0.953 * (aveCore - (averageCoreSetPoint + 0.0095)) + 0.9126;

      //皮膚血管運動・発汗・ふるえ熱生産・AVA血流を計算
      foreach (Node bp in parts.Keys)
      {
        if ((bp == Node.LeftHand) || (bp == Node.RightHand))
          parts[bp].updateControl
            (signal, sweatSignal, shiveringSignal, vasodilatationSignal, vasoconstrictionSignal, ovaHand);
        else parts[bp].updateControl
            (signal, sweatSignal, shiveringSignal, vasodilatationSignal, vasoconstrictionSignal, ovaFoot);
      }

      //血流更新処理//中央血液溜まり直結部以外はメソッド内で再帰的に呼び出し
      BloodFlow = 0;
      Node[] nds =
        new Node[] { Node.Neck, Node.Chest, Node.Back, Node.Pelvis, Node.LeftShoulder, Node.RightShoulder };
      for (int i = 0; i < nds.Length; i++)
      {
        parts[nds[i]].updateBloodFlow();
        BloodFlow += parts[nds[i]].bloodFlow[Layer.DeepVein] + parts[nds[i]].bloodFlow[Layer.SuperficialVein];
      }

      //全身代謝量[W]を更新
      MetabolicRate = BasalMetabolicRate;
      foreach (Node nd in parts.Keys)
      {
        bodyPart part = parts[nd];
        MetabolicRate += part.externalWork + part.shiveringLoad;
      }
    }

    #endregion

    #region 境界条件設定処理

    /// <summary>Sets the metabolic rate [met].</summary>
    /// <param name="met">Metabolic rate [met].</param>
    public void SetMetabolicRate(double met)
    {
      met = Math.Max(0, CONVERT_MET_TO_W * met * SurfaceArea - BasalMetabolicRate);
      foreach (Node bp in parts.Keys) parts[bp].externalWork = rMuscle[bp] * met;
    }

    /// <summary>Updates thermal boundary conditions for all body segments.</summary>
    /// <param name="velocity">Air velocity [m/s].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="drybulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    public void UpdateBoundary
      (double velocity, double meanRadiantTemperature, double drybulbTemperature, double relativeHumidity)
    {
      double hr = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
        (drybulbTemperature, relativeHumidity, ATMOSPHERIC_PRESSURE);
      double wvp = MoistAir.GetWaterVaporPartialPressureFromHumidityRatio(hr, ATMOSPHERIC_PRESSURE);
      foreach (Node bp in parts.Keys)
        parts[bp].updateBoundary(velocity, meanRadiantTemperature, drybulbTemperature, wvp);
    }

    /// <summary>Updates thermal boundary conditions for all body segments.</summary>
    /// <param name="node">Body segment node to update.</param>
    /// <param name="velocity">Air velocity [m/s].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="drybulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    public void UpdateBoundary
      (Node node, double velocity, double meanRadiantTemperature, double drybulbTemperature, double relativeHumidity)
    {
      double hr = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
        (drybulbTemperature, relativeHumidity, ATMOSPHERIC_PRESSURE);
      double wvp = MoistAir.GetWaterVaporPartialPressureFromHumidityRatio(hr, ATMOSPHERIC_PRESSURE);
      parts[node].updateBoundary(velocity, meanRadiantTemperature, drybulbTemperature, wvp);
    }

    /// <summary>Sets the clothing insulation [clo].</summary>
    /// <param name="node">Body segment node.</param>
    /// <param name="clo">Clothing insulation [clo].</param>
    public void SetClothingIndex(Node node, double clo)
    { parts[node].setClothingIndex(clo); }

    /// <summary>Sets up thermal contact between this body segment and an object.</summary>
    /// <param name="node">Body segment node.</param>
    /// <param name="temperature">Object surface temperature [°C].</param>
    /// <param name="heatConductance">Thermal conductance to the object [W/K].</param>
    /// <param name="contactPortionRate">Fraction of skin surface in contact [-].</param>
    public void Contact(Node node, double temperature, double heatConductance, double contactPortionRate)
    { parts[node].contact(temperature, heatConductance, contactPortionRate); }

    #endregion

    #region 情報取得処理

    /// <summary>Gets the air velocity [m/s].</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Air velocity [m/s].</returns>
    public double GetVelocity(Node node) { return parts[node].velocity; }

    /// <summary>Gets the mean radiant temperature [°C].</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Mean radiant temperature [°C].</returns>
    public double GetMeanRadiantTemperature(Node node) { return parts[node].meanRadiantTemperature; }

    /// <summary>Gets the ambient dry-bulb temperature [°C].</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Dry-bulb temperature [°C].</returns>
    public double GetDrybulbTemperature(Node node) { return parts[node].drybulbTemperature; }

    /// <summary>Gets the equivalent temperature [°C].</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Equivalent temperature [°C].</returns>
    public double GetOperatingTemperature(Node node) { return parts[node].operatingTemperature; }

    /// <summary>Gets the relative humidity [%].</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Relative humidity [%].</returns>
    public double GetRelativeHumidity(Node node)
    {
      double hr = MoistAir.GetHumidityRatioFromWaterVaporPartialPressure
        (parts[node].waterVaporPressure, ATMOSPHERIC_PRESSURE);
      return MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
        (parts[node].drybulbTemperature, hr, ATMOSPHERIC_PRESSURE);
    }

    /// <summary>Gets the clothing insulation [clo].</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Clothing insulation [clo].</returns>
    public double GetClothingIndex(Node node) { return parts[node].clothingIndex; }

    /// <summary>Gets the thermal conductance between tissue layers [W/K].</summary>
    /// <param name="node">Body segment node.</param>
    /// <param name="layer1">First tissue layer.</param>
    /// <param name="layer2">Second tissue layer.</param>
    /// <returns>Thermal conductance between tissue layers [W/K].</returns>
    public double GetHeatConductance(Node node, Layer layer1, Layer layer2)
    { return parts[node].getHeatConductance(layer1, layer2); }

    /// <summary>Gets the metabolic rate of this segment [W].</summary>
    /// <returns>Metabolic rate [W].</returns>
    public double GetMetabolicRate(Node node, Layer layer)
    {
      double bm = parts[node].basalMetabolicRate[layer];
      if (layer != Layer.Muscle) return bm;
      else return bm + parts[node].externalWork + parts[node].shiveringLoad;
    }

    /// <summary>Gets the body segment temperature [°C].</summary>
    /// <param name="node">Body segment node.</param>
    /// <param name="layer">Tissue layer.</param>
    /// <returns>Body segment temperature [°C].</returns>
    public double GetTemperature(Node node, Layer layer) { return parts[node].temperatures[layer]; }

    /// <summary>Gets the current blood flow rate [mL/s].</summary>
    /// <param name="node">Body segment node.</param>
    /// <param name="layer">Tissue layer.</param>
    /// <returns>Blood flow rate [mL/s].</returns>
    public double GetBloodFlow(Node node, Layer layer) { return parts[node].bloodFlow[layer]; }

    /// <summary>Gets the area-weighted mean skin temperature [°C].</summary>
    /// <returns>Area-weighted mean skin temperature [°C].</returns>
    public double GetAverageSkinTemperature()
    {
      double ctSum = 0;
      foreach (Node bp in parts.Keys)
      {
        bodyPart bPart = parts[bp];
        ctSum += bPart.temperatures[Layer.Skin] * bPart.surfaceArea;
      }
      return ctSum / SurfaceArea;
    }

    /// <summary>Gets the sensible heat loss from the skin surface [W].</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Sensible heat loss from the skin surface [W].</returns>
    public double GetSensibleHeatLoss(Node node) { return parts[node].getSensibleHeatLoss(); }

    /// <summary>Gets the latent heat loss from the skin surface [W].</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Latent heat loss from the skin surface [W].</returns>
    public double GetLatentHeatLoss(Node node) { return parts[node].latentHeatLoss; }

    #endregion

  }

}
