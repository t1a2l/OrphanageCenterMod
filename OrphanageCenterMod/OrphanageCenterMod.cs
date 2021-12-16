using ICities;
using CitiesHarmony.API;
using UnityEngine;
using OrphanageCenterMod.Utils;

namespace OrphanageCenterMod
{
    public class OrphanageCenterMod : LoadingExtensionBase, IUserMod, ISerializableData  {
        private const bool LOG_BASE = true;

        private GameObject orphanageInitializerObj;
        private OrphanageInitializer orphanageInitializer;
        private OptionsManager optionsManager = new OptionsManager();

        public new IManagers managers { get; }

        private static OrphanageCenterMod instance;
        string IUserMod.Name => "Orphanage Center Mod";

        string IUserMod.Description => "Enables functionality for Orphanage Assets to function as working child care centers.";
        
        public void OnEnabled() {
             HarmonyHelper.DoOnHarmonyReady(() => Patcher.PatchAll());
        }

        public void OnDisabled() {
            if (HarmonyHelper.IsHarmonyInstalled) Patcher.UnpatchAll();
        }

        public static OrphanageCenterMod getInstance() {
            return instance;
        }

        public OrphanageInitializer getNursingHomeInitializer()
	    {
		    return orphanageInitializer;
	    }

        public OptionsManager getOptionsManager() {
            return this.optionsManager;
        }

        public void OnSettingsUI(UIHelperBase helper) {
            this.optionsManager.initialize(helper);
            this.optionsManager.loadOptions();
        }

        public override void OnCreated(ILoading loading) {
            Logger.logInfo(LOG_BASE, "OrphanageCenterMod Created");
            instance = this;
            base.OnCreated(loading);
            if (!(this.orphanageInitializerObj != null)) {
                this.orphanageInitializerObj = new GameObject("OrphanageCenterMod Orphanages");
                this.orphanageInitializer = this.orphanageInitializerObj.AddComponent<OrphanageInitializer>();
            }
        }

        public override void OnLevelUnloading()
	    {
		    base.OnLevelUnloading();
		    orphanageInitializer?.OnLevelUnloading();
	    }

        public override void OnLevelLoaded(LoadMode mode) {
            Logger.logInfo(true, "OrphanageCenterMod Level Loaded: {0}", mode);
		    base.OnLevelLoaded(mode);
		    switch (mode)
		    {
		        case LoadMode.NewGame:
		        case LoadMode.LoadGame:
			        orphanageInitializer?.OnLevelWasLoaded(6);
			    break;
		        case LoadMode.NewAsset:
		        case LoadMode.LoadAsset:
			        orphanageInitializer?.OnLevelWasLoaded(19);
			    break;
		    }
        }

        public override void OnReleased() {
            base.OnReleased();
            if (!HarmonyHelper.IsHarmonyInstalled)
            {
                return;
            }
            if (this.orphanageInitializerObj != null) {
                Object.Destroy(this.orphanageInitializerObj);
            }
        }

        public byte[] LoadData(string id) {
            Logger.logInfo(Logger.LOG_OPTIONS, "Load Data: {0}", id);
            return null;
        }

        public void SaveData(string id, byte[] data) {
            Logger.logInfo(Logger.LOG_OPTIONS, "Save Data: {0} -- {1}", id, data);
        }

        public string[] EnumerateData()
	    {
		    return null;
	    }

        public void EraseData(string id)
	    {
	    }

	    public bool LoadGame(string saveName)
	    {
		    return false;
	    }

	    public bool SaveGame(string saveName)
	    {
		    return false;
	    }
    }
}
