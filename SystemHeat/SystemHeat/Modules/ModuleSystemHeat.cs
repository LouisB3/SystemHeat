﻿using System;
using System.Collections.Generic;
using System.Linq;
using KSP.Localization;
using UnityEngine;

namespace SystemHeat
{
  /// <summary>
  /// The simulation interface to the heat system. All heat producing or consuming modules
  /// on a vessel interact with an instance of this module to add and remove heat.
  /// </summary>
  public class ModuleSystemHeat : PartModule
  {
    // Unique name of the module on a part
    [KSPField(isPersistant = false)]
    public string moduleID = "heatModule";

    // Name of the icon to use
    [KSPField(isPersistant = false)]
    public string iconName = "Icon_Gears";

    // Volume of coolant provided by this system in m3
    [KSPField(isPersistant = false)]
    public float volume = 10f;


    // 
    [KSPField(isPersistant = false)]
    public int priority = 1;

    //  -- System level data storage --
    // Current total system temperature of all associated modules
    [KSPField(isPersistant = true, guiActive = false, guiName = "System Temp")]
    public float totalSystemTemperature = 0f;

    // Current total system flux of all associated modules
    [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "System Flux", groupName = "sysheatinfo", groupDisplayName = "#LOC_SystemHeat_ModuleSystemHeat_GroupName", groupStartCollapsed = false)]
    public float totalSystemFlux = 0f;

    public float consumedSystemFlux = 0f;


    public float systemNominalTemperature = 0f;

