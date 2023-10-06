using System;
using System.Collections.Generic;
using System.Numerics;

using IVSDKDotNet;
using IVSDKDotNet.Enums;

using static IVSDKDotNet.Native.Natives;

namespace ExtendedPedWeaponPool {
    public class Main : Script {

        #region Variables
        private readonly string[] splitChar = new string[] { "," };

        private Random rnd;
        private List<WeaponOverride> weaponOverrides;
        private int executeIn;
        private bool showAreaName;

        private bool canStartTimer = true;
        #endregion

        #region Constructor
        public Main()
        {
            weaponOverrides = new List<WeaponOverride>();

            Uninitialize += Main_Uninitialize;
            Initialized += Main_Initialized;
            Tick += Main_Tick;
        }
        #endregion

        #region Classes
        public class WeaponOverride
        {
            #region Variables
            public int Chance;
            public uint Episode;
            public string Area;
            public int PedType;
            public List<string> PedModels;
            public int NewWeapon;
            public int AmmoMin, AmmoMax;
            #endregion

            #region Constructor
            public WeaponOverride(int chance, uint episode, string area, int pedType, List<string> pedModels, int newWeapon, int ammoMin, int ammoMax)
            {
                Chance = chance;
                Episode = episode;
                Area = area;
                PedType = pedType;
                PedModels = pedModels;
                NewWeapon = newWeapon;
                AmmoMin = ammoMin;
                AmmoMax = ammoMax;
            }
            #endregion

            public override string ToString()
            {
                return string.Format("Episode: {0}, Area: {1}, PedType: {2}, NewWeapon: {3}", Episode.ToString(), Area, PedType.ToString(), NewWeapon.ToString());
            }
        }
        #endregion

        private void Main_Uninitialize(object sender, EventArgs e)
        {
            weaponOverrides.Clear();
            weaponOverrides = null;
            rnd = null;
        }
        private void Main_Initialized(object sender, EventArgs e)
        {
            // Try to read ini file
            if (Settings != null)
            {
                // General stuff
                executeIn =     Settings.GetInteger("General", "ExecuteIn", 1000);
                showAreaName =  Settings.GetBoolean("General", "ShowAreaName", false);

                // Read groups
                int overrideCount = Settings.GetInteger("General", "OverrideCount", 0);
                for (int i = 0; i < overrideCount; i++)
                {
                    string section = i.ToString();
                    int chance =        Settings.GetInteger(section, "ChancePercentage", 50);
                    int episode =       Settings.GetInteger(section, "Episode", 3);
                    string area =       Settings.GetValue(section, "Area", "");
                    int pedType =       Settings.GetInteger(section, "PedType", -1);
                    string pedModels =  Settings.GetValue(section, "PedModels", string.Empty);
                    int newWeapon =     Settings.GetInteger(section, "WeaponOverride", 0);
                    int ammoMin =       Settings.GetInteger(section, "AmmoMin", 100);
                    int ammoMax =       Settings.GetInteger(section, "AmmoMax", 999);

                    // Split ped models string by split char (,) if available and necessary and finally add to list of ped models
                    List<string> pedModelsList = new List<string>();
                    if (!string.IsNullOrWhiteSpace(pedModels))
                    {
                        if (!pedModels.Contains(","))
                        {
                            pedModelsList.Add(pedModels);
                        }
                        else
                        {
                            pedModelsList.AddRange(pedModels.Split(splitChar, StringSplitOptions.RemoveEmptyEntries));
                        }
                    }

                    weaponOverrides.Add(new WeaponOverride(chance, (uint)episode, area, pedType, pedModelsList, newWeapon, ammoMin, ammoMax));
                }
            }
        }

