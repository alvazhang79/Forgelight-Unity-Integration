﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Forgelight.Formats.Cnk;
using Forgelight.Formats.Dma;
using Forgelight.Formats.Dme;
using UnityEditor;
using Asset = Forgelight.Pack.Asset;
using MathUtils = Forgelight.Utils.MathUtils;

namespace Forgelight
{
    public class ForgelightGame
    {
        //Info
        public string Name { get; private set; }
        public string PackDirectory { get; private set; }
        public string ResourceDirectory { get; private set; }

        //Available Assets
        public List<string> AvailableActors { get; private set; }
        public Dictionary<string, Formats.Zone.Zone> AvailableZones { get; private set; }

        //Data
        public List<Pack.Pack> Packs { get; private set; }
        public Dictionary<Asset.Types, List<Asset>> AssetsByType { get; private set; }
        public MaterialDefinitionManager MaterialDefinitionManager { get; private set; }

        // Internal cache to check whether a pack has already been loaded
        private Dictionary<Int32, Pack.Pack> packLookupCache = new Dictionary<Int32, Pack.Pack>();

        public ForgelightGame(string name, string packDirectory, string resourceDirectory)
        {
            Name = name;
            PackDirectory = packDirectory;
            ResourceDirectory = resourceDirectory;

            AvailableActors = new List<string>();
            AvailableZones = new Dictionary<string, Formats.Zone.Zone>();

            Packs = new List<Pack.Pack>();
            AssetsByType = new Dictionary<Asset.Types, List<Asset>>();
        }

        public void LoadPack(string path)
        {
            Pack.Pack pack = null;

            if (packLookupCache.TryGetValue(path.GetHashCode(), out pack) == false)
            {
                pack = Pack.Pack.LoadBinary(path);

                if (pack != null)
                {
                    packLookupCache.Add(path.GetHashCode(), pack);
                    Packs.Add(pack);

                    foreach (Asset asset in pack.Assets)
                    {
                        if (!AssetsByType.ContainsKey(asset.Type))
                        {
                            AssetsByType.Add(asset.Type, new List<Asset>());
                        }

                        AssetsByType[asset.Type].Add(asset);
                    }
                }
            }
        }

        public MemoryStream CreateAssetMemoryStreamByName(String name)
        {
            MemoryStream memoryStream = null;

            foreach (Pack.Pack pack in Packs)
            {
                memoryStream = pack.CreateAssetMemoryStreamByName(name);

                if (memoryStream != null)
                {
                    break;
                }
            }

            return memoryStream;
        }

        private void ProgressBar(float progress, string currentTask)
        {
            EditorUtility.DisplayProgressBar("Forgelight - " + Name, currentTask, progress);
        }

        public void OnLoadComplete()
        {
            EditorUtility.ClearProgressBar();
        }

        public void LoadPackFiles(float progress0, float progress100)
        {
            String[] files = Directory.GetFiles(PackDirectory, "*.pack");

            //Load Pack files into AssetManager.
            ProgressBar(progress0, "Loading Pack Data...");

            for (int i = 0; i < files.Length; ++i)
            {
                ProgressBar(MathUtils.RemapProgress((float)i / (float)files.Length, progress0, progress100), "Loading Pack File: " + Path.GetFileName(files[i]));
                LoadPack(files[i]);
            }
        }

        public void InitializeMaterialDefinitionManager()
        {
            MaterialDefinitionManager = new MaterialDefinitionManager(this);
        }

        public void UpdateActors(float progress0, float progress100)
        {
            AvailableActors.Clear();
            ProgressBar(progress0, "Updating Actors List...");

            List<Asset> actors = AssetsByType[Asset.Types.ADR];

            for (int i = 0; i < actors.Count; ++i)
            {
                ProgressBar(MathUtils.RemapProgress((float)i / (float)actors.Count, progress0, progress100), "Updating Actors List...");
                AvailableActors.Add(actors[i].Name);
            }

            AvailableActors.Sort();
        }

        public void UpdateZones(float progress0, float progress100)
        {
            AvailableZones.Clear();
            ProgressBar(progress0, "Updating Zones...");

            List<Asset> zones = AssetsByType[Asset.Types.ZONE];

            for (int i = 0; i < zones.Count; ++i)
            {
                ProgressBar(MathUtils.RemapProgress((float)i / (float)zones.Count, progress0, progress100), "Updating Zone: " + zones[i].Name);

                MemoryStream memoryStream = zones[i].Pack.CreateAssetMemoryStreamByName(zones[i].Name);
                Formats.Zone.Zone zone = Formats.Zone.Zone.LoadFromStream(zones[i].Name, memoryStream);

                string rawZoneName = Path.GetFileNameWithoutExtension(zones[i].Name);
                string zoneName = rawZoneName;

                if (AvailableZones.ContainsKey(zoneName))
                {
                    zoneName = rawZoneName +  " (" + zones[i].Pack.Name + ")";
                }

                AvailableZones[zoneName] = zone;
            }
        }

