/* WallLayer.cs
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

using System;
using Popolo.Core.Physics;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>Represents a single layer in a wall or floor assembly.</summary>
  public class WallLayer : ICloneable, IReadOnlyWallLayer
  {
    #region 列挙型定義

    /// <summary>Specifies predefined wall layer materials with standard thermophysical properties.</summary>
    public enum Material
    {
      /// <summary>Cement mortar.</summary>
      Mortar,
      /// <summary>Reinforced concrete.</summary>
      ReinforcedConcrete,
      /// <summary>Lightweight aggregate concrete type 1.</summary>
      LightweightAggregateConcrete1,
      /// <summary>Lightweight aggregate concrete type 2.</summary>
      LightweightAggregateConcrete2,
      /// <summary>Autoclaved lightweight concrete (ALC).</summary>
      AutoclavedLightweightConcrete,
      /// <summary>Common brick.</summary>
      Brick,
      /// <summary>Fire brick.</summary>
      FireBrick,
      /// <summary>Copper.</summary>
      Copper,
      /// <summary>Aluminum alloy.</summary>
      Aluminum,
      /// <summary>Structural steel.</summary>
      Steel,
      /// <summary>Lead.</summary>
      Lead,
      /// <summary>Stainless steel.</summary>
      StainlessSteel,
      /// <summary>Float glass (for opaque use; not a window glass layer).</summary>
      FloatGlass,
      /// <summary>Polyvinyl chloride (PVC).</summary>
      PolyvinylChloride,
      /// <summary>Natural wood class 1 (cypress, cedar, Ezo spruce, etc.).</summary>
      Wood1,
      /// <summary>Natural wood class 2 (pine, lauan, etc.).</summary>
      Wood2,
      /// <summary>Natural wood class 3 (oak, cherry, beech, etc.).</summary>
      Wood3,
      /// <summary>Plywood.</summary>
      Plywood,
      /// <summary>Wood wool cement (insulating).</summary>
      WoodWoolCement,
      /// <summary>Wood chip cement.</summary>
      WoodChipCement,
      /// <summary>Hard board.</summary>
      HardBoard,
      /// <summary>Particle board.</summary>
      ParticleBoard,
      /// <summary>Plaster board (gypsum board).</summary>
      PlasterBoard,
      /// <summary>Gypsum plaster.</summary>
      GypsumPlaster,
      /// <summary>Lime plaster (white wash).</summary>
      WhiteWash,
      /// <summary>Soil wall (earthen wall).</summary>
      SoilWall,
      /// <summary>Fiber coating.</summary>
      FiberCoating,
      /// <summary>Tatami mat.</summary>
      Tatami,
      /// <summary>Ceramic tile.</summary>
      Tile,
      /// <summary>Plastic (vinyl) tile.</summary>
      PlasticTile,
      /// <summary>Residential glass wool insulation, 10 kg/m³.</summary>
      GlassWoolInsulation_10K,
      /// <summary>Residential glass wool insulation, 16 kg/m³.</summary>
      GlassWoolInsulation_16K,
      /// <summary>Residential glass wool insulation, 24 kg/m³.</summary>
      GlassWoolInsulation_24K,
      /// <summary>Residential glass wool insulation, 32 kg/m³.</summary>
      GlassWoolInsulation_34K,
      /// <summary>High-grade glass wool insulation, 16 kg/m³.</summary>
      HighGradeGlassWoolInsulation_16K,
      /// <summary>High-grade glass wool insulation, 24 kg/m³.</summary>
      HighGradeGlassWoolInsulation_24K,
      /// <summary>Blown glass wool insulation type 1, 13 kg/m³.</summary>
      BlowingGlassWoolInsulation_13K,
      /// <summary>Blown glass wool insulation type 2, 18 kg/m³.</summary>
      BlowingGlassWoolInsulation_18K,
      /// <summary>Blown glass wool insulation type 2, 30 kg/m³.</summary>
      BlowingGlassWoolInsulation_30K,
      /// <summary>Blown glass wool insulation type 2, 35 kg/m³.</summary>
      BlowingGlassWoolInsulation_35K,
      /// <summary>Residential rock wool insulation mat.</summary>
      RockWoolInsulationMat,
      /// <summary>Residential rock wool insulation felt.</summary>
      RockWoolInsulationFelt = 42,
      /// <summary>Residential rock wool insulation board.</summary>
      RockWoolInsulationBoard = 43,
      /// <summary>Blown rock wool insulation, 25 kg/m³.</summary>
      BlowingRockWoolInsulation_25K,
      /// <summary>Blown rock wool insulation, 35 kg/m³.</summary>
      BlowingRockWoolInsulation_35K,
      /// <summary>Rock wool decorative acoustic board.</summary>
      RockWoolAcousticBoard,
      /// <summary>Sprayed rock wool.</summary>
      SprayedRockWool,
      /// <summary>Bead-method polystyrene foam special grade.</summary>
      BeadMethodPolystyreneFoam_S,
      /// <summary>Bead-method polystyrene foam grade 1.</summary>
      BeadMethodPolystyreneFoam_1,
      /// <summary>Bead-method polystyrene foam grade 2.</summary>
      BeadMethodPolystyreneFoam_2,
      /// <summary>Bead-method polystyrene foam grade 3.</summary>
      BeadMethodPolystyreneFoam_3,
      /// <summary>Bead-method polystyrene foam grade 4.</summary>
      BeadMethodPolystyreneFoam_4,
      /// <summary>Extruded polystyrene foam type 1.</summary>
      ExtrudedPolystyreneFoam_1,
      /// <summary>Extruded polystyrene foam type 2.</summary>
      ExtrudedPolystyreneFoam_2,
      /// <summary>Extruded polystyrene foam type 3.</summary>
      ExtrudedPolystyreneFoam_3,
      /// <summary>Rigid urethane foam insulation board type 1-1.</summary>
      RigidUrethaneFoam_1_1,
      /// <summary>Rigid urethane foam insulation board type 1-2.</summary>
      RigidUrethaneFoam_1_2,
      /// <summary>Rigid urethane foam insulation board type 1-3.</summary>
      RigidUrethaneFoam_1_3,
      /// <summary>Rigid urethane foam insulation board type 2-1.</summary>
      RigidUrethaneFoam_2_1,
      /// <summary>Rigid urethane foam insulation board type 2-2.</summary>
      RigidUrethaneFoam_2_2,
      /// <summary>Rigid urethane foam insulation board type 2-3.</summary>
      RigidUrethaneFoam_2_3,
      /// <summary>Rigid urethane foam, on-site spray type.</summary>
      RigidUrethaneFoam_OnSite,
      /// <summary>Polyethylene foam type A.</summary>
      PolyethyleneFoam_A,
      /// <summary>Polyethylene foam type B.</summary>
      PolyethyleneFoam_B,
      /// <summary>Phenolic foam insulation board type 1-1.</summary>
      PhenolicFoam_1_1,
      /// <summary>Phenolic foam insulation board type 1-2.</summary>
      PhenolicFoam_1_2,
      /// <summary>Phenolic foam insulation board type 2-1.</summary>
      PhenolicFoam_2_1,
      /// <summary>Phenolic foam insulation board type 2-2.</summary>
      PhenolicFoam_2_2,
      /// <summary>Grade-A insulation board.</summary>
      InsulationBoard_A,
      /// <summary>Tatami board.</summary>
      TatamiBoard,
      /// <summary>Sheathing insulation board.</summary>
      SheathingInsulationBoard,
      /// <summary>Blown cellulose fiber insulation type 1.</summary>
      CelluloseFiberInsulation_1,
      /// <summary>Blown cellulose fiber insulation type 2.</summary>
      CelluloseFiberInsulation_2,
      /// <summary>Loam soil.</summary>
      Soil,
      /// <summary>Expanded polystyrene (EPS).</summary>
      ExpandedPolystyrene,
      /// <summary>Exterior covering material.</summary>
      CoveringMaterial,
      /// <summary>Synthetic resin / linoleum.</summary>
      Linoleum,
      /// <summary>Carpet.</summary>
      Carpet,
      /// <summary>Asbestos slate (asbestos cement board).</summary>
      AsbestosPlate,
      /// <summary>Sealed (still) air gap.</summary>
      SealedAirGap,
      /// <summary>Ventilated air gap.</summary>
      AirGap,
      /// <summary>Polystyrene foam.</summary>
      PolystyreneFoam,
      /// <summary>Styrene foam board.</summary>
      StyreneFoam,
      /// <summary>Rubber tile.</summary>
      RubberTile,
      /// <summary>Ceramic roof tile (kawara).</summary>
      Kawara,
      /// <summary>Lightweight concrete.</summary>
      LightweightConcrete,
      /// <summary>Waterproofing layer (asphalt roofing).</summary>
      Asphalt,
      /// <summary>Flexible board (fiber-reinforced cement board).</summary>
      FlexibleBoard,
      /// <summary>Calcium silicate board.</summary>
      CalciumSilicateBoard,
      /// <summary>High-performance phenolic foam board.</summary>
      PhenolicFoam,
      /// <summary>Granite.</summary>
      Granite,
      /// <summary>Acrylic resin.</summary>
      AcrylicResin
    }

    #endregion

    #region プロパティ

    /// <summary>Gets the discriminator identifying the concrete layer type.
    /// Returns <c>"wallLayer"</c> for the base class; subtypes override this.</summary>
    public virtual string Kind => "wallLayer";

    /// <summary>Gets or sets the name of the layer.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets a value indicating whether thermophysical properties can change with temperature.</summary>
    public bool IsVariableProperties { get; protected set; }

    /// <summary>Gets the thermal conductivity [W/(m·K)].</summary>
    public double ThermalConductivity { get; protected set; }

    /// <summary>Gets the moisture conductivity [(kg/s)/((kg/kg)·m)].</summary>
    public double MoistureConductivity { get; protected set; }

    /// <summary>Gets the volumetric specific heat [kJ/(m³·K)].</summary>
    public double VolSpecificHeat { get; protected set; }

    /// <summary>Gets the thermal conductance [W/(m²·K)].</summary>
    public double HeatConductance { get; protected set; }

    /// <summary>Gets the sensible heat capacity on the F side [J/(m²·K)].</summary>
    public double HeatCapacity_F { get; protected set; }

    /// <summary>Gets the sensible heat capacity on the B side [J/(m²·K)].</summary>
    public double HeatCapacity_B { get; protected set; }

    /// <summary>Gets the moisture capacity [kg/m²].</summary>
    public double WaterCapacity { get; protected set; }

    /// <summary>Gets the moisture absorption coefficient per unit humidity difference [kg/m²].</summary>
    public double KappaC { get; private set; }

    /// <summary>Gets the moisture release coefficient per unit temperature difference [kg/(m²·K)].</summary>
    public double NuC { get; private set; }

    /// <summary>Gets the layer thickness [m].</summary>
    public double Thickness { get; protected set; }

    #endregion

    #region インスタンスメソッド

    /// <summary>Initializes a new empty instance for use by derived classes.</summary>
    protected WallLayer() { }

    /// <summary>Initializes a new instance with a predefined material and thickness.</summary>
    /// <param name="material">Predefined material type.</param>
    /// <param name="thickness">Layer thickness [m].</param>
    public WallLayer(Material material, double thickness)
    {
      switch (material)
      {
        case Material.Mortar:
          Initialize("Mortar", 1.512, 1591.0, thickness);
          break;
        case Material.ReinforcedConcrete:
          Initialize("Reinforced Concrete", 1.600, 1896.0, thickness);
          break;
        case Material.LightweightAggregateConcrete1:
          Initialize("Lightweight Aggregate Concrete 1", 0.810, 1900.0, thickness);
          break;
        case Material.LightweightAggregateConcrete2:
          Initialize("Lightweight Aggregate Concrete 2", 0.580, 1599.0, thickness);
          break;
        case Material.AutoclavedLightweightConcrete:
          Initialize("Autoclaved Lightweight Concrete", 0.170, 661.4, thickness);
          break;
        case Material.Brick:
          Initialize("Brick", 0.620, 1386.0, thickness);
          break;
        case Material.FireBrick:
          Initialize("FireBrick", 0.990, 1553.0, thickness);
          break;
        case Material.Copper:
          Initialize("Copper", 370.100, 3144.0, thickness);
          break;
        case Material.Aluminum:
          Initialize("Aluminum", 200.000, 2428.0, thickness);
          break;
        case Material.Steel:
          Initialize("Steel", 53.010, 3759.0, thickness);
          break;
        case Material.Lead:
          Initialize("Lead", 35.010, 1469.0, thickness);
          break;
        case Material.StainlessSteel:
          Initialize("Stainless Steel", 15.000, 3479.0, thickness);
          break;
        case Material.FloatGlass:
          Initialize("Float Glass", 1.000, 1914.0, thickness);
          break;
        case Material.PolyvinylChloride:
          Initialize("Polyvinyl Chloride", 0.170, 1023.0, thickness);
          break;
        case Material.Wood1:
          Initialize("Wood (Cedar)", 0.120, 519.1, thickness);
          break;
        case Material.Wood2:
          Initialize("Wood (Pine, Lauan)", 0.150, 648.8, thickness);
          break;
        case Material.Wood3:
          Initialize("Wood (Cherry, Fagaceae)", 0.190, 845.6, thickness);
          break;
        case Material.Plywood:
          Initialize("Plywood", 0.190, 716.0, thickness);
          break;
        case Material.WoodWoolCement:
          Initialize("Wood Wool Cement", 0.100, 841.4, thickness);
          break;
        case Material.WoodChipCement:
          Initialize("Wood Chip Cement", 0.170, 1679.0, thickness);
          break;
        case Material.HardBoard:
          Initialize("Hard Board", 0.170, 1233.0, thickness);
          break;
        case Material.ParticleBoard:
          Initialize("Particle Board", 0.150, 715.8, thickness);
          break;
        case Material.PlasterBoard:
          Initialize("Plaster Board", 0.170, 1030.0, thickness);
          break;
        case Material.GypsumPlaster:
          Initialize("Gypsum Plaster", 0.600, 1637.0, thickness);
          break;
        case Material.WhiteWash:
          Initialize("White Wash", 0.700, 1093.0, thickness);
          break;
        case Material.SoilWall:
          Initialize("Soil Wall", 0.690, 1126.0, thickness);
          break;
        case Material.FiberCoating:
          Initialize("Fiber Coating", 0.120, 4.2, thickness);
          break;
        case Material.Tatami:
          Initialize("Tatami", 0.110, 527.4, thickness);
          break;
        case Material.Tile:
          Initialize("Tile", 1.300, 2018.0, thickness);
          break;
        case Material.PlasticTile:
          Initialize("Plastic Tile", 0.190, 4.2, thickness);
          break;
        case Material.GlassWoolInsulation_10K:
          Initialize("Glass Wool Insulation 10kg/m3", 0.050, 8.4, thickness);
          break;
        case Material.GlassWoolInsulation_16K:
          Initialize("Glass Wool Insulation 16kg/m3", 0.045, 13.4, thickness);
          break;
        case Material.GlassWoolInsulation_24K:
          Initialize("Glass Wool Insulation 24kg/m3", 0.038, 20.1, thickness);
          break;
        case Material.GlassWoolInsulation_34K:
          Initialize("Glass Wool Insulation 32kg/m3", 0.036, 26.8, thickness);
          break;
        case Material.HighGradeGlassWoolInsulation_16K:
          Initialize("High Grade Glass Wool Insulation 16kg/m3", 0.038, 13.4, thickness);
          break;
        case Material.HighGradeGlassWoolInsulation_24K:
          Initialize("High Grade Glass Wool Insulation 24kg/m3", 0.036, 20.1, thickness);
          break;
        case Material.BlowingGlassWoolInsulation_13K:
          Initialize("Blowing Glass Wool Insulation 13kg/m3", 0.052, 10.9, thickness);
          break;
        case Material.BlowingGlassWoolInsulation_18K:
          Initialize("Blowing Glass Wool Insulation 18kg/m3", 0.052, 16.7, thickness);
          break;
        case Material.BlowingGlassWoolInsulation_30K:
          Initialize("Blowing Glass Wool Insulation 30kg/m3", 0.040, 29.3, thickness);
          break;
        case Material.BlowingGlassWoolInsulation_35K:
          Initialize("Blowing Glass Wool Insulation 35kg/m3", 0.040, 37.7, thickness);
          break;
        case Material.RockWoolInsulationMat:
          Initialize("Rock Wool Insulation Mat", 0.038, 33.5, thickness);
          break;
        case Material.RockWoolInsulationFelt:
          Initialize("Rock Wool Insulation Felt", 0.038, 41.9, thickness);
          break;
        case Material.RockWoolInsulationBoard:
          Initialize("Rock Wool Insulation Board", 0.036, 58.6, thickness);
          break;
        case Material.BlowingRockWoolInsulation_25K:
          Initialize("Blowing Rock Wool Insulation 25kg/m3", 0.047, 20.9, thickness);
          break;
        case Material.BlowingRockWoolInsulation_35K:
          Initialize("Blowing Rock Wool Insulation 35kg/m3", 0.051, 29.3, thickness);
          break;
        case Material.RockWoolAcousticBoard:
          Initialize("Rock Wool Acoustic Board", 0.058, 293.9, thickness);
          break;
        case Material.SprayedRockWool:
          Initialize("Sprayed Rock Wool", 0.047, 167.9, thickness);
          break;
        case Material.BeadMethodPolystyreneFoam_S:
          Initialize("Bead Method Polystyrene Foam S", 0.034, 33.9, thickness);
          break;
        case Material.BeadMethodPolystyreneFoam_1:
          Initialize("Bead Method Polystyrene Foam 1", 0.036, 37.7, thickness);
          break;
        case Material.BeadMethodPolystyreneFoam_2:
          Initialize("Bead Method Polystyrene Foam 2", 0.037, 31.4, thickness);
          break;
        case Material.BeadMethodPolystyreneFoam_3:
          Initialize("Bead Method Polystyrene Foam 3", 0.040, 25.1, thickness);
          break;
        case Material.BeadMethodPolystyreneFoam_4:
          Initialize("Bead Method Polystyrene Foam 4", 0.043, 18.8, thickness);
          break;
        case Material.ExtrudedPolystyreneFoam_1:
          Initialize("Extruded Polystyrene Foam 1", 0.040, 25.1, thickness);
          break;
        case Material.ExtrudedPolystyreneFoam_2:
          Initialize("Extruded Polystyrene Foam 2", 0.034, 25.1, thickness);
          break;
        case Material.ExtrudedPolystyreneFoam_3:
          Initialize("Extruded Polystyrene Foam 3", 0.028, 25.1, thickness);
          break;
        case Material.RigidUrethaneFoam_1_1:
          Initialize("Rigid Urethane Foam 1_1", 0.024, 56.1, thickness);
          break;
        case Material.RigidUrethaneFoam_1_2:
          Initialize("Rigid Urethane Foam 1_2", 0.024, 44.0, thickness);
          break;
        case Material.RigidUrethaneFoam_1_3:
          Initialize("Rigid Urethane Foam 1_3", 0.026, 31.4, thickness);
          break;
        case Material.RigidUrethaneFoam_2_1:
          Initialize("Rigid Urethane Foam 2_1", 0.023, 56.1, thickness);
          break;
        case Material.RigidUrethaneFoam_2_2:
          Initialize("Rigid Urethane Foam 2_2", 0.023, 44.0, thickness);
          break;
        case Material.RigidUrethaneFoam_2_3:
          Initialize("Rigid Urethane Foam 2_3", 0.024, 31.4, thickness);
          break;
        case Material.RigidUrethaneFoam_OnSite:
          Initialize("Rigid Urethane Foam (OnSite)", 0.026, 49.8, thickness);
          break;
        case Material.PolyethyleneFoam_A:
          Initialize("Polyethylene Foam A", 0.038, 62.8, thickness);
          break;
        case Material.PolyethyleneFoam_B:
          Initialize("Polyethylene Foam B", 0.042, 62.8, thickness);
          break;
        case Material.PhenolicFoam_1_1:
          Initialize("Phenolic Foam 1_1", 0.033, 37.7, thickness);
          break;
        case Material.PhenolicFoam_1_2:
          Initialize("Phenolic Foam 1_2", 0.030, 37.7, thickness);
          break;
        case Material.PhenolicFoam_2_1:
          Initialize("Phenolic Foam 2_1", 0.036, 56.5, thickness);
          break;
        case Material.PhenolicFoam_2_2:
          Initialize("Phenolic Foam 2_2", 0.034, 56.5, thickness);
          break;
        case Material.InsulationBoard_A:
          Initialize("Insulation Board A", 0.049, 324.8, thickness);
          break;
        case Material.TatamiBoard:
          Initialize("Tatami Board", 0.045, 15.1, thickness);
          break;
        case Material.SheathingInsulationBoard:
          Initialize("Sheathing Insulation Board", 0.052, 390.1, thickness);
          break;
        case Material.CelluloseFiberInsulation_1:
          Initialize("Cellulose Fiber Insulation 1", 0.040, 37.7, thickness);
          break;
        case Material.CelluloseFiberInsulation_2:
          Initialize("Cellulose Fiber Insulation 2", 0.040, 62.8, thickness);
          break;
        case Material.Soil:
          Initialize("Soil", 1.047, 3340.0, thickness);
          break;
        case Material.ExpandedPolystyrene:
          Initialize("Expanded Polystyrene", 0.035, 300.0, thickness);
          break;
        case Material.CoveringMaterial:
          Initialize("Covering Material", 0.140, 1680.0, thickness);
          break;
        case Material.Linoleum:
          Initialize("Linoleum", 0.190, 1470.0, thickness);
          break;
        case Material.Carpet:
          Initialize("Carpet", 0.080, 318.0, thickness);
          break;
        case Material.AsbestosPlate:
          Initialize("Asbestos Plate", 1.200, 1820.0, thickness);
          break;
        case Material.SealedAirGap:
          Initialize("Sealed AirGap", 5.800, 0.838, thickness);
          break;
        case Material.AirGap:
          Initialize("Air Gap", 11.600, 0.838, thickness);
          break;
        case Material.PolystyreneFoam:
          Initialize("Polystyrene Foam", 0.035, 80.0, thickness);
          break;
        case Material.StyreneFoam:
          Initialize("Styrene Foam", 0.035, 10.0, thickness);
          break;
        case Material.RubberTile:
          Initialize("Rubber Tile", 0.400, 784.0, thickness);
          break;
        case Material.Kawara:
          Initialize("Kawara", 1.000, 1506.0, thickness);
          break;
        case Material.LightweightConcrete:
          Initialize("Lightweight Concrete", 0.780, 1607.0, thickness);
          break;
        case Material.Asphalt:
          Initialize("Asphalt", 0.110, 920.0, thickness);
          break;
        case Material.FlexibleBoard:
          Initialize("Flexible Board", 0.350, 1600.0, thickness);
          break;
        case Material.CalciumSilicateBoard:
          Initialize("Calcium Silicate Board", 0.130, 680.0, thickness);
          break;
        case Material.PhenolicFoam:
          Initialize("Phenolic Foam", 0.020, 37.7, thickness);
          break;
        case Material.Granite:
          Initialize("Granite", 4.300, 2.9, thickness);
          break;
        case Material.AcrylicResin:
          Initialize("Acrylic Resin", 0.210, 1666.0, thickness);
          break;
        default:
          throw new Exceptions.PopoloArgumentException("Wall material is not defined.", "material");
      }
    }

    /// <summary>Initializes a new instance with specified thermophysical properties.</summary>
    /// <param name="name">Layer name.</param>
    /// <param name="thermalConductivity">Thermal conductivity [W/(m·K)].</param>
    /// <param name="volSpecificHeat">Volumetric specific heat [kJ/(m³·K)].</param>
    /// <param name="thickness">Layer thickness [m].</param>
    public WallLayer(string name, double thermalConductivity, double volSpecificHeat, double thickness)
    { Initialize(name, thermalConductivity, volSpecificHeat, thickness); }

    /// <summary>Initializes a new instance with thermophysical and moisture transfer properties.</summary>
    /// <param name="name">Layer name.</param>
    /// <param name="thermalConductivity">Thermal conductivity [W/(m·K)].</param>
    /// <param name="volSpecificHeat">Volumetric specific heat [kJ/(m³·K)].</param>
    /// <param name="moistureConductivity">Moisture conductivity [(kg/s)/((kg/kg)·m)].</param>
    /// <param name="voidage">Void fraction [-].</param>
    /// <param name="kappa">Moisture absorption coefficient [kg/m³].</param>
    /// <param name="nu">Moisture release coefficient [kg/(m³·K)].</param>
    /// <param name="thickness">Layer thickness [m].</param>
    public WallLayer(string name, double thermalConductivity, double volSpecificHeat,
      double moistureConductivity, double voidage, double kappa, double nu, double thickness)
    {
      Initialize(name, thermalConductivity, volSpecificHeat, thickness);
      MoistureConductivity = moistureConductivity;
      WaterCapacity = 0.5 * voidage * thickness * PhysicsConstants.NominalMoistAirDensity;
      KappaC = 0.5 * kappa * thickness;
      NuC = 0.5 * nu * thickness;
    }

    /// <summary>Initializes internal state from thermophysical parameters.</summary>
    /// <param name="name">Layer name.</param>
    /// <param name="thermalConductivity">Thermal conductivity [W/(m·K)].</param>
    /// <param name="volumetricSpecificHeat">Volumetric specific heat [kJ/(m³·K)].</param>
    /// <param name="thickness">Layer thickness [m].</param>
    protected void Initialize
      (string name, double thermalConductivity, double volumetricSpecificHeat, double thickness)
    {
      Name = name;
      ThermalConductivity = thermalConductivity;
      VolSpecificHeat = volumetricSpecificHeat;
      Thickness = thickness;
      HeatConductance = ThermalConductivity / Thickness;
      HeatCapacity_F = HeatCapacity_B = 0.5 * VolSpecificHeat * Thickness * 1000d;
    }

    /// <summary>Updates thermophysical properties based on layer-end temperatures.</summary>
    /// <param name="temperature1">Temperature at end 1 [°C].</param>
    /// <param name="temperature2">Temperature at end 2 [°C].</param>
    /// <returns>True if properties changed; otherwise false.</returns>
    public virtual bool UpdateState(double temperature1, double temperature2) { return false; }

    /// <summary>Updates thermophysical properties based on layer-end temperatures and humidity ratios.</summary>
    /// <param name="temperature1">Temperature at end 1 [°C].</param>
    /// <param name="temperature2">Temperature at end 2 [°C].</param>
    /// <param name="humidity1">Humidity ratio at end 1 [kg/kg].</param>
    /// <param name="humidity2">Humidity ratio at end 2 [kg/kg].</param>
    /// <returns>True if properties changed; otherwise false.</returns>
    public virtual bool UpdateState
      (double temperature1, double temperature2, double humidity1, double humidity2)
    { return false; }

    #endregion

    #region ICloneable実装

    /// <summary>Creates a shallow copy of this instance.</summary>
    /// <returns>A new <see cref="WallLayer"/> with the same property values.</returns>
    public virtual object Clone() { return MemberwiseClone(); }

    #endregion

  }

}