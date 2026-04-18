/* RandomWeather.cs
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

using System;

using Popolo.Core.Numerics;
using Popolo.Core.Physics;

namespace Popolo.Core.Climate
{
  /// <summary>
  /// Generates stochastic weather time series using a probabilistic weather model.
  /// </summary>
  /// <remarks>
  /// Based on: Togashi, E., "Development of Stochastic Weather Process Model for
  /// Evaluate Risk of Energy Saving Investment," Journal of SHASE, 2015.
  /// </remarks>
  public class RandomWeather
  {

    #region 列挙型定義

    /// <summary>Specifies the location for weather generation.</summary>
    public enum Location
    {
      /// <summary>Tokyo</summary>
      Tokyo,
      /// <summary>Osaka</summary>
      Osaka,
      /// <summary>Fukuoka</summary>
      Fukuoka,
      /// <summary>Sapporo</summary>
      Sapporo,
      /// <summary>Sendai</summary>
      Sendai,
      /// <summary>Naha</summary>
      Naha
    }

    #endregion

    #region 変数

    /// <summary>Solar calculator for the calculation site.</summary>
    private Sun _sun = null!;

    /// <summary>Standard deviations of the trend component (dryBulb temp, humidity ratio, atm. transmissivity).</summary>
    private double _sdevTDT, _sdevTHR, _sdevTAT;

    /// <summary>VAR model coefficients for dryBulb temperature and humidity ratio.</summary>
    private double[] _iDTCof, _iHRCof;

    /// <summary>AR model coefficient and white noise standard deviations.</summary>
    private double _iATCof, _iDTSD, _iHRSD, _iATSD;

    /// <summary>Amplitude modulation and absolute shift coefficients.</summary>
    private double _shiftDT, _swingDTa, _swingDTb, _shiftHR;

    /// <summary>Fourier coefficients for the annual cycle component.</summary>
    private double[] _acaDT, _bcaDT, _acaHR, _bcaHR, _acaAT, _bcaAT,
      _acFF, _bcFF, _acCC, _bcCC, _acDTSD, _bcDTSD, _acHRSD, _bcHRSD;

    /// <summary>Fourier coefficients for the circadian cycle component.</summary>
    private double[] _accDT, _bccDT,
      _accHR, _bccHR, _acCLD, _bcCLD, _acFAR, _bcFAR;

    /// <summary>Minimum relative humidity [%].</summary>
    private double _minRelativeHumidity = 10;

    /// <summary>Pseudo-random number generator (Mersenne Twister).</summary>
    private MersenneTwister _rnd;

    /// <summary>Normal random number generator.</summary>
    private NormalRandom _nRnd;

    #region 東京

    //トレンド成分標準偏差
    private static readonly double sdevTDT_Tokyo = 0.5686;
    private static readonly double sdevTHR_Tokyo = 0.3078;
    private static readonly double sdevTAT_Tokyo = 0.0241;

    //ARモデル係数
    private static readonly double[] iDTCof_Tokyo = new double[] { 1.1366, 0.0836, -0.0915, 0.0017, -0.1000, -0.0253 };
    private static readonly double[] iHRCof_Tokyo = new double[] { 0.0212, 1.0330, 0.0186, -0.0618, -0.0262, -0.0197 };
    private static readonly double iATCof_Tokyo = 0.8306;
    private static readonly double iDTSD_Tokyo = 0.4572;
    private static readonly double iHRSD_Tokyo = 0.3636;
    private static readonly double iATSD_Tokyo = 0.0289;

    //振幅変調・シフト係数
    private static readonly double shiftDT_Tokyo = 2.584;
    private static readonly double swingDTa_Tokyo = 1.118;
    private static readonly double swingDTb_Tokyo = 1.249;
    private static readonly double shiftHR_Tokyo = -2.806;

    //年周期フーリエ係数
    private static readonly double[] acaDT_Tokyo = new double[] { 16.1553, -9.1951, -0.5818, 0.6386, -0.4619 };
    private static readonly double[] bcaDT_Tokyo = new double[] { 0.0000, 4.5427, -0.3686, 0.5093, -0.2657 };
    private static readonly double[] acaHR_Tokyo = new double[] { 8.4347, -5.8197, 0.2694, 0.3759, -0.1934 };
    private static readonly double[] bcaHR_Tokyo = new double[] { 0.0000, 2.9745, -1.2801, 0.4618, -0.0777 };
    private static readonly double[] acaAT_Tokyo = new double[] { 0.3797, 0.1610, 0.0332, 0.0231, -0.0285 };
    private static readonly double[] bcaAT_Tokyo = new double[] { 0.0000, -0.0061, 0.0052, 0.0085, -0.0225 };
    private static readonly double[] acFF_Tokyo = new double[] { 0.8132, 0.0770, 0.0077, 0.0152, -0.0207 };
    private static readonly double[] bcFF_Tokyo = new double[] { 0.0000, 0.0002, -0.0101, 0.0192, -0.0217 };
    private static readonly double[] acCC_Tokyo = new double[] { 0.7975, -0.0427, -0.0228, -0.0255, 0.0199 };
    private static readonly double[] bcCC_Tokyo = new double[] { 0.0000, -0.0055, 0.0156, -0.0112, 0.0037 };
    private static readonly double[] acDTSD_Tokyo = new double[] { 1.2483, 0.0497, -0.0513, 0, 0 };
    private static readonly double[] bcDTSD_Tokyo = new double[] { 0.0000, -0.1111, 0.0172, 0, 0 };
    private static readonly double[] acHRSD_Tokyo = new double[] { 1.3130, -0.4424, -0.1839, 0, 0 };
    private static readonly double[] bcHRSD_Tokyo = new double[] { 0.0000, 0.2039, 0.0424, 0, 0 };

    //日周期フーリエ係数
    private static readonly double[] accDT_Tokyo = new double[] { 0.0000, -1.5906, 0.5391, -0.0523 };
    private static readonly double[] bccDT_Tokyo = new double[] { 0.0000, 1.7772, -0.2794, -0.0602 };
    private static readonly double[] accHR_Tokyo = new double[] { 0.0000, 0.1410, -0.0029, -0.0107 };
    private static readonly double[] bccHR_Tokyo = new double[] { 0.0000, 0.1681, 0.0217, -0.0091 };
    private static readonly double[] acCLD_Tokyo = new double[] { 0.0450, 0.3672, 0.0000, -0.0390 };
    private static readonly double[] bcCLD_Tokyo = new double[] { 0.0000, 0.0230, 0.0000, -0.0037 };
    private static readonly double[] acFAR_Tokyo = new double[] { 0.3807, 0.2269, 0.0000, -0.0008 };
    private static readonly double[] bcFAR_Tokyo = new double[] { 0.0000, 0.0062, 0.0000, -0.0019 };

    //最小相対湿度
    private static readonly double minimumRelativeHumidity_Tokyo = 10;

    #endregion

    #region 大阪

    //トレンド成分標準偏差
    private static readonly double sdevTDT_Osaka = 0.5154;
    private static readonly double sdevTHR_Osaka = 0.2984;
    private static readonly double sdevTAT_Osaka = 0.0323;

    //ARモデル係数
    private static readonly double[] iDTCof_Osaka = new double[] { 1.1584, 0.0931, -0.0837, -0.0088, -0.1388, -0.0284 };
    private static readonly double[] iHRCof_Osaka = new double[] { -0.0086, 1.0589, 0.0413, -0.0643, -0.0146, -0.0403 };
    private static readonly double iATCof_Osaka = 0.7394;
    private static readonly double iDTSD_Osaka = 0.4360;
    private static readonly double iHRSD_Osaka = 0.3315;
    private static readonly double iATSD_Osaka = 0.0906;

    //振幅変調・シフト係数
    private static readonly double shiftDT_Osaka = 0;
    private static readonly double swingDTa_Osaka = 1.486;
    private static readonly double swingDTb_Osaka = 1.337;
    private static readonly double shiftHR_Osaka = -4.173;

    //年周期フーリエ係数
    private static readonly double[] acaDT_Osaka = new double[] { 16.8268, -9.3956, -0.3928, 0.3107, -0.2057 };
    private static readonly double[] bcaDT_Osaka = new double[] { 0.0000, 4.4420, -0.5665, 0.5255, -0.3072 };
    private static readonly double[] acaHR_Osaka = new double[] { 9.4201, -6.0004, 0.5700, 0.1399, -0.0428 };
    private static readonly double[] bcaHR_Osaka = new double[] { 0.0000, 2.8796, -1.3209, 0.5317, -0.0812 };
    private static readonly double[] acaAT_Osaka = new double[] { 0.3703, 0.0655, -0.0209, 0.0046, -0.0212 };
    private static readonly double[] bcaAT_Osaka = new double[] { 0.0000, 0.0317, 0.0183, 0.0068, -0.0205 };
    private static readonly double[] acFF_Osaka = new double[] { 0.8090, 0.0109, -0.0331, 0.0088, -0.0146 };
    private static readonly double[] bcFF_Osaka = new double[] { 0.0000, 0.0223, 0.0094, 0.0152, -0.0122 };
    private static readonly double[] acCC_Osaka = new double[] { 0.7729, -0.0411, -0.0171, -0.0087, 0.0242 };
    private static readonly double[] bcCC_Osaka = new double[] { 0.0000, -0.0324, 0.0147, -0.0103, 0.0210 };
    private static readonly double[] acDTSD_Osaka = new double[] { 1.3801, 0.1494, 0.0147, 0, 0 };
    private static readonly double[] bcDTSD_Osaka = new double[] { 0.0000, -0.1642, 0.0041, 0, 0 };
    private static readonly double[] acHRSD_Osaka = new double[] { 1.2499, -0.3970, -0.1582, 0, 0 };
    private static readonly double[] bcHRSD_Osaka = new double[] { 0.0000, 0.1771, 0.1389, 0, 0 };

    //日周期フーリエ係数
    private static readonly double[] accDT_Osaka = new double[] { 0.0000, -1.7793, 0.6353, -0.0995 };
    private static readonly double[] bccDT_Osaka = new double[] { 0.0000, 1.6188, -0.2374, -0.1190 };
    private static readonly double[] accHR_Osaka = new double[] { 0.0000, -0.1133, 0.0500, 0.0032 };
    private static readonly double[] bccHR_Osaka = new double[] { 0.0000, 0.2190, -0.0122, 0.0042 };
    private static readonly double[] acCLD_Osaka = new double[] { 0.0040, 0.3535, 0.0000, -0.0401 };
    private static readonly double[] bcCLD_Osaka = new double[] { 0.0000, -0.0277, 0.0000, 0.0049 };
    private static readonly double[] acFAR_Osaka = new double[] { 0.3579, 0.1904, 0.0000, 0.0053 };
    private static readonly double[] bcFAR_Osaka = new double[] { 0.0000, -0.0113, 0.0000, 0.0056 };

    //最小相対湿度
    private static readonly double minimumRelativeHumidity_Osaka = 10;

    #endregion

    #region 札幌

    //トレンド成分標準偏差
    private static readonly double sdevTDT_Sapporo = 0.5686;
    private static readonly double sdevTHR_Sapporo = 0.3078;
    private static readonly double sdevTAT_Sapporo = 0.0241;

    //ARモデル係数
    private static readonly double[] iDTCof_Sapporo = new double[] { 1.1456, 0.1611, -0.1402, -0.0552, -0.0701, -0.0481 };
    private static readonly double[] iHRCof_Sapporo = new double[] { 0.0625, 0.9749, -0.0017, -0.0038, -0.0330, -0.0330 };
    private static readonly double iATCof_Sapporo = 0.7219;
    private static readonly double iDTSD_Sapporo = 0.4664;
    private static readonly double iHRSD_Sapporo = 0.3704;
    private static readonly double iATSD_Sapporo = 0.1025;

    //振幅変調・シフト係数
    private static readonly double shiftDT_Sapporo = 0.723;
    private static readonly double swingDTa_Sapporo = 1.346;
    private static readonly double swingDTb_Sapporo = 1.343;
    private static readonly double shiftHR_Sapporo = -1.613;

    //年周期フーリエ係数
    private static readonly double[] acaDT_Sapporo = new double[] { 8.8674, -11.5445, -0.7236, 0.5122, -0.2416 };
    private static readonly double[] bcaDT_Sapporo = new double[] { 0.0000, 5.3594, -0.4775, 0.5302, -0.2817 };
    private static readonly double[] acaHR_Sapporo = new double[] { 5.8837, -4.3882, 0.5599, 0.2440, -0.2687 };
    private static readonly double[] bcaHR_Sapporo = new double[] { 0.0000, 2.3810, -1.3414, 0.4957, -0.1201 };
    private static readonly double[] acaAT_Sapporo = new double[] { 0.3778, 0.0967, -0.0072, -0.0008, 0.0109 };
    private static readonly double[] bcaAT_Sapporo = new double[] { 0.0000, 0.0038, 0.0028, -0.0116, 0.0003 };
    private static readonly double[] acFF_Sapporo = new double[] { 0.7343, -0.0526, -0.0393, 0.0037, 0.0138 };
    private static readonly double[] bcFF_Sapporo = new double[] { 0.0000, 0.0256, 0.0114, -0.0118, -0.0041 };
    private static readonly double[] acCC_Sapporo = new double[] { 0.7694, -0.0497, 0.0083, 0.0141, 0.0022 };
    private static readonly double[] bcCC_Sapporo = new double[] { 0.0000, -0.0067, -0.0044, 0.0022, 0.0001 };
    private static readonly double[] acDTSD_Sapporo = new double[] { 1.5794, 0.4146, 0.0894, 0, 0 };
    private static readonly double[] bcDTSD_Sapporo = new double[] { 0.0000, -0.0883, 0.1445, 0, 0 };
    private static readonly double[] acHRSD_Sapporo = new double[] { 0.9420, -0.3845, -0.0669, 0, 0 };
    private static readonly double[] bcHRSD_Sapporo = new double[] { 0.0000, 0.3613, -0.0884, 0, 0 };

    //日周期フーリエ係数
    private static readonly double[] accDT_Sapporo = new double[] { 0.0000, -2.0102, 0.6600, -0.0203 };
    private static readonly double[] bccDT_Sapporo = new double[] { 0.0000, 1.2000, -0.0492, -0.1270 };
    private static readonly double[] accHR_Sapporo = new double[] { 0.0000, -0.1166, 0.0264, 0.0067 };
    private static readonly double[] bccHR_Sapporo = new double[] { 0.0000, 0.1300, 0.0315, -0.0058 };
    private static readonly double[] acCLD_Sapporo = new double[] { 0.0772, 0.4520, 0.0662, -0.0249 };
    private static readonly double[] bcCLD_Sapporo = new double[] { 0.0000, 0.0431, 0.0163, -0.0144 };
    private static readonly double[] acFAR_Sapporo = new double[] { 0.3787, 0.2029, 0.0115, 0.0138 };
    private static readonly double[] bcFAR_Sapporo = new double[] { 0.0000, 0.0036, 0.0034, -0.0043 };

    //最小相対湿度
    private static readonly double minimumRelativeHumidity_Sapporo = 10;

    #endregion

    #region 仙台

    //トレンド成分標準偏差
    private static readonly double sdevTDT_Sendai = 0.6328;
    private static readonly double sdevTHR_Sendai = 0.3540;
    private static readonly double sdevTAT_Sendai = 0.0200;

    //ARモデル係数
    private static readonly double[] iDTCof_Sendai = new double[] { 1.0883, 0.1604, -0.0766, -0.0292, -0.0789, -0.0603 };
    private static readonly double[] iHRCof_Sendai = new double[] { 0.0353, 1.0105, 0.0100, -0.0091, -0.0327, -0.0448 };
    private static readonly double iATCof_Sendai = 0.7464;
    private static readonly double iDTSD_Sendai = 0.4915;
    private static readonly double iHRSD_Sendai = 0.3376;
    private static readonly double iATSD_Sendai = 0.0964;

    //振幅変調・シフト係数
    private static readonly double shiftDT_Sendai = 1.794;
    private static readonly double swingDTa_Sendai = 1.284;
    private static readonly double swingDTb_Sendai = 1.304;
    private static readonly double shiftHR_Sendai = -2.182;

    //年周期フーリエ係数
    private static readonly double[] acaDT_Sendai = new double[] { 12.3043, -9.6166, -0.6660, 0.6683, -0.4191 };
    private static readonly double[] bcaDT_Sendai = new double[] { 0.0000, 5.0537, -0.4351, 0.4763, -0.1837 };
    private static readonly double[] acaHR_Sendai = new double[] { 7.4230, -5.1868, 0.4997, 0.4007, -0.3268 };
    private static readonly double[] bcaHR_Sendai = new double[] { 0.0000, 2.9537, -1.4521, 0.4731, -0.0609 };
    private static readonly double[] acaAT_Sendai = new double[] { 0.3801, 0.1609, -0.0026, 0.0128, -0.0174 };
    private static readonly double[] bcaAT_Sendai = new double[] { 0.0000, -0.0168, 0.0156, 0.0043, -0.0167 };
    private static readonly double[] acFF_Sendai = new double[] { 0.7986, 0.0491, -0.0205, 0.0078, -0.0144 };
    private static readonly double[] bcFF_Sendai = new double[] { 0.0000, -0.0169, 0.0182, -0.0022, -0.0056 };
    private static readonly double[] acCC_Sendai = new double[] { 0.7758, -0.1076, -0.0254, -0.0058, 0.0141 };
    private static readonly double[] bcCC_Sendai = new double[] { 0.0000, 0.0066, 0.0007, -0.0130, 0.0040 };
    private static readonly double[] acDTSD_Sendai = new double[] { 1.4371, 0.0498, 0.0356, 0, 0 };
    private static readonly double[] bcDTSD_Sendai = new double[] { 0.0000, -0.1038, 0.1049, 0, 0 };
    private static readonly double[] acHRSD_Sendai = new double[] { 1.1640, -0.5124, -0.1287, 0, 0 };
    private static readonly double[] bcHRSD_Sendai = new double[] { 0.0000, 0.3059, -0.0090, 0, 0 };
    //日周期フーリエ係数
    private static readonly double[] accDT_Sendai = new double[] { 0.0000, -1.9449, 0.7034, -0.0493 };
    private static readonly double[] bccDT_Sendai = new double[] { 0.0000, 1.3603, -0.1132, -0.1271 };
    private static readonly double[] accHR_Sendai = new double[] { 0.0000, -0.0205, 0.0410, -0.0030 };
    private static readonly double[] bccHR_Sendai = new double[] { 0.0000, 0.1717, 0.0334, -0.0134 };
    private static readonly double[] acCLD_Sendai = new double[] { 0.0180, 0.3333, 0.0000, -0.0400 };
    private static readonly double[] bcCLD_Sendai = new double[] { 0.0000, 0.0282, 0.0000, -0.0099 };
    private static readonly double[] acFAR_Sendai = new double[] { 0.3852, 0.2452, 0.0000, 0.0117 };
    private static readonly double[] bcFAR_Sendai = new double[] { 0.0000, 0.0005, 0.0000, 0.0002 };

    //最小相対湿度
    private static readonly double minimumRelativeHumidity_Sendai = 10;

    #endregion

    #region 福岡

    //トレンド成分標準偏差
    private static readonly double sdevTDT_Fukuoka = 0.4559;
    private static readonly double sdevTHR_Fukuoka = 0.2169;
    private static readonly double sdevTAT_Fukuoka = 0.0221;

    //ARモデル係数
    private static readonly double[] iDTCof_Fukuoka = new double[] { 1.1385, 0.0996, -0.1107, -0.0029, -0.0907, -0.0418 };
    private static readonly double[] iHRCof_Fukuoka = new double[] { 0.0264, 0.9199, 0.0160, 0.0400, -0.0145, -0.0172 };
    private static readonly double iATCof_Fukuoka = 0.7344;
    private static readonly double iDTSD_Fukuoka = 0.4749;
    private static readonly double iHRSD_Fukuoka = 0.3547;
    private static readonly double iATSD_Fukuoka = 0.0871;

    //振幅変調・シフト係数
    private static readonly double shiftDT_Fukuoka = -0.162;
    private static readonly double swingDTa_Fukuoka = 1.324;
    private static readonly double swingDTb_Fukuoka = 1.290;
    private static readonly double shiftHR_Fukuoka = -3.323;

    //年周期フーリエ係数
    private static readonly double[] acaDT_Fukuoka = new double[] { 16.8268, -9.3956, -0.3928, 0.3107, -0.2057 };
    private static readonly double[] bcaDT_Fukuoka = new double[] { 0.0000, 4.4420, -0.5665, 0.5255, -0.3072 };
    private static readonly double[] acaHR_Fukuoka = new double[] { 9.4201, -6.0004, 0.5700, 0.1399, -0.0428 };
    private static readonly double[] bcaHR_Fukuoka = new double[] { 0.0000, 2.8796, -1.3209, 0.5317, -0.0812 };
    private static readonly double[] acaAT_Fukuoka = new double[] { 0.3703, 0.0655, -0.0209, 0.0046, -0.0212 };
    private static readonly double[] bcaAT_Fukuoka = new double[] { 0.0000, 0.0317, 0.0183, 0.0068, -0.0205 };
    private static readonly double[] acFF_Fukuoka = new double[] { 0.8090, 0.0109, -0.0331, 0.0088, -0.0146 };
    private static readonly double[] bcFF_Fukuoka = new double[] { 0.0000, 0.0223, 0.0094, 0.0152, -0.0122 };
    private static readonly double[] acCC_Fukuoka = new double[] { 0.7729, -0.0411, -0.0171, -0.0087, 0.0242 };
    private static readonly double[] bcCC_Fukuoka = new double[] { 0.0000, -0.0324, 0.0147, -0.0103, 0.0210 };
    private static readonly double[] acDTSD_Fukuoka = new double[] { 1.3338, 0.1684, 0.1002, 0, 0 };
    private static readonly double[] bcDTSD_Fukuoka = new double[] { 0.0000, -0.2047, 0.0312, 0, 0 };
    private static readonly double[] acHRSD_Fukuoka = new double[] { 1.2744, -0.1366, -0.1652, 0, 0 };
    private static readonly double[] bcHRSD_Fukuoka = new double[] { 0.0000, 0.1132, 0.1945, 0, 0 };

    //日周期フーリエ係数
    private static readonly double[] accDT_Fukuoka = new double[] { 0.0000, -1.7793, 0.6353, -0.0995 };
    private static readonly double[] bccDT_Fukuoka = new double[] { 0.0000, 1.6188, -0.2374, -0.1190 };
    private static readonly double[] accHR_Fukuoka = new double[] { 0.0000, -0.1133, 0.0500, 0.0032 };
    private static readonly double[] bccHR_Fukuoka = new double[] { 0.0000, 0.2190, -0.0122, 0.0042 };
    private static readonly double[] acCLD_Fukuoka = new double[] { 0.0040, 0.3535, 0.0000, -0.0401 };
    private static readonly double[] bcCLD_Fukuoka = new double[] { 0.0000, -0.0277, 0.0000, 0.0049 };
    private static readonly double[] acFAR_Fukuoka = new double[] { 0.3579, 0.1904, 0.0000, 0.0053 };
    private static readonly double[] bcFAR_Fukuoka = new double[] { 0.0000, -0.0113, 0.0000, 0.0056 };

    //最小相対湿度
    private static readonly double minimumRelativeHumidity_Fukuoka = 10;

    #endregion

    #region 那覇

    //トレンド成分標準偏差
    private static readonly double sdevTDT_Naha = 0.4332;
    private static readonly double sdevTHR_Naha = 0.3588;
    private static readonly double sdevTAT_Naha = 0.0210;

    //ARモデル係数
    private static readonly double[] iDTCof_Naha = new double[] { 0.9343, 0.0676, -0.0137, 0.0114, -0.0049, 0.0145 };
    private static readonly double[] iHRCof_Naha = new double[] { -0.0082, 0.7498, 0.0229, 0.1372, 0.0045, 0.0637 };
    private static readonly double iATCof_Naha = 0.7364;
    private static readonly double iDTSD_Naha = 0.6282;
    private static readonly double iHRSD_Naha = 0.3404;
    private static readonly double iATSD_Naha = 0.1009;

    //振幅変調・シフト係数
    private static readonly double shiftDT_Naha = 1.899;
    private static readonly double swingDTa_Naha = 0.582;
    private static readonly double swingDTb_Naha = 0.811;
    private static readonly double shiftHR_Naha = -3.168;

    //年周期フーリエ係数
    private static readonly double[] acaDT_Naha = new double[] { 22.9623, -5.3214, -0.2766, -0.1536, 0.1923 };
    private static readonly double[] bcaDT_Naha = new double[] { 0.0000, 2.9532, 0.0588, 0.1686, -0.0156 };
    private static readonly double[] acaHR_Naha = new double[] { 13.8874, -5.6110, 0.0933, -0.0870, 0.1813 };
    private static readonly double[] bcaHR_Naha = new double[] { 0.0000, 2.1287, -0.3409, 0.0453, 0.1980 };
    private static readonly double[] acaAT_Naha = new double[] { 0.3407, 0.0017, 0.0121, -0.0082, 0.0056 };
    private static readonly double[] bcaAT_Naha = new double[] { 0.0000, 0.0724, 0.0061, 0.0043, -0.0122 };
    private static readonly double[] acFF_Naha = new double[] { 0.7991, -0.0288, -0.0036, 0.0057, 0.0024 };
    private static readonly double[] bcFF_Naha = new double[] { 0.0000, 0.0727, -0.0097, 0.0093, -0.0065 };
    private static readonly double[] acCC_Naha = new double[] { 0.7882, 0.0221, -0.0010, 0.0026, -0.0179 };
    private static readonly double[] bcCC_Naha = new double[] { 0.0000, -0.0490, 0.0134, -0.0110, 0.0071 };
    private static readonly double[] acDTSD_Naha = new double[] { 0.8363, 0.2361, 0.0041, 0, 0 };
    private static readonly double[] bcDTSD_Naha = new double[] { 0.0000, -0.1779, -0.0364, 0, 0 };
    private static readonly double[] acHRSD_Naha = new double[] { 1.9009, 0.3594, -0.4965, 0, 0 };
    private static readonly double[] bcHRSD_Naha = new double[] { 0.0000, -0.3534, 0.4481, 0, 0 };

    //日周期フーリエ係数
    private static readonly double[] accDT_Naha = new double[] { 0.0000, -1.1943, 0.4402, -0.0188 };
    private static readonly double[] bccDT_Naha = new double[] { 0.0000, 0.7588, -0.1384, -0.0821 };
    private static readonly double[] accHR_Naha = new double[] { 0.0000, -0.1003, 0.0246, 0.0117 };
    private static readonly double[] bccHR_Naha = new double[] { 0.0000, -0.0056, 0.0006, 0.0013 };
    private static readonly double[] acCLD_Naha = new double[] { 0.0626, 0.3517, 0.0000, -0.0314 };
    private static readonly double[] bcCLD_Naha = new double[] { 0.0000, -0.0491, 0.0000, 0.0113 };
    private static readonly double[] acFAR_Naha = new double[] { 0.3428, 0.2665, 0.0000, 0.0165 };
    private static readonly double[] bcFAR_Naha = new double[] { 0.0000, -0.0426, 0.0000, -0.0061 };

    //最小相対湿度
    private static readonly double minimumRelativeHumidity_Naha = 30;

    #endregion

    #endregion

    #region コンストラクタ

    /// <summary>
    /// Initializes a new instance for the specified location and random seed.
    /// </summary>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <param name="location">Calculation location.</param>
    public RandomWeather(uint seed, Location location)
    {
      _iDTCof = new double[6];
      _iHRCof = new double[6];
      _acaDT = new double[5];
      _bcaDT = new double[5];
      _acaHR = new double[5];
      _bcaHR = new double[5];
      _acaAT = new double[5];
      _bcaAT = new double[5];
      _acFF = new double[5];
      _bcFF = new double[5];
      _acCC = new double[5];
      _bcCC = new double[5];
      _acDTSD = new double[5];
      _bcDTSD = new double[5];
      _acHRSD = new double[5];
      _bcHRSD = new double[5];

      _accDT = new double[4];
      _bccDT = new double[4];
      _accHR = new double[4];
      _bccHR = new double[4];
      _acCLD = new double[4];
      _bcCLD = new double[4];
      _acFAR = new double[4];
      _bcFAR = new double[4];

      //係数初期化
      InitializeParameters(location);

      _rnd = new MersenneTwister(seed);
      _nRnd = new NormalRandom(seed);
    }

    #endregion

    #region メイン処理

    /// <summary>
    /// Generates stochastic weather data for the specified number of years (non-leap year).
    /// </summary>
    /// <param name="year">Number of years to generate.</param>
    /// <param name="dryBulbTemperature">Output: dry-bulb temperature [°C]</param>
    /// <param name="humidityRatio">Output: humidity ratio [g/kg]</param>
    /// <param name="radiation">Output: global horizontal irradiance [W/m²]</param>
    /// <param name="isFair">Output: true if fair weather (cloud cover &lt; 10)</param>
    public void MakeWeather
      (int year, out double[] dryBulbTemperature,
      out double[] humidityRatio, out double[] radiation, out bool[] isFair)
    {
      MakeWeather(year, false, out dryBulbTemperature, out humidityRatio, out radiation, out isFair);
    }

    /// <summary>
    /// Generates stochastic weather data for the specified number of years.
    /// </summary>
    /// <param name="year">Number of years to generate.</param>
    /// <param name="isLeapYear">If true, each year has 366 days.</param>
    /// <param name="dryBulbTemperature">Output: dry-bulb temperature [°C]</param>
    /// <param name="humidityRatio">Output: humidity ratio [g/kg]</param>
    /// <param name="radiation">Output: global horizontal irradiance [W/m²]</param>
    /// <param name="isFair">Output: true if fair weather (cloud cover &lt; 10)</param>
    public void MakeWeather
      (int year, bool isLeapYear, out double[] dryBulbTemperature,
      out double[] humidityRatio, out double[] radiation, out bool[] isFair)
    {
      DateTime dt = new DateTime(2001, 1, 1, 0, 0, 0);
      int days = isLeapYear ? 366 : 365;

      int totalDay = year * days;
      int totalHour = totalDay * 24;
      dryBulbTemperature = new double[totalHour];
      humidityRatio = new double[totalHour];
      radiation = new double[totalHour];
      double[] swing = new double[totalDay];
      double[] dbtRnd = new double[totalDay];
      double[] hrtRnd = new double[totalDay];
      isFair = new bool[totalHour];

      //トレンド成分を計算
      double[] trendDT = new double[year];
      double[] trendHR = new double[year];
      double[] trendAT = new double[year];
      for (int i = 0; i < year; i++)
      {
        trendDT[i] = _nRnd.NextDouble() * _sdevTDT;
        trendHR[i] = _nRnd.NextDouble() * _sdevTHR;
        trendAT[i] = _nRnd.NextDouble() * _sdevTAT;
      }

      //確定的年周期成分を計算
      double[] caDBT, caHRT, caATM, caFTF, caCTC, caDTSIG, caHRSIG;
      MakeAnnualData(out caDBT, out caHRT, out caATM, out caFTF, out caCTC, out caDTSIG, out caHRSIG, isLeapYear);
      caATM = InterpolateDailyData(caATM);
      caFTF = InterpolateDailyData(caFTF);
      caCTC = InterpolateDailyData(caCTC);
      caDTSIG = InterpolateDailyData(caDTSIG);
      caHRSIG = InterpolateDailyData(caHRSIG);

      //確定的日周期成分を計算
      double[] ccDBT, ccHRT, ccATMF, ccATMC;
      MakeCircadianData(out ccDBT, out ccHRT, out ccATMF, out ccATMC);

      //水平面全天日射の計算
      int tHour = 0;
      int dOfY = days * 24;
      for (int i = 0; i < totalDay; i++)
      {
        int yHour = tHour % dOfY;
        int cYear = i / days;

        //晴れ曇りの状態をマルコフ連鎖で計算
        for (int j = 0; j < 8; j++)
        {
          int ch = tHour + 3 * j;
          //0時点の状態は不変分布から計算
          if (ch == 0)
            isFair[0] = isFair[1] = isFair[2] =
              ((1 - caCTC[yHour]) / (2 - caFTF[yHour] - caCTC[yHour])) < _rnd.NextDouble();
          //その他の時点の状態は推移確率から計算
          else
          {
            bool curF = isFair[ch - 1];
            if (curF) isFair[ch] = _rnd.NextDouble() < caFTF[yHour];
            else isFair[ch] = caCTC[yHour] < _rnd.NextDouble();
          }
          isFair[ch + 2] = isFair[ch + 1] = isFair[ch];
        }

        //不規則変動成分の計算
        double[] iATM = new double[24];
        //時点0の場合には助走計算
        if (i == 0)
        {
          for (int j = 0; j < 50; j++)
          {
            iATM[23] = iATM[23] * _iATCof + _nRnd.NextDouble() * _iATSD;
            iATM[23] = iATM[23] * _iATCof + _nRnd.NextDouble() * _iATSD;
          }
        }
        iATM[0] = iATM[23] * _iATCof + _nRnd.NextDouble() * _iATSD;
        iATM[1] = iATM[0] * _iATCof + _nRnd.NextDouble() * _iATSD;
        for (int j = 1; j < 12; j++)
        {
          iATM[j * 2] = iATM[j * 2 - 1] * _iATCof + _nRnd.NextDouble() * _iATSD;
          iATM[j * 2 + 1] = iATM[j * 2] * _iATCof + _nRnd.NextDouble() * _iATSD;
        }

        //確定成分と不規則変動成分を集計
        int nSum = 0;
        double rSum = 0;
        for (int j = 0; j < 24; j++)
        {
          int ch = tHour + j;
          _sun.Update(dt.AddHours(j));
          if (0 < _sun.Altitude)
          {
            nSum++;
            double idisF = (1 - caCTC[yHour]) / (2 - caFTF[yHour] - caCTC[yHour]);
            rSum -= idisF * ccATMF[j] + (1 - idisF) * ccATMC[j];
            if (isFair[ch])
            {
              radiation[ch] = trendAT[cYear] + caATM[yHour] + iATM[j] + ccATMF[j];
              rSum += iATM[j] + ccATMF[j];
            }
            else
            {
              radiation[ch] = trendAT[cYear] + caATM[yHour] + iATM[j] + ccATMC[j];
              rSum += iATM[j] + ccATMC[j];
            }

            //大気透過率から水平面全天日射を計算（渡辺の式）
            radiation[ch] = Math.Min(1.0, Math.Max(0.001, radiation[ch]));
            radiation[ch] = GetGlobalHorizontalRadiation(radiation[ch], _sun);
          }
          else radiation[ch] = 0;
        }
        //不規則成分で振幅変調および絶対値シフト
        rSum /= nSum;
        dbtRnd[i] = caDBT[i % days] + rSum * _shiftDT;
        hrtRnd[i] = caHRT[i % days] + rSum * _shiftHR;
        swing[i] = rSum * _swingDTa + _swingDTb;

        dt = dt.AddDays(1);
        tHour += 24;
      }

      //振幅変調・絶対値シフトデータを時刻別データにSpline補間
      dbtRnd = InterpolateDailyData(dbtRnd);
      hrtRnd = InterpolateDailyData(hrtRnd);
      swing = InterpolateDailyData(swing);

      //VARモデル助走計算
      double[] idbt = new double[3];
      double[] ihrt = new double[3];
      for (int i = 0; i < 100; i++)
        UpdateRandomComponent(_nRnd, ref idbt, ref ihrt);

      //確定成分と不規則変動成分を合成
      tHour = 0;
      for (int i = 0; i < totalDay; i++)
      {
        int yHour = tHour % dOfY;
        int cYear = i / days;

        for (int j = 0; j < 24; j++)
        {
          int ch = tHour + j;
          UpdateRandomComponent(_nRnd, ref idbt, ref ihrt);
          dryBulbTemperature[ch] = trendDT[cYear] +
            idbt[idbt.Length - 1] * caDTSIG[yHour] + dbtRnd[ch] + ccDBT[j] * swing[ch];
          humidityRatio[ch] = trendHR[cYear] + hrtRnd[ch] + ccHRT[j];
          double hrtMax = MoistAir.GetSaturationHumidityRatioFromDryBulbTemperature
            (dryBulbTemperature[ch], PhysicsConstants.StandardAtmosphericPressure) * 1000 - humidityRatio[ch];
          double hrtMin =
            MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
            (dryBulbTemperature[ch], _minRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure) * 1000;
          hrtMin -= humidityRatio[ch];
          ihrt[2] = (Math.Max(Math.Min(ihrt[2] * caHRSIG[yHour], hrtMax), hrtMin));
          ihrt[2] /= caHRSIG[yHour];
          humidityRatio[ch] += ihrt[2] * caHRSIG[yHour];
        }
        tHour += 24;
      }
    }

    /// <summary>
    /// Generates stochastic weather data and returns it as a
    /// <see cref="Popolo.Core.Climate.Weather.WeatherData"/> instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a thin wrapper over
    /// <see cref="MakeWeather(int, bool, out double[], out double[], out double[], out bool[])"/>
    /// that packages the generated hourly arrays into a
    /// <see cref="Popolo.Core.Climate.Weather.WeatherData"/> with records
    /// starting at <paramref name="startDate"/> at 1-hour intervals.
    /// </para>
    /// <para>
    /// The generated <c>WeatherRecord</c> instances have the following fields:
    /// <see cref="Popolo.Core.Climate.Weather.WeatherField.DryBulbTemperature"/>,
    /// <see cref="Popolo.Core.Climate.Weather.WeatherField.HumidityRatio"/>, and
    /// <see cref="Popolo.Core.Climate.Weather.WeatherField.GlobalHorizontalRadiation"/>.
    /// The model does not generate wind, cloud-cover, or atmospheric
    /// pressure fields, so those remain missing.
    /// </para>
    /// <para>
    /// <see cref="Popolo.Core.Climate.Weather.WeatherData.Source"/> is set to
    /// <see cref="Popolo.Core.Climate.Weather.WeatherDataSource.Generated"/>.
    /// <see cref="Popolo.Core.Climate.Weather.WeatherData.NominalInterval"/>
    /// is set to 1 hour.
    /// </para>
    /// </remarks>
    /// <param name="startDate">The logical start time of the first record.</param>
    /// <param name="years">Number of years to generate.</param>
    /// <param name="isLeapYear">If <c>true</c>, each year is 366 days; otherwise 365.</param>
    /// <returns>A fully-populated <c>WeatherData</c>.</returns>
    public Popolo.Core.Climate.Weather.WeatherData Generate(
        DateTime startDate, int years, bool isLeapYear = false)
    {
      if (years <= 0)
        throw new Popolo.Core.Exceptions.PopoloArgumentException(
            "years must be a positive integer.", nameof(years));

      MakeWeather(years, isLeapYear,
          out double[] dbt, out double[] hr, out double[] rad, out bool[] _);

      var data = new Popolo.Core.Climate.Weather.WeatherData
      {
        Source = Popolo.Core.Climate.Weather.WeatherDataSource.Generated,
        NominalInterval = TimeSpan.FromHours(1),
      };

      var builder = new Popolo.Core.Climate.Weather.WeatherRecordBuilder();
      for (int i = 0; i < dbt.Length; i++)
      {
        builder.Reset();
        builder.SetTime(startDate.AddHours(i));
        builder.SetDryBulbTemperature(dbt[i]);
        builder.SetHumidityRatio(hr[i]);
        builder.SetGlobalHorizontalRadiation(rad[i]);
        data.Add(builder.ToRecord());
      }

      return data;
    }

    #endregion

    #region 周期成分の計算

    /// <summary>Computes daily annual cycle component arrays.</summary>
    /// <param name="dryBulbTemperature">Annual cycle of dry-bulb temperature [°C]</param>
    /// <param name="humidityRatio">Annual cycle of humidity ratio [g/kg]</param>
    /// <param name="atmTransmissivity">Annual cycle of atmospheric transmissivity [-]</param>
    /// <param name="fairToFair">Transition probability: fair → fair [-]</param>
    /// <param name="cloudToCloud">Transition probability: cloudy → cloudy [-]</param>
    /// <param name="dbtSigma">Annual cycle of dry-bulb temperature irregular component std. dev.</param>
    /// <param name="hrtSigma">Annual cycle of humidity ratio irregular component std. dev.</param>
    /// <param name="isLeapYear">If true, 366 days are used.</param>
    private void MakeAnnualData
      (out double[] dryBulbTemperature, out double[] humidityRatio, out double[] atmTransmissivity,
      out double[] fairToFair, out double[] cloudToCloud, out double[] dbtSigma, out double[] hrtSigma, bool isLeapYear)
    {
      //うるう年の場合には366日
      int days = isLeapYear ? 366 : 365;

      //日別年周期データを作成
      dryBulbTemperature = new double[days];
      humidityRatio = new double[days];
      atmTransmissivity = new double[days];
      fairToFair = new double[days];
      cloudToCloud = new double[days];
      dbtSigma = new double[days];
      hrtSigma = new double[days];
      for (int i = 0; i < _acaDT.Length; i++)
      {
        double wk = 2d * Math.PI * (double)i / days;
        for (int j = 0; j < days; j++)
        {
          double cwk = Math.Cos(wk * j);
          double swk = Math.Sin(wk * j);
          dryBulbTemperature[j] += _acaDT[i] * cwk - _bcaDT[i] * swk;
          humidityRatio[j] += _acaHR[i] * cwk - _bcaHR[i] * swk;
          atmTransmissivity[j] += _acaAT[i] * cwk - _bcaAT[i] * swk;
          fairToFair[j] += _acFF[i] * cwk - _bcFF[i] * swk;
          cloudToCloud[j] += _acCC[i] * cwk - _bcCC[i] * swk;
          dbtSigma[j] += _acDTSD[i] * cwk - _bcDTSD[i] * swk;
          hrtSigma[j] += _acHRSD[i] * cwk - _bcHRSD[i] * swk;
        }
      }
    }

    /// <summary>Computes hourly circadian cycle component arrays.</summary>
    /// <param name="dryBulbTemperature">Circadian cycle of dry-bulb temperature [°C]</param>
    /// <param name="humidityRatio">Circadian cycle of humidity ratio [g/kg]</param>
    /// <param name="atmTransFair">Circadian cycle of atmospheric transmissivity on fair days [-]</param>
    /// <param name="atmTransCloudy">Circadian cycle of atmospheric transmissivity on cloudy days [-]</param>
    private void MakeCircadianData
      (out double[] dryBulbTemperature, out double[] humidityRatio,
       out double[] atmTransFair, out double[] atmTransCloudy)
    {
      //時刻別データを作成
      dryBulbTemperature = new double[24];
      humidityRatio = new double[24];
      atmTransCloudy = new double[24];
      atmTransFair = new double[24];
      for (int i = 0; i < _accDT.Length; i++)
      {
        double wk = 2d * Math.PI * i / 24d;
        for (int j = 0; j < 24; j++)
        {
          double cwk = Math.Cos(wk * j);
          double swk = Math.Sin(wk * j);
          dryBulbTemperature[j] += _accDT[i] * cwk - _bccDT[i] * swk;
          humidityRatio[j] += _accHR[i] * cwk - _bccHR[i] * swk;
          atmTransCloudy[j] += _acCLD[i] * cwk - _bcCLD[i] * swk;
          atmTransFair[j] += _acFAR[i] * cwk - _bcFAR[i] * swk;
        }
      }
    }

    #endregion

    #region 不規則変動成分の計算

    /// <summary>Advances the VAR model by one step to update the irregular components.</summary>
    /// <param name="nRnd">Normal random number generator.</param>
    /// <param name="dryBulbTemperature">Standardized irregular component of dry-bulb temperature (lag buffer).</param>
    /// <param name="humidityRatio">Standardized irregular component of humidity ratio (lag buffer).</param>
    private void UpdateRandomComponent
      (NormalRandom nRnd, ref double[] dryBulbTemperature, ref double[] humidityRatio)
    {
      double nrnd1 = nRnd.NextDouble();
      double nrnd2 = nRnd.NextDouble();
      double dbt = nrnd1 * _iDTSD +
        dryBulbTemperature[2] * _iDTCof[0] + humidityRatio[2] * _iDTCof[1] +
        dryBulbTemperature[1] * _iDTCof[2] + humidityRatio[1] * _iDTCof[3] +
        dryBulbTemperature[0] * _iDTCof[4] + humidityRatio[0] * _iDTCof[5];
      double hrt = nrnd2 * _iHRSD +
        dryBulbTemperature[2] * _iHRCof[0] + humidityRatio[2] * _iHRCof[1] +
        dryBulbTemperature[1] * _iHRCof[2] + humidityRatio[1] * _iHRCof[3] +
        dryBulbTemperature[0] * _iHRCof[4] + humidityRatio[0] * _iHRCof[5];
      dryBulbTemperature[0] = dryBulbTemperature[1];
      dryBulbTemperature[1] = dryBulbTemperature[2];
      dryBulbTemperature[2] = dbt;
      humidityRatio[0] = humidityRatio[1];
      humidityRatio[1] = humidityRatio[2];
      humidityRatio[2] = hrt;
    }

    #endregion

    #region その他の計算

    /// <summary>Initializes model parameters for the specified location.</summary>
    /// <param name="location">Calculation location.</param>
    private void InitializeParameters(Location location)
    {
      switch (location)
      {
        case Location.Osaka:
          _sun = new Sun(34, 40.7, 0, 135, 31.3, 0, 135, 0, 0);

          //トレンド成分標準偏差
          _sdevTDT = sdevTDT_Osaka;
          _sdevTHR = sdevTHR_Osaka;
          _sdevTAT = sdevTAT_Osaka;

          //ARモデル係数
          iDTCof_Osaka.CopyTo(_iDTCof, 0);
          iHRCof_Osaka.CopyTo(_iHRCof, 0);
          _iATCof = iATCof_Osaka;
          _iDTSD = iDTSD_Osaka;
          _iHRSD = iHRSD_Osaka;
          _iATSD = iATSD_Osaka;

          //振幅変調・シフト係数
          _shiftDT = shiftDT_Osaka;
          _swingDTa = swingDTa_Osaka;
          _swingDTb = swingDTb_Osaka;
          _shiftHR = -shiftHR_Osaka;

          //年周期フーリエ係数
          acaDT_Osaka.CopyTo(_acaDT, 0);
          bcaDT_Osaka.CopyTo(_bcaDT, 0);
          acaHR_Osaka.CopyTo(_acaHR, 0);
          bcaHR_Osaka.CopyTo(_bcaHR, 0);
          acaAT_Osaka.CopyTo(_acaAT, 0);
          bcaAT_Osaka.CopyTo(_bcaAT, 0);
          acFF_Osaka.CopyTo(_acFF, 0);
          bcFF_Osaka.CopyTo(_bcFF, 0);
          acCC_Osaka.CopyTo(_acCC, 0);
          bcCC_Osaka.CopyTo(_bcCC, 0);
          acDTSD_Osaka.CopyTo(_acDTSD, 0);
          bcDTSD_Osaka.CopyTo(_bcDTSD, 0);
          acHRSD_Osaka.CopyTo(_acHRSD, 0);
          bcHRSD_Osaka.CopyTo(_bcHRSD, 0);

          //日周期フーリエ係数
          accDT_Osaka.CopyTo(_accDT, 0);
          bccDT_Osaka.CopyTo(_bccDT, 0);
          accHR_Osaka.CopyTo(_accHR, 0);
          bccHR_Osaka.CopyTo(_bccHR, 0);
          acCLD_Osaka.CopyTo(_acCLD, 0);
          bcCLD_Osaka.CopyTo(_bcCLD, 0);
          acFAR_Osaka.CopyTo(_acFAR, 0);
          bcFAR_Osaka.CopyTo(_bcFAR, 0);

          //最小相対湿度
          _minRelativeHumidity = minimumRelativeHumidity_Osaka;
          break;
        case Location.Sapporo:
          _sun = new Sun(43, 3.5, 0, 141, 19.9, 0, 135, 0, 0);

          //トレンド成分標準偏差
          _sdevTDT = sdevTDT_Sapporo;
          _sdevTHR = sdevTHR_Sapporo;
          _sdevTAT = sdevTAT_Sapporo;

          //ARモデル係数
          iDTCof_Sapporo.CopyTo(_iDTCof, 0);
          iHRCof_Sapporo.CopyTo(_iHRCof, 0);
          _iATCof = iATCof_Sapporo;
          _iDTSD = iDTSD_Sapporo;
          _iHRSD = iHRSD_Sapporo;
          _iATSD = iATSD_Sapporo;

          //振幅変調・シフト係数
          _shiftDT = shiftDT_Sapporo;
          _swingDTa = swingDTa_Sapporo;
          _swingDTb = swingDTb_Sapporo;
          _shiftHR = -shiftHR_Sapporo;

          //年周期フーリエ係数
          acaDT_Sapporo.CopyTo(_acaDT, 0);
          bcaDT_Sapporo.CopyTo(_bcaDT, 0);
          acaHR_Sapporo.CopyTo(_acaHR, 0);
          bcaHR_Sapporo.CopyTo(_bcaHR, 0);
          acaAT_Sapporo.CopyTo(_acaAT, 0);
          bcaAT_Sapporo.CopyTo(_bcaAT, 0);
          acFF_Sapporo.CopyTo(_acFF, 0);
          bcFF_Sapporo.CopyTo(_bcFF, 0);
          acCC_Sapporo.CopyTo(_acCC, 0);
          bcCC_Sapporo.CopyTo(_bcCC, 0);
          acDTSD_Sapporo.CopyTo(_acDTSD, 0);
          bcDTSD_Sapporo.CopyTo(_bcDTSD, 0);
          acHRSD_Sapporo.CopyTo(_acHRSD, 0);
          bcHRSD_Sapporo.CopyTo(_bcHRSD, 0);

          //日周期フーリエ係数
          accDT_Sapporo.CopyTo(_accDT, 0);
          bccDT_Sapporo.CopyTo(_bccDT, 0);
          accHR_Sapporo.CopyTo(_accHR, 0);
          bccHR_Sapporo.CopyTo(_bccHR, 0);
          acCLD_Sapporo.CopyTo(_acCLD, 0);
          bcCLD_Sapporo.CopyTo(_bcCLD, 0);
          acFAR_Sapporo.CopyTo(_acFAR, 0);
          bcFAR_Sapporo.CopyTo(_bcFAR, 0);

          //最小相対湿度
          _minRelativeHumidity = minimumRelativeHumidity_Sapporo;
          break;
        case Location.Sendai:
          _sun = new Sun(38, 15.5, 0, 140, 54.0, 0, 135, 0, 0);

          //トレンド成分標準偏差
          _sdevTDT = sdevTDT_Sendai;
          _sdevTHR = sdevTHR_Sendai;
          _sdevTAT = sdevTAT_Sendai;

          //ARモデル係数
          iDTCof_Sendai.CopyTo(_iDTCof, 0);
          iHRCof_Sendai.CopyTo(_iHRCof, 0);
          _iATCof = iATCof_Sendai;
          _iDTSD = iDTSD_Sendai;
          _iHRSD = iHRSD_Sendai;
          _iATSD = iATSD_Sendai;

          //振幅変調・シフト係数
          _shiftDT = shiftDT_Sendai;
          _swingDTa = swingDTa_Sendai;
          _swingDTb = swingDTb_Sendai;
          _shiftHR = -shiftHR_Sendai;

          //年周期フーリエ係数
          acaDT_Sendai.CopyTo(_acaDT, 0);
          bcaDT_Sendai.CopyTo(_bcaDT, 0);
          acaHR_Sendai.CopyTo(_acaHR, 0);
          bcaHR_Sendai.CopyTo(_bcaHR, 0);
          acaAT_Sendai.CopyTo(_acaAT, 0);
          bcaAT_Sendai.CopyTo(_bcaAT, 0);
          acFF_Sendai.CopyTo(_acFF, 0);
          bcFF_Sendai.CopyTo(_bcFF, 0);
          acCC_Sendai.CopyTo(_acCC, 0);
          bcCC_Sendai.CopyTo(_bcCC, 0);
          acDTSD_Sendai.CopyTo(_acDTSD, 0);
          bcDTSD_Sendai.CopyTo(_bcDTSD, 0);
          acHRSD_Sendai.CopyTo(_acHRSD, 0);
          bcHRSD_Sendai.CopyTo(_bcHRSD, 0);

          //日周期フーリエ係数
          accDT_Sendai.CopyTo(_accDT, 0);
          bccDT_Sendai.CopyTo(_bccDT, 0);
          accHR_Sendai.CopyTo(_accHR, 0);
          bccHR_Sendai.CopyTo(_bccHR, 0);
          acCLD_Sendai.CopyTo(_acCLD, 0);
          bcCLD_Sendai.CopyTo(_bcCLD, 0);
          acFAR_Sendai.CopyTo(_acFAR, 0);
          bcFAR_Sendai.CopyTo(_bcFAR, 0);

          //最小相対湿度
          _minRelativeHumidity = minimumRelativeHumidity_Sendai;
          break;
        case Location.Fukuoka:
          _sun = new Sun(33, 34.8, 0, 130, 22.6, 0, 135, 0, 0);

          //トレンド成分標準偏差
          _sdevTDT = sdevTDT_Fukuoka;
          _sdevTHR = sdevTHR_Fukuoka;
          _sdevTAT = sdevTAT_Fukuoka;

          //ARモデル係数
          iDTCof_Fukuoka.CopyTo(_iDTCof, 0);
          iHRCof_Fukuoka.CopyTo(_iHRCof, 0);
          _iATCof = iATCof_Fukuoka;
          _iDTSD = iDTSD_Fukuoka;
          _iHRSD = iHRSD_Fukuoka;
          _iATSD = iATSD_Fukuoka;

          //振幅変調・シフト係数
          _shiftDT = shiftDT_Fukuoka;
          _swingDTa = swingDTa_Fukuoka;
          _swingDTb = swingDTb_Fukuoka;
          _shiftHR = -shiftHR_Fukuoka;

          //年周期フーリエ係数
          acaDT_Fukuoka.CopyTo(_acaDT, 0);
          bcaDT_Fukuoka.CopyTo(_bcaDT, 0);
          acaHR_Fukuoka.CopyTo(_acaHR, 0);
          bcaHR_Fukuoka.CopyTo(_bcaHR, 0);
          acaAT_Fukuoka.CopyTo(_acaAT, 0);
          bcaAT_Fukuoka.CopyTo(_bcaAT, 0);
          acFF_Fukuoka.CopyTo(_acFF, 0);
          bcFF_Fukuoka.CopyTo(_bcFF, 0);
          acCC_Fukuoka.CopyTo(_acCC, 0);
          bcCC_Fukuoka.CopyTo(_bcCC, 0);
          acDTSD_Fukuoka.CopyTo(_acDTSD, 0);
          bcDTSD_Fukuoka.CopyTo(_bcDTSD, 0);
          acHRSD_Fukuoka.CopyTo(_acHRSD, 0);
          bcHRSD_Fukuoka.CopyTo(_bcHRSD, 0);

          //日周期フーリエ係数
          accDT_Fukuoka.CopyTo(_accDT, 0);
          bccDT_Fukuoka.CopyTo(_bccDT, 0);
          accHR_Fukuoka.CopyTo(_accHR, 0);
          bccHR_Fukuoka.CopyTo(_bccHR, 0);
          acCLD_Fukuoka.CopyTo(_acCLD, 0);
          bcCLD_Fukuoka.CopyTo(_bcCLD, 0);
          acFAR_Fukuoka.CopyTo(_acFAR, 0);
          bcFAR_Fukuoka.CopyTo(_bcFAR, 0);

          //最小相対湿度
          _minRelativeHumidity = minimumRelativeHumidity_Fukuoka;
          break;
        case Location.Naha:
          _sun = new Sun(26, 12.2, 0, 127, 41.3, 0, 135, 0, 0);

          //トレンド成分標準偏差
          _sdevTDT = sdevTDT_Naha;
          _sdevTHR = sdevTHR_Naha;
          _sdevTAT = sdevTAT_Naha;

          //ARモデル係数
          iDTCof_Naha.CopyTo(_iDTCof, 0);
          iHRCof_Naha.CopyTo(_iHRCof, 0);
          _iATCof = iATCof_Naha;
          _iDTSD = iDTSD_Naha;
          _iHRSD = iHRSD_Naha;
          _iATSD = iATSD_Naha;

          //振幅変調・シフト係数
          _shiftDT = shiftDT_Naha;
          _swingDTa = swingDTa_Naha;
          _swingDTb = swingDTb_Naha;
          _shiftHR = -shiftHR_Naha;

          //年周期フーリエ係数
          acaDT_Naha.CopyTo(_acaDT, 0);
          bcaDT_Naha.CopyTo(_bcaDT, 0);
          acaHR_Naha.CopyTo(_acaHR, 0);
          bcaHR_Naha.CopyTo(_bcaHR, 0);
          acaAT_Naha.CopyTo(_acaAT, 0);
          bcaAT_Naha.CopyTo(_bcaAT, 0);
          acFF_Naha.CopyTo(_acFF, 0);
          bcFF_Naha.CopyTo(_bcFF, 0);
          acCC_Naha.CopyTo(_acCC, 0);
          bcCC_Naha.CopyTo(_bcCC, 0);
          acDTSD_Naha.CopyTo(_acDTSD, 0);
          bcDTSD_Naha.CopyTo(_bcDTSD, 0);
          acHRSD_Naha.CopyTo(_acHRSD, 0);
          bcHRSD_Naha.CopyTo(_bcHRSD, 0);

          //日周期フーリエ係数
          accDT_Naha.CopyTo(_accDT, 0);
          bccDT_Naha.CopyTo(_bccDT, 0);
          accHR_Naha.CopyTo(_accHR, 0);
          bccHR_Naha.CopyTo(_bccHR, 0);
          acCLD_Naha.CopyTo(_acCLD, 0);
          bcCLD_Naha.CopyTo(_bcCLD, 0);
          acFAR_Naha.CopyTo(_acFAR, 0);
          bcFAR_Naha.CopyTo(_bcFAR, 0);

          //最小相対湿度
          _minRelativeHumidity = minimumRelativeHumidity_Naha;
          break;
        default://東京
          _sun = new Sun(35, 41.2, 0, 139, 45.9, 0, 135, 0, 0);

          //トレンド成分標準偏差
          _sdevTDT = sdevTDT_Tokyo;
          _sdevTHR = sdevTHR_Tokyo;
          _sdevTAT = sdevTAT_Tokyo;

          //ARモデル係数
          iDTCof_Tokyo.CopyTo(_iDTCof, 0);
          iHRCof_Tokyo.CopyTo(_iHRCof, 0);
          _iATCof = iATCof_Tokyo;
          _iDTSD = iDTSD_Tokyo;
          _iHRSD = iHRSD_Tokyo;
          _iATSD = iATSD_Tokyo;

          //振幅変調・シフト係数
          _shiftDT = shiftDT_Tokyo;
          _swingDTa = swingDTa_Tokyo;
          _swingDTb = swingDTb_Tokyo;
          _shiftHR = -shiftHR_Tokyo;

          //年周期フーリエ係数
          acaDT_Tokyo.CopyTo(_acaDT, 0);
          bcaDT_Tokyo.CopyTo(_bcaDT, 0);
          acaHR_Tokyo.CopyTo(_acaHR, 0);
          bcaHR_Tokyo.CopyTo(_bcaHR, 0);
          acaAT_Tokyo.CopyTo(_acaAT, 0);
          bcaAT_Tokyo.CopyTo(_bcaAT, 0);
          acFF_Tokyo.CopyTo(_acFF, 0);
          bcFF_Tokyo.CopyTo(_bcFF, 0);
          acCC_Tokyo.CopyTo(_acCC, 0);
          bcCC_Tokyo.CopyTo(_bcCC, 0);
          acDTSD_Tokyo.CopyTo(_acDTSD, 0);
          bcDTSD_Tokyo.CopyTo(_bcDTSD, 0);
          acHRSD_Tokyo.CopyTo(_acHRSD, 0);
          bcHRSD_Tokyo.CopyTo(_bcHRSD, 0);

          //日周期フーリエ係数
          accDT_Tokyo.CopyTo(_accDT, 0);
          bccDT_Tokyo.CopyTo(_bccDT, 0);
          accHR_Tokyo.CopyTo(_accHR, 0);
          bccHR_Tokyo.CopyTo(_bccHR, 0);
          acCLD_Tokyo.CopyTo(_acCLD, 0);
          bcCLD_Tokyo.CopyTo(_bcCLD, 0);
          acFAR_Tokyo.CopyTo(_acFAR, 0);
          bcFAR_Tokyo.CopyTo(_bcFAR, 0);

          //最小相対湿度
          _minRelativeHumidity = minimumRelativeHumidity_Tokyo;
          break;
      }
    }

    /// <summary>
    /// Converts daily data to hourly data using cubic spline interpolation.
    /// </summary>
    /// <param name="dailyData">Daily values.</param>
    /// <returns>Hourly values (length = 24 × number of days).</returns>
    private static double[] InterpolateDailyData(double[] dailyData)
    {
      double[] dd = new double[dailyData.Length + 2];
      dailyData.CopyTo(dd, 1);
      dd[0] = dd[dd.Length - 2];
      dd[dd.Length - 1] = dd[1];
      double[] sx = new double[dailyData.Length + 2];
      double[] hx = new double[dailyData.Length * 24];
      for (int i = 0; i < sx.Length; i++) sx[i] = -12 + 24 * i;
      for (int i = 0; i < hx.Length; i++) hx[i] = i;
      double[] cDD = CubicSpline.GetParameters(sx, dd);
      return CubicSpline.Interpolate(sx, dd, cDD, hx);
    }

    /// <summary>
    /// Gets the global horizontal irradiance [W/m²] from the atmospheric transmissivity
    /// using the Watanabe method.
    /// </summary>
    /// <param name="aTransmissivity">Atmospheric transmissivity [-]</param>
    /// <param name="sun">Solar state at the calculation site.</param>
    private static double GetGlobalHorizontalRadiation
      (double aTransmissivity, IReadOnlySun sun)
    {
      double sinAltitude = Math.Sin(sun.Altitude);
      double exRadiation = sun.GetExtraterrestrialRadiation();
      double ps = Math.Pow(aTransmissivity, 1 / sinAltitude);
      double directNormalRadiation = exRadiation * ps;
      double q = (0.8672 + 0.7505 * sinAltitude) *
        Math.Pow(aTransmissivity, 0.421 / sinAltitude) *
        Math.Pow(1d - Math.Pow(aTransmissivity, 1 / sinAltitude), 2.277);
      double diffuseHorizontalRadiation = exRadiation * sinAltitude * (q / (1 + q));
      return directNormalRadiation * sinAltitude + diffuseHorizontalRadiation;
    }

    #endregion

  }
}
