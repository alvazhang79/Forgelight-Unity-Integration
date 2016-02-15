﻿using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace ForgelightInteg.Zone
{
    public class ZoneObjectFactory : MonoBehaviour
    {
        private List<long> usedIDs = new List<long>();
        private Dictionary<string, GameObject> cachedActors = new Dictionary<string, GameObject>();

        public GameObject CreateForgelightObject(string actorDefinition, Vector3 position, Quaternion rotation)
        {
            long randID = GenerateUID();

            return CreateForgelightObject(actorDefinition, position, rotation, Vector3.one, 1000, 1.0f, 0, randID);
        }

        public GameObject CreateForgelightObject(string actorDefinition, Vector3 position, Quaternion rotation, Vector3 scale, int renderDistance, float lodMultiplier, byte notCastShadows, long id)
        {
            GameObject baseActor;
            GameObject instance = null;

            if (!cachedActors.ContainsKey(actorDefinition))
            {
                baseActor = InitializeBaseActor(actorDefinition);

                instance = baseActor;
                cachedActors.Add(actorDefinition, baseActor);
            }
            else
            {
                baseActor = cachedActors[actorDefinition];

                if (baseActor != null)
                {
                    instance = Instantiate(baseActor) as GameObject;
                }
            }

            if (instance != null)
            {
                InitializeInstance(instance, position, rotation, scale, actorDefinition, renderDistance, lodMultiplier, notCastShadows, id);
            }

            return instance;
        }

        public void UpdateForgelightObject(ZoneObject forgeLightObject, string newActorDefinition)
        {
            GameObject baseActor = InitializeBaseActor(newActorDefinition);

            foreach (Transform child in forgeLightObject.transform)
            {
                forgeLightObject.DestroyObject(child.gameObject);
            }

            foreach (Transform child in baseActor.transform)
            {
                GameObject mesh = Instantiate(child.gameObject, child.position, child.rotation) as GameObject;
                mesh.transform.SetParent(forgeLightObject.transform, false);
            }

            forgeLightObject.name = baseActor.name;
        }

        private GameObject InitializeBaseActor(string actorDef)
        {
            //By default, the actor definitions are appended with the .adr extension.
            //TODO if we implement our own conversion for the models, we shouldn't need to add the "_LOD0" to the file name.
            string modelName = Path.GetFileNameWithoutExtension(actorDef) + "_LOD0";
            string baseModelDir = "Models";
            string modelPath = baseModelDir + "/" + modelName;

            var resourceObj = Resources.Load(modelPath);

            GameObject baseActor;
            Renderer baseActorRenderer;
            if (resourceObj != null)
            {
                baseActor = Instantiate(resourceObj) as GameObject;

                if (baseActor == null)
                {
                    return null;
                }

                baseActorRenderer = baseActor.GetComponentInChildren<Renderer>();

                if (baseActorRenderer == null)
                {
                    DestroyImmediate(baseActor);

                    Debug.LogWarning("Warning: Forgelight Object " + modelName + " Failed to instantiate. Ignoring object.");

                    return null;
                }

                //Set our shader to the specular material.
                //baseActorRenderer.sharedMaterial.shader = Shader.Find("Standard (Specular setup)");

                //Load our textures. This shouldn't be necessary with the correct directory layout.
                //object[] textures = Resources.LoadAll(baseModelDir);

                //object resourceAlbedo = null;
                //object resourceNormal = null;
                //object resourceSpecular = null;

                //if (textures != null)
                //{
                //    foreach (object file in textures)
                //    {
                //        Texture2D texture = file as Texture2D;
                //        if (texture != null)
                //        {
                //            if (texture.name.EndsWith("_C"))
                //            {
                //                resourceAlbedo = texture;
                //            }

                //            else if (texture.name.EndsWith("_N"))
                //            {
                //                resourceNormal = texture;
                //            }

                //            //else if (texture.name.Contains("_S"))
                //            //{
                //            //resourceSpecular = texture;
                //            //}
                //        }
                //    }
                //}

                //if (resourceAlbedo != null)
                //{
                //    baseActorRenderer.sharedMaterial.color = Color.white;
                //    baseActorRenderer.sharedMaterial.mainTexture = (Texture2D)resourceAlbedo;
                //}

                //else
                //{
                //    //Make us magenta to indicate that we are missing a texture.
                //    baseActorRenderer.sharedMaterial.color = Color.magenta;
                //}

                //if (resourceNormal != null)
                //{
                //    baseActorRenderer.sharedMaterial.SetTexture("_BumpMap", (Texture2D)resourceNormal);
                //}

                //if (resourceSpecular != null)
                //{
                //    baseActorRenderer.sharedMaterial.SetTexture("_SpecGlossMap", (Texture2D)resourceSpecular);
                //}
            }

            else
            {
                //This model does not exist.
                baseActor = GameObject.CreatePrimitive(PrimitiveType.Cube);

                baseActorRenderer = baseActor.GetComponent<Renderer>();
                baseActorRenderer.sharedMaterial.color = Color.magenta;
            }

            return baseActor;
        }

        private void InitializeInstance(GameObject instance, Vector3 position, Quaternion rotation, Vector3 scale, string actorDef, int renderDistance, float unknownFloat1, byte unknownByte1, long id)
        {
            //Set our position, scale and rotation values to the ones defined in the zone file.
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.transform.localScale = scale;

            //Attach ourselves to the master object parent.
            instance.transform.parent = transform;

            //Attach our ZoneObject script, and update its variables
            ZoneObject zoneObject = instance.GetComponent<ZoneObject>();
            if (zoneObject == null)
            {
                zoneObject = instance.AddComponent<ZoneObject>();
            }

            zoneObject.actorDefinition = actorDef;
            zoneObject.renderDistance = renderDistance;
            zoneObject.lodMultiplier = unknownFloat1;
            zoneObject.notCastShadows = unknownByte1;
            zoneObject.id = id;

            //Add the ID to our used list.
            usedIDs.Add(id);

            //Hide the gameobject. It will be made visible when we are in render range.
            zoneObject.Hide();
        }

        public void ValidateObjectUIDs()
        {
            //This list may not be updated. We create a new one.
            usedIDs.Clear();

            foreach (ZoneObject zoneObject in GetComponentsInChildren<ZoneObject>())
            {
                if (usedIDs.Contains(zoneObject.id))
                {
                    zoneObject.id = GenerateUID();
                }

                usedIDs.Add(zoneObject.id);
            }
        }

        private long GenerateUID()
        {
            long randID;

            do
            {
                randID = Random.Range(0, int.MaxValue);
            }
            while (usedIDs.Contains(randID));

            return randID;
        }
    }

}