        private void Main_Tick(object sender, EventArgs e)
        {
            // Get player ped
            int playerID = CONVERT_INT_TO_PLAYERINDEX(GET_PLAYER_ID());
            GET_PLAYER_CHAR(playerID, out int pPed);

            // Shows the current area name that the player is in if activated
            if (showAreaName)
            {
                GET_CHAR_COORDINATES(pPed, out Vector3 pos);
                ShowSubtitleMessage("Area: " + GET_NAME_OF_ZONE(pos.X, pos.Y, pos.Z));
            }

            if (canStartTimer)
            {
                StartNewTimer(executeIn, () => {

                    if (weaponOverrides == null || weaponOverrides.Count == 0)
                        return;

                    //GET_GAME_TIMER(out uint timer);
                    //rnd = new Random((int)timer);
                    rnd = new Random();

                    // Get current episode
                    uint currentEpisode = GET_CURRENT_EPISODE();

                    int[] peds = CPools.GetAllPedHandles();
                    for (int p = 0; p < peds.Length; p++)
                    {
                        int ped = peds[p];

                        if (ped == pPed)
                            continue;
                        if (IS_CHAR_DEAD(ped))
                            continue;

                        // Get ped type
                        GET_PED_TYPE(ped, out uint pType);
                        ePedType pedType = (ePedType)pType;

                        // Get ped model
                        GET_CHAR_MODEL(ped, out int pModel);

                        // Get ped coordinates
                        GET_CHAR_COORDINATES(ped, out Vector3 pos);

                        // Get current ped area
                        string currentPedArea = GET_NAME_OF_ZONE(pos.X, pos.Y, pos.Z);

                        // Loop through all available weapon overrides
                        for (int w = 0; w < weaponOverrides.Count; w++)
                        {
                            WeaponOverride wO = weaponOverrides[w];

                            // Check if things are ok and matching
                            if (wO.PedType <= -1 && wO.PedModels.Count == 0)
                                continue;

                            if (wO.Episode != currentEpisode && wO.Episode != 3)
                                continue;
                            if (wO.Area != currentPedArea && wO.Area.ToLower() != "any")
                                continue;

                            // If ped type is not smaller or equals to -1, then check for ped type.
                            if (!(wO.PedType <= -1))
                            {
                                if ((ePedType)wO.PedType != pedType)
                                    continue;
                            }

                            // Check if current ped model is in list of ped models.
                            if (!wO.PedModels.Contains(string.Format("0x{0}", pModel.ToString("X"))))
                                continue;

                            // Check chance
                            int num = rnd.Next(0, 201);
                            if (!(num <= wO.Chance))
                                continue;

                            // Give new weapon to char
                            if (!HAS_CHAR_GOT_WEAPON(ped, (uint)wO.NewWeapon))
                            {
                                REMOVE_ALL_CHAR_WEAPONS(ped);
                                
                                GIVE_WEAPON_TO_CHAR(ped, (uint)wO.NewWeapon, (uint)rnd.Next(wO.AmmoMin, wO.AmmoMax), false);
#if DEBUG
                                ShowSubtitleMessage(string.Format("rnd: {0}, chance: {1}, group: {2}", num.ToString(), wO.Chance.ToString(), w.ToString()));
#endif
                            }
                        }
                    }

                });
                canStartTimer = false;
            }

            //if (weaponOverrides == null || weaponOverrides.Count == 0)
            //    return;

            //GET_GAME_TIMER(out uint timer);
            //rnd = new Random((int)timer);

            //// Get player ped
            //int playerID = CONVERT_INT_TO_PLAYERINDEX(GET_PLAYER_ID());
            //GET_PLAYER_CHAR(playerID, out int pPed);

            //// Shows the current area name that the player is in if activated
            //if (showAreaName)
            //{
            //    GET_CHAR_COORDINATES(pPed, out Vector3 pos);
            //    ShowSubtitleMessage("Area: " + GET_NAME_OF_ZONE(pos.X, pos.Y, pos.Z));
            //}

            //// Get current episode
            //uint currentEpisode = GET_CURRENT_EPISODE();

            //int[] peds = CPools.GetAllPedHandles();
            //for (int p = 0; p < peds.Length; p++)
            //{
            //    int ped = peds[p];
                
            //    if (ped == pPed)
            //        continue;
            //    if (IS_CHAR_DEAD(ped))
            //        continue;

            //    // Get ped type
            //    GET_PED_TYPE(ped, out uint pType);
            //    ePedType pedType = (ePedType)pType;

            //    // Get ped model
            //    GET_CHAR_MODEL(ped, out int pModel);

            //    // Get ped coordinates
            //    GET_CHAR_COORDINATES(ped, out Vector3 pos);

            //    // Get current ped area
            //    string currentPedArea = GET_NAME_OF_ZONE(pos.X, pos.Y, pos.Z);

            //    // Loop through all available weapon overrides
            //    for (int w = 0; w < weaponOverrides.Count; w++)
            //    {
            //        WeaponOverride wO = weaponOverrides[w];

            //        // Check if things are ok and matching
            //        if (wO.PedType <= -1 && wO.PedModels.Count == 0)
            //            continue;

            //        if (wO.Episode != currentEpisode && wO.Episode != 3)
            //            continue;
            //        if (wO.Area != currentPedArea && wO.Area.ToLower() != "any")
            //            continue;

            //        // If ped type is not smaller or equals to -1, then check for ped type.
            //        if (!(wO.PedType <= -1))
            //        {
            //            if ((ePedType)wO.PedType != pedType)
            //                continue;
            //        }

            //        // Check if current ped model is in list of ped models.
            //        if (!wO.PedModels.Contains(string.Format("0x{0}", pModel.ToString("X"))))
            //            continue;

            //        // Check chance
            //        if (!(rnd.Next(0, 501) <= wO.Chance))
            //            continue;

            //        // Give new weapon to char
            //        if (!HAS_CHAR_GOT_WEAPON(ped, (uint)wO.NewWeapon))
            //        {
            //            REMOVE_ALL_CHAR_WEAPONS(ped);
            //            GIVE_WEAPON_TO_CHAR(ped, (uint)wO.NewWeapon, (uint)rnd.Next(wO.AmmoMin, wO.AmmoMax), false);
            //        }
            //    }
            //}
        }

    }
}
