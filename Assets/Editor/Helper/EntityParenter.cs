﻿using UnityEditor;
using UnityEngine;

namespace Forgelight.Editor.Helper
{
    public class EntityParenter
    {
        private EntityParenter() {}

        public static void ParentSelection()
        {
            GameObject mainParent = new GameObject("Parented Forgelight Entities");
            GameObject lightParent = new GameObject("Forgelight Lights");
            GameObject objectParent = new GameObject("Forgelight Objects");

            lightParent.transform.SetParent(mainParent.transform, false);
            objectParent.transform.SetParent(mainParent.transform, false);

            //Calculate the origin for each parent, and check we have any objects.
            Vector3 lightCentroid = new Vector3();
            int lightCount = 0;
            Vector3 objectCentroid = new Vector3();
            int objectCount = 0;

            foreach (GameObject o in Selection.gameObjects)
            {
                if (o.hideFlags == HideFlags.NotEditable || o.hideFlags == HideFlags.DontSave)
                {
                    continue;
                }

                if (o.GetComponent<ZoneObject>() != null)
                {
                    objectCentroid += o.transform.position;
                    objectCount++;
                }

                else if (o.GetComponent<ZoneLight>() != null)
                {
                    lightCentroid += o.transform.position;
                    lightCount++;
                }
            }

            //If no entities were found, cancel any futher processing.
            if (lightCount == 0 && objectCount == 0)
            {
                Object.DestroyImmediate(mainParent);
                return;
            }

            if (lightCount == 0)
            {
                Object.DestroyImmediate(lightParent);
            }

            if (objectCount == 0)
            {
                Object.DestroyImmediate(objectParent);
            }

            Vector3 lightParentPos = lightCentroid / lightCount;
            Vector3 objectParentPos = objectCentroid/objectCount;

            lightParent.transform.position = lightParentPos;
            objectParent.transform.position = objectParentPos;
            mainParent.transform.position = (lightParentPos + objectParentPos) / 2;

            foreach (GameObject o in Selection.gameObjects)
            {
                if (o.hideFlags == HideFlags.NotEditable || o.hideFlags == HideFlags.DontSave)
                {
                    continue;
                }

                if (o.GetComponent<ZoneObject>() != null)
                {
                    o.transform.SetParent(objectParent.transform, true);
                }

                else if (o.GetComponent<ZoneLight>() != null)
                {
                    o.transform.SetParent(lightParent.transform, true);
                }
            }


        }
    }
}