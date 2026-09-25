/* WebproToBuildingThermalModel.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;

using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;
using Popolo.Core.Exceptions;

using Popolo.Webpro.Domain;
using Popolo.Webpro.Domain.Enums;
using PopoloOrientation = Popolo.Core.Climate.Incline.Orientation;
using WebproOrientation = Popolo.Webpro.Domain.Enums.Orientation;

namespace Popolo.Webpro.Conversion
{
  /// <summary>
  /// Converts a <see cref="WebproModel"/> into a Popolo.Core
  /// <see cref="BuildingThermalModel"/> suitable for thermal load calculation.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Only rooms listed in <see cref="WebproModel.AirConditionedRoomNames"/>
  /// are converted; unconditioned rooms (corridors, restrooms, etc. that are
  /// not listed in the <c>AirConditioningZone</c> JSON section) are ignored.
  /// </para>
  /// <para>
  /// <b>Structural assumptions</b> (reproducing the legacy Popolo v2.3
  /// WEBPRO reader):
  /// </para>
  /// <list type="bullet">
  ///   <item><description>All air-conditioned rooms share a single <c>MultiRooms</c>.</description></item>
  ///   <item><description>Each zone receives a fixed 6-layer floor construction
  ///     (vinyl / air / concrete / air / gypsum board / rock wool ceiling tile)
  ///     assigned as a loop wall.</description></item>
  ///   <item><description>Interior walls use an adjacent-space factor of
  ///     <see cref="WebproConversionConstants.AdjacentSpaceFactor"/>.</description></item>
  ///   <item><description>Ground walls use
  ///     <see cref="WebproConversionConstants.GroundWallConductance"/>.</description></item>
  /// </list>
  /// <para>
  /// <b>Envelope conventions:</b>
  /// </para>
  /// <list type="bullet">
  ///   <item><description>WEBPRO lists wall layers from the room side outward;
  ///     they are reversed so that Popolo layer 0 is on the outdoor (F) side.</description></item>
  ///   <item><description>Window area = per-window area
  ///     (<c>windowArea</c>, else <c>windowWidth × windowHeight</c>) ×
  ///     <c>WindowNumber</c> (count); the same value is subtracted from the
  ///     gross wall area.</description></item>
  ///   <item><description>Window and U-value-input wall constructions are sized
  ///     so that their U-value with the WEBPRO surface resistances
  ///     (1/10 + 1/20 = 0.15 m²K/W) equals the input U-value.</description></item>
  ///   <item><description>"日の当たらない外壁" is an outdoor-air boundary with zero
  ///     solar absorptance (outdoor air temperature and nocturnal long-wave
  ///     radiation still apply). Windows on such walls are treated like
  ///     windows on sunlit walls (solar is not suppressed).</description></item>
  /// </list>
  /// <para>
  /// <b>Heat gain:</b> Internal heat gain schedules (people, lights, plug
  /// load, ventilation) are <i>not</i> installed by this converter. Callers
  /// that need occupant / lighting schedules should attach
  /// <see cref="WebproHeatGainScheduler"/> instances to individual zones
  /// after conversion, using the <see cref="RoomNameToZone"/> property of
  /// the returned <see cref="ConversionResult"/>.
  /// </para>
  /// </remarks>
  public static class WebproToBuildingThermalModel
  {

    /// <summary>
    /// Sum of the indoor and outdoor surface resistances assumed by WEBPRO
    /// U-values [m²·K/W] (1/10 + 1/20 = 0.15).
    /// </summary>
    /// <remarks>
    /// Subtracted from 1/U to obtain the resistance of the construction
    /// itself (window glazing + air gap, or U-value-input wall layers).
    /// </remarks>
    private const double SurfaceResistance = 1.0 / 10.0 + 1.0 / 20.0;

    #region Fixed 6-layer construction of floor and ceiling

    /// <summary>
    /// Creates the fixed 6-layer floor/ceiling construction used by every
    /// converted zone as a loop wall.
    /// </summary>
    /// <remarks>
    /// Reproduces the legacy Popolo v2.3 defaults: vinyl flooring (3 mm),
    /// air gap (50 mm), concrete (150 mm), air gap (50 mm), gypsum board
    /// (9 mm), rock-wool ceiling tile (15 mm).
    /// </remarks>
    private static WallLayer[] CreateDefaultFloorLayers()
    {
      return new WallLayer[]
      {
        new WallLayer("ビニル系床材", 0.190, 2000, 0.003),
        new AirGapLayer("非密閉中空層", false, 0.05),
        new WallLayer("コンクリート", 1.6, 2000, 0.150),
        new AirGapLayer("非密閉中空層", false, 0.05),
        new WallLayer("石膏ボード", 0.220, 830, 0.009),
        new WallLayer("ロックウール化粧吸音板", 0.064, 290, 0.015),
      };
    }

    #endregion

    #region Conversion results

    /// <summary>
    /// Output of <see cref="Convert(WebproModel)"/>.
    /// </summary>
    /// <remarks>
    /// Holds the generated <see cref="BuildingThermalModel"/> along with
    /// bookkeeping dictionaries that callers commonly need after conversion
    /// (e.g. to attach heat-gain schedulers to specific zones).
    /// </remarks>
    public sealed class ConversionResult
    {
      /// <summary>Gets the generated thermal model.</summary>
      public BuildingThermalModel Model { get; }

      /// <summary>Gets the single <see cref="MultiRooms"/> instance wrapped by <see cref="Model"/>.</summary>
      public MultiRoom MultiRooms { get; }

      /// <summary>Gets a mapping from WEBPRO room name to the <see cref="Zone"/> representing that room.</summary>
      public IReadOnlyDictionary<string, Zone> RoomNameToZone { get; }

      /// <summary>
      /// Gets the names of rooms whose <c>(BuildingType, roomType)</c> pair
      /// could not be resolved to a <see cref="WebproHeatGainScheduler.RoomType"/>
      /// and therefore received no automatic heat-gain scheduler.
      /// </summary>
      /// <remarks>
      /// Empty when <c>installHeatGainSchedulers</c> was false, or when all
      /// rooms resolved successfully. Callers can use this list to attach
      /// custom heat gains to unmapped rooms.
      /// </remarks>
      public IReadOnlyList<string> UnmappedRoomNames { get; }

      internal ConversionResult(
        BuildingThermalModel model,
        MultiRoom multiRooms,
        Dictionary<string, Zone> roomNameToZone,
        List<string> unmappedRoomNames)
      {
        Model = model;
        MultiRooms = multiRooms;
        RoomNameToZone = roomNameToZone;
        UnmappedRoomNames = unmappedRoomNames;
      }
    }

    #endregion

    #region Entry points

    /// <summary>
    /// Converts the given WEBPRO model into a
    /// <see cref="BuildingThermalModel"/>.
    /// </summary>
    /// <param name="model">Source WEBPRO model.</param>
    /// <param name="materials">
    /// Optional material catalog. Defaults to
    /// <see cref="MaterialCatalog.Default"/> when null.
    /// </param>
    /// <param name="glazings">
    /// Optional glazing catalog. Defaults to
    /// <see cref="GlazingCatalog.Default"/> when null.
    /// </param>
    /// <param name="roomTypeMapper">
    /// Optional room-type mapper used to resolve
    /// <c>(BuildingType, roomType)</c> pairs to
    /// <see cref="WebproHeatGainScheduler.RoomType"/>.
    /// Defaults to <see cref="RoomTypeMapper.Default"/> when null.
    /// </param>
    /// <param name="installHeatGainSchedulers">
    /// When <c>true</c> (default), a <see cref="WebproHeatGainScheduler"/>
    /// is installed on each converted zone based on the room's building and
    /// room types. When <c>false</c>, zones are created without schedulers
    /// and callers are expected to attach heat gains themselves via the
    /// <see cref="ConversionResult.RoomNameToZone"/> dictionary. Rooms whose
    /// <c>(BuildingType, roomType)</c> pair is not in the mapper are
    /// skipped silently (no exception) even when this flag is <c>true</c>;
    /// see <see cref="ConversionResult.UnmappedRoomNames"/> for the list of
    /// such rooms.
    /// </param>
    /// <returns>A <see cref="ConversionResult"/> bundling the model and lookup data.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public static ConversionResult Convert(
      WebproModel model,
      MaterialCatalog? materials = null,
      GlazingCatalog? glazings = null,
      RoomTypeMapper? roomTypeMapper = null,
      bool installHeatGainSchedulers = true)
    {
      if (model is null) throw new ArgumentNullException(nameof(model));
      var mats = materials ?? MaterialCatalog.Default;
      var glz = glazings ?? GlazingCatalog.Default;
      var mapper = roomTypeMapper ?? RoomTypeMapper.Default;

      var zones = new List<Zone>();
      var walls = new List<Wall>();
      var windows = new List<Window>();
      var roomNameToZone = new Dictionary<string, Zone>();
      var unmappedRoomNames = new List<string>();
      var loopWalls = new Dictionary<Zone, Wall>();
      var envelopeWallsByZone = new Dictionary<Zone, List<(Wall wall, WebproWall webproWall, Incline incline)>>();
      var windowsByZone = new Dictionary<Zone, List<Window>>();

      foreach (string roomName in model.AirConditionedRoomNames)
      {
        if (!model.Rooms.TryGetValue(roomName, out var room)) continue;

        // Zone 生成
        var zone = BuildZone(roomName, room);
        zones.Add(zone);
        roomNameToZone[roomName] = zone;

        // 発熱スケジューラ(オプション)
        if (installHeatGainSchedulers)
        {
          if (mapper.TryGet(room.BuildingType, room.RoomType, out var schedulerType))
          {
            zone.AddHeatGain(new WebproHeatGainScheduler(schedulerType));
          }
          else
          {
            unmappedRoomNames.Add(roomName);
          }
        }

        // 床・天井 (loop wall)
        var loopWall = new Wall(room.RoomArea, CreateDefaultFloorLayers());
        walls.Add(loopWall);
        loopWalls[zone] = loopWall;

        // 外皮 (オプション)
        if (model.Envelopes.TryGetValue(roomName, out var envelope))
        {
          var wallList = new List<(Wall, WebproWall, Incline)>();
          var windowList = new List<Window>();

          foreach (var webproWall in envelope.Walls)
          {
            var incline = OrientationToIncline(webproWall.SurfaceOrientation);

            // 窓の配置面積 (1 枚の面積 × 枚数) を一度だけ決め、
            // 窓の生成と正味壁面積の両方に同じ値を使う
            var placements = ResolveWindowPlacements(webproWall, model.WindowConfigurations);
            double windowArea = 0;
            foreach (var p in placements) windowArea += p.Area;

            var wall = BuildWall(webproWall, windowArea, model.WallConfigurations, mats);
            walls.Add(wall);
            wallList.Add((wall, webproWall, incline));

            foreach (var p in placements)
            {
              var win = BuildWindow(
                p.Window.ID, p.Configuration, p.Area, incline, p.Window.HasBlind, glz);
              windows.Add(win);
              windowList.Add(win);
            }
          }

          envelopeWallsByZone[zone] = wallList;
          windowsByZone[zone] = windowList;
        }
      }

      // MultiRooms 生成
      var multiRooms = new MultiRoom(
        rmCount: 1,
        zones: zones.ToArray(),
        walls: walls.ToArray(),
        windows: windows.ToArray());
      for (int i = 0; i < zones.Count; i++)
        multiRooms.AddZone(0, zones[i]);

      // 床・天井を loop wall として追加
      foreach (var (zone, loopWall) in loopWalls)
        multiRooms.AddLoopWall(zone, loopWall);

      // 外皮の壁を追加し、境界条件を設定
      foreach (var (zone, wallList) in envelopeWallsByZone)
      {
        foreach (var (wall, webproWall, incline) in wallList)
        {
          // B 側をゾーン内表面に、F 側を屋外側に
          multiRooms.AddWall(zone, wall, isSideF: false);
          ApplyBoundaryCondition(multiRooms, wall, webproWall, incline);
        }
      }

      // 窓を追加
      foreach (var (zone, windowList) in windowsByZone)
        foreach (var window in windowList)
          multiRooms.AddWindow(zone, window);

      // BuildingThermalModel 生成
      var thermalModel = new BuildingThermalModel(new MultiRoom[] { multiRooms })
      {
        TimeStep = 3600,
      };
      thermalModel.InitializeAirState(24, 0.018);

      return new ConversionResult(thermalModel, multiRooms, roomNameToZone, unmappedRoomNames);
    }

    #endregion

    #region Individual creation methods

    /// <summary>Builds a Popolo <see cref="Zone"/> for a WEBPRO room.</summary>
    private static Zone BuildZone(string roomName, WebproRoom room)
    {
      double airMass = WebproConversionConstants.AirDensity * room.RoomArea * room.CeilingHeight;
      var zone = new Zone(roomName, airMass, room.RoomArea)
      {
        HeatCapacity = room.RoomArea * WebproConversionConstants.ZoneHeatCapacityRate,
      };
      return zone;
    }

    /// <summary>Builds a Popolo <see cref="Wall"/> from a WEBPRO wall DTO.</summary>
    /// <param name="webproWall">Source wall entry.</param>
    /// <param name="windowArea">
    /// Total area of the windows placed on this wall [m²], as resolved by
    /// <see cref="ResolveWindowPlacements"/>; subtracted from the gross area.
    /// </param>
    /// <param name="wallConfigurations">Wall construction catalog of the model.</param>
    /// <param name="catalog">Material catalog.</param>
    private static Wall BuildWall(
      WebproWall webproWall,
      double windowArea,
      IReadOnlyDictionary<string, WebproWallConfiguration> wallConfigurations,
      MaterialCatalog catalog)
    {
      // 壁の総面積 (窓含む)
      double totalArea = webproWall.Area ?? ((webproWall.Width ?? 1.0) * (webproWall.Height ?? 1.0));

      // 窓面積の合計を差し引く (WEBPRO 慣習)。ゼロや負の壁面積を回避
      double netWallArea = Math.Max(0.1, totalArea - windowArea);

      // レイヤ構成
      if (!wallConfigurations.TryGetValue(webproWall.WallSpec, out var wallConf))
      {
        throw new InvalidOperationException(
          $"Wall spec '{webproWall.WallSpec}' is not defined in WallConfigurations.");
      }
      var layers = BuildWallLayers(webproWall.WallSpec, wallConf, webproWall, catalog);
      var wall = new Wall(netWallArea, layers);

      // 日射吸収率。日の当たらない外壁は日射を受けない (外気温・夜間放射は受ける)
      double absorptance = webproWall.Type == WallType.ShadingExternalWall
        ? 0.0
        : wallConf.SolarAbsorptionRatio ?? WebproConversionConstants.DefaultSolarAbsorptionRatio;
      wall.ShortWaveAbsorptanceF = absorptance;

      return wall;
    }

    /// <summary>
    /// Builds the ordered layer array (layer 0 = outdoor / F side) for a WEBPRO
    /// wall configuration.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><see cref="WallInputMethod.MaterialNumberAndThickness"/>
    ///     (and <see cref="WallInputMethod.None"/> for backward compatibility):
    ///     the listed layers are used in <b>reverse</b> order, because WEBPRO
    ///     lists them from the room side outward. A layer's explicit
    ///     <c>conductivity</c> overrides the catalog value.</description></item>
    ///   <item><description><see cref="WallInputMethod.HeatTransferCoefficient"/>:
    ///     an equivalent construction is synthesized from the U-value
    ///     (see <see cref="CreateEquivalentLayers"/>).</description></item>
    ///   <item><description><see cref="WallInputMethod.InsulationType"/>: not supported.</description></item>
    /// </list>
    /// </remarks>
    private static WallLayer[] BuildWallLayers(
      string wallSpec, WebproWallConfiguration wallConf, WebproWall webproWall, MaterialCatalog catalog)
    {
      switch (wallConf.Method)
      {
        case WallInputMethod.HeatTransferCoefficient:
          {
            // WallConfigure の Uvalue を優先し、無ければ壁側の Uvalue を使う
            double u = double.IsNaN(wallConf.HeatTransferCoefficient)
              ? webproWall.HeatTransferCoefficient
              : wallConf.HeatTransferCoefficient;
            return CreateEquivalentLayers(wallSpec, u);
          }

        case WallInputMethod.InsulationType:
          throw new PopoloNotImplementedException(
            $"wall input method '断熱材種類を入力' (InsulationType) used by wall spec '{wallSpec}'. " +
            $"Describe the construction with '建材構成を入力' or '熱貫流率を入力' instead.");

        case WallInputMethod.MaterialNumberAndThickness:
        case WallInputMethod.None:
        default:
          {
            int n = wallConf.Layers.Count;
            if (n == 0)
              throw new PopoloArgumentException(
                $"Wall spec '{wallSpec}' (input method '{wallConf.Method}') has no layers; " +
                $"an empty wall construction cannot be converted.", "model");

            // WEBPRO は室内側→屋外側の順。Popolo は layers[0] が F 側 (屋外側)
            var layers = new WallLayer[n];
            for (int i = 0; i < n; i++)
            {
              var layer = wallConf.Layers[n - 1 - i];
              layers[i] = catalog.MakeWallLayer(layer.MaterialID, layer.Thickness, layer.Conductivity);
            }
            return layers;
          }
      }
    }

    /// <summary>
    /// Synthesizes a layer set (layer 0 = outdoor side) whose steady-state
    /// U-value, with the WEBPRO surface resistances
    /// (<see cref="SurfaceResistance"/>), equals <paramref name="uValue"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The construction represents a typical internally insulated RC wall:
    /// 150 mm concrete (λ = 1.6 W/(m·K), 2000 kJ/(m³·K)) on the outdoor side
    /// and an insulation layer (λ = 0.040 W/(m·K), 32.5 kJ/(m³·K); extruded
    /// polystyrene foam type 1) on the room side, whose thickness supplies the
    /// remaining thermal resistance. When the required layer resistance is
    /// smaller than that of the 150 mm concrete, a single concrete layer of
    /// the matching thickness is used instead.
    /// </para>
    /// <para>
    /// The heat capacity of the synthesized wall is therefore an assumption;
    /// only the steady-state U-value is taken from the input.
    /// </para>
    /// </remarks>
    private static WallLayer[] CreateEquivalentLayers(string wallSpec, double uValue)
    {
      const double ConcreteConductivity = 1.6;
      const double ConcreteVolumetricHeat = 2000;
      const double ConcreteThickness = 0.150;
      const double InsulationConductivity = 0.040;
      const double InsulationVolumetricHeat = 32.5;

      if (!(double.IsFinite(uValue) && uValue > 0))
        throw new PopoloArgumentException(
          $"Wall spec '{wallSpec}' uses input method '熱貫流率を入力' but its U-value " +
          $"(WallConfigure 'Uvalue') is missing or not a positive number (got {uValue}).", "model");

      double rLayers = 1.0 / uValue - SurfaceResistance;
      if (rLayers <= 0)
        throw new PopoloArgumentException(
          $"Wall spec '{wallSpec}': U-value {uValue} W/(m²·K) is not below " +
          $"1/{SurfaceResistance} = {1.0 / SurfaceResistance:F3} W/(m²·K), so no positive " +
          $"layer resistance remains after subtracting the surface resistances.", "model");

      double rConcrete = ConcreteThickness / ConcreteConductivity;
      if (rLayers <= rConcrete)
      {
        return new WallLayer[]
        {
          new WallLayer("コンクリート", ConcreteConductivity, ConcreteVolumetricHeat,
            rLayers * ConcreteConductivity),
        };
      }

      return new WallLayer[]
      {
        new WallLayer("コンクリート", ConcreteConductivity, ConcreteVolumetricHeat, ConcreteThickness),
        new WallLayer("断熱材(熱貫流率換算)", InsulationConductivity, InsulationVolumetricHeat,
          (rLayers - rConcrete) * InsulationConductivity),
      };
    }

    /// <summary>A resolved window placement on a wall.</summary>
    /// <param name="Window">Source window entry.</param>
    /// <param name="Configuration">Referenced window specification.</param>
    /// <param name="Area">Placed area = per-window area × count [m²].</param>
    private readonly record struct WindowPlacement(
      WebproWindow Window, WebproWindowConfiguration Configuration, double Area);

    /// <summary>
    /// Resolves the windows placed on a WEBPRO wall and their areas.
    /// </summary>
    /// <remarks>
    /// Following builelib, the placed area is the per-window area of the
    /// specification (<c>windowArea</c>, or <c>windowWidth × windowHeight</c>
    /// when <c>windowArea</c> is not a positive finite number) multiplied by
    /// <see cref="WebproWindow.Number"/> (count; null is treated as 1).
    /// The sentinel ID "無" and zero-count entries are skipped.
    /// </remarks>
    private static List<WindowPlacement> ResolveWindowPlacements(
      WebproWall webproWall,
      IReadOnlyDictionary<string, WebproWindowConfiguration> windowConfigurations)
    {
      var result = new List<WindowPlacement>();
      foreach (var webproWindow in webproWall.Windows)
      {
        // sentinel "無" は窓なしを意味する
        if (webproWindow.ID == WebproConversionConstants.NoWindowSentinel) continue;

        if (!windowConfigurations.TryGetValue(webproWindow.ID, out var windowConf))
        {
          throw new InvalidOperationException(
            $"Window ID '{webproWindow.ID}' is not defined in WindowConfigurations.");
        }

        double count = webproWindow.Number ?? 1.0;
        if (!(double.IsFinite(count) && count >= 0))
          throw new PopoloArgumentException(
            $"Window '{webproWindow.ID}' on wall spec '{webproWall.WallSpec}' has an invalid " +
            $"WindowNumber (window count) {count}.", "model");
        if (count == 0) continue;

        double unitArea = double.IsFinite(windowConf.Area) && windowConf.Area > 0
          ? windowConf.Area
          : windowConf.Width * windowConf.Height;
        if (!(double.IsFinite(unitArea) && unitArea > 0))
          throw new PopoloArgumentException(
            $"Window spec '{webproWindow.ID}' has no valid per-window area: windowArea = " +
            $"{windowConf.Area}, windowWidth × windowHeight = {windowConf.Width} × {windowConf.Height}.",
            "model");

        result.Add(new WindowPlacement(webproWindow, windowConf, unitArea * count));
      }
      return result;
    }

    /// <summary>
    /// Builds a single Popolo <see cref="Window"/> given a WEBPRO
    /// configuration entry and the placement's area, incline, and blind flag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reproduces the legacy Popolo v2.3 <c>WebproWindowJson.MakeWindow</c>
    /// logic: resolves (τ, U-value) from the configuration method, applies
    /// the two-pane absorptance-zero correction, subtracts surface
    /// resistance, and optionally installs a bright venetian blind.
    /// </para>
    /// <para>
    /// The window is modeled as two glazing layers with one air gap. The
    /// internal resistance R = 1/U − <see cref="SurfaceResistance"/> is
    /// split between the two glass resistances (the <see cref="Window"/>
    /// default of 0.006 m²K/W each, reduced to R/4 each when R is smaller
    /// than 4 × 0.006) and the air gap (the remainder), so that
    /// Σglass + gap + 0.15 = 1/U exactly.
    /// </para>
    /// </remarks>
    /// <exception cref="PopoloArgumentException">
    /// The resolved U-value is not a positive finite number or is not below
    /// 1/<see cref="SurfaceResistance"/>.
    /// </exception>
    private static Window BuildWindow(
      string windowId,
      WebproWindowConfiguration windowConf,
      double area,
      Incline incline,
      bool hasBlind,
      GlazingCatalog catalog)
    {
      (double tau, double htCoef) = ResolveGlazingPerformance(windowConf, catalog);

      // 室内外の表面熱抵抗 (0.15) を差し引いた、ガラス + 中空層の熱抵抗
      if (!(double.IsFinite(htCoef) && htCoef > 0))
        throw new PopoloArgumentException(
          $"Window spec '{windowId}' (input method '{windowConf.Method}') has no valid U-value (got {htCoef}).",
          "model");
      double rInternal = 1.0 / htCoef - SurfaceResistance;
      if (rInternal <= 0)
        throw new PopoloArgumentException(
          $"Window spec '{windowId}': U-value {htCoef} W/(m²·K) is not below " +
          $"1/{SurfaceResistance} = {1.0 / SurfaceResistance:F3} W/(m²·K), so no positive " +
          $"glazing resistance remains after subtracting the surface resistances.", "model");

      // 吸収率=0 の二重ガラス仮定で単層透過率を 2 層等価値に補正
      tau = 2.0 * tau / (1.0 + tau);
      double rho = 1.0 - tau;

      var window = new Window(
        area,
        new double[] { tau, tau },
        new double[] { rho, rho },
        incline);

      // ガラスの熱抵抗は既定値 (0.006) を基本とし、内部抵抗が小さい場合のみ縮小する
      double rGlass = window.GetGlassResistance(0);
      if (4.0 * rGlass > rInternal)
      {
        rGlass = 0.25 * rInternal;
        for (int i = 0; i < window.GlazingCount; i++) window.SetGlassResistance(i, rGlass);
      }
      window.SetAirGapResistance(0, rInternal - window.GlazingCount * rGlass);

      // ブラインドは BrightVenetianBlind 固定 (旧版踏襲)
      if (hasBlind)
      {
        window.SetShadingDevice(
          2,
          new SimpleShadingDevice(SimpleShadingDevice.PredefinedDevice.BrightVenetianBlind));
      }

      return window;
    }

    /// <summary>
    /// Resolves (τ, U-value) for a window configuration based on its
    /// <see cref="WebproWindowConfiguration.Method"/>.
    /// </summary>
    private static (double tau, double htCoef) ResolveGlazingPerformance(
      WebproWindowConfiguration windowConf, GlazingCatalog catalog)
    {
      switch (windowConf.Method)
      {
        case WindowInputMethod.WindowSpec:
          return (windowConf.WindowSolarHeatGainRate, windowConf.WindowHeatTransferCoefficient);

        case WindowInputMethod.FrameTypeAndGlazingSpec:
          return (windowConf.GlazingSolarHeatGainRate, windowConf.GlazingHeatTransferCoefficient);

        case WindowInputMethod.FrameAndGlazingType:
          {
            var perf = catalog.Get(windowConf.GlazingID);
            return (perf.SolarHeatGain, perf.HeatTransferCoefficient);
          }

        case WindowInputMethod.None:
        default:
          throw new InvalidOperationException(
            $"Cannot resolve glazing performance for method '{windowConf.Method}'.");
      }
    }

    /// <summary>
    /// Applies the appropriate boundary condition to the F-side of the given
    /// wall based on the WEBPRO <see cref="WebproWall.Type"/>.
    /// </summary>
    private static void ApplyBoundaryCondition(
      MultiRoom multiRooms, Wall wall, WebproWall webproWall, Incline incline)
    {
      switch (webproWall.Type)
      {
        case WallType.ExternalWall:
        case WallType.ShadingExternalWall:
          // 日の当たらない外壁も外気境界 (外気温・夜間放射)。
          // 日射は BuildWall で ShortWaveAbsorptanceF = 0 として除外済み
          multiRooms.SetOutsideWall(wall, isSideF: true, incline);
          break;
        case WallType.GroundWall:
          multiRooms.SetGroundWall(
            wall, isSideF: true, WebproConversionConstants.GroundWallConductance);
          break;
        case WallType.InnerWall:
          multiRooms.UseAdjacentSpaceFactor(
            wall, isSideF: true, WebproConversionConstants.AdjacentSpaceFactor);
          break;
        default:
          throw new InvalidOperationException($"Unhandled wall type '{webproWall.Type}'.");
      }
    }

    #endregion

    #region Orientation conversion

    /// <summary>
    /// Converts a WEBPRO <see cref="WebproOrientation"/> to a Popolo.Core
    /// <see cref="Incline"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vertical (wall) orientations map to <c>verticalAngle = π/2</c>.
    /// Upper horizontal (roof) maps to <c>verticalAngle = 0</c>.
    /// Lower horizontal (floor) maps to <c>verticalAngle = π</c>, distinct
    /// from the roof case.
    /// </para>
    /// <para>
    /// Legacy <see cref="WebproOrientation.Shade"/> is treated as lower
    /// horizontal (shaded by definition);
    /// <see cref="WebproOrientation.Horizontal"/> as upper horizontal.
    /// </para>
    /// </remarks>
    public static Incline OrientationToIncline(WebproOrientation orientation)
    {
      switch (orientation)
      {
        case WebproOrientation.N: return new Incline(PopoloOrientation.N, Math.PI / 2);
        case WebproOrientation.NE: return new Incline(PopoloOrientation.NE, Math.PI / 2);
        case WebproOrientation.E: return new Incline(PopoloOrientation.E, Math.PI / 2);
        case WebproOrientation.SE: return new Incline(PopoloOrientation.SE, Math.PI / 2);
        case WebproOrientation.S: return new Incline(PopoloOrientation.S, Math.PI / 2);
        case WebproOrientation.SW: return new Incline(PopoloOrientation.SW, Math.PI / 2);
        case WebproOrientation.W: return new Incline(PopoloOrientation.W, Math.PI / 2);
        case WebproOrientation.NW: return new Incline(PopoloOrientation.NW, Math.PI / 2);
        case WebproOrientation.UpperHorizontal: return new Incline(PopoloOrientation.N, 0);
        case WebproOrientation.LowerHorizontal: return new Incline(PopoloOrientation.N, Math.PI);
        case WebproOrientation.Shade: return new Incline(PopoloOrientation.N, Math.PI);
        case WebproOrientation.Horizontal: return new Incline(PopoloOrientation.N, 0);
        default:
          throw new ArgumentOutOfRangeException(
            nameof(orientation), orientation, "Unhandled WEBPRO orientation.");
      }
    }

    #endregion
  }
}