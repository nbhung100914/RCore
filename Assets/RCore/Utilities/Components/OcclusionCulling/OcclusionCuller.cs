﻿/**
 * Author RadBear - nbhung71711 @gmail.com - 2019
 **/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RCore.Components
{
    public class OcclusionCuller : MonoBehaviour
    {
        #region Members

        private static OcclusionCuller mInstance;
        public static OcclusionCuller Instance
        {
            get
            {
                if (mInstance == null)
                    mInstance = FindObjectOfType<OcclusionCuller>();
                return mInstance;
            }
        }

        public bool active;
        public Camera mainCamera;
        public List<OcclusionCulledRenderer> culledRenderers = new List<OcclusionCulledRenderer>();
        public bool initialized;

        private Vector3 mLastCamPos;
        private Quaternion mLastCamRot;

        #endregion

        //=====================================

        #region MonoBehaviour

        private IEnumerator Start()
        {
            while (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera != null)
                    break;
                else
                    yield return null;
            }
            initialized = true;
        }

        private void OnDisable()
        {
            for (int i = 0; i < culledRenderers.Count; i++)
                culledRenderers[i].MakeVisible();
        }

#if UNITY_EDITOR
        [InspectorButton]
        private void OnValidate()
        {
            culledRenderers = new List<OcclusionCulledRenderer>();
            var targets = FindObjectsOfType<OcclusionCulledRenderer>();
            for (int i = 0; i < targets.Length; i++)
                culledRenderers.Add(targets[i]);

            if (!active)
                MakeVisibleAll();
            else
                CheckVisibleAll();
        }
#endif

        private void LateUpdate()
        {
            if (!initialized || !active)
                return;

            Check();
        }

        #endregion

        //=====================================

        #region Public

        public void Register(OcclusionCulledRenderer pObj)
        {
            if (!culledRenderers.Contains(pObj))
                culledRenderers.Add(pObj);
        }

        public void UnRegister(OcclusionCulledRenderer pObj)
        {
            if (culledRenderers.Contains(pObj))
                culledRenderers.Remove(pObj);
        }

        #endregion

        //=====================================

        #region Private

        private void Check()
        {
            if (mLastCamPos != mainCamera.transform.position || mLastCamRot != mainCamera.transform.rotation)
            {
                mLastCamPos = mainCamera.transform.position;
                mLastCamRot = mainCamera.transform.rotation;

                CheckVisibleAll();
            }
        }

        private void MakeVisibleAll()
        {
            for (int i = 0; i < culledRenderers.Count; i++)
            {
                var obj = culledRenderers[i];
                obj.MakeVisible();
#if UNITY_EDITOR
                obj.name = gameObject.name.Replace("_HIDE", "").Replace("_SHOW", "");
                obj.name += "_SHOW";
#endif
            }
        }

        private void CheckVisibleAll()
        {
            if (mainCamera == null)
                return;

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
            for (int i = 0; i < culledRenderers.Count; i++)
            {
                var obj = culledRenderers[i];
                if (obj.InSidePlanes(planes))
                {
                    obj.MakeVisible();
#if UNITY_EDITOR
                    obj.name = gameObject.name.Replace("_HIDE", "").Replace("_SHOW", "");
                    obj.name += "_SHOW";
#endif
                }
                else
                {
                    obj.MakeInvisible();
#if UNITY_EDITOR
                    obj.name = gameObject.name.Replace("_HIDE", "").Replace("_SHOW", "");
                    obj.name += "_HIDE";
#endif
                }
            }
        }

        #endregion
    }
}