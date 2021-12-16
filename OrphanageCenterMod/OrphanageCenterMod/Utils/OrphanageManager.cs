using System;
using System.Collections.Generic;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using ICities;
using OrphanageCenterMod.AI;

namespace OrphanageCenterMod.Utils {
    public class OrphanageManager : ThreadingExtensionBase {
        private const bool LOG_CHILDREN = false;

        private const int DEFAULT_NUM_SEARCH_ATTEMPTS = 3;

        private static OrphanageManager instance;

        private readonly BuildingManager buildingManager;
        private readonly CitizenManager citizenManager;

        private readonly uint[] familiesWithChildren;
        private readonly HashSet<uint> childrenBeingProcessed;
        private uint numChildrenFamilies;

        private Randomizer randomizer;

        private int refreshTimer;
        private int running;

        public OrphanageManager() {
            Logger.logInfo(LOG_CHILDREN, "OrphanageManager Created");
            instance = this;

            this.randomizer = new Randomizer((uint) 73);
            this.citizenManager = Singleton<CitizenManager>.instance;
            this.buildingManager = Singleton<BuildingManager>.instance;

            uint numCitizenUnits = this.citizenManager.m_units.m_size;

            // TODO: This array size is excessive but will allow for never worrying about resizing, should consider allowing for resizing instead
            this.familiesWithChildren = new uint[numCitizenUnits];

            this.childrenBeingProcessed = new HashSet<uint>();
        }

        public static OrphanageManager getInstance() {
            return instance;
        }

        public override void OnBeforeSimulationTick() {
            // Refresh every every so often
            if (this.refreshTimer++ % 600 == 0) {
                // Make sure refresh can occur, otherwise set the timer so it will trigger again next try
                if (Interlocked.CompareExchange(ref this.running, 1, 0) == 1) {
                    this.refreshTimer = 0;
                    return;
                }

                // Refresh the Senior Citizens Array
                this.refreshChildren();

                // Reset the timer and running flag
                this.refreshTimer = 1;
                this.running = 0;
            }
        }

        private void refreshChildren() {
            CitizenUnit[] citizenUnits = this.citizenManager.m_units.m_buffer;
            this.numChildrenFamilies = 0;
            for (uint i = 0; i < citizenUnits.Length; i++) {
                for (int j = 0; j < 5; j++) {
                    uint citizenId = citizenUnits[i].GetCitizen(j);
                    if (this.isChild(citizenId) && this.validateChild(citizenId)) {
                        this.familiesWithChildren[this.numChildrenFamilies++] = i;
                        break;
                    }
                }
            }
        }

        public uint[] getFamilyWithChildren() {
            return this.getFamilyWithChildren(DEFAULT_NUM_SEARCH_ATTEMPTS);
        }

        public uint[] getFamilyWithChildren(int numAttempts) {
            Logger.logInfo(LOG_CHILDREN, "OrphanageManager.getFamilyWithChildren -- Start");
            // Lock to prevent refreshing while running, otherwise bail
            if (Interlocked.CompareExchange(ref this.running, 1, 0) == 1) {
                return null;
            }

            // Get random family that contains at least one senior
            uint[] family = this.getFamilyWithChildrenInternal(numAttempts);
            if (family == null) {
                Logger.logInfo(LOG_CHILDREN, "OrphanageManager.getFamilyWithChildren -- No Family");
                this.running = 0;
                return null;
            }

            // Mark all seniors in the family as being processed
            foreach (uint familyMember in family) {
                if (this.isChild(familyMember)) {
                    this.childrenBeingProcessed.Add(familyMember);
                }
            }


            Logger.logInfo(LOG_CHILDREN, "OrphanageManager.getFamilyWithSenior -- Finished: {0}", string.Join(", ", Array.ConvertAll(family, item => item.ToString())));
            this.running = 0;
            return family;
        }

        public void doneProcessingChild(uint childId) {
            this.childrenBeingProcessed.Remove(childId);
        }

        private uint[] getFamilyWithChildrenInternal(int numAttempts) {
            // Check to see if too many attempts already
            if (numAttempts <= 0) {
                return null;
            }

            // Get a random senior citizen
            uint familyId = this.fetchRandomFamilyWithChildren();
            Logger.logInfo(LOG_CHILDREN, "OrphanageManager.getFamilyWithChildrenInternal -- Family Id: {0}", familyId);
            if (familyId == 0) {
                // No Family with Senior Citizens to be located
                return null;
            }


            // Validate all seniors in the family and build an array of family members
            CitizenUnit familyWithSenior = this.citizenManager.m_units.m_buffer[familyId];
            uint[] family = new uint[5];
            bool seniorPresent = false;
            for (int i = 0; i < 5; i++) {
                uint familyMember = familyWithSenior.GetCitizen(i);
                if (this.isChild(familyMember)) {
                    if (!this.validateChild(familyMember)) {
                        // This particular Child is no longer valid for some reason, call recursively with one less attempt
                        return this.getFamilyWithChildrenInternal(--numAttempts);
                    }
                    seniorPresent = true;
                }
                Logger.logInfo(LOG_CHILDREN, "OrphanageManager.getFamilyWithChildrenInternal -- Family Member: {0}", familyMember);
                family[i] = familyMember;
            }

            if (!seniorPresent) {
                // No Senior was found in this family (which is a bit weird), try again
                return this.getFamilyWithChildrenInternal(--numAttempts);
            }

            return family;
        }

        private uint fetchRandomFamilyWithChildren() {
            if (this.numChildrenFamilies <= 0) {
                return 0;
            }

            int index = this.randomizer.Int32(this.numChildrenFamilies);
            return this.familiesWithChildren[index];
        }

        public bool isChild(uint childId) {
            if (childId == 0) {
                return false;
            }

            // Validate not dead
            if (this.citizenManager.m_citizens.m_buffer[childId].Dead) {
                return false;
            }

            // Validate Age
            int age = this.citizenManager.m_citizens.m_buffer[childId].Age;
            if (age > Citizen.AGE_LIMIT_TEEN) {
                return false;
            }

            return true;
        }

        private bool validateChild(uint childId) {
            // Validate this Child is not already being processed
            if (this.childrenBeingProcessed.Contains(childId)) {
                return false;
            }

            // Validate not homeless
            ushort homeBuildingId = this.citizenManager.m_citizens.m_buffer[childId].m_homeBuilding;
            if (homeBuildingId == 0) {
                return false;
            }

            // Validate not already living in a orphanage
            if (this.buildingManager.m_buildings.m_buffer[homeBuildingId].Info.m_buildingAI is OrphanageAI) {
                return false;
            }

            return true;
        }
    }
}