﻿using System.Collections.Generic;
using System.IO;
using ForgelightInteg.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ForgelightInteg.Zone
{
    public class ZoneExporter
    {
        public void ExportZoneFile()
        {
            if (Forgelight.Instance.ZoneLoader.loadedZone != null)
            {
                var path = DialogUtils.SaveFile(
                    "Save zone file",
                    Forgelight.Instance.ZoneLoader.loadedZonePath,
                    Path.GetFileNameWithoutExtension(Forgelight.Instance.ZoneLoader.loadedZonePath),
                    "json");

                SaveZone(path);
            }
            else
            {
                DialogUtils.DisplayDialog("Cannot save zone",
                    "An existing zone file needs to be loaded first. Please import a zone file, then try again");
            }
        }

        private void SaveZone(string path)
        {
            JObject zoneData = Forgelight.Instance.ZoneLoader.loadedZone;

            Dictionary<string, List<ZoneObject>> actorInstances = new Dictionary<string, List<ZoneObject>>();

            //One final check to make sure we don't have duplicate ID's
            Forgelight.Instance.ZoneObjectFactory.ValidateObjectUIDs();

            foreach (ZoneObject zoneObject in Forgelight.Instance.ZoneObjectFactory.GetComponentsInChildren<ZoneObject>())
            {
                string actorDef = zoneObject.actorDefinition;
                if (!actorInstances.ContainsKey(actorDef))
                {
                    actorInstances.Add(actorDef, new List<ZoneObject>());
                }

                actorInstances[actorDef].Add(zoneObject);
            }

            JArray objects = new JArray();
            foreach (var actorInstanceList in actorInstances)
            {
                JObject objectElement = new JObject();

                objectElement.Add("actorDefinition", actorInstanceList.Key);
                objectElement.Add("renderDistance", actorInstanceList.Value[0].renderDistance);

                JArray instances = new JArray();

                foreach (ZoneObject zoneObject in actorInstanceList.Value)
                {
                    JObject instance = new JObject();

                    Vector3 rawPosition = zoneObject.transform.position;
                    JArray position = new JArray();
                    position.Add(-rawPosition.x);
                    position.Add(rawPosition.y);
                    position.Add(rawPosition.z);
                    position.Add(1);

                    Vector3 rawRotation = zoneObject.transform.rotation.eulerAngles;
                    JArray rotation = new JArray();
                    rotation.Add(rawRotation.y * Mathf.Deg2Rad);
                    rotation.Add(rawRotation.x * Mathf.Deg2Rad);
                    rotation.Add(rawRotation.z * Mathf.Deg2Rad);
                    rotation.Add(0);

                    Vector3 rawScale = zoneObject.transform.localScale;
                    JArray scale = new JArray();
                    scale.Add(-rawScale.x);
                    scale.Add(rawScale.y);
                    scale.Add(rawScale.z);
                    scale.Add(1);

                    instance.Add("position", position);
                    instance.Add("rotation", rotation);
                    instance.Add("scale", scale);

                    instance.Add("id", zoneObject.id);
                    instance.Add("unknownByte1", zoneObject.notCastShadows);
                    instance.Add("unknownFloat1", zoneObject.lodMultiplier);

                    instances.Add(instance);
                }

                objectElement.Add("instances", instances);

                objects.Add(objectElement);
            }

            zoneData["objects"] = objects;
            File.WriteAllText(@path, zoneData.ToString(Formatting.Indented, null));
        }
    }
}