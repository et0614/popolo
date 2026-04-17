/* TanabeMultiNodeModel_bodyPart.cs
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
  public partial class TanabeMultiNodeModel : IReadOnlyTanabeMultiNodeModel
  {
    /// <summary>Body segment node identifiers for the Tanabe 65-node model.</summary>
      private class bodyPart
    {

      #region クラス変数

      /// <summary>Weight ratio 1 for body segment scaling.</summary>
      private static readonly Dictionary<Node, double> rWeight1;

      /// <summary>Weight ratio 2 for body segment scaling.</summary>
      private static readonly Dictionary<Node, double[]> rWeight2;

      /// <summary>Ratio of body surface area relative to the reference body.</summary>
      private static readonly Dictionary<Node, double> rSurface;

      /// <summary>Equivalent cylinder length of this body segment [m].</summary>
      private static readonly Dictionary<Node, double> cLength;

      /// <summary>Ratio of basal metabolic rate relative to the reference body.</summary>
      private static readonly Dictionary<Node, double[]> bMetRate;

      /// <summary>Ratio of basal blood flow relative to the reference body.</summary>
      private static readonly Dictionary<Node, double[]> bBFRate;

      /// <summary>Thermal conductance of blood vessels [W/K].</summary>
      private static readonly Dictionary<Node, double[]> hCdBLD;

      /// <summary>Matrix row/column offset for this body part.</summary>
      internal static readonly Dictionary<Node, int> mOffset;

      /// <summary>Convective heat transfer coefficient in standing posture [W/(m²·K)].</summary>
      private static readonly Dictionary<Node, double> cHTransferStand;

      /// <summary>Convective heat transfer coefficient in seated posture [W/(m²·K)].</summary>
      private static readonly Dictionary<Node, double> cHTransferSit;

      /// <summary>Weighting coefficient for the thermal sensation control signal.</summary>
      private static readonly Dictionary<Node, double> skinSignal;

      /// <summary>Weighting coefficient for the sweating control signal.</summary>
      private static readonly Dictionary<Node, double> sweatSignalR;

      /// <summary>Weighting coefficient for the shivering control signal.</summary>
      private static readonly Dictionary<Node, double> shivSignalR;

      /// <summary>Weighting coefficient for the vasoconstriction control signal.</summary>
      private static readonly Dictionary<Node, double> dilSignalR;

      /// <summary>Weighting coefficient for the vasodilation control signal.</summary>
      private static readonly Dictionary<Node, double> strSignalR;

      #endregion

      #region インスタンス変数

      /// <summary>Represents a single body segment node with multi-layer thermal properties.</summary>
      private IReadOnlyTanabeMultiNodeModel body;

      /// <summary>Thermal conductance [W/K].</summary>
      /// <remarks>
      /// Conductance indices: 0:core-muscle, 1:muscle-fat, 2:fat-skin, 3:core-vessel, 4:skin-vessel, 5:artery-vein,
      /// 6:skin-object, 7:skin-ambient(sensible), 8:skin-ambient(latent)
      /// </remarks>
      private double[] hConductance = new double[9];

      /// <summary>Maximum arterio-venous anastomosis (AVA) blood flow rate [mL/s].</summary>
      private double maxAVA = 0;

      /// <summary>Basal skin layer blood flow rate [mL/s].</summary>
      private double basalBloodFlow_Skin;

      /// <summary>Basal muscle layer blood flow rate [mL/s].</summary>
      private double basalBloodFlow_Muscle;

      #endregion

      #region プロパティ

      /// <summary>True while the model is being initialized.</summary>
      internal bool initializing { get; set; }

      /// <summary>Gets the body segment weight [kg].</summary>
      internal double weight { get; private set; }

      /// <summary>Gets the muscle weight [kg].</summary>
      internal double muscleWeight { get; private set; }

      /// <summary>Gets the body segment surface area [m²].</summary>
      internal double surfaceArea { get; private set; }

      /// <summary>Gets the body segment node identifier.</summary>
      internal Node node { get; private set; }

      /// <summary>Gets the fraction of skin surface in contact with an object [-].</summary>
      internal double contactPortionRate { get; private set; }

      /// <summary>Gets the temperature of the object in contact with the skin [°C].</summary>
      internal double materialTemperature { get; private set; }

      /// <summary>Gets the convective heat transfer coefficient [W/(m²·K)].</summary>
      internal double convectiveHeatTransferCoefficient { get; private set; }

      /// <summary>Gets the radiative heat transfer coefficient [W/(m²·K)].</summary>
      internal double radiativeHeatTransferCoefficient { get; private set; }

      /// <summary>Gets the clothing insulation [clo].</summary>
      internal double clothingIndex { get; private set; }

      /// <summary>Gets the relative air velocity [m/s].</summary>
      internal double velocity { get; private set; }

      /// <summary>Gets the clothing surface temperature [°C].</summary>
      internal double clothTemperature { get; private set; }

      /// <summary>Gets the mean radiant temperature [°C].</summary>
      internal double meanRadiantTemperature { get; private set; }

      /// <summary>Gets the ambient dry-bulb temperature [°C].</summary>
      internal double dryBulbTemperature { get; private set; }

      /// <summary>Gets the water vapor pressure [kPa].</summary>
      internal double waterVaporPressure { get; private set; }

      /// <summary>Gets the current layer temperature [°C].</summary>
      internal Dictionary<Layer, double> temperatures { get; private set; }

      /// <summary>Heat capacity [kJ/K].</summary>
      internal Dictionary<Layer, double> heatCapacity { get; private set; }

      /// <summary>Blood flow rate [mL/s].</summary>
      internal Dictionary<Layer, double> bloodFlow { get; private set; }

      /// <summary>Gets the heat production due to shivering [W].</summary>
      internal double shiveringLoad { get; private set; }

      /// <summary>Gets or sets the external mechanical work [W].</summary>
      internal double externalWork { get; set; }

      /// <summary>Gets the basal metabolic rate of this segment [W].</summary>
      internal Dictionary<Layer, double> basalMetabolicRate { get; private set; }

      /// <summary>Gets the equivalent temperature [°C].</summary>
      internal double operatingTemperature { get; private set; }

      /// <summary>Gets the heat capacity of the central blood pool [J/K].</summary>
      internal double centralBloodHeatCapacity { get; private set; }

      /// <summary>Gets or sets the skin setpoint temperature [°C].</summary>
      internal double setpoint_Skin { get; set; }

      /// <summary>Gets or sets the core setpoint temperature [°C].</summary>
      internal double setpoint_Core { get; set; }

      /// <summary>Gets the evaporative heat loss due to sweating [W].</summary>
      internal double evaporativeHeatLoss_Sweat { get; private set; }

      /// <summary>Gets the total evaporative heat loss [W].</summary>
      internal double latentHeatLoss { get; private set; }

      /// <summary>Gets the downstream (venous-side) connected body segment.</summary>
      internal List<bodyPart> downStreamParts { get; private set; }

      /// <summary>Gets the upstream (arterial-side) connected body segment.</summary>
      internal bodyPart upperStreamPart { get; private set; } = null!;

      #endregion

      #region staticコンストラクタ

      /// <summary>Static constructor: initializes body segment constants.</summary>
      static bodyPart()
      {
        //クラス変数初期化処理
        //重量比1
        rWeight1 = new Dictionary<TanabeMultiNodeModel.Node, double>();
        rWeight1.Add(Node.Head, 0.0427);
        rWeight1.Add(Node.Neck, 0.0113);
        rWeight1.Add(Node.Chest, 0.1666);
        rWeight1.Add(Node.Back, 0.1482);
        rWeight1.Add(Node.Pelvis, 0.2362);
        rWeight1.Add(Node.LeftShoulder, 0.0290);
        rWeight1.Add(Node.LeftArm, 0.0184);
        rWeight1.Add(Node.LeftHand, 0.0046);
        rWeight1.Add(Node.RightShoulder, 0.0290);
        rWeight1.Add(Node.RightArm, 0.0184);
        rWeight1.Add(Node.RightHand, 0.0046);
        rWeight1.Add(Node.LeftThigh, 0.0942);
        rWeight1.Add(Node.LeftLeg, 0.0449);
        rWeight1.Add(Node.LeftFoot, 0.0064);
        rWeight1.Add(Node.RightThigh, 0.0942);
        rWeight1.Add(Node.RightLeg, 0.0449);
        rWeight1.Add(Node.RightFoot, 0.0064);

        //表面積比
        rSurface = new Dictionary<Node, double>();
        rSurface.Add(Node.Head, 0.0559);
        rSurface.Add(Node.Neck, 0.0151);
        rSurface.Add(Node.Chest, 0.0900);
        rSurface.Add(Node.Back, 0.0821);
        rSurface.Add(Node.Pelvis, 0.1275);
        rSurface.Add(Node.LeftShoulder, 0.0515);
        rSurface.Add(Node.LeftArm, 0.0327);
        rSurface.Add(Node.LeftHand, 0.0243);
        rSurface.Add(Node.RightShoulder, 0.0515);
        rSurface.Add(Node.RightArm, 0.0327);
        rSurface.Add(Node.RightHand, 0.0243);
        rSurface.Add(Node.LeftThigh, 0.1169);
        rSurface.Add(Node.LeftLeg, 0.0566);
        rSurface.Add(Node.LeftFoot, 0.0327);
        rSurface.Add(Node.RightThigh, 0.1169);
        rSurface.Add(Node.RightLeg, 0.0566);
        rSurface.Add(Node.RightFoot, 0.0327);

        //円柱長さ[m]
        cLength = new Dictionary<Node, double>();
        cLength.Add(Node.Head, 0);
        cLength.Add(Node.Neck, 0.075);
        cLength.Add(Node.Chest, 0.182);
        cLength.Add(Node.Back, 0.170);
        cLength.Add(Node.Pelvis, 0.257);
        cLength.Add(Node.LeftShoulder, 0.343);
        cLength.Add(Node.LeftArm, 0.217);
        cLength.Add(Node.LeftHand, 0.480);
        cLength.Add(Node.RightShoulder, 0.343);
        cLength.Add(Node.RightArm, 0.217);
        cLength.Add(Node.RightHand, 0.480);
        cLength.Add(Node.LeftThigh, 0.542);
        cLength.Add(Node.LeftLeg, 0.267);
        cLength.Add(Node.LeftFoot, 0.625);
        cLength.Add(Node.RightThigh, 0.542);
        cLength.Add(Node.RightLeg, 0.267);
        cLength.Add(Node.RightFoot, 0.625);

        //重量比2
        rWeight2 = new Dictionary<Node, double[]>();
        rWeight2.Add(Node.Head, new double[] { 0.303, 0.341, 0.071, 0.092, 0.067, 0.029, 0.097, 0, 0 });
        rWeight2.Add(Node.Neck, rWeight2[Node.Head]);
        rWeight2.Add(Node.Chest, new double[] { 0.069, 0.170, 0.407, 0.172, 0.033, 0.009, 0.033, 0, 0.107 });
        rWeight2.Add(Node.Back, rWeight2[Node.Chest]);
        rWeight2.Add(Node.Pelvis, new double[] { 0.069, 0.266, 0.401, 0.172, 0.033, 0.014, 0.045, 0, 0 });
        rWeight2.Add(Node.LeftShoulder, new double[] { 0.214, 0.100, 0.453, 0.137, 0.057, 0.008, 0.02, 0.011, 0 });
        rWeight2.Add(Node.LeftArm, new double[] { 0.214, 0.101, 0.458, 0.137, 0.057, 0.006, 0.017, 0.010, 0 });
        rWeight2.Add(Node.LeftHand, new double[] { 0.343, 0.033, 0.076, 0.224, 0.253, 0.012, 0.028, 0.031, 0 });
        rWeight2.Add(Node.RightShoulder, rWeight2[Node.LeftShoulder]);
        rWeight2.Add(Node.RightArm, rWeight2[Node.LeftArm]);
        rWeight2.Add(Node.RightHand, rWeight2[Node.LeftHand]);
        rWeight2.Add(Node.LeftThigh, new double[] { 0.242, 0.087, 0.459, 0.115, 0.048, 0.011, 0.028, 0.01, 0 });
        rWeight2.Add(Node.LeftLeg, new double[] { 0.242, 0.086, 0.459, 0.115, 0.044, 0.011, 0.029, 0.014, 0 });
        rWeight2.Add(Node.LeftFoot, new double[] { 0.385, 0.031, 0.036, 0.229, 0.208, 0.021, 0.048, 0.042, 0 });
        rWeight2.Add(Node.RightThigh, rWeight2[Node.LeftThigh]);
        rWeight2.Add(Node.RightLeg, rWeight2[Node.LeftLeg]);
        rWeight2.Add(Node.RightFoot, rWeight2[Node.LeftFoot]);

        //基礎代謝配分比
        bMetRate = new Dictionary<Node, double[]>();
        bMetRate.Add(Node.Head, new double[] { 0.19580, 0.00252, 0.00127, 0.00123 });
        bMetRate.Add(Node.Neck, new double[] { 0.00318, 0.00004, 0.00002, 0.00033 });
        bMetRate.Add(Node.Chest, new double[] { 0.25023, 0.02997, 0.00671, 0.00211 });
        bMetRate.Add(Node.Back, new double[] { 0.22090, 0.02997, 0.00592, 0.00187 });
        bMetRate.Add(Node.Pelvis, new double[] { 0.09509, 0.04804, 0.00950, 0.00300 });
        bMetRate.Add(Node.LeftShoulder, new double[] { 0.00214, 0.00500, 0.00721, 0.00059 });
        bMetRate.Add(Node.LeftArm, new double[] { 0.00111, 0.00260, 0.00037, 0.00031 });
        bMetRate.Add(Node.LeftHand, new double[] { 0.00053, 0.00026, 0.00027, 0.00059 });
        bMetRate.Add(Node.RightShoulder, bMetRate[Node.LeftShoulder]);
        bMetRate.Add(Node.RightArm, bMetRate[Node.LeftArm]);
        bMetRate.Add(Node.RightHand, bMetRate[Node.LeftHand]);
        bMetRate.Add(Node.LeftThigh, new double[] { 0.00405, 0.00973, 0.00178, 0.00144 });
        bMetRate.Add(Node.LeftLeg, new double[] { 0.00120, 0.00260, 0.00041, 0.00027 });
        bMetRate.Add(Node.LeftFoot, new double[] { 0.00144, 0.00041, 0.00066, 0.00118 });
        bMetRate.Add(Node.RightThigh, bMetRate[Node.LeftThigh]);
        bMetRate.Add(Node.RightLeg, bMetRate[Node.LeftLeg]);
        bMetRate.Add(Node.RightFoot, bMetRate[Node.LeftFoot]);

        //基礎血流比
        bBFRate = new Dictionary<Node, double[]>();
        bBFRate.Add(Node.Head, new double[] { 0.10822, 0.00210, 0.00081, 0.01974 });
        bBFRate.Add(Node.Neck, new double[] { 0.05118, 0.00099, 0.00038, 0.00112 });
        bBFRate.Add(Node.Chest, new double[] { 0.27575, 0.02714, 0.00474, 0.00678 });
        bBFRate.Add(Node.Back, new double[] { 0.27040, 0.02713, 0.00474, 0.00509 });
        bBFRate.Add(Node.Pelvis, new double[] { 0.06443, 0.04349, 0.00765, 0.00783 });
        bBFRate.Add(Node.LeftShoulder, new double[] { 0.00113, 0.00454, 0.00056, 0.00314 });
        bBFRate.Add(Node.LeftArm, new double[] { 0.00056, 0.00237, 0.00031, 0.00175 });
        bBFRate.Add(Node.LeftHand, new double[] { 0.00032, 0.00028, 0.00015, 0.00384 });
        bBFRate.Add(Node.RightShoulder, bBFRate[Node.LeftShoulder]);
        bBFRate.Add(Node.RightArm, bBFRate[Node.LeftArm]);
        bBFRate.Add(Node.RightHand, bBFRate[Node.LeftHand]);
        bBFRate.Add(Node.LeftThigh, new double[] { 0.00129, 0.00303, 0.00053, 0.00502 });
        bBFRate.Add(Node.LeftLeg, new double[] { 0.00026, 0.00024, 0.00006, 0.00224 });
        bBFRate.Add(Node.LeftFoot, new double[] { 0.00018, 0.00004, 0.00006, 0.00322 });
        bBFRate.Add(Node.RightThigh, bBFRate[Node.LeftThigh]);
        bBFRate.Add(Node.RightLeg, bBFRate[Node.LeftLeg]);
        bBFRate.Add(Node.RightFoot, bBFRate[Node.LeftFoot]);

        //血管の熱コンダクタンス[W/K]
        hCdBLD = new Dictionary<Node, double[]>();
        hCdBLD.Add(Node.Head, new double[] { 0, 0, 0 });
        hCdBLD.Add(Node.Neck, hCdBLD[Node.Head]);
        hCdBLD.Add(Node.Chest, hCdBLD[Node.Head]);
        hCdBLD.Add(Node.Back, hCdBLD[Node.Head]);
        hCdBLD.Add(Node.Pelvis, hCdBLD[Node.Head]);
        hCdBLD.Add(Node.LeftShoulder, new double[] { 0.586, 57.735, 0.537 });
        hCdBLD.Add(Node.LeftArm, new double[] { 0.383, 37.768, 0.351 });
        hCdBLD.Add(Node.LeftHand, new double[] { 1.534, 16.634, 0.762 });
        hCdBLD.Add(Node.RightShoulder, hCdBLD[Node.LeftShoulder]);
        hCdBLD.Add(Node.RightArm, hCdBLD[Node.LeftArm]);
        hCdBLD.Add(Node.RightHand, hCdBLD[Node.LeftHand]);
        hCdBLD.Add(Node.LeftThigh, new double[] { 0.81, 102.012, 0.826 });
        hCdBLD.Add(Node.LeftLeg, new double[] { 0.435, 54.784, 0.444 });
        hCdBLD.Add(Node.LeftFoot, new double[] { 1.816, 24.277, 0.444 });
        hCdBLD.Add(Node.RightThigh, hCdBLD[Node.LeftThigh]);
        hCdBLD.Add(Node.RightLeg, hCdBLD[Node.LeftLeg]);
        hCdBLD.Add(Node.RightFoot, hCdBLD[Node.LeftFoot]);

        //行列のオフセット
        mOffset = new Dictionary<Node, int>();
        mOffset.Add(Node.Head, 1);
        mOffset.Add(Node.Neck, 7);
        mOffset.Add(Node.Chest, 13);
        mOffset.Add(Node.Back, 19);
        mOffset.Add(Node.Pelvis, 25);
        mOffset.Add(Node.LeftShoulder, 31);
        mOffset.Add(Node.LeftArm, 38);
        mOffset.Add(Node.LeftHand, 45);
        mOffset.Add(Node.RightShoulder, 52);
        mOffset.Add(Node.RightArm, 59);
        mOffset.Add(Node.RightHand, 66);
        mOffset.Add(Node.LeftThigh, 73);
        mOffset.Add(Node.LeftLeg, 80);
        mOffset.Add(Node.LeftFoot, 87);
        mOffset.Add(Node.RightThigh, 94);
        mOffset.Add(Node.RightLeg, 101);
        mOffset.Add(Node.RightFoot, 108);

        //立位の対流熱伝達率[W/(m2 K)]
        cHTransferStand = new Dictionary<Node, double>();
        cHTransferStand.Add(Node.Head, 4.48);
        cHTransferStand.Add(Node.Neck, 4.48);
        cHTransferStand.Add(Node.Chest, 2.97);
        cHTransferStand.Add(Node.Back, 2.91);
        cHTransferStand.Add(Node.Pelvis, 2.85);
        cHTransferStand.Add(Node.LeftShoulder, 3.61);
        cHTransferStand.Add(Node.LeftArm, 3.55);
        cHTransferStand.Add(Node.LeftHand, 3.67);
        cHTransferStand.Add(Node.RightShoulder, 3.61);
        cHTransferStand.Add(Node.RightArm, 3.55);
        cHTransferStand.Add(Node.RightHand, 3.67);
        cHTransferStand.Add(Node.LeftThigh, 2.80);
        cHTransferStand.Add(Node.LeftLeg, 2.04);
        cHTransferStand.Add(Node.LeftFoot, 2.04);
        cHTransferStand.Add(Node.RightThigh, 2.80);
        cHTransferStand.Add(Node.RightLeg, 2.04);
        cHTransferStand.Add(Node.RightFoot, 2.04);

        //座位の対流熱伝達率[W/(m2 K)]
        cHTransferSit = new Dictionary<Node, double>();
        cHTransferSit.Add(Node.Head, 4.75);
        cHTransferSit.Add(Node.Neck, 4.75);
        cHTransferSit.Add(Node.Chest, 3.12);
        cHTransferSit.Add(Node.Back, 2.48);
        cHTransferSit.Add(Node.Pelvis, 1.84);
        cHTransferSit.Add(Node.LeftShoulder, 3.76);
        cHTransferSit.Add(Node.LeftArm, 3.62);
        cHTransferSit.Add(Node.LeftHand, 2.06);
        cHTransferSit.Add(Node.RightShoulder, 3.76);
        cHTransferSit.Add(Node.RightArm, 3.62);
        cHTransferSit.Add(Node.RightHand, 2.06);
        cHTransferSit.Add(Node.LeftThigh, 2.98);
        cHTransferSit.Add(Node.LeftLeg, 2.98);
        cHTransferSit.Add(Node.LeftFoot, 2.62);
        cHTransferSit.Add(Node.RightThigh, 2.98);
        cHTransferSit.Add(Node.RightLeg, 2.98);
        cHTransferSit.Add(Node.RightFoot, 2.62);

        //温冷感信号の重み付け係数[-]
        skinSignal = new Dictionary<Node, double>();
        skinSignal.Add(Node.Head, 0.0547);
        skinSignal.Add(Node.Neck, 0.0146);
        skinSignal.Add(Node.Chest, 0.1492);
        skinSignal.Add(Node.Back, 0.1321);
        skinSignal.Add(Node.Pelvis, 0.2122);
        skinSignal.Add(Node.LeftShoulder, 0.0227);
        skinSignal.Add(Node.LeftArm, 0.0117);
        skinSignal.Add(Node.LeftHand, 0.0923);
        skinSignal.Add(Node.RightShoulder, 0.0227);
        skinSignal.Add(Node.RightArm, 0.0117);
        skinSignal.Add(Node.RightHand, 0.0923);
        skinSignal.Add(Node.LeftThigh, 0.0501);
        skinSignal.Add(Node.LeftLeg, 0.0251);
        skinSignal.Add(Node.LeftFoot, 0.0167);
        skinSignal.Add(Node.RightThigh, 0.0501);
        skinSignal.Add(Node.RightLeg, 0.0251);
        skinSignal.Add(Node.RightFoot, 0.0167);

        //発汗信号の重み付け係数[-]
        sweatSignalR = new Dictionary<Node, double>();
        sweatSignalR.Add(Node.Head, 0.0640);
        sweatSignalR.Add(Node.Neck, 0.0170);
        sweatSignalR.Add(Node.Chest, 0.1460);
        sweatSignalR.Add(Node.Back, 0.1290);
        sweatSignalR.Add(Node.Pelvis, 0.2060);
        sweatSignalR.Add(Node.LeftShoulder, 0.0510);
        sweatSignalR.Add(Node.LeftArm, 0.0260);
        sweatSignalR.Add(Node.LeftHand, 0.0155);
        sweatSignalR.Add(Node.RightShoulder, 0.0510);
        sweatSignalR.Add(Node.RightArm, 0.0260);
        sweatSignalR.Add(Node.RightHand, 0.0155);
        sweatSignalR.Add(Node.LeftThigh, 0.00730);
        sweatSignalR.Add(Node.LeftLeg, 0.00360);
        sweatSignalR.Add(Node.LeftFoot, 0.00175);
        sweatSignalR.Add(Node.RightThigh, 0.00730);
        sweatSignalR.Add(Node.RightLeg, 0.00360);
        sweatSignalR.Add(Node.RightFoot, 0.00175);

        //ふるえ信号の重み付け係数[-]
        shivSignalR = new Dictionary<Node, double>();
        shivSignalR.Add(Node.Head, 0.0339);
        shivSignalR.Add(Node.Neck, 0.0436);
        shivSignalR.Add(Node.Chest, 0.2739);
        shivSignalR.Add(Node.Back, 0.2410);
        shivSignalR.Add(Node.Pelvis, 0.3874);
        shivSignalR.Add(Node.LeftShoulder, 0.0024);
        shivSignalR.Add(Node.LeftArm, 0.0014);
        shivSignalR.Add(Node.LeftHand, 0.0002);
        shivSignalR.Add(Node.RightShoulder, 0.0024);
        shivSignalR.Add(Node.RightArm, 0.0014);
        shivSignalR.Add(Node.RightHand, 0.0002);
        shivSignalR.Add(Node.LeftThigh, 0.0039);
        shivSignalR.Add(Node.LeftLeg, 0.0018);
        shivSignalR.Add(Node.LeftFoot, 0.0004);
        shivSignalR.Add(Node.RightThigh, 0.0039);
        shivSignalR.Add(Node.RightLeg, 0.0018);
        shivSignalR.Add(Node.RightFoot, 0.0004);

        //血管収縮信号の重み付け係数[-]
        dilSignalR = new Dictionary<Node, double>();
        dilSignalR.Add(Node.Head, 0.1042);
        dilSignalR.Add(Node.Neck, 0.0277);
        dilSignalR.Add(Node.Chest, 0.0980);
        dilSignalR.Add(Node.Back, 0.0860);
        dilSignalR.Add(Node.Pelvis, 0.1379);
        dilSignalR.Add(Node.LeftShoulder, 0.0313);
        dilSignalR.Add(Node.LeftArm, 0.0163);
        dilSignalR.Add(Node.LeftHand, 0.0605);
        dilSignalR.Add(Node.RightShoulder, 0.0313);
        dilSignalR.Add(Node.RightArm, 0.0163);
        dilSignalR.Add(Node.RightHand, 0.0605);
        dilSignalR.Add(Node.LeftThigh, 0.0920);
        dilSignalR.Add(Node.LeftLeg, 0.0230);
        dilSignalR.Add(Node.LeftFoot, 0.0500);
        dilSignalR.Add(Node.RightThigh, 0.0920);
        dilSignalR.Add(Node.RightLeg, 0.0230);
        dilSignalR.Add(Node.RightFoot, 0.0500);

        //血管拡張信号の重み付け係数[-]
        strSignalR = new Dictionary<Node, double>();
        strSignalR.Add(Node.Head, 0.0213);
        strSignalR.Add(Node.Neck, 0.0213);
        strSignalR.Add(Node.Chest, 0.0638);
        strSignalR.Add(Node.Back, 0.0638);
        strSignalR.Add(Node.Pelvis, 0.0638);
        strSignalR.Add(Node.LeftShoulder, 0.0213);
        strSignalR.Add(Node.LeftArm, 0.0213);
        strSignalR.Add(Node.LeftHand, 0.1489);
        strSignalR.Add(Node.RightShoulder, 0.0213);
        strSignalR.Add(Node.RightArm, 0.0213);
        strSignalR.Add(Node.RightHand, 0.1489);
        strSignalR.Add(Node.LeftThigh, 0.0920);
        strSignalR.Add(Node.LeftLeg, 0.0230);
        strSignalR.Add(Node.LeftFoot, 0.0500);
        strSignalR.Add(Node.RightThigh, 0.0920);
        strSignalR.Add(Node.RightLeg, 0.0230);
        strSignalR.Add(Node.RightFoot, 0.0500);
      }

      #endregion

      #region コンストラクタ

      /// <summary>Initializes a new instance of the Tanabe multi-node model.</summary>
      /// <param name="body">Body segment instance.</param>
      /// <param name="node">Body segment node.</param>
      /// <param name="bodyWeight">Whole-body weight [kg].</param>
      /// <param name="bodyHeight">Height [m].</param>
      /// <param name="bodySurfaceArea">Whole-body surface area [m²].</param>
      /// <param name="fatPercentage">Body fat percentage [%].</param>
      /// <param name="metabolicRate">Basal metabolic rate of this segment [W].</param>
      /// <param name="basalBloodFlow">Basal blood flow rate [mL/s].</param>
      internal bodyPart
        (IReadOnlyTanabeMultiNodeModel body, Node node, double bodyWeight, double bodyHeight, 
        double bodySurfaceArea, double fatPercentage, double metabolicRate, double basalBloodFlow)
      {
        this.body = body;

        temperatures = new Dictionary<Layer, double>();
        heatCapacity = new Dictionary<Layer, double>();
        bloodFlow = new Dictionary<Layer, double>();
        basalMetabolicRate = new Dictionary<Layer, double>();
        downStreamParts = new List<bodyPart>();

        this.weight = bodyWeight * rWeight1[node];
        this.surfaceArea = bodySurfaceArea * rSurface[node];
        this.node = node;
        double[] rWt = (double[])rWeight2[node].Clone();
        double[] rMb = bMetRate[node];
        double[] rBFb = bBFRate[node];
        double len = cLength[node] * Math.Pow(bodyHeight / 1.72, 0.725);
        hCdBLD[node].CopyTo(hConductance, 3);

        //脂肪率による重量調整
        double rFat;
        if (fatPercentage < 0.15) rFat = rWt[3] * fatPercentage / 0.15;
        else rFat = (rWt[3] * (1 - fatPercentage) + fatPercentage - 0.15) / (1 - 0.15);
        double rr = (1 - rFat) / (1 - rWt[3]);
        for (int i = 0; i < rWt.Length; i++) rWt[i] *= rr * weight;
        rWt[3] = rFat * weight;
        muscleWeight = rWt[2];

        //基礎代謝[W]の計算
        basalMetabolicRate[Layer.Core] = metabolicRate * rMb[0];
        basalMetabolicRate[Layer.Muscle] = metabolicRate * rMb[1];
        basalMetabolicRate[Layer.Fat] = metabolicRate * rMb[2];
        basalMetabolicRate[Layer.Skin] = metabolicRate * rMb[3];

        //基礎血流[mL/s]の計算//核と脂肪は固定値
        bloodFlow[Layer.Core] = basalBloodFlow * rBFb[0];
        basalBloodFlow_Muscle = basalBloodFlow * rBFb[1];
        bloodFlow[Layer.Fat] = basalBloodFlow * rBFb[2];
        basalBloodFlow_Skin = basalBloodFlow * rBFb[3];

        //熱容量[J/K]の計算
        heatCapacity[Layer.Core] = rWt[0] * SPECIFIC_HEAT_BORN + rWt[1] * SPECIFIC_HEAT_ELSE;
        heatCapacity[Layer.Muscle] = rWt[2] * SPECIFIC_HEAT_ELSE;
        heatCapacity[Layer.Fat] = rWt[3] * SPECIFIC_HEAT_FAT;
        heatCapacity[Layer.Skin] = rWt[4] * SPECIFIC_HEAT_ELSE;
        heatCapacity[Layer.Artery] = rWt[5] * SPECIFIC_HEAT_ELSE;
        heatCapacity[Layer.DeepVein] = rWt[6] * SPECIFIC_HEAT_ELSE;
        heatCapacity[Layer.SuperficialVein] = rWt[7] * SPECIFIC_HEAT_ELSE;
        centralBloodHeatCapacity = rWt[8] * SPECIFIC_HEAT_ELSE;

        //各層の体積[m3]の計算
        double[] wt = new double[4];
        wt[0] = 0.001 * (rWt[0] + rWt[1] + rWt[5] + rWt[6] + rWt[8]); //核の体積[m3]
        wt[1] = rWt[2] / 1000d; //筋肉の体積[m3]
        wt[2] = rWt[3] / 1000d; //脂肪の体積[m3]
        wt[3] = 0.001 * (rWt[4] + rWt[7]);  //皮膚の体積[m3]

        //四肢末端部の場合にはAVA最大流量を計算
        if ((node & TERMINAL_NODE) != 0) maxAVA = wt[3] * 5000;

        //熱コンダクタンス[W/K]の計算
        double[] lmda = new double[] { 0.4184, 0.4184, 0.3347, 0.3347 };  //熱伝導率[W/mK]
        double[] rads = new double[7];
        rads[0] = wt[0] / 2;
        for (int i = 1; i < rads.Length; i++) rads[i] = rads[i - 1] + wt[i / 2] / 2;
        if (node == Node.Head)
        {
          //球とみなして計算
          for (int i = 0; i < rads.Length; i++) rads[i] = Math.Pow(rads[i] * 3d / (4d * Math.PI), 1d / 3d);
          for (int i = 0; i < 3; i++)
            hConductance[i] = 4d * Math.PI / ((1 / rads[2 * i] - 1 / rads[2 * i + 1]) / lmda[i]
              + (1 / rads[2 * i + 1] - 1 / rads[2 * i + 2]) / lmda[i + 1]);
        }
        else
        {
          //多層円管とみなして計算
          for (int i = 0; i < rads.Length; i++) rads[i] = Math.Sqrt(rads[i] / (Math.PI * len));
          for (int i = 0; i < 3; i++)
            hConductance[i] = 2d * Math.PI * len / (Math.Log(rads[2 * i + 1] / rads[2 * i]) / lmda[i] 
              + Math.Log(rads[2 * i + 2] / rads[2 * i + 1]) / lmda[i + 1]);
        }
      }

      #endregion

      #region internalメソッド

      /// <summary>Sets matrix elements for this body segment.</summary>
      /// <param name="bMatrix">B-matrix (thermal conductance matrix).</param>
      /// <param name="zVector">Z-vector (heat source term).</param>
      internal void makeMatrix(IMatrix bMatrix, IVector zVector)
      {
        double tStep = body.TimeStep;
        int os = mOffset[node]; //行列のオフセット

        //BM行列を生成
        //核
        bMatrix[os + 0, os + 0] = heatCapacity[Layer.Core] / tStep
          + BLD_SPECIFICHEAT * bloodFlow[Layer.Core] + 2d * hConductance[3] + hConductance[0];
        bMatrix[os + 0, os + 1] = -hConductance[0];
        bMatrix[os + 0, os + 4] = -BLD_SPECIFICHEAT * bloodFlow[Layer.Core] - hConductance[3];
        bMatrix[os + 0, os + 5] = -hConductance[3];
        //筋肉
        bMatrix[os + 1, os + 0] = -hConductance[0];
        bMatrix[os + 1, os + 1] = heatCapacity[Layer.Muscle] / tStep
          + BLD_SPECIFICHEAT * bloodFlow[Layer.Muscle] + hConductance[0] + hConductance[1];
        bMatrix[os + 1, os + 2] = -hConductance[1];
        bMatrix[os + 1, os + 4] = -BLD_SPECIFICHEAT * bloodFlow[Layer.Muscle];
        //脂肪
        bMatrix[os + 2, os + 1] = -hConductance[1];
        bMatrix[os + 2, os + 2] = heatCapacity[Layer.Fat] / tStep
          + BLD_SPECIFICHEAT * bloodFlow[Layer.Fat] + hConductance[1] + hConductance[2];
        bMatrix[os + 2, os + 3] = -hConductance[2];
        bMatrix[os + 2, os + 4] = -BLD_SPECIFICHEAT * bloodFlow[Layer.Fat];
        //皮膚
        bMatrix[os + 3, os + 2] = -hConductance[2];
        bMatrix[os + 3, os + 3] = heatCapacity[Layer.Skin] / tStep
          + BLD_SPECIFICHEAT * bloodFlow[Layer.Skin] + hConductance[2]
          + contactPortionRate * hConductance[6] + (1 - contactPortionRate) * hConductance[7];
        if ((node & LIMBS) != 0) bMatrix[os + 3, os + 3] += hConductance[4];
        bMatrix[os + 3, os + 4] = -BLD_SPECIFICHEAT * bloodFlow[Layer.Skin];
        //動脈
        double upperFlow = bloodFlow[Layer.DeepVein] + bloodFlow[Layer.SuperficialVein];
        bMatrix[os + 4, os + 0] = -hConductance[3];
        bMatrix[os + 4, os + 4] = heatCapacity[Layer.Artery] / tStep +
          BLD_SPECIFICHEAT * upperFlow + hConductance[3] + hConductance[5];
        bMatrix[os + 4, os + 5] = -hConductance[5];
        //静脈
        bMatrix[os + 5, os + 0] = -BLD_SPECIFICHEAT * bloodFlow[Layer.Core] - hConductance[3];
        bMatrix[os + 5, os + 1] = -BLD_SPECIFICHEAT * bloodFlow[Layer.Muscle];
        bMatrix[os + 5, os + 2] = -BLD_SPECIFICHEAT * bloodFlow[Layer.Fat];
        bMatrix[os + 5, os + 3] = -BLD_SPECIFICHEAT * bloodFlow[Layer.Skin];
        bMatrix[os + 5, os + 4] = -hConductance[5];
        bMatrix[os + 5, os + 5] = heatCapacity[Layer.DeepVein] / tStep
          + BLD_SPECIFICHEAT * bloodFlow[Layer.DeepVein] + hConductance[3] + hConductance[5];
        if (node == Node.Pelvis)
          bMatrix[os + 5, os + 5] += BLD_SPECIFICHEAT * bloodFlow[Layer.SuperficialVein];
        //四肢部位のみ（表在静脈）
        if ((node & LIMBS) != 0)
        {
          bMatrix[os + 6, os + 3] = -hConductance[4];
          if ((node & TERMINAL_NODE) != 0)
            bMatrix[os + 6, os + 4] = -BLD_SPECIFICHEAT * bloodFlow[Layer.SuperficialVein];
          bMatrix[os + 6, os + 6] = heatCapacity[Layer.SuperficialVein] / tStep +
            BLD_SPECIFICHEAT * bloodFlow[Layer.SuperficialVein] + hConductance[4];

          if ((node & LIMBS) != 0) bMatrix[os + 3, os + 6] = -hConductance[4];
        }

        //上流部位の動脈血流入
        if (upperStreamPart != null)
          bMatrix[os + 4, mOffset[upperStreamPart.node] + 4] = -BLD_SPECIFICHEAT * upperFlow;
        else bMatrix[os + 4, 0] = -BLD_SPECIFICHEAT * upperFlow;

        //下流部位の静脈血流入
        foreach (bodyPart bp in downStreamParts)
        {
          //深部静脈
          bMatrix[os + 5, mOffset[bp.node] + 5] = -BLD_SPECIFICHEAT * bp.bloodFlow[Layer.DeepVein];
          //表在静脈
          double downFlow = -BLD_SPECIFICHEAT * bp.bloodFlow[Layer.SuperficialVein];
          if (node == Node.Pelvis) bMatrix[os + 5, mOffset[bp.node] + 6] += downFlow;
          else bMatrix[os + 6, mOffset[bp.node] + 6] = downFlow;
        }

        //Zベクトルを生成
        zVector[os + 0] = heatCapacity[Layer.Core] / tStep * temperatures[Layer.Core]
          + basalMetabolicRate[Layer.Core];
        zVector[os + 1] = heatCapacity[Layer.Muscle] / tStep * temperatures[Layer.Muscle]
          + basalMetabolicRate[Layer.Muscle] + shiveringLoad + externalWork;
        zVector[os + 2] = heatCapacity[Layer.Fat] / tStep * temperatures[Layer.Fat]
          + basalMetabolicRate[Layer.Fat];
        double wvSk = Water.GetSaturationPressure(temperatures[Layer.Skin]);
        zVector[os + 3] = heatCapacity[Layer.Skin] / tStep * temperatures[Layer.Skin]
          + basalMetabolicRate[Layer.Skin] + contactPortionRate * hConductance[6] * materialTemperature
          + (1 - contactPortionRate) * (hConductance[7] * operatingTemperature) - latentHeatLoss;
        zVector[os + 4] = heatCapacity[Layer.Artery] / tStep * temperatures[Layer.Artery];
        zVector[os + 5] = heatCapacity[Layer.DeepVein] / tStep
          * temperatures[Layer.DeepVein];
        //四肢部位のみ（表在静脈）
        if ((node & LIMBS) != 0) zVector[os + 6] =
          heatCapacity[Layer.SuperficialVein] / tStep * temperatures[Layer.SuperficialVein];
      }

      /// <summary>Updates thermoregulatory control signals (sweating, shivering, vasomotion).</summary>
      /// <param name="signal">Thermoregulatory control signal.</param>
      /// <param name="sweatSignal">Sweating control signal.</param>
      /// <param name="shiveringSignal">Shivering control signal.</param>
      /// <param name="vasodilatationSignal">Vasodilation control signal.</param>
      /// <param name="vasoconstrictionSignal">Vasoconstriction control signal.</param>
      /// <param name="avaRate">AVA (arterio-venous anastomosis) opening rate.</param>
      internal void updateControl
        (double signal, double sweatSignal, double shiveringSignal,
        double vasodilatationSignal, double vasoconstrictionSignal, double avaRate)
      {
        //最大蒸発熱損失[W]
        double wvSk = Water.GetSaturationPressure(temperatures[Layer.Skin]);
        double eMax = (1 - contactPortionRate) * hConductance[8] * (wvSk - waterVaporPressure);

        //制御OFFの場合
        if (initializing)
        {
          evaporativeHeatLoss_Sweat = 0;
          latentHeatLoss = eMax * 0.06;
          shiveringLoad = 0;
          bloodFlow[Layer.Skin] = basalBloodFlow_Skin;
          bloodFlow[Layer.AVA] = 0;
          return;
        }

        //係数計算
        double dsp = temperatures[Layer.Skin] - setpoint_Skin;
        double pow1 = sweatSignal * Math.Pow(2, dsp / 10d);
        double pow2 = Math.Pow(2, dsp / 6d);

        //蒸発熱損失[W]
        evaporativeHeatLoss_Sweat = pow1 * sweatSignalR[node];
        latentHeatLoss = eMax * Math.Min(0.85, 0.06 + 0.94 * evaporativeHeatLoss_Sweat / eMax);

        //ふるえによる熱産生[W]
        shiveringLoad = shiveringSignal * shivSignalR[node];

        //皮膚血流量[mL/s]
        bloodFlow[Layer.Skin] = (basalBloodFlow_Skin + dilSignalR[node]
          * vasodilatationSignal) / (1 + strSignalR[node] * vasoconstrictionSignal) * pow2;

        //AVA血流量[mL/s]
        bloodFlow[Layer.AVA] = maxAVA * Math.Max(0, Math.Min(1, avaRate));
      }

      /// <summary>Connects two body segments for blood flow heat exchange.</summary>
      /// <param name="downStreamPart">Downstream (venous-side) body segment.</param>
      internal void connect(bodyPart downStreamPart)
      {
        downStreamPart.upperStreamPart = this;
        this.downStreamParts.Add(downStreamPart);
      }

      /// <summary>Updates blood flow rates based on current control signals.</summary>
      internal void updateBloodFlow()
      {
        //血流の計算
        bloodFlow[Layer.Muscle] = basalBloodFlow_Muscle + 0.239 * (externalWork + shiveringLoad);
        double bfSum = bloodFlow[Layer.Core] + bloodFlow[Layer.Muscle] + bloodFlow[Layer.Fat] + bloodFlow[Layer.Skin];

        //下流部位の血流を更新して静脈を計算
        Layer dv = Layer.DeepVein;
        Layer sv = Layer.SuperficialVein;
        bloodFlow[dv] = bloodFlow[sv] = 0;
        foreach (bodyPart bp in downStreamParts)
        {
          bp.updateBloodFlow();
          bloodFlow[dv] += bp.bloodFlow[Layer.DeepVein];
          bloodFlow[sv] += bp.bloodFlow[Layer.SuperficialVein];
        }
        bloodFlow[Layer.Artery] = bloodFlow[dv] + bloodFlow[sv];
        bloodFlow[dv] += bfSum;
        bloodFlow[sv] += bloodFlow[Layer.AVA];
        if (node == Node.Pelvis)
        {
          bloodFlow[dv] += bloodFlow[sv];
          bloodFlow[sv] = 0;
        }
      }

      /// <summary>Gets the current thermoregulatory control signals.</summary>
      /// <returns>Thermoregulatory control signal value.</returns>
      internal double getSignal()
      { return (temperatures[Layer.Skin] - setpoint_Skin) * skinSignal[node]; }

      /// <summary>Computes sensible heat loss from the skin surface [W].</summary>
      /// <returns>Sensible heat loss from skin [W].</returns>
      internal double getSensibleHeatLoss()
      {
        return (temperatures[Layer.Skin] - operatingTemperature) * hConductance[7] * (1 - contactPortionRate) 
          + (temperatures[Layer.Skin] - materialTemperature) * hConductance[6] * contactPortionRate;
      }

      /// <summary>Gets the thermal conductance between body segments [W/K].</summary>
      /// <param name="layer1">First body segment.</param>
      /// <param name="layer2">Second body segment.</param>
      /// <returns>Thermal conductance between body segments [W/K].</returns>
      internal double getHeatConductance(Layer layer1, Layer layer2)
      {
        switch (layer1 | layer2)
        {
          case (Layer.Core | Layer.Muscle):
            return hConductance[0];
          case (Layer.Muscle | Layer.Fat):
            return hConductance[1];
          case (Layer.Fat | Layer.Skin):
            return hConductance[2];
          case (Layer.DeepVein | Layer.Core):
          case (Layer.Artery | Layer.Core):
            return hConductance[3];
          case (Layer.SuperficialVein | Layer.Skin):
            return hConductance[4];
          case (Layer.Artery | Layer.DeepVein):
            return hConductance[5];
          default:
            return 0;
        }
      }

      /// <summary>Updates the temperature of this body segment from the solution vector.</summary>
      /// <param name="tVector">Temperature state vector.</param>
      internal void updateTemperature(IVector tVector)
      {
        int os = mOffset[node];
        temperatures[Layer.Core] = tVector[os];
        temperatures[Layer.Muscle] = tVector[os + 1];
        temperatures[Layer.Fat] = tVector[os + 2];
        temperatures[Layer.Skin] = tVector[os + 3];
        temperatures[Layer.Artery] = tVector[os + 4];
        temperatures[Layer.DeepVein] = tVector[os + 5];
        if ((node & LIMBS) != 0) temperatures[Layer.SuperficialVein] = tVector[os + 6];
        UpdateSkinHeatConductance();
      }

      #endregion

      #region 境界条件設定処理

      /// <summary>Sets up thermal contact between this body segment and an object.</summary>
      /// <param name="temperature">Object surface temperature [°C].</param>
      /// <param name="heatConductance">Thermal conductance to the object [W/K].</param>
      /// <param name="contactPortionRate">Fraction of skin surface in contact [-].</param>
      internal void contact(double temperature, double heatConductance, double contactPortionRate)
      {
        this.materialTemperature = temperature;
        this.hConductance[6] = heatConductance;
        this.contactPortionRate = contactPortionRate;
      }

      /// <summary>Sets the clothing insulation [clo].</summary>
      /// <param name="clothingIndex">Clothing insulation [clo].</param>
      internal void setClothingIndex(double clothingIndex)
      {
        this.clothingIndex = clothingIndex;
        UpdateSkinHeatConductance();
      }

      /// <summary>Sets the thermal boundary condition for this body segment.</summary>
      /// <param name="velocity">Air velocity [m/s].</param>
      /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
      /// <param name="dryBulbTemperature">Dry-bulb temperature [°C].</param>
      /// <param name="waterVaporPressure">Water vapor pressure [kPa].</param>
      internal void updateBoundary
        (double velocity, double meanRadiantTemperature,double dryBulbTemperature, double waterVaporPressure)
      {
        this.velocity = velocity;
        this.meanRadiantTemperature = meanRadiantTemperature;
        this.dryBulbTemperature = dryBulbTemperature;
        this.waterVaporPressure = waterVaporPressure;
        UpdateSkinHeatConductance();
      }

      /// <summary>Updates sensible and latent heat transfer coefficients at the skin surface.</summary>
      private void UpdateSkinHeatConductance()
      {
        //着衣面積率[-]の計算
        double fcl = 1 + 0.25 * clothingIndex;
        double rcl = 0.155 * clothingIndex;

        double cht;
        if (body.IsStanding) cht = cHTransferStand[node];
        else cht = cHTransferSit[node];

        //放射熱伝達率[W/(m2 K)]を更新する(衣服の表面温度を収束計算)
        double vel = Math.Max(0.15, velocity);
        double ra, eff;
        clothTemperature = 30;
        if (body.IsStanding) eff = 0.73;
        else eff = 0.72;
        while (true)
        {
          double ctOld = clothTemperature;
          //放射熱伝達率[W/(m2K)]の計算
          radiativeHeatTransferCoefficient = 4d * PhysicsConstants.StefanBoltzmannConstant * eff 
            * Math.Pow(PhysicsConstants.ToKelvin((clothTemperature + meanRadiantTemperature) / 2d), 3);
          //対流熱伝達率[W/(m2K)の計算]
          if (initializing) convectiveHeatTransferCoefficient = cht;
          else
          {
            convectiveHeatTransferCoefficient = cht * Math.Max(2.58 * Math.Sqrt(vel),
              (clothTemperature - dryBulbTemperature) / (setpoint_Skin - 28.8));
          }
          //総合熱伝達率[W/(m2K)]の計算
          double hcr = radiativeHeatTransferCoefficient + convectiveHeatTransferCoefficient;
          //空気層顕熱抵抗[(m2K)/W]の計算
          ra = 1 / (fcl * hcr);
          //作用温度[C]の計算
          operatingTemperature = (radiativeHeatTransferCoefficient * meanRadiantTemperature
            + convectiveHeatTransferCoefficient * dryBulbTemperature) / hcr;
          //衣服温度[C]の計算
          clothTemperature = (ra * temperatures[Layer.Skin] + rcl * operatingTemperature) / (ra + rcl);
          //衣服温度の更新量が0.01C以下で収束と判定
          if (Math.Abs(ctOld - clothTemperature) < 0.01) break;
        }

        //顕熱伝達率[W/K]の計算
        hConductance[7] = surfaceArea / (rcl + 1d / (fcl 
          * (radiativeHeatTransferCoefficient + convectiveHeatTransferCoefficient)));

        //潜熱伝達率[W/K]の計算
        double lewis = 0.0555 * (PhysicsConstants.ToKelvin(temperatures[Layer.Skin]));
        hConductance[8] = surfaceArea * lewis / (1 / (fcl * convectiveHeatTransferCoefficient) + rcl / I_CLS);
      }

      #endregion

    }
  }
}
