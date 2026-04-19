/* Sun.cs
 *
 * Copyright (C) 2008 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using System;
using System.Collections.Generic;
using Popolo.Core.Numerics;
using Popolo.Core.Physics;

namespace Popolo.Core.Climate
{
  /// <inheritdoc cref="IReadOnlySun"/>
  /// <remarks>
  /// <para>
  /// This is the mutable implementation of <see cref="IReadOnlySun"/>.
  /// Construct a <see cref="Sun"/> either with explicit latitude / longitude /
  /// standard-meridian values, or with a <see cref="City"/> enum entry whose
  /// coordinates are looked up from a built-in table of major world cities.
  /// After construction, advance time with <c>Update</c>; the solar altitude,
  /// azimuth, and equation-of-time correction are recomputed from the date,
  /// time, and site coordinates.
  /// </para>
  /// <para>
  /// Irradiance is typically supplied from weather data through the mutable
  /// <c>DirectNormalRadiation</c> / <c>DiffuseHorizontalRadiation</c> /
  /// <c>GlobalHorizontalRadiation</c> setters (the class does not enforce the
  /// GHI ≈ DNI · sin(altitude) + DHI relationship — it is the caller's
  /// responsibility to keep the three consistent). When irradiance is not
  /// available directly, the <c>SeparationMethod</c> enum selects among
  /// published atmospheric models (Berlage, Akasaka, Erbs, etc.) for
  /// estimating the direct / diffuse split from global horizontal values
  /// and atmospheric transmissivity.
  /// </para>
  /// <para>
  /// Illuminance is optional and recomputed on each update only when
  /// <see cref="IReadOnlySun.CalculateIlluminance"/> is true; the conversion
  /// uses <see cref="SolarLuminousEfficacy"/> as a fixed luminous-efficacy
  /// estimate.
  /// </para>
  /// <para>
  /// References:
  /// <list type="bullet">
  ///   <item><description>Shukuya, M., "Light and Heat in the Architectural Environment — Numerical Approaches," Maruzen, 1993, p. 20.</description></item>
  ///   <item><description>Udagawa, M., "Air Conditioning Calculations with Personal Computers," 1986.</description></item>
  /// </list>
  /// </para>
  /// </remarks>
  public class Sun : IReadOnlySun
  {

    #region 定数

    /// <summary>Solar constant [W/m²].</summary>
    public const double SolarConstant = 1367.0;

    /// <summary>Luminous efficacy of solar radiation [lm/W].</summary>
    public const double SolarLuminousEfficacy = 93.9;

    /// <summary>Conversion factor from degrees to radians.</summary>
    private const double DegToRad = Math.PI / 180.0;

    #endregion

    #region 列挙型

    /// <summary>
    /// Specifies the method used for direct/diffuse radiation separation.
    /// </summary>
    public enum SeparationMethod
    {
      /// <summary>
      /// Berlage method.
      /// Berlage, H.P.: Zur Theorie der Beleuchtung einer horizontalen Flache durch Tageslicht,
      /// Meteorologische Zeitschrift, May 1928, pp.174-180.
      /// </summary>
      Berlage,

      /// <summary>
      /// Matsuo method.
      /// Matsuo, H.: Journal of AIJ, On Solar Radiation on Clear Days, No.2, pp.21-24, 1960.
      /// </summary>
      Matsuo,

      /// <summary>
      /// Nagata method.
      /// Nagata, T.: A Proposed Formula for Diffuse Solar Radiation on Clear Skies,
      /// AIJ Annual Conference, 1978.
      /// </summary>
      Nagata,

      /// <summary>
      /// Liu-Jordan method.
      /// Liu, B.Y.H., Jordan, R.C.: The interrelationship and characteristic distribution of
      /// direct, diffuse and total solar radiation, Solar Energy, Vol.4, No.3, 1960.
      /// </summary>
      LiuJordan,

      /// <summary>
      /// Udagawa method.
      /// Udagawa, M., Kimura, K.: Estimation of direct solar radiation from observed global
      /// radiation, Journal of AIJ, No.267, pp.83-90, 1978.
      /// </summary>
      Udagawa,

      /// <summary>
      /// Watanabe method.
      /// Watanabe, T.: Separation of global horizontal radiation and estimation of tilted surface
      /// radiation, Journal of AIJ, No.330, pp.96-108, 1983.
      /// </summary>
      Watanabe,

      /// <summary>
      /// Akasaka method.
      /// Akasaka, H.: Model of circumsolar radiation and diffuse sky radiation including cloudy sky,
      /// ISES Solar World Congress, 1991.
      /// </summary>
      Akasaka,

      /// <summary>
      /// Miki method.
      /// Miki, N.: Study on separation of standard weather data, AIJ Annual Conference,
      /// pp.857-858, 1991.
      /// </summary>
      Miki,

      /// <summary>
      /// Erbs method.
      /// Erbs, D.G., Klein, S.A., Duffie, J.A.: Estimation of the diffuse radiation fraction
      /// for hourly, daily and monthly-average global radiation,
      /// Solar Energy, Vol.28, No.4, pp.293-302, 1982.
      /// </summary>
      Erbs
    }

    /// <summary>
    /// Specifies a city for initializing the solar calculation site.
    /// </summary>
    public enum City
    {
      /// <summary>Aberdeen</summary>
      Aberdeen,
      /// <summary>Algiers</summary>
      Algiers,
      /// <summary>Amsterdam</summary>
      Amsterdam,
      /// <summary>Ankara</summary>
      Ankara,
      /// <summary>Asuncion</summary>
      Asuncion,
      /// <summary>Athens</summary>
      Athens,
      /// <summary>Auckland</summary>
      Auckland,
      /// <summary>Bangkok</summary>
      Bangkok,
      /// <summary>Barcelona</summary>
      Barcelona,
      /// <summary>Beijing</summary>
      Beijing,
      /// <summary>Belem</summary>
      Belem,
      /// <summary>Belfast</summary>
      Belfast,
      /// <summary>Belgrade</summary>
      Belgrade,
      /// <summary>Berlin</summary>
      Berlin,
      /// <summary>Birmingham</summary>
      Birmingham,
      /// <summary>Bogota</summary>
      Bogota,
      /// <summary>Bordeaux</summary>
      Bordeaux,
      /// <summary>Bremen</summary>
      Bremen,
      /// <summary>Brisbane</summary>
      Brisbane,
      /// <summary>Bristol</summary>
      Bristol,
      /// <summary>Brussels</summary>
      Brussels,
      /// <summary>Bucharest</summary>
      Bucharest,
      /// <summary>Budapest</summary>
      Budapest,
      /// <summary>Buenos Aires</summary>
      BuenosAires,
      /// <summary>Cairo</summary>
      Cairo,
      /// <summary>Canton</summary>
      Canton,
      /// <summary>Cape Town</summary>
      CapeTown,
      /// <summary>Caracas</summary>
      Caracas,
      /// <summary>Cayenne</summary>
      Cayenne,
      /// <summary>Chihuahua</summary>
      Chihuahua,
      /// <summary>Chongqing</summary>
      Chongqing,
      /// <summary>Copenhagen</summary>
      Copenhagen,
      /// <summary>Cordoba</summary>
      Cordoba,
      /// <summary>Dakar</summary>
      Dakar,
      /// <summary>Djibouti</summary>
      Djibouti,
      /// <summary>Dublin</summary>
      Dublin,
      /// <summary>Durban</summary>
      Durban,
      /// <summary>Edinburgh</summary>
      Edinburgh,
      /// <summary>Frankfurt</summary>
      Frankfurt,
      /// <summary>Georgetown</summary>
      Georgetown,
      /// <summary>Glasgow</summary>
      Glasgow,
      /// <summary>Guatemala City</summary>
      GuatemalaCity,
      /// <summary>Guayaquil</summary>
      Guayaquil,
      /// <summary>Hamburg</summary>
      Hamburg,
      /// <summary>Hammerfest</summary>
      Hammerfest,
      /// <summary>Havana</summary>
      Havana,
      /// <summary>Helsinki</summary>
      Helsinki,
      /// <summary>Hobart</summary>
      Hobart,
      /// <summary>Hong Kong</summary>
      HongKong,
      /// <summary>Iquique</summary>
      Iquique,
      /// <summary>Irkutsk</summary>
      Irkutsk,
      /// <summary>Jakarta</summary>
      Jakarta,
      /// <summary>Johannesburg</summary>
      Johannesburg,
      /// <summary>Kingston</summary>
      Kingston,
      /// <summary>Kinshasa</summary>
      Kinshasa,
      /// <summary>Kuala Lumpur</summary>
      KualaLumpur,
      /// <summary>La Paz</summary>
      LaPaz,
      /// <summary>Leeds</summary>
      Leeds,
      /// <summary>Lima</summary>
      Lima,
      /// <summary>Lisbon</summary>
      Lisbon,
      /// <summary>Liverpool</summary>
      Liverpool,
      /// <summary>London</summary>
      London,
      /// <summary>Lyons</summary>
      Lyons,
      /// <summary>Madrid</summary>
      Madrid,
      /// <summary>Manchester</summary>
      Manchester,
      /// <summary>Manila</summary>
      Manila,
      /// <summary>Marseilles</summary>
      Marseilles,
      /// <summary>Mazatlan</summary>
      Mazatlan,
      /// <summary>Mecca</summary>
      Mecca,
      /// <summary>Melbourne</summary>
      Melbourne,
      /// <summary>Mexico City</summary>
      MexicoCity,
      /// <summary>Milan</summary>
      Milan,
      /// <summary>Montevideo</summary>
      Montevideo,
      /// <summary>Moscow</summary>
      Moscow,
      /// <summary>Munich</summary>
      Munich,
      /// <summary>Nagasaki</summary>
      Nagasaki,
      /// <summary>Nagoya</summary>
      Nagoya,
      /// <summary>Nairobi</summary>
      Nairobi,
      /// <summary>Nanjing</summary>
      Nanjing,
      /// <summary>Naples</summary>
      Naples,
      /// <summary>Newcastle-on-Tyne</summary>
      NewcastleOnTyne,
      /// <summary>Odessa</summary>
      Odessa,
      /// <summary>Osaka</summary>
      Osaka,
      /// <summary>Oslo</summary>
      Oslo,
      /// <summary>Panama City</summary>
      PanamaCity,
      /// <summary>Paramaribo</summary>
      Paramaribo,
      /// <summary>Paris</summary>
      Paris,
      /// <summary>Perth</summary>
      Perth,
      /// <summary>Plymouth</summary>
      Plymouth,
      /// <summary>Port Moresby</summary>
      PortMoresby,
      /// <summary>Prague</summary>
      Prague,
      /// <summary>Reykjavik</summary>
      Reykjavík,
      /// <summary>Rio de Janeiro</summary>
      RioDeJaneiro,
      /// <summary>Rome</summary>
      Rome,
      /// <summary>Salvador</summary>
      Salvador,
      /// <summary>Santiago</summary>
      Santiago,
      /// <summary>St. Petersburg</summary>
      StPetersburg,
      /// <summary>São Paulo</summary>
      SaoPaulo,
      /// <summary>Shanghai</summary>
      Shanghai,
      /// <summary>Singapore</summary>
      Singapore,
      /// <summary>Sofia</summary>
      Sofia,
      /// <summary>Stockholm</summary>
      Stockholm,
      /// <summary>Sydney</summary>
      Sydney,
      /// <summary>Tananarive</summary>
      Tananarive,
      /// <summary>Tokyo</summary>
      Tokyo,
      /// <summary>Tripoli</summary>
      Tripoli,
      /// <summary>Venice</summary>
      Venice,
      /// <summary>Veracruz</summary>
      Veracruz,
      /// <summary>Vienna</summary>
      Vienna,
      /// <summary>Vladivostok</summary>
      Vladivostok,
      /// <summary>Warsaw</summary>
      Warsaw,
      /// <summary>Wellington</summary>
      Wellington,
      /// <summary>Zurich</summary>
      Zurich
    }

    #endregion

    #region クラス変数

    /// <summary>Lookup of city locations (latitude, longitude, standard-time longitude).</summary>
    private static readonly Dictionary<City, double[]> _cities = new Dictionary<City, double[]>();

    #endregion

    #region インスタンス変数

    /// <summary>Direct normal irradiance [W/m²].</summary>
    private double _directNormalRadiation;

    /// <summary>Diffuse horizontal irradiance [W/m²].</summary>
    private double _diffuseHorizontalRadiation;

    /// <summary>Global horizontal irradiance [W/m²].</summary>
    private double _globalHorizontalRadiation;

    #endregion

    #region プロパティ

    /// <summary>Gets or sets the solar altitude angle [radian].</summary>
    public double Altitude { get; set; }

    /// <summary>Gets or sets the solar azimuth angle [radian].</summary>
    public double Azimuth { get; set; }

    /// <summary>Gets the latitude of the calculation site (positive north) [degree].</summary>
    public double Latitude { get; private set; }

    /// <summary>Gets the longitude of the calculation site (positive east) [degree].</summary>
    public double Longitude { get; private set; }

    /// <summary>Gets the longitude of the standard time meridian (positive east) [degree].</summary>
    public double StandardLongitude { get; private set; }

    /// <summary>Gets or sets the direct normal irradiance (DNI) [W/m²].</summary>
    public double DirectNormalRadiation
    {
      get => _directNormalRadiation;
      set => _directNormalRadiation = Math.Max(0, value);
    }

    /// <summary>Gets or sets the diffuse horizontal irradiance (DHI) [W/m²].</summary>
    public double DiffuseHorizontalRadiation
    {
      get => _diffuseHorizontalRadiation;
      set => _diffuseHorizontalRadiation = Math.Max(0, value);
    }

    /// <summary>Gets or sets the global horizontal irradiance (GHI) [W/m²].</summary>
    public double GlobalHorizontalRadiation
    {
      get => _globalHorizontalRadiation;
      set => _globalHorizontalRadiation = Math.Max(0, value);
    }

    /// <summary>Gets the current date and time.</summary>
    public DateTime CurrentDateTime { get; private set; }

    /// <summary>Gets the direct normal illuminance [lx].</summary>
    public double DirectNormalIlluminance { get; private set; }

    /// <summary>Gets the diffuse horizontal illuminance [lx].</summary>
    public double DiffuseIlluminance { get; private set; }

    /// <summary>Gets the global horizontal illuminance [lx].</summary>
    public double GlobalHorizontalIlluminance { get; private set; }

    /// <summary>Gets or sets a value indicating whether illuminance calculation is enabled.</summary>
    public bool CalculateIlluminance { get; set; } = false;

    #endregion

    #region 静的コンストラクタ・コンストラクタ

    /// <summary>Static constructor that initializes the city location data.</summary>
    static Sun()
    {
      _cities.Add(City.Aberdeen, new double[] { 57.15, -2.15, 0 });
      _cities.Add(City.Algiers, new double[] { 36.83, 3.00, 15 });
      _cities.Add(City.Amsterdam, new double[] { 52.37, 4.88, 15 });
      _cities.Add(City.Ankara, new double[] { 39.92, 32.92, 30 });
      _cities.Add(City.Asuncion, new double[] { -25.25, -57.67, -60 });
      _cities.Add(City.Athens, new double[] { 37.97, 23.72, 30 });
      _cities.Add(City.Auckland, new double[] { -36.87, 174.75, 180 });
      _cities.Add(City.Bangkok, new double[] { 13.75, 100.50, -75 });
      _cities.Add(City.Barcelona, new double[] { 41.38, 2.15, 15 });
      _cities.Add(City.Beijing, new double[] { 39.92, 116.42, 120 });
      _cities.Add(City.Belem, new double[] { -1.47, -48.48, -45 });
      _cities.Add(City.Belfast, new double[] { 54.62, -5.93, 0 });
      _cities.Add(City.Belgrade, new double[] { 44.87, 20.53, 15 });
      _cities.Add(City.Berlin, new double[] { 52.50, 13.42, 15 });
      _cities.Add(City.Birmingham, new double[] { 52.42, -1.92, 0 });
      _cities.Add(City.Bogota, new double[] { 4.53, -74.25, -75 });
      _cities.Add(City.Bordeaux, new double[] { 44.83, -0.52, 15 });
      _cities.Add(City.Bremen, new double[] { 53.08, 8.82, 15 });
      _cities.Add(City.Brisbane, new double[] { -27.48, 153.13, 150 });
      _cities.Add(City.Bristol, new double[] { 51.47, -2.58, 0 });
      _cities.Add(City.Brussels, new double[] { 50.87, 4.37, 15 });
      _cities.Add(City.Bucharest, new double[] { 44.42, 26.12, 30 });
      _cities.Add(City.Budapest, new double[] { 47.50, 19.08, 15 });
      _cities.Add(City.BuenosAires, new double[] { -34.58, -58.37, -45 });
      _cities.Add(City.Cairo, new double[] { 30.03, 31.35, 30 });
      _cities.Add(City.Canton, new double[] { 23.12, 113.25, 120 });
      _cities.Add(City.CapeTown, new double[] { -33.92, 18.37, 30 });
      _cities.Add(City.Caracas, new double[] { 10.47, -67.03, -60 });
      _cities.Add(City.Cayenne, new double[] { 4.82, -52.30, -45 });
      _cities.Add(City.Chihuahua, new double[] { 28.62, -106.08, -105 });
      _cities.Add(City.Chongqing, new double[] { 29.77, 106.57, 120 });
      _cities.Add(City.Copenhagen, new double[] { 55.67, 12.57, 15 });
      _cities.Add(City.Cordoba, new double[] { -31.47, -64.17, -45 });
      _cities.Add(City.Dakar, new double[] { 14.67, -17.47, 0 });
      _cities.Add(City.Djibouti, new double[] { 11.50, 43.05, 45 });
      _cities.Add(City.Dublin, new double[] { 53.33, -6.25, 0 });
      _cities.Add(City.Durban, new double[] { -29.88, 30.88, 30 });
      _cities.Add(City.Edinburgh, new double[] { 55.92, -3.17, 0 });
      _cities.Add(City.Frankfurt, new double[] { 50.12, 8.68, 15 });
      _cities.Add(City.Georgetown, new double[] { 6.75, -58.25, -60 });
      _cities.Add(City.Glasgow, new double[] { 55.83, -4.25, 0 });
      _cities.Add(City.GuatemalaCity, new double[] { 14.62, -90.52, -90 });
      _cities.Add(City.Guayaquil, new double[] { -2.17, -79.93, -75 });
      _cities.Add(City.Hamburg, new double[] { 53.55, 10.03, 15 });
      _cities.Add(City.Hammerfest, new double[] { 70.63, 23.63, 15 });
      _cities.Add(City.Havana, new double[] { 23.13, -82.38, -75 });
      _cities.Add(City.Helsinki, new double[] { 60.17, 25.00, 30 });
      _cities.Add(City.Hobart, new double[] { -42.87, 147.32, 150 });
      _cities.Add(City.HongKong, new double[] { 22.33, 114.18, 120 });
      _cities.Add(City.Iquique, new double[] { -20.17, -70.12, -60 });
      _cities.Add(City.Irkutsk, new double[] { 52.50, 104.33, 120 });
      _cities.Add(City.Jakarta, new double[] { -6.27, 106.80, 105 });
      _cities.Add(City.Johannesburg, new double[] { -26.20, 28.07, 30 });
      _cities.Add(City.Kingston, new double[] { 17.98, -76.82, -75 });
      _cities.Add(City.Kinshasa, new double[] { -4.30, 15.28, 15 });
      _cities.Add(City.KualaLumpur, new double[] { 3.13, 101.70, 120 });
      _cities.Add(City.LaPaz, new double[] { -16.45, -68.37, -60 });
      _cities.Add(City.Leeds, new double[] { 53.75, -1.50, 0 });
      _cities.Add(City.Lima, new double[] { -12.00, -77.03, -75 });
      _cities.Add(City.Lisbon, new double[] { 38.73, -9.15, 0 });
      _cities.Add(City.Liverpool, new double[] { 53.42, -3.00, 0 });
      _cities.Add(City.London, new double[] { 51.53, -0.08, 0 });
      _cities.Add(City.Lyons, new double[] { 45.75, 4.83, 15 });
      _cities.Add(City.Madrid, new double[] { 40.43, -3.70, 15 });
      _cities.Add(City.Manchester, new double[] { 53.50, -2.25, 0 });
      _cities.Add(City.Manila, new double[] { 14.58, 120.95, 120 });
      _cities.Add(City.Marseilles, new double[] { 43.33, 5.33, 15 });
      _cities.Add(City.Mazatlan, new double[] { 23.20, -106.42, -105 });
      _cities.Add(City.Mecca, new double[] { 21.48, 39.75, 45 });
      _cities.Add(City.Melbourne, new double[] { -37.78, 144.97, 150 });
      _cities.Add(City.MexicoCity, new double[] { 19.43, -99.12, -90 });
      _cities.Add(City.Milan, new double[] { 45.45, 9.17, 15 });
      _cities.Add(City.Montevideo, new double[] { -34.88, -56.17, -45 });
      _cities.Add(City.Moscow, new double[] { 55.75, 37.60, 45 });
      _cities.Add(City.Munich, new double[] { 48.13, 11.58, 15 });
      _cities.Add(City.Nagasaki, new double[] { 32.80, 129.95, 135 });
      _cities.Add(City.Nagoya, new double[] { 35.12, 136.93, 135 });
      _cities.Add(City.Nairobi, new double[] { -1.42, 36.92, 45 });
      _cities.Add(City.Nanjing, new double[] { 32.05, 118.88, 120 });
      _cities.Add(City.Naples, new double[] { 40.83, 14.25, 15 });
      _cities.Add(City.NewcastleOnTyne, new double[] { 54.97, -1.62, 0 });
      _cities.Add(City.Odessa, new double[] { 46.45, 30.80, 30 });
      _cities.Add(City.Osaka, new double[] { 34.53, 135.50, 135 });
      _cities.Add(City.Oslo, new double[] { 59.95, 10.70, 15 });
      _cities.Add(City.PanamaCity, new double[] { 8.97, -79.53, -75 });
      _cities.Add(City.Paramaribo, new double[] { 5.75, -55.25, -45 });
      _cities.Add(City.Paris, new double[] { 48.80, 2.33, 15 });
      _cities.Add(City.Perth, new double[] { -31.95, 115.87, 120 });
      _cities.Add(City.Plymouth, new double[] { 50.42, -4.08, 0 });
      _cities.Add(City.PortMoresby, new double[] { -9.42, 147.13, 150 });
      _cities.Add(City.Prague, new double[] { 50.08, 14.43, 15 });
      _cities.Add(City.Reykjavík, new double[] { 64.07, -21.97, 0 });
      _cities.Add(City.RioDeJaneiro, new double[] { -22.95, -43.20, -45 });
      _cities.Add(City.Rome, new double[] { 41.90, 12.45, 15 });
      _cities.Add(City.Salvador, new double[] { -12.93, -38.45, -45 });
      _cities.Add(City.Santiago, new double[] { -33.47, -70.75, -60 });
      _cities.Add(City.StPetersburg, new double[] { 59.93, 30.30, 45 });
      _cities.Add(City.SaoPaulo, new double[] { -23.52, -46.52, -45 });
      _cities.Add(City.Shanghai, new double[] { 31.17, 121.47, 120 });
      _cities.Add(City.Singapore, new double[] { 1.23, 103.92, 120 });
      _cities.Add(City.Sofia, new double[] { 42.67, 23.33, 30 });
      _cities.Add(City.Stockholm, new double[] { 59.28, 18.05, 15 });
      _cities.Add(City.Sydney, new double[] { -34.00, 151.00, 150 });
      _cities.Add(City.Tananarive, new double[] { -18.83, 47.55, 45 });
      _cities.Add(City.Tokyo, new double[] { 35.67, 139.75, 135 });
      _cities.Add(City.Tripoli, new double[] { 32.95, 13.20, 30 });
      _cities.Add(City.Venice, new double[] { 45.43, 12.33, 15 });
      _cities.Add(City.Veracruz, new double[] { 19.17, -96.17, -90 });
      _cities.Add(City.Vienna, new double[] { 48.23, 16.33, 15 });
      _cities.Add(City.Vladivostok, new double[] { 43.17, 132.00, 150 });
      _cities.Add(City.Warsaw, new double[] { 52.23, 21.00, 15 });
      _cities.Add(City.Wellington, new double[] { -41.28, 174.78, 180 });
      _cities.Add(City.Zurich, new double[] { 47.35, 8.52, 15 });
    }

    /// <summary>
    /// Initializes a new instance with the specified location.
    /// </summary>
    /// <param name="latitude">Latitude of the site (positive north) [degree]</param>
    /// <param name="longitude">Longitude of the site (positive east) [degree]</param>
    /// <param name="standardLongitude">Longitude of the standard time meridian [degree]</param>
    public Sun(double latitude, double longitude, double standardLongitude)
    {
      Latitude = latitude;
      Longitude = longitude;
      StandardLongitude = standardLongitude;
    }

    /// <summary>
    /// Initializes a new instance with the specified location in degrees, minutes, and seconds.
    /// </summary>
    public Sun(
        double latitudeDeg, double latitudeMin, double latitudeSec,
        double longitudeDeg, double longitudeMin, double longitudeSec,
        double standardLongitudeDeg, double standardLongitudeMin, double standardLongitudeSec)
    {
      Latitude = latitudeDeg + latitudeMin / 60.0 + latitudeSec / 3600.0;
      Longitude = longitudeDeg + longitudeMin / 60.0 + longitudeSec / 3600.0;
      StandardLongitude = standardLongitudeDeg + standardLongitudeMin / 60.0
          + standardLongitudeSec / 3600.0;
    }

    /// <summary>
    /// Initializes a new instance using a predefined city location.
    /// </summary>
    /// <param name="city">The city to use as the calculation site.</param>
    public Sun(City city)
    {
      double[] loc = _cities[city];
      Latitude = loc[0];
      Longitude = loc[1];
      StandardLongitude = loc[2];
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>
    /// Updates the solar position for the specified date and time.
    /// </summary>
    /// <param name="dateTime">The date and time for which to calculate the solar position.</param>
    public void Update(DateTime dateTime)
    {
      CurrentDateTime = dateTime;
      GetSunPosition(Latitude, Longitude, StandardLongitude, dateTime,
          out double al, out double or);
      Altitude = al;
      Azimuth = or;
    }

    /// <summary>
    /// Gets the extraterrestrial radiation [W/m²] for the current date.
    /// </summary>
    /// <returns>Extraterrestrial radiation [W/m²]</returns>
    public double GetExtraterrestrialRadiation()
        => GetExtraterrestrialRadiation(CurrentDateTime.DayOfYear);

    /// <summary>
    /// Gets the direct normal irradiance [W/m²] from the atmospheric transmissivity.
    /// </summary>
    /// <param name="atmosphericTransmissivity">Atmospheric transmissivity [-]</param>
    /// <returns>Direct normal irradiance [W/m²]</returns>
    public double GetDirectNormalRadiation(double atmosphericTransmissivity)
        => GetDirectNormalRadiation(Altitude, atmosphericTransmissivity, CurrentDateTime.DayOfYear);

    /// <summary>
    /// Separates the global horizontal radiation into direct and diffuse components
    /// and updates the internal radiation state.
    /// </summary>
    /// <param name="globalHorizontalRadiation">Global horizontal irradiance [W/m²]</param>
    /// <param name="method">The separation method to use.</param>
    public void SeparateGlobalHorizontalRadiation(
        double globalHorizontalRadiation, SeparationMethod method)
    {
      GlobalHorizontalRadiation = globalHorizontalRadiation;
      SeparateGlobalHorizontalRadiation(globalHorizontalRadiation,
          Latitude, Longitude, StandardLongitude, CurrentDateTime, method,
          out _directNormalRadiation, out _diffuseHorizontalRadiation);
      UpdateIlluminance();
    }

    /// <summary>
    /// Sets the direct normal irradiance from global and diffuse components.
    /// </summary>
    /// <param name="globalHorizontalRadiation">Global horizontal irradiance [W/m²]</param>
    /// <param name="diffuseHorizontalRadiation">Diffuse horizontal irradiance [W/m²]</param>
    public void SetDirectNormalRadiation(
        double globalHorizontalRadiation, double diffuseHorizontalRadiation)
    {
      GlobalHorizontalRadiation = globalHorizontalRadiation;
      DiffuseHorizontalRadiation = diffuseHorizontalRadiation;
      DirectNormalRadiation = GetDirectNormalRadiation(
          GlobalHorizontalRadiation, DiffuseHorizontalRadiation, Altitude);
      UpdateIlluminance();
    }

    /// <summary>
    /// Sets the diffuse horizontal irradiance from direct and global components.
    /// </summary>
    /// <param name="directNormalRadiation">Direct normal irradiance [W/m²]</param>
    /// <param name="globalHorizontalRadiation">Global horizontal irradiance [W/m²]</param>
    public void SetDiffuseHorizontalRadiation(
        double directNormalRadiation, double globalHorizontalRadiation)
    {
      GlobalHorizontalRadiation = globalHorizontalRadiation;
      DirectNormalRadiation = directNormalRadiation;
      DiffuseHorizontalRadiation = GetDiffuseHorizontalRadiation(
          DirectNormalRadiation, GlobalHorizontalRadiation, Altitude);
      UpdateIlluminance();
    }

    /// <summary>
    /// Sets the global horizontal irradiance from diffuse and direct components.
    /// </summary>
    /// <param name="diffuseHorizontalRadiation">Diffuse horizontal irradiance [W/m²]</param>
    /// <param name="directNormalRadiation">Direct normal irradiance [W/m²]</param>
    public void SetGlobalHorizontalRadiation(
        double diffuseHorizontalRadiation, double directNormalRadiation)
    {
      DirectNormalRadiation = directNormalRadiation;
      DiffuseHorizontalRadiation = diffuseHorizontalRadiation;
      GlobalHorizontalRadiation = GetGlobalHorizontalRadiation(
          DiffuseHorizontalRadiation, DirectNormalRadiation, Altitude);
      UpdateIlluminance();
    }

    /// <summary>Gets the sunrise time for the current date.</summary>
    /// <returns>Sunrise time</returns>
    public DateTime GetSunRiseTime()
        => GetSunRiseTime(Latitude, Longitude, StandardLongitude, CurrentDateTime);

    /// <summary>Gets the sunset time for the current date.</summary>
    /// <returns>Sunset time</returns>
    public DateTime GetSunSetTime()
        => GetSunSetTime(Latitude, Longitude, StandardLongitude, CurrentDateTime);

    /// <summary>Update Illuminance</summary>
    private void UpdateIlluminance()
    {
      if (!CalculateIlluminance) return;
      DiffuseIlluminance = DiffuseHorizontalRadiation
          * GetDiffuseLuminousEfficacy(Altitude);
      DirectNormalIlluminance = DirectNormalRadiation
          * GetDirectLuminousEfficacy(Altitude, DirectNormalRadiation, CurrentDateTime.DayOfYear);
      GlobalHorizontalIlluminance = DirectNormalIlluminance * Math.Sin(Altitude)
          + DiffuseIlluminance;
    }

    #endregion

    #region 太陽位置関連の静的メソッド

    /// <summary>
    /// Gets the solar altitude [radian] and azimuth [radian] for the specified location and time.
    /// </summary>
    /// <param name="latitude">Latitude (positive north) [degree]</param>
    /// <param name="longitude">Longitude (positive east) [degree]</param>
    /// <param name="standardLongitude">Standard time meridian longitude (positive east) [degree]</param>
    /// <param name="dTime">Date and time</param>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <param name="orientation">Solar azimuth [radian]</param>
    /// <example>
    /// Calculate solar altitude and azimuth for Tokyo (35.7°N, 139.8°E, JST=135°E):
    /// <code>
    /// Sun.GetSunPosition(35.7, 139.8, 135,
    ///     new DateTime(2004, 6, 22, 12, 0, 0), out double alt, out double az);
    /// </code>
    /// </example>
    public static void GetSunPosition(
        double latitude, double longitude, double standardLongitude, DateTime dTime,
        out double altitude, out double orientation)
    {
      double phi = DegToRad * latitude;
      double b = (360.0 * (dTime.DayOfYear - 81) / 365.0) * DegToRad;
      double sd = 0.397949 * Math.Sin(b);
      double cd = Math.Sqrt(1 - sd * sd);
      double e = 0.1645 * Math.Sin(2 * b) - 0.1255 * Math.Cos(b) - 0.025 * Math.Sin(b);
      double taum = dTime.Hour + dTime.Minute / 60.0 + dTime.Second / 3600.0
          + e + (longitude - standardLongitude) / 15.0;
      double t = ((taum - 12) * 15) * DegToRad;

      double sp = Math.Sin(phi);
      double cp = Math.Cos(phi);
      double sh = sp * sd + cp * cd * Math.Cos(t);

      //日の出前・日没後
      if (sh <= 0)
      {
        altitude = 0;
        orientation = 0;
      }
      else
      {
        altitude = Math.Asin(sh);
        double ch = Math.Sqrt(1.0 - sh * sh);
        double ca = (sh * sp - sd) / (ch * cp);
        orientation = Math.Acos(ca);
        orientation *= Math.Sign(t);
      }
    }

    /// <summary>
    /// Gets the solar altitude [radian] for the specified location and time.
    /// </summary>
    public static double GetSunAltitude(
        double latitude, double longitude, double standardLongitude, DateTime dTime)
    {
      GetSunPosition(latitude, longitude, standardLongitude, dTime,
          out double altitude, out _);
      return altitude;
    }

    /// <summary>
    /// Gets the solar azimuth [radian] for the specified location and time.
    /// </summary>
    public static double GetSunAzimuth(
        double latitude, double longitude, double standardLongitude, DateTime dTime)
    {
      GetSunPosition(latitude, longitude, standardLongitude, dTime,
          out _, out double orientation);
      return orientation;
    }

    /// <summary>
    /// Gets the sunset time for the specified location and date.
    /// </summary>
    public static DateTime GetSunSetTime(
        double latitude, double longitude, double standardLongitude, DateTime dTime)
        => GetSunRiseOrSetTime(latitude, longitude, standardLongitude, dTime, true);

    /// <summary>
    /// Gets the sunrise time for the specified location and date.
    /// </summary>
    public static DateTime GetSunRiseTime(
        double latitude, double longitude, double standardLongitude, DateTime dTime)
        => GetSunRiseOrSetTime(latitude, longitude, standardLongitude, dTime, false);

    /// <summary>Computes the sunrise or sunset time.</summary>
    private static DateTime GetSunRiseOrSetTime(
        double latitude, double longitude, double standardLongitude,
        DateTime dTime, bool isSunSet)
    {
      double phi = DegToRad * latitude;
      double b = (360.0 * (dTime.DayOfYear - 81) / 365.0) * DegToRad;
      double sd = 0.397949 * Math.Sin(b);
      double tptd = -Math.Tan(phi) * Math.Tan(Math.Asin(sd));

      //白夜・極夜の場合
      if (tptd < -1 || 1 < tptd)
        return new DateTime(dTime.Year, dTime.Month, dTime.Day, 0, 0, 0);

      double e = 0.1645 * Math.Sin(2 * b) - 0.1255 * Math.Cos(b) - 0.025 * Math.Sin(b);
      double tsr = -Math.Acos(tptd) / DegToRad;
      if (isSunSet) tsr *= -1;
      double tstsr = (tsr - longitude + standardLongitude) / 15 + 12 - e;

      int hour = (int)Math.Truncate(tstsr);
      tstsr = 60 * (tstsr - hour);
      int minute = (int)Math.Truncate(tstsr);
      tstsr = 60 * (tstsr - minute);
      int sec = (int)Math.Truncate(tstsr);
      return new DateTime(dTime.Year, dTime.Month, dTime.Day, hour, minute, sec);
    }

    /// <summary>
    /// Gets the solar declination [radian] for the specified date.
    /// </summary>
    /// <param name="dTime">Date and time</param>
    /// <returns>Solar declination [radian]</returns>
    public static double GetSunDeclination(DateTime dTime)
    {
      double dDeg = 2.0 * Math.PI * dTime.DayOfYear / 365.0;
      return DegToRad * (0.3622133 - 23.24763 * Math.Cos(dDeg + 0.153231)
          - 0.3368908 * Math.Cos(2.0 * dDeg + 0.2070988)
          - 0.1852646 * Math.Cos(3.0 * dDeg + 0.6201293));
    }

    /// <summary>
    /// Gets the equation of time for the specified date.
    /// </summary>
    /// <param name="dTime">Date and time</param>
    /// <returns>Equation of time [minutes]</returns>
    public static double GetEquationOfTime(DateTime dTime)
    {
      double dDeg = 2.0 * Math.PI * dTime.DayOfYear / 365.0;
      return 60.0 * (-0.0002786409 + 0.1227715 * Math.Cos(dDeg + 1.498311)
          - 0.1654575 * Math.Cos(2.0 * dDeg - 1.261546)
          - 0.00535383 * Math.Cos(3.0 * dDeg - 1.1571));
    }

    /// <summary>
    /// Gets the hour angle [radian] for the specified conditions.
    /// </summary>
    /// <param name="equationOfTime">Equation of time [minutes]</param>
    /// <param name="longitude">Longitude of the site [degree]</param>
    /// <param name="standardLongitude">Standard time meridian longitude [degree]</param>
    /// <param name="dTime">Date and time</param>
    /// <returns>Hour angle [radian]</returns>
    /// <remarks>Shukuya, "Light and Heat in the Architectural Environment," p.20</remarks>
    public static double GetHourAngle(
        double equationOfTime, double longitude, double standardLongitude, DateTime dTime)
    {
      double ts = dTime.Hour + dTime.Minute / 60.0;
      return (15.0 * (ts - 12.0) + longitude - standardLongitude + 0.25 * equationOfTime)
          * DegToRad;
    }

    #endregion

    #region 日射相互変換の静的メソッド

    /// <summary>
    /// Gets the direct normal irradiance [W/m²] from global and diffuse components.
    /// </summary>
    /// <param name="globalHorizontalRadiation">Global horizontal irradiance [W/m²]</param>
    /// <param name="diffuseHorizontalRadiation">Diffuse horizontal irradiance [W/m²]</param>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <returns>Direct normal irradiance [W/m²]</returns>
    public static double GetDirectNormalRadiation(
        double globalHorizontalRadiation, double diffuseHorizontalRadiation, double altitude)
        => (globalHorizontalRadiation - diffuseHorizontalRadiation) / Math.Sin(altitude);

    /// <summary>
    /// Gets the diffuse horizontal irradiance [W/m²] from direct and global components.
    /// </summary>
    /// <param name="directNormalRadiation">Direct normal irradiance [W/m²]</param>
    /// <param name="globalHorizontalRadiation">Global horizontal irradiance [W/m²]</param>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <returns>Diffuse horizontal irradiance [W/m²]</returns>
    public static double GetDiffuseHorizontalRadiation(
        double directNormalRadiation, double globalHorizontalRadiation, double altitude)
        => globalHorizontalRadiation - Math.Sin(altitude) * directNormalRadiation;

    /// <summary>
    /// Gets the global horizontal irradiance [W/m²] from diffuse and direct components.
    /// </summary>
    /// <param name="diffuseHorizontalRadiation">Diffuse horizontal irradiance [W/m²]</param>
    /// <param name="directNormalRadiation">Direct normal irradiance [W/m²]</param>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <returns>Global horizontal irradiance [W/m²]</returns>
    public static double GetGlobalHorizontalRadiation(
        double diffuseHorizontalRadiation, double directNormalRadiation, double altitude)
        => directNormalRadiation * Math.Sin(altitude) + diffuseHorizontalRadiation;

    #endregion

    #region 直散分離の静的メソッド

    /// <summary>
    /// Separates global horizontal irradiance [W/m²] into direct normal and diffuse components.
    /// </summary>
    /// <param name="globalHorizontalRadiation">Global horizontal irradiance [W/m²]</param>
    /// <param name="latitude">Latitude [degree]</param>
    /// <param name="longitude">Longitude [degree]</param>
    /// <param name="standardLongitude">Standard time meridian longitude [degree]</param>
    /// <param name="dTime">Date and time</param>
    /// <param name="method">Separation method</param>
    /// <param name="directSolarRadiation">Direct normal irradiance [W/m²]</param>
    /// <param name="diffuseHorizontalRadiation">Diffuse horizontal irradiance [W/m²]</param>
    public static void SeparateGlobalHorizontalRadiation(
        double globalHorizontalRadiation,
        double latitude, double longitude, double standardLongitude,
        DateTime dTime, SeparationMethod method,
        out double directSolarRadiation, out double diffuseHorizontalRadiation)
    {
      double h = GetSunAltitude(latitude, longitude, standardLongitude, dTime);
      if (h <= 0 || globalHorizontalRadiation <= 0)
      {
        directSolarRadiation = diffuseHorizontalRadiation = 0;
        return;
      }
      //誤差拡大を防ぐため太陽高度の下限を3°とする
      h = Math.Max(0.05, h);
      double sinH = Math.Sin(h);
      double io = GetExtraterrestrialRadiation(dTime.DayOfYear);
      double dn, dff;

      //宇田川の手法
      if (method == SeparationMethod.Udagawa)
      {
        double ktt = globalHorizontalRadiation / (io * sinH);
        double ktc = io * sinH * (0.5163 + sinH * (0.333 + 0.00803 * sinH));
        if (ktc <= globalHorizontalRadiation) dn = (-0.43 + 1.43 * ktt) * io;
        else dn = (2.277 + sinH * (-1.258 + 0.2396 * sinH)) * Math.Pow(ktt, 3) * io;
        dff = globalHorizontalRadiation - dn * sinH;
      }
      //Erbsの手法
      else if (method == SeparationMethod.Erbs)
      {
        double ktt = globalHorizontalRadiation / (io * sinH);
        if (ktt < 0.22) dff = globalHorizontalRadiation * (1.0 - 0.09 * ktt);
        else if (ktt < 0.8) dff = globalHorizontalRadiation * (0.9511 + ktt
            * (-0.1604 + ktt * (4.388 + ktt * (-16.638 + 12.336 * ktt))));
        else dff = 0.1651 * globalHorizontalRadiation;
        dn = (globalHorizontalRadiation - dff) / sinH;
      }
      //三木の手法
      else if (method == SeparationMethod.Miki)
      {
        double lkt = Math.Min(1, globalHorizontalRadiation / (io * sinH));
        double skt = (lkt - 0.15 - 0.2 * sinH) / 0.6;
        double skd = skt <= 0 ? 0 : skt * skt * (3 - 2 * skt);
        double lkd = Math.Min(skd * lkt, Math.Pow(0.8, (7 + sinH) / (1 + 7 * sinH)));
        double lks = Math.Max(lkt - lkd, 0.005);
        dn = lkd * io;
        dff = globalHorizontalRadiation - dn * sinH;
      }
      //数値計算による手法（二分法）
      else
      {
        //大気透過率=0で観測値を上回る場合
        GetDirectAndDiffuseRadiation(0, sinH, io, method, out dn, out dff);
        if (globalHorizontalRadiation < dn * sinH + dff)
        {
          directSolarRadiation = 0;
          diffuseHorizontalRadiation = globalHorizontalRadiation;
          return;
        }

        //大気透過率=1で観測値を下回る場合
        GetDirectAndDiffuseRadiation(1, sinH, io, method, out dn, out dff);
        if (dn * sinH + dff < globalHorizontalRadiation)
        {
          double rate = globalHorizontalRadiation / (dn * sinH + dff);
          directSolarRadiation = dn * sinH * rate;
          diffuseHorizontalRadiation = dff * rate;
          return;
        }

        //二分法で大気透過率を収束計算
        Roots.ErrorFunction eFnc = atmTrans =>
        {
          GetDirectAndDiffuseRadiation(atmTrans, sinH, io, method, out dn, out dff);
          return globalHorizontalRadiation - (dn * sinH + dff);
        };
        Roots.Bisection(eFnc, 0, 1, 0.001, 0.00001, 20);
      }
      directSolarRadiation = Math.Max(0, dn);
      diffuseHorizontalRadiation = Math.Max(0, dff);
    }

    /// <summary>
    /// Gets the extraterrestrial radiation [W/m²] for the specified day of year.
    /// </summary>
    /// <param name="daysOfYear">Day of year (1 = Jan 1, 365 = Dec 31)</param>
    /// <returns>Extraterrestrial radiation [W/m²]</returns>
    public static double GetExtraterrestrialRadiation(int daysOfYear)
        => SolarConstant * (1.0 + 0.033 * Math.Cos(2.0 * Math.PI * daysOfYear / 365.0));

    /// <summary>
    /// Gets the direct normal irradiance [W/m²] from altitude, transmissivity, and day of year.
    /// </summary>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <param name="atmosphericTransmissivity">Atmospheric transmissivity [-]</param>
    /// <param name="dayOfYear">Day of year</param>
    /// <returns>Direct normal irradiance [W/m²]</returns>
    public static double GetDirectNormalRadiation(
        double altitude, double atmosphericTransmissivity, int dayOfYear)
    {
      double sinh = Math.Sin(altitude);
      if (sinh <= 0) return 0;
      return GetExtraterrestrialRadiation(dayOfYear)
          * Math.Pow(atmosphericTransmissivity, 1.0 / sinh);
    }

    /// <summary>Computes the direct and diffuse irradiance from the atmospheric transmissivity.</summary>
    private static void GetDirectAndDiffuseRadiation(
        double aTransmissivity, double sinAltitude, double exRadiation,
        SeparationMethod method,
        out double directNormalRadiation, out double diffuseHorizontalRadiation)
    {
      double ps = Math.Pow(aTransmissivity, 1.0 / sinAltitude);
      directNormalRadiation = exRadiation * ps;
      double shi = sinAltitude * (exRadiation - directNormalRadiation);
      diffuseHorizontalRadiation = 0;

      switch (method)
      {
        case SeparationMethod.Akasaka:
          diffuseHorizontalRadiation = shi * 0.95
              * Math.Pow(aTransmissivity, 1.0 / (0.5 + 2.5 * sinAltitude))
              * Math.Pow(1 - aTransmissivity, 2.0 / 3.0);
          break;
        case SeparationMethod.Berlage:
          diffuseHorizontalRadiation = shi * 0.5 / (1 - 1.4 * Math.Log(aTransmissivity));
          break;
        case SeparationMethod.LiuJordan:
          diffuseHorizontalRadiation = sinAltitude * exRadiation * (0.271 - 0.2939 * ps);
          break;
        case SeparationMethod.Matsuo:
          diffuseHorizontalRadiation = shi * (1 - aTransmissivity) * 1.2
              / (1 - 1.4 * Math.Log(aTransmissivity));
          break;
        case SeparationMethod.Nagata:
          diffuseHorizontalRadiation = shi * (0.66 - 0.32 * sinAltitude)
              * (0.5 + (0.4 - 0.3 * aTransmissivity) * sinAltitude);
          break;
        case SeparationMethod.Watanabe:
          double qq = (0.9013 + 1.123 * sinAltitude)
              * Math.Pow(aTransmissivity, 0.489 / sinAltitude)
              * Math.Pow(1 - ps, 2.525);
          diffuseHorizontalRadiation = sinAltitude * exRadiation * qq / (1 + qq);
          break;
      }
    }

    #endregion

    #region 照度関連の静的メソッド

    /// <summary>
    /// Gets the luminous efficacy of diffuse sky radiation [lm/W].
    /// </summary>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <returns>Luminous efficacy [lm/W]</returns>
    public static double GetDiffuseLuminousEfficacy(double altitude)
    {
      double sh = Math.Sin(altitude);
      return SolarLuminousEfficacy * (sh * (sh * (3.375 * sh - 6.175) + 3.4713) + 0.7623);
    }

    /// <summary>
    /// Gets the luminous efficacy of direct normal radiation [lm/W].
    /// </summary>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <param name="directNormalRadiation">Direct normal irradiance [W/m²]</param>
    /// <param name="dayOfYear">Day of year</param>
    /// <returns>Luminous efficacy [lm/W]</returns>
    public static double GetDirectLuminousEfficacy(
        double altitude, double directNormalRadiation, int dayOfYear)
    {
      double sh = Math.Sin(altitude);
      double exRad = GetExtraterrestrialRadiation(dayOfYear);
      return SolarLuminousEfficacy * (
          (sh * (sh * (6.25 * sh - 10) + 3.94)) * directNormalRadiation / exRad
          + 0.983 * sh + 0.451);
    }

    /// <summary>
    /// Estimates the luminous efficacy for global, diffuse, and direct radiation.
    /// </summary>
    /// <param name="diffuseHorizontalRadiation">Diffuse horizontal irradiance [W/m²]</param>
    /// <param name="globalHorizontalRadiation">Global horizontal irradiance [W/m²]</param>
    /// <param name="solarAltitude">Solar altitude [radian]</param>
    /// <param name="elevation">Elevation above sea level [m]</param>
    /// <param name="dewpointTemperature">Dew point temperature [°C]</param>
    /// <param name="dayOfYear">Day of year</param>
    /// <param name="globalEfficacy">Luminous efficacy of global radiation [lm/W]</param>
    /// <param name="diffuseEfficacy">Luminous efficacy of diffuse radiation [lm/W]</param>
    /// <param name="directEfficacy">Luminous efficacy of direct radiation [lm/W]</param>
    /// <remarks>
    /// Igawa, N.: Improving the All Sky Model for the luminance and radiance distributions
    /// of the sky, Solar Energy 105 (2014) 354-372.
    /// </remarks>
    public static void EstimateLuminousEfficacy(
        double diffuseHorizontalRadiation, double globalHorizontalRadiation,
        double solarAltitude, double elevation, double dewpointTemperature, int dayOfYear,
        out double globalEfficacy, out double diffuseEfficacy, out double directEfficacy)
    {
      double[][][] cf = {
                new double[][] {
                    new double[]{ 31.777,-36.903, 20.341},
                    new double[]{-84.690,152.800,-86.306},
                    new double[]{-16.534, 20.942,-20.828},
                    new double[]{ 40.441,-76.504, 45.149},
                    new double[]{-2.7163,  4.023, 0.6567},
                    new double[]{-60.423, 99.559, 45.919}
                },
                new double[][] {
                    new double[]{4.1472, 21.852,-28.685},
                    new double[]{35.775,-42.243, 25.986},
                    new double[]{-4.6244,-2.3053,-6.5705},
                    new double[]{-11.192,-2.8112, 26.243},
                    new double[]{-3.4999, 4.1531, 1.125},
                    new double[]{ 11.216,-13.942, 94.711}
                },
                new double[][] {
                    new double[]{100.750,-287.250,171.560},
                    new double[]{-178.920,321.040,-205.490},
                    new double[]{-17.329,120.470,-95.215},
                    new double[]{141.020,-257.770,151.910},
                    new double[]{-1.5475, 4.2673,-0.3197},
                    new double[]{-302.240, 661.760,-275.270}
                }
            };

      if (globalHorizontalRadiation <= 0 || solarAltitude <= 0)
      {
        globalEfficacy = diffuseEfficacy = directEfficacy = 0;
        return;
      }

      //基準クラウドレイショ
      double ces = 0.08302 + 0.5358 * Math.Exp(-17.394 * solarAltitude)
          + 0.3818 * Math.Exp(-3.2899 * solarAltitude);
      //クラウドレイショ
      double ce = diffuseHorizontalRadiation / globalHorizontalRadiation;
      //澄清指標（0・1は発散するのでクリップ）
      double cle = 1.0 <= ces ? 1.0 : (1.0 - ce) / (1.0 - ces);
      cle = Math.Min(0.99, Math.Max(0.01, cle));

      //大気路程
      double m = MoistAir.GetAtmosphericPressure(elevation) / PhysicsConstants.StandardAtmosphericPressure
          * Math.Sin(solarAltitude);
      double seeg = 0.84 * GetExtraterrestrialRadiation(dayOfYear) / m * Math.Exp(-0.054 * m);
      //晴天指標（0・1は発散するのでクリップ）
      double kc = globalHorizontalRadiation / seeg;
      kc = Math.Min(0.99, Math.Max(0.01, kc));

      //可降水量 [cm]
      double pWat = 0.1 * Sky.GetPrecipitableWater(elevation, dewpointTemperature);

      double[] eff = new double[3];
      for (int i = 0; i < 3; i++)
      {
        double[] af = new double[6];
        for (int j = 0; j < 6; j++)
          af[j] = (cf[i][j][0] * solarAltitude + cf[i][j][1]) * solarAltitude + cf[i][j][2];
        eff[i] = af[0] * kc + af[1] * cle + af[2] * Math.Log(kc)
            + af[3] * Math.Log(cle) + af[4] * pWat + af[5];
      }

      globalEfficacy = Math.Min(683, Math.Max(0.0, eff[0]));
      diffuseEfficacy = Math.Min(683, Math.Max(0.0, eff[1]));
      directEfficacy = Math.Min(683, Math.Max(0.0, eff[2]));
    }

    #endregion

  }
}