    // -- Loop level data storage --
    // Loop that this system is part of
    [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_SystemHeat_ModuleSystemHeat_Field_LoopID", groupName = "sysheatinfo", groupDisplayName = "#LOC_SystemHeat_ModuleSystemHeat_GroupName")]
    [UI_ChooseOption(affectSymCounterparts = UI_Scene.Editor, options = new[] { "None" }, scene = UI_Scene.All, suppressEditorShipModified = false)]
    public int currentLoopID = 0;

    // Current temperature of the loop
    [KSPField(isPersistant = true, guiActive = false, guiName = "Loop Temp")]
    public float currentLoopTemperature = 0f;

    // Current nominal temperature of the loop
    [KSPField(isPersistant = true, guiActive = false, guiName = "Loop Nom. Temp")]
    public float nominalLoopTemperature = 0f;

    // Current net flux of the loop
    [KSPField(isPersistant = true, guiActive = false, guiName = "Loop Flux")]
    public float currentLoopFlux = 0f;

    // Coolant being used (maps to a COOLANTTYPE)
    [KSPField(isPersistant = false)]
    public string coolantName = "default";


    // Current total system flux of all associated modules
    [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_SystemHeat_ModuleSystemHeat_Field_SystemFlux", groupName = "sysheatinfo", groupDisplayName = "#LOC_SystemHeat_ModuleSystemHeat_GroupName", groupStartCollapsed = false)]
    public string SystemFluxUI = "-";

    // Current total system flux of all associated modules
    [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_SystemHeat_ModuleSystemHeat_Field_SystemTemperature", groupName = "sysheatinfo", groupDisplayName = "#LOC_SystemHeat_ModuleSystemHeat_GroupName", groupStartCollapsed = false)]
    public string SystemTemperatureUI = "-";

    public int LoopID
    {
      get { return currentLoopID; }
      set { currentLoopID = value; }
    }

    public float LoopTemperature
    {
      get { return currentLoopTemperature; }
      set { currentLoopTemperature = value; }
    }

    public float LoopFlux
    {
      get { return currentLoopFlux; }
      set { currentLoopFlux = value; }
    }

    public string CurrentStatusString
    {
      get
      {
        if (totalSystemFlux >= 0f)
        {
          return $"Temperature Output: {systemNominalTemperature} K \nHeat Output: {totalSystemFlux} kW";
        }
        else
        {
          return $"Maximum Temperature {systemNominalTemperature} K \nHeat Consumed {totalSystemFlux} kW";
        }
      }
    }

    protected SystemHeatSimulator simulator;
    protected Dictionary<string, float> fluxes;
    protected Dictionary<string, float> temperatures;
    protected List<int> loopIDs;

    public override string GetModuleDisplayName()
    {
      return Localizer.Format("#LOC_SystemHeat_ModuleSystemHeat_DisplayName");
    }

    public override string GetInfo()
    {
      string msg = "";

      msg += Localizer.Format("#LOC_SystemHeat_ModuleSystemHeat_PartInfo", volume.ToString("F2"));
      return msg;
    }

    public void Start()
    {

      loopIDs = new List<int>();
      fluxes = new Dictionary<string, float>();
      temperatures = new Dictionary<string, float>();

      for (int i = 0; i < SystemHeatSettings.maxLoopCount; i++)
        loopIDs.Add(i);

      SetupUI();

      Utils.Log("[ModuleSystemHeat]: Setup complete", LogType.Modules);


      Fields["totalSystemTemperature"].guiActive = SystemHeatSettings.DebugPartUI;
      Fields["totalSystemTemperature"].guiActiveEditor = SystemHeatSettings.DebugPartUI;
      Fields["totalSystemFlux"].guiActive = SystemHeatSettings.DebugPartUI;
      Fields["totalSystemFlux"].guiActiveEditor = SystemHeatSettings.DebugPartUI;
      Fields["nominalLoopTemperature"].guiActive = SystemHeatSettings.DebugPartUI;
      Fields["nominalLoopTemperature"].guiActiveEditor = SystemHeatSettings.DebugPartUI;
      Fields["currentLoopTemperature"].guiActive = SystemHeatSettings.DebugPartUI;
      Fields["currentLoopTemperature"].guiActiveEditor = SystemHeatSettings.DebugPartUI;
      Fields["currentLoopFlux"].guiActive = SystemHeatSettings.DebugPartUI;
      Fields["currentLoopFlux"].guiActiveEditor = SystemHeatSettings.DebugPartUI;

      if (HighLogic.LoadedSceneIsFlight)
      {
        simulator = part.vessel.GetComponent<SystemHeatVessel>().Simulator;
      }
    }

    void SetupUI()
    {
      BaseField chooseField = Fields["currentLoopID"];
      UI_ChooseOption chooseOption = HighLogic.LoadedSceneIsFlight ? chooseField.uiControlFlight as UI_ChooseOption : chooseField.uiControlEditor as UI_ChooseOption;
      chooseOption.options = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
      chooseOption.onFieldChanged = ChangeLoop;

    }

    private void ChangeLoop(BaseField field, object oldFieldValueObj)
    {
      if (HighLogic.LoadedSceneIsFlight)
      {
        Utils.Log($"[ModuleSystemHeat] Changing all loop {(int)oldFieldValueObj} modules to loop {currentLoopID}", LogType.Modules);


        List<ModuleSystemHeat> allHeatModules = new List<ModuleSystemHeat>();
        for (int i = 0; i < part.vessel.Parts.Count; i++)
        {
          if (part.vessel.Parts[i].GetComponent<ModuleSystemHeat>())
          {
            allHeatModules.Add(part.vessel.Parts[i].GetComponent<ModuleSystemHeat>());
          }
        }

        // Find list of used heat modules
        List<int> usedModules = new List<int>();
        for (int i = 0; i < allHeatModules.Count; i++)
        {
          if (allHeatModules[i] != this)
          {
            if (!usedModules.Contains(allHeatModules[i].currentLoopID))
            {
              usedModules.Add(allHeatModules[i].currentLoopID);

              Utils.Log($"[ModuleSystemHeat] {allHeatModules[i].currentLoopID} is in use", LogType.Modules);

            }
          }
        }

        bool unused = false;

        int newID = currentLoopID;
        while (!unused)
        {

          if (usedModules.Contains(newID))
          {
            Utils.Log($"[ModuleSystemHeat] {newID} is in use and cannot be used", LogType.Modules);
            newID++;
          }
          else
          {
            unused = true;
            Utils.Log($"[ModuleSystemHeat] {newID} will be the new ID", LogType.Modules);
          }
        }

        for (int i = 0; i < allHeatModules.Count; i++)
        {
          if (allHeatModules[i] == this)
          {
            allHeatModules[i].currentLoopID = newID;

            UIPartActionWindow window = UIPartActionController.Instance?.GetItem(part, false);
            if (window == null) return;
            window.displayDirty = true;
          }
          if (allHeatModules[i].currentLoopID == (int)oldFieldValueObj)
          {

            Utils.Log($"[ModuleSystemHeat] Changing module with loop ID {allHeatModules[i].currentLoopID } to new {newID}", LogType.Modules);
            allHeatModules[i].currentLoopID = newID;

          }
        }
        simulator.ChangeLoopID((int)oldFieldValueObj, newID);
      }

    }

    /// <summary>
    /// Add heat flux at a given temperature to system
    /// </summary>
    /// <param name="id">the string ID of the source (should be unique)</param>
    /// <param name="sourceTemperature">the temperature of the source</param>
    /// <param name="flux">the flux of the source</param>
    public void AddFlux(string id, float sourceTemperature, float flux)
    {

      if (fluxes != null && temperatures != null)
      {

        fluxes[id] = flux;

        if (flux > 0f)
        {
          temperatures[id] = sourceTemperature;
        }
        else
        {
          temperatures[id] = 0f;
        }

        totalSystemFlux = fluxes.Sum(x => x.Value) * (float)(PhysicsGlobals.InternalHeatProductionFactor / 0.025d);
        totalSystemTemperature = temperatures.Sum(x => x.Value);

        systemNominalTemperature = totalSystemTemperature / (fluxes.Values.ToList().Where(x => x > 0f).Count());
      }
    }

    public void UpdateSimulationValues(float nominalTemp, float currentTemp, float currentNetFlux)
    {
      nominalLoopTemperature = nominalTemp;
      currentLoopTemperature = currentTemp;
      currentLoopFlux = currentNetFlux;
    }

    protected void FixedUpdate()
    {
      SystemFluxUI = String.Format("{0:F0} kW", totalSystemFlux);
      SystemTemperatureUI = String.Format("{0:F0} / {1:F0} K", LoopTemperature, nominalLoopTemperature);
    }



  }
}