        public void ExportModels(float progress0, float progress100)
        {
            ProgressBar(progress0, "Exporting Models...");

            List<Asset> modelAssets = AssetsByType[Asset.Types.DME];
            int modelsProcessed = 0;
            string lastAssetProcessed = "";

            BackgroundWorker backgroundWorker = Parallel.AsyncForEach(modelAssets, asset =>
            {
                //Ignore auto-generated LOD's and Don't export if the file already exists.
                if (!asset.Name.EndsWith("Auto.dme") && !File.Exists(ResourceDirectory + "/Models/" + Path.GetFileNameWithoutExtension(asset.Name) + ".obj"))
                {
                    using (MemoryStream modelMemoryStream = asset.Pack.CreateAssetMemoryStreamByName(asset.Name))
                    {
                        Model model = Model.LoadFromStream(asset.Name, modelMemoryStream);

                        if (model != null)
                        {
                            ModelExporter.ExportModel(this, model, ResourceDirectory + "/Models");
                        }
                    }

                    lastAssetProcessed = asset.Name;
                }

                Interlocked.Increment(ref modelsProcessed);
            });

            while (modelsProcessed < modelAssets.Count && backgroundWorker.IsBusy)
            {
                lock (lastAssetProcessed)
                {
                    ProgressBar(MathUtils.RemapProgress((float)modelsProcessed / (float)modelAssets.Count, progress0, progress100), "Exporting Model: " + lastAssetProcessed);
                }
            }

            backgroundWorker.Dispose();
        }

        //TODO Less Code Duplication.
        //TODO Update CNK0 Parsing. The current format seems to be incorrect.
        //TODO Make Progress Bars more verbose.
        public void ExportTerrain(float progress0, float progress100)
        {
            int chunksProcessed = 0;
            string lastAssetProcessed = "";

            //CNK0
            //ProgressBar(progress0, "Exporting Terrain Data (LOD 0)...");
            //List<Asset> terrainAssetsCnk0 = AssetsByType[Asset.Types.CNK0];
            //int terrainAssetsCnk0Processed = 0;

            //foreach (Asset asset in terrainAssetsCnk0)
            //{
            //    using (MemoryStream terrainMemoryStream = asset.Pack.CreateAssetMemoryStreamByName(asset.Name))
            //    {
            //        Cnk0 chunk = Cnk0.LoadFromStream(asset.Name, terrainMemoryStream);
            //    }
            //}

            //Parallel.ForEach(terrainAssetsCnk0, asset =>
            //{
            //    using (MemoryStream terrainMemoryStream = asset.Pack.CreateAssetMemoryStreamByName(asset.Name))
            //    {
            //        Cnk0 chunk = Cnk0.LoadFromStream(asset.Name, terrainMemoryStream);
            //    }

            //    Interlocked.Increment(ref terrainAssetsCnk0Processed);
            //    //ProgressBar(MathUtils.RemapProgress((float)terrainAssetsCnk0Processed / (float)terrainAssetsCnk0.Count, progress0, progress100), "Exporting Chunk (LOD0): " + Path.GetFileName(asset.Name));
            //});

            //CNK1
            List<Asset> terrainAssetsCnk1 = AssetsByType[Asset.Types.CNK1];
            chunksProcessed = 0;

            BackgroundWorker backgroundWorker = Parallel.AsyncForEach(terrainAssetsCnk1, asset =>
            {
                using (MemoryStream terrainMemoryStream = asset.Pack.CreateAssetMemoryStreamByName(asset.Name))
                {
                    CnkLOD chunk = CnkLOD.LoadFromStream(asset.Name, terrainMemoryStream);

                    ChunkExporter.ExportChunk(this, chunk, ResourceDirectory + "/Terrain");
                    ChunkExporter.ExportTextures(this, chunk, ResourceDirectory + "/Terrain");

                    Interlocked.Increment(ref chunksProcessed);
                    lastAssetProcessed = chunk.Name;
                }
            });

            while (chunksProcessed < terrainAssetsCnk1.Count && backgroundWorker.IsBusy)
            {
                lock (lastAssetProcessed)
                {
                    ProgressBar(MathUtils.RemapProgress((float)chunksProcessed / (float)terrainAssetsCnk1.Count, progress0, progress100), "Exporting Chunk: " + lastAssetProcessed);
                }
            }

            backgroundWorker.Dispose();

            ////CNK2
            //ProgressBar(progress0 + MathUtils.RemapProgress(0.50f, progress0, progress100), "Exporting Terrain Data (LOD 2)...");
            //List<Asset> terrainAssetsCnk2 = AssetsByType[Asset.Types.CNK2];
            //int terrainAssetsCnk2Processed = 0;

            //Parallel.ForEach(terrainAssetsCnk2, asset =>
            //{
            //    using (MemoryStream terrainMemoryStream = asset.Pack.CreateAssetMemoryStreamByName(asset.Name))
            //    {
            //        CnkLOD chunk = CnkLOD.LoadFromStream(asset.Name, terrainMemoryStream);
            //    }

            //    Interlocked.Increment(ref terrainAssetsCnk2Processed);
            //    //ProgressBar(MathUtils.RemapProgress((float)terrainAssetsCnk2Processed / (float)terrainAssetsCnk2.Count, progress0, progress100), "Exporting Chunk (LOD2): " + Path.GetFileName(asset.Name));
            //});

            ////CNK3
            //ProgressBar(progress0 + MathUtils.RemapProgress(0.75f, progress0, progress100), "Exporting Terrain Data (LOD 3)...");
            //List<Asset> terrainAssetsCnk3 = AssetsByType[Asset.Types.CNK3];
            //int terrainAssetsCnk3Processed = 0;

            //Parallel.ForEach(terrainAssetsCnk3, asset =>
            //{
            //    using (MemoryStream terrainMemoryStream = asset.Pack.CreateAssetMemoryStreamByName(asset.Name))
            //    {
            //        CnkLOD chunk = CnkLOD.LoadFromStream(asset.Name, terrainMemoryStream);
            //    }

            //    Interlocked.Increment(ref terrainAssetsCnk3Processed);
            //    //ProgressBar(MathUtils.RemapProgress((float)terrainAssetsCnk3Processed / (float)terrainAssetsCnk3.Count, progress0, progress100), "Exporting Chunk (LOD3): " + Path.GetFileName(asset.Name));
            //});
        }
    }
}
