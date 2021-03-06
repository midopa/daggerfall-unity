﻿// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2016 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Uncanny_Valley
// Contributors:    TheLacus 
// 
// Notes:
//

/*
 * TODO:
 * - StreamingWorld
 * - Optimize FinaliseCustomGameObject() to avoid importing the same texture more than once
 */

using System.IO;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace DaggerfallWorkshop.Utility.AssetInjection
{
    /// <summary>
    /// Handles import and injection of custom meshes and materials
    /// with the purpose of providing modding support.
    /// </summary>
    static public class MeshReplacement
    {
        #region Fields

        // Paths
        static public string modelsPath = Path.Combine(Application.streamingAssetsPath, "Models");
        static public string flatsPath = Path.Combine(Application.streamingAssetsPath, "Flats");

        // Extensions
        const string modelExtension = ".model";
        const string flatExtension = ".flat";

        // Variants
        const string winterTag = "_winter";

        #endregion

        #region Models

        /// <summary>
        /// Import the custom GameObject if available
        /// </summary>
        /// <returns>Returns the imported model or null.</returns>
        static public GameObject ImportCustomGameobject (uint modelID, Vector3 position, Transform parent, Quaternion rotation)
        {
            // Check user settings
            if (!DaggerfallUnity.Settings.MeshAndTextureReplacement)
                return null;

            // Import Gameobject from Resources
            // This is useful to test models
            if (ReplacementPrefabExist(modelID))
                return LoadReplacementPrefab(modelID, position, parent, rotation);

            // Get model from disk
            string modelName = modelID.ToString();
            string path = Path.Combine(modelsPath, modelName + modelExtension);
            if (File.Exists(path))
            {
                // Get AssetBundle
                AssetBundle LoadedAssetBundle;
                if (TryGetAssetBundle(path, modelName, out LoadedAssetBundle))
                {
                    // Assign the name according to the current season
                    if (IsWinter())
                    {
                        if (LoadedAssetBundle.Contains(modelName + winterTag))
                            modelName += winterTag;
                    }

                    // Instantiate GameObject
                    GameObject object3D = GameObject.Instantiate(LoadedAssetBundle.LoadAsset<GameObject>(modelName));
                    LoadedAssetBundle.Unload(false);
                    InstantiateCustomModel(object3D, ref position, parent, ref rotation, modelName);
                    return object3D;
                }
            }
            
            // Get model from mods using load order
            Mod[] mods = ModManager.Instance.GetAllMods(true);
            for (int i = mods.Length; i-- > 0;)
            {
                if (mods[i].AssetBundle.Contains(modelName))
                {
                    // Assign the name according to the current season
                    if (IsWinter())
                    {
                        if (mods[i].AssetBundle.Contains(modelName + winterTag))
                            modelName += winterTag;
                    }

                    GameObject go = mods[i].GetAsset<GameObject>(modelName, true);
                    if (go != null)
                    {
                        InstantiateCustomModel(go, ref position, parent, ref rotation, modelName);
                        return go;
                    }

                    Debug.LogError("Failed to import " + modelName + " from " + mods[i].Title + " as GameObject.");
                }
            }

            return null;
        }

        /// <summary>
        /// Check existence of gameobject in Resources
        /// </summary>
        /// <returns>Bool</returns>
        static public bool ReplacementPrefabExist(uint modelID)
        {
            if (DaggerfallUnity.Settings.MeshAndTextureReplacement //check .ini setting
                 && Resources.Load("Models/" + modelID.ToString() + "/" + modelID.ToString()) != null)
                return true;
 
            return false;
        }

        /// <summary>
        /// Import gameobject from Resources
        /// </summary>
        static public GameObject LoadReplacementPrefab(uint modelID, Vector3 position, Transform parent, Quaternion rotation)
        {
            GameObject object3D = null;
            string name = modelID.ToString();
            string path = "Models/" + name + "/";

            // Assign the name according to the current season
            if (IsWinter())
            {
                if (Resources.Load(path + name + winterTag) != null)
                    name += winterTag;
            }

            // Import GameObject
            object3D = GameObject.Instantiate(Resources.Load(path + name) as GameObject);
            InstantiateCustomModel(object3D, ref position, parent, ref rotation, modelID.ToString());
            return object3D;
        }

        #endregion

        #region Flats

        /// <summary>
        /// Import the custom GameObject for billboard if available
        /// </summary>
        /// <param name="inDungeon">Fix position for dungeon models.</param>
        /// <returns>Returns the imported model or null.</returns>
        static public GameObject ImportCustomFlatGameobject (int archive, int record, Vector3 position, Transform parent, bool inDungeon = false)
        {
            // Check user settings
            if (!DaggerfallUnity.Settings.MeshAndTextureReplacement)
                return null;

            // Import Gameobject from Resources
            // This is useful to test models
            if (ReplacementFlatExist(archive, record)) 
                return LoadReplacementFlat(archive, record, position, parent, inDungeon);

            // Get model from disk
            string modelName = archive.ToString() + "_" + record.ToString();
            string path = Path.Combine(flatsPath, modelName + flatExtension);
            if (File.Exists(path))
            {
                // Get AssetBundle
                AssetBundle LoadedAssetBundle;
                if (TryGetAssetBundle(path, modelName, out LoadedAssetBundle))
                {
                    // Instantiate GameObject
                    GameObject go = GameObject.Instantiate(LoadedAssetBundle.LoadAsset<GameObject>(modelName));
                    LoadedAssetBundle.Unload(false);
                    InstantiateCustomFlat(go, ref position, parent, archive, record, inDungeon);
                    return go;
                }
            }

            // Get model from mods using load order
            Mod[] mods = ModManager.Instance.GetAllMods(true);
            for (int i = mods.Length; i-- > 0;)
            {
                if (mods[i].AssetBundle.Contains(modelName))
                {
                    GameObject go = mods[i].GetAsset<GameObject>(modelName, true);
                    if (go != null)
                    {
                        InstantiateCustomFlat(go, ref position, parent, archive, record, inDungeon);
                        return go;
                    }

                    Debug.LogError("Failed to import " + modelName + " from " + mods[i].Title + " as GameObject.");
                }
            }

            return null;
        }
        
        /// <summary>
        /// Check existence of gameobject for billboard in Resources
        /// </summary>
        /// <returns>Bool</returns>
        static public bool ReplacementFlatExist(int archive, int record)
        {
            if (DaggerfallUnity.Settings.MeshAndTextureReplacement //check .ini setting
                 && Resources.Load("Flats/" + archive.ToString() + "_" + record.ToString() + "/" + archive.ToString() + "_" + record.ToString()) != null)
                return true;
      
            return false;
        }
        
        /// <summary>
        /// Import gameobject from Resources
        /// </summary>
        static public GameObject LoadReplacementFlat(int archive, int record, Vector3 position, Transform parent, bool inDungeon = false)
        {
            GameObject object3D = GameObject.Instantiate(Resources.Load("Flats/" + archive.ToString() + "_" + record.ToString() + "/" + archive.ToString() + "_" + record.ToString()) as GameObject);
            InstantiateCustomFlat(object3D, ref position, parent, archive, record, inDungeon);
            return object3D;
        }

        /// <summary>
        /// Checks if dungeon flat should have a torch sound.
        /// </summary>
        static public bool HasTorchSound (int archive, int record)
        {  
            if (archive == TextureReader.LightsTextureArchive)
            {
                switch (record)
                {
                    case 0:
                    case 1:
                    case 6:
                    case 16:
                    case 17:
                    case 18:
                    case 19:
                    case 20:
                        return true;
                }
            }

            return false;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Check if is winter and we are in a location which supports winter models.
        /// </summary>
        static private bool IsWinter()
        {
            return ((GameManager.Instance.PlayerGPS.ClimateSettings.ClimateType != DaggerfallConnect.DFLocation.ClimateBaseType.Desert) 
                && (DaggerfallUnity.Instance.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter));
        }

        /// <summary>
        /// Try loading AssetBundle and check if it contains the GameObject.
        /// </summary>
        /// <param name="path">Location of the AssetBundle.</param>
        /// <param name="modelName">Name of GameObject.</param>
        /// <param name="assetBundle">Loaded AssetBundle.</param>
        /// <returns>True if AssetBundle is loaded.</returns>
        static private bool TryGetAssetBundle (string path, string modelName, out AssetBundle assetBundle)
        {
            var loadedAssetBundle = AssetBundle.LoadFromFile(path);
            if (loadedAssetBundle != null)
            {
                // Check if AssetBundle contain the model we are looking for
                if (loadedAssetBundle.Contains(modelName))
                {
                    assetBundle = loadedAssetBundle;
                    return true;
                }
                else
                    loadedAssetBundle.Unload(false);
            }

            Debug.LogError("Error with AssetBundle: " + path + " doesn't contain " + 
                modelName + " or is corrupted.");
            assetBundle = null;
            return false;
        }

        /// <summary>
        /// Assign parent, position, rotation and texture filtermode.
        /// </summary>
        static private void InstantiateCustomModel(GameObject go, ref Vector3 position, Transform parent, ref Quaternion rotation, string modelName)
        {
            // Update Position
            go.transform.parent = parent;
            go.transform.position = position;
            go.transform.rotation = rotation;

            // Finalise gameobject
            FinaliseCustomGameObject(ref go, modelName);
        }

        /// <summary>
        /// Assign parent, position, rotation and texture filtermode.
        /// </summary>
        static private void InstantiateCustomFlat(GameObject go, ref Vector3 position, Transform parent, int archive, int record, bool inDungeon)
        {
            // Update Position
            if (inDungeon)
                GetFixedPosition(ref position, archive, record);
            go.transform.parent = parent;
            go.transform.localPosition = position;

            // Assign a random rotation so that flats in group won't look all aligned.
            // We use a seed becuse we want the models to have the same
            // rotation every time the same location is loaded.
            if (go.GetComponent<FaceWall>() == null)
            {
                Random.InitState((int)position.x);
                go.transform.Rotate(0, Random.Range(0f, 360f), 0);
            }

            // Finalise gameobject
            FinaliseCustomGameObject(ref go, archive.ToString() + "_" + record.ToString());
        }

        /// <summary>
        /// Assign texture filtermode as user settings and check integrity of materials.
        /// Import textures from disk if available. This is for consistency when custom models
        /// use one or more vanilla textures and the user has installed a texture pack.
        /// Vanilla textures should follow the nomenclature (archive_record-frame.png) while unique
        /// textures should have unique names (MyModelPack_WoodChair2.png) to avoid involuntary replacements.
        /// This can also be used to import optional normal maps.
        /// </summary>
        /// <param name="object3D">Custom prefab</param>
        /// <param name="ModelName">ID of model or sprite to be replaced. Used for debugging</param>
        static private void FinaliseCustomGameObject(ref GameObject object3D, string ModelName)
        {
            // Get MeshRenderer
            MeshRenderer meshRenderer = object3D.GetComponent<MeshRenderer>();

            // Check all materials
            for (int i = 0; i < meshRenderer.materials.Length; i++)
            {
                if (meshRenderer.materials[i].mainTexture != null)
                {
                    // Get name of texture
                    string textureName = meshRenderer.materials[i].mainTexture.name;

                    // Use texture(s) from disk if available
                    // Albedo
                    if (TextureReplacement.CustomTextureExist(textureName))
                        meshRenderer.materials[i].mainTexture = TextureReplacement.LoadCustomTexture(textureName);
                    // Normal map
                    if (TextureReplacement.CustomNormalExist(textureName))
                    {
                        meshRenderer.materials[i].EnableKeyword("_NORMALMAP"); // Enable normal map in the shader if the original material doesn't have one
                        meshRenderer.materials[i].SetTexture("_BumpMap", TextureReplacement.LoadCustomNormal(textureName));
                    }
                    // Emission map
                    if (TextureReplacement.CustomEmissionExist(textureName))
                    {
                        meshRenderer.materials[i].EnableKeyword("_EMISSION"); // Enable emission map in the shader if the original material doesn't have one
                        meshRenderer.materials[i].SetTexture("_EmissionMap", TextureReplacement.LoadCustomEmission(textureName));
                    }
                    // MetallicGloss map
                    if (TextureReplacement.CustomMetallicGlossExist(textureName))
                    {
                        meshRenderer.materials[i].EnableKeyword("_METALLICGLOSSMAP"); // Enable metallicgloss map in the shader if the original material doesn't have one
                        meshRenderer.materials[i].SetTexture("_MetallicGlossMap", TextureReplacement.LoadCustomMetallicGloss(textureName));
                    }

                    // Assign filtermode
                    meshRenderer.materials[i].mainTexture.filterMode = (FilterMode)DaggerfallUnity.Settings.MainFilterMode;
                    if (meshRenderer.materials[i].GetTexture("_BumpMap") != null)
                        meshRenderer.materials[i].GetTexture("_BumpMap").filterMode = (FilterMode)DaggerfallUnity.Settings.MainFilterMode;
                    if (meshRenderer.materials[i].GetTexture("_EmissionMap") != null)
                        meshRenderer.materials[i].GetTexture("_EmissionMap").filterMode = (FilterMode)DaggerfallUnity.Settings.MainFilterMode;
                    if (meshRenderer.materials[i].GetTexture("_MetallicGlossMap") != null)
                        meshRenderer.materials[i].GetTexture("_MetallicGlossMap").filterMode = (FilterMode)DaggerfallUnity.Settings.MainFilterMode;
                }
                else
                    Debug.LogError("Custom model " + ModelName + " is missing a material or a texture");
            }
        }

        /// <summary>
        /// Fix position of dungeon gameobjects.
        /// </summary>
        /// <param name="position">localPosition</param>
        /// <param name="archive">Archive of billboard texture</param>
        /// <param name="record">Record of billboard texture</param>
        static private void GetFixedPosition (ref Vector3 position, int archive, int record)
        {
            // Get height
            int height = ImageReader.GetImageData("TEXTURE." + archive, record, createTexture:false).height;

            // Correct transform
            position.y -= height / 2 * MeshReader.GlobalScale;
        }

        #endregion

        #region Legacy Methods

        // This was used to import mesh and materials separately from Resources.
        // It might be useful again in the future.
        //
        //static public Mesh LoadReplacementModel(uint modelID, ref CachedMaterial[] cachedMaterialsOut)
        //{
        //    // Import mesh
        //    GameObject object3D = Resources.Load("Models/" + modelID.ToString() + "/" + modelID.ToString() + "_mesh") as GameObject;
        //
        //    // Import materials
        //    cachedMaterialsOut = new CachedMaterial[object3D.GetComponent<MeshRenderer>().sharedMaterials.Length];
        //
        //    string materialPath;
        //
        //    // If it's not winter or the model doesn't have a winter version, it loads default materials
        //    if ((DaggerfallUnity.Instance.WorldTime.Now.SeasonValue != DaggerfallDateTime.Seasons.Winter) || (Resources.Load("Models/" + modelID.ToString() + "/material_w_0") == null))
        //        materialPath = "/material_";
        //    // If it's winter and the model has a winter version, it loads winter materials
        //   else
        //        materialPath = "/material_w_";
        //
        //    for (int i = 0; i < cachedMaterialsOut.Length; i++)
        //    {
        //        if (Resources.Load("Models/" + modelID.ToString() + materialPath + i) != null)
        //        {
        //            cachedMaterialsOut[i].material = Resources.Load("Models/" + modelID.ToString() + materialPath + i) as Material;
        //            if (cachedMaterialsOut[i].material.mainTexture != null)
        //                cachedMaterialsOut[i].material.mainTexture.filterMode = (FilterMode)DaggerfallUnity.Settings.MainFilterMode; //assign texture filtermode as user settings
        //            else
        //                Debug.LogError("Custom model " + modelID + " is missing a texture");
        //        }
        //        else
        //            Debug.LogError("Custom model " + modelID + " is missing a material");
        //    }
        //
        //    return object3D.GetComponent<MeshFilter>().sharedMesh;
        //}
        //

        #endregion
    }
}